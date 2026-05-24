using System;
using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-02 D-v1.5-07 — Wave 0 test asserting the
/// <c>[live] entering live block at line N — opts OUT of two-run cmp-clean
/// determinism</c> advisory fires exactly once per (line, process) via
/// <see cref="RenderingDiagnostics.WarnOnce"/> with sentinel
/// <c>live-determinism-optout:&lt;line&gt;</c>.
///
/// Captures <see cref="Console.Error"/> by swapping in a <see cref="StringWriter"/>
/// for the duration of the test, then restores. Re-running the same source in the
/// same process MUST NOT emit a second advisory — the dedup sentinel is per
/// (sentinel-key, process), and the test asserts that semantics.
/// </summary>
[Collection("FlowScripts")]
public class LiveBlockDeterminismAdvisoryTests : IDisposable
{
    private readonly TextWriter _originalError;
    private readonly StringWriter _capturedError;

    public LiveBlockDeterminismAdvisoryTests()
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

    [Fact]
    public void EnterLiveBlock_EmitsDV15_07AdvisoryOncePerLine()
    {
        var source = "live 1bar { (print \"hi\") }";

        using var engine = new FlowLang.Core.FlowEngine();
        var ok = engine.Execute(source, "<test>");
        Assert.True(ok, "FlowEngine.Execute should succeed");

        var stderr = _capturedError.ToString();
        const string expectedFragment = "[live] entering live block at line";
        const string expectedTail = "opts OUT of two-run cmp-clean determinism";

        int firstHit = stderr.IndexOf(expectedFragment, StringComparison.Ordinal);
        Assert.True(firstHit >= 0,
            $"Expected stderr to contain '{expectedFragment}'. Got:\n{stderr}");
        Assert.Contains(expectedTail, stderr);

        // Exactly one occurrence within the first FlowEngine.Execute.
        int secondHit = stderr.IndexOf(expectedFragment, firstHit + 1, StringComparison.Ordinal);
        Assert.True(secondHit < 0,
            $"Expected ONLY ONE '{expectedFragment}' advisory after first execute. Got:\n{stderr}");

        // Re-execute the SAME source in the SAME process. The WarnOnce sentinel
        // `live-determinism-optout:<line>` must dedup the advisory — the captured
        // stderr should STILL contain exactly one such advisory line.
        using var engine2 = new FlowLang.Core.FlowEngine();
        var ok2 = engine2.Execute(source, "<test>");
        Assert.True(ok2);

        stderr = _capturedError.ToString();
        int second = stderr.IndexOf(expectedFragment, firstHit + 1, StringComparison.Ordinal);
        Assert.True(second < 0,
            $"Expected dedup — second FlowEngine.Execute MUST NOT re-emit the advisory. Got:\n{stderr}");
    }
}
