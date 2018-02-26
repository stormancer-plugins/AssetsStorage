using MsgPack.Serialization;

namespace Stormancer.Server.AssetsStorage
{
    public class BranchDto
    {
        [MessagePackMember(0)]
        public string BranchName { get; set; }
        [MessagePackMember(1)]
        public string BranchPath { get; set; }                
    }
}
