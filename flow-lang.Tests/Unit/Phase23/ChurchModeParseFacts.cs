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
/// Per WARNING-6: the original <c>TryParseKey(out bool isMajor)</c> entry is
/// strictly preserved — <see cref="ScaleDatabase.ResolveRomanNumeral"/> and
/// <see cref="ScaleDatabase.GetScaleNotes"/> still call it. Verified end-to-end
/// here by <see cref="ExistingTryParseKey_StillWorks_ForChordResolution"/>.
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
    public void ExistingTryParseKey_StillWorks_ForChordResolution()
    {
        // WARNING-6: the original TryParseKey(out bool isMajor) callers at
        // ResolveRomanNumeral (line ~118) + GetScaleNotes (line ~234) must still
        // route through the legacy entry. We can't call private TryParseKey
        // directly, but the public surfaces that depend on it must still resolve
        // major/minor inputs.
        Assert.NotNull(ScaleDatabase.GetScaleNotes("Cmajor"));
        Assert.NotNull(ScaleDatabase.GetScaleNotes("Aminor"));
        // ResolveRomanNumeral uses TryParseKey too — V in C major should resolve.
        Assert.NotNull(ScaleDatabase.ResolveRomanNumeral("V", "Cmajor"));
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
