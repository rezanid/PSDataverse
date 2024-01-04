namespace PSDataverse;
using System;
using System.Collections.Generic;
using System.Linq;
using Humanizer;
using PSDataverse.Extensions;
using Scriban.Runtime;

public static class ScribanExtensionCache
{
    public enum KnownAssemblies
    {
        Humanizr,
    }

    private static readonly Dictionary<KnownAssemblies, ScriptObject> CachedResults =
    new();

    public static ScriptObject GetHumanizrMethods() => GetOrCreate(KnownAssemblies.Humanizr,
        () =>
        {
            //force a load of the DLL otherwise we won't see the types
            "force load".Humanize();
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(a => a.FullName.EmptyWhenNull().Contains("Humanizer"))
                .GetTypes()
                .Where(t => t.Name.EndsWith("Extensions", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        });

    private static ScriptObject GetOrCreate(KnownAssemblies name, Func<IEnumerable<Type>> typeFetcher)
    {
        if (CachedResults.TryGetValue(name, out var scriptObject))
        {
            return scriptObject;
        }

        scriptObject = new ScriptObject();
        foreach (var extensionClass in typeFetcher())
        {
            scriptObject.Import(extensionClass);
        }

        CachedResults[name] = scriptObject;

        return scriptObject;
    }
}
