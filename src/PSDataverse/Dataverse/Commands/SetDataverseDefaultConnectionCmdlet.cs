namespace PSDataverse;

using System;
using System.Collections.Generic;
using System.Management.Automation;

[Cmdlet(VerbsCommon.Set, "DataverseDefaultConnection")]
[OutputType(typeof(DataverseConnection))]
public sealed class SetDataverseDefaultConnectionCmdlet : DataverseCmdlet
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string Name { get; set; }

    [Parameter]
    public SwitchParameter PassThru { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            var connection = GetConnectionRegistry(create: false)?.SetDefault(Name)
                ?? throw new KeyNotFoundException($"No Dataverse connection named '{Name}' exists.");
            if (PassThru)
            {
                WriteObject(connection);
            }
        }
        catch (KeyNotFoundException exception)
        {
            ThrowTerminatingError(new ErrorRecord(
                exception,
                Globals.ErrorIdConnectionNotFound,
                ErrorCategory.ObjectNotFound,
                Name));
        }
    }
}
