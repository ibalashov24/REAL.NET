using System;
using System.Collections.Generic;

namespace GoogleDrivePlugin.Model
{
    /// <summary>
    /// Contains file list of some folder on Drive
    /// </summary>
    public class FileListArgs : EventArgs
    {
        /// <summary>
        /// Initializes new instance of FileListArgs
        /// </summary>
        /// <param name="parentID">Path to data in the Drive</param>
        /// <param name="itemInfo">Pair [itemName, isDirectory flag] </param>
        public FileListArgs(
            string parentID, 
            List<ItemMetaInfo> itemInfo, 
            string pageToken, 
            string nextPageToken = null)
        {
            this.FolderID = parentID;
            this.FileList = itemInfo;
            this.PageToken = pageToken;
            this.NextPageToken = nextPageToken;
        }

        /// <summary>
        /// Google Drive ID of folder
        /// </summary>
        public string FolderID { get; }

        /// <summary>
        /// List of files in folder with FolderID
        /// </summary>
        public List<ItemMetaInfo> FileList { get; }

        /// <summary>
        /// Token of current page of files in current folder
        /// </summary>
        public string PageToken { get; }

        /// <summary>
        /// Token of the next page of files in current folder.
        /// Null if it is the last page
        /// </summary>
        public string NextPageToken { get; }
    }
}
