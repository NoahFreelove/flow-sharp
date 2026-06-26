using System;
using System.IO;
using System.Linq;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Helpers;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Debug2026;

/// <summary>
/// Debug session chord-dynamic-bar-doubling (2026-06-25).
///
/// A note-stream bar that mixed a dynamic marking (f/ff/mf/…) with chords rendered at
/// ~2× its notated duration. Root cause: NoteStreamCompiler.InterpolateVelocities rebuilt
/// each interpolated MIDDLE note with the 12-arg MusicalNoteData ctor, which silently reset
/// the 5 trailing fields — IsChordTone / DurationFraction / OnsetOffset / DurationOverlap /
/// PortamentoMs — to their defaults. Dropping IsChordTone made interpolated chord tones
/// advance the bar's beat cursor in BarType.ToTimeline instead of sharing the leading tone's
/// onset, so the bar's beat-sum ballooned.
///
/// InterpolateVelocities only runs when a bar carries ≥2 distinct non-rest velocities. A
/// dynamic token attaches a sticky velocity to plain NoteElements only (ChordElement never
/// carries it — Parser.NoteStream.cs:260), so a bar with chords (default velocity) plus a
/// plain note carrying the dynamic produces exactly the velocity spread that triggers
/// interpolation. That is why the bug needed BOTH a dynamic AND chords; single-note bars and
/// chord-only (no dynamic) bars were immune.
///
/// Fix: route the rebuild through MusicalNoteData.With(velocity: vel) so every untouched
/// field — IsChordTone included — survives by construction, while dynamics still set velocity.
/// This is the same drop-on-reconstruct pattern the 2026-06-09 transform audit fixed
/// (see TransformDataLossTests §4.2); InterpolateVelocities was the one path it missed.
///
/// Each Fact fails on the pre-fix code (the doubling repro rendered ~2.5× long for this bar)
/// and passes after.
/// </summary>
[Collection("FlowScripts")]
public class ChordDynamicBarDoublingTests
{
    private const string Prelude = "use \"@std\"\nuse \"@audio\"\n";

    // One 4/4 bar: leading dynamic f + three quarter chords + a plain quarter that carries the
    // sticky f velocity. Chords stay at the default velocity → the bar holds two distinct
    // velocities → InterpolateVelocities rewrites the six middle chord tones. Nominal = 4 beats.
    private const string DynChordBar =
        "Sequence s = | f [C4 E4 G4]q [C4 E4 G4]q [C4 E4 G4]q D4q |";

    private static SequenceData EvalSequence(string body, string varName)
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(Prelude + body);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nsource:\n{body}");
        return runner.GetVariable(varName).As<SequenceData>();
    }

    [Fact]
    public void DynamicPlusChordBar_TotalsNominalBeats_NotDoubled()
    {
        // The headline pin: after velocity interpolation, the bar still totals exactly its
        // nominal 4 beats. Pre-fix the six interpolated chord tones lost IsChordTone and each
        // advanced the cursor → GetActualBeats reported 10 (4 leads/notes + 6 stray tones).
        var seq = EvalSequence(DynChordBar, "s");
        var bar = seq.Bars[0];

        Assert.Equal(4.0, bar.GetActualBeats(), 6);

        // 3 chords × (1 lead + 2 chord tones) + 1 plain note = 10 entries; exactly 6 of them
        // (the E and G of each chord) must remain flagged IsChordTone after interpolation.
        Assert.Equal(10, bar.MusicalNotes.Count);
        Assert.Equal(6, bar.MusicalNotes.Count(n => n.IsChordTone));
    }

    [Fact]
    public void DynamicPlusChordBar_StillInterpolatesVelocity_DynamicsPreserved()
    {
        // Guard the fix didn't "fix" the bug by disabling dynamics: interpolation must still
        // run, so the non-rest velocities must spread across more than one value.
        var seq = EvalSequence(DynChordBar, "s");
        var vels = seq.Bars[0].MusicalNotes
            .Where(n => !n.IsRest)
            .Select(n => Math.Round(n.Velocity, 6))
            .Distinct()
            .ToArray();
        Assert.True(vels.Length > 1,
            "velocity interpolation did not run — the dynamic+chord bar would not exercise the bug");

        // The first chord's three tones still share a single onset at beat 0 (chord, not arpeggio).
        var timeline = seq.Bars[0].ToTimeline();
        var firstChordOnsets = timeline.Take(3).Select(t => Math.Round(t.offsetBeats, 6)).Distinct().ToArray();
        Assert.Single(firstChordOnsets);
        Assert.Equal(0.0, firstChordOnsets[0], 6);
    }

    [Fact]
    public void ChordBarWithoutDynamic_IsUnaffected_Control()
    {
        // Control: the same chord rhythm WITHOUT the leading dynamic never triggered
        // interpolation and always totalled 4 beats. Confirms the fix leaves the immune path
        // byte-for-byte unchanged in shape.
        var seq = EvalSequence(
            "Sequence s = | [C4 E4 G4]q [C4 E4 G4]q [C4 E4 G4]q D4q |", "s");
        Assert.Equal(4.0, seq.Bars[0].GetActualBeats(), 6);
        Assert.Equal(6, seq.Bars[0].MusicalNotes.Count(n => n.IsChordTone));
    }

    [Fact]
    public void DynamicPlusChordBar_RendersAtNominalDuration_EndToEnd()
    {
        // End-to-end mirror of the user's symptom: 8 copies of the dynamic+chord bar @175 BPM
        // in 4/4 (sine) should be 32 beats = 10.97 s. Pre-fix this rendered ~27.4 s (each bar
        // doubled-and-then-some by the stray chord tones).
        string oneBar = "f [C4 E4 G4]q [C4 E4 G4]q [C4 E4 G4]q D4q";
        string eightBars = string.Join(" | ", Enumerable.Repeat(oneBar, 8));
        string wavPath = Path.Combine(Path.GetTempPath(),
            $"flow_chord_dyn_doubling_{Guid.NewGuid():N}.wav");

        string source = Prelude +
            "tempo 175 { timesig 4/4 { key Cmajor {\n" +
            "  section sec { Sequence s = | " + eightBars + " | }\n" +
            "  Song s2 = [sec]\n" +
            $"  (writeWav \"{wavPath.Replace("\\", "/")}\" s2 \"sine\")\n" +
            "} } }";

        using (var runner = new FlowEngineRunner())
        {
            var (success, _, stderr, errorCount) = runner.RunSource(source);
            Assert.True(success && errorCount == 0,
                $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}");
        }

        Assert.True(File.Exists(wavPath), $"writeWav did not produce {wavPath}");
        var wav = WavReader.ReadWav(wavPath);
        File.Delete(wavPath);

        double durationSec = (double)wav.Frames / wav.SampleRate;
        // Nominal 10.97 s. Accept a generous window; the bug produced ~27.4 s, far outside it.
        Assert.InRange(durationSec, 10.0, 12.0);
    }
}
