using System;
using System.IO;
using System.Linq;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Helpers;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 Packet A — transform data loss (worst composer-facing language bugs).
///
///   §4.1  Every bar-rebuilding transform dropped BarData.ParallelVoices → voice-block
///         sequences render SILENT after transpose / invert / legato / augment / diminish /
///         retrograde / repeat / concat / swell / crescendo / ritardando / fermata / quantize /
///         humanize. Fix: TransformBar / CloneBarWithVoices / ReverseBar / MapNotesIndexed /
///         QuantizeBar / Trill/TremoloBar all recurse into ParallelVoices.
///   §4.2  Transforms rebuilt notes via the 12-arg ctor, stripping IsChordTone (chord brackets
///         re-arpeggiate + overflow the bar), DurationFraction (tuplets flatten), OnsetOffset
///         (quantize undone), DurationOverlap (legato), PortamentoMs. Fix: every rebuild routes
///         through MusicalNoteData.With(...), now extended with pitch + duration slots.
///   §4.5  trill dropped isDotted (dotted notes lost 1/3 duration) + dropped CentOffset /
///         Articulation on the upper neighbour; tremolo subdivided at a fixed 1/4 regardless of
///         reps (only reps=4 preserved length). Fix: derive subdivision from reps + carry isDotted.
///   §10-gap-3  (transpose seq +50c) rounded cents to whole semitones — a silent no-op. Fix:
///         whole-semitone part shifts pitch, fractional remainder folds into each note's CentOffset.
///
/// Each Fact fails on the pre-fix code and passes after.
/// </summary>
[Collection("FlowScripts")]
public class TransformDataLossTests
{
    private const string Prelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    private static string TempWav(string name) =>
        Path.Combine(Path.GetTempPath(), $"flow_audit0609_{name}_{Guid.NewGuid():N}.wav");

    /// <summary>Run Flow source that ends with (writeWav PATH mix); read the WAV back.</summary>
    private static AudioBuffer RenderToWav(string body, string testName)
    {
        string wavPath = TempWav(testName);
        string source = Prelude + body.Replace("{{WAV}}", wavPath.Replace("\\", "/"));
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nsource:\n{source}");
        Assert.True(File.Exists(wavPath), $"writeWav did not produce {wavPath}\nstderr:\n{stderr}");
        var wav = WavReader.ReadWav(wavPath);
        File.Delete(wavPath);
        return wav;
    }

    private static double Rms(AudioBuffer buf)
    {
        double sumSq = 0.0;
        long count = 0;
        for (int i = 0; i < buf.Frames; i++)
            for (int ch = 0; ch < buf.Channels; ch++)
            {
                double s = buf.GetSample(i, ch);
                sumSq += s * s;
                count++;
            }
        return count == 0 ? 0.0 : Math.Sqrt(sumSq / count);
    }

    private static SequenceData EvalSequence(string body, string varName, out string stderr)
    {
        using var runner = new FlowEngineRunner();
        var (success, _, err, errorCount) = runner.RunSource(Prelude + body);
        stderr = err;
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{err}\nsource:\n{body}");
        return runner.GetVariable(varName).As<SequenceData>();
    }

    // ==================================================================
    // §4.1 — voice-block content survives bar-rebuilding transforms
    // ==================================================================

    // A two-voice bar: held C4 whole + running C5 D5 E5 F5 quarters.
    private const string VoicedSeq =
        @"Sequence src = | {voice C4w} {voice C5q D5q E5q F5q} |";

