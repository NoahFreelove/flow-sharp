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

namespace FlowLang.Tests.Integration.Debug2026;

/// <summary>
/// Debug session <c>varispeed-aliasing-static</c> (2026-06-26) — per-beat "static"
/// on sampled-piano renders, worst in dense high-register passages (ragtime RH).
///
/// <para>ROOT CAUSE (confirmed by direct waveform measurement, NOT the originally
/// suspected varispeed decimation aliasing — that lead was falsified): in
/// <see cref="SampledInstrumentRenderer.Render"/> the Phase 28 ADSR envelope is
/// applied only to <c>[0, authoredFrames)</c> and its 0.05 s RELEASE phase ramps
/// the signal to ~0 at frame <c>authoredFrames</c>. The release-tail loop then
/// restarts at <c>level = 1.0</c> multiplying the RAW (un-enveloped) sample, so the
/// signal jumps from ~0 back to the full sample amplitude in a single sample — a
/// step discontinuity at every note's authored-end. The step size equals the raw
/// sample amplitude at that frame, so it is LARGE for short notes (authored end
/// early in the still-loud sample) and sub-threshold for long ones. Dense short
/// notes whose authored-ends align on the beat grid stack these steps into the
/// audible per-beat "static".</para>
///
/// <para>This test renders a SHORT loud Normal piano note and asserts the
/// single-sample jump at the envelope/tail boundary (frame <c>authoredFrames</c>)
/// is comparable to the smooth-tail neighbour diffs — NOT an order of magnitude
/// larger. It FAILS on the pre-fix code (boundary jump ~10-100x the neighbours) and
/// PASSES once the envelope and exponential release tail meet continuously.</para>
/// </summary>
[Collection("FlowScripts")]
public class SampledEnvelopeTailContinuityTests : IDisposable
{
    private const int SampleRate = 44100;
    // 0.1 beat @ 120 bpm = 0.05 s = 2205 frames — SHORTER than the renderer's
    // attack(220)+decay(2205)=2425-frame budget, so GenerateADSRCurve's rescale path
    // fires. That path used to dump its floor-rounding leftover into the release ramp
    // even when baseRelease=0, dipping the last authored frame(s) to ~0 while the tail
    // restarts at full amplitude. This duration exercises BOTH the baseRelease=0 seam
    // and the rescale-leftover seam — the authored window lands while the piano sample
    // is still loud, so any residual discontinuity is unmistakable.
    private const double DurationBeats = 0.1;
    private const double Bpm = 120.0;

    public SampledEnvelopeTailContinuityTests()
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
    public void ShortPianoNote_EnvelopeTailBoundary_IsContinuous_NoPerBeatStep()
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
            var setup = runner.RunSource(setupScript, "<tail-continuity-setup>");
            Assert.True(setup.Success, $"Setup render failed: {setup.Stderr}");

            var cache = FlowEngine.CurrentSampleCache;
            Assert.NotNull(cache);
            var renderer = new SampledInstrumentRenderer(cache!, "piano", hasVelocityLayers: true);

            // Loud Normal note — the bug's step magnitude scales with the raw sample
            // level at the boundary, so a forte velocity makes the pre-fix step
            // unmistakable.
            var note = MakeNote(Articulation.Normal, velocity: 0.9);
            var buf = renderer.Render(note, SampleRate, DurationBeats, Bpm, RenderTuning.Default);
            float[] d = buf.Data; // mono (ToMonoBuffer)

            // The renderer computes authoredFrames = (int)(durationSeconds * sr).
            double durationSeconds = DurationBeats * 60.0 / Bpm;
            int authoredFrames = (int)(durationSeconds * SampleRate);

            Assert.True(d.Length > authoredFrames + 256,
                $"expected a release tail beyond {authoredFrames} authored frames, got {d.Length}");

            // Single-sample jump exactly at the envelope/tail boundary.
            double boundaryJump = Math.Abs(d[authoredFrames] - d[authoredFrames - 1]);

            // Reference smoothness: the largest adjacent-sample diff in the smooth
            // tail region just past the boundary. The decaying sample is continuous
            // here, so this is the natural per-sample slew of the signal at this
            // amplitude — the boundary jump must not dwarf it.
            double maxNeighbor = 0.0;
            for (int i = authoredFrames + 2; i < authoredFrames + 256 && i < d.Length; i++)
                maxNeighbor = Math.Max(maxNeighbor, Math.Abs(d[i] - d[i - 1]));

            // A clean boundary has a jump on the order of the local slew. The pre-fix
            // discontinuity is the full raw-sample amplitude (~10-100x the neighbour
            // slew). 4x + a small absolute floor is a generous gate that the fix
            // clears and the bug fails.
            Assert.True(boundaryJump <= 4.0 * maxNeighbor + 1e-4,
                $"Envelope/tail boundary at frame {authoredFrames} jumps {boundaryJump:E3} but the " +
                $"smooth-tail neighbour slew is only {maxNeighbor:E3} ({boundaryJump / Math.Max(maxNeighbor, 1e-9):F1}x). " +
                "The ADSR release ramps to ~0 at the authored end while the release-tail loop restarts " +
                "at level=1.0 on the raw sample — a per-note step that stacks into the audible per-beat static.");
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    private static MusicalNoteData MakeNote(Articulation art, double velocity) =>
        new MusicalNoteData(
            noteName: 'C', octave: 4, alteration: 0,
            durationValue: 16, isRest: false, velocity: velocity, articulation: art);
}
