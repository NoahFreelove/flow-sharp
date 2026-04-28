using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase20;

/// <summary>
/// Phase 20 DEFER-04 multi-letter enharmonic edge Facts.
///
/// Closes the v1.2-deferred enharmonic edge respelling deliberately scope-cut by Phase 14
/// D-05. Under DEFER-04 the four "edge naturals" (E, F, B, C) respell to their multi-letter
/// enharmonic neighbor (E↔Fb, F↔E#, B↔Cb octave+1, C↔B# octave−1). D/G/A naturals continue
/// to return unchanged because they sit between two whole-step letters with no adjacent
/// same-pitch spelling. In-key diatonic preservation (D-USER-B) wins over edge respelling:
/// when the input pitch matches a scale tone, we return that scale spelling — only chromatic
/// in-key inputs fall through to the no-key natural-edge.
///
/// Tests drive the built-in via <see cref="FlowEngineRunner"/> because <c>enharmonic</c>
/// requires an <see cref="FlowLang.Runtime.ExecutionContext"/> to read the active musical
/// context. Stdout substring assertions are keyed off the Format canonical emission:
///   Format('F', 4, -1) == "F4-"   (Fb4)
///   Format('E', 4, +1) == "E4+"   (E#4)
///   Format('C', 5, -1) == "C5-"   (Cb5, octave +1)
///   Format('B', 3, +1) == "B3+"   (B#3, octave -1)
///
/// Round-trip Theory asserts pitch-equivalence (NOT string-equivalence per 20-RESEARCH
/// Pitfall 8 — enharmonic is not involutive at the string level for double-accidentals,
/// but every output round-trips back to the input MIDI).
/// </summary>
[Collection("FlowScripts")]
public class EnharmonicEdgesTests
{
    [Fact]
    public void NoKey_E4_RespellsFb4()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic E4)))
");
        Assert.Equal(0, errorCount);
        // E4 (MIDI 64) → Fb4 → Format('F', 4, -1) = "F4-"
        Assert.Contains("F4-", stdout);
    }

    [Fact]
    public void NoKey_F4_RespellsEsharp4()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic F4)))
");
        Assert.Equal(0, errorCount);
        // F4 (MIDI 65) → E#4 → Format('E', 4, +1) = "E4+"
        Assert.Contains("E4+", stdout);
    }

    [Fact]
    public void NoKey_B4_RespellsCb5()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic B4)))
");
        Assert.Equal(0, errorCount);
        // B4 (MIDI 71) → Cb5 (octave +1) → Format('C', 5, -1) = "C5-"
        Assert.Contains("C5-", stdout);
    }

    [Fact]
    public void NoKey_C4_RespellsBsharp3()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic C4)))
");
        Assert.Equal(0, errorCount);
        // C4 (MIDI 60) → B#3 (octave -1) → Format('B', 3, +1) = "B3+"
        Assert.Contains("B3+", stdout);
    }

    [Fact]
    public void NoKey_D4_Unchanged()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic D4)))
");
        Assert.Equal(0, errorCount);
        // D is not an edge per D-USER-C — unchanged.
        Assert.Contains("D4", stdout);
        Assert.DoesNotContain("C4+", stdout);
        Assert.DoesNotContain("E4-", stdout);
    }

    [Fact]
    public void NoKey_G4_Unchanged()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic G4)))
");
        Assert.Equal(0, errorCount);
        Assert.Contains("G4", stdout);
        Assert.DoesNotContain("F4+", stdout);
        Assert.DoesNotContain("A4-", stdout);
    }

    [Fact]
    public void NoKey_A4_Unchanged()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic A4)))
