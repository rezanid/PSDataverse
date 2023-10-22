namespace PSDataverse.Dataverse;

using System;
using System.Runtime.Serialization;
using PSDataverse.Dataverse.Model;

[Serializable]
public class BatchException<T> : Exception
{
    public Batch<T> Batch { get; set; }
    public Guid CorrelationId { get; set; }
    public BatchException() { }
    public BatchException(string message) : base(message) { }
    public BatchException(string message, Exception inner) : base(message, inner) { }
    protected BatchException(
      SerializationInfo info,
      StreamingContext context) : base(info, context)
    {
        //To add read Batch as a custom type: Batch = (Batch<T>)info.GetValue("Batch", typeof(Batch<T>));
        Batch = Batch<T>.Parse(info.GetString("Batch"));
        _ = Guid.TryParse(info.GetString("CorrelationId"), out var correlationId);
        CorrelationId = correlationId;
    }
    //[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        if (info == null)
        { throw new ArgumentNullException(nameof(info)); }
        // To add batch as a custom type: info.AddValue("Batch", Batch, typeof(Batch<T>))
        info.AddValue("Batch", Batch.ToJsonCompressedBase64(System.IO.Compression.CompressionLevel.Optimal));
        info.AddValue("CorrelationId", CorrelationId.ToString());
        base.GetObjectData(info, context);
    }
}
