using System;

namespace Stormancer.Server.AssetsStorage
{
    public class Metafile
    {
        // Id == file GUID
        public string Id { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Filename { get; set; }        
        public string Path { get; set; }
        public string BranchId { get; set; }  
        public string MD5Hash { get; set; }
        public string ContentMD5Hash { get; set; }
    }
}
