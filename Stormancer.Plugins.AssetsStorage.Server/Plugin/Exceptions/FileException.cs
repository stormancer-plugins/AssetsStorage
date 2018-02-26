using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.AssetsStorage
{
    public class FileException : System.Exception
    {
        public FileException() : base() { }
        public FileException(string message) : base(message) { }
        public FileException(string message, System.Exception inner) : base(message, inner) { }        
    }
}
