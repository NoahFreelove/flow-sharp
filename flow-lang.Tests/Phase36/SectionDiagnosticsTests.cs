using System.Linq;
using FlowLang.Core;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-10 Task 3 — section-call diagnostic facts (D-36-16).
///
/// <para>
/// Pins the messages emitted on the four observable error paths:
/// <list type="bullet">
///   <item>Arity mismatch — wrong number of positional args.</item>
///   <item>Type mismatch — typed BindingPattern's TypeAnnotation rejects the
///   arg.</item>
///   <item>Ambiguous overload — two same-shape declarations.</item>
///   <item>Unknown section — call to a name that's not registered.</item>
/// </list>
/// </para>
/// </summary>
public class SectionDiagnosticsTests
{
    [Fact]
    public void ArityMismatchRendersDiagnostic()
    {
        using var engine = new FlowEngine(verbose: false);
        engine.Execute(
            "section verse(Note root) { Sequence inner = | C4q | }\n" +
            "Song s = [verse(C4, 2)]\n");
        // Either an arity-mismatch diagnostic OR a no-overload diagnostic
        // (the overload pipeline disqualifies the candidate due to too many
        // positional args, leaving 0 matches).
        var msg = engine.ErrorReporter.FormatErrors();
        Assert.True(
            msg.Contains("expects", System.StringComparison.OrdinalIgnoreCase)
            || msg.Contains("no overload", System.StringComparison.OrdinalIgnoreCase)
            || msg.Contains("got", System.StringComparison.OrdinalIgnoreCase),
            $"Expected arity-related diagnostic. Got: {msg}");
    }

    [Fact]
    public void TypeMismatchRendersDiagnostic()
    {
        using var engine = new FlowEngine(verbose: false);
        engine.Execute(
            "section verse(Note root) { Sequence inner = | C4q | }\n" +
            "Song s = [verse(\"string\")]\n");
        var msg = engine.ErrorReporter.FormatErrors();
        Assert.True(
            msg.Contains("no overload", System.StringComparison.OrdinalIgnoreCase)
            || msg.Contains("does not match", System.StringComparison.OrdinalIgnoreCase),
            $"Expected a type-mismatch / no-overload diagnostic. Got: {msg}");
    }

    [Fact]
    public void AmbiguousOverloadRendersBothCandidates()
    {
        using var engine = new FlowEngine(verbose: false);
        engine.Execute(
            "section verse(Note root) { Sequence a = | C4q | }\n" +
            "section verse(Note root2) { Sequence b = | D4q | }\n");
        var msg = engine.ErrorReporter.FormatErrors();
        Assert.Contains("ambiguous", msg.ToLowerInvariant());
    }

    [Fact]
    public void UnknownSectionRaises()
    {
        using var engine = new FlowEngine(verbose: false);
        engine.Execute(
            "section chorus { Sequence c = | C4q | }\n" +
            "Song s = [verses(C4)]\n");
        var msg = engine.ErrorReporter.FormatErrors();
        Assert.True(
            msg.Contains("Undefined section", System.StringComparison.OrdinalIgnoreCase)
            || msg.Contains("no section", System.StringComparison.OrdinalIgnoreCase),
            $"Expected an unknown-section diagnostic. Got: {msg}");
    }
}
