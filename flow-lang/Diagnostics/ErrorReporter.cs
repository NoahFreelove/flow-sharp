namespace FlowLang.Diagnostics;

/// <summary>
/// Collects and reports errors during compilation and execution.
/// </summary>
public class ErrorReporter
{
    private readonly List<FlowError> _errors = [];

    public IReadOnlyList<FlowError> Errors => _errors;

    public bool HasErrors => _errors.Any(e => e.Level == DiagnosticLevel.Error);

    public void Report(FlowError error)
    {
        _errors.Add(error);
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
    }

    public string FormatErrors()
    {
        return string.Join("\n", _errors.Select(e => e.ToString()));
    }
}
