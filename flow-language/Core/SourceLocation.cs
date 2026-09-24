namespace FlowLang.Core;

/// <summary>
/// Represents a location in source code for error reporting and debugging.
/// </summary>
public record SourceLocation(int Line, int Column, string? FileName = null)
{
    public static SourceLocation Unknown { get; } = new(0, 0, null);

    public override string ToString()
    {
        if (FileName != null)
            return $"{FileName}:{Line}:{Column}";
        return $"{Line}:{Column}";
    }
}
