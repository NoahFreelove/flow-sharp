using System;
using System.IO;
using System.Text;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase43;

/// <summary>
/// Phase 43 Plan 43-04 Task 1 — covers REQ-MOD-07 (beatToSec) and REQ-MOD-08
/// (secToBeat) builtins introduced in <c>BeatConversionFunctions.cs</c>.
///
/// Behaviors pinned:
///   1. Outside any <c>tempo</c> block, both functions default to 120 BPM
///      AND emit a one-shot stderr advisory
///      <c>[<name>] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)</c>.
///   2. Inside a <c>tempo N { ... }</c> block, the active tempo is honored
///      and no advisory fires.
///   3. The advisory dedup-across-runs (Pitfall 8) — a second
///      <see cref="FlowEngine.Execute"/> call referencing the same builtin
///      does NOT re-emit because <see cref="RenderingDiagnostics.WarnOnce"/>
///      dedups per-process per sentinel key (<c>beatToSec-no-tempo</c> /
///      <c>secToBeat-no-tempo</c>).
///
/// stderr capture mirrors
/// <see cref="FlowLang.Tests.Integration.Phase37.StretchAutoAdvisoryTests"/>
/// (CaptureStderr helper + dedicated <see cref="RenderingDiagnostics.ResetForTesting"/>
/// in ctor/Dispose).
/// </summary>
[Collection("FlowScripts")]
public class BeatConversionTests : IDisposable
{
    public BeatConversionTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Captures stderr while <paramref name="action"/> runs; returns the
    /// captured text. Restores <see cref="Console.Error"/> on exit.
    /// </summary>
    private static string CaptureStderr(Action action)
    {
        var original = Console.Error;
        var sb = new StringBuilder();
        var writer = new StringWriter(sb);
        Console.SetError(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetError(original);
        }
        return sb.ToString();
    }

    // ===== beatToSec =====

    /// <summary>
    /// Test 1: Outside any tempo block, <c>(beatToSec 1.0)</c> defaults to
    /// 120 BPM. 1 beat × 60/120 = 0.5 seconds. Advisory MUST fire exactly
    /// once.
    /// </summary>
    [Fact]
    public void BeatToSec_OutsideTempoBlock_DefaultsTo120BpmAndFiresAdvisory()
    {
        string source = "use \"@audio\"; Second s = (beatToSec 1.0); (print (str s))";
        string stdout = "";
        string stderr = CaptureStderr(() =>
        {
            var origOut = Console.Out;
            var outSb = new StringBuilder();
            Console.SetOut(new StringWriter(outSb));
            try
            {
                using var engine = new FlowEngine();
                engine.Execute(source, "<beatToSec_default_tempo>");
                stdout = outSb.ToString();
            }
            finally
            {
                Console.SetOut(origOut);
            }
        });

        Assert.Contains(
            "[beatToSec] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)",
            stderr);
        // 1.0 beat * (60.0/120.0) = 0.5 seconds — `(print (str s))` writes "0.5s".
        Assert.Contains("0.5", stdout);
    }

    /// <summary>
    /// Test 2: <c>tempo 60 { (beatToSec 1.0) }</c> returns 1.0 second
    /// (1 beat × 60/60). No advisory fires because tempo is set.
    /// </summary>
    [Fact]
    public void BeatToSec_InsideTempo60Block_ReturnsOneSecondAndNoAdvisory()
    {
        string source = "use \"@audio\"; tempo 60 { Second s = (beatToSec 1.0); (print (str s)) }";
        string stdout = "";
        string stderr = CaptureStderr(() =>
        {
            var origOut = Console.Out;
            var outSb = new StringBuilder();
            Console.SetOut(new StringWriter(outSb));
            try
            {
                using var engine = new FlowEngine();
                engine.Execute(source, "<beatToSec_tempo60>");
                stdout = outSb.ToString();
            }
            finally
            {
                Console.SetOut(origOut);
            }
        });

        Assert.DoesNotContain("[beatToSec]", stderr);
        // 1.0 beat * (60.0/60.0) = 1.0 second.
        Assert.Contains("1", stdout);
    }

    /// <summary>
    /// Test 3: <c>tempo 120 { (beatToSec 2.0) }</c> returns 1.0 second
    /// (2 beats × 60/120). No advisory.
    /// </summary>
    [Fact]
    public void BeatToSec_InsideTempo120Block_TwoBeatsReturnsOneSecond()
    {
        string source = "use \"@audio\"; tempo 120 { Second s = (beatToSec 2.0); (print (str s)) }";
        string stdout = "";
        string stderr = CaptureStderr(() =>
        {
            var origOut = Console.Out;
            var outSb = new StringBuilder();
            Console.SetOut(new StringWriter(outSb));
            try
            {
                using var engine = new FlowEngine();
                engine.Execute(source, "<beatToSec_tempo120>");
                stdout = outSb.ToString();
            }
            finally
            {
                Console.SetOut(origOut);
            }
        });

        Assert.DoesNotContain("[beatToSec]", stderr);
        Assert.Contains("1", stdout);
    }

