using System;
using System.IO;
using FlowLang.Core;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Helpers;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase29;

/// <summary>
/// Phase 29 REQ-3 — velocity-driven timbre vs amplitude.
///
/// Piano (≥4 velocity layers post-Phase-37 PIANO-01 — pp / mp / mf / ff) should
/// still show a measurable spectral-envelope CHANGE between v=0.2 (mostly pp) and
/// v=0.95 (mostly ff): cosine similarity over the magnitude spectrum &lt; 0.98,
/// indicating genuine timbre change (more partials + brighter content in the ff
/// layer).
///
/// Phase 37 PIANO-01 ceiling note: pre-Phase-37 (2-layer pp/ff) ceiling was 0.92.
/// Phase 37's 4-way crossfade with synthesized mp via RmsInterpolate(pp, mf, α=0.6)
/// legitimately smooths adjacent velocities — that IS the design intent (smoother
/// dynamic curve). The ceiling was raised to 0.98 to allow the smoothing while
/// still asserting a non-trivial timbral delta between v=0.2 and v=0.95. Empirical
/// measurement at HEAD: cosSim ≈ 0.9693.
///
/// Other tonal instruments (Brass/Sax/Strings/Flute/Bell — single mf velocity, linear
/// amplitude scaling) should show the SAME spectral envelope, just at different
/// amplitudes: cosSim ≥ 0.92, indicating timbre is preserved across velocity.
///
/// IMPORTANT: this plan (29-03) only delegates piano to SampledInstrumentRenderer.
/// The 5 non-piano tonal Theory rows currently run against the pre-Phase-29
/// hand-rolled synths (Plan 04 flips them). For those synths, velocity is a linear
/// amplitude multiplier — cosSim ≈ 1.0 by construction, so the Theory rows pass
/// trivially. Once Plan 04 lands and the 5 non-piano synths delegate to
/// SampledInstrumentRenderer (hasVelocityLayers: false), the test continues to
/// pass because the renderer's single-velocity branch is also pure linear scaling.
///
/// Serialized via <c>[Collection("FlowScripts")]</c> for the same reason as the
/// other Phase29 tests — Environment.CurrentDirectory mutation.
/// </summary>
[Collection("FlowScripts")]
public class VelocityLayerTests
{
    // Piano: ≥4 velocity layers (Phase 37 PIANO-01) — distinct timbre expected,
    // but ceiling raised from 0.92 (2-layer era) to 0.98 to accommodate the
    // legitimate smoothing introduced by the 4-way RmsInterpolate(pp, mf, α=0.6)
    // crossfade. See class-level xmldoc for full rationale.
    private const double PianoMaxCosSim = 0.98;
    // Non-piano tonal: single-velocity amplitude scaling — same timbre expected.
    private const double OtherMinCosSim = 0.92;

    private const double SoftVelocity = 0.2;
    private const double LoudVelocity = 0.95;

    [Fact]
    public void Piano_VelocityLayers_ProduceDifferentTimbre()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string pianoSample = Path.Combine(repoRoot, "flow-lang", "Samples", "piano", "C4_ff.wav");
        if (!File.Exists(pianoSample)) return; // skip if Plan 01 not yet shipped

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;

            using var runner = new FlowEngineRunner();
            string setupScript = @"
                use ""@audio""
                tempo 120 {
                    section velocity_demo_piano {
                        Sequence main = | C4q |
                    }
                }
                Song s = [velocity_demo_piano]
                Buffer rendered = (renderSong s ""piano"")
            ";
            var setup = runner.RunSource(setupScript, "<velocity-piano-setup>");
            Assert.True(setup.Success, $"Setup render for piano velocity test failed: {setup.Stderr}");

            var cache = FlowEngine.CurrentSampleCache;
            Assert.NotNull(cache);
            var renderer = new SampledInstrumentRenderer(cache!, "piano", hasVelocityLayers: true);

            var noteSoft = new MusicalNoteData(
                noteName: 'C', octave: 4, alteration: 0,
                durationValue: 4, isRest: false, velocity: SoftVelocity);
            var noteLoud = new MusicalNoteData(
                noteName: 'C', octave: 4, alteration: 0,
                durationValue: 4, isRest: false, velocity: LoudVelocity);

