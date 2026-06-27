using System.Globalization;
using FlowLang.Parsing;

namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Diagnostic exception raised by <see cref="ScalaParser"/> when a <c>.scl</c>
/// file fails to satisfy the Huygens-Fokker spec. Extends Flow's existing
/// <see cref="ParseException"/> (TypeParser.cs:335) so callers can catch the
/// shared base type, and so the diagnostic format matches the rest of the
/// language (em-dash U+2014 separator, quoted 'found' token).
/// </summary>
public sealed class ScalaParseException : ParseException
{
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }
    public string Expected { get; }
    public string Found { get; }

    public ScalaParseException(string filePath, int line, int column, string expected, string found)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1}:{2} — expected {3}, got '{4}'",
            filePath, line, column, expected, found))
    {
        FilePath = filePath;
        Line = line;
        Column = column;
        Expected = expected;
        Found = found;
    }
}
