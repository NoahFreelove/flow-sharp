// Phase 1 minimal-host prototype (restructuring roadmap section 10).
//
// Embeds flow-lang the way a language-only host would: construct an engine, run a
// program that uses no music, capture its output. Then records everything the
// current engine initialized anyway, so Phase 2/3 extraction has a measured list
// of direct dependencies to remove. This is a probe, not a supported host API.
//
// Usage: dotnet run --project scripts/MinimalHost -- [--json out.json] [program.flow]
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;

const string DefaultProgram = """
    use "@std"
    Strings words = (list "alpha" "beta" "gamma" "beta")
    Dict<String, Int> none = (dict)
    Dict<String, Int> counts = (reduce words none (fn Dict<String, Int> acc, String w => (set acc w (add (getOr acc w 0) 1))))
    (each counts (fn String w, Int n => (print $"{w}={n}")))
    (print (str (map (range 1 6) (fn Int n => (mul n n)))))
    """;

string? jsonPath = null;
string? programPath = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--json") jsonPath = args[++i];
    else programPath = args[i];
}
var source = programPath is null ? DefaultProgram : File.ReadAllText(programPath);
var fileName = programPath ?? "<minimal-host>";

var assembliesBefore = LoadedAssemblies();
var nativeBefore = NativeLibraries();
var statics = new Dictionary<string, object?>();

// Output and advisories go to the engine's own sinks (no console redirection).
var stdout = new StringWriter();
var stderr = new StringWriter();

long allocBefore = GC.GetAllocatedBytesForCurrentThread();
var construct = Stopwatch.StartNew();
FlowEngine engine;
try
{
    engine = new FlowEngine(new EngineOptions { Output = stdout, Diagnostics = stderr });
}
finally
{
    construct.Stop();
}
long allocConstruct = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

var assembliesAfterConstruct = LoadedAssemblies();
var nativeAfterConstruct = NativeLibraries();
// Engine state reachable from outside the engine's entry points (all should be false).
statics["SessionServices.Current after construction"] = SessionServices.Current is not null;
statics["RenderServices.Current after construction"] = FlowLang.StandardLibrary.Audio.RenderServices.Current is not null;
statics["FlowConfig.Active is defaults"] = ReferenceEquals(FlowConfig.Active, FlowConfigPoco.Defaults);
var modulesAfterConstruct = LoadedModules(engine.ModuleLoader);
var styleCount = engine.Context.StyleRegistry.Count;

var run = Stopwatch.StartNew();
bool success;
try
{
    success = engine.Execute(source, fileName);
}
finally
{
    run.Stop();
}
statics["SessionServices.Current after execution"] = SessionServices.Current is not null;

var reporter = engine.ErrorReporter;
var diagnostics = reporter.Errors.Select(e => $"{e.Level}: {e.Message}")
    .Concat(reporter.Diagnostics.Select(d => $"{d.Level}: {d.Message}"))
    .ToList();

// Builtins are classified by the namespace of the lambda that implements them.
var builtins = BuiltinsByNamespace(engine);

var report = new
{
    recordedUtc = DateTime.UtcNow.ToString("u"),
    runtime = Environment.Version.ToString(),
    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    program = new
    {
        source = fileName,
        success,
        stdout = stdout.ToString().Replace("\r\n", "\n"),
        stderr = stderr.ToString().Replace("\r\n", "\n"),
        diagnostics,
    },
    construction = new
    {
        milliseconds = Math.Round(construct.Elapsed.TotalMilliseconds, 1),
        allocatedBytesThisThread = allocConstruct,
        note = "Single cold process measurement; indicative only.",
    },
    executionMilliseconds = Math.Round(run.Elapsed.TotalMilliseconds, 1),
    assemblies = new
    {
        // Static closure: what flow-lang.dll references regardless of what runs.
        flowLangReferences = typeof(FlowEngine).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? "?").Where(n => !n.StartsWith("System") && n != "netstandard").Order().ToList(),
        loadedByConstruction = assembliesAfterConstruct.Except(assembliesBefore).Order().ToList(),
        loadedByExecution = LoadedAssemblies().Except(assembliesAfterConstruct).Order().ToList(),
    },
    nativeLibraries = new
    {
        loadedByConstruction = nativeAfterConstruct.Except(nativeBefore).Order().ToList(),
        loadedByExecution = NativeLibraries().Except(nativeAfterConstruct).Order().ToList(),
    },
    processStatics = statics,
    modulesLoadedAtConstruction = modulesAfterConstruct,
    modulesLoadedAfterExecution = LoadedModules(engine.ModuleLoader),
    improvStylesLoaded = styleCount,
    builtinSignaturesByImplementationNamespace = builtins,
};

engine.Dispose();

var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if (jsonPath is not null) File.WriteAllText(jsonPath, json + "\n");
Console.Write(report.program.stdout);
if (report.program.stderr.Length > 0) Console.Error.Write(report.program.stderr);
foreach (var d in diagnostics) Console.Error.WriteLine(d);
return success && diagnostics.Count == 0 ? 0 : 1;

static List<string> LoadedAssemblies() =>
    AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name ?? "?").Distinct().ToList();

// Linux only: native shared objects mapped into this process.
static List<string> NativeLibraries()
{
    if (!File.Exists("/proc/self/maps")) return [];
    return File.ReadLines("/proc/self/maps")
        .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Where(p => p.Length >= 6 && p[5].Contains(".so"))
        .Select(p => Path.GetFileName(p[5]))
        .Distinct()
        .ToList();
}

// The loader exposes no list of loaded modules; read its private set.
static List<string> LoadedModules(ModuleLoader loader)
{
    var field = typeof(ModuleLoader).GetField("_loadedModules", BindingFlags.NonPublic | BindingFlags.Instance);
    if (field?.GetValue(loader) is not IEnumerable<string> set) return ["<unavailable>"];
    var root = Path.GetDirectoryName(typeof(FlowEngine).Assembly.Location)!;
    return set.Select(p => p.StartsWith(root) ? Path.GetRelativePath(root, p) : p).Order().ToList();
}

static SortedDictionary<string, int> BuiltinsByNamespace(FlowEngine engine)
{
    var registry = engine.Context.InternalRegistry;
    var result = new SortedDictionary<string, int>(StringComparer.Ordinal);

    var implField = typeof(FlowLang.StandardLibrary.InternalFunctionRegistry)
        .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
        .FirstOrDefault(f => f.Name.Contains("implementations", StringComparison.OrdinalIgnoreCase));
    if (implField?.GetValue(registry) is not System.Collections.IDictionary impls)
    {
        result["<implementations unavailable>"] = registry.EnumerateSignatures().Sum(kv => kv.Value.Count);
        return result;
    }
    foreach (System.Collections.DictionaryEntry entry in impls)
    {
        if (entry.Value is not System.Collections.IEnumerable overloads) continue;
        foreach (var overload in overloads)
        {
            // Each overload is a (Signature, Implementation) value tuple.
            var del = overload?.GetType().GetFields()
                .Select(f => f.GetValue(overload)).OfType<Delegate>().FirstOrDefault();
            var ns = del?.Method.DeclaringType is { } t
                ? (t.DeclaringType ?? t).Namespace ?? "<global>"
                : "<unknown>";
            result[ns] = result.GetValueOrDefault(ns) + 1;
        }
    }
    return result;
}
