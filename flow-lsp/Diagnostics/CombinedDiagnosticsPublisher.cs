using FlowLsp.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace FlowLsp.Diagnostics;

/// <summary>
/// Phase 24 Plan 24-04: orchestrator owning the single wire-level
/// <c>_server.TextDocument.PublishDiagnostics</c> call per URI per parse cycle.
///
/// LSP <c>publishDiagnostics</c> REPLACES per-URI (DiagnosticsPublisher.cs:52 contract).
/// If parse errors and scale-lint diagnostics each tried to publish independently,
/// the second call would clobber the first. This class composes both diagnostic
/// sources into one <see cref="Container{T}"/> so editors see them simultaneously.
///
/// Source-tag separation is preserved (D-18): parse errors keep <c>Source="flow"</c>
/// (via <see cref="DiagnosticsPublisher.BuildDiagnostics"/>), scale-lint keeps
/// <c>Source="flow.scaleLint"</c> (via <see cref="ScaleLintAnalyzer"/> /
/// <see cref="IScaleLintPublisher"/>). Editors can filter independently.
///
/// Empty-publish-clears-squiggles invariant (DiagnosticsPublisher.cs:52): the
/// <see cref="Publish"/> method MUST call <c>PublishDiagnostics</c> UNCONDITIONALLY —
/// no <c>if (merged.Count &gt; 0)</c> or <c>if (merged.Any())</c> guard. An empty
/// publish is the only way to clear stale squiggles when an edit removes the
/// last offending note.
/// </summary>
public sealed class CombinedDiagnosticsPublisher
{
    private readonly ILanguageServerFacade _server;
    private readonly IScaleLintPublisher _lint;

    public CombinedDiagnosticsPublisher(ILanguageServerFacade server, IScaleLintPublisher lint)
    {
        _server = server;
        _lint = lint;
    }

    /// <summary>
    /// Pure: build the union of parse-error diagnostics and scale-lint diagnostics.
    /// Exposed static so unit tests can exercise the merge without standing up an
    /// <see cref="ILanguageServerFacade"/> — mirrors the static
    /// <see cref="DiagnosticsPublisher.BuildDiagnostics"/>. The static form bypasses
    /// dependency injection by invoking <see cref="ScaleLintAnalyzer.Analyze"/>
    /// directly (Wave 1 ParseSession populates <c>Ast.Pragmas</c>).
    /// </summary>
    public static IReadOnlyList<Diagnostic> BuildAll(ParseResult result, string source)
    {
        var parseDiags = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
        var lintDiags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, source);
        if (parseDiags.Count == 0 && lintDiags.Count == 0)
            return Array.Empty<Diagnostic>();
        var merged = new List<Diagnostic>(parseDiags.Count + lintDiags.Count);
        merged.AddRange(parseDiags);
        merged.AddRange(lintDiags);
        return merged;
    }

    /// <summary>
    /// Wire-level publish: build the union and call <c>PublishDiagnostics</c> ONCE.
    /// MUST publish even when the merged list is empty — that is how LSP clears
    /// prior markers (parse-error squiggles AND scale-lint squiggles).
    /// </summary>
    public void Publish(DocumentUri uri, ParseResult result, string source)
    {
        var parseDiags = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
        var lintDiags = _lint.Analyze(result, source);
        var merged = new List<Diagnostic>(parseDiags.Count + lintDiags.Count);
        merged.AddRange(parseDiags);
        merged.AddRange(lintDiags);

        // MUST publish even when empty — that is how LSP clears prior markers.
        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(merged)
        });
    }
}