");
        Assert.Equal(0, errorCount);
        Assert.Contains("A4", stdout);
        Assert.DoesNotContain("G4+", stdout);
        Assert.DoesNotContain("B4-", stdout);
    }

    /// <summary>
    /// Round-trip pitch-equivalence: enharmonic(enharmonic(n)) must produce a note whose MIDI
    /// equals the input's MIDI for every chromatic note. Per 20-RESEARCH Pitfall 8 this is a
    /// MIDI-equality assertion, NOT a string-equality assertion — double-accidental inputs
    /// (e.g. F##4) are documented non-involutive at the string level (F##4 → G4 → A4-?
    /// no — G4 stays G4 by the D/G/A unchanged branch, so MIDI 67 → MIDI 67). The test set is
    /// chromatic-12 + a few enharmonic variants to exercise both branches of the no-key flip.
    /// </summary>
    [Theory]
    [InlineData("C4", 60)]
    [InlineData("D4", 62)]
    [InlineData("E4", 64)]
    [InlineData("F4", 65)]
    [InlineData("G4", 67)]
    [InlineData("A4", 69)]
    [InlineData("B4", 71)]
    [InlineData("C5", 72)]
    [InlineData("Db4", 61)]
    [InlineData("C#4", 61)]
    [InlineData("D#4", 63)]
    [InlineData("F#4", 66)]
    [InlineData("Bb4", 70)]
    public void RoundTrip_PitchEquivalent(string input, int expectedMidi)
    {
        // First pass: enharmonic(input) → result1 string.
        string result1 = RunEnharmonicAndExtract(input);

        // Second pass: enharmonic(result1) → result2 string.
        string result2 = RunEnharmonicAndExtract(result1);

        // Parse result2 and assert its MIDI equals the input's MIDI.
        var (letter2, octave2, alteration2) = NoteType.Parse(result2);
        int actualMidi = NoteType.ToMidiNote(letter2, octave2, alteration2);
        Assert.Equal(expectedMidi, actualMidi);
    }

    [Fact]
    public void NoKey_Fb4_RoundTripsToE4()
    {
        // The existing ComputeFlippedSpelling already handles edge inverses via LetterDown
        // (Fb → E natural). Verifies DEFER-04 didn't break the inverse path.
        string result = RunEnharmonicAndExtract("Fb4");
        var (letter, octave, alteration) = NoteType.Parse(result);
        int midi = NoteType.ToMidiNote(letter, octave, alteration);
        Assert.Equal(64, midi); // E4
    }

    [Fact]
    public void NoKey_Bsharp3_RoundTripsToC4()
    {
        // B#3 → C4 via ComputeFlippedSpelling LetterUp (B → C, octave bump).
        string result = RunEnharmonicAndExtract("B#3");
        var (letter, octave, alteration) = NoteType.Parse(result);
        int midi = NoteType.ToMidiNote(letter, octave, alteration);
        Assert.Equal(60, midi); // C4
    }

    [Fact]
    public void InKey_Fmajor_E4_PreservesDiatonic()
    {
        // F major scale: F G A Bb C D E. E4 is diatonic, so the in-key branch fires and
        // returns "E4" — the natural-edge respelling does NOT trip. This pins D-USER-B
        // (in-key diatonic preservation wins over edge respelling) per Phase 14 D-04 precedent.
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
key Fmajor {
    (print (str (enharmonic E4)))
}
");
        Assert.Equal(0, errorCount);
        Assert.Contains("E4", stdout);
        Assert.DoesNotContain("F4-", stdout);
    }

    [Fact]
    public void InKey_Bbmajor_E4_FallsThroughToEdge()
    {
        // Bb major scale: Bb C D Eb F G A. E natural is chromatic (not in scale), so the
        // in-key TryEnharmonicInKey returns false and we fall through to the no-key edge:
        // E4 → Fb4 → "F4-". Pins the chromatic-in-key fall-through path per D-USER-B.
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
key Bbmajor {
    (print (str (enharmonic E4)))
}
");
        Assert.Equal(0, errorCount);
        Assert.Contains("F4-", stdout);
    }

    /// <summary>
    /// Helper: runs <c>(print (str (enharmonic &lt;input&gt;)))</c> via FlowEngineRunner and
    /// extracts the canonical note string (last non-empty whitespace-separated token in stdout).
    /// </summary>
    private static string RunEnharmonicAndExtract(string input)
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource($@"
use ""@std""
(print (str (enharmonic {input})))
");
        Assert.Equal(0, errorCount);

        // stdout is the canonical Format string followed by a newline. Trim and take the
        // last non-empty token (defensive against stray whitespace).
        var tokens = stdout.Split(new[] { ' ', '\t', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(tokens);
        return tokens[^1];
    }
}
