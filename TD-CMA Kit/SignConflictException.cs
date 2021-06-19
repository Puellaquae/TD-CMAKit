using System;

namespace TD_CMAKit
{
    [Serializable]
    public class SignConflictException : Exception
    {
        public SignConflictException() { }
        public SignConflictException(string message) : base(message) { }
        public SignConflictException(string message, Exception inner) : base(message, inner) { }
        protected SignConflictException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
