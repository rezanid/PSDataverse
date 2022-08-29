using Newtonsoft.Json.Linq;
using System;
using System.Runtime.Serialization;

namespace PSDataverse.Dataverse.Model
{
    [Serializable]
    public class OperationException : Exception
    {
        public OperationError Error { get; set; }
        public string EntityName { get; set; }
        public string BatchId { get; set; }
        public Guid CorrelationId { get; set; }
        public OperationException() { }
        public OperationException(string message) : base(message) { }
        public OperationException(string message, Exception inner) : base(message, inner) { }
        public OperationException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class OperationException<T> : OperationException
    {
        public Operation<T> Operation { get; set; }
        public OperationException() { }
        public OperationException(string message) : base(message) { }
        public OperationException(string message, Exception inner) : base(message, inner) { }
        protected OperationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            Operation = (Operation<T>)info.GetValue("Operation", typeof(Operation<T>));
            Error = (OperationError)info.GetValue("Error", typeof(OperationError));
            EntityName = info.GetString("EntityName");
            BatchId = info.GetString("BatchId");
            CorrelationId = (Guid)info.GetValue("CorrelationId", typeof(Guid));
        }

        //[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) { throw new ArgumentNullException(nameof(info)); }
            info.AddValue("Operation", Operation, typeof(Operation<T>));
            info.AddValue("Error", Error, typeof(OperationError));
            info.AddValue("EntityName", EntityName);
            info.AddValue("BatchId", BatchId);
            info.AddValue("CorrelationId", CorrelationId);
            base.GetObjectData(info, context);
        }
    }
}
