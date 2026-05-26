using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace FlowLang.Audio;

/// <summary>
/// Phase 48 Plan 48-03 — C#↔JS [JSImport] boundary for the WebAudio backend.
///
/// Five static-partial method declarations bind <see cref="WebAudioBackend"/>
/// against the matching JS-side exports that <c>flow-lang/wasm/flow-runtime.js</c>
/// (Plan 48-04) wires via <c>setModuleImports('flow-runtime', ...)</c>:
///
/// <list type="bullet">
///   <item><c>createAudioContext</c>   — creates / returns the per-engine <c>AudioContext</c>.</item>
///   <item><c>playStereoFloat32</c>    — one-shot <c>Float32Array</c> marshal → <c>AudioBufferSourceNode.start()</c>.</item>
///   <item><c>stopSource</c>           — revokes an active <c>AudioBufferSourceNode</c>.</item>
///   <item><c>closeContext</c>         — closes the <c>AudioContext</c> and clears tracked sources.</item>
///   <item><c>resumeContext</c>        — D-48-09 escape hatch; Phase 49 calls inside user-gesture chain.</item>
/// </list>
///
/// <para>Design references:</para>
/// <list type="bullet">
///   <item>D-48-06: <c>[JSImport]</c>/<c>[JSExport]</c> chosen over Blazor's <c>Microsoft.JSInterop</c>
///     — modern .NET 10 interop surface, type-safe at compile time, BCL-provided so the file
///     compiles on every target (the attribute itself is just method-binding metadata).</item>
///   <item>D-48-07: stereo promotion happens in <see cref="WebAudioBackend"/> BEFORE this layer.
///     <c>PlayStereoFloat32</c> always receives interleaved stereo Float32 — JS-side has no
///     branching on channel count.</item>
///   <item>D-48-09: <c>ResumeContext</c> is callable from this binding but
///     <see cref="WebAudioBackend.Play"/> NEVER calls it — autoplay-policy
///     <c>resume()</c> is the playground's user-gesture responsibility in Phase 49.</item>
///   <item>RESEARCH §5: marshalling multi-MB <c>Float32Array</c> across <c>[JSImport]</c> is
///     one-shot and fast. The <c>[JSMarshalAs&lt;JSType.MemoryView&gt;] Span&lt;float&gt;</c>
///     annotation avoids per-buffer streaming-interop latency.</item>
/// </list>
///
/// <para>The <c>"flow-runtime"</c> module-name string MUST match the
/// <c>setModuleImports('flow-runtime', ...)</c> call Plan 48-04 wires in
/// <c>flow-runtime.js</c>. Plan 48-03 commits to this name; Plan 48-04 honors it.</para>
///
/// <para><c>[SupportedOSPlatform("browser")]</c> emits CA1416 at every call site
/// invoked WITHOUT a <c>OperatingSystem.IsBrowser()</c> runtime guard — guides
/// <see cref="WebAudioBackend"/> to wrap interop calls in the documented Pattern B
/// runtime branch (per 48-PATTERNS.md). On Desktop the class compiles cleanly;
/// invocation throws via the BCL's platform-not-supported guard.</para>
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class FlowRuntimeInterop
{
    /// <summary>
    /// Creates (or returns the existing) <c>AudioContext</c> at the given sample rate.
    /// D-48-08: JS-side holds one <c>AudioContext</c> per browser tab — the JS module
    /// caches the instance and returns the cached handle on subsequent calls.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (e.g. 44100, 48000).</param>
    /// <returns>Opaque <see cref="JSObject"/> handle to the JS-side <c>AudioContext</c>.</returns>
    [JSImport("createAudioContext", "flow-runtime")]
    internal static partial JSObject CreateAudioContext(int sampleRate);

    /// <summary>
    /// One-shot Float32Array marshal: copies <paramref name="samplesAsBytes"/> into
    /// a new <c>AudioBuffer</c>, wires a fresh <c>AudioBufferSourceNode</c> to
    /// <c>ctx.destination</c>, and calls <c>node.start()</c>. Returns the
    /// source-node handle so <see cref="StopSource"/> can revoke it later.
    /// </summary>
    /// <remarks>
    /// <para>The source-generated JS interop (<c>SYSLIB1072</c>) only supports
    /// <c>Span&lt;byte&gt;</c> / <c>Span&lt;int&gt;</c> / <c>Span&lt;double&gt;</c> for
    /// <c>[JSMarshalAs&lt;JSType.MemoryView&gt;]</c> — <c>Span&lt;float&gt;</c> is not
    /// directly supported. We marshal the <c>float[]</c> as its raw byte view
    /// (via <c>MemoryMarshal.AsBytes</c> at the WebAudioBackend callsite) and
    /// reinterpret on the JS side as <c>new Float32Array(bytes.buffer,
    /// bytes.byteOffset, byteLength / 4)</c>. The marshalled view is
    /// zero-copy across the boundary per RESEARCH §5.</para>
    /// <para>D-48-07 invariant: the C# caller (<see cref="WebAudioBackend.Play"/>) ALWAYS
    /// promotes mono → stereo BEFORE marshalling. JS-side has no branching on channel
    /// count; <paramref name="channels"/> is always 2 in v1 (passed for forward
    /// compatibility if D-48-07 is revisited in v1.6).</para>
    /// </remarks>
    /// <param name="ctx">The <c>AudioContext</c> from <see cref="CreateAudioContext"/>.</param>
    /// <param name="samplesAsBytes">Interleaved stereo Float32 samples viewed as raw
    /// bytes (4 bytes per sample, little-endian). D-48-07 PRE-PROMOTED in C#.</param>
    /// <param name="channels">Channel count — always 2 by the D-48-07 invariant.</param>
    /// <param name="sampleRate">Sample rate of the underlying Float32 samples.</param>
    /// <returns>Opaque <see cref="JSObject"/> handle to the active <c>AudioBufferSourceNode</c>.</returns>
    [JSImport("playStereoFloat32", "flow-runtime")]
    internal static partial JSObject PlayStereoFloat32(
        JSObject ctx,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> samplesAsBytes,
        int channels,
        int sampleRate);

    /// <summary>
    /// Revokes an active <c>AudioBufferSourceNode</c> (calls <c>node.stop()</c>
    /// and removes it from the JS-side <c>_activeSources</c> set). Idempotent —
    /// JS catches the "already stopped" exception charitably.
    /// </summary>
    [JSImport("stopSource", "flow-runtime")]
    internal static partial void StopSource(JSObject sourceNode);

    /// <summary>
    /// Closes the <c>AudioContext</c> and stops every tracked source node.
    /// Called from <see cref="WebAudioBackend.Dispose"/> at engine teardown.
    /// </summary>
    [JSImport("closeContext", "flow-runtime")]
    internal static partial void CloseContext(JSObject ctx);

    /// <summary>
    /// D-48-09: callable BUT <see cref="WebAudioBackend"/> NEVER invokes this.
    /// The autoplay policy requires <c>AudioContext.resume()</c> to fire inside
    /// the user-gesture chain (e.g. the playground "Run" button's
    /// <c>onclick</c> handler). Phase 49 wires that responsibility; this
    /// binding exists so the runtime API can expose it via a separate
    /// <c>[JSExport]</c> path.
    /// </summary>
    [JSImport("resumeContext", "flow-runtime")]
    internal static partial void ResumeContext(JSObject ctx);
}
