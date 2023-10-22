namespace PSDataverse.Dataverse;

using System;
using System.Runtime.Serialization;

[Serializable]
internal class ParseException : Exception
{
    public ParseException() { }

    public ParseException(string message) : base(message) { }

    public ParseException(string message, Exception innerException) : base(message, innerException) { }

    protected ParseException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
