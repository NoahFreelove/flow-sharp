using FlowLang.Analysis;
using FlowLang.Core;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Analysis;

public class EvaluationAdapterTests
{
    [Fact]
    public void ParsedSyntaxEvaluatesInIndependentExplicitSessions()
    {
        var syntax = LanguageAnalysis.Parse("use \"@core\"; Int answer = 42; (print answer)", "session.flow");
        using var firstOutput = new StringWriter();
        using var secondOutput = new StringWriter();
        using var first = new FlowEngine(new EngineOptions { Output = firstOutput, Diagnostics = TextWriter.Null });
        using var second = new FlowEngine(new EngineOptions { Output = secondOutput, Diagnostics = TextWriter.Null });
        Assert.True(first.Evaluate(syntax).Succeeded);
        Assert.True(second.Evaluate(syntax).Succeeded);
        Assert.Equal("42" + Environment.NewLine, firstOutput.ToString());
        Assert.Equal(firstOutput.ToString(), secondOutput.ToString());
        Assert.False(first.Evaluate(syntax).Succeeded); // explicit session retains its bindings
    }

    [Fact]
    public void ParsedFailurePreservesCodesSpansAndDoesNotEvaluatePartialTree()
    {
        var syntax = LanguageAnalysis.Parse("use \"@core\"; (print \"must not run\"); proc (", "invalid.flow");
        using var output = new StringWriter();
        using var engine = new FlowEngine(new EngineOptions { Output = output, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(syntax);
        Assert.Equal(EvaluationOutcome.Failed, result.Outcome);
        Assert.Equal(syntax.Diagnostics, result.CodedDiagnostics);
        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData("proc (", "flow.syntax.parse")]
    [InlineData("enable notARealPragma;", "flow.syntax.pragma")]
    [InlineData("(missing)", "flow.evaluation")]
    public void LegacySourceEvaluationAlsoExposesLocatedCodes(string source, string code)
    {
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(source, "coded.flow");
        Assert.False(result.Succeeded);
        Assert.Contains(result.CodedDiagnostics, d => d.Code == code && d.Span.Start.FileName == "coded.flow"
            && d.Span.Start.Line > 0);
    }

    private sealed class ThrowingWriter : StringWriter
    {
        public bool Enabled { get; set; }
        public override void WriteLine(string? value)
        {
            if (Enabled) throw new IOException("host sink unavailable");
        }
    }

    [Fact]
    public void HostFailureHasItsOwnOutcomeAndLocatedDiagnostic()
    {
        using var sink = new ThrowingWriter();
        using var engine = new FlowEngine(new EngineOptions { Verbose = true, Diagnostics = sink, Output = TextWriter.Null });
        sink.Enabled = true;
        var result = engine.Evaluate("42", "host.flow");
        Assert.Equal(EvaluationOutcome.HostFailure, result.Outcome);
        Assert.IsType<IOException>(result.HostException);
        var diagnostic = Assert.Single(result.CodedDiagnostics);
        Assert.Equal("flow.host.failure", diagnostic.Code);
        Assert.Equal("host.flow", diagnostic.Span.Start.FileName);
        Assert.Equal(1, diagnostic.Span.Start.Line);
    }

    [Fact]
    public void BrowserAdapterPreservesCategoryAndUsesDiagnosticSourceForSnippet()
    {
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate("proc (", "parsed.flow");
        var errors = WasmEntry.MapEvaluation(result, engine.SourceMap);
        var error = Assert.Single(errors);
        Assert.Equal("parse", error.Kind);
        Assert.Equal("proc (", error.SourceSnippet);
        Assert.True(error.Line > 0);
    }
    [Theory]
    [MemberData(nameof(FlowLang.Tests.Contracts.LanguageContractTests.Contracts), MemberType = typeof(FlowLang.Tests.Contracts.LanguageContractTests))]
    public void ParsedEvaluationPreservesEveryLanguageContract(string relativePath)
    {
        var path = Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot, relativePath);
        var source = File.ReadAllText(path);
        using var legacyOutput = new StringWriter();
        using var parsedOutput = new StringWriter();
        using var legacy = new FlowEngine(new EngineOptions { Output = legacyOutput, Diagnostics = TextWriter.Null });
        using var parsed = new FlowEngine(new EngineOptions { Output = parsedOutput, Diagnostics = TextWriter.Null });
        var before = legacy.Evaluate(source, path);
        var after = parsed.Evaluate(LanguageAnalysis.Parse(source, path));
        Assert.Equal(before.Outcome, after.Outcome);
        Assert.Equal(legacyOutput.ToString(), parsedOutput.ToString());
        Assert.Equal(before.CodedDiagnostics.Select(d => (d.Code, d.Message, d.Span)),
            after.CodedDiagnostics.Select(d => (d.Code, d.Message, d.Span)));
    }

}
