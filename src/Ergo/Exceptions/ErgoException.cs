using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Ergo.Exceptions
{
    public abstract class ErgoException : Exception
    {
        public ErgoException()
        {
        }

        public ErgoException(string message) : base(message)
        {
        }

        public ErgoException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ErgoException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    public class ErgolParserException : ErgoException
    {
        public ErgolParserException(string message)
            : base(message)
        {

        }
    }
}
