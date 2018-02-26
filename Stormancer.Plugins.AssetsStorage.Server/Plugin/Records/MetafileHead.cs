namespace Stormancer.Server.AssetsStorage
{
    public class MetafileHead
    {
        // Id == BranchName_path_Head
        public string Id { get; set; }
        // HeadFile == GUID of the head file
        public string HeadFile { get; set; }
        // Disable File
        public bool IsActive { get; set; }  
        // Store the file data
        public Metafile Record { get; set; }      
    }
}
