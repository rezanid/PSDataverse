namespace PSDataverse;

using System.Management.Automation;

[Cmdlet(VerbsCommon.Get, "DataverseConnection")]
[OutputType(typeof(DataverseConnection))]
public sealed class GetDataverseConnectionCmdlet : DataverseCmdlet
{
    [Parameter(Position = 0)]
    [SupportsWildcards]
    public string Name { get; set; }

    protected override void ProcessRecord()
    {
        var registry = GetConnectionRegistry(create: false);
        if (registry is null)
        {
            return;
        }
        var pattern = string.IsNullOrWhiteSpace(Name) ? null : new WildcardPattern(Name, WildcardOptions.IgnoreCase);
        foreach (var connection in registry.Connections)
        {
            if (pattern is null || pattern.IsMatch(connection.Name))
            {
                WriteObject(connection);
            }
        }
    }
}
