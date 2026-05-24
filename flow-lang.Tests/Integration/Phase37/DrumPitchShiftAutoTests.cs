using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.Tests.Helpers;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DRUM-01 — drum note pitch-shift uses the Plan 37-02
/// <see cref="FlowLang.StandardLibrary.Audio.DSP.PitchShiftEngine"/>
/// <c>#auto</c> path (transient-preserving PSOLA-or-vocoder dispatch) for
/// percussion patches, instead of the Phase 33 varispeed route which couples
/// pitch and time.
///
/// <para>W7 LOCK ACCEPTANCE: gate is <see cref="SfzData.IsPercussion"/> —
/// set at SfzBuiltins LOAD TIME by the dict-symbol (<c>#drums</c>) — NOT
/// the filename. Constructing an SfzData with <c>IsPercussion = true</c>
/// directly here exercises the gate without needing a VSCO-CE install.</para>
///
/// <para>Tests strategy: directly invoke
/// <see cref="SfzRenderer.Render(MusicalNoteData,int,double,double,SfzData)"/>
/// at an OFF-center MIDI pitch and verify the rendered output differs
/// between a percussion patch (IsPercussion=true → PitchShiftEngine route)
/// and a non-percussion patch (IsPercussion=false → varispeed route). At
/// the SAMPLE-CENTER pitch (semitonesShift=0) the two paths produce the
/// same output (Pitfall 11 identity fast-path — PitchShiftEngine returns
/// input verbatim at cents=0).</para>
/// </summary>
[Collection("FlowScripts")]
public class DrumPitchShiftAutoTests : IDisposable
{
    private const int SampleRate = 44100;
    private const double DurationBeats = 1.0;
    private const double Bpm = 120.0;

    public DrumPitchShiftAutoTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Build a minimal SFZ patch around the Phase 33 smoke-fixture C4 sine
    /// with one region centered at MIDI 60 covering 0..127 pitch range so
    /// any composer-authored note routes through the region with a non-zero
    /// pitch shift unless authored exactly at MIDI 60.
    /// </summary>
    private static (SfzData patch, SfzSampleCache cache) BuildSineSfzPatch(bool isPercussion)
    {
        string smokeDir = Path.Combine(FindRepoRoot(), "flow-lang.Tests",
            "fixtures", "sfz-smoke");
        // Single region: keycenter 60, covers MIDI 0..127 / vel 1..127 so
        // any off-center note routes here.
        var region = new SfzRegion(
            SamplePath: "C4_sine.wav",
            PitchKeycenter: 60,
            LoKey: 0, HiKey: 127,
            LoVel: 1, HiVel: 127,
            LoopMode: SfzLoopMode.NoLoop,
            LoopStart: 0, LoopEnd: 0,
            AmpegAttack: 0.0, AmpegRelease: 0.0,
            Volume: 1.0, Pan: 0.0);

        var regions = new List<SfzRegion> { region };
        var grid = new SfzRegion?[128, 128];
        for (int p = 0; p < 128; p++)
            for (int v = 1; v < 128; v++)
                grid[p, v] = region;

        var data = new SfzData(
            Description: "drum-test-patch",
            BasePath: smokeDir,
            Regions: regions,
            Grid: grid,
            SortedByPitch: new[] { 60 },
            IsPercussion: isPercussion);

        var cache = new SfzSampleCache();
        var wav = WavReader.ReadWav(Path.Combine(smokeDir, "C4_sine.wav"));
        cache.SetRaw_TestOnly(data, "C4_sine.wav", wav);
        return (data, cache);
    }

    private static MusicalNoteData MakeNote(char name, int octave, int alteration = 0, double velocity = 0.7)
        => new MusicalNoteData(
            noteName: name, octave: octave, alteration: alteration,
            durationValue: 4, isRest: false, velocity: velocity);

    private static double Rms(AudioBuffer buf)
    {
        if (buf is null || buf.Frames == 0) return 0.0;
        int ch = buf.Channels;
        double sumSq = 0.0;
        int n = buf.Data.Length;
        for (int i = 0; i < n; i++) sumSq += (double)buf.Data[i] * buf.Data[i];
        return Math.Sqrt(sumSq / n);
    }

    /// <summary>
    /// Fact 1 — At the SAMPLE-CENTER pitch (MIDI 60 = C4 = keycenter), the
    /// pitch shift is 0 semitones → PitchShiftEngine identity fast-path
    /// (cents=0 returns input verbatim per Pitfall 11). Output is non-empty
    /// and non-silent; renderer runs without error.
    /// </summary>
    [Fact]
    public void DrumPitchShift_AtSampleCenter_NoShiftNeeded()
    {
        var (patch, cache) = BuildSineSfzPatch(isPercussion: true);
        var renderer = new SfzRenderer(cache);
        var note = MakeNote('C', 4); // MIDI 60 == keycenter

        var buf = renderer.Render(note, SampleRate, DurationBeats, Bpm, patch);

        Assert.NotNull(buf);
        Assert.True(buf.Frames > 0, "expected non-empty buffer at sample center");
        Assert.True(Rms(buf) > 1e-6, "expected non-silent output at sample center");
    }

