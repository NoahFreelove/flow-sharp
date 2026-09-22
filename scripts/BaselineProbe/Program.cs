using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;

if (args.Length != 1)
    throw new ArgumentException("Usage: BaselineProbe OUTPUT_DIRECTORY (Release build)");
#if DEBUG
throw new InvalidOperationException("Measurements require a Release build.");
#endif
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name),
    JsonSerializer.Serialize(value, jsonOptions) + "\n");

// Reflect metadata only: do not read static values or instantiate exported types.
var assemblyNames = new[] { "flow-lang", "flow-midi", "flow-interpreter", "flow-lsp", "flow" };
var api = new List<object>();
var globals = new List<object>();
foreach (var name in assemblyNames)
{
    var assembly = Assembly.Load(name);
    foreach (var type in assembly.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
    {
        if (type.IsVisible)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => $"{m.MemberType}: {m}").Order(StringComparer.Ordinal).ToArray();
            api.Add(new { assembly = name, type = type.FullName, members });
        }
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                     BindingFlags.Static | BindingFlags.DeclaredOnly).Where(f => !f.IsLiteral))
            globals.Add(new { assembly = name, type = type.FullName, field = field.Name,
                fieldType = field.FieldType.ToString(), readOnly = field.IsInitOnly });
    }
}
Write("public-api.json", api);
Write("static-fields.json", globals);

var measurements = new List<object>();
long Measure(string name, Func<long> action, int repeats = 5, bool warmup = true)
{
    if (warmup) action();
    var samples = new List<object>();
    long result = 0;
    for (int i = 0; i < repeats; i++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long heapBefore = GC.GetTotalMemory(false);
        long allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var timer = Stopwatch.StartNew();
        result = action();
        timer.Stop();
        long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        long heapAfter = GC.GetTotalMemory(false);
        using var process = Process.GetCurrentProcess();
        samples.Add(new { milliseconds = timer.Elapsed.TotalMilliseconds, allocatedBytes = allocated,
            managedHeapBeforeBytes = heapBefore, managedHeapAfterBytes = heapAfter,
            workingSetBytes = process.WorkingSet64, processPeakWorkingSetBytes = process.PeakWorkingSet64,
            result });
    }
    measurements.Add(new { name, samples });
    return result;
}

Measure("first-engine-create-dispose", () => { using var engine = new FlowEngine(); return engine.ErrorReporter.HasErrors ? throw new Exception(engine.ErrorReporter.FormatErrors()) : 1; }, 1, false);
Measure("warm-engine-create-dispose", () => { using var engine = new FlowEngine(); return engine.ErrorReporter.HasErrors ? throw new Exception(engine.ErrorReporter.FormatErrors()) : 1; });

string parseSource = string.Join('\n', Enumerable.Range(0, 1000).Select(i => $"Int item{i} = (add {i} 1)"));
FlowLang.Ast.Program Parse(string source, ErrorReporter errors)
{
    var program = new Parser(new SimpleLexer(source, errors, "<baseline>").Tokenize(), errors).Parse();
    if (errors.HasErrors) throw new Exception(errors.FormatErrors());
    return program;
}
Measure("lex-parse-1000-declarations", () => Parse(parseSource, new ErrorReporter()).Statements.Count);

const string evalSource = "(reduce (map (range 0 10000) (fn Int n => (mul n 2))) 0 (fn Int acc, Int n => (add acc n)))";
using (var engine = new FlowEngine())
{
    var errors = new ErrorReporter();
    var ast = Parse(evalSource, errors);
    var interpreter = new FlowLang.Interpreter.Interpreter(engine.Context, errors, engine.ModuleLoader);
    long result = Measure("evaluate-preparsed-map-reduce-10000", () =>
    {
        errors.Clear();
        interpreter.Execute(ast);
        if (errors.HasErrors) throw new Exception(errors.FormatErrors());
        return Convert.ToInt64(interpreter.GetLastExpressionValue()!.Data);
    });
    if (result != 99990000) throw new Exception($"Unexpected evaluation result: {result}");
}

var renderEvidence = new List<object>();
foreach (int sections in new[] { 1, 8, 32 })
{
    using var engine = new FlowEngine();
    string source = "use \"@audio\"\nsection phrase { Sequence lead = | C4q E4q G4q C5q | }\nSong piece = [" +
        string.Join(' ', Enumerable.Repeat("phrase", sections)) + "]\n";
    if (!engine.Execute(source, "<baseline-render>")) throw new Exception(engine.ErrorReporter.FormatErrors());
    var song = engine.Context.GetVariable("piece");
    AudioBuffer? last = null;
    Measure($"render-sine-{sections}-sections", () =>
    {
        last = SongRenderer.RenderSong(new[] { song, Value.String("sine") }).As<AudioBuffer>();
        return last.Frames;
    });
    if (last is null || last.Frames == 0 || last.Data.Any(v => !float.IsFinite(v)))
        throw new Exception("Invalid render output");
    byte[] bytes = new byte[last.Data.Length * sizeof(float)];
    Buffer.BlockCopy(last.Data, 0, bytes, 0, bytes.Length);
    renderEvidence.Add(new { sections, last.Frames, last.SampleRate, last.Channels,
        pcmBytes = bytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(bytes)), source });
}
Write("measurements.json", new {
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount,
    stopwatchFrequency = Stopwatch.Frequency, parseSource, evalSource, measurements, renderEvidence,
    notes = "Release, one warmup then five samples except first engine. GC forced before each sample; process-wide allocation delta. Evaluation excludes parsing/engine setup. Rendering excludes composition setup; no playback or device opening requested. Peak RSS is cumulative across the process, not per workload. First engine includes initialization, not process launch. Current full compatibility host may load user style packs. No real-time guarantees." });
Console.WriteLine($"Baseline artifacts: {output}");
