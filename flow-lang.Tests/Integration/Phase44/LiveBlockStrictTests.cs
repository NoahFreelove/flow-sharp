using System;
using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-10 Task 2 (D-15) — Facts pinning that strict mode
/// applies INSIDE <c>live { }</c> blocks when the enclosing file declares
/// <c>enable strict;</c>:
///
/// <list type="bullet">
///   <item>Initial file load: parse + Axis A dispatch + Axis B/C checks
///   inside the live block body run strict per Plan 44-01's
///   <c>ApplyStrictPragma</c> + the Plan 44-02 OverloadResolver tier
///   filter.</item>
///   <item>Live-reload re-eval also applies strict checks to the new body
///   automatically — RESEARCH Pattern 7 confirms
///   <see cref="FlowInterpreter.LiveReloadManager"/>'s
///   <c>RenderScript</c> constructs a fresh <see cref="FlowEngine"/> and
///   calls <c>engine.Execute(source)</c> which re-runs PragmaScanner +
///   ApplyStrictPragma per reload. Zero new plumbing in
///   LiveReloadManager.</item>
///   <item>D-15 carve-out: the <c>[live] entering live block</c> stderr
///   advisory stays charitable regardless of strict mode — Plan 44-07's
///   CarveOutsPreservedTests pinned this design lock (D-v1.5-07 — live
///   sessions must never die mid-set, so the entry advisory is
///   intentionally not promoted to a strict error).</item>
/// </list>
///
/// <para>
/// The Facts here exercise the strict-aware BEHAVIOR at the parse +
/// dispatch level by calling <see cref="FlowEngine.Execute"/> directly on
/// authored .flow source strings. The file-watch event handling itself is
/// covered by the Phase 38 LIVE-02 test suite; Plan 44-10 makes ZERO
/// functional changes to LiveReloadManager — strict re-application is
/// automatic via Pattern 7 (fresh-engine ApplyStrictPragma per reload).
/// Together, the Phase 38 fixtures + the Phase 44 Facts cover the full
/// path without duplicating the Phase 38 file-watch test surface.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class LiveBlockStrictTests : IDisposable
{
    private readonly TextWriter _originalError;
    private readonly StringWriter _capturedError;

    public LiveBlockStrictTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _originalError = Console.Error;
        _capturedError = new StringWriter();
        Console.SetError(_capturedError);
    }

    public void Dispose()
    {
        Console.SetError(_originalError);
        _capturedError.Dispose();
        RenderingDiagnostics.ResetForTesting();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 1: a strict file with `live 1bar { (print 1) }` — the body's
    //         (print 1) Int-not-String call surfaces the canonical
    //         [strict] error via ErrorReporter.
    //         Pattern 7 verification: ApplyStrictPragma fires once at
    //         engine.Execute, sets StrictMode=true, and the live block
    //         body executes strict.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_StrictFileLiveBlockBody_RunsStrict()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nlive 1bar { (print 1) }\n");

        Assert.False(ok, "expected strict-mode error from (print 1) inside live block");
        Assert.True(errorCount >= 1, $"expected at least one strict error; got {errorCount}");
        Assert.Contains("[strict] (print) requires String — got Int", stderr);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 2: a strict file with `live 1bar { (print "ok") }` — the
    //         String-overload matches; no strict error fires regardless of
    //         the live block wrapper.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_StrictFileLiveBlockBody_NoErrorOnValidString()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nlive 1bar { (print \"ok\") }\n");

        Assert.True(ok,
            $"expected (print \"ok\") inside strict live block to succeed; stderr: {stderr}");
        Assert.Equal(0, errorCount);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 3: a non-strict file with `live 1bar { (print 1) }` — Plan 44-08
    //         charitable auto-str fires; "1" appears in captured stdout.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_NonStrictFileLiveBlockBody_AutoStrs()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, _) = runner.RunSource(
            "live 1bar { (print 1) }\n");

        Assert.True(ok,
            $"expected non-strict live block (print 1) to auto-str to '1'; stderr: {stderr}");
        Assert.Contains("1", stdout);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 4: D-15 carve-out — the `[live] entering live block` advisory
    //         STILL fires in strict files. The advisory is a
    //         determinism-opt-out warning (D-v1.5-07); strict mode does NOT
    //         elevate it to an error (carve-out preserved by Plan 44-07's
    //         CarveOutsPreservedTests at the WarnOnce site).
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_LiveEntryAdvisoryStillCharitableInStrict()
    {
        using var runner = new FlowEngineRunner();
        // Use a benign body so no [strict] error competes with the
        // [live] advisory assertion.
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nlive 1bar { (print \"ok\") }\n");

        Assert.True(ok,
            $"expected strict + (print \"ok\") body to succeed; errors: {errorCount}, stderr: {stderr}");
        // FlowEngineRunner sets Console.Error → an internal StringWriter;
        // RenderingDiagnostics.WarnOnce writes via Console.Error.WriteLine
        // (verified at flow-lang/Interpreter/Interpreter.cs:476-478).
        Assert.Contains("[live] entering live block", stderr);
        Assert.Contains("opts OUT of two-run cmp-clean determinism", stderr);
        // Carve-out check: no [strict] error fires on the live-entry itself.
        Assert.DoesNotContain("[strict] [live]", stderr);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 5: simulate Phase 38 LIVE-02 reload sequence — first load is
    //         non-strict with `live 1bar { (print 1) }` (charitable auto-str
    //         fires); composer edits file to ADD `enable strict;`. On the
    //         second engine.Execute (fresh-engine path mirrors Pattern 7),
    //         the body now runs strict and the canonical error fires.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_LiveReloadAddStrictPragma_BodyRerunStrict()
    {
        // First load — non-strict.
        using (var runnerA = new FlowEngineRunner())
        {
            var (okA, stdoutA, _, _) = runnerA.RunSource(
                "live 1bar { (print 1) }\n");
            Assert.True(okA, "precondition: non-strict live block runs cleanly");
            Assert.Contains("1", stdoutA);
        }

        // Reset the dedup sentinel so a second engine's [live] advisory can
        // observe the determinism warning if it fires on the same line.
        RenderingDiagnostics.ResetForTesting();

        // Second load — composer added `enable strict;`. Fresh engine
        // mirrors LiveReloadManager.RenderScript's per-reload behavior.
        using (var runnerB = new FlowEngineRunner())
        {
            var (okB, _, stderrB, errorCountB) = runnerB.RunSource(
                "enable strict;\nlive 1bar { (print 1) }\n");
            Assert.False(okB, "expected reload-with-pragma to surface strict error");
            Assert.True(errorCountB >= 1,
                $"expected at least one strict error post-reload; got {errorCountB}");
            Assert.Contains("[strict] (print) requires String — got Int", stderrB);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fact 6: Phase 38 LiveBlockDeterminismAdvisory regression smoke — pin
    //         that the existing non-strict live entry advisory wording is
    //         preserved when Plan 44-10 is in effect (i.e., strict-aware
    //         changes did not alter the Phase 38 advisory).
    //         W12 note (plan): full Phase 38 file-watch path is covered by
    //         Phase 38 LIVE-02 fixtures; this Fact only pins the advisory
    //         wording at the FlowEngine.Execute boundary so a Phase 44
    //         regression cannot silently break the Phase 38 contract.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fact_BackCompat_LiveEntryAdvisoryWording_Phase38Contract()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "live 1bar { (print \"ok\") }\n");

        Assert.True(ok, $"expected non-strict live block to run cleanly; stderr: {stderr}");
        // Phase 38 D-v1.5-07 wording — identical to LiveBlockDeterminismAdvisoryTests
        // at Phase38/LiveBlockDeterminismAdvisoryTests.cs lines 52-54.
        Assert.Contains("[live] entering live block at line", stderr);
        Assert.Contains("opts OUT of two-run cmp-clean determinism", stderr);
    }
}
