using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Sweep0614;

/// <summary>
/// Regression coverage for the sweep-2026-06-14 "interpreter-dispatch-types"
/// group:
///
///   1. Top-level `return` (bare, and inside a top-level for/while loop) must
///      NOT silently truncate the rest of the program. It now reports a
///      charitable advisory and clears the flag so subsequent statements still
///      run (mirrors the ClearLeakedReturn discipline already applied to
///      musical-context / tuning / live / section blocks).
///   2. User procs and Flow-defined stdlib procs honour the universal named-arg
///      surface (parameter names threaded through to the FunctionSignature).
///   3. Named-arg overload resolution tries EVERY name-eligible candidate and
///      picks the type-matching one, instead of locking onto the first
///      registered and failing.
///   4. A whole-number Long routes to the transpose(Sequence, Semitone)
///      overload, consistent with the Int → Long widening chain.
///   5. TupleType.Equals / GetHashCode honour the .NET equal-objects-hash-equal
///      invariant (AnyArity no longer short-circuits Equals to true).
/// </summary>
[Collection("FlowScripts")] // serialize Console.SetOut
public class InterpreterDispatchTypesSweepTests
{
    // ===== Bug: top-level `return` truncates the program =====

    [Fact]
    public void TopLevelReturn_InForLoop_DoesNotTruncateProgram()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, _) = runner.RunSource(
            "use \"@std\"\n" +
            "for Int i in [1,2,3] {\n" +
            "  (print (str i))\n" +
            "  return i\n" +
            "}\n" +
            "(print \"AFTER LOOP\")\n");

        // The first iteration's print fires, then the leaked return is reported
        // + cleared, and the statement AFTER the loop still runs.
        Assert.Contains("AFTER LOOP", stdout);
        Assert.Contains("'return' at top level is not allowed", stderr);
    }

    [Fact]
    public void TopLevelReturn_Bare_DoesNotTruncateProgram()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, _) = runner.RunSource(
            "use \"@std\"\n" +
            "(print \"BEFORE\")\n" +
            "return 5\n" +
            "(print \"AFTER\")\n");

        Assert.Contains("BEFORE", stdout);
        Assert.Contains("AFTER", stdout); // would be silently dropped before the fix
        Assert.Contains("'return' at top level is not allowed", stderr);
    }

    [Fact]
    public void ReturnInsideProc_StillExitsProc_Unaffected()
    {
        // Guard: the fix is gated on top-level only; an in-proc return must
        // still short-circuit the proc body and produce its value.
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, _) = runner.RunSource(
            "use \"@std\"\n" +
            "proc f(Int: x)\n" +
            "  return x\n" +
            "  (print \"UNREACHABLE\")\n" +
            "end proc\n" +
            "Int r = (f 9)\n" +
            "(print (str r))\n");

        Assert.Contains("9", stdout);
        Assert.DoesNotContain("UNREACHABLE", stdout);
        Assert.DoesNotContain("top level", stderr);
    }

    // ===== Bug: named-arg support on user / stdlib procs =====

    [Fact]
    public void UserProc_AcceptsNamedArgs()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, _) = runner.RunSource(
            "use \"@std\"\n" +
            "proc addThem(Int: a, Int: b)\n" +
            "  (add a b)\n" +
            "end proc\n" +
            "Int r = (addThem a=3 b=4)\n" +
            "(print (str r))\n");

        Assert.Contains("7", stdout);
        Assert.DoesNotContain("does not yet support named arguments", stderr);
    }

    [Fact]
    public void UserProc_NamedArgs_ReorderToCorrectSlots()
    {
        // Order-independence: name binding must beat positional order.
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, _) = runner.RunSource(
            "use \"@std\"\n" +
            "proc sub2(Int: a, Int: b)\n" +
            "  (sub a b)\n" +
            "end proc\n" +
            "Int r = (sub2 b=1 a=10)\n" +
            "(print (str r))\n");

        Assert.Contains("9", stdout); // 10 - 1, NOT 1 - 10
    }

    [Fact]
    public void FlowDefinedStdlibProc_AcceptsNamedArgs()
    {
        // createSineTone is a Flow-defined stdlib proc (audio.flow) whose names
        // were previously dropped. The call must now succeed.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource(
            "use \"@audio\"\n" +
            "Buffer b = (createSineTone frequency=440.0 duration=1.0 amplitude=0.5)\n");

        Assert.True(success, stderr);
        Assert.DoesNotContain("does not yet support named arguments", stderr);
    }

    // ===== Bug: named-arg overload resolution picks first candidate =====

    [Fact]
    public void NamedArg_OverloadedBuiltin_PicksTypeMatchingOverload()
    {
        // transpose registers (Sequence, Semitone) before (Sequence, Cent),
        // both named [seq, amount]. `amount=+50c` (Cent) must reach the Cent
        // overload, not fail "No matching overload (Sequence, Cent)".
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, _) = runner.RunSource(
            "use \"@std\"\n" +
            "Sequence s = | C4 D4 E4 |\n" +
            "Sequence b = (transpose s amount=+50c)\n" +
            "(print \"ok\")\n");

        Assert.True(success, stderr);
        Assert.Contains("ok", stdout);
        Assert.DoesNotContain("No matching overload", stderr);
    }

    [Fact]
    public void NamedArg_OverloadedConversion_PicksTypeMatchingOverload()
    {
        // db registers Int first; `(db x=12.0)` is a Double and must reach the
        // Double overload rather than failing on the Int survivor.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource(
            "use \"@std\"\n" +
            "Decibel d = (db x=12.0)\n");

        Assert.True(success, stderr);
        Assert.DoesNotContain("No matching overload", stderr);
    }

    // ===== Bug: Long argument fits neither Semitone nor Cent =====

    [Fact]
    public void Transpose_AcceptsWholeNumberLong_AsSemitone()
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource(
            "use \"@std\"\n" +
            "Sequence s = | C4 D4 E4 |\n" +
            "Long amt = 2\n" +
            "Sequence b = (transpose s amt)\n");

        Assert.True(success, stderr);
        Assert.DoesNotContain("No matching overload", stderr);
    }

    [Fact]
    public void Transpose_AcceptsLong_AndIsEquivalentToInt()
    {
        // The Long path must produce the same transposed sequence as the Int
        // path (both behave as a semitone count).
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, _) = runner.RunSource(
            "use \"@std\"\n" +
            "Sequence si = (transpose (| C4 |) 2)\n" +
            "Long n = 2\n" +
            "Sequence sl = (transpose (| C4 |) n)\n" +
            "(print (str si))\n" +
            "(print (str sl))\n");
        Assert.True(success, stderr);
        Assert.DoesNotContain("No matching overload", stderr);
        // The two printed sequences should be identical (D4 in both).
        var lines = stdout.Trim().Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal(lines[0], lines[1]);
    }

    [Fact]
    public void SemitoneType_StaysIntOnly_LongNotCompatible()
    {
        // The D-08 carve-out: SemitoneType is NOT globally widened to Long
        // (which would break the Int-only (semitones x) contract). The Long
        // transpose support is an explicit transpose-scoped overload instead.
        Assert.True(SemitoneType.Instance.IsCompatibleWith(IntType.Instance));
        Assert.False(SemitoneType.Instance.IsCompatibleWith(LongType.Instance));
    }

    // ===== Bug: TupleType.AnyArity Equals/GetHashCode invariant =====

    [Fact]
    public void TupleType_AnyArity_NotEqualToConcreteTuple_AndHashesConsistently()
    {
        var concrete = new TupleType(new[] { (FlowLang.TypeSystem.FlowType)IntType.Instance });

        // AnyArity must NOT claim structural equality with a concrete tuple
        // (Equals must agree with GetHashCode, which already differs).
        Assert.False(TupleType.AnyArity.Equals(concrete));
        Assert.False(concrete.Equals(TupleType.AnyArity));

        // Invariant: equal objects hash equally — verified by NOT being equal
        // when hashes differ. AnyArity stays equal to itself.
        Assert.True(TupleType.AnyArity.Equals(TupleType.AnyArity));
        Assert.Equal(TupleType.AnyArity.GetHashCode(), TupleType.AnyArity.GetHashCode());

        // Wildcard matching still lives in IsCompatibleWith (the dispatch path).
        Assert.True(TupleType.AnyArity.IsCompatibleWith(concrete));
        Assert.True(concrete.IsCompatibleWith(TupleType.AnyArity));
    }

    [Fact]
    public void TupleType_ConcreteTuples_EqualsImpliesHashEquals()
    {
        var a = new TupleType(new[] { (FlowLang.TypeSystem.FlowType)IntType.Instance, StringType.Instance });
        var b = new TupleType(new[] { (FlowLang.TypeSystem.FlowType)IntType.Instance, StringType.Instance });
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
