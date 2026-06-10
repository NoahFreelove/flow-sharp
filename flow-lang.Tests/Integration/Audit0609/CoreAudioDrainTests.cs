using System;
using System.Diagnostics;
using FlowLang.Audio;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 §3.3 — <c>CoreAudioBackend.Play</c> claimed "AudioQueueStop
/// with immediate=false blocks until the queue runs dry", but per AudioToolbox
/// semantics that call returns IMMEDIATELY and the queue stops asynchronously.
/// Play() therefore returned as soon as the last chunk was *enqueued*, up to
/// BufferCount × FramesPerBuffer (~280 ms at 44.1 kHz) before the audio finished
/// — and a script ending right after (play buf) disposed the engine, whose
/// immediate stop audibly truncated the tail. This violated Play's documented
/// blocking contract (PlaybackFunctions: "Blocks until playback completes");
/// the PulseAudio sibling genuinely drains via pa_simple_drain.
///
/// These tests drive the REAL AudioToolbox queue (macOS only via [PrereqFact];
/// charitable-skip when no output device is available). Audio content is pure
/// silence so test runs make no sound.
/// </summary>
[Trait("Category", "Audit0609")]
public class CoreAudioDrainTests
{
    private const int SampleRate = 44100;

    private static CoreAudioBackend RequireBackend()
    {
        if (!CoreAudioBackend.IsAvailable())
            Assert.Skip("AudioToolbox.framework unavailable");
        var backend = new CoreAudioBackend();
        if (!backend.Initialize(SampleRate, 1))
        {
            backend.Dispose();
            Assert.Skip("CoreAudio output queue could not be initialized (no output device?)");
        }
        return backend;
    }

    [PrereqFact("macos")]
    public void Play_BlocksUntilAudioFinishes()
    {
        using var backend = RequireBackend();

        // 600 ms of silence — far more than the 3 × 4096-frame buffer pipeline
        // (~280 ms) can hold, so a non-draining Play returns well before the
        // audio has been consumed by the device.
        var samples = new float[(int)(0.6 * SampleRate)];

        var sw = Stopwatch.StartNew();
        backend.Play(samples, SampleRate, channels: 1);
        sw.Stop();

        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(540),
            $"Play returned after {sw.ElapsedMilliseconds} ms for 600 ms of audio — " +
            "the queued tail was not drained (audit §3.3).");
    }

    [PrereqFact("macos")]
    public void BackToBackPlays_BothCompleteFully()
    {
        using var backend = RequireBackend();

        // Pre-fix, the second Play enqueued into a queue carrying a pending
        // deferred stop, which could drop its buffers. Post-fix each Play drains
        // and the pair takes at least the sum of both durations.
        var a = new float[(int)(0.4 * SampleRate)];
        var b = new float[(int)(0.4 * SampleRate)];

        var sw = Stopwatch.StartNew();
        backend.Play(a, SampleRate, channels: 1);
        backend.Play(b, SampleRate, channels: 1);
        sw.Stop();

        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(720),
            $"Two 400 ms plays finished in {sw.ElapsedMilliseconds} ms — a tail was dropped or truncated.");
    }
}
