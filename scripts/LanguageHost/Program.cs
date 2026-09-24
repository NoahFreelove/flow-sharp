using System.Text.Json;
using FlowLang.Runtime;
using LanguageHost;

// Scripts and stdin have no prelude. Interactive modes explicitly import @core.
string? reportPath = null;
var arguments = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--json" && i + 1 < args.Length) reportPath = args[++i];
    else arguments.Add(args[i]);
}

using var session = new LanguageSession(Console.Out, Console.Error);
bool success = true;
if (arguments is ["--repl"] || (arguments.Count == 0 && !Console.IsInputRedirected))
{
    success = session.Execute("use \"@core\"");
    while (true)
    {
        if (!Console.IsInputRedirected) Console.Write("flow> ");
        var line = Console.ReadLine();
        if (line is null or ":quit") break;
        success = session.Execute(line) && success;
    }
}
else if (arguments is ["-e", var source])
{
    success = session.Execute("use \"@core\"") && session.Execute(source);
}
else if (arguments.Count == 0)
{
    success = session.Execute(Console.In.ReadToEnd(), "<stdin>");
}
else if (arguments is [var file] && !file.StartsWith('-'))
{
    var path = Path.GetFullPath(file);
    success = session.Execute(File.ReadAllText(path), path);
}
else
{
    Console.Error.WriteLine("Usage: LanguageHost [--json report.json] [script.flow | -e source | --repl]");
    return 2;
}

if (reportPath is not null)
{
    var report = new
    {
        success,
        languageReferences = typeof(Value).Assembly.GetReferencedAssemblies().Select(a => a.Name).Order().ToArray(),
        loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).Order().ToArray(),
        nativeLibraries = File.Exists("/proc/self/maps")
            ? File.ReadLines("/proc/self/maps")
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(p => p.Length >= 6 && p[5].Contains(".so"))
                .Select(p => Path.GetFileName(p[5])).Distinct().Order().ToArray()
            : [],
    };
    File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
}
return success ? 0 : 1;
