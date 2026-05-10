using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase14;

/// <summary>
/// Phase 14 DX-06 flat-literal surface regression Facts.
///
/// CONTEXT D-07: Parse accepts arbitrary composition of b/#/+/- on either side of octave
/// digits. Net alteration = sharps - flats, any int.
/// CONTEXT D-08: Format emits canonical run-based +N/-N strings. Parse(Format(x)) == x
/// for all int alterations (within post-alt MIDI range).
/// CONTEXT D-09: Range validation uses post-alteration MIDI value, not letter+octave.
/// </summary>
public class NoteTypeTests
{
    [Fact]
    public void Parse_FlatLetter_Db()
    {
        var (n, o, a) = NoteType.Parse("Db4");
        Assert.Equal('D', n);
        Assert.Equal(4, o);
        Assert.Equal(-1, a);
    }

    [Fact]
    public void Parse_FlatLetter_Eb()
    {
        var (n, o, a) = NoteType.Parse("Eb4");
        Assert.Equal('E', n);
        Assert.Equal(4, o);
        Assert.Equal(-1, a);
    }

    [Fact]
    public void Parse_FlatLetter_Gb()
    {
        var (n, o, a) = NoteType.Parse("Gb4");
        Assert.Equal('G', n);
        Assert.Equal(4, o);
        Assert.Equal(-1, a);
    }

    [Fact]
    public void Parse_FlatLetter_Ab()
    {
        var (n, o, a) = NoteType.Parse("Ab4");
        Assert.Equal('A', n);
        Assert.Equal(4, o);
        Assert.Equal(-1, a);
    }

    [Fact]
    public void Parse_FlatLetter_Bb()
    {
        var (n, o, a) = NoteType.Parse("Bb4");
        Assert.Equal('B', n);
        Assert.Equal(4, o);
        Assert.Equal(-1, a);
    }

    [Fact]
    public void Parse_FlatLetter_Cb_InRange()
    {
        // Cb4 = MIDI 59 = B3; post-alt range check (D-09) allows it.
        var (n, o, a) = NoteType.Parse("Cb4");
        Assert.Equal('C', n);
        Assert.Equal(4, o);
        Assert.Equal(-1, a);
    }

    [Fact]
    public void Parse_FlatLetter_Fb()
    {
        var (n, o, a) = NoteType.Parse("Fb4");
        Assert.Equal('F', n);
        Assert.Equal(4, o);
        Assert.Equal(-1, a);
    }

    [Fact]
    public void Parse_BareFlat_DefaultOctave()
    {
        // No octave digits → default octave 4 (existing convention preserved).
        var (n, o, a) = NoteType.Parse("Bb");
        Assert.Equal('B', n);
        Assert.Equal(4, o);
        Assert.Equal(-1, a);
    }

    [Fact]
    public void Parse_SharpHash_Equivalent()
    {
        // '#' treated identically to '+' for alteration counting.
        var (n, o, a) = NoteType.Parse("C#5");
        Assert.Equal('C', n);
        Assert.Equal(5, o);
        Assert.Equal(1, a);
    }

    [Fact]
    public void Parse_DoubleSharp_Hash()
    {
        var (n, o, a) = NoteType.Parse("F##4");
        Assert.Equal('F', n);
        Assert.Equal(4, o);
        Assert.Equal(2, a);
    }

    [Fact]
    public void Parse_MixedAlteration_BbMinusPlusBBB()
    {
        // Char breakdown of Bb-+bbb after the 'B' letter:
        //   'b' → flat (1)
        //   '-' → flat (2)
        //   '+' → sharp (1)
        //   'b' → flat (3)
        //   'b' → flat (4)
        //   'b' → flat (5)
        // alteration = sharps - flats = 1 - 5 = -4
        var (n, o, a) = NoteType.Parse("Bb-+bbb");
        Assert.Equal('B', n);
        Assert.Equal(4, o);
        Assert.Equal(-4, a);
    }

