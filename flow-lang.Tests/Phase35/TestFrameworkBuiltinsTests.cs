using FlowLang.Core;
using FlowLang.StandardLibrary.TestFramework;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-04 Wave 0 — TEST-01 gating facts for the (test ...) +
/// 5 assertion-primitive builtins. Asserts that:
///
///   1. The six builtin names register on every fresh FlowEngine.
///   2. <c>(assert false)</c> throws <see cref="AssertionException"/> with a
///      message containing "assert failed" (RESEARCH §Example 3).
///
/// RED state: this file references <c>FlowLang.StandardLibrary.TestFramework</c>
/// types that Task 2 creates. Until Task 2 lands the namespace, this file
/// fails to compile — Wave 0's expected RED signal.
/// </summary>
public class TestFrameworkBuiltinsTests
{
    private static readonly string[] ExpectedBuiltins =
    {
        "test",
        "assert",
        "assertEq",
        "assertNotesMatch",
        "assertBytesEqual",
        "assertWithinDb",
    };

    [Fact]
    public void AllAssertBuiltinsRegistered()
    {
        using var engine = new FlowEngine(verbose: false);
        foreach (var name in ExpectedBuiltins)
        {
            Assert.True(
                engine.Context.InternalRegistry.HasImplementation(name),
                $"Expected TEST-01 builtin '{name}' to be registered on FlowEngine init.");
        }
    }

    [Fact]
    public void AssertThrowsOnFalse()
    {
        // FlowEngine.Execute catches every exception (including AssertionException)
        // and converts to an ErrorReporter entry — so (assert false) at top-level
        // returns false from Execute and populates the error reporter. Inside a
        // (test "name" lazy(...)) body the TestRunner catches AssertionException
        // directly and converts to a FAIL outcome. Both paths surface the failure
        // — this fact pins the top-level behavior.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute("use \"@test\"\n(assert false)");
        Assert.False(ok, "(assert false) should cause Execute to return false.");
        var errors = engine.ErrorReporter.FormatErrors();
        Assert.Contains("assert", errors, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssertThrowsOnFalse_DirectThrow_InsideTestRunner()
    {
        // When (assert false) runs INSIDE a (test ...) body forced by the
        // TestRunner, the AssertionException propagates out of BodyThunk.Force
        // and the runner catches it (FAIL outcome). This fact validates the
        // raw throw without the FlowEngine.Execute wrapper swallowing it.
        using var engine = new FlowEngine(verbose: false);
        var registerOk = engine.Execute(
            "use \"@test\"\n(test \"fails\" lazy((assert false)))");
        Assert.True(registerOk, "Registration of the failing test should succeed.");
        Assert.Single(engine.Context.TestRegistry);

        var runner = new TestRunner();
        var (passed, failed) = runner.Run(engine, "<inline>");
        Assert.Equal(0, passed);
        Assert.Equal(1, failed);
    }

    [Fact]
    public void AssertWithTruePasses()
    {
        // Negative-control: (assert true) MUST NOT throw — proves the gate is
        // not "throws on every call". Pairs with AssertThrowsOnFalse.
        using var engine = new FlowEngine(verbose: false);
        var ok = engine.Execute("use \"@test\"\n(assert true)");
        Assert.True(ok, "FlowEngine.Execute should succeed on (assert true).");
    }
}
