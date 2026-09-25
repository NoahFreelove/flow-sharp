using FlowLang.Analysis;
using FlowLang.Diagnostics;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace FlowLsp;

/// <summary>Editor adapter for the same non-executing analysis used by flow check.</summary>
public static class EditorAnalysis
{
    public static AnalysisResult Analyze(string text, string path, StdlibSymbolIndex stdlib,
        DocumentManager? documents = null, CancellationToken cancellation = default)
    {
        var root = LanguageAnalysis.Parse(text, path);
        var sources = new FileModuleSourceProvider(Path.GetDirectoryName(Path.GetFullPath(path))!,
            sourceOverlay: candidate => documents?.GetText(DocumentUri.FromFileSystemPath(candidate))
                ?? stdlib.Descriptors.FirstOrDefault(d => d.Id == candidate)?.Syntax.Source.Text);
        return LanguageAnalysis.Analyze(root, sources, new AnalysisOptions(Cancellation: cancellation));
    }
    /// <summary>Locate imported errors on the importing use statement with a related source span.</summary>
    public static IReadOnlyList<AnalysisDiagnostic> DocumentDiagnostics(AnalysisResult analysis)
    {
        var diagnostics = analysis.Diagnostics.Where(d => d.Span.Start.FileName == analysis.Root.Source.Id).ToList();
        foreach (var import in analysis.Dependencies.Where(d => d.ImporterId == analysis.Root.Source.Id))
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(import.SourceId);
            while (pending.TryPop(out var id))
            {
                if (!reachable.Add(id)) continue;
                foreach (var edge in analysis.Dependencies.Where(d => d.ImporterId == id)) pending.Push(edge.SourceId);
            }
            foreach (var failure in analysis.Diagnostics.Where(d => d.Level == DiagnosticLevel.Error
                && d.Span.Start.FileName != analysis.Root.Source.Id && reachable.Contains(d.Span.Start.FileName ?? "")))
                diagnostics.Add(new("flow.module.invalid", new FlowDiagnostic(DiagnosticLevel.Error,
                    $"Imported source has an error [{failure.Code}]: {failure.Message}", import.ImportSpan,
                    new[] { new DiagnosticLabel(failure.Span, failure.Message) }, Array.Empty<string>())));
        }
        return diagnostics;
    }

}
