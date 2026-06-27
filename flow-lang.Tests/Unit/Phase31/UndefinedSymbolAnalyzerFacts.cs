using System.Linq;
using FlowLang.Tests.Unit.Phase17;
using FlowLsp.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase31;

/// <summary>
/// Phase 31 Plan 31-08 (SPEC-1 follow-up) — UndefinedSymbolAnalyzer pins
/// the "missing imports" diagnostic surface that <see cref="UnusedImportAnalyzer"/>
/// can't catch.
///
/// Algorithm pinned here:
///   - `(arpeggio C E G)` without `use "@std"` → flagged
///   - `(arpeggio C E G)` WITH `use "@std"` → not flagged
///   - Files with non-`@` imports (relative user modules) → analyzer skips
///   - Roman numerals (I, IV, V, vi, ...) → never flagged (note-stream surface)
///   - User-declared procs → never flagged
///   - Lambda parameters → never flagged inside the lambda body
///   - Charitable fail-open on malformed input
/// </summary>
public class UndefinedSymbolAnalyzerFacts
{
    private static IReadOnlyList<Diagnostic> Analyze(string source)
    {
        var result = LspFixtures.Parse(source);
        return UndefinedSymbolAnalyzer.Analyze(
            result.Ast,
            result.Tokens,
            source,
            LspFixtures.StdlibIndex());
    }

    [Fact]
    public void Arpeggio_WithoutStdImport_Flagged()
    {
        var src = "(arpeggio C4 E4 G4)\n";
        var diags = Analyze(src);
        Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Warning, diags[0].Severity);
        Assert.Equal("flow.undefinedSymbol", diags[0].Source);
        Assert.Contains("arpeggio", diags[0].Message);
        Assert.Contains("@std", diags[0].Message); // helpful hint
    }

    [Fact]
    public void Arpeggio_WithStdImport_NotFlagged()
    {
        // @std re-exports arpeggio via `internal proc arpeggio (...)` in
        // std.flow. With the import, the call must not flag.
        var src = "use \"@std\"\n(arpeggio C4 E4 G4)\n";
        var diags = Analyze(src);
        Assert.DoesNotContain(diags, d => d.Source == "flow.undefinedSymbol");
    }

    [Fact]
    public void NonAtImport_SkipsAnalyzer()
    {
        // Files with relative-path imports (user modules) bypass the check
        // entirely — we can't resolve their exports.
        var src = "use \"./helpers.flow\"\n(myHelper 1 2 3)\n";
        var diags = Analyze(src);
        Assert.DoesNotContain(diags, d => d.Source == "flow.undefinedSymbol");
    }

    [Fact]
    public void RomanNumeral_InCallPosition_NeverFlagged()
    {
        // Roman numerals resolve via musical key context, not symbol lookup.
        // Flagging them would be noise.
        var src = "(I)\n(IV)\n(V7)\n(vi)\n";
        var diags = Analyze(src);
        // V7 isn't a pure roman numeral (has the 7 suffix) so it may flag;
        // but I, IV, vi are stock roman numerals and must not flag.
        Assert.DoesNotContain(diags, d => d.Source == "flow.undefinedSymbol" && d.Message.Contains("'I'"));
        Assert.DoesNotContain(diags, d => d.Source == "flow.undefinedSymbol" && d.Message.Contains("'IV'"));
        Assert.DoesNotContain(diags, d => d.Source == "flow.undefinedSymbol" && d.Message.Contains("'vi'"));
    }

    [Fact]
    public void UserDeclaredProc_CalledByName_NotFlagged()
    {
        var src = @"
proc demo (Int x) -> Int
    return x
end proc

(demo 5)
";
        var diags = Analyze(src);
        Assert.DoesNotContain(diags, d => d.Source == "flow.undefinedSymbol");
    }

    [Fact]
    public void UserVariable_CalledAsLambda_NotFlagged()
    {
        // A user-declared variable holding a callable should not flag at the
        // call site (conservative: we trust the binding). Use `use "@std"` to
        // make `add` resolvable inside the lambda body — we're asserting that
        // `f` specifically doesn't flag, not that the file is fully clean.
        var src = @"use ""@std""

Function f = fn Int x => (add x 1)
(f 5)
";
        var diags = Analyze(src);
        Assert.DoesNotContain(diags, d =>
            d.Source == "flow.undefinedSymbol" && d.Message.Contains("'f'"));
    }

    [Fact]
    public void LambdaParameter_UsedAsFunctionInBody_NotFlagged()
    {
        // Lambda parameters are in scope inside the body. The analyzer
        // collects lambda parameters into the universe so `(f x)` inside the
        // lambda body doesn't flag.
        var src = @"
Function callTwice = fn Function f, Int x => (f (f x))
";
        var diags = Analyze(src);
        Assert.DoesNotContain(diags, d => d.Source == "flow.undefinedSymbol");
    }

    [Fact]
    public void Print_WithoutStdImport_Flagged()
    {
        // `print` is declared in std.flow as `internal proc print (String: s)`
        // — Flow requires `use "@std"` to make it visible. The analyzer flags
        // it (mirroring the runtime "Function 'print' not found" error).
        var src = "(print \"hi\")\n";
        var diags = Analyze(src);
        var printDiag = diags.FirstOrDefault(d =>
            d.Source == "flow.undefinedSymbol" && d.Message.Contains("'print'"));
        Assert.NotNull(printDiag);
        Assert.Contains("@std", printDiag.Message);
    }

    [Fact]
    public void Print_WithStdImport_NotFlagged()
    {
        var src = "use \"@std\"\n(print \"hi\")\n";
        var diags = Analyze(src);
        Assert.DoesNotContain(diags, d => d.Source == "flow.undefinedSymbol");
    }

    [Fact]
    public void UnknownIdentifier_NotInAnyModule_GetsGenericMessage()
    {
        // A name that doesn't exist anywhere gets the generic "unknown
        // identifier" message (no `Did you forget` suggestion).
        var src = "(xyzNotAThing 1 2 3)\n";
        var diags = Analyze(src);
        Assert.Single(diags);
        Assert.Equal("flow.undefinedSymbol", diags[0].Source);
        Assert.Contains("xyzNotAThing", diags[0].Message);
        Assert.DoesNotContain("Did you forget", diags[0].Message);
    }

    [Fact]
    public void EmptyProgram_ReturnsEmpty()
    {
        var diags = Analyze("");
        Assert.Empty(diags);
    }
}
