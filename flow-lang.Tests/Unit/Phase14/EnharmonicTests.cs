using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase14;

/// <summary>
/// Phase 14 DX-06 enharmonic() Facts.
///
/// CONTEXT D-03: signature is <c>enharmonic(Note) → Note</c>, registered as a context-dependent
/// built-in reading <c>MusicalContext.Key</c>.
/// CONTEXT D-04: in-key inputs respell to the diatonic scale tone.
/// CONTEXT D-05 (original Phase 14 contract): no-key flips sharp ↔ flat; naturals return
/// unchanged.
///
/// Phase 20 plan 20-02 (DEFER-04) MIGRATED 4 NoKey_NaturalUnchanged_C4/E4/B4/F4 Facts into
/// NoKey_NaturalEdgeRespells_C4/E4/B4/F4 — naturals at edge letters (E/F/B/C) now respell to
/// multi-letter neighbors (E↔Fb, F↔E#, B↔Cb octave+1, C↔B# octave−1). The Phase 14 D-05
/// "naturals always unchanged" contract was deliberately scope-cut and reframed by
/// REQUIREMENTS.md DEFER-04. D/G/A naturals continue to return unchanged (no enharmonic edge
/// — they sit between two whole-step letters with no adjacent same-pitch spelling).
/// Migration shape (a) per 20-RESEARCH Pitfall 1: rename + re-pin (preserves audit trail in
/// the Phase14 directory rather than delete + replace).
///
/// Facts drive the built-in via <see cref="FlowEngineRunner"/> because enharmonic() requires
/// an <c>ExecutionContext</c> to read the active musical context. stdout substring assertions
/// are keyed off the Format canonical emission from Commit A (run-based <c>+N</c>/<c>-N</c>):
///   Format('C', 4, 1)  == "C4+"
///   Format('G', 3, -1) == "G3-"
///   Format('D', 4, -1) == "D4-"
///   Format('G', 4, -1) == "G4-"
///   Format('G', 4, 0)  == "G4"
///   Format('F', 4, -1) == "F4-"   (Fb4 — DEFER-04)
///   Format('E', 4, +1) == "E4+"   (E#4 — DEFER-04)
///   Format('C', 5, -1) == "C5-"   (Cb5 — DEFER-04)
///   Format('B', 3, +1) == "B3+"   (B#3 — DEFER-04)
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

    /// <summary>
    /// MIGRATED by Phase 20 plan 20-02 (DEFER-04): C4 (MIDI 60) now respells to B#3 (MIDI 60).
    /// Previously NoKey_NaturalUnchanged_C4 — see class XML doc for migration rationale.
    /// </summary>
    [Fact]
    public void NoKey_NaturalEdgeRespells_C4()
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

    /// <summary>
    /// MIGRATED by Phase 20 plan 20-02 (DEFER-04): E4 (MIDI 64) now respells to Fb4 (MIDI 64).
    /// Previously NoKey_NaturalUnchanged_E4 — see class XML doc for migration rationale.
    /// </summary>
    [Fact]
    public void NoKey_NaturalEdgeRespells_E4()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic E4)))
");
        Assert.Equal(0, errorCount);
        // E4 (MIDI 64) → Fb4 (same octave) → Format('F', 4, -1) = "F4-"
        Assert.Contains("F4-", stdout);
    }

    /// <summary>
    /// MIGRATED by Phase 20 plan 20-02 (DEFER-04): B4 (MIDI 71) now respells to Cb5 (MIDI 71).
    /// Previously NoKey_NaturalUnchanged_B4 — see class XML doc for migration rationale.
    /// </summary>
    [Fact]
    public void NoKey_NaturalEdgeRespells_B4()
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

    /// <summary>
    /// MIGRATED by Phase 20 plan 20-02 (DEFER-04): F4 (MIDI 65) now respells to E#4 (MIDI 65).
    /// Previously NoKey_NaturalUnchanged_F4 — see class XML doc for migration rationale.
    /// </summary>
    [Fact]
    public void NoKey_NaturalEdgeRespells_F4()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
(print (str (enharmonic F4)))
");
        Assert.Equal(0, errorCount);
        // F4 (MIDI 65) → E#4 (same octave) → Format('E', 4, +1) = "E4+"
        Assert.Contains("E4+", stdout);
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
