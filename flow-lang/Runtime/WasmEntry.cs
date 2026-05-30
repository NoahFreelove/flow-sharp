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
using System.Threading.Tasks;
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
///   <item><c>Wav</c>     — rendered Float32 PCM if the source produced one
///         (Phase 49 wires download / playback UI). Null when no buffer
///         was rendered.</item>
///   <item><c>Midi</c>    — encoded SMF bytes if the source called
///         <c>writeMidi</c> via the in-memory hook (Phase 49 wires download
///         UI per D-48-18). Null when no MIDI was emitted.</item>
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
/// <para>D-48-10 30-second wall-clock cap: <see cref="RunFromJs"/> wraps
/// <see cref="FlowEngine.Execute"/> in <c>Task.Run + Wait(TimeSpan.FromSeconds(30))</c>
/// (Pattern C — same shape as Phase 38 LIVE-02 LiveReloadManager.cs:82,470-499).
/// On timeout, returns a RunResult with a single
/// <see cref="RunError"/> of kind <c>"cancel"</c>; the worker continues
/// running as an orphan per RESEARCH §E Option A.</para>
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

    /// <summary>D-48-10 — 30-second wall-clock cap on a single Execute call.</summary>
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Lazy-init (under lock) the shared per-process <see cref="FlowEngine"/>.
    /// Mono-WASM runs single-threaded by default, but the lock is cheap and
    /// guards against future v1.6 multi-threaded WASM (dotnet/runtime#85592).
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
    /// for "the script could not run to completion". Top-level catch sites
    /// in <see cref="RunFromJs"/> emit kind=<c>"runtime"</c> for uncaught
    /// host-side exceptions, kind=<c>"cancel"</c> for the 30s timeout, and
    /// kind=<c>"parse"</c> is reserved for future per-stage tagging when
    /// the ErrorReporter grows a category field (v1.6 backlog).
    /// </remarks>
    private static RunError[] MapFlowErrors(IEnumerable<FlowError> errors)
    {
        if (errors == null) return Array.Empty<RunError>();
        return errors
            .Where(e => e.Level == DiagnosticLevel.Error)
            .Select(e => new RunError(
                Kind: "eval",
                Message: e.Message ?? string.Empty,
                Line: e.Location?.Line > 0 ? e.Location.Line : null,
                Column: e.Location?.Column > 0 ? e.Location.Column : null,
                SourceSnippet: null))
            .ToArray();
    }

    /// <summary>
    /// Executes a Flow source string and returns a JSON-serialized
    /// <see cref="RunResult"/> per D-48-14. Charitable on every error path —
    /// the JS caller ALWAYS receives a valid JSON string.
    /// </summary>
    /// <param name="source">Flow source code (composer-authored).</param>
    /// <returns>JSON-serialized <see cref="RunResult"/> with camelCase property
    /// names and null-omission for <c>wav</c> / <c>midi</c>.</returns>
    [JSExport]
    public static string RunFromJs(string source)
    {
        var stopwatch = Stopwatch.StartNew();
        var stdoutCapture = new StringWriter();
        var stderrCapture = new StringWriter();
        var prevOut = Console.Out;
        var prevErr = Console.Error;

        Console.SetOut(stdoutCapture);
        Console.SetError(stderrCapture);

        RunError[] errors;
        try
        {
            try
            {
                var engine = GetEngine();
                // Pattern C 30-second wall-clock cap — Task.Run + Wait(TimeSpan).
                // Mirrors Phase 38 LIVE-02 LiveReloadManager.cs:82,470-499. Workers
                // that exceed the cap orphan per RESEARCH §E Option A.
                var workerTask = Task.Run(() => engine.Execute(source ?? string.Empty, "<wasm>"));

                if (!workerTask.Wait(RunTimeout))
                {
                    // T-48-16 mitigation: structured cancel error; do NOT throw across the boundary.
                    errors = new[]
                    {
                        new RunError(
                            Kind: "cancel",
                            Message: "evaluation exceeded 30s cap (D-48-10)",
                            Line: null,
                            Column: null,
                            SourceSnippet: null),
                    };
                }
                else
                {
                    errors = MapFlowErrors(engine.ErrorReporter.Errors);
                }
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

        stopwatch.Stop();

        var result = new RunResult
        {
            Wav = null,
            Midi = null,
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
    /// Revokes any active audio source node. Idempotent — safe to call
    /// before <see cref="PlayFromJs"/> has ever been invoked.
    /// </summary>
    [JSExport]
    public static void StopFromJs()
    {
        try
        {
            _sharedBackend?.Stop();
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
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[runtime] DisposeFromJs: {ex.Message}");
        }
    }
}
