using System.Collections.Generic;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase0615;

/// <summary>
/// Feature-addition 0615 (#5 chord-duration-fusion) — a chord name immediately
/// followed by a single duration suffix (q/h/w/e/s + optional dot/tie) lexes +
/// parses as that chord at that duration in a note stream, mirroring the
/// existing note+duration fusion (<c>C4q</c>).
///
/// <para>
/// Regression for: <c>| Cmaj7q |</c> charitably became a REST — the lexer
/// scanned <c>Cmaj7q</c> as one identifier, every whole-token chord/note check
/// failed (trailing <c>q</c> broke <see cref="FlowLang.StandardLibrary.Harmony.ChordParser.IsChordSymbol"/>),
/// and the parser's "unrecognized note → rest" path swallowed it.
/// </para>
///
/// <para>
/// The fix adds <see cref="FlowLang.StandardLibrary.Harmony.ChordParser.TryMatchChordWithDuration"/>:
/// the lexer greedily matches the LONGEST valid chord name, THEN one trailing
/// duration letter, emitting a ChordLiteral + a separate duration Identifier
/// (the existing NamedChordElement parse path consumes durSuffix → dot → tie).
/// Conservative: <c>Cq</c> stays a note-path token, <c>C4q</c> stays note+dur,
/// <c>G7q</c> stays note G octave 7, and a genuine typo still renders as a rest.
/// </para>
/// </summary>
public class ChordDurationFusionTests
{
    private static SequenceData CompileSeq(string streamBody)
    {
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute($"Sequence s = {streamBody}\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Empty(engine.ErrorReporter.Errors);
        var v = engine.Context.GetVariable("s");
        Assert.NotNull(v);
        return v!.As<SequenceData>();
    }

    private static List<MusicalNoteData> FirstBarNotes(SequenceData seq)
    {
        Assert.NotEmpty(seq.Bars);
        return seq.Bars[0].MusicalNotes;
    }

    // Pitch class 0-11 from a MusicalNoteData's letter + alteration. ChordParser
    // spells enharmonically (e.g. Bb7's root may surface as A#), so chord IDENTITY
    // is asserted by pitch class, not by spelling.
    private static int PitchClass(MusicalNoteData n)
    {
        int baseClass = n.NoteName switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11,
            _ => 0
        };
        return ((baseClass + n.Alteration) % 12 + 12) % 12;
    }

    [Fact]
    public void Cmaj7q_FusesIntoMajorSeventhChordAtQuarter()
    {
        var seq = CompileSeq("| Cmaj7q |");
        var notes = FirstBarNotes(seq);

        // Cmaj7 = C E G B (4 tones), all non-rest, all quarter (DurationValue=2).
        Assert.Equal(4, notes.Count);
        Assert.All(notes, n => Assert.False(n.IsRest));
        Assert.All(notes, n => Assert.Equal((int)NoteValueType.Value.QUARTER, n.DurationValue));
        // Leading tone advances the cursor; the other three stack as chord tones.
        Assert.False(notes[0].IsChordTone);
        Assert.True(notes.Skip(1).All(n => n.IsChordTone));
        // Pitch classes C(0) E(4) G(7) B(11) — verify via NoteName + alteration.
        Assert.Equal('C', notes[0].NoteName);
        Assert.Equal('E', notes[1].NoteName);
        Assert.Equal('G', notes[2].NoteName);
        Assert.Equal('B', notes[3].NoteName);
    }

    [Fact]
    public void Dm7e_FusesIntoMinorSeventhChordAtEighth()
    {
        var seq = CompileSeq("| Dm7e |");
        var notes = FirstBarNotes(seq);

        // Dm7 = D F A C (4 tones), all eighth (DurationValue=3).
        Assert.Equal(4, notes.Count);
        Assert.All(notes, n => Assert.False(n.IsRest));
        Assert.All(notes, n => Assert.Equal((int)NoteValueType.Value.EIGHTH, n.DurationValue));
        Assert.Equal('D', notes[0].NoteName);
    }

    [Fact]
    public void FSharpDim7DottedHalf_FusesWithSharpRootAndDot()
    {
        var seq = CompileSeq("| F#dim7h. |");
        var notes = FirstBarNotes(seq);

        // F#dim7 = pitch classes 6,9,0,3 (F# A C D#), all dotted half.
        Assert.Equal(4, notes.Count);
        Assert.All(notes, n => Assert.False(n.IsRest));
        Assert.All(notes, n => Assert.Equal((int)NoteValueType.Value.HALF, n.DurationValue));
        Assert.All(notes, n => Assert.True(n.IsDotted));
        // Root pitch class is F# (6); full set is the fully-diminished stack.
        Assert.Equal(6, PitchClass(notes[0]));
        Assert.Equal(new[] { 6, 9, 0, 3 }, notes.Select(PitchClass).ToArray());
    }

