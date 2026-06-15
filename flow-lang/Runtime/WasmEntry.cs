using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowLang.Audio;
using FlowLang.Core;
using FlowLang.Diagnostics;

namespace FlowLang.Runtime;

/// <summary>
/// Phase 48 Plan 48-04 — structured result returned from
/// <see cref="WasmEntry.RunFromJs"/>. JS receives the JSON-serialized form
/// (camelCase, null-omission); shape pinned by D-48-14.
/// </summary>
/// <remarks>
/// Field-by-field semantics:
/// <list type="bullet">
///   <item><c>Wav</c>     — reserved; currently always null from
///         <see cref="RunFromJs"/>.  The playground calls
///         <see cref="PlayFromJs"/> directly when a script invokes
///         <c>(play buffer)</c>; a <c>wav</c> download path is v1.6
///         backlog.</item>
///   <item><c>Midi</c>    — encoded SMF bytes when the source called
///         <c>writeMidi</c>; populated via
///         <see cref="FlowLang.StandardLibrary.Audio.MidiExport.DrainInMemorySink"/>
///         after each run (§5.4 in-memory hook, D-48-17/D-48-18).
///         Null when no MIDI was emitted.</item>
///   <item><c>Stdout</c>  — captured <c>Console.Out</c> from the run
///         (<c>print</c> output per D-48-15).</item>
///   <item><c>Stderr</c>  — captured <c>Console.Error</c> from the run
///         (advisory <c>[X]</c> lines per D-48-15).</item>
///   <item><c>Errors</c>  — structured run errors (parse/eval/runtime/cancel/
///         platform-not-supported per D-48-14).</item>
///   <item><c>DurationMs</c> — wall-clock duration of the call in
///         milliseconds (measured via <see cref="Stopwatch"/>).</item>
/// </list>
/// </remarks>
public sealed class RunResult
{
    public float[]? Wav { get; init; }
    public byte[]? Midi { get; init; }
    public string Stdout { get; init; } = string.Empty;
    public string Stderr { get; init; } = string.Empty;
    public RunError[] Errors { get; init; } = Array.Empty<RunError>();
    public long DurationMs { get; init; }
}

/// <summary>
/// Phase 48 Plan 48-04 — single structured error in a <see cref="RunResult"/>.
/// Shape pinned by D-48-14. <c>Kind</c> is one of:
/// <c>"parse" | "eval" | "runtime" | "cancel" | "platform-not-supported"</c>.
/// </summary>
/// <remarks>
/// The <c>"cancel"</c> kind remains DEFINED in the D-48-14 contract (field
/// names + kinds are PINNED — JS and tests parse them directly), but it is
/// NOT raised by <see cref="WasmEntry.RunFromJs"/> in single-threaded WASM:
/// the D-48-10 hard 30s wall-clock cap is unenforceable by blocking on a
/// single-threaded runtime (see <see cref="WasmEntry"/> remarks for the
/// debug-session amendment).
/// </remarks>
/// <param name="Kind">Error category (see remarks above).</param>
/// <param name="Message">Human-readable message; no .NET stack traces leak
/// across the JS boundary per T-48-15 mitigation.</param>
/// <param name="Line">1-based line number when known; null otherwise.</param>
/// <param name="Column">1-based column number when known; null otherwise.</param>
/// <param name="SourceSnippet">Quoted source line for Rust-style diagnostic
/// rendering on the playground UI side; null when no snippet is available.</param>
public sealed record RunError(
    string Kind,
    string Message,
    int? Line,
    int? Column,
    string? SourceSnippet);

