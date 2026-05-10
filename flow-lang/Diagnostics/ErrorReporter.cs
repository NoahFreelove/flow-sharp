namespace FlowLang.Diagnostics;

/// <summary>
/// Collects and reports errors during compilation and execution.
/// </summary>
public class ErrorReporter
{
    private readonly List<FlowError> _errors = [];
    private bool _hasErrors = false;
    private const int MaxErrorCount = 50;

    public IReadOnlyList<FlowError> Errors => _errors;

    public bool HasErrors => _hasErrors;

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
        _hasErrors = false;
    }

    public string FormatErrors()
    {
        return string.Join("\n", _errors.Select(e => e.ToString()));
    }
}
