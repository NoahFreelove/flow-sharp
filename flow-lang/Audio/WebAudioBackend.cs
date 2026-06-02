using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace FlowLang.Audio;

/// <summary>
/// Phase 48 Plan 48-03: real Mono-WASM <c>AudioContext</c> backend.
///
/// Implements <see cref="IAudioBackend"/> for the browser via [JSImport]
/// partials declared in <see cref="FlowRuntimeInterop"/>. The class compiles
/// cleanly on both Desktop and Web targets — every interop call is gated by
/// <see cref="OperatingSystem.IsBrowser"/> (Pattern B per 48-PATTERNS.md), so
/// Desktop callers get a charitable no-op / fallback instead of a
/// platform-not-supported exception.
///
/// <para>Phase 47 D-47-05 PINNED the public surface. Phase 48 only fills
/// bodies; signatures stay byte-identical:</para>
///
/// <list type="bullet">
///   <item><see cref="Initialize"/>            — lazy-creates the per-engine <c>AudioContext</c>.</item>
///   <item><see cref="EnsureInitialized"/>     — no-op if already initialized with matching params.</item>
///   <item><see cref="Play"/>                  — promotes mono → stereo, marshals via [JSImport].</item>
///   <item><see cref="Stop"/>                  — revokes the active <c>AudioBufferSourceNode</c>.</item>
///   <item><see cref="GetDevices"/>            — returns ["default"] (WebAudio has no enumeration).</item>
///   <item><see cref="SetDevice"/>             — accepts "default" only.</item>
///   <item><see cref="WriteChunk"/>            — throws <see cref="NotSupportedException"/> per D-48-01
///         (offline-render canonical; streaming is v1.6 backlog).</item>
///   <item><see cref="Dispose"/>               — closes the <c>AudioContext</c>; never throws.</item>
/// </list>
///
/// <para>Design references:</para>
/// <list type="bullet">
///   <item>D-48-07: stereo promotion via <see cref="PromoteToStereo"/> BEFORE marshal.</item>
///   <item>D-48-08: one <c>AudioContext</c> per FlowEngine, lazy on first <see cref="Initialize"/>.</item>
///   <item>D-48-09: <see cref="Play"/> NEVER calls <c>resume()</c> — that's the playground's
///         user-gesture responsibility in Phase 49.</item>
///   <item>D-48-10 (AMENDED, debug session wasm-boot-no-app-bundle cycle 7): the 30-second
///         wall-clock cap is best-effort / NON-ENFORCEABLE in single-threaded WASM. The prior
///         <c>Task.Run + Wait(30s)</c> wrapper DEADLOCKED the one browser main thread (Task.Run
///         queues the marshal to that same thread, Wait then blocks it → the marshal never runs
///         → 30s freeze → bogus "exceeded 30s cap" advisory, no audio). Same single-threaded-WASM
///         deadlock cycle 3 fixed in <c>WasmEntry.RunFromJs</c>; this extends the synchronous
///         treatment to the playback path. <see cref="Play"/> now calls
///         <see cref="FlowRuntimeInterop.PlayStereoFloat32"/> SYNCHRONOUSLY on the calling thread.
///         The marshal is fire-and-forget anyway (it builds an <c>AudioBufferSourceNode</c> and
///         calls <c>.start()</c>, returning immediately; WebAudio plays asynchronously), so a
///         blocking-cap wrapper was never meaningful in the browser.</item>
///   <item>D-48-11: <see cref="IsAvailable"/> guards both browser host AND interop reachability.</item>
/// </list>
///
/// <para>On Desktop the class still compiles — the [JSImport] attributes are BCL-provided
/// in .NET 7+ — but every <see cref="FlowRuntimeInterop"/> callsite branches on
/// <see cref="OperatingSystem.IsBrowser"/> so the interop methods never execute there.
/// <see cref="AudioPlaybackManager.DetectBackend"/> still picks <see cref="CoreAudioBackend"/>
/// or <see cref="PulseAudioSimpleBackend"/> on Desktop per the Phase 47 D-47-06
/// probe ordering.</para>
/// </summary>
public sealed class WebAudioBackend : IAudioBackend
{
    private JSObject? _audioContext;
    private JSObject? _activeSource;
    private int _sampleRate;
    private int _channels;
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>
    /// Phase 47 D-47-07: probes for browser host via the .NET BCL's JIT
    /// intrinsic. Returns true under Mono-WASM, false on every Desktop
    /// runtime (Linux/macOS/Windows). Called from
    /// <see cref="AudioPlaybackManager.DetectBackend"/> as the FIRST probe
    /// branch — cheapest check; constant-folded on Desktop.
    /// </summary>
    public static bool IsAvailable() => OperatingSystem.IsBrowser();

