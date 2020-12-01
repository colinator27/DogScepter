using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Util
{
    [Serializable]
    internal class GMException : Exception
    {
        public GMException()
        {
        }

        public GMException(string message) : base(message)
        {
        }

        public GMException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
