using System;
using System.IO;
using FlowLang.Core;
using FlowLang.StandardLibrary.TestFramework;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-04 Wave 0 — TEST-01 Pitfall 10 gate: <c>(test "name" body)</c>
/// MUST defer body evaluation until the TestRunner forces the captured Thunk.
///
/// Pitfall 10 is LOAD-BEARING — without the <c>LazyType</c> wrap on the second
/// parameter, the body argument evaluates eagerly at the registration call site
/// and hermetic isolation is meaningless (all bodies have already run by the
/// time TestRunner walks the registry).
///
/// <para>
/// <c>[Collection("FlowScripts")]</c> — serializes with the other
/// Console.SetOut-touching tests (Phase 15 ReverbTime, Phase 26.1 Tuples,
/// InterpreterTests) so the captured StringWriter doesn't race with
/// concurrent test output (xUnit's default parallel runner would otherwise
/// interleave). RESEARCH Pitfall 4.
/// </para>
///
/// RED state: requires <see cref="TestRunner"/> + the <c>(test ...)</c> builtin
/// — both land in Task 2.
/// </summary>
[Collection("FlowScripts")]
public class TestBodyDeferralTests
{
    [Fact]
    public void TestBodyNotEvaluatedAtRegistration()
    {
        using var engine = new FlowEngine(verbose: false);
        var originalOut = Console.Out;
        using var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);

            // Registering the test MUST NOT execute the body. If the LazyType
            // wrap is missing, (print "WOULD_RUN") runs at parse-eval time and
            // the captured stream contains the sentinel before TestRunner runs.
            var ok = engine.Execute(
                "use \"@test\"\n(test \"deferred\" lazy((print \"WOULD_RUN\")))");
            Assert.True(ok, "FlowEngine.Execute should succeed registering a test.");

            // PRE-RUN: nothing should have printed.
            var preRun = captured.ToString();
            Assert.DoesNotContain("WOULD_RUN", preRun);

            // Registry should now hold exactly one TestRecord.
            Assert.Single(engine.Context.TestRegistry);

            // Force the test bodies via TestRunner — the body MUST print now.
            var runner = new TestRunner();
            runner.Run(engine, "<inline>");
            var postRun = captured.ToString();
            Assert.Contains("WOULD_RUN", postRun);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
