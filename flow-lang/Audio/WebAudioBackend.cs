namespace FlowLang.Audio;

/// <summary>
/// Phase 47 D-47-05: Stub WebAudio backend. Implements <see cref="IAudioBackend"/>
/// so the type is reachable in both Desktop and Web builds, but every method
/// EXCEPT <see cref="IsAvailable"/> throws <see cref="PlatformNotSupportedException"/>.
///
/// Phase 48 replaces the method bodies with [JSImport]/[JSExport] interop that
/// drives a browser <c>AudioContext</c> via <c>AudioBufferSourceNode</c>. Method
/// signatures here are PINNED — Phase 48 must not change the public surface.
///
/// Per D-47-07: <see cref="IsAvailable"/> returns <see cref="OperatingSystem.IsBrowser"/>
/// which is a JIT intrinsic. On Desktop net10.0 trim-mode builds, the
/// returns-false constant lets the linker dead-code-eliminate the rest of
/// this class. On Mono-WASM, the constant-true return lets the runtime pick
/// this backend in <see cref="AudioPlaybackManager.DetectBackend"/>.
///
/// On Desktop the class never picks up — Plan 47-02's
/// AudioPlaybackManager probe ordering ensures <see cref="CoreAudioBackend"/>
/// or <see cref="PulseAudioSimpleBackend"/> wins. This stub exists so the
/// Web build's csproj-conditional <c>&lt;Compile Remove&gt;</c> does NOT
/// have to strip CoreAudio/PulseAudio backends and STILL provide a working
/// backend factory — the file just lives in both builds and only IsAvailable
/// answers truthfully per platform.
/// </summary>
public sealed class WebAudioBackend : IAudioBackend
{
    private const string StubMessage =
        "WebAudioBackend stub — Phase 48 will implement via [JSImport]";

    /// <summary>
    /// Phase 47 D-47-07: probes for browser host via the .NET BCL's JIT
    /// intrinsic. Returns true under Mono-WASM, false on every Desktop
    /// runtime (Linux/macOS/Windows). Called from
    /// <see cref="AudioPlaybackManager.DetectBackend"/> as the FIRST probe
    /// branch — cheapest check; constant-folded on Desktop.
    /// </summary>
    public static bool IsAvailable() => OperatingSystem.IsBrowser();

    public string Name => "WebAudio";

    public bool IsInitialized => false;

    public bool Initialize(int sampleRate, int channels)
        => throw new PlatformNotSupportedException(StubMessage);

    public void Play(float[] samples, int sampleRate, int channels, CancellationToken cancellationToken = default)
        => throw new PlatformNotSupportedException(StubMessage);

    public void Stop()
        => throw new PlatformNotSupportedException(StubMessage);

    public IReadOnlyList<string> GetDevices()
        => throw new PlatformNotSupportedException(StubMessage);

    public bool SetDevice(string deviceName)
        => throw new PlatformNotSupportedException(StubMessage);

    public void WriteChunk(float[] samples, int offset, int count, int sampleRate, int channels)
        => throw new PlatformNotSupportedException(StubMessage);

    public void EnsureInitialized(int sampleRate, int channels)
        => throw new PlatformNotSupportedException(StubMessage);

    public void Dispose()
    {
        // Stub-safe no-op — Phase 48 will revoke the AudioBufferSourceNode here.
        // Intentionally does NOT throw because Dispose() must be safe to call
        // (e.g., from a `using` block or AudioPlaybackManager.Dispose chain).
        //
        // The Phase 48 implementation will:
        //   1. Close the JS-side AudioBufferSourceNode (revoke handle).
        //   2. Disconnect from the AudioContext (let the runtime GC the node).
        //   3. Release any JSObject references held via [JSImport] proxies.
        // Phase 47's stub leaves these as comments — see WebAudioBackendStubTests
        // .Dispose_IsNoOp_DoesNotThrow for the Desktop contract this method honors.
    }
}
