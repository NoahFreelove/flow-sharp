using System;
using System.Collections.Generic;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_1;

/// <summary>
/// Phase 26.1 Wave 2 (GREEN): pins TUP-09 — Tuple type with
/// <c>&lt;&lt;a, b&gt;&gt;</c> literal syntax, empty/singleton arities, indexing via @N,
/// structural equality, the AnyArity sentinel (Assumption A5 from RESEARCH), and
/// the lexer expression-start gate extension (revision 1) so destructure-statement-
/// after-prev-statement parses correctly.
///
/// Source-running facts go through <see cref="FlowEngineRunner"/> with
/// <c>[Collection("FlowScripts")]</c> so Console.SetOut is serialized (RESEARCH Pitfall 4).
/// AnyArity is a pure type-system check that doesn't need the engine.
/// </summary>
[Collection("FlowScripts")]
public class TupleFacts
{
    [Fact]
    public void Literal_ParsesAndEvaluates()
    {
        // <<1, 2, 3>> evaluates to a 3-arity Tuple value with each component reachable via @N.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Tuple<<Int, Int, Int>> t = <<1, 2, 3>>
(print (str t@0))
(print (str t@1))
(print (str t@2))
(print ""OK"")
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("1", stdout);
        Assert.Contains("2", stdout);
        Assert.Contains("3", stdout);
        Assert.Contains("OK", stdout);
    }

    [Fact]
    public void EmptyAndSingleton_BothValid()
    {
        // <<>> empty + <<5>> singleton — both parse, both evaluate, singleton is indexable.
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errCount) = runner.RunSource(@"
use ""@std""
Tuple<<>> e = <<>>
Tuple<<Int>> s = <<5>>
(print (str s@0))
(print ""OK"")
");
        Assert.True(success);
        Assert.Equal(0, errCount);
        Assert.Contains("5", stdout);
        Assert.Contains("OK", stdout);
    }

    [Fact]
    public void AnnotationParse_TupleOfNoteAndBeat()
    {
        // Verifies the `Tuple<<Note, Beat>>` type annotation parses (TUP-09 specifics).
        // Uses default-init (no `= <<...>>` initializer) because Beat has no direct literal
        // construction path in user-source code (CONTEXT block 2's `<<C4, q>>` shape requires
        // a `q` constant that doesn't ship in 26.1 — see Plan 26.1-03 SUMMARY deviation note).
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errCount) = runner.RunSource(@"
use ""@std""
Tuple<<Note, Beat>> entry
(print ""OK"")
");
        Assert.True(success);
        Assert.Equal(0, errCount);
        Assert.Contains("OK", stdout);
    }

    [Fact]
    public void IndexAccess_AtZeroAndOne()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errCount) = runner.RunSource(@"
use ""@std""
Tuple<<Int, Int>> t = <<10, 20>>
(print (str t@0))
(print (str t@1))
");
        Assert.True(success);
        Assert.Equal(0, errCount);
        Assert.Contains("10", stdout);
        Assert.Contains("20", stdout);
    }

    [Fact]
    public void StructuralEquality_PerPositionMatch()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errCount) = runner.RunSource(@"
use ""@std""
Bool ab = (equals <<1, 2>> <<1, 2>>)
Bool cd = (equals <<1, 2>> <<1, 3>>)
(print (str ab))
(print (str cd))
");
        Assert.True(success);
        Assert.Equal(0, errCount);
        var lines = stdout.Trim().Split('\n');
        Assert.Contains("true", lines);
        Assert.Contains("false", lines);
    }

    [Fact]
    public void AnyArityMatches_AnyTuple()
    {
        // Type-system-level check: TupleType.AnyArity.IsCompatibleWith(<<Int, String>>) must be true.
        // Locks Assumption A5 from RESEARCH — required by Wave 3 (unpack) registration.
        var concrete = new TupleType(new FlowType[] { IntType.Instance, StringType.Instance });
        Assert.True(TupleType.AnyArity.IsCompatibleWith(concrete));
        Assert.True(concrete.IsCompatibleWith(TupleType.AnyArity));
    }

    /// <summary>
    /// Revision 1 (plan-checker BLOCKER fix) — exercises the extended expression-start
    /// gate so the lexer fuses <c>&lt;&lt;</c> after value-end tokens that end the
    /// previous statement (CLOSE-DELIM, LITERAL, GreaterGreater).
    /// </summary>
    [Fact]
    public void LexLessLess_AfterValueEndTokens()
    {
        using var runner1 = new FlowEngineRunner();
        var (s1, out1, err1, e1) = runner1.RunSource(@"
use ""@std""
Int x = 5
<<Int a>> = <<10>>
(print (str a))
");
        Assert.True(s1, $"after IntLiteral failed. stderr={err1}");
        Assert.Equal(0, e1);
        Assert.Contains("10", out1);

        using var runner2 = new FlowEngineRunner();
        var (s2, out2, err2, e2) = runner2.RunSource(@"
use ""@std""
(print ""setup"")
<<Int a, Int b>> = <<1, 2>>
(print (str a))
(print (str b))
");
        Assert.True(s2, $"after RParen failed. stderr={err2}");
        Assert.Equal(0, e2);
        Assert.Contains("1", out2);
        Assert.Contains("2", out2);

        using var runner3 = new FlowEngineRunner();
        var (s3, out3, err3, e3) = runner3.RunSource(@"
use ""@std""
Tuple<<Int>> t = <<5>>
<<Int x>> = t
(print (str x))
");
        Assert.True(s3, $"after GreaterGreater failed. stderr={err3}");
        Assert.Equal(0, e3);
        Assert.Contains("5", out3);
    }

    /// <summary>
    /// sweep-0614 regression: a tuple LITERAL must be allowed to start a
    /// statement — the headline `~>` tuple-unpack form `&lt;&lt;3, 4&gt;&gt; ~&gt; f`
    /// and a bare `&lt;&lt;1, 2&gt;&gt;`. Previously ParseStatement committed ANY
    /// statement-start `&lt;&lt;` to the destructure grammar, so these reported
    /// "Expected identifier in destructure pattern". The disambiguation peeks for
    /// a `=` after the matching `&gt;&gt;`; only then is it a destructure target.
    /// </summary>
    [Fact]
    public void TupleLiteral_CanStartStatement_ViaUnpackFlow()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
<<3, 4>> ~> add -> print
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("7", stdout);
    }

    [Fact]
    public void TupleLiteral_CanStartStatement_Bare()
    {
        // A bare tuple-literal statement must parse without a destructure error.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
(print ""before"")
<<1, 2>>
(print ""after"")
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("before", stdout);
        Assert.Contains("after", stdout);
    }

    [Fact]
    public void TupleDestructure_StillParses_AfterLiteralDisambiguation()
    {
        // Regression guard: the disambiguation must NOT break the destructure form.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
<<Int a, Int b>> = <<10, 20>>
(print (str a))
(print (str b))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("10", stdout);
        Assert.Contains("20", stdout);
    }
}