    /// <summary>
    /// Fact 2 — W7 ACCEPTANCE: an off-center note (5 semitones above
    /// keycenter 60 → MIDI 65 = F4) on a percussion patch routes through
    /// PitchShiftEngine, producing output that DIFFERS from the same note
    /// rendered through a non-percussion patch (varispeed route).
    ///
    /// <para>The PitchShiftEngine path preserves duration (target buffer
    /// length matches authored duration via stretch + resample inverse
    /// remap). The varispeed route also preserves duration via
    /// <see cref="AssembleBody"/>'s targetFrames zero-pad. So we can't
    /// distinguish by length — we distinguish by SAMPLE CONTENT difference.</para>
    /// </summary>
    [Fact]
    public void DrumPitchShift_OffCenter5Semitones_DiffersFromVarispeed()
    {
        var (drumPatch, drumCache) = BuildSineSfzPatch(isPercussion: true);
        var (toneePatch, toneCache) = BuildSineSfzPatch(isPercussion: false);

        var drumRenderer = new SfzRenderer(drumCache);
        var toneRenderer = new SfzRenderer(toneCache);

        var noteF4 = MakeNote('F', 4); // MIDI 65 = +5 semitones from keycenter 60

        var drumBuf = drumRenderer.Render(noteF4, SampleRate, DurationBeats, Bpm, drumPatch);
        var toneBuf = toneRenderer.Render(noteF4, SampleRate, DurationBeats, Bpm, toneePatch);

        Assert.Equal(drumBuf.Frames, toneBuf.Frames);
        Assert.Equal(drumBuf.Channels, toneBuf.Channels);

        // The two paths produce different sample content. Sum absolute
        // delta — a meaningfully large value confirms the two paths
        // produced distinct outputs (i.e. PitchShiftEngine was on a
        // different code path than varispeed). Floor at a non-trivial
        // delta to avoid false-positives from numerical noise.
        double totalDelta = 0.0;
        int n = Math.Min(drumBuf.Data.Length, toneBuf.Data.Length);
        for (int i = 0; i < n; i++)
            totalDelta += Math.Abs(drumBuf.Data[i] - toneBuf.Data[i]);

        Assert.True(totalDelta > 1.0,
            $"expected drum/tone outputs to differ at off-center pitch (W7 LOCK gate); " +
            $"got cumulative delta {totalDelta:F4}");
    }

    /// <summary>
    /// Fact 3 — W7 ACCEPTANCE: a NON-percussion patch off-center note does
    /// NOT trigger the >12st drum-advisory (because it's not routing
    /// through PitchShiftEngine at all). The advisory text contains
    /// "&gt;12st shift on drum sample" — its absence on the tone path
    /// confirms the gate is IsPercussion-driven, not filename or pitch-shift
    /// driven.
    /// </summary>
    [Fact]
    public void DrumPitchShift_NonPercussionPatchOffCenter_NoLargeShiftAdvisory()
    {
        // Capture stderr for advisory inspection.
        var origErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var (tonePatch, toneCache) = BuildSineSfzPatch(isPercussion: false);
            var renderer = new SfzRenderer(toneCache);
            // 24 semitones off-center — would trigger >12st advisory if
            // the percussion path were taken.
            var noteC6 = MakeNote('C', 6); // MIDI 84 = +24 from keycenter 60

            var buf = renderer.Render(noteC6, SampleRate, DurationBeats, Bpm, tonePatch);

            Assert.NotNull(buf);
            string stderr = sw.ToString();
            Assert.DoesNotContain(">12st shift on drum sample", stderr);
            Assert.DoesNotContain("varispeed artifacts likely dominate", stderr);
        }
        finally
        {
            Console.SetError(origErr);
        }
    }

    /// <summary>
    /// Fact 4 — &gt;12 semitone shift on a PERCUSSION patch emits the
    /// one-shot stderr advisory per OQ3 resolution + RESEARCH §Pattern 11.
    /// </summary>
    [Fact]
    public void DrumPitchShift_LargeShiftOnPercussionPatch_EmitsAdvisory()
    {
        var origErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var (drumPatch, drumCache) = BuildSineSfzPatch(isPercussion: true);
            var renderer = new SfzRenderer(drumCache);
            // 24 semitones above keycenter 60 → MIDI 84 = C6 (well above
            // the 12-semitone advisory threshold).
            var noteC6 = MakeNote('C', 6);

            var buf = renderer.Render(noteC6, SampleRate, DurationBeats, Bpm, drumPatch);

            Assert.NotNull(buf);
            string stderr = sw.ToString();
            Assert.Contains(">12st shift on drum sample", stderr);
        }
        finally
        {
            Console.SetError(origErr);
        }
    }
}
