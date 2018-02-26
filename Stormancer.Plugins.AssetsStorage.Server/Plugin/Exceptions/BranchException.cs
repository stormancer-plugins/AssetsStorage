using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.AssetsStorage
{
    public class BranchException : System.Exception
    {
        public BranchException() : base() { }
        public BranchException(string message) : base(message) { }
        public BranchException(string message, System.Exception inner) : base(message, inner) { }        
    }
}