    [Fact]
    public void Parse_PreOctaveAndPostOctave()
    {
        // C+5++: pre-octave + (sharp 1), octave 5, post-octave ++ (sharps 2,3) → alt +3
        var (n, o, a) = NoteType.Parse("C+5++");
        Assert.Equal('C', n);
        Assert.Equal(5, o);
        Assert.Equal(3, a);
    }

    [Fact]
    public void Parse_Cb0_BelowRange_Throws()
    {
        // Cb0 = MIDI 11 = below E0 (MIDI 16). Post-alt check rejects.
        var ex = Assert.Throws<ArgumentException>(() => NoteType.Parse("Cb0"));
        Assert.Equal("Note Cb0 is out of valid range (E0 to E10)", ex.Message);
    }

    [Fact]
    public void Parse_Eb0_BelowRange_Throws()
    {
        // Eb0 = MIDI 15 = below E0 (MIDI 16). Replacement for the plan's Fb0 case: Fb0 is
        // actually MIDI 16 = exactly E0 (in range). Eb0 is the true below-range edge for
        // a flat at octave 0. Rule 1 deviation — plan test case was a math bug (Fb at
        // octave 0 equals E at octave 0, which IS in range).
        var ex = Assert.Throws<ArgumentException>(() => NoteType.Parse("Eb0"));
        Assert.Equal("Note Eb0 is out of valid range (E0 to E10)", ex.Message);
    }

    [Fact]
    public void Parse_Fb0_AtBoundary_Valid()
    {
        // Fb0 post-alt MIDI = 16 = exactly E0 = minimum in-range MIDI. Must NOT throw.
        // Pins the boundary semantics under D-09 post-alteration range check.
        var (n, o, a) = NoteType.Parse("Fb0");
        Assert.Equal('F', n);
        Assert.Equal(0, o);
        Assert.Equal(-1, a);
    }

    [Fact]
    public void Parse_InvalidChar_Throws()
    {
        // 'm' is not a valid alteration character → ArgumentException.
        var ex = Assert.Throws<ArgumentException>(() => NoteType.Parse("Cm4"));
        Assert.StartsWith("Invalid note character 'm' in Cm4", ex.Message);
    }

    [Fact] public void Format_NaturalEmpty()    => Assert.Equal("C4",     NoteType.Format('C', 4, 0));
    [Fact] public void Format_SinglePlus()      => Assert.Equal("F4+",    NoteType.Format('F', 4, 1));
    [Fact] public void Format_DoublePlus()      => Assert.Equal("F4++",   NoteType.Format('F', 4, 2));
    [Fact] public void Format_SingleMinus()     => Assert.Equal("B4-",    NoteType.Format('B', 4, -1));
    [Fact] public void Format_QuadrupleMinus()  => Assert.Equal("B4----", NoteType.Format('B', 4, -4));
    [Fact] public void Format_PreservesNoteAndOctave() => Assert.Equal("A5", NoteType.Format('A', 5, 0));

    [Fact]
    public void RoundTrip_AllAlterations()
    {
        // Parse(Format(x)) == x for every alteration in [-5, +5] at octave 4 for each letter A-G,
        // skipping combinations that would land outside the E0..E10 MIDI range.
        for (char letter = 'A'; letter <= 'G'; letter++)
        {
            for (int alt = -5; alt <= 5; alt++)
            {
                int midi = NoteType.GetNoteValue(letter, 4) + alt;
                int minMidi = NoteType.GetNoteValue('E', 0);
                int maxMidi = NoteType.GetNoteValue('E', 10);
                if (midi < minMidi || midi > maxMidi)
                    continue;

                string formatted = NoteType.Format(letter, 4, alt);
                var (n, o, a) = NoteType.Parse(formatted);
                Assert.Equal(letter, n);
                Assert.Equal(4, o);
                Assert.Equal(alt, a);
            }
        }
    }
}
