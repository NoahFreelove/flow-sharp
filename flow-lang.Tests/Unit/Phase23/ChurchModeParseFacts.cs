using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.StandardLibrary.Harmony;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// Phase 23 D-04 (Plan 23-03 Task 1): <see cref="ScaleDatabase.TryParseKeyWithMode"/>
/// (shipped Wave 2, widened Wave 3) recognizes 7 mode suffixes: major, minor, and
/// the 5 church modes (dorian, phrygian, lydian, mixolydian, locrian). Longer-suffix-first
/// ordering avoids prefix collisions (`lydian` is a substring of `mixolydian`).
///
/// sweep-0614: <see cref="ScaleDatabase.ResolveRomanNumeral"/> and
/// <see cref="ScaleDatabase.GetScaleNotes"/> now route through this mode-aware
/// parser (the legacy bool-isMajor <c>TryParseKey</c> was removed — it ignored the
/// 5 church-mode suffixes, so a valid <c>key Ddorian { }</c> context resolved every
/// roman numeral to a rest). Verified end-to-end by
/// <see cref="ExistingKeyParse_StillWorks_ForChordResolution"/> +
/// <see cref="RomanNumerals_ResolveInChurchModeKeys"/>.
/// </summary>
public class ChurchModeParseFacts
{
    [Theory]
    [InlineData("Cmajor",       "C",       Mode.Major)]
    [InlineData("Aminor",       "A",       Mode.Minor)]
    [InlineData("Cdorian",      "C",       Mode.Dorian)]
    [InlineData("Aphrygian",    "A",       Mode.Phrygian)]
    [InlineData("Glydian",      "G",       Mode.Lydian)]
    [InlineData("Bmixolydian",  "B",       Mode.Mixolydian)]
    [InlineData("Dlocrian",     "D",       Mode.Locrian)]
    [InlineData("Csharpdorian", "Csharp",  Mode.Dorian)]
    [InlineData("Bbmixolydian", "Bb",      Mode.Mixolydian)]
    public void TryParseKeyWithMode_RecognizesAllSuffixes(string input, string expectedRoot, Mode expectedMode)
    {
        bool ok = ScaleDatabase.TryParseKeyWithMode(input, out string? root, out Mode mode);
        Assert.True(ok, $"expected TryParseKeyWithMode to accept {input}");
        Assert.Equal(expectedRoot, root);
        Assert.Equal(expectedMode, mode);
    }

    [Fact]
    public void TryParseKeyWithMode_UnknownSuffix_ReturnsFalse()
    {
        bool ok = ScaleDatabase.TryParseKeyWithMode("Cwhatever", out string? root, out Mode mode);
        Assert.False(ok);
        Assert.Null(root);
    }

    [Fact]
    public void TryParseKeyWithMode_LongerSuffixWins_MixolydianNotLydian()
    {
        // Critical ordering test: "mixolydian" must NOT be misparsed as "lydian"
        // with root "Bmixo". The longer suffix is checked first.
        bool ok = ScaleDatabase.TryParseKeyWithMode("Bmixolydian", out string? root, out Mode mode);
        Assert.True(ok);
        Assert.Equal("B", root);
        Assert.Equal(Mode.Mixolydian, mode);
    }

    [Fact]
    public void ExistingKeyParse_StillWorks_ForChordResolution()
    {
        // sweep-0614: ResolveRomanNumeral + GetScaleNotes now route through
        // TryParseKeyWithMode. Major/minor inputs must still resolve unchanged.
        Assert.NotNull(ScaleDatabase.GetScaleNotes("Cmajor"));
        Assert.NotNull(ScaleDatabase.GetScaleNotes("Aminor"));
        Assert.NotNull(ScaleDatabase.ResolveRomanNumeral("V", "Cmajor"));
    }