            var softBuf = renderer.Render(noteSoft, 44100, 1.0, 120.0, RenderTuning.Default);
            var loudBuf = renderer.Render(noteLoud, 44100, 1.0, 120.0, RenderTuning.Default);

            var softMag = Phase29Fft.ComputeMagnitudeSpectrum(softBuf);
            var loudMag = Phase29Fft.ComputeMagnitudeSpectrum(loudBuf);
            double cosSim = Phase29Fft.CosineSimilarity(softMag, loudMag);

            Assert.True(cosSim < PianoMaxCosSim,
                $"Piano velocity layers should produce different timbres. Got cosSim={cosSim:F4}, expected < {PianoMaxCosSim}");
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    [Theory]
    [InlineData("brass")]
    [InlineData("sax")]
    [InlineData("strings")]
    [InlineData("flute")]
    [InlineData("bell")]
    public void OtherTonalInstruments_VelocityScaling_PreservesTimbre(string instrument)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string sampleDir = Path.Combine(repoRoot, "flow-lang", "Samples", instrument);
        if (!Directory.Exists(sampleDir) || Directory.GetFiles(sampleDir, "*.wav").Length == 0)
            return; // skip if Plan 01 samples not yet committed

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;

            // Note: Plan 03 only delegates piano. The other 5 tonal instruments still
            // run through the hand-rolled synths until Plan 04 lands the delegation.
            // For those pre-Plan-04 hand-rolled synths, velocity is a linear amplitude
            // multiplier (cosSim ≈ 1.0 — trivially passes). After Plan 04, the renderer's
            // single-velocity branch is also pure linear scaling — same outcome. Both
            // pre-Plan-04 and post-Plan-04 paths satisfy cosSim ≥ 0.92.
            //
            // We construct the renderer directly here for the post-Plan-04-ready path
            // so this test continues to assert the SampledInstrumentRenderer's
            // single-velocity-scaling contract.
            var (softBuf, loudBuf) = RenderC4AtTwoVelocities(instrument, hasVelocityLayers: false);

            var softMag = Phase29Fft.ComputeMagnitudeSpectrum(softBuf);
            var loudMag = Phase29Fft.ComputeMagnitudeSpectrum(loudBuf);
            double cosSim = Phase29Fft.CosineSimilarity(softMag, loudMag);

            Assert.True(cosSim >= OtherMinCosSim,
                $"{instrument} should preserve timbre across velocity. Got cosSim={cosSim:F4}, expected ≥ {OtherMinCosSim}");
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    /// <summary>
    /// Render C4 quarter note at v=0.2 and v=0.95 through SampledInstrumentRenderer
    /// (direct API). Uses a tiny setup-script via FlowEngineRunner first so that
    /// FlowEngine.CurrentSampleCache is populated by SongRenderer.EagerLoad.
    /// </summary>
    private static (AudioBuffer soft, AudioBuffer loud) RenderC4AtTwoVelocities(string instrument, bool hasVelocityLayers)
    {
        using var runner = new FlowEngineRunner();
        string setupScript = $@"
            use ""@audio""
            tempo 120 {{
                section velocity_demo_{instrument} {{
                    Sequence main = | C4q |
                }}
            }}
            Song s = [velocity_demo_{instrument}]
            Buffer rendered = (renderSong s ""{instrument}"")
        ";
        var setup = runner.RunSource(setupScript, $"<velocity-setup-{instrument}>");
        Assert.True(setup.Success,
            $"Setup render for {instrument} velocity test failed: {setup.Stderr}");

        var cache = FlowEngine.CurrentSampleCache;
        Assert.NotNull(cache);

        var renderer = new SampledInstrumentRenderer(cache!, instrument, hasVelocityLayers);

        // C4 quarter note at SoftVelocity vs LoudVelocity. durationValue=4 follows the
        // existing SampledInstrumentSmokeTests convention — the SampledInstrumentRenderer
        // uses the explicit durationBeats parameter, not the note's GetBeats(), so the
        // enum value doesn't change the render.
        var noteSoft = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 4, isRest: false, velocity: SoftVelocity);
        var noteLoud = new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 4, isRest: false, velocity: LoudVelocity);

        var softBuf = renderer.Render(noteSoft, 44100, 1.0, 120.0, RenderTuning.Default);
        var loudBuf = renderer.Render(noteLoud, 44100, 1.0, 120.0, RenderTuning.Default);
        return (softBuf, loudBuf);
    }
}
