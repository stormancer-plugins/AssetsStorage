using MsgPack.Serialization;

namespace Stormancer.Server.AssetsStorage
{
    public class MetafileDto
    {
        [MessagePackMember(0)]
        public string FileName { get; set; }
        [MessagePackMember(1)]
        public string URL { get; set; }
        [MessagePackMember(2)]
        public string Path { get; set; }
        [MessagePackMember(3)]
        public string MD5Hash { get; set; }
        [MessagePackMember(4)]
        public string ContentMD5Hash { get; set; }
    }
}
