using FlowLang.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 Plan 48-03 — pin <see cref="WebAudioBackend.PromoteToStereo"/>
/// stereo-promotion contract (D-48-07) + Dispose-safety + Desktop fallback
/// invariants. These Facts run cross-target — they exercise pure-C# helpers
/// (PromoteToStereo + the public surface that survives the
/// <see cref="OperatingSystem.IsBrowser"/> gate on Desktop), NOT the [JSImport]
/// interop boundary itself. The [JSImport]-backed paths
/// (Initialize/Play/Stop returning success under a browser host) are
/// HUMAN-UAT in Plan 48-06's browser smoke.
///
/// <para>Supersedes Phase 47's <c>WebAudioBackendStubTests</c> (deleted in
/// Task 2 commit) — the 3 stub-throw assertions there inverted under Phase
/// 48, but the 4 Desktop-side invariants (IsAvailable=false, Dispose no-op,
/// Name=WebAudio, IsInitialized starts false) are still valid and
/// re-covered here.</para>
/// </summary>
public class WebAudioBackendIntegrationTests
{
    // ---------------------------------------------------------------
    // D-48-07 stereo-promotion contract
    // ---------------------------------------------------------------

    [Fact]
    public void MonoInput_PromotesToStereo_LengthDoubles()
    {
        // Input: deterministic ramp so element-wise assertion is unambiguous.
        var mono = new float[100];
        for (int i = 0; i < mono.Length; i++)
            mono[i] = i * 0.01f;

        float[] promoted = WebAudioBackend.PromoteToStereo(mono, channels: 1);

        Assert.Equal(200, promoted.Length);
        for (int i = 0; i < mono.Length; i++)
        {
            Assert.Equal(mono[i], promoted[i * 2]);
            Assert.Equal(mono[i], promoted[i * 2 + 1]);
        }
    }

    [Fact]
    public void PromoteToStereo_StereoInput_PassesThrough()
    {
        // Cheap-stereo optimization: when channels >= 2 the input array is
        // returned by reference (no allocation, no copy). Verified by
        // ReferenceEquals — if the implementation ever drops this
        // optimization, this fact fires RED.
        var stereo = new float[200];
        for (int i = 0; i < stereo.Length; i++)
            stereo[i] = i * 0.005f;

        float[] promoted = WebAudioBackend.PromoteToStereo(stereo, channels: 2);

        Assert.True(
            ReferenceEquals(stereo, promoted),
            "Stereo input should pass through without allocation (cheap-stereo optimization).");
    }

    [Fact]
    public void PromoteToStereo_ThreeChannelInput_DownmixesToStereo()
    {
        // sweep-0614 wasm-web regression: a >2-channel interleaved buffer used to
        // pass through UNCHANGED (the old `channels >= 2` guard), but the JS
        // marshal hard-codes channels:2 and de-interleaves with stride 2 — so a
        // 3-channel buffer played back garbled/mistimed. The fix downmixes
        // (average all channels into both L and R) so output is true stereo.
        //
        // PromoteToStereo emits a one-shot [webaudio] advisory via Console.Error
        // on the >2-channel path. xUnit runs this (parallel) class alongside the
        // serial WasmEntryConsoleCollection, whose tests redirect process-wide
        // Console.Error; an unguarded write here would race into their capture.
        // Redirect to a local sink (restore in finally) to stay isolated.
        var prevErr = Console.Error;
        var localErr = new System.IO.StringWriter();
        Console.SetError(localErr);
        try
        {
            // 2 frames, 3 channels, interleaved [L0 C0 R0 | L1 C1 R1].
            var threeCh = new float[] { 0.3f, 0.6f, 0.9f, -0.3f, -0.6f, -0.9f };

            float[] promoted = WebAudioBackend.PromoteToStereo(threeCh, channels: 3);

            // Must be a true 2-channel buffer: frames(2) * 2 = 4 samples.
            Assert.Equal(4, promoted.Length);

            // Frame 0 average = (0.3 + 0.6 + 0.9) / 3 = 0.6 → both L and R.
            Assert.Equal(0.6f, promoted[0], 5);
            Assert.Equal(0.6f, promoted[1], 5);
            // Frame 1 average = (-0.3 + -0.6 + -0.9) / 3 = -0.6 → both L and R.
            Assert.Equal(-0.6f, promoted[2], 5);
            Assert.Equal(-0.6f, promoted[3], 5);
        }
        finally
        {
            Console.SetError(prevErr);
        }
    }

