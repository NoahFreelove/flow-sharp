using System;
using System.IO;
using System.Reflection;
using FlowInterpreter;
using FlowLang.Core;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-10 Task 1 (D-16) — Facts pinning the REPL strict
/// meta-command surface and the sticky <c>_sessionStrict</c> session flag:
///
/// <list type="bullet">
///   <item><c>:strict on</c> / <c>:strict off</c> meta-commands flip
///   <c>_sessionStrict</c> AND mutate <c>engine.Context.StrictMode</c>
///   immediately (no wait for next Execute).</item>
///   <item>Per-line input inherits the sticky flag — every
///   <c>engine.Execute(line, fileName)</c> call sets
///   <c>context.StrictMode</c> from <c>_sessionStrict</c> BEFORE evaluating
///   the line.</item>
///   <item>Typing <c>enable strict;</c> at the REPL flips the sticky flag
///   too — after Execute returns, the per-line PragmaScanner has flipped
///   the context's bit; the REPL syncs the session flag from that bit
///   (RESEARCH §Pattern 8 sticky-flag-from-pragma sync requirement).</item>
///   <item><c>:strikt on</c> (typo) falls through to UnknownCommand
///   without mutating session state.</item>
/// </list>
///
/// <para>
/// The Facts here reach into Repl.Run's per-line loop ONLY via
/// <see cref="Repl.HandleCommandForTesting"/> (the existing test seam
/// landed in Phase 38 Plan 38-04 for ReplHelpMetaCommandTests) plus
/// reflection on the new private <c>_sessionStrict</c> field. Production
/// callers go through <c>Repl.Run</c>; tests bypass the Console.ReadLine
/// loop because xUnit cannot drive an interactive prompt deterministically.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class ReplStrictMetaCommandTests : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly StringWriter _capturedOut;

    public ReplStrictMetaCommandTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _originalOut = Console.Out;
        _capturedOut = new StringWriter();
        Console.SetOut(_capturedOut);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        _capturedOut.Dispose();
        RenderingDiagnostics.ResetForTesting();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Reflection helpers — reach the private state laid down by Plan 44-10:
    //   * `_sessionStrict` (bool) — the sticky session flag.
    //   * `_engine` (FlowEngine)  — the per-REPL engine instance (already
    //     present pre-44-10; we read it to assert context.StrictMode flips
    //     in tandem with _sessionStrict).
    // ────────────────────────────────────────────────────────────────────────

    private static bool GetSessionStrict(Repl repl)
    {
        var field = typeof(Repl).GetField(
            "_sessionStrict",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return (bool)field!.GetValue(repl)!;
    }

    private static FlowEngine GetEngine(Repl repl)
    {
        var field = typeof(Repl).GetField(
            "_engine",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return (FlowEngine)field!.GetValue(repl)!;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 1: `:strict on` flips both the session flag AND the engine context.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_StrictOn_SetsSessionFlagAndContextStrictMode()
    {
        var repl = new Repl();
        Assert.False(GetSessionStrict(repl), "precondition: _sessionStrict starts false");
        Assert.False(GetEngine(repl).Context.StrictMode,
            "precondition: engine.Context.StrictMode starts false");

        var keepGoing = repl.HandleCommandForTesting(":strict on");

        Assert.True(keepGoing, ":strict on must keep the REPL alive (returns true)");
        Assert.True(GetSessionStrict(repl), "expected _sessionStrict to flip to true");
        Assert.True(GetEngine(repl).Context.StrictMode,
            "expected engine.Context.StrictMode to flip to true immediately");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 2: `:strict off` flips back.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_StrictOff_FlipsBack()
    {
        var repl = new Repl();
        repl.HandleCommandForTesting(":strict on");
        Assert.True(GetSessionStrict(repl), "precondition: _sessionStrict is true after :strict on");

        var keepGoing = repl.HandleCommandForTesting(":strict off");

        Assert.True(keepGoing, ":strict off must keep the REPL alive (returns true)");
        Assert.False(GetSessionStrict(repl), "expected _sessionStrict to flip back to false");
        Assert.False(GetEngine(repl).Context.StrictMode,
            "expected engine.Context.StrictMode to flip back to false");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 3: with sticky=on, (print 42) on the next line runs strict and
    //         the Plan 44-08 strict error fires.
    //         Exercises the per-line StrictMode-sync site Plan 44-10 inserts
    //         around the existing _engine.ExecuteScriptAndGetResult call.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_StrictOnFollowedByPrintInt_ReportsStrictError()
    {
        var repl = new Repl();
        repl.HandleCommandForTesting(":strict on");

        var engine = GetEngine(repl);
        engine.ErrorReporter.Clear();
        // Drive the production per-line sandwich via the Plan 44-10 test seam.
        var ok = repl.ExecuteLineForTesting("(print 42)");

        Assert.False(ok, "expected per-line execution to fail under sticky strict");
        Assert.True(engine.ErrorReporter.HasErrors,
            "expected strict-mode error from (print 42) when _sessionStrict=true");
        var msg = engine.ErrorReporter.FormatErrors();
        Assert.Contains("[strict] (print) requires String — got Int", msg);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 4: with sticky=off, (print 42) runs charitable and auto-strs to "42".
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_StrictOffFollowedByPrintInt_AutoStrs()
    {
        var repl = new Repl();
        // Default _sessionStrict=false; no toggle.

        var engine = GetEngine(repl);
        engine.ErrorReporter.Clear();
        var preLen = _capturedOut.GetStringBuilder().Length;
        // Drive the production per-line sandwich via the Plan 44-10 test seam.
        var ok = repl.ExecuteLineForTesting("(print 42)");

        Assert.True(ok, "expected (print 42) to succeed in non-strict via auto-str");
        Assert.False(engine.ErrorReporter.HasErrors,
            "expected zero errors when _sessionStrict=false");
        var newOut = _capturedOut.GetStringBuilder().ToString().Substring(preLen);
        Assert.Contains("42", newOut);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 5: typing `enable strict;` at the REPL flips the sticky flag too.
    //         The per-line PragmaScanner observes the pragma, FlowEngine
    //         flips context.StrictMode=true, and the REPL sync line copies
    //         that back into _sessionStrict (RESEARCH §Pattern 8 sticky-from-
    //         pragma sync — symmetric with the pre-Execute sync direction).
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_EnableStrictAtPrompt_FlipsSessionFlag()
    {
        var repl = new Repl();
        Assert.False(GetSessionStrict(repl), "precondition: _sessionStrict starts false");

        var engine = GetEngine(repl);
        engine.ErrorReporter.Clear();
        // Drive the production per-line sandwich via the Plan 44-10 test seam.
        // The seam runs the source through Execute (which triggers
        // PragmaScanner → ApplyStrictPragma → context.StrictMode=true) and
        // then performs the symmetric sticky-from-pragma sync.
        var ok = repl.ExecuteLineForTesting("enable strict;");

        Assert.True(ok, $"expected `enable strict;` to execute cleanly; errors: {engine.ErrorReporter.FormatErrors()}");
        Assert.True(engine.Context.StrictMode,
            "expected context.StrictMode=true after `enable strict;` per Plan 44-01 ApplyStrictPragma");
        Assert.True(GetSessionStrict(repl),
            "expected `enable strict;` typed at REPL to flip _sessionStrict via Plan 44-10 sync");

        // Stickiness check: the NEXT line (no pragma) must still run strict.
        engine.ErrorReporter.Clear();
        var ok2 = repl.ExecuteLineForTesting("(print 42)");
        Assert.False(ok2, "expected sticky strict to propagate into the next line");
        Assert.Contains(
            "[strict] (print) requires String — got Int",
            engine.ErrorReporter.FormatErrors());
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 6: `:strikt on` (typo) falls through to UnknownCommand without
    //         mutating _sessionStrict.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_StrictTypo_FallsThroughUnknownCommand()
    {
        var repl = new Repl();
        Assert.False(GetSessionStrict(repl), "precondition: _sessionStrict=false");

        var keepGoing = repl.HandleCommandForTesting(":strikt on");

        // UnknownCommand prints + returns true to keep REPL alive.
        Assert.True(keepGoing, ":strikt on must be consumed as unknown + return true");
        Assert.False(GetSessionStrict(repl),
            "_sessionStrict must remain false after typo command");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 7: `:strict on` prints exactly `[strict] on` to stdout.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_StrictOn_StatusOutputMatches()
    {
        var repl = new Repl();
        var preLen = _capturedOut.GetStringBuilder().Length;

        repl.HandleCommandForTesting(":strict on");

        var newOut = _capturedOut.GetStringBuilder().ToString().Substring(preLen);
        Assert.Contains("[strict] on", newOut);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 8: HandleCommandForTesting is callable from xUnit (test seam).
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_HandleCommandForTesting_Accessible()
    {
        var repl = new Repl();
        // Method exists with the expected signature — fails to compile if
        // Plan 44-10 accidentally hides the seam.
        bool result = repl.HandleCommandForTesting(":quit");
        Assert.False(result, ":quit must return false to signal REPL exit");
    }
}
