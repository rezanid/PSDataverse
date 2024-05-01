namespace PSDataverse.Commands;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Scriban;
using Scriban.Runtime;

[Cmdlet(VerbsData.ConvertTo, "CustomText")]
[OutputType(typeof(string))]
public class ConvertToCustomTextCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    public PSObject InputObject { get; set; }

    [Parameter(Mandatory = true, Position = 0)]
    public string Template { get; set; }

    [Parameter(Position = 1)]
    public string OutputFile { get; set; }

    protected override void ProcessRecord()
    {
        var scriptObject = BuildScriptObject(InputObject);
        scriptObject.Import(typeof(ScribanExtensions));
        scriptObject.Add(ScribanExtensionCache.KnownAssemblies.Humanizr.ToString().ToLowerInvariant(), ScribanExtensionCache.GetHumanizrMethods());

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        var templateContent = File.ReadAllText(Template);
        var template = Scriban.Template.Parse(templateContent);

        var result = template.Render(context);

        if (string.IsNullOrEmpty(OutputFile))
        {
            WriteObject(result);
        }
        else
        {
            File.WriteAllText(OutputFile, result);
        }
    }

    private static ScriptObject BuildScriptObject(PSObject input)
    {
        var scriptObject = new ScriptObject();

        foreach (var propInfo in input.Properties)
        {
            if (propInfo.Value is PSObject obj)
            {
                scriptObject.Add(propInfo.Name, BuildScriptObject(obj));
            }
            else if (propInfo.Value is IEnumerable<object> objs)
            {
                scriptObject.Add(propInfo.Name, objs.Select(o => o is PSObject pso ? BuildScriptObject(pso) : o).ToList());
            }
            else
            {
                scriptObject.Add(propInfo.Name, propInfo.Value);
            }
        }

        return scriptObject;
    }
}
