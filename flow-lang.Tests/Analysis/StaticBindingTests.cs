using FlowLang.Analysis;
using FlowLsp;
using FlowLsp.Diagnostics;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;

namespace FlowLang.Tests.Analysis;

public class StaticBindingTests
{
    private sealed class Sources(Dictionary<string, string>? sources = null) : IModuleSourceProvider
    {
        public SourceDocument? Resolve(string path, string importer) => sources?.TryGetValue(path, out var text) == true
            ? new(path, text) : null;
    }

    private static AnalysisResult Analyze(string source, Dictionary<string, string>? modules = null) =>
        LanguageAnalysis.Analyze(LanguageAnalysis.Parse(source, "/project/main.flow"), new Sources(modules));

    [Fact]
    public void RelativeImportsProvideProcedureAndVariableBindingsWithoutRunning()
    {
        var result = Analyze("use \"helper\"; (identity exported); (missing 1)", new()
        {
            ["helper"] = "Int exported = 42; proc identity(Int: value) value end proc; while true {}",
        });
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("flow.binding.unknown", diagnostic.Code);
        Assert.Contains("missing", diagnostic.Message);
        Assert.Equal("/project/main.flow", diagnostic.Span.Start.FileName);
    }

    [Fact]
    public void ProcedureParametersDoNotLeakIntoSiblingScopes()
    {
        var result = Analyze("proc identity(Int: secret) secret end proc; secret");
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("secret", diagnostic.Message);
        Assert.Equal("flow.binding.unknown", diagnostic.Code);
    }

    [Theory]
    [InlineData("(identity 1 2)", "flow.call.arguments")]
    [InlineData("(identity wrong=1)", "flow.call.arguments")]
    [InlineData("(identity \"bad\")", "flow.call.types")]
    [InlineData("1 -> identity", null)]
    [InlineData("(identity value=1)", null)]
    public void ChecksKnownSourceProcedureContracts(string call, string? code)
    {
        var result = Analyze("proc identity(Int: value) value end proc; " + call);
        if (code is null) Assert.Empty(result.Diagnostics);
        else Assert.Equal(code, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void DynamicCallsAndHostSignaturesRemainUnknown()
    {
        var result = Analyze("internal proc host(Int: x); (host); proc identity(Int: x) x end proc; Void f = fn Int x => x; (f 1); (identity (host))");
        Assert.Empty(result.Diagnostics);
    }

    [FlowLang.Tests.Helpers.FlowTargetFact("Desktop")]
    public void EditorUsesUnsavedImportedSourceAndSameCodesAsCli()
    {
        var root = Path.Combine(Path.GetTempPath(), "flow-overlay-" + Guid.NewGuid().ToString("N"));
        var helper = Path.Combine(root, "helper.flow");
        var docs = new DocumentManager((_, _, _) => Task.CompletedTask);
        var helperUri = DocumentUri.FromFileSystemPath(helper);
        docs.Open(helperUri, "proc identity(Int: value) value end proc");
        try
        {
            var text = "use \"helper.flow\"; (identity 1 2)";
            var stdlib = new StdlibSymbolIndex(new ParseSession());
            var result = EditorAnalysis.Analyze(text, Path.Combine(root, "main.flow"), stdlib, docs);
            Assert.Equal("flow.call.arguments", Assert.Single(result.Diagnostics).Code);
            var parsed = new ParseResult(result.Root.Program, result.Root.Tokens, result.Root.LegacyErrors)
                { Syntax = result.Root, Analysis = result };
            var wire = CombinedDiagnosticsPublisher.BuildAll(parsed, text, stdlib);
            Assert.Contains(wire, d => d.Code?.String == "flow.call.arguments");
            Assert.NotNull(new BuiltInIndex(result.Modules).Find("identity"));
        }
        finally { docs.Close(helperUri); }
    }

    [FlowLang.Tests.Helpers.FlowTargetFact("Desktop")]
    public void StandardLibraryVisibilityFollowsActualImportsAndKeepsOwners()
    {
        var stdlib = new StdlibSymbolIndex(new ParseSession());
        Assert.Contains("collections", stdlib.VisibleModules("core"));
        Assert.Equal(stdlib.VisibleModules("core"), stdlib.VisibleModules("core.flow"));
        Assert.DoesNotContain("osc", stdlib.VisibleModules("std"));
        Assert.NotEmpty(stdlib.ProcsForModule("core"));
        Assert.NotEmpty(stdlib.ProcsForModule("std"));
    }

    [FlowLang.Tests.Helpers.FlowTargetFact("Desktop")]
    public void ImportedSyntaxErrorIsRelatedToTheImportInsteadOfTheWrongDocumentLine()
    {
        var result = Analyze("use \"broken\"", new() { ["broken"] = "\n\nproc (" });
        var local = EditorAnalysis.DocumentDiagnostics(result);
        var diagnostic = Assert.Single(local);
        Assert.Equal("flow.module.invalid", diagnostic.Code);
        Assert.Equal(1, diagnostic.Span.Start.Line);
        Assert.Equal("broken", Assert.Single(diagnostic.Detail.Labels).Span.Start.FileName);
        Assert.Contains("flow.syntax.parse", diagnostic.Message);
    }

}
