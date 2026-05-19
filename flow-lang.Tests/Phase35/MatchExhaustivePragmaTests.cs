using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-06 Wave 0 — D-v1.5-05 strict-mode policy via the
/// <c>enable matchExhaustive;</c> pragma.
///
/// Pins:
///   1. With <c>enable matchExhaustive;</c>, a non-exhaustive match is promoted
///      to a FlowDiagnostic at Level=Error in ErrorReporter; HasErrors is true.
///   2. The diagnostic message contains "non-exhaustive".
///   3. A wildcard arm <c>_ =&gt; ...</c> satisfies exhaustiveness under the
///      pragma — no error reported, normal arm-body result returned.
///
/// RED state: Plan 35-05's EvaluateMatch returns silent Void with no pragma
/// awareness. Task 4 wires the pragma lookup at the marker comment.
/// </summary>
[Collection("RenderingDiagnostics")]
public class MatchExhaustivePragmaTests
{
    [Fact]
    public void PragmaPromotesWarnToError()
    {
        RenderingDiagnostics.ResetForTesting();

        var src = "enable matchExhaustive;\n(match 5 | 1 => \"one\" | 2 => \"two\")";
        using var engine = new FlowEngine(verbose: false);
        // Use Execute directly because ExecuteScriptAndGetResult returns null
        // when Execute reports errors — and the very thing we're testing is
        // that the engine reports an error here.
        engine.Execute(src);

        Assert.True(engine.ErrorReporter.HasErrors,
            "matchExhaustive pragma must promote non-exhaustive match to error.");

        // At least one diagnostic must mention non-exhaustive.
        var found = false;
        foreach (var diag in engine.ErrorReporter.Diagnostics)
        {
            if (diag.Level == DiagnosticLevel.Error
                && diag.Message.Contains("non-exhaustive", System.StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }
        if (!found)
        {
            // Fallback path: legacy FlowError surface — also acceptable.
            foreach (var err in engine.ErrorReporter.Errors)
            {
                if (err.Message.Contains("non-exhaustive", System.StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
        }
        Assert.True(found,
            "Expected an error-level diagnostic mentioning 'non-exhaustive'. "
            + $"Diagnostics count={engine.ErrorReporter.Diagnostics.Count}, "
            + $"Errors count={engine.ErrorReporter.Errors.Count}");
    }

    [Fact]
    public void WildcardArmSatisfiesExhaustiveness()
    {
        RenderingDiagnostics.ResetForTesting();

        var src = "enable matchExhaustive;\n(match 5 | 1 => \"one\" | _ => \"other\")";
        using var engine = new FlowEngine(verbose: false);
        var result = engine.ExecuteScriptAndGetResult(src);

        // Wildcard handles the 5 case — arm body returns "other".
        Assert.NotNull(result);
        Assert.Equal("other", result!.As<string>());

        // No error: exhaustiveness satisfied by the _ arm.
        Assert.False(engine.ErrorReporter.HasErrors,
            "Wildcard arm should satisfy exhaustiveness under matchExhaustive pragma.");
    }
}
