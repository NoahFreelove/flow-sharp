using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Sweep0702;

/// <summary>
/// quick-260702-ijs — <c>tempo { }</c> blocks must reach direct note-stream playback
/// (<c>(play seq)</c> / <c>(stream seq)</c>). Before the fix, <c>PlaySequence</c> read the
/// BPM from <see cref="Timeline"/>'s <c>[ThreadStatic]</c> default (120, only ever written by
/// <c>setBPM</c>), so <c>tempo 120 { }</c> and <c>tempo 480 { }</c> rendered at the SAME speed.
/// The fix resolves the BPM from the active <c>MusicalContext.Tempo</c> (the same seam
/// SongRenderer already uses), with a charitable fallback to <c>Timeline.GetBPM()</c>.
///
/// Test-surface decision (why xUnit + CaptureMode, not a `.flow` script): <c>play</c>/
/// <c>stream</c> return Void and share NO composer-observable offline duration surface —
/// <c>renderSequenceToVoices</c> takes an explicit bpm and never consults context, and
/// wrapping the stream in a Song uses the separate SongRenderer path. So a `.flow` script
/// cannot observe the rendered duration of a Void-returning <c>play</c>. Instead we drive the
/// exact <c>PlaySequence</c> path offline via CaptureMode (auto-enabled in this assembly by
/// <c>FLOW_SUPPRESS_PLAYBACK=1</c>), reading <c>engine.AudioManager.GetCapturedBuffer()</c> —
/// the <c>PlaySongErgonomicsTests</c> precedent.
///
/// <c>stream</c>'s <c>Task.Run</c> render shares the IDENTICAL resolved-bpm value (captured in
/// the registered lambda on the originating thread BEFORE dispatch), so it is covered by
/// construction. An automated timing assertion on <c>stream</c> is intentionally omitted
/// because the background capture populates asynchronously (racy).
/// </summary>
[Collection("FlowScripts")]
public class TempoAffectsPlaybackTests : IDisposable
{
    public TempoAffectsPlaybackTests() => RenderingDiagnostics.ResetForTesting();

    public void Dispose() => RenderingDiagnostics.ResetForTesting();

    // 8-note stream. Bare (undurated) notes default to eighth notes, so the stream
    // spans 4 beats total. At 44.1 kHz mono, PlaySequence renders:
    //   frames = 4 * (60/bpm) * 44100.  At 120 BPM → 88200; at 480 BPM → 22050.
    private const string Stream = "| C4 D4 E4 F4 G4 A4 B4 C5 |";

    /// <summary>
    /// Renders the fixed 8-note stream inside a <c>tempo bpm { }</c> block and returns the
    /// captured mix's frame count. A fresh engine per call because
    /// <c>GetCapturedBuffer()</c> clears the capture buffer.
    /// </summary>
    private static int RenderFrames(int bpm)
    {
        using var engine = new FlowEngine();
        Assert.True(engine.AudioManager.CaptureMode, "CaptureMode should be auto-enabled in tests");

        bool ok = engine.Execute(
            $"use \"@audio\"\ntempo {bpm} {{ (play {Stream} ) }}\n", "<tempo-play>");
        Assert.True(ok, "script must execute without error");

        var buffer = engine.AudioManager.GetCapturedBuffer();
        Assert.NotNull(buffer);
        Assert.True(buffer!.Frames > 0, "captured buffer must be non-empty");
        return buffer.Frames;
    }

    /// <summary>
    /// Renders the same stream with NO tempo block, exercising the charitable
    /// <see cref="Timeline"/> fallback (default 120 BPM).
    /// </summary>
    private static int RenderFramesNoTempo()
    {
        using var engine = new FlowEngine();
        Assert.True(engine.AudioManager.CaptureMode, "CaptureMode should be auto-enabled in tests");

        bool ok = engine.Execute(
            $"use \"@audio\"\n(play {Stream} )\n", "<no-tempo-play>");
        Assert.True(ok, "script must execute without error");

        var buffer = engine.AudioManager.GetCapturedBuffer();
        Assert.NotNull(buffer);
        Assert.True(buffer!.Frames > 0, "captured buffer must be non-empty");
        return buffer.Frames;
    }

    [Fact]
    public void PlaySequence_TempoBlock_ScalesRenderedDuration()
    {
        int frames120 = RenderFrames(120);
        int frames480 = RenderFrames(480);

        // 4x tempo → ~1/4 the frames. Analytic ratio is 4.0; allow a generous band
        // for integer-frame truncation. Before the fix both were ~176400 (ratio ~1.0).
        double ratio = (double)frames120 / frames480;
        Assert.InRange(ratio, 3.5, 4.5);
    }

    [Fact]
    public void PlaySequence_TempoBlock_MapsToAnalyticFrameCount()
    {
        // 4 beats * (60/120) s * 44100 Hz = 88200 frames (bare notes = eighths).
        int frames120 = RenderFrames(120);
        Assert.InRange(frames120, 88200 - 4410, 88200 + 4410); // ±0.1s tolerance
    }

    [Fact]
    public void PlaySequence_NoTempoBlock_DefaultsTo120()
    {
        // Charitable Timeline fallback: no tempo block → default 120 BPM, so the frame
        // count matches the explicit tempo-120 render within a small tolerance.
        int framesDefault = RenderFramesNoTempo();
        int frames120 = RenderFrames(120);
        Assert.InRange(framesDefault, frames120 - 441, frames120 + 441); // ±0.01s
    }
}
