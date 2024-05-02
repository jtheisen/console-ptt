using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ptt
{
    public class AssertionException : Exception
    {
        public AssertionException(String message)
            : base("Internal error: " + message) { }
    }
}
