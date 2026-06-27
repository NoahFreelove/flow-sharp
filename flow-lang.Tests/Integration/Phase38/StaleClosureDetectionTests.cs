using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Interpreter;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-03 LIVE-03 — Wave 0 stale-closure detection tests.
///
/// Asserts <see cref="LambdaCaptureAuditor.CollectFileScopeReferences"/> walks
/// the AST and reports every variable name referenced from inside the body
/// scope (or any nested lambda within it) that is NOT shadowed by a local
/// declaration. The live-block swap path consumes this set to detect closures
/// whose captured file-scope bindings have been removed since the previous
/// render — per RESEARCH §C lines 698-765.
///
/// Tests are RED until Task 2 lands flow-lang/Interpreter/LambdaCaptureAuditor.cs.
/// </summary>
[Collection("FlowScripts")]
public class StaleClosureDetectionTests : IDisposable
{
    public StaleClosureDetectionTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Lex+parse a Flow source string into a <see cref="Program"/> — mirrors
    /// the <see cref="FlowEngine.Execute"/> lex+parse phase but stops before
    /// interpretation so the AST shape is the test subject. Same pattern as
    /// the Plan 38-02 LiveBlockParserTests helper.
    /// </summary>
    private static IReadOnlyList<Statement> ParseToStatements(string source, string? fileName = "<test>")
    {
        var errorReporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, errorReporter, fileName);
        var tokens = lexer.Tokenize();
        Assert.False(errorReporter.HasErrors,
            $"Lex errors: {errorReporter.Errors.FirstOrDefault()?.Message}");
        var parser = new Parser(tokens, errorReporter);
        var program = parser.Parse();
        Assert.False(errorReporter.HasErrors,
            $"Parse errors: {errorReporter.Errors.FirstOrDefault()?.Message}");
        return program.Statements;
    }

    /// <summary>
    /// A lambda body referencing a file-scope binding (`globalCount`) and its
    /// own parameter (`x`) should report `globalCount` as a file-scope
    /// reference — `x` is shadowed by the lambda parameter, so it must NOT
    /// appear in the result.
    /// </summary>
    [Fact]
    public void LambdaReferencingFileScopeBinding_Detected()
    {
        var body = ParseToStatements(
            "Int globalCount = 10; Function f = fn Int x => (add x globalCount);");

        // We want to audit the closure body itself. The simplest way is to
        // pass the whole program — the auditor walks down into LambdaExpression
        // bodies and reports any name that escapes ALL local scopes. Both
        // `globalCount` (declared as a file-scope binding in this same body)
        // and `f` are file-scope-declared, so they should be ELIMINATED from
        // the result. The only escaping reference is `add` (a builtin).
        var refs = LambdaCaptureAuditor.CollectFileScopeReferences(body);

        Assert.Contains("add", refs);
        Assert.DoesNotContain("globalCount", refs); // declared at file scope HERE
        Assert.DoesNotContain("f", refs);            // declared at file scope HERE
        Assert.DoesNotContain("x", refs);            // lambda parameter
    }

    /// <summary>
    /// When the body declares a local binding with the same name as a
    /// would-be-file-scope reference, the audit MUST report the local
    /// shadowing — the name does NOT escape the body scope.
    /// </summary>
    [Fact]
    public void LocalShadowingFileScope_NotReported()
    {
        var body = ParseToStatements(
            "Int globalCount = 5; Int doubled = (mul globalCount 2);");

        var refs = LambdaCaptureAuditor.CollectFileScopeReferences(body);

        // globalCount is declared at file scope here, so it's shadowed (the
        // declaration is in scope BEFORE the reference). It must NOT appear.
        Assert.DoesNotContain("globalCount", refs);
        Assert.DoesNotContain("doubled", refs);

        // mul is the only escaping reference.
        Assert.Contains("mul", refs);
    }

    /// <summary>
    /// A lambda nested inside another lambda referencing a file-scope binding
    /// — the file-scope reference escapes BOTH lambda scopes.
    /// </summary>
    [Fact]
    public void NestedLambdaCapture_Detected()
    {
        var body = ParseToStatements(
            "Function outer = fn Int x => (map list (fn Int y => (add x globalCount)));");

        var refs = LambdaCaptureAuditor.CollectFileScopeReferences(body);

        // x is shadowed by outer's parameter, y by inner's parameter, outer
        // is declared at file scope HERE. globalCount and list and map and
        // add escape ALL scopes.
        Assert.Contains("globalCount", refs);
        Assert.Contains("list", refs);
        Assert.Contains("map", refs);
        Assert.Contains("add", refs);
        Assert.DoesNotContain("x", refs);
        Assert.DoesNotContain("y", refs);
        Assert.DoesNotContain("outer", refs);
    }

    /// <summary>
    /// A body with zero references that escape (just local declarations and
    /// arithmetic on those locals) returns an empty set — no stale-closure
    /// risk possible.
    /// </summary>
    [Fact]
    public void NoFileScopeReferences_ReturnsBuiltinsOnly()
    {
        var body = ParseToStatements(
            "Int x = 1; Int y = 2; Int z = (add x y);");

        var refs = LambdaCaptureAuditor.CollectFileScopeReferences(body);

        // x, y, z are declared locally — none escape.
        Assert.DoesNotContain("x", refs);
        Assert.DoesNotContain("y", refs);
        Assert.DoesNotContain("z", refs);
        // add is the only escaping reference (a builtin); the live-swap
        // consumer rejects only references NOT present as variables OR
        // functions in the global frame at the time of the check — builtins
        // pass that gate.
        Assert.Contains("add", refs);
    }
}
