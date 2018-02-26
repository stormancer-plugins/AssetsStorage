using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.AssetsStorage
{
    public class Branch
    {
        // Id Used by elasticsearch
        public string Id { get; set; }
        public string ParentBranchId { get; set; }
        public string Path { get; set; }
        public List<MetafileHead> MetafilesHead { get; set; } = new List<MetafileHead>();
    }
}
