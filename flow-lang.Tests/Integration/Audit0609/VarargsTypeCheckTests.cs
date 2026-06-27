using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 0609 §2.9 follow-up (deferred out of Packet D, applied at integration).
///
/// FunctionSignature.Matches previously validated trailing vararg arguments ONLY
/// when the last InputType was an ArrayType. User procs register `T...: xs` with
/// the bare element type T, so every user-defined varargs proc accepted
/// arbitrarily-typed trailing args that exploded downstream as internal cast
/// errors. The fix mirrors CalculateSpecificity's element-type fallback.
///
/// The one shipped builtin that relied on the skip-validation hole was oscSend
/// (its signature ended at the `path` String, implying String payload elements);
/// it now carries a trailing VoidType wildcard element slot instead.
/// </summary>
[Trait("Category", "Audit0609")]
[Collection("FlowScripts")] // serialize Console.SetOut (RESEARCH Pitfall 4)
public class VarargsTypeCheckTests
{
    [Fact]
    public void UserProcVarargs_MatchingTrailingArgs_Resolve()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errorCount) = runner.RunSource(
            "proc count(Int...: nums)\n" +
            "  (len nums)\n" +
            "end\n" +
            "Int c = (count 1 2 3)\n" +
            "(print (str c))");
        Assert.True(success, "well-typed varargs call must resolve");
        Assert.Equal(0, errorCount);
        Assert.Contains("3", stdout);
    }

    [Fact]
    public void UserProcVarargs_MismatchedTrailingArgs_ComposerFacingError()
    {
        using var runner = new FlowEngineRunner();
        // Pre-fix: Matches skipped vararg validation (Int is not an ArrayType),
        // the call dispatched, and the body's As<int> threw an internal
        // InvalidCastException. Post-fix: no matching overload — a reported,
        // composer-facing diagnostic, not a crash.
        var (_, _, _, errorCount) = runner.RunSource(
            "proc count(Int...: nums)\n" +
            "  (len nums)\n" +
            "end\n" +
            "(count \"a\" #b)");
        Assert.True(errorCount > 0, "mismatched varargs must surface a reported diagnostic");
    }

    [Fact]
    public void UserProcVarargs_ZeroTrailingArgs_StillResolve()
    {
        using var runner = new FlowEngineRunner();
        var (success, _, _, errorCount) = runner.RunSource(
            "proc count(Int...: nums)\n" +
            "  (len nums)\n" +
            "end\n" +
            "Int c = (count)\n" +
            "(print (str c))");
        Assert.True(success, "zero varargs is a legal arity");
        Assert.Equal(0, errorCount);
    }

    [Fact]
    public void OscSendShape_VoidWildcardElement_AcceptsHeterogeneousPayload()
    {
        // Mirrors the oscSend registration shape: 3 fixed params + Void vararg
        // element. The wildcard must admit a mixed Int/Double/Bool payload.
        var sig = new FunctionSignature("oscSend",
            new FlowType[] { StringType.Instance, IntType.Instance, StringType.Instance, VoidType.Instance },
            IsVarArgs: true,
            ParameterNames: new[] { "host", "port", "path", "args" });

        Assert.True(sig.Matches(
            new FlowType[] { StringType.Instance, IntType.Instance, StringType.Instance,
                             IntType.Instance, DoubleType.Instance, BoolType.Instance },
            strictMode: false));

        // path is now a validated FIXED param — an Int in its slot must not match.
        Assert.False(sig.Matches(
            new FlowType[] { StringType.Instance, IntType.Instance, IntType.Instance },
            strictMode: false));
    }

    [Fact]
    public void TypedVarargElement_RejectsMismatch_NonArrayTypeForm()
    {
        // The bare-element-type form (how user procs register) must validate.
        var sig = new FunctionSignature("sum",
            new FlowType[] { IntType.Instance },
            IsVarArgs: true,
            ParameterNames: new[] { "nums" });

        Assert.True(sig.Matches(new FlowType[] { IntType.Instance, IntType.Instance }, strictMode: false));
        Assert.False(sig.Matches(new FlowType[] { StringType.Instance }, strictMode: false));
    }
}
