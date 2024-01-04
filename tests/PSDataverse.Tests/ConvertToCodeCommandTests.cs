namespace PSDataverse.Dataverse.Tests;

using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using FluentAssertions;
using PSDataverse.Commands;
using Xunit;

public class ConvertToCodeCommandTests
{
    private readonly ConvertToCodeCmdlet convertToCodeCommand;

    public ConvertToCodeCommandTests() => convertToCodeCommand = new ConvertToCodeCmdlet();

    [Fact]
    public void ProcessRecord_NoOutputFile_ProvidesExpectedResult()
    {
        // Arrange
        var iss = InitialSessionState.CreateDefault();
        iss.Commands.Add(new SessionStateCmdletEntry(
            "Get-Command", typeof(Microsoft.PowerShell.Commands.GetCommandCommand), ""));
        iss.Commands.Add(new SessionStateCmdletEntry(
            "Import-Module", typeof(Microsoft.PowerShell.Commands.ImportModuleCommand), ""));
        iss.Commands.Add(new SessionStateCmdletEntry(
            "ConvertTo-Code", typeof(ConvertToCodeCmdlet), ""));
        iss.Commands.Add(new SessionStateCmdletEntry(
            "ConvertFrom-Json", typeof(Microsoft.PowerShell.Commands.ConvertFromJsonCommand), ""));
        var rs = RunspaceFactory.CreateRunspace(iss);
        rs.Open();
        using var powershell = PowerShell.Create();
        powershell.Runspace = rs;
        //var input = new PSObject();
        //input.Properties.Add(new PSNoteProperty("EntityLogicalName", "account"));
        //input.Properties.Add(new PSNoteProperty("Attributes", new PSObject[]
        //{
        //        new(new { LogicalName = "firstname", DisplayName = "First Name", Type = "string" }),
        //        new(new { LogicalName = "lastname", DisplayName = "Last Name", Type = "string" })
        //}));
        powershell
            .AddCommand("Get-Content").AddParameter("Path", Path.Combine(Directory.GetCurrentDirectory(), "samples", "account-Definition.json"))
            .AddCommand("ConvertFrom-Json")
            .AddCommand("ConvertTo-Code")
            .AddParameter("Template", Path.Combine(Directory.GetCurrentDirectory(), "samples", "Template1.sbn"));

        // Act
        var results = powershell.Invoke();
        rs.Close();
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "account.cs"), results[0].ToString());

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(1);
    }
}