    public string Name => "WebAudio";

    public bool IsInitialized => _audioContext != null && !_disposed;

    public bool Initialize(int sampleRate, int channels)
    {
        if (_disposed) return false;

        if (sampleRate <= 0)
            throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
        if (channels < 1 || channels > 8)
            throw new ArgumentException("Channel count must be between 1 and 8.", nameof(channels));

        // D-48-11 charitable fallback: not in browser → return false so
        // AudioPlaybackManager.DetectBackend can route to a different
        // backend (or NullAudioBackend if v1.6 lands it).
        if (!OperatingSystem.IsBrowser())
            return false;

        lock (_lock)
        {
            if (_audioContext != null
                && _sampleRate == sampleRate
                && _channels == channels)
            {
                return true;  // already initialized with matching params
            }

            try
            {
#pragma warning disable CA1416  // browser-only platform check — guarded by OperatingSystem.IsBrowser() above
                _audioContext = FlowRuntimeInterop.CreateAudioContext(sampleRate);
#pragma warning restore CA1416
                _sampleRate = sampleRate;
                _channels = channels;
                return _audioContext != null;
            }
            catch (JSException ex)
            {
                // T-48-11 mitigation: log to stderr only — never bubble JS internals
                // into composer-visible output.
                Console.Error.WriteLine($"WebAudio: Failed to create AudioContext: {ex.Message}");
                _audioContext = null;
                return false;
            }
        }
    }

    public void EnsureInitialized(int sampleRate, int channels)
    {
        if (_audioContext != null && _sampleRate == sampleRate && _channels == channels)
            return;
        Initialize(sampleRate, channels);
    }

    public void Play(float[] samples, int sampleRate, int channels, CancellationToken cancellationToken = default)
    {
        if (samples is null || samples.Length == 0)
            return;
        if (_disposed)
            return;

        // D-48-11 charitable fallback for Desktop: silently skip — caller's
        // contract is "best effort"; Desktop never picks this backend in
        // AudioPlaybackManager.DetectBackend, so this branch is dead code on
        // production paths but kept for test-harness safety.
        if (!OperatingSystem.IsBrowser())
            return;

        EnsureInitialized(sampleRate, channels);
        if (_audioContext == null)
            return;  // Initialize failed; advisory already logged in Initialize

        if (cancellationToken.IsCancellationRequested)
            return;

        // D-48-07: promote mono → stereo BEFORE marshal so JS-side has no
        // branching on channel count. The promoted buffer is always 2-channel.
        float[] stereo = PromoteToStereo(samples, channels);

        // D-48-10 (AMENDED — debug session wasm-boot-no-app-bundle cycle 7):
        // call PlayStereoFloat32 SYNCHRONOUSLY on the calling thread. The prior
        // Task.Run + workerTask.Wait(30s) wrapper DEADLOCKED the single browser
        // main thread (Task.Run queues the marshal to that same thread; Wait
        // then blocks it → the marshal never runs → 30s freeze → bogus
        // "exceeded 30s cap" advisory + no AudioBufferSourceNode). This is the
        // exact single-threaded-WASM deadlock cycle 3 already fixed in
        // WasmEntry.RunFromJs (commit a8c1911) for the Execute path — the same
        // treatment now extends to the playback path it missed. The marshal is
        // fire-and-forget anyway (it builds an AudioBufferSourceNode, calls
        // .start(), and returns immediately; WebAudio plays asynchronously), so
        // a blocking 30s cap was never meaningful in the browser. The 30s cap
        // becomes best-effort / non-preemptive in single-threaded WASM, exactly
        // like cycle 3's WasmEntry amendment.
#pragma warning disable CA1416  // browser-only platform check — guarded by OperatingSystem.IsBrowser() above
        // SYSLIB1072 workaround: marshal the Float32 samples as their raw byte
        // view. JS-side reinterprets via `new Float32Array(bytes.buffer,
        // bytes.byteOffset, byteLength / 4)`. Span<byte> is supported by the
        // source generator's [JSMarshalAs<JSType.MemoryView>] mapping;
        // Span<float> is not (see FlowRuntimeInterop.cs XMLdoc).
        Span<byte> samplesAsBytes = MemoryMarshal.AsBytes(stereo.AsSpan());
        Console.Error.WriteLine($"[flow-audio-cs] samples={samples.Length} channels={channels} stereo={stereo.Length} bytes={samplesAsBytes.Length}");
        _activeSource = FlowRuntimeInterop.PlayStereoFloat32(
            _audioContext, samplesAsBytes, channels: 2, sampleRate);
#pragma warning restore CA1416
    }

