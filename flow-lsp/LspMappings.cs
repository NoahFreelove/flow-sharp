using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.TypeSystem;
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

    /// <summary>
    /// Format a <see cref="FunctionSignature"/> for hover / signature-help /
    /// completion-tooltip surfaces. Variadic params render with the Unicode
    /// horizontal ellipsis <c>…</c> (U+2026, UTF-8 <c>E2 80 A6</c>) trailing the
    /// parameter TYPE per Phase 31 CONTEXT D-01 (glyph) + D-02 (position) —
    /// NOT three ASCII dots, NOT after the parameter name.
    ///
    /// flow-lang stays untouched per Phase 24 D-04 ("zero flow-lang touch for
    /// LSP-only work") — <see cref="FunctionSignature.ToString"/> continues to
    /// emit ASCII <c>"..."</c> for runtime use. This LSP-side renderer is the
    /// missing layer.
    /// </summary>
    public static string FormatSignature(FunctionSignature sig)
    {
        var inputs = sig.InputTypes.Select((t, i) =>
            sig.IsVarArgs && i == sig.InputTypes.Count - 1
                ? $"{t}…"   // U+2026 horizontal ellipsis — trails the type (D-02)
                : $"{t}");
        return $"{sig.Name}({string.Join(", ", inputs)})";
    }

    /// <summary>
    /// Emit an explicit <see cref="ParameterInformation"/> array for
    /// <see cref="SignatureInformation.Parameters"/>. Mitigates Pitfall 3
    /// (RESEARCH.md): U+2026 is 3 bytes in UTF-8 / 1 grapheme — LSP clients
    /// compute <c>ActiveParameter</c> offsets in UTF-16 code units, and the
    /// safer path is to expose explicit per-parameter Labels rather than rely
    /// on offset arithmetic inside the merged signature string. Each parameter
    /// label uses the same <c>Type…</c> form as <see cref="FormatSignature"/>
    /// for the trailing varargs slot.
    /// </summary>
    public static Container<ParameterInformation> BuildParameters(FunctionSignature sig)
    {
        var list = new List<ParameterInformation>(sig.InputTypes.Count);
        for (int i = 0; i < sig.InputTypes.Count; i++)
        {
            var typeStr = sig.IsVarArgs && i == sig.InputTypes.Count - 1
                ? $"{sig.InputTypes[i]}…"
                : $"{sig.InputTypes[i]}";
            list.Add(new ParameterInformation
            {
                Label = new ParameterInformationLabel(typeStr)
            });
        }
        return new Container<ParameterInformation>(list);
    }
}
