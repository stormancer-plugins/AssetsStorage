using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.AssetsStorage
{
    public class ElasticsearchException : Exception
    {
        public ElasticsearchException() : base() { }
        public ElasticsearchException(string message) : base(message) { }
        public ElasticsearchException(string message, System.Exception inner) : base(message, inner) { }
    }
}
