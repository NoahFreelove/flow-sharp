using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Flow.Audio;
using Flow.Music.Model;
using Flow.Platform.Linux;
using FlowLang.Core;

if (args.Length == 2 && args[0] == "--worker")
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(int.Parse(args[1])));
    var load = RunLoad(deadline.Token, () => { Console.WriteLine("ready"); Console.Out.Flush(); });
    Console.WriteLine(JsonSerializer.Serialize(load));
    return 0;
}
if (args.Length == 1 && args[0] == "--list")
{
    Console.WriteLine(JsonSerializer.Serialize(PortAudioOutput.ListDevices(), new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
#if DEBUG
Console.Error.WriteLine("Use a Release build for callback measurements.");
return 2;
#endif
if (args.Length < 3 || args.Length > 6 || !int.TryParse(args[0], out int seconds) || seconds < 2 || seconds > 1800 ||
    args[1] is not ("idle" or "inprocess" or "isolated") ||
    (args.Length > 3 && !int.TryParse(args[3], out _) && !args[3].Contains('/')) ||
    (args.Length > 5 && args[5] != "--starve") ||
    (args.Length > 4 && (!int.TryParse(args[4], out int requestedBlock) || requestedBlock is not (128 or 256))))
{
    Console.Error.WriteLine("Usage: AudioCallbackProbe SECONDS idle|inprocess|isolated OUTPUT.json [DEVICE_INDEX|HOST/NAME] [128|256] [--starve]\n       AudioCallbackProbe --list\nOutput is muted after rendering. Requires Linux and libportaudio.so.2.");
    return 2;
}
int blockFrames = args.Length > 4 ? int.Parse(args[4]) : 256;
int? deviceIndex = args.Length > 3 && int.TryParse(args[3], out int parsedDevice) ? parsedDevice : null;
string? deviceSelector = args.Length > 3 && deviceIndex is null ? args[3] : null;
bool starvation = args.Length > 5;
if (starvation && (seconds < 6 || args[1] != "idle"))
{
    Console.Error.WriteLine("--starve requires at least 6 seconds and idle mode; it is a diagnostic, not a performance run.");
    return 2;
}
long clockBefore = Stopwatch.GetTimestamp();
long monotonicNs = DiagnosticClock.ReadMonotonicNs();
long realtimeNs = DiagnosticClock.ReadRealtimeNs();
long clockAfter = Stopwatch.GetTimestamp();
long preparationStarted = Stopwatch.GetTimestamp();
int capacity = (int)Math.Ceiling((seconds + 10) * 48000.0 / blockFrames);
var playback = new QueuedSinePlayback(Prepare(440, blockFrames));
var next = Prepare(660, blockFrames);
var probe = new CallbackRenderProbe(playback, capacity,
    stallAfterCallbacks: starvation ? 2 * 48000 / blockFrames : -1, stallMilliseconds: starvation ? 100 : 0, captureThreadIdentity: true);
playback.TrySetLoop(0, playback.TotalFrames);
playback.TryPlay();
double preparationMilliseconds = Stopwatch.GetElapsedTime(preparationStarted).TotalMilliseconds;
using var cancel = new CancellationTokenSource();
using var loadReady = new ManualResetEventSlim();
Task<LoadResult>? loadTask = null;
Process? worker = null;
try
{
    if (args[1] == "inprocess")
    {
        loadTask = Task.Run(() => RunLoad(cancel.Token, loadReady.Set));
        if (!loadReady.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Load worker did not initialize");
    }
    else if (args[1] == "isolated")
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--worker");
        start.ArgumentList.Add((seconds + 5).ToString());
        worker = Process.Start(start) ?? throw new InvalidOperationException("Worker launch failed");
        if (await worker.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)) != "ready")
            throw new InvalidOperationException("Worker initialization failed");
    }
    int[] collectionsBefore = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
    OutputDevice device;
    string version, underflowObservability;
    double latency, actualRate;
    bool fault;
    int retired = 0;
    long openStarted = Stopwatch.GetTimestamp(), startRequested = 0, closeStarted = 0;
    double openMilliseconds = 0, startMilliseconds = 0;
    using (var output = new PortAudioOutput(probe, deviceIndex, deviceSelector))
    {
        openMilliseconds = Stopwatch.GetElapsedTime(openStarted).TotalMilliseconds;
        device = output.Device; version = output.NativeVersion;
        underflowObservability = output.UnderflowObservability;
        latency = output.OutputLatencySeconds; actualRate = output.ActualSampleRate;
        startRequested = Stopwatch.GetTimestamp();
        output.Start();
        startMilliseconds = Stopwatch.GetElapsedTime(startRequested).TotalMilliseconds;
        var timer = Stopwatch.StartNew();
        bool paused = false, resumed = false, replaced = false, restarted = false, stopped = false, replayed = false;
        long lastSeekSecond = -1;
        bool identityPublished = false;
        while (timer.Elapsed.TotalSeconds < seconds)
        {
            if (!output.IsActive) throw new InvalidOperationException($"Device callback stopped; callbackFault={output.CallbackFaulted}");
            if (!identityPublished && probe.NativeThreadId > 0)
            {
                // Publish once from the control thread, never from the native callback.
                string identityPath = args[2] + ".thread";
                File.WriteAllText(identityPath + ".tmp", $"{Environment.ProcessId} {probe.NativeThreadId}\n");
                File.Move(identityPath + ".tmp", identityPath, overwrite: true);
                identityPublished = true;
            }
            double elapsed = timer.Elapsed.TotalSeconds;
            if (!paused && elapsed > seconds * 0.2) paused = playback.TryPause();
            if (paused && !resumed && elapsed > seconds * 0.2 + 0.05) resumed = playback.TryPlay();
            if (!replaced && elapsed > seconds * 0.4) replaced = playback.TryReplace(next);
            if (replaced && !restarted && !playback.ReplacementPending)
            {
                playback.TrySetLoop(0, playback.TotalFrames);
                restarted = playback.TryPlay();
            }
            if (!stopped && elapsed > seconds * 0.7) { playback.RequestStop(); stopped = true; }
            if (stopped && !replayed && !playback.StopPending) replayed = playback.TryPlay();
            long second = (long)elapsed;
            if (second != lastSeekSecond && !playback.ReplacementPending)
            {
                playback.TrySeek(playback.TotalFrames / 3);
                lastSeekSecond = second;
            }
            if (playback.TryTakeRetired(out _)) retired++;
            Thread.Sleep(10);
        }
        fault = output.CallbackFaulted;
        closeStarted = Stopwatch.GetTimestamp();
    } // Native close joins callbacks before Capture or source ownership is released.
    double closeMilliseconds = Stopwatch.GetElapsedTime(closeStarted).TotalMilliseconds;
    int[] collectionsAfter = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
    cancel.Cancel();
    LoadResult? load = loadTask is null ? null : await loadTask;
    if (worker is not null)
    {
        string result = await worker.StandardOutput.ReadToEndAsync();
        await worker.WaitForExitAsync();
        if (worker.ExitCode != 0) throw new InvalidOperationException("Isolated load worker failed");
        load = JsonSerializer.Deserialize<LoadResult>(result);
    }
    var capture = probe.Capture();
    var samples = capture.Samples;
    if (samples.Length == 0) throw new InvalidOperationException("No callbacks captured");
    var steady = samples.Where(s => s.StartTicks - samples[0].StartTicks >= Stopwatch.Frequency).ToArray();
    long endClockBefore = Stopwatch.GetTimestamp();
    long endMonotonicNs = DiagnosticClock.ReadMonotonicNs();
    long endRealtimeNs = DiagnosticClock.ReadRealtimeNs();
    long endClockAfter = Stopwatch.GetTimestamp();
    var report = new
    {
        diagnosticStall = new { enabled = starvation, requestedMilliseconds = starvation ? 100 : 0, count = probe.InjectedStalls,
            note = "Intentional callback-only starvation; injected runs are not performance evidence. Native flags are never synthesized." },
        timestampUtc = DateTimeOffset.UtcNow, mode = args[1], seconds, muted = true,
        os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        runtime = RuntimeInformation.FrameworkDescription, underflowObservability, cpuCount = Environment.ProcessorCount,
        serverGc = GCSettings.IsServerGC, device, nativeVersion = version,
        requestedSampleRate = 48000, actualSampleRate = actualRate, blockFrames,
        reportedOutputLatencySeconds = latency, voices = 32, sequences = 2,
        lifecycle = new { preparationMilliseconds, openMilliseconds, startMilliseconds, closeMilliseconds,
            firstCallbackAfterStartRequestMilliseconds = (samples[0].StartTicks - startRequested) * 1000.0 / Stopwatch.Frequency,
            note = "Wall-clock control-thread startup/teardown timings; first callback entry is not first audible sample or physical output latency. Isolated worker startup is excluded from preparation/open." },
        allCallbacks = Summarize(samples), afterFirstSecond = Summarize(steady),
        callbackCadence = SummarizeCadence(samples, 48000),
        diagnosticClock = new { stopwatchBeforeTicks = clockBefore, stopwatchAfterTicks = clockAfter,
            monotonicNs, realtimeNs, stopwatchFrequency = Stopwatch.Frequency, firstCallbackStartTicks = samples[0].StartTicks,
            nativeCallbackThreadId = probe.NativeThreadId },
        diagnosticClockEnd = new { stopwatchBeforeTicks = endClockBefore, stopwatchAfterTicks = endClockAfter,
            monotonicNs = endMonotonicNs, realtimeNs = endRealtimeNs },
        droppedTimingSamples = capture.Dropped, callbackFault = fault,
        parentGcCollections = new[] { collectionsAfter[0] - collectionsBefore[0], collectionsAfter[1] - collectionsBefore[1], collectionsAfter[2] - collectionsBefore[2] },
        load, playback.Generation, retired, playback.RejectedCommands, playback.DiscardedCommands,
        limitation = "Muted device probe; managed-body duration excludes native dispatch/entry pauses. Callback spacing and underflows are separate evidence. Zero callback flags do not establish zero underflows; consult underflowObservability. Duration alone does not establish a pass. No UI load, listening certification or native DSP comparison.",
    };
    string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(args[2], json + "\n");
    Console.WriteLine(json);
    return fault || capture.Dropped != 0 ? 1 : 0; // Timing failures remain in the report, not hidden by exit status.
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    File.WriteAllText(args[2], JsonSerializer.Serialize(new
    {
        success = false, mode = args[1], seconds, deviceIndex, blockFrames,
        deviceSelector, starvation, error = error.GetType().Name, message = error.Message,
    }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    return 1;
}
finally
{
    cancel.Cancel();
    if (loadTask is not null) await loadTask;
    if (worker is not null)
    {
        if (!worker.HasExited) { worker.Kill(entireProcessTree: true); await worker.WaitForExitAsync(); }
        worker.Dispose();
    }
}

static PreparedSinePlayback Prepare(double baseFrequency, int blockFrames)
{
    var sequences = Enumerable.Range(0, 2).Select(track => new SequenceSnapshot(Guid.NewGuid(), $"track{track}", 4,
        Enumerable.Range(0, 16).Select(i => new NoteEvent(Guid.NewGuid(), $"voice{track}-{i}", 0, 4,
            new('A', 4, 0, null, 69, baseFrequency * Math.Pow(2, (track * 16 + i) / 12.0))))));
    var section = new SectionSnapshot(Guid.NewGuid(), "stress", new(Bpm: 120, Gain: 0.03), sequences);
    return PreparedSinePlayback.Prepare(new(Guid.NewGuid(), [new(Guid.NewGuid(), section, 1000)]), new(48000, blockFrames));
}

static object Summarize(CallbackSample[] samples)
{
    var durations = samples.Select(s => s.ElapsedTicks * 1000.0 / Stopwatch.Frequency).Order().ToArray();
    double maxGap = 0;
    for (int i = 1; i < samples.Length; i++) maxGap = Math.Max(maxGap, (samples[i].StartTicks - samples[i - 1].StartTicks) * 1000.0 / Stopwatch.Frequency);
    return new
    {
        count = samples.Length,
        capturedSpanSeconds = samples.Length < 2 ? 0 : (samples[^1].StartTicks - samples[0].StartTicks) / (double)Stopwatch.Frequency,
        renderedFrames = samples.Sum(s => (long)s.Frames),
        worstCallbacks = samples.OrderByDescending(s => s.ElapsedTicks).Take(20).Select(s => new
        {
            secondsFromWindowStart = (s.StartTicks - samples[0].StartTicks) / (double)Stopwatch.Frequency,
            milliseconds = s.ElapsedTicks * 1000.0 / Stopwatch.Frequency,
            s.Frames, s.OutputUnderflow, s.AllocatedBytes,
        }).ToArray(),
        p50Milliseconds = durations.Length == 0 ? 0 : durations[(durations.Length - 1) / 2],
        p99Milliseconds = durations.Length == 0 ? 0 : durations[(int)((durations.Length - 1) * 0.99)],
        maxMilliseconds = durations.Length == 0 ? 0 : durations[^1], maxEntryGapMilliseconds = maxGap,
        over70Percent = samples.Count(s => s.ElapsedTicks / (double)Stopwatch.Frequency > s.Frames / 48000.0 * 0.7),
        overDeadline = samples.Count(s => s.ElapsedTicks / (double)Stopwatch.Frequency > s.Frames / 48000.0),
        outputUnderflowFlagCount = samples.Count(s => s.OutputUnderflow), allocatedBytes = samples.Sum(s => s.AllocatedBytes),
    };
}

static object SummarizeCadence(CallbackSample[] samples, int rate)
{
    double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    var gaps = Enumerable.Range(1, samples.Length - 1).Select(i => new
    {
        index = i,
        gap = Milliseconds(samples[i].StartTicks - samples[i - 1].StartTicks),
        period = samples[i - 1].Frames * 1000.0 / rate,
    }).ToArray();
    var timing = samples.Where(s => s.NativeTiming.HasValue).Select(s => s.NativeTiming!.Value).ToArray();
    var lead = timing.Select(t => (t.OutputDacSeconds - t.CurrentSeconds) * 1000).Where(double.IsFinite).Order().ToArray();
    object Describe(int i) => new
    {
        index = i,
        secondsFromStart = (samples[i].StartTicks - samples[0].StartTicks) / (double)Stopwatch.Frequency,
        entryGapMilliseconds = i == 0 ? 0 : Milliseconds(samples[i].StartTicks - samples[i - 1].StartTicks),
        bodyMilliseconds = Milliseconds(samples[i].ElapsedTicks),
        samples[i].Frames, samples[i].StatusFlags, samples[i].NativeTiming, samples[i].InjectedStall,
    };
    return new
    {
        entryGapP50Milliseconds = gaps.Length == 0 ? 0 : gaps.Select(g => g.gap).Order().ElementAt((gaps.Length - 1) / 2),
        entryGapP99Milliseconds = gaps.Length == 0 ? 0 : gaps.Select(g => g.gap).Order().ElementAt((int)((gaps.Length - 1) * 0.99)),
        gapsOverOneAndHalfPeriods = gaps.Count(g => g.gap > g.period * 1.5),
        gapsUnderHalfPeriod = gaps.Count(g => g.gap < g.period * 0.5),
        nativeTimingSamples = timing.Length,
        injectedStallCallbacks = Enumerable.Range(0, samples.Length).Where(i => samples[i].InjectedStall).Select(Describe).ToArray(),
        underflowFlagCallbacks = Enumerable.Range(0, samples.Length).Where(i => samples[i].OutputUnderflow).Take(100).Select(Describe).ToArray(),
        underflowFlagRecordsTruncated = samples.Count(s => s.OutputUnderflow) > 100,
        nativeOutputLeadMilliseconds = lead.Length == 0 ? null : new
        {
            min = lead[0], median = lead[(lead.Length - 1) / 2], max = lead[^1],
            note = "Backend-provided estimate, not measured hardware latency; startup/stale/invalid timing may be present.",
        },
        longestGapNeighborhoods = gaps.OrderByDescending(g => g.gap).Take(10).Select(g => new
        {
            gapIndex = g.index,
            callbacks = Enumerable.Range(Math.Max(0, g.index - 2), Math.Min(samples.Length, g.index + 4) - Math.Max(0, g.index - 2))
                .Select(Describe).ToArray(),
        }).ToArray(),
    };
}

static LoadResult RunLoad(CancellationToken cancel, Action ready)
{
    string path = Path.Combine(Path.GetTempPath(), $"flow-audio-load-{Guid.NewGuid():N}.bin");
    File.WriteAllBytes(path, new byte[4 * 1024 * 1024]);
    int[] before = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
    long iterations = 0;
    string script = "use \"@core\"; (reduce (list " + string.Join(' ', Enumerable.Range(1, 256)) + ") 0 (fn Int acc, Int item => (add acc item)))";
    try
    {
        ready();
        while (!cancel.IsCancellationRequested)
        {
            using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
            var result = engine.Evaluate(script, "callback-load.flow");
            if (!result.Succeeded) throw new InvalidOperationException(engine.ErrorReporter.FormatErrors());
            var asset = File.ReadAllBytes(path);
            _ = SHA256.HashData(asset);
            iterations++;
            if (iterations % 8 == 0) GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        }
        return new(iterations, iterations * 4 * 1024 * 1024,
            [GC.CollectionCount(0) - before[0], GC.CollectionCount(1) - before[1], GC.CollectionCount(2) - before[2]]);
    }
    finally { File.Delete(path); }
}
internal sealed record LoadResult(long Iterations, long AssetBytesRead, int[] GcCollections);

internal static class DiagnosticClock
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec { public nint Seconds; public nint Nanoseconds; }
    [DllImport("libc", EntryPoint = "clock_gettime", SetLastError = true)]
    private static extern int ClockGetTime(int clock, out Timespec value);
    public static long ReadMonotonicNs() => ReadNs(1);
    public static long ReadRealtimeNs() => ReadNs(0);
    private static long ReadNs(int clock)
    {
        if (ClockGetTime(clock, out var time) != 0) throw new InvalidOperationException("Diagnostic clock unavailable");
        return checked((long)time.Seconds * 1_000_000_000 + (long)time.Nanoseconds);
    }
}
