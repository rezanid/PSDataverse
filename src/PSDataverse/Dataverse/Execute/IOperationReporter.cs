namespace PSDataverse.Dataverse.Execute;
using System.Management.Automation;

public interface IOperationReporter
{
    void WriteError(ErrorRecord errorRecord);
    void WriteInformation(string messageData, string[] tags);
    void WriteObject(object obj);
}
