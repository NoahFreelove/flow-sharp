using System.Diagnostics;
using FlowLang.Analysis;
using FlowLang.Tests.Characterization;
using Xunit;

namespace FlowLang.Tests.Analysis;

public class LanguageAnalysisTests
{
    private sealed class MemorySources(Dictionary<string, string> files) : IModuleSourceProvider
    {
        public SourceDocument? Resolve(string path, string importer) => files.TryGetValue(path, out var text)
            ? new SourceDocument(path, text) : null;
    }

    [Fact]
    public void DiscoversSignaturesAndCyclesWithoutEvaluatingInitializers()
    {
        var tree = LanguageAnalysis.Parse("use \"helper\"\nwhile true { (print \"never\") }", "root");
        var sources = new MemorySources(new()
        {
            ["helper"] = "use \"root\"\n(print \"module effect\")\n/// Identity.\nproc identity(Int: x)\nx\nend proc",
            ["root"] = tree.Source.Text,
        });
        var result = LanguageAnalysis.Analyze(tree, sources);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.Equal(2, result.Modules.Count);
        var procedure = Assert.Single(result.Modules.Single(m => m.Id == "helper").Procedures);
        Assert.Equal("identity", procedure.Name);
        Assert.Equal(new[] { "x" }, procedure.Signature.ParameterNames);
        Assert.Equal("Identity.", procedure.Documentation);
        Assert.NotEmpty(result.Unchecked);
    }

    [Fact]
    public void ImportFailuresKeepCodesAndImporterSpans()
    {
        var tree = LanguageAnalysis.Parse("\nuse \"missing\"", "/virtual/root.flow");
        var result = LanguageAnalysis.Analyze(tree, new MemorySources(new()));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.Success);
        Assert.Equal("flow.module.missing", diagnostic.Code);
        Assert.Equal("/virtual/root.flow", diagnostic.Span.Start.FileName);
        Assert.Equal(2, diagnostic.Span.Start.Line);
    }

    [Fact]
    public void ImportedSyntaxErrorsKeepTheirOwnSourceIdentity()
    {
        var result = LanguageAnalysis.Analyze(LanguageAnalysis.Parse("use \"broken\"", "root"),
            new MemorySources(new() { ["broken"] = "proc (" }));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "flow.syntax.parse" && d.Span.Start.FileName == "broken");
    }

    [Fact]
    public void LspUsesTheSameSyntaxDiagnosticCodesAndSpans()
    {
        var parsed = new FlowLsp.ParseSession().Parse("proc (", "editor.flow");
        var syntax = Assert.IsType<SyntaxTree>(parsed.Syntax);
        var wire = FlowLsp.Handlers.DiagnosticsPublisher.BuildAnalysisDiagnostics(syntax.Diagnostics);
        Assert.Equal(syntax.Diagnostics.Count, wire.Count);
        for (int i = 0; i < wire.Count; i++)
        {
            Assert.Equal(syntax.Diagnostics[i].Code, wire[i].Code!.Value.String);
            Assert.Equal(Math.Max(0, syntax.Diagnostics[i].Span.Start.Line - 1), wire[i].Range.Start.Line);
            Assert.Equal(Math.Max(0, syntax.Diagnostics[i].Span.End.Column - 1), wire[i].Range.End.Character);
        }
    }

    [Fact]
    public void FileSourcesUseExplicitRootsAndConfiguredSearchPaths()
    {
        var temp = Path.Combine(Path.GetTempPath(), "flow-module-sources-" + Guid.NewGuid().ToString("N"));
        var cwd = Environment.CurrentDirectory;
        Directory.CreateDirectory(Path.Combine(temp, "library"));
        try
        {
            File.WriteAllText(Path.Combine(temp, "library", "helper.flow"), "proc identity(Int: x) x end proc");
            var provider = new FileModuleSourceProvider(temp, searchPaths: new[] { "library" });
            var source = provider.Resolve("helper", Path.Combine(temp, "main.flow"));
            Assert.NotNull(source);
            Assert.Equal(Path.Combine(temp, "library", "helper.flow"), source.Id);
            Assert.Equal(cwd, Environment.CurrentDirectory);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    [Fact]
    public void ModuleBudgetAndCancellationBoundDiscovery()
    {
        var root = LanguageAnalysis.Parse("use \"one\"", "root");
        var sources = new MemorySources(new() { ["one"] = "use \"two\"", ["two"] = "" });
        var result = LanguageAnalysis.Analyze(root, sources, new AnalysisOptions(MaxModules: 2));
        Assert.Contains(result.Diagnostics, d => d.Code == "flow.module.limit");
        Assert.Equal(2, result.Modules.Count);
        Assert.Throws<OperationCanceledException>(() => LanguageAnalysis.Analyze(root, sources,
            new AnalysisOptions(Cancellation: new CancellationToken(canceled: true))));
    }

    [FlowLang.Tests.Helpers.FlowTargetFact("Desktop")]
    public async Task CheckDoesNotRunWritesPlaybackLoopsOrImportedBodies()
    {
        var temp = Path.Combine(Path.GetTempPath(), "flow-static-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var output = Path.Combine(temp, "forbidden.wav").Replace('\\', '/');
            await File.WriteAllTextAsync(Path.Combine(temp, "helper.flow"), $"""
                use "@audio"
                (print "MODULE RAN")
                (writeWav "{output}" (createSineTone 440Hz 0.01 0.1))
                """);
            var script = Path.Combine(temp, "main.flow");
            await File.WriteAllTextAsync(script, """
                use "helper.flow"
                use "@audio"
                (print "SCRIPT RAN")
                (play (createSineTone 440Hz 0.01 0.1))
                while true { (print "LOOP RAN") }
                """);
            var cli = Path.Combine(Snapshot.RepoRoot, "flow-cli", "bin", "Debug", "net10.0", "flow.dll");
            var psi = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = temp, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false,
            };
            psi.ArgumentList.Add(cli);
            psi.ArgumentList.Add("check");
            psi.ArgumentList.Add(script);
            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            Assert.Equal(0, process.ExitCode);
            Assert.StartsWith("OK:", await stdout);
            Assert.DoesNotContain("RAN", await stdout);
            Assert.Empty(await stderr);
            Assert.False(File.Exists(output));
        }
        finally { Directory.Delete(temp, recursive: true); }
    }
}
