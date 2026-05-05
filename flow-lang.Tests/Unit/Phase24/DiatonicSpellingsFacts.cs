using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLsp.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Unit.Phase24;

/// <summary>
/// Phase 24 Plan 24-02 (D-04 + D-05): pins the 17-root × 7-mode = 119-entry
/// hardcoded diatonic-spelling map. Spelling-aware (D-01): in C major, `E#`
/// is NOT in the set even though pitch-class 5 (= F natural) IS — the analyzer
/// uses letter+accidental membership, not pitch-class.
///
/// Closed-set growth pattern: lower-bound count Fact (`>= 119`) is intentionally
/// expressed as `==` here because the closed-set is exactly the ScaleDatabase
/// root × Mode product. If a future phase extends the root set, this Fact
/// becomes the audit trail.
/// </summary>
public class DiatonicSpellingsFacts
{
    [Theory]
    [InlineData("C",      Mode.Major,      new[] { "C", "D", "E",  "F",  "G",  "A",  "B"  })]
    [InlineData("C",      Mode.Minor,      new[] { "C", "D", "Eb", "F",  "G",  "Ab", "Bb" })]
    [InlineData("C",      Mode.Dorian,     new[] { "C", "D", "Eb", "F",  "G",  "A",  "Bb" })]
    [InlineData("C",      Mode.Phrygian,   new[] { "C", "Db","Eb", "F",  "G",  "Ab", "Bb" })]
    [InlineData("C",      Mode.Lydian,     new[] { "C", "D", "E",  "F#", "G",  "A",  "B"  })]
    [InlineData("C",      Mode.Mixolydian, new[] { "C", "D", "E",  "F",  "G",  "A",  "Bb" })]
    [InlineData("C",      Mode.Locrian,    new[] { "C", "Db","Eb", "F",  "Gb", "Ab", "Bb" })]
    [InlineData("F",      Mode.Major,      new[] { "F", "G", "A",  "Bb", "C",  "D",  "E"  })]  // canonical b̂7 = Bb
    [InlineData("E",      Mode.Dorian,     new[] { "E", "F#","G",  "A",  "B",  "C#", "D"  })]  // D-05 example
    [InlineData("Bb",     Mode.Major,      new[] { "Bb","C", "D",  "Eb", "F",  "G",  "A"  })]
    [InlineData("G",      Mode.Major,      new[] { "G", "A", "B",  "C",  "D",  "E",  "F#" })]  // LINT-03 inner-key case
    [InlineData("A",      Mode.Minor,      new[] { "A", "B", "C",  "D",  "E",  "F",  "G"  })]  // natural minor
    // Sharp-side major keys — pinning every sharp-side root once
    [InlineData("D",      Mode.Major,      new[] { "D", "E", "F#", "G",  "A",  "B",  "C#" })]
    [InlineData("A",      Mode.Major,      new[] { "A", "B", "C#", "D",  "E",  "F#", "G#" })]
    [InlineData("E",      Mode.Major,      new[] { "E", "F#","G#", "A",  "B",  "C#", "D#" })]
    [InlineData("B",      Mode.Major,      new[] { "B", "C#","D#", "E",  "F#", "G#", "A#" })]
    [InlineData("Fsharp", Mode.Major,      new[] { "F#","G#","A#", "B",  "C#", "D#", "E#" })]  // E# spelling-aware canary
    [InlineData("Csharp", Mode.Major,      new[] { "C#","D#","E#", "F#", "G#", "A#", "B#" })]  // B#/E# spelling-aware canary
    // Flat-side major keys — pinning every flat-side root once
    [InlineData("Eb",     Mode.Major,      new[] { "Eb","F", "G",  "Ab", "Bb", "C",  "D"  })]
    [InlineData("Ab",     Mode.Major,      new[] { "Ab","Bb","C",  "Db", "Eb", "F",  "G"  })]
    [InlineData("Db",     Mode.Major,      new[] { "Db","Eb","F",  "Gb", "Ab", "Bb", "C"  })]
    [InlineData("Gb",     Mode.Major,      new[] { "Gb","Ab","Bb", "Cb", "Db", "Eb", "F"  })]  // Cb spelling-aware canary
    // Cross-mode coverage on non-C roots
    [InlineData("D",      Mode.Dorian,     new[] { "D", "E", "F",  "G",  "A",  "B",  "C"  })]  // all-natural Dorian
    [InlineData("E",      Mode.Phrygian,   new[] { "E", "F", "G",  "A",  "B",  "C",  "D"  })]  // all-natural Phrygian
    [InlineData("F",      Mode.Lydian,     new[] { "F", "G", "A",  "B",  "C",  "D",  "E"  })]  // all-natural Lydian
    [InlineData("G",      Mode.Mixolydian, new[] { "G", "A", "B",  "C",  "D",  "E",  "F"  })]  // all-natural Mixolydian
    [InlineData("B",      Mode.Locrian,    new[] { "B", "C", "D",  "E",  "F",  "G",  "A"  })]  // all-natural Locrian
    // Enharmonic-equivalent roots (different spellings, same pitch-class set must differ)
    [InlineData("Dsharp", Mode.Minor,      new[] { "D#","E#","F#", "G#", "A#", "B",  "C#" })]  // contrast with Eb minor
    [InlineData("Asharp", Mode.Minor,      new[] { "A#","B#","C#", "D#", "E#", "F#", "G#" })]  // double-sharp-edge case
    [InlineData("Gsharp", Mode.Minor,      new[] { "G#","A#","B",  "C#", "D#", "E",  "F#" })]
    public void GetDiatonicSpellings_KnownPairs_ReturnsExpectedSet(string root, Mode mode, string[] expected)
    {
        var actual = DiatonicSpellings.GetDiatonicSpellings(root, mode);
        Assert.NotNull(actual);
        Assert.Equal(7, actual!.Count);
        foreach (var spelling in expected)
            Assert.Contains(spelling, actual);
    }

