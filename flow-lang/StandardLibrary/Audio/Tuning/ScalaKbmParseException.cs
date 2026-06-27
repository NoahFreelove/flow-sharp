using System.Globalization;
using FlowLang.Parsing;

namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Diagnostic exception raised by <see cref="ScalaKbmParser"/> when a <c>.kbm</c>
/// file fails to satisfy the Huygens-Fokker spec. Distinct type (not a reuse of
/// <see cref="ScalaParseException"/>) so callers can <c>Assert.Throws&lt;…&gt;</c>
/// per format. Same message shape: em-dash U+2014 separator, quoted 'found'.
/// </summary>
public sealed class ScalaKbmParseException : ParseException
{
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }
    public string Expected { get; }
    public string Found { get; }

    public ScalaKbmParseException(string filePath, int line, int column, string expected, string found)
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
