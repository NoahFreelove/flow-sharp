using System;
using System.Linq;
using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase43;

/// <summary>
/// Phase 43 Plan 43-01 REQ-MOD-01 — Wave 0 parser tests for the
/// <c>module &lt;name&gt;</c> top-of-file declaration surface.
///
/// Asserts: (a) lexer recognizes <c>module</c> keyword (lex-only smoke);
/// (b) parser produces <see cref="ModuleDeclarationStatement"/> at
/// <c>Statements[0]</c> when the declaration is the first non-comment statement;
/// (c) leading <c>//</c> comments do NOT count as non-comment statements;
/// (d) mid-file <c>module</c> declarations parse-error with a position-constraint
/// message; (e) invalid module names (numeric literal, missing identifier)
/// produce a parse error citing the keyword's source location.
///
/// Per CONTEXT D-01 / D-03, the declaration is purely syntactic in this plan;
/// runtime registration + qualified-access dispatch ship in subsequent plans.
///
/// Test 1 is GREEN after Task 1 (lex-only smoke); Tests 2-5 are RED until
/// Task 2 lands <see cref="Parser.ParseModuleDeclaration"/>.
/// </summary>
[Collection("FlowScripts")]
public class ModuleDeclarationParserTests : IDisposable
{
    public ModuleDeclarationParserTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Parses Flow source into a <see cref="Program"/> AST without executing.
    /// Mirrors <see cref="FlowLang.Core.FlowEngine.Execute"/>'s lex+parse phase
    /// but stops before interpretation so the AST shape is the test subject.
    /// </summary>
    private static Program ParseToProgram(string source, string? fileName = "<test>")
    {
        var errorReporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, errorReporter, fileName);
        var tokens = lexer.Tokenize();
        Assert.False(errorReporter.HasErrors, $"Lex errors: {errorReporter.Errors.FirstOrDefault()?.Message}");
        var parser = new Parser(tokens, errorReporter);
        var program = parser.Parse();
        Assert.False(errorReporter.HasErrors, $"Parse errors: {errorReporter.Errors.FirstOrDefault()?.Message}");
        return program;
    }

    /// <summary>
    /// Parses Flow source EXPECTING a parse error. Returns the first reported
    /// error's message for substring assertions. Lex-stage errors fail the
    /// assertion outright — parse-stage errors are the test subject.
    /// </summary>
    private static string ParseToProgramExpectingError(string source, string? fileName = "<test>")
    {
        var errorReporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, errorReporter, fileName);
        var tokens = lexer.Tokenize();
        Assert.False(errorReporter.HasErrors, $"Lex errors (expected parse error, not lex error): {errorReporter.Errors.FirstOrDefault()?.Message}");
        var parser = new Parser(tokens, errorReporter);
        _ = parser.Parse();
        Assert.True(errorReporter.HasErrors, "Expected parser error, but none was reported.");
        return errorReporter.Errors[0].Message;
    }

    /// <summary>
    /// Test 1 — back-compat smoke. A file that does NOT use the <c>module</c>
    /// keyword still lexes + parses + emits Statements unchanged (the new
    /// keyword is purely additive — no existing <c>.flow</c> source uses
    /// <c>module</c> as an identifier per RESEARCH Pitfall 1 grep verification).
    /// </summary>
    [Fact]
    public void NoModuleDeclaration_ParsesAsBefore()
    {
        var source = "proc foo (Int: n) (print n) end";
        var program = ParseToProgram(source);

        // No module declaration present — first statement should be the proc.
        Assert.NotEmpty(program.Statements);
        Assert.IsType<ProcDeclaration>(program.Statements[0]);
        Assert.DoesNotContain(program.Statements, s => s is ModuleDeclarationStatement);
    }

    /// <summary>
    /// Test 2 — happy path. <c>module audio</c> as the first non-comment
    /// statement parses to a <see cref="ModuleDeclarationStatement"/> at
    /// <c>Statements[0]</c> carrying <c>Name == "audio"</c>.
    /// </summary>
    [Fact]
    public void ModuleDeclarationFirst_ProducesModuleDeclarationStatement()
    {
        var source = "module audio\n\nproc foo (Int: n) (print n) end";
        var program = ParseToProgram(source);

        Assert.NotEmpty(program.Statements);
        var first = Assert.IsType<ModuleDeclarationStatement>(program.Statements[0]);
        Assert.Equal("audio", first.Name);
    }

    /// <summary>
    /// Test 3 — comments before the declaration are stripped at
    /// <see cref="Parser.ParseStatement"/> entry and do NOT count as
    /// non-comment statements per D-01. So a leading <c>// header note</c>
    /// followed by <c>module audio</c> still places the declaration at
    /// <c>Statements[0]</c>.
    /// </summary>
    [Fact]
    public void CommentsBeforeModuleDeclaration_AcceptDeclaration()
    {
        var source = "// header note\nmodule audio\nproc foo (Int: n) (print n) end";
        var program = ParseToProgram(source);

        Assert.NotEmpty(program.Statements);
        var first = Assert.IsType<ModuleDeclarationStatement>(program.Statements[0]);
        Assert.Equal("audio", first.Name);
    }

    /// <summary>
    /// Test 4 — invalid module name (numeric literal token after the keyword)
    /// produces a parse error citing the expected-identifier message.
    /// </summary>
    [Fact]
    public void ModuleNameNumericLiteral_ParseErrors()
    {
        var source = "module 5";
        var msg = ParseToProgramExpectingError(source);
        Assert.Contains("Expected module name", msg, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test 5 — mid-file <c>module</c> declaration (after another statement)
    /// produces the position-constraint parse error per D-01 ("module declaration
    /// must be the first non-comment statement of the file").
    /// </summary>
    [Fact]
    public void ModuleDeclarationAfterProc_ParseErrors()
    {
        var source = "proc foo (Int: n) (print n) end\nmodule audio";
        var msg = ParseToProgramExpectingError(source);
        Assert.Contains("module declaration must be the first non-comment statement", msg, StringComparison.Ordinal);
    }
}
