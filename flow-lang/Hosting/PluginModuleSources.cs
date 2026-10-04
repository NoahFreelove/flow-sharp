using System.Text;
using Flow.Studio.Model;
using FlowLang.Runtime;
namespace FlowLang.Hosting;

/// <summary>Plugin import namespace: bare dependency IDs resolve only to saved UTF-8
/// bytes. Explicit @ names resolve to this runtime's known bundled API modules.
/// This constrains module loading, not arbitrary native builtin IO privileges.</summary>
internal static class PluginModuleSources
{
    private static readonly HashSet<string> Bundled = new(StringComparer.Ordinal)
    { "core", "collections", "std", "bars", "flowDaw", "audio", "composition", "patterns", "generative", "improv", "notation", "midi" };
    internal static Func<string, string, ResolvedModuleSource> Create(PluginPackage package) => CreateResolver(package);
    internal static Func<string, string, ResolvedModuleSource> ForGenerator() => CreateResolver(null);
    private static Func<string, string, ResolvedModuleSource> CreateResolver(PluginPackage? package)
    {
        var utf8 = new UTF8Encoding(false, true);
        var dependencies = (package?.Dependencies ?? []).ToDictionary(d => d.Id, StringComparer.Ordinal);
        var modules = new Dictionary<string, ResolvedModuleSource>(StringComparer.Ordinal);
        var bundled = new Dictionary<string, ResolvedModuleSource>(StringComparer.Ordinal);
        return (path, _) =>
        {
            if (dependencies.TryGetValue(path, out var dependency))
            {
                if (!modules.TryGetValue(path, out var pinned))
                    modules.Add(path, pinned = new("/__flow_plugin__/" + path + ".flow", utf8.GetString(Convert.FromBase64String(dependency.Base64))));
                return pinned;
            }
            if (path.StartsWith('@'))
            {
                string name = path[1..];
                if (name.EndsWith(".flow", StringComparison.Ordinal)) name = name[..^5];
                if (Bundled.Contains(name) || package is null && name == "test")
                {
                    if (!bundled.TryGetValue(name, out var source))
                        bundled.Add(name, source = new(ModuleLoader.ResolveStdlibPath(name), ModuleLoader.ReadBundledModule(name)));
                    return source;
                }
            }
            throw new InvalidOperationException($"Module '{path}' is not a pinned dependency or supported bundled DAW API module");
        };
    }
}
