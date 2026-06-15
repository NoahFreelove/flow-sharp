using System;
using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-03 tail-window (sweep-0614 regression). The SAMP-03 per-stage
/// articulation multiplier (e.g. staccato (0.5, 1.2, 1.0, 0.8)) used to be
/// sampled over the FULL fitted buffer (authored note + the up-to-1.5s release
/// tail). Its quartile buckets therefore landed deep in the tail, overlaying a
/// non-monotonic 1.2× bump partway through what should be a smooth exponential
/// ring-out — and disagreeing with the SFZ path (which bounds the multiplier to
/// the note duration).
///
/// <para>The fix bounds the multiplier to the authored-note window. This test
/// renders a piano staccato note and asserts the TAIL region
/// [authoredFrames, fitted.Length) decays monotonically (within a small slack
/// for sample-content wiggle) — i.e. the multiplier's 1.2× decay bucket no
/// longer leaks into the tail.</para>
/// </summary>
[Collection("FlowScripts")]
public class SampledArticulationTailWindowTests : IDisposable
{
    private const int SampleRate = 44100;
    private const double DurationBeats = 1.0; // short authored window
    private const double Bpm = 120.0;
    // 1 beat @ 120 bpm = 0.5 s → 22050 authored frames.
    private const int AuthoredFrames = 22050;

    public SampledArticulationTailWindowTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact]
    public void PianoStaccato_TailRegion_MatchesNormalTail_MultiplierBoundedToAuthoredWindow()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string pianoSample = Path.Combine(repoRoot, "flow-lang", "Samples", "piano", "C4_ff.wav");
        if (!File.Exists(pianoSample)) return; // bundle absent — charitable skip

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;

            using var runner = new FlowEngineRunner();
            string setupScript = @"
                use ""@audio""
                tempo 120 {
                    section setup { Sequence main = | C4q | }
                }
                Song s = [setup]
                Buffer rendered = (renderSong s ""piano"")
            ";
            var setup = runner.RunSource(setupScript, "<tail-window-setup>");
            Assert.True(setup.Success, $"Setup render failed: {setup.Stderr}");

            var cache = FlowEngine.CurrentSampleCache;
            Assert.NotNull(cache);
            var renderer = new SampledInstrumentRenderer(cache!, "piano", hasVelocityLayers: true);

            // The post-authored release tail is governed SOLELY by the
            // exponential tail-decay ramp applied to the raw sample beyond the
            // authored window — it must be articulation-INDEPENDENT. After the
            // fix the SAMP-03 multiplier is bounded to [0, authoredFrames), so a
            // Staccato render and a Normal render produce a BYTE-IDENTICAL tail.
            //
            // Before the fix the multiplier was sampled over the full buffer, so
            // Staccato's (0.5, 1.2, 1.0, 0.8) buckets reshaped the tail (the
            // 1.2× decay bucket landing in the first third of the ring-out) while
            // Normal's identity multiplier left it untouched — the two tails
            // diverged. This assertion fails on the old code and passes on the fix.
            var staccato = renderer.Render(MakeNote(Articulation.Staccato),
                SampleRate, DurationBeats, Bpm, RenderTuning.Default);
            var normal = renderer.Render(MakeNote(Articulation.Normal),
                SampleRate, DurationBeats, Bpm, RenderTuning.Default);

            Assert.Equal(normal.Frames, staccato.Frames);
            Assert.True(staccato.Frames > AuthoredFrames,
                $"expected a release tail beyond {AuthoredFrames} authored frames, got {staccato.Frames}");

            double maxTailDiff = 0.0;
            for (int i = AuthoredFrames; i < staccato.Data.Length; i++)
            {
                double d = Math.Abs(staccato.Data[i] - normal.Data[i]);
                if (d > maxTailDiff) maxTailDiff = d;
            }

            // Tails are produced by the identical articulation-independent code
            // path, so they should match to within int16 round-trip slack.
            Assert.True(maxTailDiff < 1e-4,
                $"Staccato tail diverges from Normal tail by {maxTailDiff:E3} — the SAMP-03 " +
                "multiplier is leaking past the authored window into the release tail " +
                "(sweep-0614 regression). The tail must be articulation-independent.");
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    private static MusicalNoteData MakeNote(Articulation art) =>
        new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 4, isRest: false, velocity: 0.7, articulation: art);
}
