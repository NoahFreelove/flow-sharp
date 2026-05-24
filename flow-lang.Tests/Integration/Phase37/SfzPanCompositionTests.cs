using System;
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
/// Phase 37 MIX-02 — OQ4 additive-with-clamp composition: per-region SFZ pan
/// + per-voice composer pan compose via
/// <c>effectivePan = clamp(region.Pan + voice.Pan, -1.0, +1.0)</c> inside
/// SfzRenderer. Confirms RESEARCH §Open Question 4 lock.
/// </summary>
[Collection("FlowScripts")]
public class SfzPanCompositionTests : IDisposable
{
    private const int SampleRate = 44100;
    private const double DurationBeats = 1.0;
    private const double Bpm = 120.0;

    public SfzPanCompositionTests()
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

    private (SfzData patch, SfzSampleCache cache) BuildSineSfzPatch(double regionPan)
    {
        string smokeDir = Path.Combine(FindRepoRoot(), "flow-lang.Tests",
            "fixtures", "sfz-smoke");
        string sfzContent = $@"<region>
sample=C4_sine.wav
pitch_keycenter=60
lokey=60 hikey=60 lovel=1 hivel=127
pan={(int)Math.Round(regionPan * 100)}
";
        string sfzPath = Path.Combine(smokeDir, "_phase37_composition_inline.sfz");
        var data = SfzParser.Parse(sfzContent, sfzPath, "phase37_composition");
        var cache = new SfzSampleCache();
        var wav = WavReader.ReadWav(Path.Combine(smokeDir, "C4_sine.wav"));
        cache.SetRaw_TestOnly(data, "C4_sine.wav", wav);
        return (data, cache);
    }

    private static MusicalNoteData MakeC4Note()
        => new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 4, isRest: false, velocity: 0.7);

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
    public void RegionPanPlusVoicePan_AdditiveWithClamp_Effective02()
    {
        // region.Pan = -0.3, voice.Pan = +0.5 → effectivePan = clamp(0.2) = 0.2.
        // Slight right bias — right RMS > left RMS by ≥ 0.5 dB (small but
        // measurable).
        var (patch, cache) = BuildSineSfzPatch(regionPan: -0.3);
        var renderer = new SfzRenderer(cache);
        var note = MakeC4Note();
        var buf = renderer.Render(note, SampleRate, DurationBeats, Bpm, patch, voicePan: 0.5);

        Assert.Equal(2, buf.Channels);
        double left = RmsChannel(buf, 0);
        double right = RmsChannel(buf, 1);
        double db = 20.0 * Math.Log10(Math.Max(right, 1e-12) / Math.Max(left, 1e-12));
        Assert.True(db >= 0.5,
            $"region.Pan=-0.3 + voice.Pan=+0.5 → effective=0.2: R/L dB delta {db:F2} should be >= 0.5");
    }

    [Fact]
    public void RegionPanPositive_VoicePanNegative_ClampToZero()
    {
        // region.Pan = +0.6, voice.Pan = -0.6 → effectivePan = clamp(0.0) = 0.0.
        // Centered constant-power: stereo + equal L/R.
        var (patch, cache) = BuildSineSfzPatch(regionPan: 0.6);
        var renderer = new SfzRenderer(cache);
        var note = MakeC4Note();
        var buf = renderer.Render(note, SampleRate, DurationBeats, Bpm, patch, voicePan: -0.6);

        Assert.Equal(2, buf.Channels);
        double left = RmsChannel(buf, 0);
        double right = RmsChannel(buf, 1);
        Assert.True(left > 0, "centered effective-pan should carry signal in L");
        Assert.True(right > 0, "centered effective-pan should carry signal in R");
        double dbDelta = Math.Abs(20.0 * Math.Log10(Math.Max(left, 1e-12) / Math.Max(right, 1e-12)));
        Assert.True(dbDelta < 0.5,
            $"region+voice pan sum to 0: |L-R| dB delta {dbDelta:F2} should be < 0.5");
    }
}
