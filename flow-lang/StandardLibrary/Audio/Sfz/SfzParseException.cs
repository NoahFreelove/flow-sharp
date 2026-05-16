using System.Globalization;
using FlowLang.Parsing;

namespace FlowLang.StandardLibrary.Audio.Sfz;

/// <summary>
/// Phase 33 — diagnostic exception raised by <c>SfzParser</c> (Plan 33-04) when
/// a <c>.sfz</c> file fails to parse. Mirrors
/// <see cref="FlowLang.StandardLibrary.Audio.Tuning.ScalaParseException"/>:
/// extends Flow's existing <see cref="ParseException"/> base
/// (<c>flow-lang/Parsing/TypeParser.cs:339</c>) so callers can catch the shared
/// language-wide parse-error type, and so the diagnostic format matches the
/// rest of the language (em-dash <c>U+2014</c> separator, quoted <c>'got'</c>
/// token).
///
/// Format: <c>{filePath}:{line}:{column} — expected {expected} got '{got}'</c>.
/// </summary>
public sealed class SfzParseException : ParseException
{
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }
    public string Expected { get; }
    public string Got { get; }

    public SfzParseException(string filePath, int line, int column, string expected, string got)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1}:{2} — expected {3} got '{4}'",
            filePath, line, column, expected, got))
    {
        FilePath = filePath;
        Line = line;
        Column = column;
        Expected = expected;
        Got = got;
    }
}