    public void Stop()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_activeSource == null)
                return;

            if (OperatingSystem.IsBrowser())
            {
                try
                {
#pragma warning disable CA1416  // browser-only platform check — guarded by OperatingSystem.IsBrowser() above
                    FlowRuntimeInterop.StopSource(_activeSource);
#pragma warning restore CA1416
                }
                catch (JSException)
                {
                    // Idempotent — JS-side already catches the
                    // "already stopped" exception charitably. Swallow on
                    // C# side too so Stop() is a true no-op when called
                    // after the source completed naturally.
                }
            }

            _activeSource = null;
        }
    }

    public IReadOnlyList<string> GetDevices()
    {
        // D-48-08: WebAudio exposes one AudioContext per FlowEngine; no
        // concept of device enumeration. Returning ["default"] signals the
        // AudioPlaybackManager that exactly one (unnamed) output exists.
        return new[] { "default" };
    }

    public bool SetDevice(string deviceName)
    {
        // No-op selection — WebAudio has no per-device selection API on
        // the JS side. Accept "default" charitably; reject anything else
        // so callers using PulseAudio-style device names get a clear
        // false return.
        return string.Equals(deviceName, "default", StringComparison.Ordinal);
    }

    public void WriteChunk(float[] samples, int offset, int count, int sampleRate, int channels)
    {
        // D-48-01: offline-render is canonical for WebAudio in v1. Streaming
        // interop is the latency trap we explicitly avoid — it requires the
        // SharedArrayBuffer ring-buffer + AudioWorklet pattern (D-48-02) which
        // is v1.6 backlog (frontier territory; no .NET-in-WASM precedent).
        // Callers that need WriteChunk on Web must wait for Plan 48-05+
        // streaming work; v1 composers use Play(float[]) for one-shot
        // offline render.
        throw new NotSupportedException(
            "WebAudioBackend does not support streaming WriteChunk — use Play(float[]) " +
            "for one-shot offline render. SharedArrayBuffer streaming is v1.6 backlog per D-48-02.");
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;

            try
            {
                if (_audioContext != null && OperatingSystem.IsBrowser())
                {
#pragma warning disable CA1416  // browser-only platform check — guarded by OperatingSystem.IsBrowser() above
                    FlowRuntimeInterop.CloseContext(_audioContext);
#pragma warning restore CA1416
                }
            }
            catch (JSException)
            {
                // Dispose contract: NEVER throws (Phase 47 D-47-05 + Pitfall #12
                // "live session never dies mid-set"). Swallow JS-side close
                // failures — the AudioContext is garbage-collected by the
                // browser once the C# handle drops anyway.
            }
            catch (Exception)
            {
                // Same as above — swallow ALL exceptions to honor the
                // dispose-is-noop-safe contract that
                // WebAudioBackendStubTests.Dispose_IsNoOp_DoesNotThrow pinned
                // in Phase 47.
            }
            finally
            {
                _audioContext = null;
                _activeSource = null;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// D-48-07: mono → stereo promotion. Returns the input array unchanged when
    /// already stereo (no allocation; reference-equal pass-through) — the
    /// cheap-stereo optimization is asserted by
    /// <c>WebAudioBackendIntegrationTests.PromoteToStereo_StereoInput_PassesThrough</c>.
    ///
    /// <para>Mirrors the Phase 26.2 / Phase 28 / Phase 37 B2 LOCK posture: "SFZ
    /// voices ALWAYS promote to stereo". The WebAudio backend extends the same
    /// invariant to the JS marshal boundary — JS-side has no per-channel
    /// branching, every <see cref="FlowRuntimeInterop.PlayStereoFloat32"/> call
    /// receives interleaved 2-channel Float32 data.</para>
    ///
    /// <para>Analog: <c>flow-lang/StandardLibrary/Audio/AudioCore.cs:209-219</c>
    /// (<c>MonoToStereo</c>) — same loop shape but on <c>float[]</c> instead of
    /// <c>AudioBuffer</c> since <see cref="IAudioBackend.Play"/> takes raw
    /// samples (Phase 47 D-47-05 pinned signature).</para>
    /// </summary>
    /// <param name="samples">Interleaved mono (length == frames) or stereo
    /// (length == frames * 2) Float32 samples.</param>
    /// <param name="channels">1 for mono input; 2 for already-stereo (pass-through).</param>
    /// <returns>Interleaved stereo Float32 (length == samples.Length * 2 for mono;
    /// reference-equal pass-through for stereo).</returns>
    public static float[] PromoteToStereo(float[] samples, int channels)
    {
        if (samples is null) return Array.Empty<float>();
        if (channels >= 2) return samples;  // already stereo (or multi-channel) — pass-through

        var stereo = new float[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            stereo[i * 2]     = samples[i];
            stereo[i * 2 + 1] = samples[i];
        }
        return stereo;
    }
}
