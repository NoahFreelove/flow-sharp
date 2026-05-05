using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace FlowLsp.Diagnostics;

/// <summary>
/// Phase 24 Plan 24-04 (RESEARCH §Pattern 1 Shape A): "diagnostic source" interface
/// for scale-lint analysis. Unlike <see cref="FlowLsp.Handlers.IDiagnosticsPublisher"/>,
/// this interface RETURNS the diagnostic list rather than publishing it directly —
/// the <see cref="CombinedDiagnosticsPublisher"/> orchestrator owns the single
/// wire-level <c>PublishDiagnostics</c> call so that LSP REPLACE semantics don't
/// clobber parse errors. This is the deliberate shape deviation from the
/// IDiagnosticsPublisher analog: the analyzer-as-source pattern means parse errors
/// and scale-lint diagnostics merge into ONE Container per parse cycle.
///
/// Mockable for tests; the real implementation (<see cref="ScaleLintPublisher"/>)
/// delegates to <see cref="ScaleLintAnalyzer.Analyze"/>.
/// </summary>
public interface IScaleLintPublisher
{
    IReadOnlyList<Diagnostic> Analyze(ParseResult result, string source);
}
