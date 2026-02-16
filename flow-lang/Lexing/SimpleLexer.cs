using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem.SpecialTypes;
using System.Text;

namespace FlowLang.Lexing;

/// <summary>
/// Simple manual lexer for the Flow language.
/// </summary>
public class SimpleLexer
{
    private readonly string _source;
    private readonly ErrorReporter _errorReporter;
    private readonly string? _fileName;
    private int _position = 0;
    private int _line = 1;
    private int _column = 1;

    public SimpleLexer(string source, ErrorReporter errorReporter, string? fileName = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _fileName = fileName;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd()) break;

            var token = NextToken();
            if (token != null)
                tokens.Add(token);
        }

        tokens.Add(new Token(TokenType.Eof, "", new SourceLocation(_line, _column, _fileName)));
        return tokens;
    }

    private Token? NextToken()
    {
        var start = new SourceLocation(_line, _column, _fileName);
        char c = Peek();

        // Three-character operators
        if (c == '.' && PeekNext() == '.' && _position + 2 < _source.Length && _source[_position + 2] == '.')
        {
            Advance();
            Advance();
            Advance();
            return new Token(TokenType.Ellipsis, "...", start);
        }

        // Two-character operators
        if (c == '-' && PeekNext() == '>')
        {
            Advance();
            Advance();
            return new Token(TokenType.Arrow, "->", start);
        }

        // Check for special literals that start with +/- before treating them as operators
        // Semitones: +/-Nst (e.g., +1st, -5st)
        // Decibels: +/-NdB (e.g., +6dB, -3dB)
        if ((c == '+' || c == '-') && char.IsDigit(PeekNext()))
        {
            var lookahead = TryLookAheadSpecialLiteral();
            if (lookahead != null)
                return lookahead;
        }

        // Check for specific single-character tokens
        switch (c)
        {
            case '@': return SingleChar(TokenType.At);
            case '=':
                if (PeekNext() == '>')
                {
                    Advance();
                    Advance();
                    return new Token(TokenType.FatArrow, "=>", start);
                }
                return SingleChar(TokenType.Assign);
            case '.': return SingleChar(TokenType.Dot);
            case ':': return SingleChar(TokenType.Colon);
            case '+': return SingleChar(TokenType.Plus);
            case '-': return SingleChar(TokenType.Minus);
            case '*': return SingleChar(TokenType.Star);
            case '/': return SingleChar(TokenType.Slash);
            case '(': return SingleChar(TokenType.LParen);
            case ')': return SingleChar(TokenType.RParen);
            case '[': return SingleChar(TokenType.LBracket);
            case ']': return SingleChar(TokenType.RBracket);
            case '{': return SingleChar(TokenType.LBrace);
            case '}': return SingleChar(TokenType.RBrace);
            case '|': return SingleChar(TokenType.Pipe);
            case '~': return SingleChar(TokenType.Tilde);
            case '_':
                // Standalone underscore is a rest token; if followed by word characters it's part of an identifier
                if (IsAtEnd() || !char.IsLetterOrDigit(PeekNext()))
                    return SingleChar(TokenType.Underscore);
                break; // Fall through to identifier scanning
            case ',': return SingleChar(TokenType.Comma);
            case ';': return SingleChar(TokenType.Semicolon);
            case '<': return SingleChar(TokenType.LessThan);
            case '>': return SingleChar(TokenType.GreaterThan);
            case '"': return ScanString(start);
        }

        // Numbers start with digits - could be part of time/decibel literals
        if (char.IsDigit(c))
            return ScanNumberOrSpecialLiteral(start);

        // Everything else is an identifier (any character that's not whitespace or reserved)
        if (!IsAtEnd())
            return ScanIdentifierOrKeyword(start);

        throw new Exception($"Unexpected end of input at {start}");
    }

    private Token SingleChar(TokenType type)
    {
        var start = new SourceLocation(_line, _column, _fileName);
        char c = Advance();
        return new Token(type, c.ToString(), start);
    }

    private Token ScanString(SourceLocation start)
    {
        Advance(); // Skip opening quote
        var sb = new StringBuilder();

        while (!IsAtEnd() && Peek() != '"')
        {
            if (Peek() == '\\')
            {
                Advance();
                if (IsAtEnd()) break;

                char escaped = Advance();
                sb.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => escaped
                });
            }
            else
            {
                sb.Append(Advance());
            }
        }

        if (IsAtEnd())
        {
            _errorReporter.ReportError("Unterminated string literal", start);
            var partialValue = sb.ToString();
            return new Token(TokenType.StringLiteral, $"\"{partialValue}\"", start, partialValue);
        }

        Advance(); // Skip closing quote

        var value = sb.ToString();
        return new Token(TokenType.StringLiteral, $"\"{value}\"", start, value);
    }

    private Token ScanNumber(SourceLocation start)
    {
        var sb = new StringBuilder();

        while (!IsAtEnd() && char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }

        // Check for float
        if (!IsAtEnd() && Peek() == '.' && char.IsDigit(PeekNext()))
        {
            sb.Append(Advance()); // Consume '.'

            while (!IsAtEnd() && char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }

            var floatValue = double.Parse(sb.ToString());
            return new Token(TokenType.FloatLiteral, sb.ToString(), start, floatValue);
        }

        var intValue = int.Parse(sb.ToString());
        return new Token(TokenType.IntLiteral, sb.ToString(), start, intValue);
    }

    private Token? TryLookAheadSpecialLiteral()
    {
        // Try to match +/-Nst or +/-NdB or +/-Nms or +/-Ns
        var start = new SourceLocation(_line, _column, _fileName);
        int savePos = _position;
        int saveLine = _line;
        int saveCol = _column;

        var sb = new StringBuilder();

        // Consume sign
        sb.Append(Advance());

        // Consume digits
        if (!char.IsDigit(Peek()))
        {
            // Rewind
            _position = savePos;
            _line = saveLine;
            _column = saveCol;
            return null;
        }

        while (!IsAtEnd() && char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }

        // Check for decimal point (for time/decibel values)
        if (!IsAtEnd() && Peek() == '.' && char.IsDigit(PeekNext()))
        {
            sb.Append(Advance()); // Consume '.'
            while (!IsAtEnd() && char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }

        // Check for suffix
        var text = sb.ToString();

        // Try "st" suffix (semitone)
        if (!IsAtEnd() && Peek() == 's' && PeekNext() == 't')
        {
            sb.Append(Advance());
            sb.Append(Advance());
            text = sb.ToString();

            // Parse as semitone
            string numberPart = text.Substring(0, text.Length - 2);
            if (int.TryParse(numberPart, out int semitoneValue))
            {
                return new Token(TokenType.SemitoneLiteral, text, start, semitoneValue);
            }
        }

        // Try "c" suffix (cent - microtone)
        if (!IsAtEnd() && Peek() == 'c' && !char.IsLetter(PeekNext()))
        {
            sb.Append(Advance());
            text = sb.ToString();

            // Parse as cent
            string numberPart = text.Substring(0, text.Length - 1);
            if (double.TryParse(numberPart, out double centValue))
            {
                return new Token(TokenType.CentLiteral, text, start, centValue);
            }
        }

        // Try "dB" suffix (decibel)
        if (!IsAtEnd() && Peek() == 'd' && PeekNext() == 'B')
        {
            sb.Append(Advance());
            sb.Append(Advance());
            text = sb.ToString();

            // Parse as decibel
            string numberPart = text.Substring(0, text.Length - 2);
            if (double.TryParse(numberPart, out double decibelValue))
            {
                return new Token(TokenType.DecibelLiteral, text, start, decibelValue);
            }
        }

        // Try "ms" suffix (milliseconds)
        if (!IsAtEnd() && Peek() == 'm' && PeekNext() == 's')
        {
            sb.Append(Advance());
            sb.Append(Advance());
            text = sb.ToString();

            // Parse as milliseconds
            string numberPart = text.Substring(0, text.Length - 2);
            if (double.TryParse(numberPart, out double msValue))
            {
                return new Token(TokenType.TimeLiteral, text, start, msValue);
            }
        }

        // Try "s" suffix (seconds) - but not if followed by 't' (that would be part of 'st')
        if (!IsAtEnd() && Peek() == 's' && PeekNext() != 't')
        {
            sb.Append(Advance());
            text = sb.ToString();

            // Parse as seconds
            string numberPart = text.Substring(0, text.Length - 1);
            if (double.TryParse(numberPart, out double sValue))
            {
                return new Token(TokenType.TimeLiteral, text, start, sValue);
            }
        }

        // Not a special literal - rewind
        _position = savePos;
        _line = saveLine;
        _column = saveCol;
        return null;
    }

    private Token ScanNumberOrSpecialLiteral(SourceLocation start)
    {
        var sb = new StringBuilder();

        // Consume digits
        while (!IsAtEnd() && char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }

        // Check for float
        if (!IsAtEnd() && Peek() == '.' && char.IsDigit(PeekNext()))
        {
            sb.Append(Advance()); // Consume '.'

            while (!IsAtEnd() && char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }

        var numberText = sb.ToString();

        // Check for special suffixes (ms, s, dB, c) - NOT st because that requires a sign
        if (!IsAtEnd())
        {
            // Try "ms" suffix (milliseconds)
            if (Peek() == 'm' && PeekNext() == 's')
            {
                sb.Append(Advance());
                sb.Append(Advance());
                var text = sb.ToString();

                string numberPart = text.Substring(0, text.Length - 2);
                if (double.TryParse(numberPart, out double msValue))
                {
                    return new Token(TokenType.TimeLiteral, text, start, msValue);
                }
            }
            // Try "dB" suffix (decibel) - for unsigned decibels like 0dB
            else if (Peek() == 'd' && PeekNext() == 'B')
            {
                sb.Append(Advance());
                sb.Append(Advance());
                var text = sb.ToString();

                string numberPart = text.Substring(0, text.Length - 2);
                if (double.TryParse(numberPart, out double dbValue))
                {
                    return new Token(TokenType.DecibelLiteral, text, start, dbValue);
                }
            }
            // Try "c" suffix (cent) - but not if followed by a letter (could be 'c' in a longer identifier)
            else if (Peek() == 'c' && !char.IsLetter(PeekNext()))
            {
                sb.Append(Advance());
                var text = sb.ToString();

                string numberPart = text.Substring(0, text.Length - 1);
                if (double.TryParse(numberPart, out double centValue))
                {
                    return new Token(TokenType.CentLiteral, text, start, centValue);
                }
            }
            // Try "s" suffix (seconds) - but not if followed by 't'
            else if (Peek() == 's' && PeekNext() != 't')
            {
                sb.Append(Advance());
                var text = sb.ToString();

                string numberPart = text.Substring(0, text.Length - 1);
                if (double.TryParse(numberPart, out double sValue))
                {
                    return new Token(TokenType.TimeLiteral, text, start, sValue);
                }
            }
        }

        // Regular number (int or float)
        if (numberText.Contains('.'))
        {
            var floatValue = double.Parse(numberText);
            return new Token(TokenType.FloatLiteral, numberText, start, floatValue);
        }
        else
        {
            var intValue = int.Parse(numberText);
            return new Token(TokenType.IntLiteral, numberText, start, intValue);
        }
    }

    private Token ScanIdentifierOrKeyword(SourceLocation start)
    {
        var sb = new StringBuilder();

        // Consume characters until we hit whitespace or a token boundary
        while (!IsAtEnd())
        {
            char c = Peek();

            if (char.IsWhiteSpace(c) || IsTokenBoundary(c))
                break;

            sb.Append(Advance());
        }

        var text = sb.ToString();

        // Special case: Check if this looks like a note (A-G + digits) followed by alteration (+/-)
        // We need to peek ahead for alterations because +/- are token boundaries
        if (text.Length >= 2)
        {
            char firstChar = char.ToUpper(text[0]);
            if (firstChar >= 'A' && firstChar <= 'G' && char.IsDigit(text[1]))
            {
                // This could be a note like A3, check for alteration
                if (!IsAtEnd() && (Peek() == '+' || Peek() == '-'))
                {
                    char alterationChar = Peek();
                    sb.Append(Advance()); // Consume first +/-

                    // Check for double alteration (++ or --)
                    if (!IsAtEnd() && Peek() == alterationChar)
                    {
                        sb.Append(Advance()); // Consume second +/-
                    }

                    text = sb.ToString();
                }
            }
        }

        if (string.IsNullOrEmpty(text))
            throw new Exception($"Empty identifier at {start}");

        // Check for keywords
        var type = text switch
        {
            "proc" => TokenType.Proc,
            "end" => TokenType.EndProc,
            "return" => TokenType.Return,
            "use" => TokenType.Use,
            "internal" => TokenType.Internal,
            "lazy" => TokenType.Lazy,
            "fn" => TokenType.Fn,
            "timesig" => TokenType.Timesig,
            "tempo" => TokenType.Tempo,
            "swing" => TokenType.Swing,
            "key" => TokenType.Key,
            "section" => TokenType.Section,
            "dynamics" => TokenType.Dynamics,
            "Void" => TokenType.Void,
            "Int" => TokenType.Int,
            "Float" => TokenType.Float,
            "Long" => TokenType.Long,
            "Double" => TokenType.Double,
            "String" => TokenType.String,
            "Bool" => TokenType.Bool,
            "Number" => TokenType.Number,
            "buf" => TokenType.Buf,
            "true" => TokenType.BoolLiteral,
            "false" => TokenType.BoolLiteral,
            _ => TokenType.Identifier
        };

        object? value = type == TokenType.BoolLiteral ? (text == "true") : null;

        // If it's an identifier, check if it's a special literal
        if (type == TokenType.Identifier)
        {
            // Try to parse as Note (A-G followed by optional octave and alteration)
            if (TryParseNote(text, out var noteValue))
            {
                return new Token(TokenType.NoteLiteral, text, start, noteValue);
            }

            // Check for note + duration suffix (e.g., C4h, D5q, E3w)
            // The duration suffix (w/h/q/e/s/t) gets consumed as part of the identifier
            // but should be a separate token for the parser's TryParseDurationSuffix
            if (text.Length >= 3)
            {
                char lastChar = text[^1];
                if (lastChar is 'w' or 'h' or 'q' or 'e' or 's' or 't')
                {
                    string notePartText = text[..^1];
                    if (TryParseNote(notePartText, out var notePartValue))
                    {
                        // Rewind position by 1 so the duration suffix becomes a separate token
                        _position--;
                        _column--;
                        return new Token(TokenType.NoteLiteral, notePartText, start, notePartValue);
                    }
                }
            }

            // Try to parse as Semitone (+/-Nst)
            if (TryParseSemitone(text, out var semitoneValue))
            {
                return new Token(TokenType.SemitoneLiteral, text, start, semitoneValue);
            }

            // Try to parse as Time (Nms or Ns)
            if (TryParseTime(text, out var timeValue, out var timeUnit))
            {
                return new Token(TokenType.TimeLiteral, text, start, timeValue);
            }

            // Try to parse as Decibel (+/-NdB)
            if (TryParseDecibel(text, out var decibelValue))
            {
                return new Token(TokenType.DecibelLiteral, text, start, decibelValue);
            }

            // Try to parse as Chord (Cmaj7, Dm, Gsus4, etc.)
            if (ChordParser.IsChordSymbol(text))
            {
                return new Token(TokenType.ChordLiteral, text, start, text);
            }
        }

        return new Token(type, text, start, value);
    }

    private bool TryParseNote(string text, out string noteValue)
    {
        noteValue = text;

        if (text.Length == 0)
            return false;

        // Only recognize uppercase note names as note literals (A-G)
        // Lowercase names like c4, d4 are treated as identifiers (variable names)
        char firstChar = text[0];
        if (firstChar < 'A' || firstChar > 'G')
            return false;

        // Don't tokenize bare single letters as notes - they could be variable names
        // Only recognize as note literal if it has:
        // 1. An octave number (A4, C3, etc.)
        // 2. An alteration (A+, C--, etc.)
        if (text.Length == 1)
            return false;

        try
        {
            // Use the NoteType.Parse method to validate
            var (note, octave, alteration) = NoteType.Parse(text);
            // Store the original text as the value
            noteValue = text;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryParseSemitone(string text, out int semitoneValue)
    {
        semitoneValue = 0;

        // Semitone format: +Nst or -Nst
        if (!text.EndsWith("st"))
            return false;

        string numberPart = text.Substring(0, text.Length - 2);
        if (string.IsNullOrEmpty(numberPart))
            return false;

        // Must start with + or -
        if (numberPart[0] != '+' && numberPart[0] != '-')
            return false;

        if (int.TryParse(numberPart, out semitoneValue))
            return true;

        return false;
    }

    private bool TryParseTime(string text, out double timeValue, out string unit)
    {
        timeValue = 0;
        unit = "";

        // Time format: Nms or Ns
        if (text.EndsWith("ms"))
        {
            string numberPart = text.Substring(0, text.Length - 2);
            if (double.TryParse(numberPart, out timeValue))
            {
                unit = "ms";
                return true;
            }
        }
        else if (text.EndsWith("s") && !text.EndsWith("ms"))
        {
            string numberPart = text.Substring(0, text.Length - 1);
            if (double.TryParse(numberPart, out timeValue))
            {
                unit = "s";
                return true;
            }
        }

        return false;
    }

    private bool TryParseDecibel(string text, out double decibelValue)
    {
        decibelValue = 0;

        // Decibel format: +/-NdB or NdB
        if (!text.EndsWith("dB"))
            return false;

        string numberPart = text.Substring(0, text.Length - 2);
        if (string.IsNullOrEmpty(numberPart))
            return false;

        if (double.TryParse(numberPart, out decibelValue))
            return true;

        return false;
    }

    private bool IsTokenBoundary(char c)
    {
        return c is '@' or '=' or ':' or '+' or '-' or '*' or '/' or '.'
            or '(' or ')' or '[' or ']' or '{' or '}' or ',' or ';' or '"'
            or '<' or '>' or '|' or '~';
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            char c = Peek();

            if (char.IsWhiteSpace(c))
            {
                Advance();
            }
            else if (c == '\\' && PeekNext() == '\n')
            {
                // Line continuation: backslash followed by newline
                Advance(); // Skip backslash
                Advance(); // Skip newline
                // Continue processing (whitespace will be skipped in next iteration)
            }
            else if (c == '\\' && PeekNext() == '\r' && _position + 2 < _source.Length && _source[_position + 2] == '\n')
            {
                // Line continuation: backslash followed by CRLF (Windows line endings)
                Advance(); // Skip backslash
                Advance(); // Skip \r
                Advance(); // Skip \n
            }
            else if (c == 'N' && IsStartOfLineContent() && _source.Substring(_position).StartsWith("Note:"))
            {
                // Skip comment until end of line
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }
            }
            else
            {
                break;
            }
        }
    }

    private bool IsStartOfLineContent()
    {
        // Check if all preceding characters on the current line are whitespace
        for (int i = _position - 1; i >= 0; i--)
        {
            char ch = _source[i];
            if (ch == '\n') return true; // Reached start of line, all whitespace before us
            if (!char.IsWhiteSpace(ch)) return false; // Non-whitespace found before us
        }
        return true; // Reached start of source
    }

    private char Peek() => IsAtEnd() ? '\0' : _source[_position];
    private char PeekNext() => _position + 1 >= _source.Length ? '\0' : _source[_position + 1];

    private char Advance()
    {
        char c = _source[_position++];

        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        return c;
    }

    private bool IsAtEnd() => _position >= _source.Length;
}