    // ---------------------------------------------------------------
    // Dispose-safety contract (carryforward from Phase 47 D-47-05)
    // ---------------------------------------------------------------

    [Fact]
    public void Dispose_IsNoOpSafe_OnDesktop()
    {
        // Dispose must NEVER throw — using-block discipline + Pitfall #12
        // "live session never dies mid-set". This fact pins the contract
        // that Phase 47's WebAudioBackendStubTests.Dispose_IsNoOp_DoesNotThrow
        // covered before its file was deleted in Task 2.
        var backend = new WebAudioBackend();
        backend.Dispose();   // first dispose
        backend.Dispose();   // double-dispose also safe (idempotent)
        // No assert needed — pass if no exception.
    }

    // ---------------------------------------------------------------
    // Desktop-side invariants (carryforward from deleted Phase 47 stub tests)
    // ---------------------------------------------------------------

    [Fact]
    public void Name_IsWebAudio()
    {
        var backend = new WebAudioBackend();
        Assert.Equal("WebAudio", backend.Name);
    }

    [Fact]
    public void IsInitialized_IsFalse_BeforeInitializeCall()
    {
        // Per D-48-08: AudioContext is lazy-created on the first Initialize()
        // call. A freshly constructed backend has _audioContext == null →
        // IsInitialized returns false.
        var backend = new WebAudioBackend();
        Assert.False(
            backend.IsInitialized,
            "IsInitialized must be false until Initialize() succeeds.");
    }

    [Fact]
    public void IsAvailable_ReturnsFalse_OnDesktop()
    {
        // Per D-47-07: IsAvailable wraps OperatingSystem.IsBrowser(), which
        // returns false on every non-Mono-WASM runtime. AudioPlaybackManager
        // .DetectBackend relies on this to route Desktop to PulseAudio /
        // CoreAudio instead.
        Assert.False(
            WebAudioBackend.IsAvailable(),
            "WebAudioBackend.IsAvailable() must return false on Desktop test runners.");
    }

    [Fact]
    public void Initialize_ReturnsFalse_OnDesktop_CharitableFallback()
    {
        // D-48-11: when OperatingSystem.IsBrowser() is false, Initialize
        // charitably returns false (no exception). AudioPlaybackManager
        // never picks WebAudioBackend on Desktop in production, but a
        // hostile caller that constructs it directly gets a clean false
        // instead of PlatformNotSupportedException — different from
        // Phase 47 D-47-05 which threw.
        var backend = new WebAudioBackend();
        Assert.False(
            backend.Initialize(44100, 2),
            "Initialize must charitably return false on Desktop (D-48-11).");
    }

    [Fact]
    public void WriteChunk_ThrowsNotSupportedException_OnAnyTarget()
    {
        // D-48-01: WriteChunk is explicitly out-of-scope for v1 WebAudio.
        // Streaming requires SharedArrayBuffer + AudioWorklet (D-48-02
        // v1.6 backlog). Throwing makes the constraint visible at
        // first-use; charitable null-fallback would mask the issue.
        var backend = new WebAudioBackend();
        var samples = new float[100];
        var ex = Assert.Throws<NotSupportedException>(
            () => backend.WriteChunk(samples, 0, samples.Length, 44100, 2));
        Assert.Contains("WriteChunk", ex.Message);
        Assert.Contains("v1.6", ex.Message);
    }
}
