using System;
using FlowLang.Core;

namespace FlowLang.StandardLibrary.TestFramework;

/// <summary>
/// Phase 35 Plan 35-04 TEST-01 + TEST-02 — orchestrates the run of every
/// <see cref="TestRecord"/> on a <see cref="FlowEngine"/>'s ExecutionContext.
/// Each test runs inside a SnapshotState/RestoreState guard so the 11+
/// state-mutable surfaces enumerated in RESEARCH §Pitfall 3 do not leak
/// between tests.
///
/// <para>
/// Output format per RESEARCH §C.5:
///   <c>  PASS  {file}::{name}</c>
///   <c>  FAIL  {file}::{name}: {AssertionException.Message}</c> (red on TTY)
/// followed by a single summary line
///   <c>Total: N; Passed: P; Failed: F</c>.
/// </para>
///
/// <para>
/// Caller decides what to do with the (passed, failed) tuple — the CLI
/// surface (<c>flow test [path]</c>, lands in Task 3) returns
/// <c>failed == 0 ? 0 : 1</c> as the process exit code.
/// </para>
/// </summary>
public class TestRunner
{
    /// <summary>
    /// Runs every test registered on <paramref name="engine"/>'s
    /// <c>Context.TestRegistry</c>, returning the (passed, failed) totals.
    /// <paramref name="filePath"/> is used in the PASS/FAIL output prefix
    /// so the composer can identify which file a failing test came from
    /// when multiple .flow files share a name like "smoke test".
    /// </summary>
    public (int passed, int failed) Run(FlowEngine engine, string filePath)
    {
        if (engine is null) throw new ArgumentNullException(nameof(engine));
        if (filePath is null) throw new ArgumentNullException(nameof(filePath));

        int passed = 0, failed = 0;
        foreach (var test in engine.Context.TestRegistry)
        {
            var snapshot = engine.Context.SnapshotState();
            try
            {
                test.BodyThunk.Force();
                Console.WriteLine($"  PASS  {filePath}::{test.Name}");
                passed++;
            }
            catch (AssertionException ex)
            {
                EmitFailLine(filePath, test.Name, ex.Message);
                failed++;
            }
            catch (Exception ex)
            {
                // Non-assertion exception bubbling out of the body is a FAIL
                // with the exception type included for diagnosability —
                // distinguishes "the assertion failed" from "the body itself
                // crashed" in the FAIL line. The body wrapper exception path
                // is rare (most bodies should propagate AssertionException
                // via the assertion primitives) but we never want a thrown
                // NullReferenceException to abort the whole run.
                EmitFailLine(filePath, test.Name, $"{ex.GetType().Name}: {ex.Message}");
                failed++;
            }
            finally
            {
                engine.Context.RestoreState(snapshot);
            }
        }
        return (passed, failed);
    }

    private static void EmitFailLine(string filePath, string testName, string message)
    {
        // TTY color per CLAUDE.md precedent (CheckCommand prints red on
        // failure via Console.ForegroundColor). Safe on non-TTY: the color
        // codes are swallowed when stdout is not a terminal.
        bool useColor = !Console.IsOutputRedirected;
        if (useColor) Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  FAIL  {filePath}::{testName}: {message}");
        if (useColor) Console.ResetColor();
    }
}
