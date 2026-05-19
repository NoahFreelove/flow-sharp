using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-06 Wave 0 — D-v1.5-05 charitable non-exhaustive policy
/// (default — no <c>enable matchExhaustive;</c> pragma).
///
/// Pins:
///   1. Non-exhaustive match emits a stderr WARN containing "non-exhaustive".
///   2. The match expression returns <see cref="Value.Void"/> (charitable
///      fall-through), and the engine does NOT mark this as an error.
///   3. Repeated evaluation of the SAME match Span emits the warning ONCE
///      (RenderingDiagnostics.WarnOnce per-sentinel dedup).
///
/// RED state: Plan 35-05's EvaluateMatch silently returns Value.Void(). Task 4
/// adds the WARN-vs-error policy at the marker comment.
/// </summary>
[Collection("RenderingDiagnostics")]
public class MatchExhaustivenessDefaultTests
{
    private static (string stderr, Value? result, bool hasErrors) RunWithStderrCapture(string source)
    {
        RenderingDiagnostics.ResetForTesting();

        var originalStderr = System.Console.Error;
        using var sw = new StringWriter();
        System.Console.SetError(sw);
        try
        {
            using var engine = new FlowEngine(verbose: false);
            var v = engine.ExecuteScriptAndGetResult(source);
            return (sw.ToString(), v, engine.ErrorReporter.HasErrors);
        }
        finally
        {
            System.Console.SetError(originalStderr);
        }
    }

    [Fact]
    public void NonExhaustiveDefaultWarnsAndReturnsVoid()
    {
        var src = "(match 5 | 1 => \"one\" | 2 => \"two\")";
        var (stderr, result, hasErrors) = RunWithStderrCapture(src);

        // Result is Void (charitable fall-through, NOT an error).
        Assert.NotNull(result);
        Assert.Equal(TypeSystem.PrimitiveTypes.VoidType.Instance, result!.Type);

        // ErrorReporter must NOT carry an error — strict mode is opt-in.
        Assert.False(hasErrors, $"Default non-exhaustive should warn, not error. Stderr: {stderr}");

        // Stderr must contain the warning advisory.
        Assert.Contains("non-exhaustive", stderr);
        Assert.Contains("warning", stderr, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WarnDedupedPerMatchSpan()
    {
        // Three evaluations of the SAME match expression in the same process.
        // Per Pitfall 5 / RenderingDiagnostics.WarnOnce, the warning emits ONCE
        // per (sentinel-key = match Span) per process. The dedup state must be
        // cleared at the start so we observe a single emit, not zero.
        RenderingDiagnostics.ResetForTesting();

        var originalStderr = System.Console.Error;
        using var sw = new StringWriter();
        System.Console.SetError(sw);
        try
        {
            // Run the SAME source 3 times within one stderr-capture window.
            // Each FlowEngine instance has independent state EXCEPT for
            // RenderingDiagnostics, which is process-global and key-deduped.
            for (int i = 0; i < 3; i++)
            {
                using var engine = new FlowEngine(verbose: false);
                engine.ExecuteScriptAndGetResult("(match 5 | 1 => \"one\" | 2 => \"two\")");
            }
        }
        finally
        {
            System.Console.SetError(originalStderr);
        }

        var stderr = sw.ToString();

        // Count how many "non-exhaustive" lines we have.
        var count = 0;
        var idx = 0;
        while ((idx = stderr.IndexOf("non-exhaustive", idx, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += "non-exhaustive".Length;
        }

        Assert.Equal(1, count);
    }
}
