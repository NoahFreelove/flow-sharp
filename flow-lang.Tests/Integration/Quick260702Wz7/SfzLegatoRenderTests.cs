using System;
using System.Security.Cryptography;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Quick260702Wz7;

/// <summary>
/// quick-260702-wz7 — make <see cref="Articulation.Legato"/> audibly meaningful
/// on the SFZ sampler path.
///
/// <para>Before this task every SFZ note read its source sample from frame 0, so
/// each note retriggered the full recorded bow-start / tongue attack transient at
/// full level — connected string/wind lines "sound like a keyboard is playing
/// them" (composer feedback on the Swan Lake render). Real SFZ players emulate
/// legato without dedicated legato samples by (a) offsetting the sample start past
/// the attack transient and (b) applying a longer envelope attack to mask the
/// seam. This fix implements both, gated to <c>Articulation.Legato</c> only.</para>
///
/// <para>These facts pin: the softened envelope attack (early RMS lower than
/// Normal on a constant source), the sample-start offset (Legato reads different
/// source material than Normal in a settled window past both attacks), non-Legato
/// byte-identity (golden SHA256 hashes captured pre-change), and two-run
/// determinism for Legato.</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzLegatoRenderTests : IDisposable
{
    private const int SampleRate = 44100;
    private const double DurationBeats = 1.0;   // → 22050 authored frames @ 120 BPM
    private const double Bpm = 120.0;

    // Legato offset = min((int)(0.1*sr), sourceFrames/4). With a 44100-frame source
    // this is min(4410, 11025) = 4410. Legato softened attack = 80ms = 3528 frames,
    // so every "settled" probe window starts past 3528.
    private const int ExpectedOffset = 4410;

    // Golden SHA256 of the raw buf.Data bytes for the STEP fixture, captured against
    // the UNMODIFIED SfzRenderer (Task 1). Task 2 must NOT move these — non-Legato
    // articulations stay byte-identical (offset resolves to 0, baseAttack unchanged).
    private const string GoldenNormalSha =
        "6ae07c024350572e9d6d330baca51aa73fdd829b1e07f7be940dce54427e5caf";
    private const string GoldenStaccatoSha =
        "c085c38c4d6f10595e1826388cd36c78c7dd554236478a746a48f69397f25ffc";

    private readonly ITestOutputHelper _output;

    public SfzLegatoRenderTests(ITestOutputHelper output)
    {
        _output = output;
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    // ----- fixtures (built entirely in memory — no committed WAV) -------------

    /// <summary>44100-frame mono buffer, every sample = 0.5f. Isolates the
    /// ENVELOPE change: the offset reads the same value everywhere, so only the
    /// softened attack moves early RMS.</summary>
    private static AudioBuffer ConstantFixture()
    {
        var buf = new AudioBuffer(44100, 1, SampleRate);
        for (int i = 0; i < buf.Data.Length; i++) buf.Data[i] = 0.5f;
        return buf;
    }

    /// <summary>44100-frame mono buffer, sample = 1.0f for frame &lt; 8820, else
    /// 0.2f. [0,8820) is a synthetic "attack transient"; [8820,44100) is the quiet
    /// "body". Isolates the OFFSET: Legato's 4410-frame skip makes it read the 0.2
    /// body where Normal still reads the 1.0 transient.</summary>
    private static AudioBuffer StepFixture()
    {
        var buf = new AudioBuffer(44100, 1, SampleRate);
        for (int i = 0; i < buf.Data.Length; i++) buf.Data[i] = i < 8820 ? 1.0f : 0.2f;
        return buf;
    }

    /// <summary>One NoLoop region. AmpegRelease=0.001 (absent-sentinel → no vud
    /// tail); AmpVeltrack=0.0 (velocity does NOT scale amplitude → Legato vs Normal
    /// comparisons are purely offset/envelope, not the tpn velocity curve).</summary>
    private static SfzData BuildNoLoopPatch(string description)
    {
        var region = new SfzRegion(
            SamplePath: "step.wav",
            PitchKeycenter: 60,
            LoKey: 48, HiKey: 71,
            LoVel: 1, HiVel: 127,
            LoopMode: SfzLoopMode.NoLoop,
            LoopStart: 0, LoopEnd: 0,
            AmpegAttack: 0.001,
            AmpegRelease: 0.001,
            Volume: 1.0, Pan: 0.0,
            AmpVeltrack: 0.0);
        var grid = new SfzRegion?[128, 128];
        for (int p = region.LoKey; p <= region.HiKey; p++)
            for (int v = region.LoVel; v <= region.HiVel; v++)
                grid[p, v] = region;
        return new SfzData(description, "/tmp/wz7.sfz",
            new System.Collections.Generic.List<SfzRegion> { region },
            grid, new[] { 60 });
    }

    private static SfzRenderer NewRenderer(SfzData patch, AudioBuffer source)
    {
        var cache = new SfzSampleCache();
        foreach (var region in patch.Regions)
            cache.SetRaw_TestOnly(patch, region.SamplePath, source);
        return new SfzRenderer(cache);
    }

    private static MusicalNoteData MakeC4(Articulation articulation) =>
        new(noteName: 'C', octave: 4, alteration: 0,
            durationValue: 4, isRest: false,
            velocity: 0.8, articulation: articulation);

    private static AudioBuffer RenderNote(SfzData patch, AudioBuffer source, Articulation art)
    {
        var renderer = NewRenderer(patch, source);
        return renderer.Render(MakeC4(art), SampleRate, DurationBeats, Bpm, patch);
    }

    /// <summary>Stereo-aware RMS over frames [startFrame, endFrame) across all
    /// channels.</summary>
    private static double Rms(AudioBuffer buf, int startFrame, int endFrame)
    {
        int ch = buf.Channels;
        startFrame = Math.Max(0, startFrame);
        endFrame = Math.Min(buf.Frames, endFrame);
        if (endFrame <= startFrame) return 0.0;
        double sumSq = 0.0;
        int n = 0;
        for (int f = startFrame; f < endFrame; f++)
            for (int c = 0; c < ch; c++)
            {
                double v = buf.Data[f * ch + c];
                sumSq += v * v;
                n++;
            }
        return n == 0 ? 0.0 : Math.Sqrt(sumSq / n);
    }

    private static string Sha256Of(float[] data)
    {
        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    // ----- Fact 1: softened attack (constant fixture) ------------------------

    [Fact]
    public void LegatoSoftensAttack_EarlyRmsLowerThanNormal()
    {
        var source = ConstantFixture();
        var patch = BuildNoLoopPatch("legato-attack");

        var normal = RenderNote(patch, source, Articulation.Normal);
        var legato = RenderNote(patch, source, Articulation.Legato);

        // Early window [0, 1764) ≈ 40ms. With a constant source the only early
        // difference is Legato's 80ms attack vs Normal's ~1ms.
        double rmsNormal = Rms(normal, 0, 1764);
        double rmsLegato = Rms(legato, 0, 1764);

        Assert.True(rmsLegato < 0.6 * rmsNormal,
            $"Legato early RMS {rmsLegato:F5} must be < 60% of Normal early RMS " +
            $"{rmsNormal:F5} (softened 80ms attack).");
    }

    // ----- Fact 2: sample-start offset (step fixture) ------------------------

    [Fact]
    public void LegatoSkipsAttackTransient_BodyReadsOffsetMaterial()
    {
        var source = StepFixture();
        var patch = BuildNoLoopPatch("legato-offset");

        var normal = RenderNote(patch, source, Articulation.Normal);
        var legato = RenderNote(patch, source, Articulation.Legato);

        // Settled window [4410, 8820): both envelopes are at sustain here.
        // Normal reads source[f] ∈ [4410,8820) = 1.0 (still transient);
        // Legato reads source[f+4410] ∈ [8820,13230) = 0.2 (body).
        double rmsNormal = Rms(normal, ExpectedOffset, 8820);
        double rmsLegato = Rms(legato, ExpectedOffset, 8820);

        Assert.True(rmsLegato < 0.5 * rmsNormal,
            $"Legato settled RMS {rmsLegato:F5} must be < 50% of Normal settled RMS " +
            $"{rmsNormal:F5} (4410-frame sample-start skip past the transient).");
    }

    // ----- Fact 3: non-Legato byte-identity (golden hash) --------------------

    [Fact]
    public void NonLegato_ByteIdentical_GoldenHash()
    {
        var source = StepFixture();
        var patch = BuildNoLoopPatch("golden");

        var normal = RenderNote(patch, source, Articulation.Normal);
        var staccato = RenderNote(patch, source, Articulation.Staccato);

        string normalSha = Sha256Of(normal.Data);
        string staccatoSha = Sha256Of(staccato.Data);

        _output.WriteLine($"GoldenNormalSha   = {normalSha}");
        _output.WriteLine($"GoldenStaccatoSha = {staccatoSha}");

        Assert.Equal(GoldenNormalSha, normalSha);
        Assert.Equal(GoldenStaccatoSha, staccatoSha);
    }

    // ----- Fact 4: two-run determinism for Legato ----------------------------

    [Fact]
    public void TwoRuns_Legato_ByteIdentical()
    {
        var source = StepFixture();
        var patch = BuildNoLoopPatch("determinism");

        var a = RenderNote(patch, source, Articulation.Legato);
        var b = RenderNote(patch, source, Articulation.Legato);

        Assert.Equal(a.Data.Length, b.Data.Length);
        for (int i = 0; i < a.Data.Length; i++)
            Assert.Equal(a.Data[i], b.Data[i]);
    }

    // ----- Fact 5: step-fixture early RMS companion --------------------------

    [Fact]
    public void Legato_EarlyRms_LowerThanNormal_StepFixture()
    {
        var source = StepFixture();
        var patch = BuildNoLoopPatch("legato-early-step");

        var normal = RenderNote(patch, source, Articulation.Normal);
        var legato = RenderNote(patch, source, Articulation.Legato);

        double rmsNormal = Rms(normal, 0, 1764);
        double rmsLegato = Rms(legato, 0, 1764);

        Assert.True(rmsLegato < rmsNormal,
            $"Legato early RMS {rmsLegato:F5} must be < Normal early RMS {rmsNormal:F5} " +
            "(combined offset + softened attack).");
    }
}
