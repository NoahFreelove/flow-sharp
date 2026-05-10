using FlowLang.StandardLibrary.Harmony;
using Xunit;

namespace FlowLang.Tests.Unit.QuickFixes;

/// <summary>
/// QUICK-260504-cks regression facts for the runtime <c>(chord String)</c>
/// constructor and the comprehensively-expanded <see cref="ChordParser"/>
/// quality vocabulary.
///
/// Each fact pins one canonical input to its expected interval set so the
/// dictionary entries (added in this quick task) cannot silently regress.
/// Output uses Flow's display form: naturals as <c>"X{octave}"</c>, sharps
/// as <c>"X{octave}+"</c> (consult <c>ChordParser.ExpandIntervals</c>).
/// </summary>
public class ChordStringConstructorFacts
{
    private static string[] Notes(string symbol)
    {
        Assert.True(ChordParser.TryParseFlexible(symbol, out var chord),
            $"TryParseFlexible failed on \"{symbol}\"");
        Assert.NotNull(chord);
        return chord!.NoteNames;
    }

    // --- Triads ---
    [Fact] public void BareLetterIsMajorTriad() =>
        Assert.Equal(new[] { "C4", "E4", "G4" }, Notes("C"));

    [Fact] public void MajAliasMatchesM() =>
        Assert.Equal(Notes("Cmaj"), Notes("CM"));

    [Fact] public void MinAliasMatchesm() =>
        Assert.Equal(Notes("Cm"), Notes("Cmin"));

    [Fact] public void DimTriad() =>
        Assert.Equal(new[] { "C4", "D4+", "F4+" }, Notes("Cdim"));

    [Fact] public void AugTriad() =>
        Assert.Equal(new[] { "C4", "E4", "G4+" }, Notes("Caug"));

    // --- Power chord ---
    [Fact] public void PowerChord_OmitsThird() =>
        Assert.Equal(new[] { "C4", "G4" }, Notes("C5"));

