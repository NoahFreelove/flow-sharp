using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase14;

/// <summary>
/// Phase 14 DX-06 enharmonic() Facts.
///
/// CONTEXT D-03: signature is <c>enharmonic(Note) → Note</c>, registered as a context-dependent
/// built-in reading <c>MusicalContext.Key</c>.
/// CONTEXT D-04: in-key inputs respell to the diatonic scale tone.
/// CONTEXT D-05: no-key flips sharp ↔ flat; naturals return unchanged (no E↔Fb / F↔E# /
///              B↔Cb / C↔B# edge respelling).
///
/// Facts drive the built-in via <see cref="FlowEngineRunner"/> because enharmonic() requires
/// an <c>ExecutionContext</c> to read the active musical context. stdout substring assertions
/// are keyed off the Format canonical emission from Commit A (run-based <c>+N</c>/<c>-N</c>):
///   Format('C', 4, 1)  == "C4+"
///   Format('G', 3, -1) == "G3-"
///   Format('D', 4, -1) == "D4-"
///   Format('G', 4, -1) == "G4-"
///   Format('G', 4, 0)  == "G4"
/// </summary>
[Collection("FlowScripts")]
public class EnharmonicTests
{
    [Fact]
    public void NoKey_FlatToSharp_Db4()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic Db4)))
");
        Assert.Equal(0, errorCount);
        // Db4 (MIDI 61) flips to C#4 → Format('C', 4, +1) = "C4+"
        Assert.Contains("C4+", stdout);
    }

    [Fact]
    public void NoKey_SharpToFlat_Fsharp3()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic F#3)))
");
        Assert.Equal(0, errorCount);
        // F#3 (MIDI 54) flips to Gb3 → Format('G', 3, -1) = "G3-"
        Assert.Contains("G3-", stdout);
    }

    [Fact]
    public void NoKey_NaturalUnchanged_C4()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic C4)))
");
        Assert.Equal(0, errorCount);
        Assert.Contains("C4", stdout);
        // D-05: no edge respelling — must not echo B# or Cb.
        Assert.DoesNotContain("B#", stdout);
        Assert.DoesNotContain("Cb", stdout);
    }

    [Fact]
    public void NoKey_NaturalUnchanged_E4()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic E4)))
");
        Assert.Equal(0, errorCount);
        Assert.Contains("E4", stdout);
        Assert.DoesNotContain("Fb", stdout);
    }

    [Fact]
    public void NoKey_NaturalUnchanged_B4()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic B4)))
");
        Assert.Equal(0, errorCount);
        Assert.Contains("B4", stdout);
        Assert.DoesNotContain("Cb", stdout);
    }

    [Fact]
    public void NoKey_NaturalUnchanged_F4()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic F4)))
");
        Assert.Equal(0, errorCount);
        Assert.Contains("F4", stdout);
        Assert.DoesNotContain("E#", stdout);
    }

    [Fact]
    public void InKey_Dbmajor_CsharpRespells()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
key Dbmajor {
    (print (str (enharmonic C#4)))
}
");
        Assert.Equal(0, errorCount);
        // C#4 (MIDI 61) is diatonic in Dbmajor → Db4 spelling → Format('D', 4, -1) = "D4-"
        // Pitfall 3 mitigation gate: ScaleDatabase.GetScaleNotes("Dbmajor") returns sharp-
        // spelled tokens, but the MIDI-based in-key lookup + preferFlat heuristic recovers
        // the flat-key spelling. If this Fact goes RED, the preferFlat heuristic needs
        // extension (documented in 14-02-SUMMARY.md Divergences).
        Assert.Contains("D4-", stdout);
    }

    [Fact]
    public void InKey_Cmajor_FsharpFallsBack()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
key Cmajor {
    (print (str (enharmonic F#4)))
}
");
        Assert.Equal(0, errorCount);
        // F#4 (MIDI 66) is chromatic (not diatonic) in Cmajor → falls to no-key flip →
        // Gb4 → Format('G', 4, -1) = "G4-".
        Assert.Contains("G4-", stdout);
    }

    [Fact]
    public void DoubleSharp_NonInvolutive_FdoubleSharp()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic F##4)))
");
        Assert.Equal(0, errorCount);
        // F##4 = MIDI 67 = G4 natural. Letter-up from F is G; alt = 67 - 67 = 0. Documented
        // non-involutive per D-05: enharmonic(enharmonic(F##4)) = enharmonic(G4) = G4, not F##4.
        Assert.Contains("G4", stdout);
    }
}
