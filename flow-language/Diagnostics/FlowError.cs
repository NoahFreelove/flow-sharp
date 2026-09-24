using FlowLang.Core;

namespace FlowLang.Diagnostics;

/// <summary>
/// Represents an error or diagnostic message with source location.
/// </summary>
public record FlowError(
    DiagnosticLevel Level,
    string Message,
    SourceLocation Location,
    Exception? InnerException = null)
{
    public static FlowError Create(string message, SourceLocation? location = null)
        => new(DiagnosticLevel.Error, message, location ?? SourceLocation.Unknown);

    public static FlowError Warning(string message, SourceLocation? location = null)
        => new(DiagnosticLevel.Warning, message, location ?? SourceLocation.Unknown);

    public static FlowError Info(string message, SourceLocation? location = null)
        => new(DiagnosticLevel.Info, message, location ?? SourceLocation.Unknown);

    public override string ToString()
    {
        var levelStr = Level switch
        {
            DiagnosticLevel.Error => "error",
            DiagnosticLevel.Warning => "warning",
            DiagnosticLevel.Info => "info",
            _ => "unknown"
        };

        return $"{Location}: {levelStr}: {Message}";
    }
}