    [Fact]
    public void Bb7w_StaysTiedFlatNoteOctave7_BareDigitQualityNeverFuses()
    {
        // HONEST SCOPE: "Bb7" is structurally identical to "F#5" — an accidented
        // letter + a single digit. The digit is a NOTE OCTAVE (Bb octave 7), and
        // the documented tests/test_chords.flow + IsChordSymbol convention spells
        // every <root><accidental><digit> token as a NOTE, not a power/6th/7th
        // chord. Making "Bb7w" a chord would have to make "F#5e" a chord too,
        // breaking the Phase 45 6/8-jig tutorial baseline (its melody uses F#5e /
        // F#5q.). So "Bb7w~" fuses as the TIED B-flat octave-7 WHOLE NOTE — a
        // valid, sensible reading. Letter-bearing chord qualities (Cmaj7q / Dm7e /
        // F#dim7h) fuse as chords; bare-digit "chords" stay notes.
        var seq = CompileSeq("| Bb7w~ |");
        var notes = FirstBarNotes(seq);

        Assert.Single(notes);
        Assert.False(notes[0].IsChordTone);
        Assert.False(notes[0].IsRest);
        Assert.True(notes[0].IsTied);
        Assert.Equal((int)NoteValueType.Value.WHOLE, notes[0].DurationValue);
        Assert.Equal('B', notes[0].NoteName);
        Assert.Equal(-1, notes[0].Alteration); // flat
        Assert.Equal(7, notes[0].Octave);
    }

    [Fact]
    public void C4q_StaysASingleNoteAtQuarter_NoFusionRegression()
    {
        var seq = CompileSeq("| C4q D4q |");
        var notes = FirstBarNotes(seq);

        // Two SINGLE notes, not chords.
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.False(n.IsChordTone));
        Assert.All(notes, n => Assert.Equal((int)NoteValueType.Value.QUARTER, n.DurationValue));
        Assert.Equal('C', notes[0].NoteName);
        Assert.Equal(4, notes[0].Octave);
        Assert.Equal('D', notes[1].NoteName);
        Assert.Equal(4, notes[1].Octave);
    }

    [Fact]
    public void G7q_StaysNoteGOctave7_DocumentedConvention()
    {
        // tests/test_chords.flow convention: "G7 is parsed as note G at octave 7,
        // use dom7 for chord". A bare-digit quality WITHOUT a root accidental must
        // NOT fuse into a chord.
        var seq = CompileSeq("| G7q |");
        var notes = FirstBarNotes(seq);

        Assert.Single(notes);
        Assert.False(notes[0].IsChordTone);
        Assert.Equal('G', notes[0].NoteName);
        Assert.Equal(7, notes[0].Octave);
        Assert.Equal((int)NoteValueType.Value.QUARTER, notes[0].DurationValue);
    }

    [Fact]
    public void Bbq_StaysFlatNoteAtQuarter_BareAccidentalIsNotAChord()
    {
        // "Bb" (bare flat, empty quality) must remain a NOTE, not a B-flat MAJOR
        // chord. Only a non-empty quality fuses.
        var seq = CompileSeq("| Bbq |");
        var notes = FirstBarNotes(seq);

        Assert.Single(notes);
        Assert.False(notes[0].IsChordTone);
        Assert.Equal('B', notes[0].NoteName);
        Assert.Equal(-1, notes[0].Alteration);
        Assert.Equal((int)NoteValueType.Value.QUARTER, notes[0].DurationValue);
    }

    [Fact]
    public void Cmaj7_NoDurationSuffix_StillChord_NoRegression()
    {
        // The pre-existing zero-duration named-chord form must be untouched.
        var seq = CompileSeq("| Cmaj7 |");
        var notes = FirstBarNotes(seq);

        Assert.Equal(4, notes.Count);
        Assert.All(notes, n => Assert.False(n.IsRest));
        Assert.Equal('C', notes[0].NoteName);
    }

    [Fact]
    public void RealNoteTypo_StillRendersAsRestWithAdvisory_CharitablePathPreserved()
    {
        // A genuine note-name typo (Z9 — not A-G, not a chord) must STILL fall to
        // the charitable "unrecognized note → rest" path, not be mistaken for a
        // chord-duration fusion.
        RenderingDiagnostics.ResetForTesting();
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute("Sequence s = | Z9q C4q |\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());

        var seq = engine.Context.GetVariable("s")!.As<SequenceData>();
        var notes = seq.Bars[0].MusicalNotes;
        // Z9q → rest; C4q → real note. The surrounding note is NOT dropped. (The
        // whole token "Z9q" is the unrecognized identifier — its trailing q is part
        // of the identifier, not a separate duration suffix, so the rest renders at
        // the compiler's default duration. The point is: typo → rest, not a chord.)
        Assert.Equal(2, notes.Count);
        Assert.True(notes[0].IsRest);
        Assert.False(notes[1].IsRest);
        Assert.Equal('C', notes[1].NoteName);
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("note-stream-bad-note:Z9q:1:16"));
    }

    [Fact]
    public void MultiLineStream_WithFusedChordBars_ParsesAllBars()
    {
        // NO regression to multi-line streams: a fused-chord bar followed by a
        // note bar on the next line must produce TWO bars, not collapse/abort.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute(
            "Sequence s = | Cmaj7q Dm7q |\n" +
            "             | E4q F4q |\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Empty(engine.ErrorReporter.Errors);

        var seq = engine.Context.GetVariable("s")!.As<SequenceData>();
        Assert.Equal(2, seq.Bars.Count);
        // Bar 0: two chords (4 + 4 tones). Bar 1: two single notes.
        Assert.Equal(8, seq.Bars[0].MusicalNotes.Count);
        Assert.Equal(2, seq.Bars[1].MusicalNotes.Count);
    }
}
