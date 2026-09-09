namespace PSDataverse.Dataverse.Commands;

using System;
using System.Management.Automation;

[Cmdlet(VerbsCommunications.Disconnect, "Dataverse", SupportsShouldProcess = true, DefaultParameterSetName = "Name")]
public sealed class DisconnectDataverseCmdlet : DataverseCmdlet
{
    [Parameter(Position = 0, ParameterSetName = "Name", ValueFromPipelineByPropertyName = true)]
    public string Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "All")]
    public SwitchParameter All { get; set; }

    protected override void ProcessRecord()
    {
        var registry = GetConnectionRegistry(create: false);
        var disconnected = false;
        if (All)
        {
            if (registry is not null && ShouldProcess("all Dataverse connections", "Disconnect"))
            {
                registry.Dispose();
                SessionState.PSVariable.Remove(Globals.VariableNameConnectionRegistry);
                disconnected = true;
            }
        }
        else
        {
            var connection = registry?.Get(Name);
            if (connection is null && !string.IsNullOrWhiteSpace(Name))
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException($"No Dataverse connection named '{Name}' exists."),
                    Globals.ErrorIdConnectionNotFound,
                    ErrorCategory.ObjectNotFound,
                    Name));
                return;
            }
            if (connection is not null && ShouldProcess(connection.Name, "Disconnect Dataverse connection"))
            {
                _ = registry.Remove(connection.Name);
                disconnected = true;
            }
        }

        if (disconnected)
        {
            RemoveLegacyVariables();
            WriteInformation("Dataverse disconnected successfully.", ["dataverse"]);
        }
    }

    private void RemoveLegacyVariables()
    {
        (GetVariableValue(Globals.VariableNameServiceProvider) as IDisposable)?.Dispose();
        SessionState.PSVariable.Remove(Globals.VariableNameIsOnPremise);
        SessionState.PSVariable.Remove(Globals.VariableNameAuthResult);
        SessionState.PSVariable.Remove(Globals.VariableNameAccessToken);
        SessionState.PSVariable.Remove(Globals.VariableNameAccessTokenExpiresOn);
        SessionState.PSVariable.Remove(Globals.VariableNameConnectionString);
        SessionState.PSVariable.Remove(Globals.VariableNameServiceProvider);
    }
}
