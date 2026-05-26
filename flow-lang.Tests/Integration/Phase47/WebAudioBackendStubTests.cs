using FlowLang.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase47;

/// <summary>
/// Phase 47 Plan 47-02 — Pin stub behavior of <see cref="WebAudioBackend"/> on
/// Desktop. The test process runs on Desktop (Linux/macOS/Windows) so
/// <see cref="WebAudioBackend.IsAvailable"/> must return false here, and any
/// attempt to use the methods must throw <see cref="PlatformNotSupportedException"/>
/// with the documented stub message substring.
///
/// These tests use the public IAudioBackend surface — they will continue to
/// pass under Plan 47-03 (which adds guards in FlowEngine but does not touch
/// AudioBackend). Phase 48 will swap the throws for [JSImport] bodies; at that
/// point these Facts EITHER stay valid because Desktop still throws (Phase 48
/// only fires on Mono-WASM) OR get retired when the platform-conditional
/// implementation lands.
/// </summary>
public class WebAudioBackendStubTests
{
    [Fact]
    public void IsAvailable_ReturnsFalse_OnDesktop()
    {
        // Per D-47-07: IsAvailable wraps OperatingSystem.IsBrowser(), which
        // returns false on every non-Mono-WASM runtime.
        Assert.False(WebAudioBackend.IsAvailable(),
            "WebAudioBackend.IsAvailable() must return false on Desktop test runners.");
    }

    [Fact]
    public void Play_ThrowsPlatformNotSupportedException_WithStubMessage()
    {
        var backend = new WebAudioBackend();
        var samples = new float[] { 0f, 0f };
        var ex = Assert.Throws<PlatformNotSupportedException>(
            () => backend.Play(samples, 44100, 1));
        Assert.Contains("WebAudioBackend stub", ex.Message);
        Assert.Contains("Phase 48", ex.Message);
    }

    [Fact]
    public void Initialize_ThrowsPlatformNotSupportedException()
    {
        var backend = new WebAudioBackend();
        Assert.Throws<PlatformNotSupportedException>(
            () => backend.Initialize(44100, 1));
    }

    [Fact]
    public void Stop_ThrowsPlatformNotSupportedException()
    {
        var backend = new WebAudioBackend();
        Assert.Throws<PlatformNotSupportedException>(() => backend.Stop());
    }

    [Fact]
    public void Dispose_IsNoOp_DoesNotThrow()
    {
        // Phase 47 D-47-05: Dispose MUST be safe to call (using-block discipline).
        // The stub's Dispose is a true no-op; Phase 48 will revoke the
        // AudioBufferSourceNode here.
        var backend = new WebAudioBackend();
        backend.Dispose();  // first dispose
        backend.Dispose();  // double-dispose also safe
    }

    [Fact]
    public void Name_IsWebAudio()
    {
        var backend = new WebAudioBackend();
        Assert.Equal("WebAudio", backend.Name);
    }

    [Fact]
    public void IsInitialized_IsFalse_OnStub()
    {
        var backend = new WebAudioBackend();
        Assert.False(backend.IsInitialized,
            "Stub never initializes — IsInitialized always false until Phase 48 lands.");
    }
}
