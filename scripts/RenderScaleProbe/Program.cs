using System.Diagnostics;
using System.Text.Json;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;

// Long-song render scale: one measurement per process so peak working set is per case.
// Usage (Release): RenderScaleProbe legacy|native REPEATS  — each repeat is 16 s of music.
if (args.Length != 2 || !int.TryParse(args[1], out int repeats) || repeats < 1)
{
    Console.Error.WriteLine("Usage: RenderScaleProbe legacy|native REPEATS");
    return 2;
}
#if DEBUG
Console.Error.WriteLine("Measurements require a Release build.");
return 2;
#endif
string lead = string.Concat(Enumerable.Repeat("| C4q E4q G4q C5q | A3q C4q E4q A4q ", 4));
string bass = string.Concat(Enumerable.Repeat("| C2w | A1w ", 4));
string source = $"use \"@std\"\ntempo 120 {{ section a {{\n  Sequence lead = {lead}|\n  Sequence bass = {bass}|\n}} }}\nSong song = [a*{repeats}]\nsong";
using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
var evaluated = engine.Evaluate(source, "scale.flow");
if (!evaluated.Succeeded) { Console.Error.WriteLine(engine.ErrorReporter.FormatErrors()); return 1; }
var song = evaluated.LastValue!.As<SongData>();

GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
int gen2Before = GC.CollectionCount(2);
var clock = Stopwatch.StartNew();
long frames;
switch (args[0])
{
    case "legacy":
        var buffer = SongRenderer.RenderSong([MusicValue.Song(song), Value.String("sine")]).As<AudioBuffer>();
        frames = buffer.Data.Length / buffer.Channels;
        break;
#if !LEGACY_ONLY
    case "native":
        var snapshot = FlowLang.Music.CompositionCompiler.Compile(song, "scale.flow");
        long delivered = 0;
        Flow.Audio.SineCompositionRenderer.Render(snapshot, block => delivered += block.Length / 2);
        frames = delivered;
        break;
#endif
    default:
        Console.Error.WriteLine($"Unknown mode {args[0]}");
        return 2;
}
clock.Stop();
Console.WriteLine(JsonSerializer.Serialize(new
{
    mode = args[0], repeats, outputSeconds = frames / 44100.0, frames,
    elapsedMs = clock.Elapsed.TotalMilliseconds,
    allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore,
    gen2Collections = GC.CollectionCount(2) - gen2Before,
    peakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64,
}));
return 0;