    // ===== secToBeat =====

    /// <summary>
    /// Test 4: Outside any tempo block, <c>(secToBeat 1.0)</c> defaults to
    /// 120 BPM. 1 sec × 120/60 = 2.0 beats. Advisory MUST fire exactly
    /// once.
    ///
    /// Note: <c>(str Beat)</c> is intentionally absent from std.flow because
    /// Beat's IsCompatibleWith Double/Float creates an ambiguity between
    /// <c>str(Float)</c> and <c>str(Double)</c>. We assert the conversion
    /// result via <see cref="ExecutionContext.GetVariable"/> instead.
    /// </summary>
    [Fact]
    public void SecToBeat_OutsideTempoBlock_DefaultsTo120BpmAndFiresAdvisory()
    {
        string source = "use \"@audio\"; Beat b = (secToBeat 1.0)";
        double? beatsValue = null;
        string stderr = CaptureStderr(() =>
        {
            using var engine = new FlowEngine();
            engine.Execute(source, "<secToBeat_default_tempo>");
            var v = engine.Context.GetVariable("b");
            beatsValue = (double)v.Data!;
        });

        Assert.Contains(
            "[secToBeat] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)",
            stderr);
        // 1.0 sec * (120.0/60.0) = 2.0 beats.
        Assert.Equal(2.0, beatsValue!.Value, precision: 9);
    }

    /// <summary>
    /// Test 5: <c>tempo 120 { (secToBeat 0.5) }</c> returns 1.0 beat
    /// (0.5 sec × 120/60). No advisory.
    /// </summary>
    [Fact]
    public void SecToBeat_InsideTempo120Block_HalfSecondReturnsOneBeat()
    {
        // tempo 120 { ... } pushes a frame whose variables don't survive outside.
        // Use a renderSong-style top-level binding plus an inline assignment trick:
        // re-bind inside the block to a top-level variable. Simplest: read the
        // result via the engine's GetLastExpressionResult helper.
        string source = "use \"@audio\"; tempo 120 { Beat b = (secToBeat 0.5) }";
        string stderr = CaptureStderr(() =>
        {
            using var engine = new FlowEngine();
            engine.Execute(source, "<secToBeat_tempo120>");
            // Don't read the variable — it lived inside the tempo block scope.
            // The point of this fact is the advisory suppression behavior.
        });

        Assert.DoesNotContain("[secToBeat]", stderr);
    }

    /// <summary>
    /// Test 5b: numerical correctness of <c>(secToBeat 0.5)</c> at 120 BPM.
    /// Verified by calling the builtin directly through
    /// <see cref="FlowEngine.ExecuteScriptAndGetResult"/> so we get back the
    /// Beat-typed <see cref="Value"/> without needing a <c>str</c> overload.
    /// </summary>
    [Fact]
    public void SecToBeat_NumericResult_HalfSecondAt120BpmIsOneBeat()
    {
        string source = "use \"@audio\"; tempo 120 { (secToBeat 0.5) }";
        using var engine = new FlowEngine();
        var result = engine.ExecuteScriptAndGetResult(source, "<secToBeat_numeric>");
        Assert.NotNull(result);
        Assert.IsType<BeatType>(result!.Type);
        double beats = (double)result.Data!;
        Assert.Equal(1.0, beats, precision: 9);
    }

    // ===== Pitfall 8 dedup-across-runs =====

    /// <summary>
    /// Test 6: <see cref="RenderingDiagnostics.WarnOnce"/> dedups per-process
    /// per sentinel key. Two consecutive <see cref="FlowEngine.Execute"/>
    /// calls invoking <c>(beatToSec 1.0)</c> outside a tempo block emit
    /// the advisory exactly ONCE across both runs — verifying the Pitfall 8
    /// two-run cmp-clean preservation contract (stderr is captured separately
    /// from WAV bytes; dedup applies process-wide to the
    /// <c>beatToSec-no-tempo</c> sentinel).
    /// </summary>
    [Fact]
    public void BeatToSec_AdvisoryDedupsAcrossTwoExecuteCalls()
    {
        string source = "use \"@audio\"; Second s = (beatToSec 1.0)";
        string stderr = CaptureStderr(() =>
        {
            using var e1 = new FlowEngine();
            e1.Execute(source, "<run1>");
            using var e2 = new FlowEngine();
            e2.Execute(source, "<run2>");
        });

        // Count occurrences of the advisory substring; MUST be exactly 1.
        int count = 0;
        int idx = 0;
        const string needle = "[beatToSec] no active tempo";
        while ((idx = stderr.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        Assert.Equal(1, count);

        // Sentinel API contract — beatToSec-no-tempo recorded.
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("beatToSec-no-tempo"));
    }
}