    // --- Sevenths ---
    [Fact] public void Dom7() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "A4+" }, Notes("C7"));

    [Fact] public void Maj7() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "B4" }, Notes("Cmaj7"));

    [Fact] public void Min7() =>
        Assert.Equal(new[] { "C4", "D4+", "G4", "A4+" }, Notes("Cm7"));

    [Fact] public void Dim7() =>
        Assert.Equal(new[] { "C4", "D4+", "F4+", "A4" }, Notes("Cdim7"));

    [Fact] public void HalfDim_BothAccidentalForms() =>
        Assert.Equal(Notes("Cm7f5"), Notes("Cm7b5"));

    // --- 9 / 11 / 13 family ---
    [Fact] public void Dom9_StackedThirds() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "A4+", "D5" }, Notes("C9"));

    [Fact] public void Dom11_StackedThirds() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "A4+", "D5", "F5" }, Notes("C11"));

    [Fact] public void Dom13_StackedThirds() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "A4+", "D5", "F5", "A5" }, Notes("C13"));

    [Fact] public void Maj13_StackedThirds() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "B4", "D5", "F5", "A5" }, Notes("Cmaj13"));

    [Fact] public void Min13_StackedThirds() =>
        Assert.Equal(new[] { "C4", "D4+", "G4", "A4+", "D5", "F5", "A5" }, Notes("Cm13"));

    // --- Sixths ---
    [Fact] public void Six() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "A4" }, Notes("C6"));

    [Fact] public void SixNine() =>
        Assert.Equal(Notes("C69"), Notes("C6/9"));

    // --- Sus + 7 ---
    [Fact] public void SevenSus4() =>
        Assert.Equal(new[] { "C4", "F4", "G4", "A4+" }, Notes("C7sus4"));

    [Fact] public void NineSus4() =>
        Assert.Equal(new[] { "C4", "F4", "G4", "A4+", "D5" }, Notes("C9sus4"));

    // --- Adds (no 7th) ---
    [Fact] public void Add9_OmitsSeventh() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "D5" }, Notes("Cadd9"));

    [Fact] public void Add11() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "F5" }, Notes("Cadd11"));

    // --- Alterations: b/# === f/s aliases ---
    [Fact] public void Seven_b9_eq_Seven_f9() =>
        Assert.Equal(Notes("C7f9"), Notes("C7b9"));

    [Fact] public void Seven_sharp9_eq_Seven_s9() =>
        Assert.Equal(Notes("C7s9"), Notes("C7#9"));

    [Fact] public void Seven_sharp11_eq_Seven_s11() =>
        Assert.Equal(Notes("C7s11"), Notes("C7#11"));

    [Fact] public void Maj7_sharp11() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "B4", "F5+" }, Notes("Cmaj7#11"));

    [Fact] public void Thirteen_b9() =>
        Assert.Equal(new[] { "C4", "E4", "G4", "A4+", "C5+", "F5", "A5" }, Notes("C13b9"));

    // --- Root accidentals: b/# === f/s aliases ---
    [Fact] public void RootSharp_BothForms() =>
        Assert.Equal(Notes("Csmaj"), Notes("C#maj"));

    [Fact] public void RootFlat_BothForms() =>
        Assert.Equal(Notes("Bfm7"), Notes("Bbm7"));

    [Fact] public void Bare_C_sharp_LengthOne() =>
        Assert.Equal(Notes("Csmaj"), Notes("C#"));

    [Fact] public void Bare_D_flat_LengthOne() =>
        Assert.Equal(Notes("Dfmaj"), Notes("Db"));

    // --- Slash bass ---
    [Fact] public void SlashBass_PrependsBassOctaveBelow()
    {
        var notes = Notes("C/G");
        Assert.Equal("G3", notes[0]);
        // Triad after the bass:
        Assert.Equal(new[] { "C4", "E4", "G4" }, notes[1..]);
    }

    [Fact] public void SlashBass_OnDom7()
    {
        var notes = Notes("G7/B");
        Assert.Equal("B3", notes[0]);
        Assert.Equal(new[] { "G4", "B4", "D5", "F5" }, notes[1..]);
    }

    [Fact] public void SlashBass_OnMinor()
    {
        var notes = Notes("Am/E");
        Assert.Equal("E3", notes[0]);
        Assert.Equal(new[] { "A4", "C5", "E5" }, notes[1..]);
    }

    // --- Charitable on hopeless input ---
    [Fact]
    public void GarbageReturnsFalse()
    {
        Assert.False(ChordParser.TryParseFlexible("this-is-not-a-chord", out _));
        Assert.False(ChordParser.TryParseFlexible("", out _));
        Assert.False(ChordParser.TryParseFlexible("Z9", out _));
    }

    // --- Backwards compatibility: existing literal-form symbols still parse via TryParse ---
    [Fact]
    public void LegacyLiterals_StillRecognized()
    {
        Assert.True(ChordParser.TryParse("Cmaj", out _));
        Assert.True(ChordParser.TryParse("Dm", out _));
        Assert.True(ChordParser.TryParse("Gdom7", out _));
        Assert.True(ChordParser.TryParse("Cmaj7", out _));
        Assert.True(ChordParser.TryParse("Am7", out _));
        Assert.True(ChordParser.TryParse("Bdim", out _));
        Assert.True(ChordParser.TryParse("Caug", out _));
        Assert.True(ChordParser.TryParse("Csmaj", out _));
        Assert.True(ChordParser.TryParse("Bfm", out _));
    }

    // --- Lexer convention preserved: bare-digit qualities stay as note literals ---
    [Fact]
    public void Lexer_BareDigitsAreNotChords()
    {
        // IsChordSymbol is the lexer's gate before the chord-before-note dispatch.
        // These shapes MUST remain notes (project convention from tests/test_chords.flow:13)
        // even though the dictionary now contains "5", "6", "7", "9", "11", "13" entries.
        Assert.False(ChordParser.IsChordSymbol("C5"));
        Assert.False(ChordParser.IsChordSymbol("G7"));
        Assert.False(ChordParser.IsChordSymbol("D6"));
        Assert.False(ChordParser.IsChordSymbol("D9"));
        Assert.False(ChordParser.IsChordSymbol("C11"));
        // With-accidental branch must respect the same gate (added in this quick task)
        Assert.False(ChordParser.IsChordSymbol("Cs5"));
        Assert.False(ChordParser.IsChordSymbol("Df7"));
    }

    // --- Lexer convention preserved: alphabetic suffixes are still chords ---
    [Fact]
    public void Lexer_AlphabeticQualitiesAreChords()
    {
        // These must continue to be recognized as chord literals at lex time
        // (NoteStreamCompiler depends on this for chord-injection in note streams).
        Assert.True(ChordParser.IsChordSymbol("Cmaj"));
        Assert.True(ChordParser.IsChordSymbol("Dm"));
        Assert.True(ChordParser.IsChordSymbol("Cmaj7"));
        Assert.True(ChordParser.IsChordSymbol("Cm7"));
        Assert.True(ChordParser.IsChordSymbol("Cm7b5"));   // alteration absorbed by lexer
        Assert.True(ChordParser.IsChordSymbol("Cm9"));
        Assert.True(ChordParser.IsChordSymbol("Cmaj13"));
        Assert.True(ChordParser.IsChordSymbol("Csus4"));
        Assert.True(ChordParser.IsChordSymbol("Csmaj"));
        Assert.True(ChordParser.IsChordSymbol("Bfm"));
    }
}
