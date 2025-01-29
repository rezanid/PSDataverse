namespace PSDataverse.Dataverse.Commands;
using System;
using System.Management.Automation;

[Cmdlet(VerbsCommunications.Disconnect, "Dataverse")]
public class DisconnectDataverseCmdlet : PSCmdlet
{
    protected override void ProcessRecord()
    {
        SessionState.PSVariable.Remove(Globals.VariableNameIsOnPremise);
        SessionState.PSVariable.Remove(Globals.VariableNameAuthResult);
        SessionState.PSVariable.Remove(Globals.VariableNameAccessToken);
        SessionState.PSVariable.Remove(Globals.VariableNameAccessTokenExpiresOn);
        SessionState.PSVariable.Remove(Globals.VariableNameConnectionString);
        var serviceProvider = (IServiceProvider)GetVariableValue(Globals.VariableNameServiceProvider);
        WriteInformation("Dataverse disconnected successfully.", ["dataverse"]);
    }
}