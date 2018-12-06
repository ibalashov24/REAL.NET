﻿namespace GoogleDrivePlugin.Model
{
    using System;

    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Google.Apis.Oauth2.v2;
    using Google.Apis.Auth.OAuth2;
    using Google.Apis.Drive.v3;

    using Model.ClientSecret;

    using EditorPluginInterfaces;

    public class GoogleDriveModel
    {
        public static string RootFolderName = "root";

        public delegate void OperationHandler(object sender, OperationProgressArgs info);
        public event OperationHandler ImportWindowStatusChanged;
        public event OperationHandler ExportWindowStatusChanged;

        public delegate void FileListHandler(object sender, FileListArgs args);
        public event FileListHandler FileListReceived;

        private DriveService drive;
        private UserCredential userCredentials;
        private string username;

        private IModel editorModel;

        private const string ApplicationName = "REALNET-GoogleDrivePlugin";

        public GoogleDriveModel(IModel editorModel)
        {
            this.editorModel = editorModel;
        }

        public async Task LogUserOut()
        {
            this.RequestDownloadHide();
            this.RequestUploadHide();

            await this.userCredentials.RevokeTokenAsync(CancellationToken.None);
            this.userCredentials = null;
        }

        public async Task RequestUpload()
        {
            if (this.userCredentials == null)
            {
                try
                {
                    await this.InitiateNewSessionWithDrive();
                }
                catch (Google.Apis.Auth.OAuth2.Responses.TokenResponseException)
                {
                    return;
                }
            }

            this.ExportWindowStatusChanged?.Invoke(
                this, 
                new OperationProgressArgs(OperationType.OpenWindow, this.username));
        }

        public void RequestUploadHide()
        {
            this.ExportWindowStatusChanged?.Invoke(
                this, 
                new OperationProgressArgs(OperationType.CloseWindow, this.username));
        }

        public async Task RequestDownload()
        {
            if (this.userCredentials == null)
            {
                try
                {
                    await this.InitiateNewSessionWithDrive();
                }
                catch (Google.Apis.Auth.OAuth2.Responses.TokenResponseException)
                {
                    return;
                }
            }

            this.ImportWindowStatusChanged?.Invoke(
                this, 
                new OperationProgressArgs(OperationType.OpenWindow, this.username));
        }
        
        public void RequestDownloadHide()
        {
            this.ImportWindowStatusChanged?.Invoke(
                this, 
                new OperationProgressArgs(OperationType.CloseWindow, this.username));
        }

        public async void CreateNewFile(string parentID, string fileName, string mimeType = null)
        {
            var newEmptyFile = new Google.Apis.Drive.v3.Data.File();
            newEmptyFile.Name = fileName;
            if (mimeType != null)
            {
                newEmptyFile.MimeType = mimeType;
            }

            if (parentID != RootFolderName)
            {
                newEmptyFile.Parents = new List<string>() { parentID };
            }

            var request = this.drive.Files.Create(newEmptyFile);
            await request.ExecuteAsync();

            await this.RequestFolderContent(parentID);
        }

        public void CreateNewFolder(string parentID, string folderName)
        {
            this.CreateNewFile(parentID, folderName, "application/vnd.google-apps.folder");
        }

        public async void DeleteElement(string parentID, string itemID)
        {
            var deleteRequest = this.drive.Files.Delete(itemID);
            await deleteRequest.ExecuteAsync();

            await this.RequestFolderContent(parentID);
        }

        public async void MoveElement(string sourceFolderID, string itemID, string destFolderID)
        {
            var movementRequest = 
                this.drive.Files.Update(new Google.Apis.Drive.v3.Data.File(), itemID);

            movementRequest.Fields = "id, parents";
            movementRequest.AddParents = destFolderID;
            movementRequest.RemoveParents = sourceFolderID;

            await movementRequest.ExecuteAsync();

            await this.RequestFolderContent(sourceFolderID);
        }

        public async void SaveCurrentModelTo(string parentID, string fileID)
        {
            var tempFilePath = Path.GetTempFileName();
            this.editorModel.Repo.Save(tempFilePath);

            Google.Apis.Upload.IUploadProgress progress;
            using (var stream = File.OpenRead(tempFilePath))
            {
                var uploadRequest = this.drive.Files.Update(
                    new Google.Apis.Drive.v3.Data.File(),
                    fileID,
                    stream,
                    "application/json");

                progress = await uploadRequest.UploadAsync();
            }

            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                this.ExportWindowStatusChanged?.Invoke(
                    this,
                    new OperationProgressArgs(
                        OperationType.Error, 
                        $"Failed to upload model: { progress.Exception.Message }"));
            }
            else
            {
                this.RequestUploadHide();
            }
            
            await this.RequestFolderContent(parentID);
        }

        public async void LoadModelFrom(string fileID)
        {
            var tempFilePath = Path.GetTempFileName();

            Google.Apis.Download.IDownloadProgress progress;
            using (var stream = new FileStream(tempFilePath, FileMode.Open))
            {
                var downloadRequest = this.drive.Files.Get(fileID);
                progress = await downloadRequest.DownloadAsync(stream);
            }

            if (progress.Status == Google.Apis.Download.DownloadStatus.Failed)
            {
                this.ImportWindowStatusChanged?.Invoke(
                    this,
                    new OperationProgressArgs(
                        OperationType.Error,
                        $"Failed to download model: { progress.Exception.Message }"));
                return;
            }

            try
            {
                this.editorModel.Open(tempFilePath);
            }
            // Because we do not know exactly which exception should be caught
            catch (Exception)
            {
                this.ImportWindowStatusChanged?.Invoke(
                    this,
                    new OperationProgressArgs(
                        OperationType.Error, 
                        "Selected file contains corrupted model"));
                return;
            }

            this.RequestDownloadHide();
        }

        public async Task RequestFolderContent(string folderID)
        {
            var request = this.drive.Files.List();

            request.Spaces = "drive";
            request.Fields = "files(id, name, size, mimeType)";
            request.OrderBy = "folder";
            request.Q = $"'{(folderID == RootFolderName ? "root" : folderID)}' in parents";

            var response = await request.ExecuteAsync();

            var itemList = new List<FileMetaInfo>();
            foreach (var item in response.Files)
            {
                var isFolder = item.MimeType == "application/vnd.google-apps.folder";
                var itemSize = isFolder ? null : GetPrettySize((long)item.Size);
                var fileInfo = new FileMetaInfo(item.Id, item.Name, itemSize, isFolder);

                itemList.Add(fileInfo);
            }

            this.FileListReceived?.Invoke(this, new FileListArgs(folderID, itemList));
        }

        private async Task InitiateNewSessionWithDrive()
        {
            this.userCredentials = await AuthorizeUser();
            this.username = await GetUserInfo(this.userCredentials);
            this.drive = InitDriveService(this.userCredentials);
        }
        
        private static async Task<UserCredential> AuthorizeUser()
        {
            var scopes = new[] 
            {
                Oauth2Service.Scope.UserinfoProfile,
                Oauth2Service.Scope.UserinfoEmail,
                DriveService.Scope.Drive
            };

            return await GoogleWebAuthorizationBroker.AuthorizeAsync(
                new ClientSecrets
                {
                    ClientId = ClientSecretParams.ClientID,
                    ClientSecret = ClientSecretParams.ClientSecret
                },
                scopes,
                "PluginUser",
                CancellationToken.None);
        }

        private static async Task<string> GetUserInfo(UserCredential credential)
        {
            var service = new Oauth2Service(
                new Google.Apis.Services.BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName
                });

            var info = await service.Userinfo.Get().ExecuteAsync();
            return info.Email;
        }

        private static DriveService InitDriveService(UserCredential credential)
        {
            return new DriveService(
                new Google.Apis.Services.BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName
                });
        }

        private static string GetPrettySize(long sizeInBytes)
        {
            if (sizeInBytes >= 0x1000000000000000)
            {
                return $"{(sizeInBytes >> 60)}EB";
            }
            else if (sizeInBytes >= 0x4000000000000)
            {
                return $"{(sizeInBytes >> 50)}PB";
            }
            else if (sizeInBytes >= 0x10000000000)
            {
                return $"{(sizeInBytes >> 40)}TB";
            }
            else if (sizeInBytes >= 0x40000000)
            {
                return $"{(sizeInBytes >> 30)}GB";
            }
            else if (sizeInBytes >= 0x100000)
            {
                return $"{(sizeInBytes >> 20)}MB";
            }
            else if (sizeInBytes >= 0x400) 
            {
                return $"{(sizeInBytes >> 10)}KB";
            }
            else
            {
                return $"{sizeInBytes}B";
            }
        }
    }
}