    [Fact]
    public void Cmajor_DoesNotContainEsharp()
    {
        // D-01 spelling-aware canary: pitch-class 5 IS diatonic in Cmajor (= F natural),
        // but the spelling "E#" is NOT in the set. The analyzer flags E#4 in Cmajor
        // because of this — even though E# sounds like F.
        var set = DiatonicSpellings.GetDiatonicSpellings("C", Mode.Major);
        Assert.NotNull(set);
        Assert.DoesNotContain("E#", set!);
        Assert.Contains("F", set!);
    }

    [Fact]
    public void Cmajor_DoesNotContainGb()
    {
        // Same-pitch-class-different-spelling canary: Gb in Cmajor flags too.
        var set = DiatonicSpellings.GetDiatonicSpellings("C", Mode.Major);
        Assert.NotNull(set);
        Assert.DoesNotContain("Gb", set!);
        Assert.DoesNotContain("F#", set!);
        Assert.Contains("F", set!);
        Assert.Contains("G", set!);
    }

    [Fact]
    public void Map_HasExactly119Entries()
    {
        // 17 roots × 7 modes = 119 — matches MusicalContext.ValidKeys.Count
        // (pinned by Phase 23 ChurchModeParseFacts.MusicalContext_ValidKeys_HasExpectedCount).
        Assert.Equal(119, DiatonicSpellings.EntryCount);
    }

    [Fact]
    public void GetDiatonicSpellings_UnknownRoot_ReturnsNull()
    {
        // D-22 silent fail-open: any (root, mode) pair not in the closed set returns null.
        Assert.Null(DiatonicSpellings.GetDiatonicSpellings("NotARealRoot", Mode.Major));
    }

    [Fact]
    public void GetDiatonicSpellings_AllRootsAllModes_NonNull()
    {
        // Pin the full 17 × 7 = 119 coverage. Any missing (root, mode) pair is a Phase 24
        // ship-stopper because the analyzer would silently fail-open per D-22 even though
        // ScaleDatabase.TryParseKeyWithMode accepts the input.
        string[] roots = { "C", "Csharp", "Db", "D", "Dsharp", "Eb", "E", "F", "Fsharp",
                           "Gb", "G", "Gsharp", "Ab", "A", "Asharp", "Bb", "B" };
        Mode[] modes = { Mode.Major, Mode.Minor, Mode.Dorian, Mode.Phrygian,
                         Mode.Lydian, Mode.Mixolydian, Mode.Locrian };
        foreach (var root in roots)
            foreach (var mode in modes)
                Assert.NotNull(DiatonicSpellings.GetDiatonicSpellings(root, mode));
    }
}