    [Theory]
    // sweep-0614 (harmony case bug): numeral CASE carries triad-quality intent.
    // Uppercase = major triad, lowercase = minor triad. Borrowed `iv` in C major
    // was silently rendered F-A-C (major); it must be F-Ab-C (minor).
    [InlineData("iv", "Cmajor", "F", "m")]   // borrowed minor iv — the headline bug
    [InlineData("IV", "Cmajor", "F", "maj")] // diatonic-correct major IV unchanged
    [InlineData("v",  "Cmajor", "G", "m")]   // borrowed minor v
    [InlineData("V",  "Cmajor", "G", "maj")] // diatonic-correct dominant
    [InlineData("i",  "Cmajor", "C", "m")]   // borrowed minor tonic
    [InlineData("I",  "Cmajor", "C", "maj")] // diatonic tonic
    public void RomanNumeralCase_DeterminesTriadQuality(string numeral, string key, string expectedRoot, string expectedQuality)
    {
        var chord = ScaleDatabase.ResolveRomanNumeral(numeral, key);
        Assert.NotNull(chord);
        Assert.Equal(expectedRoot, chord!.Root);
        Assert.Equal(expectedQuality, chord.Quality);
    }

    [Fact]
    public void RomanNumeral_LowercaseDiminished_StaysDiminished()
    {
        // Case alone cannot express a diminished triad — `vii` in major must remain
        // the diminished leading-tone triad, not be flattened to a plain minor.
        var chord = ScaleDatabase.ResolveRomanNumeral("vii", "Cmajor");
        Assert.NotNull(chord);
        Assert.Equal("B", chord!.Root);
        Assert.Equal("dim", chord.Quality);
    }

    [Fact]
    public void RomanNumeral_ExplicitExtension_OverridesCase()
    {
        // An explicit quality extension (V7) always wins over the case heuristic.
        var chord = ScaleDatabase.ResolveRomanNumeral("V7", "Cmajor");
        Assert.NotNull(chord);
        Assert.Equal("G", chord!.Root);
        Assert.Equal("7", chord.Quality);
    }

    [Theory]
    // sweep-0614 (church-mode bug): roman numerals + scale notes used to resolve to
    // null/empty in modal keys because the resolver called the major/minor-only
    // legacy parser. They must now resolve against the modal interval/quality table.
    [InlineData("i",  "Ddorian", "D", "m")]   // dorian tonic minor
    [InlineData("iv", "Ddorian", "G", "m")]   // dorian iv minor
    [InlineData("I",  "Glydian", "G", "maj")] // lydian tonic major
    [InlineData("i",  "Aphrygian", "A", "m")] // phrygian tonic minor
    public void RomanNumerals_ResolveInChurchModeKeys(string numeral, string key, string expectedRoot, string expectedQuality)
    {
        var chord = ScaleDatabase.ResolveRomanNumeral(numeral, key);
        Assert.NotNull(chord);
        Assert.Equal(expectedRoot, chord!.Root);
        Assert.Equal(expectedQuality, chord.Quality);
    }

    [Fact]
    public void ScaleNotes_ResolveInChurchModeKey()
    {
        // (scaleNotes "Ddorian") used to return [] (silent note loss). It must now
        // return the 7 modal pitches: D E F G A B C.
        var notes = ScaleDatabase.GetScaleNotes("Ddorian");
        Assert.NotNull(notes);
        Assert.Equal(new[] { "D", "E", "F", "G", "A", "B", "C" }, notes);
    }

    [Fact]
    public void MusicalContext_ValidKeys_IncludesChurchModes()
    {
        // D-04: ValidKeys extended from 34 (17 × 2) to 119 (17 × 7) entries.
        // Without this extension, `key Cdorian { ... }` fails IsValidKey before
        // tuning math sees it.
        Assert.True(FlowLang.Runtime.MusicalContext.IsValidKey("Cdorian"));
        Assert.True(FlowLang.Runtime.MusicalContext.IsValidKey("Aphrygian"));
        Assert.True(FlowLang.Runtime.MusicalContext.IsValidKey("Glydian"));
        Assert.True(FlowLang.Runtime.MusicalContext.IsValidKey("Bmixolydian"));
        Assert.True(FlowLang.Runtime.MusicalContext.IsValidKey("Dlocrian"));
        // Old keys still valid:
        Assert.True(FlowLang.Runtime.MusicalContext.IsValidKey("Cmajor"));
        Assert.True(FlowLang.Runtime.MusicalContext.IsValidKey("Aminor"));
    }

    [Fact]
    public void MusicalContext_ValidKeys_HasExpectedCount()
    {
        // 17 roots × 7 modes = 119 entries.
        Assert.Equal(119, FlowLang.Runtime.MusicalContext.ValidKeys.Count);
    }
}
