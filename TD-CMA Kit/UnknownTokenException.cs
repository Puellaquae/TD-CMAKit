using System;

namespace TD_CMAKit
{
    [Serializable]
    public class UnknownTokenException : Exception
    {
        public UnknownTokenException() { }
        public UnknownTokenException(string message) : base(message) { }
        public UnknownTokenException(string message, Exception inner) : base(message, inner) { }
        protected UnknownTokenException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
