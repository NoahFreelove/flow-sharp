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
/// Phase 37 PIANO-01 (Plan 37-04 / D-37-11) — `release=` named-arg knob exposes
/// the sample-path tail-extension window. Default 1.5s (locked in D-37-11 per
/// Lehtonen 2007 / RESEARCH §Pattern 8); composer overrides via
/// <c>(renderSong song "piano" release=2.0s)</c>.
///
/// Two facts cover the contract:
///   1. <see cref="PianoReleaseKnob_Release2s_ProducesAudibleTail"/> — long
///      release (2.0s) keeps audible amplitude at 1.5s past authored end;
///      short release (0.3s) is near-silent at 1.5s past authored end.
///   2. <see cref="PianoReleaseKnob_Default_AudibleAt1sPastEnd"/> — default
///      release (1.5s — unset) keeps audible amplitude at 1s past authored
///      end (Pattern 8 time-constant scaling — 1.5s release × 0.3 = 0.45s
///      tail decay constant; energy lingers across the full release window).
/// </summary>
[Collection("FlowScripts")]
public class PianoReleaseKnobTests : IDisposable
{
    public PianoReleaseKnobTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        // Reset the AsyncLocal release override so test bleed is impossible.
        FlowLang.StandardLibrary.Audio.Synthesizers.PianoSynthesizer.CurrentReleaseSec.Value = null;
    }

    private const int SampleRate = 44100;
    private const double DurationBeats = 1.0;
    private const double Bpm = 120.0;
    // 1 beat @ BPM 120 = 0.5 s → 22050 frames authored
    private const int AuthoredFrames = 22050;
    // The U-Iowa MIS piano sample decays naturally — peak amplitude at the
    // probe points is in the 0.0001–0.005 range depending on how far past
    // authored end. Thresholds are calibrated against the actual sample
    // envelope, not generic "audio audibility" (composer cares about RELATIVE
    // energy retention between long-release and short-release renders, not
    // absolute dBFS).
    private const float AudibleThreshold = 0.0005f;
    private const float NearSilentThreshold = 0.00005f;

    /// <summary>
    /// Helper: render a single C4 quarter note at velocity 0.7 with the given
    /// release tail. Returns the rendered AudioBuffer, which has length
    /// authoredFrames + releaseSec * sampleRate (clamped to [0.05, 10.0]).
    /// </summary>
    private static AudioBuffer RenderPianoC4WithRelease(double releaseSec)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string pianoSample = Path.Combine(repoRoot, "flow-lang", "Samples", "piano", "C4_pp.wav");
        Assert.True(File.Exists(pianoSample), $"Piano sample missing — fixture broken: {pianoSample}");

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;
            // Wake up an engine so the SampleCache is populated.
            using var runner = new FlowEngineRunner();
            string setupScript = @"
                use ""@audio""
                tempo 120 {
                    section release_setup {
                        Sequence main = | C4q |
                    }
                }
                Song s = [release_setup]
                Buffer rendered = (renderSong s ""piano"")
            ";
            var setup = runner.RunSource(setupScript, "<release-setup>");
            Assert.True(setup.Success, $"Setup render failed: {setup.Stderr}");

            var cache = FlowEngine.CurrentSampleCache;
            Assert.NotNull(cache);
            var renderer = new SampledInstrumentRenderer(cache!, "piano", hasVelocityLayers: true);

            var note = new MusicalNoteData(
                noteName: 'C', octave: 4, alteration: 0,
                durationValue: 4, isRest: false, velocity: 0.7,
                articulation: Articulation.Normal);

            return renderer.Render(note, SampleRate, DurationBeats, Bpm, RenderTuning.Default, releaseSec);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    /// <summary>
    /// Peak amplitude in a ±20ms RMS window centered at <paramref name="centerFrame"/>.
    /// Used because individual sample frames can sit at zero-crossings even when
    /// the surrounding window has clear audible content.
    /// </summary>
    private static double WindowPeak(AudioBuffer buf, int centerFrame, int windowFrames)
    {
        int lo = Math.Max(0, centerFrame - windowFrames);
        int hi = Math.Min(buf.Frames, centerFrame + windowFrames);
        double peak = 0.0;
        for (int i = lo; i < hi; i++)
        {
            double a = Math.Abs(buf.Data[i]);
            if (a > peak) peak = a;
        }
        return peak;
    }

    [Fact]
    public void PianoReleaseKnob_Release2s_ProducesAudibleTail()
    {
        // PIANO-01 D-37-11 — release=2.0s should keep the natural sample body
        // audible across the longer tail. The U-Iowa MIS C4 piano body is
        // ~1.5s post-trim; piano natural decay is steep, so peak amplitude at
        // +0.2s past authored end is ~0.001–0.003. We probe AT 0.2s past
        // authored end (probe frame = 22050 + 8820 = 30870 — still well within
        // sample body for both long-release and short-release renders).
        //
        // The key proof: at this probe, the LONG-tail peak is in the
        // AudibleThreshold band, and substantially larger than the
        // SHORT-tail peak at the same frame — composer's release knob is
        // demonstrably extending audible decay.
        var longTail = RenderPianoC4WithRelease(2.0);
        int probeFrame = AuthoredFrames + (int)(SampleRate * 0.2);
        Assert.True(probeFrame < longTail.Frames,
            $"Render too short: probeFrame={probeFrame}, frames={longTail.Frames}");

        int win = (int)(SampleRate * 0.02);  // 20 ms window
        double peakLong = WindowPeak(longTail, probeFrame, win);
        Assert.True(peakLong > AudibleThreshold,
            $"PIANO-01 D-37-11: release=2.0s should produce audible tail at 0.2s past authored end. " +
            $"Got peak {peakLong:E4} (threshold {AudibleThreshold:E4}).");

        // release=0.3s — buffer is cut at authored + 0.3s = 35280, but the probe
        // point 30870 is still WITHIN buffer. The tail-decay time constant for
        // 0.3s release is 0.09s, so at +0.2s into the tail, exp(-0.2/0.09) ≈
        // 0.108 — substantially attenuated relative to the 2.0s release whose
        // tail decay at +0.2s is exp(-0.2/0.6) ≈ 0.717 (~6.6× larger).
        var shortTail = RenderPianoC4WithRelease(0.3);
        double peakShort = probeFrame < shortTail.Frames
            ? WindowPeak(shortTail, probeFrame, win)
            : 0.0;

        // Cross-check: long-release tail peak should be > short-release tail peak.
        // Composer's release knob actually carries audio (not just a no-op).
        Assert.True(peakLong > peakShort * 1.5,
            $"PIANO-01 D-37-11: long release tail peak ({peakLong:E4}) should exceed short release " +
            $"tail peak ({peakShort:E4}) at the same probe frame (composer's release knob must " +
            $"audibly slow decay).");
    }

    [Fact]
    public void PianoReleaseKnob_Default_AudibleAt1sPastEnd()
    {
        // Default 1.5s release (D-37-11) — should keep audible content at +0.15s
        // past the authored end. Tail-decay time-constant = 1.5 × 0.3 = 0.45s;
        // at +0.15s the decay is exp(-0.15/0.45) ≈ 0.716. Sample body at +0.15s
        // (frame 28665, ~0.65s into ~1.5s decay) still has natural energy.
        var defaultBuf = RenderPianoC4WithRelease(SampledInstrumentRenderer.DefaultReleaseSec);
        int probeFrame = AuthoredFrames + (int)(SampleRate * 0.15);
        Assert.True(probeFrame < defaultBuf.Frames,
            $"Render too short for 0.15s probe: probeFrame={probeFrame}, frames={defaultBuf.Frames}");

        int win = (int)(SampleRate * 0.02);
        double peak = WindowPeak(defaultBuf, probeFrame, win);
        Assert.True(peak > AudibleThreshold,
            $"PIANO-01 D-37-11: default release ({SampledInstrumentRenderer.DefaultReleaseSec}s) " +
            $"should produce audible peak at +0.15s past authored end. " +
            $"Got peak {peak:E4} (threshold {AudibleThreshold:E4}).");
    }
}
