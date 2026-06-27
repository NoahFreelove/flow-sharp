using System;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit-0609 §5.8 — RenderScript must surface parse/eval diagnostics.
///
/// Before the fix, when engine.Execute produced errors the <c>errors</c>
/// out-parameter stayed null and the caller emitted "no audio output detected"
/// with no line number — useless during save-listen iteration.
///
/// The fix in LiveReloadManager.RenderScript: after engine.Execute, when
/// engine.ErrorReporter.HasErrors, set errors = Program.FormatErrorsForEmit(engine).
/// Program.FormatErrorsForEmit internally delegates to
/// engine.ErrorReporter.FormatErrors() / FormatDiagnostics(), both of which
/// are tested directly here since Program is internal to flow-interpreter.
///
/// These tests exercise <see cref="FlowEngine"/> directly (the same path
/// RenderScript uses) to verify that a script with a syntax error produces
/// non-empty, line-number-containing diagnostics via the same ErrorReporter
/// API that RenderScript now calls.
/// </summary>
[Collection("FlowScripts")]
public class RenderScriptErrorSurfacingTests : IDisposable
{
    public RenderScriptErrorSurfacingTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// A script with a parse error (unclosed paren) must cause HasErrors == true
    /// and FormatErrors must produce non-empty output. This is the exact condition
    /// RenderScript now checks — errors out-param is set from FormatErrors.
    /// </summary>
    [Fact]
    public void ParseError_HasErrors_AndFormatErrors_IsNonEmpty()
    {
        const string badSource = "(print \"hello\"\n// unclosed paren above\n";

        using var engine = new FlowEngine();
        engine.Execute(badSource, "<test>");

        Assert.True(engine.ErrorReporter.HasErrors,
            "A script with an unclosed paren must produce errors");

        // ErrorReporter.FormatErrors() is what Program.FormatErrorsForEmit delegates to.
        string formatted = engine.ErrorReporter.FormatErrors();
        Assert.False(string.IsNullOrWhiteSpace(formatted),
            "FormatErrors must produce non-empty text when parse errors exist");

        // The formatted output must reference the source location (line 1).
        Assert.True(
            formatted.Contains("1") || formatted.Contains("line"),
            $"Error diagnostic must reference line 1; got: {formatted}");
    }

    /// <summary>
    /// A script that runs clean (no errors, no audio output) must leave
    /// HasErrors == false. This ensures the fix doesn't spuriously set errors
    /// for healthy scripts.
    /// </summary>
    [Fact]
    public void CleanScript_HasNoErrors()
    {
        const string goodSource = "Int x = 5\n";

        using var engine = new FlowEngine();
        engine.Execute(goodSource, "<test>");

        Assert.False(engine.ErrorReporter.HasErrors,
            "A clean script must produce no errors");
    }

    /// <summary>
    /// An eval error (type mismatch) must also cause HasErrors == true and
    /// produce non-empty formatted output so watch-mode users see the real
    /// diagnostic rather than "no audio output detected".
    /// </summary>
    [Fact]
    public void EvalError_HasErrors_AndFormatErrors_IsNonEmpty()
    {
        // Force a type error: assigning a string to an Int variable.
        const string badSource = "Int x = \"not a number\"\n";

        using var engine = new FlowEngine();
        engine.Execute(badSource, "<test>");

        Assert.True(engine.ErrorReporter.HasErrors,
            "A type-error script must produce errors");

        string formatted = engine.ErrorReporter.FormatErrors();
        Assert.False(string.IsNullOrWhiteSpace(formatted),
            "FormatErrors must produce non-empty text for a type error");
    }
}