/// <summary>
/// Phase 48 Plan 48-06 — source-generated <see cref="JsonSerializerContext"/>
/// for the <see cref="RunResult"/> / <see cref="RunError"/> shape.
/// </summary>
/// <remarks>
/// <para><b>Why source-gen (D-48-06 / debug session wasm-boot-no-app-bundle):</b>
/// the <c>FlowTarget=Web</c> publish sets <c>&lt;TrimMode&gt;full&lt;/TrimMode&gt;</c>,
/// which disables System.Text.Json's reflection-based serializer in the trimmed
/// WASM build. A plain <c>JsonSerializer.Serialize(obj, options)</c> call therefore
/// throws <c>JsonSerializerIsReflectionDisabled</c> at runtime in the browser —
/// even though the identical call succeeds in the Desktop in-process test runner
/// (where reflection-based JSON is enabled). Source generation emits the
/// serialization metadata at compile time, sidestepping reflection entirely,
/// keeping the trimmed graph lean (no IL2026/IL3050), and producing the same
/// camelCase + null-omission output the D-48-14/15 JSON shape pins.</para>
/// <para><b>Shape pin (D-48-14/15):</b> <see cref="JsonSourceGenerationOptionsAttribute"/>
/// replicates the retired reflection-based options exactly —
/// <c>PropertyNamingPolicy = CamelCase</c> (so <c>Stdout</c> → <c>stdout</c>,
/// <c>DurationMs</c> → <c>durationMs</c>) and
/// <c>DefaultIgnoreCondition = WhenWritingNull</c> (so null <c>wav</c> / <c>midi</c>
/// are omitted). Field names + casing MUST NOT drift — JS (and
/// <c>WasmDeterminismTests</c>) parse these keys directly.</para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RunResult))]
[JsonSerializable(typeof(RunError))]
internal partial class FlowWasmJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Phase 48 Plan 48-04 — JS-callable entry-point surface. The companion
/// of <see cref="FlowLang.Audio.FlowRuntimeInterop"/>: where
/// <c>FlowRuntimeInterop</c> declares <c>[JSImport]</c> bindings the
/// runtime calls INTO JS, <c>WasmEntry</c> declares <c>[JSExport]</c>
/// methods JS calls INTO the runtime.
///
/// <para>The four exports freeze the D-48-13 API surface that
/// <c>flow-lang/wasm/flow-runtime.js</c> reaches via
/// <c>getAssemblyExports</c>:</para>
///
/// <list type="bullet">
///   <item><see cref="RunFromJs"/>    — execute a Flow source string and
///         return a JSON-serialized <see cref="RunResult"/>.</item>
///   <item><see cref="PlayFromJs"/>   — push a Float32 PCM array into the
///         shared <see cref="WebAudioBackend"/>.</item>
///   <item><see cref="StopFromJs"/>   — revoke any active source node.</item>
///   <item><see cref="DisposeFromJs"/> — tear down the shared backend +
///         engine. Idempotent.</item>
/// </list>
///
/// <para>Charitable contract (D-v1.5-05 + T-48-15 mitigation): every public
/// method catches <see cref="Exception"/> internally and returns / swallows
/// safely. NO uncaught exception EVER propagates across the <c>[JSExport]</c>
/// boundary — the JS side sees structured errors in <see cref="RunResult.Errors"/>,
/// never raw .NET internals.</para>
///
/// <para>D-48-15 stdout/stderr split: <see cref="RunFromJs"/> redirects
/// both streams to per-call <see cref="StringWriter"/> sinks via
/// <see cref="Console.SetOut"/> / <see cref="Console.SetError"/>, then restores
/// the prior streams in a <c>finally</c> block (T-48-14 mitigation —
/// restoration guaranteed even on exception path).</para>
///
/// <para><b>D-48-10 30-second wall-clock cap — AMENDED (debug session
/// wasm-boot-no-app-bundle, cycle 3, 2026-05-30):</b> the cap is a HARD wall
/// on Desktop only and is <b>best-effort (non-preemptive) in single-threaded
/// WASM</b>. <see cref="RunFromJs"/> calls <see cref="FlowEngine.Execute"/>
/// <b>synchronously on the calling (main) thread</b>. The previous
/// <c>Task.Run + Wait(TimeSpan.FromSeconds(30))</c> shape (Pattern C, carried
/// over from Phase 38 LIVE-02 where a real Desktop thread pool exists)
/// DEADLOCKS under Mono-WASM, which is single-threaded by default
/// (dotnet/runtime#85592): <c>Task.Run</c> queues the work to the one main
/// thread and <c>Wait</c> then blocks that same thread, so <c>Execute</c> never
/// runs and every call timed out at exactly 30s. A hard cap is fundamentally
/// unenforceable by blocking in a single-threaded runtime (no preemption). The
/// accepted tradeoff: a runaway Flow script hangs its own browser tab exactly
/// like any synchronous single-threaded JS — the composer controls their own
/// script, this matches the browser execution model, and ergonomics-first wins.
/// The <c>"cancel"</c> RunError kind stays DEFINED in the D-48-14 contract
/// (field names + kinds are PINNED) but is no longer raised here. The Plan
/// 48-07 closer / 48-VERIFICATION.md should record D-48-10 as "hard cap on
/// Desktop, best-effort (synchronous, non-preemptive) in single-threaded WASM".</para>
///
/// <para>D-48-09 contract: <see cref="RunFromJs"/> does NOT call
/// <c>resumeContext</c> on the AudioContext. The autoplay-policy
/// <c>resume()</c> is the playground's user-gesture responsibility (the
/// dev harness <c>index.html</c> and Phase 49 SvelteKit wire it inside
/// the same click handler that calls <c>runtime.run(source)</c>).</para>
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class WasmEntry
{
    private static readonly object _lock = new();
    private static WebAudioBackend? _sharedBackend;
    private static FlowEngine? _sharedEngine;

    /// <summary>
    /// Last run's encoded SMF bytes, cached so the JS consumer can pull a REAL
    /// <c>Uint8Array</c> via <see cref="GetLastMidiBytes"/> instead of the
    /// base64 STRING that System.Text.Json emits for <c>byte[]</c> inside the
    /// serialized <see cref="RunResult"/> (sweep-0614 wasm-web). Set at the end
    /// of every <see cref="RunFromJs"/> call (null when the run emitted no MIDI).
    /// </summary>
    private static byte[]? _lastMidiBytes;

    /// <summary>
    /// Builds a FRESH per-run <see cref="FlowEngine"/> (under lock), disposing
    /// any previous one. sweep-0614 wasm-web: <see cref="FlowEngine.Execute"/>
    /// runs every statement against a PERSISTENT global scope + section
    /// registry — so re-running a script with a top-level <c>Buffer x = ...</c>
    /// / <c>Song s = [...]</c> / <c>section ...</c> through a reused engine
    /// throws "Variable X already declared" / reports "Section X is already
    /// defined" on the SECOND run. The common playground loop (edit → click Run
    /// again) hit this on every declaring script. A fresh engine re-runs the
    /// constructor's full stdlib bootstrap (@std import + StyleRegistry shipped/
    /// user packs) and starts with a clean GlobalFrame + empty SectionRegistry —
    /// exactly the semantics WasmContractTests emulated with DisposeFromJs()
    /// before each RunFromJs(). A plain "clear GlobalFrame" reset would wipe the
    /// stdlib bindings the constructor loads, so fresh-engine-per-run is the
    /// correct (and cheapest-to-reason-about) reset path.
    /// </summary>
    private static FlowEngine NewEngineForRun()
    {
        lock (_lock)
        {
            if (_sharedEngine != null)
            {
                try { _sharedEngine.Dispose(); }
                catch (Exception ex) { Console.Error.WriteLine($"[runtime] engine recycle: {ex.Message}"); }
                _sharedEngine = null;
            }
            _sharedEngine = new FlowEngine(verbose: false);
            return _sharedEngine;
        }
    }

    /// <summary>
    /// Lazy-init (under lock) the shared per-process <see cref="FlowEngine"/>.
    /// Mono-WASM runs single-threaded by default, but the lock is cheap and
    /// guards against future v1.6 multi-threaded WASM (dotnet/runtime#85592).
    /// Used by the playback / stop paths that must reach the SAME engine
    /// <see cref="RunFromJs"/> last executed against (so script <c>(play ...)</c>
    /// audio can be stopped). <see cref="RunFromJs"/> itself uses
    /// <see cref="NewEngineForRun"/> for a clean scope per run.
    /// </summary>
    private static FlowEngine GetEngine()
    {
        if (_sharedEngine != null) return _sharedEngine;
        lock (_lock)
        {
            _sharedEngine ??= new FlowEngine(verbose: false);
            return _sharedEngine;
        }
    }

    /// <summary>
    /// Lazy-init (under lock) the shared <see cref="WebAudioBackend"/>.
    /// Mirrors <see cref="GetEngine"/>'s lock posture.
    /// </summary>
    private static WebAudioBackend GetBackend()
    {
        if (_sharedBackend != null) return _sharedBackend;
        lock (_lock)
        {
            _sharedBackend ??= new WebAudioBackend();
            return _sharedBackend;
        }
    }

    /// <summary>
    /// Maps <see cref="FlowError"/> entries from
    /// <see cref="ErrorReporter.Errors"/> to the D-48-14 structured
    /// <see cref="RunError"/> shape JS consumes.
    /// </summary>
    /// <remarks>
    /// The <see cref="FlowError"/> shape carries a single
    /// <see cref="DiagnosticLevel"/> (Info/Warning/Error) but not a parse
    /// vs. eval vs. runtime category. The mapping below is conservative:
    /// any Error-level FlowError becomes kind=<c>"eval"</c> — the catch-all
    /// for "the script could not run to completion". The top-level catch site
    /// in <see cref="RunFromJs"/> emits kind=<c>"runtime"</c> for uncaught
    /// host-side exceptions; kind=<c>"parse"</c> is reserved for future
    /// per-stage tagging when the ErrorReporter grows a category field (v1.6
    /// backlog). kind=<c>"cancel"</c> stays DEFINED in the D-48-14 contract but
    /// is no longer raised — the 30s cap is non-preemptive in single-threaded
    /// WASM (see <see cref="WasmEntry"/> remarks, debug-session amendment).
    /// </remarks>
    private static RunError[] MapFlowErrors(IEnumerable<FlowError> errors, SourceMap? sourceMap = null)
    {
        if (errors == null) return Array.Empty<RunError>();
        return errors
            .Where(e => e.Level == DiagnosticLevel.Error)
            .Select(e =>
            {
                int? line = e.Location?.Line > 0 ? e.Location.Line : null;
                return new RunError(
                    Kind: "eval",
                    Message: e.Message ?? string.Empty,
                    Line: line,
                    Column: e.Location?.Column > 0 ? e.Location.Column : null,
                    SourceSnippet: SnippetFor(sourceMap, line));
            })
            .ToArray();
    }

    /// <summary>
    /// sweep-0614 wasm-web: quote the offending source line for the playground's
    /// Rust-style diagnostic box. <see cref="FlowEngine.Execute"/> registers the
    /// full source under the <c>"&lt;wasm&gt;"</c> key (the <c>fileName</c> passed
    /// from <see cref="RunFromJs"/>), so the line is reachable via
    /// <see cref="SourceMap.TryGetSource"/>. Returns null when no source map, no
    /// line, or the line is out of range — matching the documented "null when no
    /// snippet is available" semantics on <see cref="RunError.SourceSnippet"/>.
    /// </summary>
    private static string? SnippetFor(SourceMap? sourceMap, int? line)
    {
        if (sourceMap == null || line is not int ln || ln < 1) return null;
        if (!sourceMap.TryGetSource(WasmSourceKey, out var src) || string.IsNullOrEmpty(src))
            return null;
        var lines = src.Split('\n');
        if (ln > lines.Length) return null;
        // Trim a trailing CR so CRLF-authored snippets render cleanly.
        return lines[ln - 1].TrimEnd('\r');
    }

    /// <summary>Source-map key <see cref="RunFromJs"/> registers the run source under.</summary>
    private const string WasmSourceKey = "<wasm>";

    /// <summary>
    /// Executes a Flow source string and returns a JSON-serialized
    /// <see cref="RunResult"/> per D-48-14. Charitable on every error path —
    /// the JS caller ALWAYS receives a valid JSON string.
    /// </summary>
    /// <remarks>
    /// Runs <see cref="FlowEngine.Execute"/> SYNCHRONOUSLY on the calling
    /// thread. Mono-WASM is single-threaded by default, so offloading to a
    /// worker task and blocking on it (the prior Pattern C shape) deadlocks —
    /// see the <see cref="WasmEntry"/> D-48-10 amendment. The 30s wall-clock cap
    /// is therefore best-effort / non-preemptive in-browser (a runaway script
    /// hangs its own tab, exactly like synchronous single-threaded JS).
    /// </remarks>
    /// <param name="source">Flow source code (composer-authored).</param>
    /// <returns>JSON-serialized <see cref="RunResult"/> with camelCase property
    /// names and null-omission for <c>wav</c> / <c>midi</c>.</returns>
    [JSExport]
    public static string RunFromJs(string source)
    {
        var stopwatch = Stopwatch.StartNew();
        var stdoutCapture = new StringWriter();
        var stderrCapture = new StringWriter();

        // sweep-0614 regression-wasm-determinism: build the FRESH per-run engine
        // BEFORE redirecting Console. NewEngineForRun re-runs the full @std +
        // style-pack bootstrap (a non-trivial amount of work); the constructor
        // emits ZERO Console.Out/Error output the run needs to capture (verified —
        // ctor stdout/stderr length is 0). Keeping that bootstrap OUTSIDE the
        // Console-redirect window shrinks the window to just engine.Execute, so
        // the process-global Console.SetOut/SetError redirect is held for the
        // minimum time — narrowing the cross-test race that an in-window bootstrap
        // (the 1f31a5e shape) widened enough to flip RunResult stdout/stderr to
        // empty under a parallel runner (D-48-16 two-run cmp-clean). The
        // assembly-level parallelizeTestCollections=false in the test project is
        // the hard guarantee; this is the defense-in-depth narrowing.
        FlowEngine engine;
        Exception? engineBuildError = null;
        try { engine = NewEngineForRun(); }
        catch (Exception ex)
        {
            // Engine construction failed before any redirect — surface as a
            // structured runtime error without ever having touched Console.
            engine = null!;
            engineBuildError = ex;
        }

        var prevOut = Console.Out;
        var prevErr = Console.Error;

        Console.SetOut(stdoutCapture);
        Console.SetError(stderrCapture);

        // §5.4 two-run cmp-clean: clear any MIDI bytes from a previous run
        // BEFORE execution so the sink always reflects THIS run only.
        FlowLang.StandardLibrary.Audio.MidiExport.DrainInMemorySink();

        // sweep-0614 wasm-web (D-48-16): WarnOnce dedups on a process-static set.
        // The long-lived WASM runtime would suppress an advisory on the SECOND
        // run of identical source — so RunResult.stderr would DIFFER across two
        // runs, breaking the "same source → byte-identical RunResult" contract.
        // Reset per-run (mirrors the per-run MIDI-sink drain above) so the
        // advisory channel is run-scoped, not process-scoped.
        FlowLang.Diagnostics.RenderingDiagnostics.ResetForTesting();

        RunError[] errors;
        byte[]? midiBytes;
        try
        {
            try
            {
                if (engineBuildError != null)
                    throw engineBuildError;
                // sweep-0614 wasm-web: a FRESH engine per run gives a clean
                // GlobalFrame + empty SectionRegistry for this run's top-level
                // declarations (built above, outside the Console window). A reused
                // engine threw "already declared" on the second run of any
                // declaring script (the common edit→Run loop).
                // D-48-10 (AMENDED, debug session wasm-boot-no-app-bundle cycle 3):
                // run SYNCHRONOUSLY on the calling thread. Mono-WASM is single-
                // threaded by default — the prior Task.Run + Wait(30s) shape
                // deadlocked (Task.Run queues to the one main thread, Wait then
                // blocks it, so Execute never ran → every call timed out at 30s).
                // The hard 30s cap is unenforceable without preemption; in-browser
                // it is best-effort (a runaway script hangs its own tab, like any
                // synchronous single-threaded JS). The "cancel" RunError kind stays
                // DEFINED (D-48-14 contract) but is no longer raised here.
                engine.Execute(source ?? string.Empty, WasmSourceKey);
                // sweep-0614 wasm-web: thread the engine SourceMap so parse /
                // runtime errors carry the quoted source line for the
                // playground's Rust-style diagnostic box (D-48-14).
                errors = MapFlowErrors(engine.ErrorReporter.Errors, engine.SourceMap);
            }
            catch (Exception ex)
            {
                // T-48-15 mitigation: only ex.Message — no stack traces leak.
                errors = new[]
                {
                    new RunError(
                        Kind: "runtime",
                        Message: ex.Message ?? ex.GetType().Name,
                        Line: null,
                        Column: null,
                        SourceSnippet: null),
                };
            }
        }
        finally
        {
            // T-48-14 mitigation: stream restoration ALWAYS runs, even on
            // exception path. Without this an internal throw would leave the
            // process-wide Console.Out pointed at our StringWriter forever.
            Console.SetOut(prevOut);
            Console.SetError(prevErr);
        }

        // §5.4 — drain after Execute so midi is set for THIS run.
        midiBytes = FlowLang.StandardLibrary.Audio.MidiExport.DrainInMemorySink();

        // sweep-0614 wasm-web: cache for GetLastMidiBytes() so the JS consumer
        // can pull a REAL Uint8Array. System.Text.Json serializes the Midi byte[]
        // inside RunResult as a Base64 STRING (not a number array) — so a
        // consumer doing `new Blob([result.midi])` would write the literal
        // base64 text and produce a corrupt .mid. The byte[] stays in the JSON
        // for back-compat, but GetLastMidiBytes() is the typed-array path.
        lock (_lock) { _lastMidiBytes = midiBytes; }

        stopwatch.Stop();

        var result = new RunResult
        {
            Wav = null,
            Midi = midiBytes,
            Stdout = stdoutCapture.ToString(),
            Stderr = stderrCapture.ToString(),
            Errors = errors,
            DurationMs = stopwatch.ElapsedMilliseconds,
        };

        try
        {
            // D-48-06 / debug wasm-boot-no-app-bundle: serialize through the
            // source-generated context, NOT the reflection-based serializer.
            // TrimMode=full on the Web target disables reflection-based JSON;
            // the generated metadata (camelCase + null-omission) sidesteps it
            // and keeps the D-48-14/15 shape byte-stable.
            return JsonSerializer.Serialize(result, FlowWasmJsonContext.Default.RunResult);
        }
        catch (Exception ex)
        {
            // Last-resort guard: even the serializer must not throw across the
            // boundary. Return a minimal hand-rolled JSON shape that the JS
            // caller can still JSON.parse.
            return "{\"wav\":null,\"midi\":null,\"stdout\":\"\",\"stderr\":\"\",\"errors\":[{\"kind\":\"runtime\",\"message\":\"" +
                   (ex.Message ?? "JSON serialization failed").Replace("\"", "'") +
                   "\",\"line\":null,\"column\":null,\"sourceSnippet\":null}],\"durationMs\":0}";
        }
    }

    /// <summary>
    /// sweep-0614 wasm-web: returns the LAST run's encoded SMF bytes as a real
    /// <c>byte[]</c>, which marshals across the <c>[JSExport]</c> boundary as a
    /// JS <c>Uint8Array</c> — the type the D-48-18 contract + the
    /// <c>flow-runtime.js</c> typedef advertise for <c>RunResult.midi</c>.
    ///
    /// <para><b>Why this exists:</b> <see cref="RunResult.Midi"/> is a
    /// <c>byte[]</c>, and System.Text.Json (including the source-generated
    /// <see cref="FlowWasmJsonContext"/>) serializes <c>byte[]</c> as a
    /// Base64-encoded STRING — never a JSON number array. So <c>result.midi</c>
    /// arrives JS-side as a base64 string; a consumer doing
    /// <c>new Blob([result.midi])</c> writes the literal base64 TEXT and produces
    /// a corrupt, unplayable <c>.mid</c>. This getter hands the consumer the raw
    /// bytes directly so the download Blob is correct.</para>
    ///
    /// <para><b>Owner action (frozen-runtime constraint):</b>
    /// <c>flow-lang/wasm/flow-runtime.js</c> is frozen (Phase 48/49); its
    /// <c>run()</c> does only <c>JSON.parse</c> and never calls this getter, so
    /// the typed bytes only reach the consumer after the runtime/consumer is
    /// wired to prefer <c>getLastMidiBytes()</c> over the base64 <c>result.midi</c>.
    /// Until then, the consumer wrapper must base64-decode <c>result.midi</c>
    /// (<c>Uint8Array.from(atob(midi), c =&gt; c.charCodeAt(0))</c>).</para>
    ///
    /// <para>Returns an empty array (never null) when the last run emitted no
    /// MIDI, so the JS side always receives a typed array. Never throws across
    /// the boundary.</para>
    /// </summary>
    [JSExport]
    public static byte[] GetLastMidiBytes()
    {
        try
        {
            lock (_lock) { return _lastMidiBytes ?? Array.Empty<byte>(); }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[runtime] GetLastMidiBytes: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    /// <summary>
    /// Pushes a Float32 PCM array into the shared <see cref="WebAudioBackend"/>.
    /// Charitable on every error path — never throws across the JS boundary.
    /// </summary>
    /// <remarks>
    /// <para><b>SYSLIB1072 workaround:</b> source-generated JS interop does not
    /// support <c>float[]</c> for <c>[JSExport]</c> — only <c>double[]</c>,
    /// <c>int[]</c>, <c>byte[]</c>, and string/primitive scalars. We accept the
    /// PCM samples as <c>byte[]</c> (the JS-side <c>Float32Array</c>'s
    /// underlying <c>ArrayBuffer</c> reinterpreted as bytes) and cast to
    /// <c>float[]</c> server-side via <see cref="MemoryMarshal.Cast{TFrom,TTo}"/>
    /// — zero-copy on the C# side, same posture as Plan 48-03's
    /// <c>PlayStereoFloat32</c> [JSImport] marshalling.</para>
    /// <para>JS-side caller does:
    /// <c>const u8 = new Uint8Array(float32.buffer, float32.byteOffset, float32.byteLength);
    /// exports.FlowLang.Runtime.WasmEntry.PlayFromJs(u8, sampleRate, channels);</c>
    /// — the boundary stays zero-copy through the same memory.</para>
    /// </remarks>
    /// <param name="wavBytes">Interleaved Float32 PCM samples viewed as raw
    /// bytes (4 bytes per sample, little-endian). Mono input is promoted to
    /// stereo inside the backend per D-48-07.</param>
    /// <param name="sampleRate">Sample rate in Hz (e.g. 44100).</param>
    /// <param name="channels">Channel count (1 for mono, 2 for stereo).</param>
    [JSExport]
    public static void PlayFromJs(byte[] wavBytes, int sampleRate, int channels)
    {
        try
        {
            if (wavBytes == null || wavBytes.Length == 0) return;
            if (sampleRate <= 0 || channels < 1) return;

            // Zero-copy reinterpret bytes → float[]. The byte buffer carries
            // interleaved Float32 samples; the cast reads 4 bytes per float.
            // If the byte length is not a multiple of 4, the trailing partial
            // sample is silently dropped (charitable; the WebAudio destination
            // would reject mismatched input anyway).
            var floatSpan = MemoryMarshal.Cast<byte, float>(wavBytes);
            // Backend.Play needs a float[] for its existing IAudioBackend
            // signature. Allocate once (cheap relative to playback) — the
            // alternative (changing IAudioBackend.Play to accept ReadOnlySpan)
            // is a v1.6 surface change.
            var wav = new float[floatSpan.Length];
            floatSpan.CopyTo(wav);

            var backend = GetBackend();
            backend.EnsureInitialized(sampleRate, channels);
            backend.Play(wav, sampleRate, channels, default);
        }
        catch (JSException ex)
        {
            // T-48-11 carryforward — log only; never propagate JS internals.
            Console.Error.WriteLine($"[runtime] PlayFromJs: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[runtime] PlayFromJs: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops all active audio source nodes — both the shared playground backend
    /// (created by <see cref="PlayFromJs"/>) and the engine-owned backend that
    /// handles script-level <c>(play ...)</c> calls.
    ///
    /// <para>§5.11: the original implementation only stopped <c>_sharedBackend</c>,
    /// which is created by <c>runtime.play()</c>.  The playground never calls
    /// <c>runtime.play()</c> directly (because <c>RunResult.wav</c> is null from
    /// <see cref="RunFromJs"/>); script <c>(play buffer)</c> invocations route
    /// through the engine's OWN <see cref="WebAudioBackend"/> instance obtained
    /// from <see cref="FlowEngine.AudioManager"/>. Stopping only the shared backend
    /// left script audio playing indefinitely after the playground Stop button click.
    /// This overload now stops BOTH backends so the Stop button actually works.</para>
    ///
    /// <para>Idempotent — safe to call before any playback has been started.
    /// Never throws across the JS boundary.</para>
    /// </summary>
    [JSExport]
    public static void StopFromJs()
    {
        try
        {
            // §5.11 — stop the shared backend (runtime.play() path).
            _sharedBackend?.Stop();

            // §5.11 — stop the engine's own backend (script (play ...) path).
            // GetEngine() is safe here: if the engine hasn't been created yet
            // there is nothing playing; the lazy-init is cheap.
            try
            {
                _sharedEngine?.AudioManager.StopPlayback();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[runtime] StopFromJs engine backend: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[runtime] StopFromJs: {ex.Message}");
        }
    }

    /// <summary>
    /// Tears down the shared backend + engine and clears both references.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    [JSExport]
    public static void DisposeFromJs()
    {
        try
        {
            lock (_lock)
            {
                try { _sharedBackend?.Dispose(); }
                catch (Exception ex) { Console.Error.WriteLine($"[runtime] DisposeFromJs backend: {ex.Message}"); }

                try { _sharedEngine?.Dispose(); }
                catch (Exception ex) { Console.Error.WriteLine($"[runtime] DisposeFromJs engine: {ex.Message}"); }

                _sharedBackend = null;
                _sharedEngine = null;
                _lastMidiBytes = null;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[runtime] DisposeFromJs: {ex.Message}");
        }
    }
}
