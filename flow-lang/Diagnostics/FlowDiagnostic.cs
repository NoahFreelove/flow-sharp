using FlowLang.Core;

namespace FlowLang.Diagnostics;

/// <summary>
/// Phase 35 LANG-04 Wave 2a — richer diagnostic record consumed by the
/// <see cref="DiagnosticRenderer"/> to emit Rust-style multi-line errors
/// with source-quoted spans, secondary labels, notes, and did-you-mean
/// suggestions.
///
/// <para>
/// Sits ALONGSIDE the legacy <see cref="FlowError"/> — FlowError stays as
/// the single-line fallback for emit sites that don't yet have Span
/// context. Both records flow through the same <see cref="ErrorReporter"/>
/// (parallel <c>Report</c> overloads); the top-level emit picks
/// <see cref="ErrorReporter.FormatDiagnostics"/> when diagnostics are
/// present, falling back to <see cref="ErrorReporter.FormatErrors"/>
/// otherwise.
/// </para>
///
/// <para>
/// Per PATTERNS.md Bucket 2a §FlowDiagnostic.cs, the static factories
/// (<see cref="Create"/> / <see cref="Warning"/> / <see cref="Info"/>)
/// mirror <see cref="FlowError"/>'s shape but take a <see cref="Span"/>
/// instead of a single <see cref="SourceLocation"/>.
/// </para>
/// </summary>
public record FlowDiagnostic(
    DiagnosticLevel Level,
    string Message,
    Span Primary,
    IReadOnlyList<DiagnosticLabel> Labels,
    IReadOnlyList<string> Notes,
    string? Suggestion = null)
{
    /// <summary>
    /// Convenience factory mirroring <see cref="FlowError.Create"/>'s shape
    /// — Level=Error, empty Labels, empty Notes, no Suggestion. Use the
    /// full positional ctor to attach labels / notes / suggestions.
    /// </summary>
    public static FlowDiagnostic Create(string message, Span primary)
        => new(DiagnosticLevel.Error, message, primary,
               Labels: Array.Empty<DiagnosticLabel>(),
               Notes: Array.Empty<string>(),
               Suggestion: null);

    /// <summary>
    /// Convenience factory mirroring <see cref="FlowError.Warning"/>'s shape.
    /// </summary>
    public static FlowDiagnostic Warning(string message, Span primary)
        => new(DiagnosticLevel.Warning, message, primary,
               Labels: Array.Empty<DiagnosticLabel>(),
               Notes: Array.Empty<string>(),
               Suggestion: null);

    /// <summary>
    /// Convenience factory mirroring <see cref="FlowError.Info"/>'s shape.
    /// </summary>
    public static FlowDiagnostic Info(string message, Span primary)
        => new(DiagnosticLevel.Info, message, primary,
               Labels: Array.Empty<DiagnosticLabel>(),
               Notes: Array.Empty<string>(),
               Suggestion: null);
}

/// <summary>
/// Phase 35 LANG-04 Wave 2a — secondary span + label text, attached to a
/// <see cref="FlowDiagnostic"/> to describe a related code region (e.g.,
/// "this argument has type String" pointing at the offending argument
/// while the primary span covers the whole call).
///
/// <para>
/// The renderer draws each label's span as a caret-line beneath the
/// matching source line. Labels whose span lies on the SAME line as the
/// primary span share a single source-quote row; labels on different
/// lines get their own source-quote row.
/// </para>
/// </summary>
public record DiagnosticLabel(Span Span, string Text);
