using FlowLang.Core;

namespace FlowLang.Diagnostics;

/// <summary>
/// Collects and reports errors during compilation and execution.
///
/// <para>
/// Phase 35 LANG-04 Wave 2a — extended with a parallel
/// <see cref="FlowDiagnostic"/> collection alongside the existing
/// <see cref="FlowError"/> list. Emit sites that have rich Span context
/// (post-Plan-35-01 Span migration) call <see cref="Report(FlowDiagnostic)"/>;
/// emit sites that don't yet have Span context continue to call
/// <see cref="Report(FlowError)"/>. Both collections coexist mid-migration
/// per PATTERNS.md Bucket 2a §ErrorReporter.cs Notable Departures.
/// </para>
/// <para>
/// Top-level emit (flow-interpreter/Program.cs, Task 3) picks
/// <see cref="FormatDiagnostics"/> when <see cref="HasDiagnostics"/> is
/// true, falling back to <see cref="FormatErrors"/> otherwise. This keeps
/// the rich rendering active for span-aware emit sites while preserving
/// legacy single-line output for any error path that hasn't migrated.
/// </para>
/// </summary>
public class ErrorReporter
{
    private readonly List<FlowError> _errors = [];
    private readonly List<FlowDiagnostic> _diagnostics = [];
    private bool _hasErrors = false;
    private const int MaxErrorCount = 50;

    public IReadOnlyList<FlowError> Errors => _errors;

    /// <summary>
    /// Phase 35 LANG-04 Wave 2a — rich FlowDiagnostic accumulator. Parallel
    /// to <see cref="Errors"/> — both collections grow independently as
    /// emit sites migrate to the Span-aware diagnostic.
    /// </summary>
    public IReadOnlyList<FlowDiagnostic> Diagnostics => _diagnostics;

    public bool HasErrors => _hasErrors;

    /// <summary>
    /// True when at least one <see cref="FlowDiagnostic"/> has been
    /// reported. Used by the top-level emit (Program.cs) to decide
    /// between <see cref="FormatDiagnostics"/> and the legacy
    /// <see cref="FormatErrors"/>.
    /// </summary>
    public bool HasDiagnostics => _diagnostics.Count > 0;

    public void Report(FlowError error)
    {
        if (error.Level == DiagnosticLevel.Error)
        {
            _hasErrors = true;
        }

        if (_errors.Count < MaxErrorCount)
        {
            _errors.Add(error);
        }
        else if (_errors.Count == MaxErrorCount)
        {
            _errors.Add(FlowError.Warning("Maximum error limit reached. Further errors will be suppressed.", null));
        }
    }

    /// <summary>
    /// Phase 35 LANG-04 Wave 2a — overload accumulating a rich
    /// <see cref="FlowDiagnostic"/>. Sets <see cref="HasErrors"/> when
    /// the diagnostic's level is Error (mirrors <see cref="Report(FlowError)"/>
    /// behavior so the existing pipeline gating on HasErrors continues
    /// to short-circuit correctly).
    /// </summary>
    public void Report(FlowDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (diagnostic.Level == DiagnosticLevel.Error)
        {
            _hasErrors = true;
        }

        if (_diagnostics.Count < MaxErrorCount)
        {
            _diagnostics.Add(diagnostic);
        }
        else if (_diagnostics.Count == MaxErrorCount)
        {
            _diagnostics.Add(FlowDiagnostic.Warning(
                "Maximum diagnostic limit reached. Further diagnostics will be suppressed.",
                Span.Unknown));
        }
    }

    public void ReportError(string message, Core.SourceLocation? location = null)
    {
        Report(FlowError.Create(message, location));
    }

    public void ReportWarning(string message, Core.SourceLocation? location = null)
    {
        Report(FlowError.Warning(message, location));
    }

    public void ReportInfo(string message, Core.SourceLocation? location = null)
    {
        Report(FlowError.Info(message, location));
    }

    public void Clear()
    {
        _errors.Clear();
        _diagnostics.Clear();
        _hasErrors = false;
    }

    public string FormatErrors()
    {
        return string.Join("\n", _errors.Select(e => e.ToString()));
    }

    /// <summary>
    /// Phase 35 LANG-04 Wave 2a — renders each accumulated
    /// <see cref="FlowDiagnostic"/> through <see cref="DiagnosticRenderer.Render"/>
    /// and joins with a single blank-line separator (double <c>\n</c>) — matches
    /// rustc's inter-diagnostic convention.
    ///
    /// <para>
    /// Returns <see cref="string.Empty"/> when no diagnostics are accumulated.
    /// Top-level emit (flow-interpreter/Program.cs Task 3) calls this when
    /// <see cref="HasDiagnostics"/> is true; otherwise falls back to
    /// <see cref="FormatErrors"/>.
    /// </para>
    /// </summary>
    public string FormatDiagnostics(SourceMap sources, bool useColor = true)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (_diagnostics.Count == 0) return string.Empty;
        return string.Join("\n\n",
            _diagnostics.Select(d => DiagnosticRenderer.Render(d, sources, useColor)));
    }
}
