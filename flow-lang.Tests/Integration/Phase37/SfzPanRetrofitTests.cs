using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.Tests.Helpers;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 MIX-02 — SFZ-rendered voices honor composer voice.Pan via the
/// 6-arg <see cref="SfzRenderer.Render(MusicalNoteData,int,double,double,SfzData,double)"/>
/// overload. B2 lock per RESEARCH §Pitfall 12: SFZ render path UNCONDITIONALLY
/// promotes to stereo via constant-power split; centered (effectivePan == 0)
/// produces equal L/R at √0.5 rather than mono.
/// </summary>
[Collection("FlowScripts")]
public class SfzPanRetrofitTests : IDisposable
{
    private const int SampleRate = 44100;
    private const double DurationBeats = 1.0;
    private const double Bpm = 120.0;

    public SfzPanRetrofitTests()
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
    /// (4410 frames @ 0.5 amp) with a single region covering MIDI 60. Lets
    /// the test exercise the SfzRenderer's pan math without needing a full
    /// VSCO-CE install.
    /// </summary>
    private (SfzData patch, SfzSampleCache cache) BuildSineSfzPatch(double regionPan = 0.0)
    {
        string smokeDir = Path.Combine(FindRepoRoot(), "flow-lang.Tests",
            "fixtures", "sfz-smoke");
        // Inline SFZ pointing at the smoke fixture's C4_sine.wav via the
        // standard <region>.sample path resolution.
        string sfzContent = $@"<region>
sample=C4_sine.wav
pitch_keycenter=60
lokey=60 hikey=60 lovel=1 hivel=127
pan={(int)Math.Round(regionPan * 100)}
";
        string sfzPath = Path.Combine(smokeDir, "_phase37_pan_inline.sfz");
        var data = SfzParser.Parse(sfzContent, sfzPath, "phase37_pan");

        var cache = new SfzSampleCache();
        // Reuse the smoke fixture's C4_sine.wav by feeding it into the cache
        // via the EagerLoad path — but EagerLoad takes a SongData; for a
        // direct test we use SetRaw to bypass.
        var wav = WavReader.ReadWav(Path.Combine(smokeDir, "C4_sine.wav"));
        cache.SetRaw_TestOnly(data, "C4_sine.wav", wav);
        return (data, cache);
    }

    private static MusicalNoteData MakeC4Note(double velocity = 0.7)
        => new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 4, isRest: false, velocity: velocity);

    private static double RmsChannel(AudioBuffer buf, int channel)
    {
        if (buf is null || buf.Frames == 0) return 0.0;
        double sumSq = 0.0;
        for (int f = 0; f < buf.Frames; f++)
        {
            float s = buf.GetSample(f, channel);
            sumSq += (double)s * s;
        }
        return Math.Sqrt(sumSq / buf.Frames);
    }

    [Fact]
    public void SfzVoice_WithPan07_RightChannelLouderThanLeft()
    {
        var (patch, cache) = BuildSineSfzPatch(regionPan: 0.0);
        var renderer = new SfzRenderer(cache);
        var note = MakeC4Note();
        var buf = renderer.Render(note, SampleRate, DurationBeats, Bpm, patch, voicePan: 0.7);

        Assert.Equal(2, buf.Channels);
        double left = RmsChannel(buf, 0);
        double right = RmsChannel(buf, 1);
        // ≥ 3 dB right-over-left at pan=0.7.
        double db = 20.0 * Math.Log10(Math.Max(right, 1e-12) / Math.Max(left, 1e-12));
        Assert.True(db >= 3.0,
            $"voice.Pan=+0.7: R/L dB delta {db:F2} should be >= 3.0");
    }

    [Fact]
    public void SfzVoice_WithPanMinus07_LeftChannelLouderThanRight()
    {
        var (patch, cache) = BuildSineSfzPatch(regionPan: 0.0);
        var renderer = new SfzRenderer(cache);
        var note = MakeC4Note();
        var buf = renderer.Render(note, SampleRate, DurationBeats, Bpm, patch, voicePan: -0.7);

        Assert.Equal(2, buf.Channels);
        double left = RmsChannel(buf, 0);
        double right = RmsChannel(buf, 1);
        double db = 20.0 * Math.Log10(Math.Max(left, 1e-12) / Math.Max(right, 1e-12));
        Assert.True(db >= 3.0,
            $"voice.Pan=-0.7: L/R dB delta {db:F2} should be >= 3.0");
    }

    /// <summary>
    /// B2 ACCEPTANCE — SFZ render path UNCONDITIONALLY promotes to stereo
    /// regardless of voice.Pan value. Composer-set voice.Pan == 0 AND
    /// region.Pan == 0 still produces a stereo buffer (channels == 2) with
    /// equal L/R within tolerance (constant-power center = √0.5 each).
    /// </summary>
    [Fact]
    public void SfzVoice_WithPanZero_OutputIsStereo_NotMono()
    {
        var (patch, cache) = BuildSineSfzPatch(regionPan: 0.0);
        var renderer = new SfzRenderer(cache);
        var note = MakeC4Note();
        var buf = renderer.Render(note, SampleRate, DurationBeats, Bpm, patch, voicePan: 0.0);

        // B2 LOCK: must be stereo.
        Assert.Equal(2, buf.Channels);

        double left = RmsChannel(buf, 0);
        double right = RmsChannel(buf, 1);
        // Both channels must carry signal (centered constant-power = √0.5).
        Assert.True(left > 0, "centered voice should carry signal in L");
        Assert.True(right > 0, "centered voice should carry signal in R");

        // |L - R| within ±0.5 dB tolerance (centered = equal L/R).
        double dbDelta = Math.Abs(20.0 * Math.Log10(Math.Max(left, 1e-12) / Math.Max(right, 1e-12)));
        Assert.True(dbDelta < 0.5,
            $"voice.Pan=0 + region.Pan=0: |L-R| dB delta {dbDelta:F2} should be < 0.5");
    }
}
