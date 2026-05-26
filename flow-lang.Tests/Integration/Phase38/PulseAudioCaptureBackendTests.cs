using System;
using FlowLang.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-05 Task 1 — sanity tests for the new
/// <see cref="PulseAudioCaptureBackend"/> sibling class. The "real" capture
/// path tests live in <c>MicBufferAttenuationTests</c> + <c>MicBufferResampleTests</c>
/// which use the injectable capture seam to avoid requiring a live PulseAudio
/// daemon in CI (RESEARCH §I line 1003 "test seam" recommendation).
///
/// This file asserts the type structure + the charitable-failure contract
/// (Initialize returns false rather than throwing when libpulse-simple is
/// absent — Pitfall #12 "live session never dies mid-set" lock applied to the
/// init path).
/// </summary>
[Collection("FlowScripts")]
public class PulseAudioCaptureBackendTests : IDisposable
{
    public PulseAudioCaptureBackendTests() { }
    public void Dispose() { }

    /// <summary>
    /// Construction with valid args does NOT touch the libpulse-simple
    /// runtime (lazy init — Initialize is a separate method). Ctor must
    /// succeed on every platform Flow runs on, including macOS/Windows/CI
    /// containers where libpulse-simple.so.0 is absent.
    /// </summary>
    [Fact]
    public void Ctor_ValidArgs_SetsSampleRateAndChannelsWithoutTouchingLibpulse()
    {
        var backend = new PulseAudioCaptureBackend(48_000, 2);
        try
        {
            Assert.Equal(48_000, backend.SampleRate);
            Assert.Equal(2, backend.Channels);
            Assert.False(backend.IsInitialized);
            Assert.Equal("PulseAudio-Capture", backend.Name);
        }
        finally
        {
            backend.Dispose();
        }
    }

    /// <summary>
    /// Invalid sample rate is rejected at the ctor boundary — protects against
    /// composer typos like (micBuffer 0) producing confusing PulseAudio errors
    /// deep in the P/Invoke layer.
    /// </summary>
    [Fact]
    public void Ctor_InvalidSampleRate_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PulseAudioCaptureBackend(0, 1));
        Assert.Throws<ArgumentException>(() => new PulseAudioCaptureBackend(-1, 1));
    }

    /// <summary>
    /// Invalid channel count is rejected — mirrors the playback sibling's
    /// 1..8 channel bound.
    /// </summary>
    [Fact]
    public void Ctor_InvalidChannelCount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PulseAudioCaptureBackend(44_100, 0));
        Assert.Throws<ArgumentException>(() => new PulseAudioCaptureBackend(44_100, 9));
    }

    /// <summary>
    /// CaptureSamples on an uninitialized backend returns null + populates
    /// the error message rather than throwing — caller (InputFunctions.MicBuffer)
    /// turns this into a charitable silent-buffer fallback per D-v1.5-05.
    /// </summary>
    [Fact]
    public void CaptureSamples_Uninitialized_ReturnsNullWithError()
    {
        var backend = new PulseAudioCaptureBackend(44_100, 1);
        try
        {
            var samples = backend.CaptureSamples(100, out var error);
            Assert.Null(samples);
            Assert.NotNull(error);
            Assert.Contains("not initialized", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            backend.Dispose();
        }
    }

    /// <summary>
    /// CaptureSamples(0) returns an empty array (not null) without contacting
    /// the underlying libpulse-simple library. Defensive ergonomics for
    /// composers calling (micBuffer 0s) which compiles to 0 frames.
    /// </summary>
    [Fact]
    public void CaptureSamples_ZeroFrames_ReturnsEmptyArrayWithoutLibpulseCall()
    {
        var backend = new PulseAudioCaptureBackend(44_100, 1);
        try
        {
            var samples = backend.CaptureSamples(0, out var error);
            Assert.NotNull(samples);
            Assert.Empty(samples!);
            Assert.Null(error);
        }
        finally
        {
            backend.Dispose();
        }
    }

    /// <summary>
    /// Dispose is idempotent — multiple calls do not throw. Protects against
    /// `using` blocks + explicit Dispose chains in caller code (the
    /// charitable-fallback path in InputFunctions.MicBuffer always disposes
    /// after capture even if Initialize returned false).
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var backend = new PulseAudioCaptureBackend(44_100, 1);
        backend.Dispose();
        backend.Dispose(); // must not throw
    }
}
