namespace GoogleDrivePlugin.Controls.FileExplorer
{
    public class PageInfo
    {
        public PageInfo(string folderID, string pageToken = null)
        {
            this.FolderID = folderID;
            this.PageToken = pageToken;
        }

        public string FolderID { get; }

        public string PageToken { get; }
    }
}
