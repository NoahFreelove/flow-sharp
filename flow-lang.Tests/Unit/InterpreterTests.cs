using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit;

/// <summary>
/// FIX-07a regression tests: after the return;→break; edits in
/// Interpreter.ExecuteMusicalContext, the body of every musical-context
/// block MUST still execute when the context value fails validation —
/// under the partial/default context — while the error is still reported
/// via the ErrorReporter. These tests lock in that invariant across all
/// five invalid-context branches (tempo, swing, gain, pan, key).
/// </summary>
[Collection("FlowScripts")]   // serialize Console.SetOut with wrap-as-Theory (RESEARCH Pitfall 4)
public class ExecuteMusicalContextTests
{
    [Fact]
    public void BadTempo_BodyStillRuns_ErrorReported()
    {
        using var runner = new FlowEngineRunner();
        // `use "@std"` is required for `print` to resolve — the stdlib module
        // registers the `internal proc print` declaration that binds to the
        // C# StdLib.Print implementation. Without it, `print` is unresolved
        // even though the C# implementation is registered at engine init.
        var (_, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
tempo -5 {
    (print ""body-ran"")
}
(print ""after-block"")
");
        Assert.Contains("body-ran", stdout);
        Assert.Contains("after-block", stdout);
        Assert.True(errorCount >= 1);
        Assert.Contains("Tempo must be positive", stderr);
    }

    [Theory]
    [InlineData("tempo -5", "Tempo must be positive")]
    [InlineData("swing 2.0", "Swing must be between 0.0 and 1.0")]
    [InlineData("gain 5.0", "Gain must be between 0.0 and 2.0")]
    [InlineData("pan 2.0", "Pan value must be between -1.0 and 1.0")]
    [InlineData("key NotAKey", "Unrecognized key")]
    public void ValidationPath_BodyRunsUnderDefaultContext(string contextDecl, string expectedError)
    {
        using var runner = new FlowEngineRunner();
        // `use "@std"` required — see note on BadTempo_BodyStillRuns_ErrorReported.
        var source = $"use \"@std\"\n{contextDecl} {{ (print \"body-ran\") }}";
        var (_, stdout, stderr, _) = runner.RunSource(source);
        Assert.Contains("body-ran", stdout);
        Assert.Contains(expectedError, stderr);
    }
}