    [Fact]
    public void Transpose_VoiceBlockBar_RendersNonSilent_AndKeepsVoiceCount()
    {
        // §4.1: transpose used to drop ParallelVoices → silent WAV. Now the transposed
        // voice-block bar still carries both voices and renders audibly.
        var wav = RenderToWav(VoicedSeq + @"
Sequence moved = (transpose src +2st)
section main { Sequence v = moved }
Song s = [main]
Buffer mix = (renderSong s ""organ"")
(writeWav ""{{WAV}}"" mix)
", nameof(Transpose_VoiceBlockBar_RendersNonSilent_AndKeepsVoiceCount));

        Assert.True(Rms(wav) > 1e-3,
            $"transpose of a voice-block sequence rendered SILENT (RMS={Rms(wav):E2}) — ParallelVoices dropped (§4.1)");

        // Structural: voice count preserved on the transposed bar.
        var moved = EvalSequence(VoicedSeq + "\nSequence moved = (transpose src +2st)", "moved", out _);
        Assert.NotEmpty(moved.Bars);
        Assert.NotNull(moved.Bars[0].ParallelVoices);
        Assert.Equal(2, moved.Bars[0].ParallelVoices!.Count);
        // First voice = held C4 (transposed up 2 → D4); second voice = 4 running notes.
        Assert.Single(moved.Bars[0].ParallelVoices![0].MusicalNotes);
        Assert.Equal(4, moved.Bars[0].ParallelVoices![1].MusicalNotes.Count);
        Assert.Equal('D', moved.Bars[0].ParallelVoices![0].MusicalNotes[0].NoteName); // C4 +2st = D4
    }

    [Fact]
    public void Concat_VoiceBlockBars_RendersNonSilent()
    {
        // §4.1: concat used to drop ParallelVoices in both halves → silent WAV.
        var wav = RenderToWav(VoicedSeq + @"
Sequence joined = (concat src src)
section main { Sequence v = joined }
Song s = [main]
Buffer mix = (renderSong s ""organ"")
(writeWav ""{{WAV}}"" mix)
", nameof(Concat_VoiceBlockBars_RendersNonSilent));

        Assert.True(Rms(wav) > 1e-3,
            $"concat of voice-block sequences rendered SILENT (RMS={Rms(wav):E2}) — ParallelVoices dropped (§4.1)");

        var joined = EvalSequence(VoicedSeq + "\nSequence joined = (concat src src)", "joined", out _);
        Assert.Equal(2, joined.Bars.Count);
        Assert.NotNull(joined.Bars[0].ParallelVoices);
        Assert.NotNull(joined.Bars[1].ParallelVoices);
        Assert.Equal(2, joined.Bars[1].ParallelVoices!.Count);
    }

    [Fact]
    public void Retrograde_VoiceBlockBar_RendersNonSilent()
    {
        // §4.1: retrograde used to drop ParallelVoices → silent WAV.
        var wav = RenderToWav(VoicedSeq + @"
Sequence rev = (retrograde src)
section main { Sequence v = rev }
Song s = [main]
Buffer mix = (renderSong s ""organ"")
(writeWav ""{{WAV}}"" mix)
", nameof(Retrograde_VoiceBlockBar_RendersNonSilent));

        Assert.True(Rms(wav) > 1e-3,
            $"retrograde of a voice-block sequence rendered SILENT (RMS={Rms(wav):E2}) — ParallelVoices dropped (§4.1)");

        var rev = EvalSequence(VoicedSeq + "\nSequence rev = (retrograde src)", "rev", out _);
        Assert.NotNull(rev.Bars[0].ParallelVoices);
        Assert.Equal(2, rev.Bars[0].ParallelVoices!.Count);
        // The running voice's notes are reversed: C5 D5 E5 F5 -> F5 E5 D5 C5.
        var running = rev.Bars[0].ParallelVoices![1].MusicalNotes;
        Assert.Equal(4, running.Count);
        Assert.Equal('F', running[0].NoteName);
        Assert.Equal('C', running[3].NoteName);
    }

    // ==================================================================
    // §4.2 — chord brackets / tuplets / quantize fields survive transforms
    // ==================================================================

    [Fact]
    public void Transpose_ChordBracket_KeepsTwoBeatsAndSharedOnset()
    {
        // (transpose | [C4 E4 G4]q D4q | +2st): the chord-quarter + D4-quarter = 2 beats.
        // Pre-fix, IsChordTone was dropped → the three chord tones became sequential quarters
        // (3 beats of chord + 1 of D4 = 4 beats; bar overflows + chord arpeggiates).
        var moved = EvalSequence(
            "Sequence src = | [C4 E4 G4]q D4q |\nSequence moved = (transpose src +2st)",
            "moved", out _);

        var bar = moved.Bars[0];
        Assert.Equal(4, bar.MusicalNotes.Count); // 3 chord tones + D4

        // Exactly two of the four notes are NOT chord tones (the chord leading tone + D4),
        // i.e. two chord tones (E,G transposed) keep IsChordTone=true.
        int chordTones = bar.MusicalNotes.Count(n => n.IsChordTone);
        Assert.Equal(2, chordTones);

        // Bar's actual beats counts chord tones once (shared onset) → quarter + quarter = 2.
        Assert.Equal(2.0, bar.GetActualBeats(), 6);

        // The three transposed chord tones share one onset in the timeline.
        var timeline = bar.ToTimeline();
        var chordOnsets = timeline.Take(3).Select(t => t.offsetBeats).Distinct().ToArray();
        Assert.Single(chordOnsets);
        Assert.Equal(0.0, chordOnsets[0], 6);

        // Pitch shifted up 2 semitones: C->D, E->F#, G->A.
        Assert.Equal('D', timeline[0].note.NoteName);
    }

    [Fact]
    public void QuantizeThenTranspose_PreservesOnsetOffset()
    {
        // quantize stamps OnsetOffset; transpose must not reset it (it rebuilt via 12-arg ctor).
        var seq = EvalSequence(@"
Sequence src = | C4e C4e C4e C4e |
Sequence q = (quantize src QUARTER 1.0 0.0)
Sequence moved = (transpose q +2st)
", "moved", out _);

        // At least one note carries a non-zero OnsetOffset after quantize→transpose.
        bool anyOffset = seq.Bars.SelectMany(b => b.MusicalNotes)
            .Any(n => Math.Abs(n.OnsetOffset) > 1e-9);
        Assert.True(anyOffset,
            "transpose after quantize reset every OnsetOffset to 0 — quantize undone (§4.2)");
    }

    [Fact]
    public void Transpose_Tuplet_PreservesDurationFraction()
    {
        // A triplet (DurationFraction set) must survive transpose with its fraction intact.
        // Build the tuplet directly so we control DurationFraction, then transpose.
        var input = BuildTripletSequence();
        var transposed = FlowLang.StandardLibrary.Transforms.TransformFunctions
            .ApplyTransposeForTesting(input, 2);

        var notes = transposed.Bars[0].MusicalNotes;
        Assert.Equal(3, notes.Count);
        foreach (var n in notes)
        {
            Assert.True(n.DurationFraction.HasValue,
                "transpose dropped DurationFraction — tuplet reverted to power-of-2 timing (§4.2)");
            Assert.Equal(new FlowLang.TypeSystem.Fraction(1, 3), n.DurationFraction);
        }
    }

    private static SequenceData BuildTripletSequence()
    {
        var seq = new SequenceData();
        var notes = new[]
        {
            new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
                durationFraction: new FlowLang.TypeSystem.Fraction(1, 3)),
            new MusicalNoteData('D', 4, 0, (int)NoteValueType.Value.QUARTER, false,
                durationFraction: new FlowLang.TypeSystem.Fraction(1, 3)),
            new MusicalNoteData('E', 4, 0, (int)NoteValueType.Value.QUARTER, false,
                durationFraction: new FlowLang.TypeSystem.Fraction(1, 3)),
        };
        seq.AddBar(new BarData(notes, new TimeSignatureData(4, 4)));
        return seq;
    }

    // ==================================================================
    // §4.5 — trill / tremolo duration math
    // ==================================================================

    [Fact]
    public void Trill_DottedHalf_FillsThreeBeats()
    {
        // trill on C4h. (dotted half = 3 beats) must fill 3 beats. Pre-fix dropped isDotted
        // and used a fixed 4 alternations of eighths = 2 beats (lost the dot's third beat).
        var trilled = EvalSequence(
            "Sequence src = | C4h. |\nSequence t = (trill src +2st)", "t", out _);

        var bar = trilled.Bars[0];
        double beats = bar.MusicalNotes.Sum(n => n.GetBeats(bar.TimeSignature!.Denominator));
        Assert.Equal(3.0, beats, 6);

        // 6 eighths fill the dotted half; each is a plain (non-dotted) eighth.
        Assert.Equal(6, bar.MusicalNotes.Count);
        Assert.All(bar.MusicalNotes, n => Assert.False(n.IsDotted));
    }

    [Fact]
    public void Trill_PlainHalf_FillsTwoBeats()
    {
        // Regression guard: a plain half still trills to 4 eighths = 2 beats.
        var trilled = EvalSequence(
            "Sequence src = | C4h |\nSequence t = (trill src +2st)", "t", out _);
        var bar = trilled.Bars[0];
        double beats = bar.MusicalNotes.Sum(n => n.GetBeats(bar.TimeSignature!.Denominator));
        Assert.Equal(2.0, beats, 6);
        Assert.Equal(4, bar.MusicalNotes.Count);
    }

    [Fact]
    public void Trill_UpperNeighbour_CarriesCentOffsetAndArticulation()
    {
        // §4.5: the alternation's upper neighbour kept defaults (CentOffset null, Normal artic.)
        // while the lower kept the source's. Now both carry them. Build the source note in C#
        // (CentOffset +25, Marcato) so we control those fields exactly, then trill.
        var input = new SequenceData();
        var note = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            centOffset: 25.0, articulation: Articulation.Marcato);
        input.AddBar(new BarData(new[] { note }, new TimeSignatureData(4, 4)));

        var trilled = FlowLang.StandardLibrary.Transforms.TransformFunctions
            .TrillForTesting(input, 2);
        var notes = trilled.Bars[0].MusicalNotes;
        Assert.True(notes.Count >= 2, "trill produced too few alternations");

        // Index 0 = lower (source pitch), index 1 = upper neighbour.
        var upper = notes[1];
        Assert.Equal(Articulation.Marcato, upper.Articulation);
        Assert.NotNull(upper.CentOffset);
        Assert.Equal(25.0, upper.CentOffset!.Value, 6);
        Assert.Equal('D', upper.NoteName); // C4 + 2st = D4 upper neighbour
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Tremolo_AnyReps_PreservesTotalDuration(int reps)
    {
        // §4.5: tremolo subdivided at a fixed 1/4 regardless of reps. reps=2 halved length,
        // reps=8 doubled it. Now N repetitions fill exactly the source note's duration.
        var trem = EvalSequence(
            $"Sequence src = | C4q |\nSequence t = (tremolo src {reps})", "t", out _);
        var bar = trem.Bars[0];
        double beats = bar.MusicalNotes.Sum(n => n.GetBeats(bar.TimeSignature!.Denominator));
        Assert.Equal(1.0, beats, 6); // a quarter = 1 beat in 4/4 regardless of reps
        Assert.Equal(reps, bar.MusicalNotes.Count);
    }

    [Fact]
    public void Tremolo_DottedHalf_PreservesDuration()
    {
        // Dotted source: total must stay 3 beats with the dot honoured.
        var trem = EvalSequence(
            "Sequence src = | C4h. |\nSequence t = (tremolo src 8)", "t", out _);
        var bar = trem.Bars[0];
        double beats = bar.MusicalNotes.Sum(n => n.GetBeats(bar.TimeSignature!.Denominator));
        Assert.Equal(3.0, beats, 6);
        Assert.Equal(8, bar.MusicalNotes.Count);
    }

    // ==================================================================
    // Sweep 2026-06-14 — trill / tremolo on chord brackets + tuplets;
    //                    invert on voice blocks; humanize determinism.
    // ==================================================================

    [Fact]
    public void Trill_ChordBracket_UpperTonesGetDistinctOnsets()
    {
        // (trill | [C4 E4 G4]h | +2st): the chord must trill AS A UNIT — every subdivision
        // is a full stacked chord. Pre-fix, only the lead (C4) trilled over time while the
        // upper tones (E4/G4) all piled onto the lead's LAST onset (single-onset cluster).
        var trilled = EvalSequence(
            "Sequence src = | [C4 E4 G4]h |\nSequence t = (trill src +2st)", "t", out _);

        var bar = trilled.Bars[0];
        var timeline = bar.ToTimeline();

        // Half-note lead → 4 eighth-note subdivisions; each is C+E+G → 12 timeline entries.
        Assert.Equal(12, timeline.Count);

        // The bar still totals exactly 2 beats (a half) — chord tones don't advance the cursor.
        Assert.Equal(2.0, bar.GetActualBeats(), 6);

        // Group the timeline by pitch class of the UPPER (transposed) chord tones. E4 trills
        // E<->F#, G4 trills G<->A. Collect onsets for the non-C entries and assert they span
        // MORE THAN ONE distinct onset (the bug collapsed them all to a single tick).
        var nonLeadOnsets = timeline
            .Where(e => e.note.NoteName != 'C')  // C is the lead's own trill (C<->D)
            .Select(e => Math.Round(e.offsetBeats, 6))
            .Distinct()
            .ToArray();
        Assert.True(nonLeadOnsets.Length >= 4,
            $"upper chord tones collapsed to {nonLeadOnsets.Length} onset(s) — expected ≥4 " +
            "(one per trill subdivision)");
    }

    [Fact]
    public void Tremolo_ChordBracket_UpperTonesGetDistinctOnsets()
    {
        // (tremolo | [C4 E4 G4]q | 4): each of the 4 repetitions is a full C+E+G chord.
        // Pre-fix, the upper tones piled onto the lead's last onset.
        var trem = EvalSequence(
            "Sequence src = | [C4 E4 G4]q |\nSequence t = (tremolo src 4)", "t", out _);

        var bar = trem.Bars[0];
        var timeline = bar.ToTimeline();

        // Quarter lead at reps=4 → 4 sixteenth repetitions; each is C+E+G → 12 entries.
        Assert.Equal(12, timeline.Count);
        // Quarter chord = 1 beat, chord tones share onsets → still 1 beat total.
        Assert.Equal(1.0, bar.GetActualBeats(), 6);

        var upperOnsets = timeline
            .Where(e => e.note.NoteName != 'C')   // E and G repetitions
            .Select(e => Math.Round(e.offsetBeats, 6))
            .Distinct()
            .ToArray();
        Assert.True(upperOnsets.Length >= 4,
            $"upper chord tones collapsed to {upperOnsets.Length} onset(s) — expected ≥4 reps");
    }

    [Fact]
    public void Trill_Tuplet_HonoursDurationFraction_AndStaysInBar()
    {
        // A triplet quarter (DurationFraction = 1/3 quarter-units) trilled must keep its
        // tuplet timing: each alternation's duration = base/alternations, summing back to the
        // source note's 1/3 quarter. Pre-fix, With(durationValue:) left the OLD DurationFraction
        // intact → every alternation kept the full 1/3 quarter → the trill overflowed the bar.
        var input = BuildTripletSequence();
        var trilled = FlowLang.StandardLibrary.Transforms.TransformFunctions
            .TrillForTesting(input, 2);

        var notes = trilled.Bars[0].MusicalNotes;
        // 3 source triplet notes, each → ≥2 alternations.
        Assert.True(notes.Count >= 6, $"too few alternations: {notes.Count}");

        // Every emitted note must carry a (shortened) DurationFraction, NOT the stale 1/3.
        Assert.All(notes, n => Assert.True(n.DurationFraction.HasValue,
            "trill dropped DurationFraction on a tuplet alternation"));
        Assert.All(notes, n => Assert.NotEqual(new FlowLang.TypeSystem.Fraction(1, 3),
            n.DurationFraction!.Value));

        // The whole bar still totals exactly 1 beat (three triplet quarters = 1 quarter).
        double beats = notes.Sum(n => n.GetBeats(trilled.Bars[0].TimeSignature!.Denominator));
        Assert.Equal(1.0, beats, 6);
    }

    [Fact]
    public void Trill_Tuplet_NoBarOverflow_EndToEnd()
    {
        // End-to-end guard mirroring the repro: a triplet followed by plain quarters, trilled,
        // must not overflow one 4/4 bar. We assert the rendered bar's total beats == 4.
        var input = new SequenceData();
        var notes = new[]
        {
            new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
                durationFraction: new FlowLang.TypeSystem.Fraction(1, 3)),
            new MusicalNoteData('D', 4, 0, (int)NoteValueType.Value.QUARTER, false,
                durationFraction: new FlowLang.TypeSystem.Fraction(1, 3)),
            new MusicalNoteData('E', 4, 0, (int)NoteValueType.Value.QUARTER, false,
                durationFraction: new FlowLang.TypeSystem.Fraction(1, 3)),
            new MusicalNoteData('D', 4, 0, (int)NoteValueType.Value.QUARTER, false),
            new MusicalNoteData('E', 4, 0, (int)NoteValueType.Value.QUARTER, false),
            new MusicalNoteData('F', 4, 0, (int)NoteValueType.Value.QUARTER, false),
        };
        input.AddBar(new BarData(notes, new TimeSignatureData(4, 4)));

        var trilled = FlowLang.StandardLibrary.Transforms.TransformFunctions
            .TrillForTesting(input, 2);
        var bar = trilled.Bars[0];
        double beats = bar.MusicalNotes.Sum(n => n.GetBeats(bar.TimeSignature!.Denominator));
        // Triplet = 1 quarter, three plain quarters = 3 → 4 beats. Pre-fix this ran to ~6.75.
        Assert.Equal(4.0, beats, 6);
    }

    [Fact]
    public void Invert_VoiceBlockSequence_ActuallyInverts()
    {
        // (invert | {voice C4w} {voice E4q F4q G4q A4q} |): the axis must be found inside the
        // voice blocks (the parent bar holds only a whole-bar rest placeholder). Pre-fix,
        // axisMidi stayed null → invert returned a byte-identical clone (silent no-op).
        var vb = EvalSequence(
            "Sequence src = | {voice C4w} {voice E4q F4q G4q A4q} |\n" +
            "Sequence inv = (invert src)", "inv", out _);

        // Axis is the first non-rest note in document order = C4 (whole). Inversion is
        // 2*axis - midi. C4 inverts to itself; the running voice (E F G A above C4) inverts
        // DOWN below C4. Assert at least one voice note ended up below the original C4.
        Assert.NotNull(vb.Bars[0].ParallelVoices);
        var allVoiceNotes = vb.Bars[0].ParallelVoices!
            .SelectMany(v => v.MusicalNotes)
            .Where(n => !n.IsRest)
            .ToArray();
        Assert.NotEmpty(allVoiceNotes);

        const int c4 = 60; // MIDI C4
        // The running voice (E4=64, F4=65, G4=67, A4=69 — all ABOVE the C4 axis) must invert
        // DOWN to pitches < C4. Pre-fix the no-op clone left them all at their original (>C4)
        // pitches, so no pitch landed below the axis.
        var inverted = allVoiceNotes.Select(NoteMidi).ToArray();
        Assert.True(inverted.Any(m => m < c4),
            "invert was a no-op on the voice-block sequence (no pitch landed below the C4 axis)");
    }

    private static int NoteMidi(MusicalNoteData n)
    {
        // Mirror TransformFunctions.ToMidi: (octave+1)*12 + semitone(noteName)+alteration.
        int[] baseSemitone = { 9, 11, 0, 2, 4, 5, 7 }; // A B C D E F G
        int idx = n.NoteName - 'A';
        return (n.Octave + 1) * 12 + baseSemitone[idx] + n.Alteration;
    }

    [Fact]
    public void Humanize_Uniform_IsDeterministicAcrossRenders()
    {
        // Sweep 2026-06-14: uniform humanize used a process-global wall-clock new Random(),
        // so two renders of the same (humanize seq amount) -> writeWav produced different
        // bytes (violating two-run cmp-clean). Now it routes through PrngRegistry (reseeded
        // at the render boundary), so two independent renders are byte-identical.
        const string body = @"
tempo 120 { timesig 4/4 {
  Sequence mel = | C4q D4q E4q F4q |
  Sequence h = (humanize mel 0.5)
  section a { Sequence v = h }
  Song sa = [a]
  Buffer b = (renderSong sa ""organ"")
  (writeWav ""{{WAV}}"" b)
} }";
        var first = RenderToWav(body, "humanize_det1");
        var second = RenderToWav(body, "humanize_det2");

        Assert.Equal(first.Frames, second.Frames);
        Assert.Equal(first.Channels, second.Channels);
        for (int f = 0; f < first.Frames; f++)
            for (int ch = 0; ch < first.Channels; ch++)
                Assert.Equal(first.GetSample(f, ch), second.GetSample(f, ch));
    }

    [Fact]
    public void Humanize_Uniform_ActuallyJittersVelocity()
    {
        // Guard the fix didn't accidentally neuter humanize: with amount=1.0 the velocities
        // must spread (not all identical to the un-humanized base).
        var seq = EvalSequence(
            "Sequence src = | C4q C4q C4q C4q C4q C4q C4q C4q |\n" +
            "Sequence h = (humanize src 1.0)", "h", out _);
        var vels = seq.Bars[0].MusicalNotes.Where(n => !n.IsRest).Select(n => n.Velocity).ToArray();
        Assert.Equal(8, vels.Length);
        Assert.True(vels.Distinct().Count() > 1, "humanize produced no velocity variation");
    }

    // ==================================================================
    // §10-gap-3 — true cent-precision transpose
    // ==================================================================

    [Fact]
    public void Transpose_50c_ShiftsEveryNoteCentOffsetBy50()
    {
        // (transpose seq +50c): pitch unchanged (0 whole semitones), every note's CentOffset +50.
        var seq = EvalSequence(
            "Sequence src = | C4q E4q G4q |\nSequence moved = (transpose src +50c)", "moved", out _);
        var notes = seq.Bars[0].MusicalNotes.Where(n => !n.IsRest).ToArray();
        Assert.Equal(3, notes.Length);
        foreach (var n in notes)
        {
            Assert.NotNull(n.CentOffset);
            Assert.Equal(50.0, n.CentOffset!.Value, 6);
        }
        // Pitch class unchanged (no whole-semitone shift).
        Assert.Equal('C', notes[0].NoteName);
        Assert.Equal('E', notes[1].NoteName);
        Assert.Equal('G', notes[2].NoteName);
    }

    [Fact]
    public void Transpose_150c_ShiftsOneSemitoneAndFolds50Cents()
    {
        // +150c = +1 semitone + 50c. C4 -> C#4 with CentOffset +50.
        var seq = EvalSequence(
            "Sequence src = | C4q |\nSequence moved = (transpose src +150c)", "moved", out _);
        var n = seq.Bars[0].MusicalNotes[0];
        Assert.Equal('C', n.NoteName);
        Assert.Equal(1, n.Alteration);   // C# (sharp)
        Assert.NotNull(n.CentOffset);
        Assert.Equal(50.0, n.CentOffset!.Value, 6);
    }

    [Fact]
    public void Transpose_50c_AddsToExistingCentOffset()
    {
        // A note that already carries +25c should end at +75c after a +50c transpose.
        // Build the source note in C# so the existing CentOffset is exactly +25.
        var input = new SequenceData();
        var note = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            centOffset: 25.0);
        input.AddBar(new BarData(new[] { note }, new TimeSignatureData(4, 4)));

        var moved = FlowLang.StandardLibrary.Transforms.TransformFunctions
            .ApplyTransposeCentForTesting(input, 50.0);
        var n = moved.Bars[0].MusicalNotes[0];
        Assert.NotNull(n.CentOffset);
        Assert.Equal(75.0, n.CentOffset!.Value, 6);
    }
}
