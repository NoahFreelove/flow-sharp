using FlowLang.Core;
using FlowLang.Diagnostics;
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

        // Check for specific single-character tokens
        switch (c)
        {
            case '@': return SingleChar(TokenType.At);
            case '=': return SingleChar(TokenType.Assign);
            case ':': return SingleChar(TokenType.Colon);
            case '+': return SingleChar(TokenType.Plus);
            case '-': return SingleChar(TokenType.Minus);
            case '*': return SingleChar(TokenType.Star);
            case '/': return SingleChar(TokenType.Slash);
            case '(': return SingleChar(TokenType.LParen);
            case ')': return SingleChar(TokenType.RParen);
            case '[': return SingleChar(TokenType.LBracket);
            case ']': return SingleChar(TokenType.RBracket);
            case ',': return SingleChar(TokenType.Comma);
            case ';': return SingleChar(TokenType.Semicolon);
            case '"': return ScanString(start);
        }

        // Numbers start with digits
        if (char.IsDigit(c))
            return ScanNumber(start);

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
            throw new Exception($"Unterminated string at {start}");

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

        return new Token(type, text, start, value);
    }

    private bool IsTokenBoundary(char c)
    {
        return c is '@' or '=' or ':' or '+' or '-' or '*' or '/' or '.'
            or '(' or ')' or '[' or ']' or ',' or ';' or '"';
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
            else if (c == 'N' && _source.Substring(_position).StartsWith("Note:"))
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
