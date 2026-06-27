using FlowLsp.Handlers;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace FlowLsp.Diagnostics;

/// <summary>
/// Phase 24 Plan 24-04 + Phase 31 Plan 31-02: orchestrator owning the single
/// wire-level <c>_server.TextDocument.PublishDiagnostics</c> call per URI per
/// parse cycle.
///
/// LSP <c>publishDiagnostics</c> REPLACES per-URI (DiagnosticsPublisher.cs:52 contract).
/// If parse errors and analyzer diagnostics each tried to publish independently,
/// the second call would clobber the first. This class composes ALL diagnostic
/// sources into one <see cref="Container{T}"/> so editors see them simultaneously.
///
/// Phase 31 Plan 31-02 wired three additional analyzer sources into BuildAll
/// alongside the existing parse-error + scale-lint pipeline:
///   - <see cref="UnusedImportAnalyzer"/> (Warning, "flow.unusedImport")
///   - <see cref="UnreachableSectionAnalyzer"/> (Information, "flow.unreachableSection")
///   - <see cref="ShadowedVariableAnalyzer"/> (Warning, "flow.shadowedVariable")
/// Five sources total. Per-source dotted Source strings preserved (Phase 24 D-18 +
/// Phase 31 D-05) so editors filter independently.
///
/// Empty-publish-clears-squiggles invariant (DiagnosticsPublisher.cs:52): the
/// <see cref="Publish"/> method MUST call <c>PublishDiagnostics</c> UNCONDITIONALLY —
/// no <c>if (merged.Count &gt; 0)</c> or <c>if (merged.Any())</c> guard. An empty
/// publish is the only way to clear stale squiggles when an edit removes the
/// last offending note.
///
/// Phase 31 Plan 31-02 deliberately keeps the new analyzers invoked as DIRECT
/// STATIC CALLS in BuildAll — no <c>IUnusedImportPublisher</c> /
/// <c>IUnreachableSectionPublisher</c> / <c>IShadowedVariablePublisher</c>
/// interfaces. Per RESEARCH §Open Questions answer #3, DI symmetry with
/// <see cref="IScaleLintPublisher"/> can wait for v1.5.
/// </summary>
public sealed class CombinedDiagnosticsPublisher
{
    private readonly ILanguageServerFacade _server;
    private readonly IScaleLintPublisher _lint;
    private readonly StdlibSymbolIndex _stdlib;

    public CombinedDiagnosticsPublisher(
        ILanguageServerFacade server,
        IScaleLintPublisher lint,
        StdlibSymbolIndex stdlib)
    {
        _server = server;
        _lint = lint;
        _stdlib = stdlib;
    }

    /// <summary>
    /// Pure: build the union of parse-error + scale-lint + unused-import +
    /// unreachable-section + shadowed-variable diagnostics. Exposed static so
    /// unit tests can exercise the merge without standing up an
    /// <see cref="ILanguageServerFacade"/> — mirrors the static
    /// <see cref="DiagnosticsPublisher.BuildDiagnostics"/>. The static form bypasses
    /// dependency injection by invoking each analyzer's <c>Analyze</c> method
    /// directly (Wave 1 ParseSession populates <c>Ast.Pragmas</c>).
    ///
    /// Phase 31 D-03: <see cref="ScaleLintAnalyzer.Analyze"/> no longer requires
    /// the <c>scaleLint</c> pragma — it runs unconditionally on every parse.
    /// </summary>
    public static IReadOnlyList<Diagnostic> BuildAll(
        ParseResult result, string source, StdlibSymbolIndex stdlib)
    {
        var parseDiags     = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
        var lintDiags      = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, source);
        var unusedDiags    = UnusedImportAnalyzer.Analyze(result.Ast, result.Tokens, source, stdlib);
        var unreachDiags   = UnreachableSectionAnalyzer.Analyze(result.Ast, result.Tokens, source);
        var shadowDiags    = ShadowedVariableAnalyzer.Analyze(result.Ast, result.Tokens, source);
        var undefinedDiags = UndefinedSymbolAnalyzer.Analyze(result.Ast, result.Tokens, source, stdlib);

        // Phase 31 Plan 31-08 (scope expansion): six-source merge — adds
        // UndefinedSymbolAnalyzer to the 5-source Phase 31 Plan 31-02 set.
        var merged = new List<Diagnostic>(
            parseDiags.Count + lintDiags.Count + unusedDiags.Count +
            unreachDiags.Count + shadowDiags.Count + undefinedDiags.Count);
        merged.AddRange(parseDiags);
        merged.AddRange(lintDiags);
        merged.AddRange(unusedDiags);
        merged.AddRange(unreachDiags);
        merged.AddRange(shadowDiags);
        merged.AddRange(undefinedDiags);
        return merged;
    }

    /// <summary>
    /// Wire-level publish: build the union of all five diagnostic sources and
    /// call <c>PublishDiagnostics</c> ONCE. MUST publish even when the merged
    /// list is empty — that is how LSP clears prior markers (parse-error,
    /// scale-lint, unused-import, unreachable-section, AND shadowed-variable
    /// squiggles).
    ///
    /// Phase 31 Plan 31-02: delegates the merge logic to <see cref="BuildAll"/>
    /// instead of duplicating it inline. The DI-mockable
    /// <see cref="IScaleLintPublisher"/> wrapper exists for v1.3 backward
    /// compatibility but the static path now drives both instance and test
    /// invocations identically (Phase 24 D-19 short-circuit removed).
    /// </summary>
    public void Publish(DocumentUri uri, ParseResult result, string source)
    {
        var merged = BuildAll(result, source, _stdlib);

        // MUST publish even when empty — that is how LSP clears prior markers.
        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(merged)
        });
    }
}
