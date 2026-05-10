using FlowLang.Core;
using FlowLang.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace FlowLsp;

/// <summary>
/// Pure translations between Flow diagnostic types and LSP wire types.
/// Centralizing the 1-based → 0-based math here keeps every handler's use
/// consistent and regression-testable in one place.
/// </summary>
public static class LspMappings
{
    /// <summary>
    /// Convert Flow's 1-based SourceLocation to LSP's 0-based single-column Range.
    /// SourceLocation.Unknown has (0,0) — Math.Max guards prevent underflow.
    /// Length-1 range because FlowError carries only a start position; widen later
    /// if parse-error recovery adds end tracking.
    /// </summary>
    public static Range ToRange(SourceLocation loc)
    {
        var line = Math.Max(0, loc.Line - 1);
        var col = Math.Max(0, loc.Column - 1);
        return new Range(new Position(line, col), new Position(line, col + 1));
    }

    public static DiagnosticSeverity ToSeverity(DiagnosticLevel level) => level switch
    {
        DiagnosticLevel.Error => DiagnosticSeverity.Error,
        DiagnosticLevel.Warning => DiagnosticSeverity.Warning,
        DiagnosticLevel.Info => DiagnosticSeverity.Information,
        _ => DiagnosticSeverity.Error
    };
}
