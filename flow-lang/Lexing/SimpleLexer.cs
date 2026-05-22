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
    private readonly PragmaSet _pragmaSet;
    private int _position = 0;
    private int _line = 1;
    private int _column = 1;
    private readonly Queue<Token> _pendingTokens = new();
    private TokenType? _lastEmittedType = null;   // Phase 26 D-04

    public SimpleLexer(string source, ErrorReporter errorReporter, string? fileName = null,
                       PragmaSet? pragmaSet = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _fileName = fileName;
        // Phase 21 Plan 21-01: field is wired for Plan 21-02's TryParseNote H→B substitution.
        // TryParseNote does NOT yet read _pragmaSet in this plan.
        _pragmaSet = pragmaSet ?? PragmaSet.Empty;
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
            {
                tokens.Add(token);
                _lastEmittedType = token.Type;   // Phase 26 D-04
            }
        }

        // Phase 35 LANG-04 Wave 1: EOF is zero-width at the post-source position.
        var eofLoc = new SourceLocation(_line, _column, _fileName);
        tokens.Add(new Token(TokenType.Eof, "", eofLoc, Span: Span.At(eofLoc)));
        return tokens;
    }

    private Token? NextToken()
    {
        // Return pending tokens from multi-token productions (e.g., interpolated strings)
        if (_pendingTokens.Count > 0)
            return _pendingTokens.Dequeue();

        var start = new SourceLocation(_line, _column, _fileName);
        char c = Peek();

        // Three-character operators
        if (c == '.' && PeekNext() == '.' && _position + 2 < _source.Length && _source[_position + 2] == '.')
        {
            Advance();
            Advance();
            Advance();
            return new Token(TokenType.Ellipsis, "...", start, Span: new Span(start, CurrentLocation()));
        }

        // Two-character operators
        if (c == '-' && PeekNext() == '>')
        {
            Advance();
            Advance();
            return new Token(TokenType.Arrow, "->", start, Span: new Span(start, CurrentLocation()));
        }

        // Phase 26.1 TUP-10: TildeArrow `~>` (tuple-unpack flow operator).
        // CRITICAL placement: this two-char check MUST precede the single-char
        // `case '~': return SingleChar(TokenType.Tilde);` arm below, otherwise
        // `~>` lexes as Tilde + GreaterThan and the parser never sees `~>`.
        // Note-stream tied notes (`C4h~`) are unaffected — they use a bare `~`
        // not followed by `>`, so the single-char arm continues to fire.
        if (c == '~' && PeekNext() == '>')
        {
            Advance();
            Advance();
            return new Token(TokenType.TildeArrow, "~>", start, Span: new Span(start, CurrentLocation()));
        }

        // Check for special literals that start with +/- before treating them as operators
        // Semitones: +/-Nst (e.g., +1st, -5st)
        // Decibels: +/-NdB (e.g., +6dB, -3dB)
        if ((c == '+' || c == '-') && char.IsDigit(PeekNext()))
        {
            // Step 1: typed literals (existing — preserves -3dB/-5st/+50c)
            var lookahead = TryLookAheadSpecialLiteral();
            if (lookahead != null)
                return lookahead;

            // Step 2 (Phase 26 D-04): plain signed number at expression-start
            var signed = TryLexSignedNumber(start);
            if (signed != null) return signed;

            // Step 3: fall through to SingleChar(Plus/Minus) preserves musical-context
            // sign consumption + parser shorthand `-IDENT`.
        }

        // Interpolated string: $"..."
        if (c == '$' && PeekNext() == '"')
        {
            return ScanInterpolatedString(start);
        }

        // Phase 26.1 TUP-09: two-char `<<` / `>>` (tuple delimiters) at expression-start positions.
        // MUST be checked BEFORE the single-char `case '<' / '>':` arms below, otherwise the
        // single-char arm consumes one `<` or `>` and the second char lexes alone, breaking
        // tuple parsing. Mirrors the Arrow (`->`) two-char dispatch above (lines 75-80).
        // Note-stream `>` accent (e.g. `| C4q> D4q |`) is preserved by the PeekNext-equality
        // gate inside TryLexAngleAngle: when the second char is NOT another `>`, the helper
        // returns null and control falls through to the single-char `case '>':` arm.
        {
            var aa = TryLexAngleAngle(start, c);
            if (aa is not null) return aa;
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
                    return new Token(TokenType.FatArrow, "=>", start, Span: new Span(start, CurrentLocation()));
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
                // Standalone underscore is a rest token; if followed by word
                // characters OR another underscore (e.g. `__enableSfzModule` per
                // Phase 33 internal-marker naming) it's part of an identifier.
                if (IsAtEnd() || (!char.IsLetterOrDigit(PeekNext()) && PeekNext() != '_'))
                    return SingleChar(TokenType.Underscore);
                break; // Fall through to identifier scanning
            case ',': return SingleChar(TokenType.Comma);
            case ';': return SingleChar(TokenType.Semicolon);
            case '<': return SingleChar(TokenType.LessThan);
            case '>': return SingleChar(TokenType.GreaterThan);
            case '"': return ScanString(start);
            case '#':
            {
                // Phase 26.1 SYM-01: `#identifier` lexes as a single SymbolLiteral token.
                // The leading '#' is a token boundary; the lexeme is the body without '#'.
                Advance(); // consume '#'
                var sb = new StringBuilder();
                while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
                {
                    sb.Append(Peek());
                    Advance();
                }
                if (sb.Length == 0)
                    throw new Exception($"Expected identifier after '#' at {start}");
                return new Token(TokenType.SymbolLiteral, sb.ToString(), start, Span: new Span(start, CurrentLocation()));
            }
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
        // Phase 35 LANG-04 Wave 1: single-char tokens use a zero-width Span.At(start)
        // per PATTERNS.md Bucket 1 § SimpleLexer.cs note (single-char SingleChar arm).
        return new Token(type, c.ToString(), start, Span: Span.At(start));
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
            return new Token(TokenType.StringLiteral, $"\"{partialValue}\"", start, partialValue, Span: new Span(start, CurrentLocation()));
        }

        Advance(); // Skip closing quote

        var value = sb.ToString();
        return new Token(TokenType.StringLiteral, $"\"{value}\"", start, value, Span: new Span(start, CurrentLocation()));
    }

    private Token ScanInterpolatedString(SourceLocation start)
    {
        Advance(); // Skip '$'
        Advance(); // Skip '"'

        var tokens = new List<Token>();
        // Phase 35 LANG-04: `$"` delimiter is 2 chars; capture end at current pos.
        tokens.Add(new Token(TokenType.InterpolatedStringStart, "$\"", start, Span: new Span(start, CurrentLocation())));

        var textSb = new StringBuilder();

        while (!IsAtEnd() && Peek() != '"')
        {
            if (Peek() == '\\')
            {
                Advance(); // Skip backslash
                if (IsAtEnd()) break;

                char escaped = Advance();
                textSb.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    '{' => '{',
                    '}' => '}',
                    _ => escaped
                });
            }
            else if (Peek() == '{')
            {
                // Flush accumulated text as InterpolatedStringText
                if (textSb.Length > 0)
                {
                    var textValue = textSb.ToString();
                    var textTokLoc = new SourceLocation(_line, _column, _fileName);
                    tokens.Add(new Token(TokenType.InterpolatedStringText, textValue,
                        textTokLoc, textValue, Span: Span.At(textTokLoc)));
                    textSb.Clear();
                }

                Advance(); // Skip '{'

                // Lex the expression tokens until matching '}'
                int braceDepth = 1;
                while (!IsAtEnd() && braceDepth > 0)
                {
                    SkipWhitespaceAndComments();
                    if (IsAtEnd()) break;

                    if (Peek() == '}')
                    {
                        braceDepth--;
                        if (braceDepth == 0)
                        {
                            Advance(); // Skip closing '}'
                            break;
                        }
                    }
                    else if (Peek() == '{')
                    {
                        // Nested braces not supported - report error
                        _errorReporter.ReportError("Nested braces not supported in string interpolation",
                            new SourceLocation(_line, _column, _fileName));
                        Advance();
                        break;
                    }

                    var exprToken = NextToken();
                    if (exprToken != null)
                        tokens.Add(exprToken);
                }
            }
            else
            {
                textSb.Append(Advance());
            }
        }

        // Flush remaining text
        if (textSb.Length > 0)
        {
            var textValue = textSb.ToString();
            var textTokLoc = new SourceLocation(_line, _column, _fileName);
            tokens.Add(new Token(TokenType.InterpolatedStringText, textValue,
                textTokLoc, textValue, Span: Span.At(textTokLoc)));
        }

        if (!IsAtEnd())
            Advance(); // Skip closing '"'

        var endTokLoc = new SourceLocation(_line, _column, _fileName);
        tokens.Add(new Token(TokenType.InterpolatedStringEnd, "\"",
            endTokLoc, Span: Span.At(endTokLoc)));

        // Return the first token, enqueue the rest
        for (int i = 1; i < tokens.Count; i++)
            _pendingTokens.Enqueue(tokens[i]);

        return tokens[0];
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

            var floatValue = double.Parse(sb.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            return new Token(TokenType.FloatLiteral, sb.ToString(), start, floatValue, Span: new Span(start, CurrentLocation()));
        }

        // Phase 26: int-overflow → long-overflow → BigInteger fallthrough.
        string text = sb.ToString();
        if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int intValue))
            return new Token(TokenType.IntLiteral, text, start, intValue, Span: new Span(start, CurrentLocation()));
        if (long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long longValue))
            return new Token(TokenType.IntLiteral, text, start, longValue, Span: new Span(start, CurrentLocation()));
        return new Token(TokenType.IntLiteral, text, start,
            System.Numerics.BigInteger.Parse(text, System.Globalization.CultureInfo.InvariantCulture),
            Span: new Span(start, CurrentLocation()));
    }

    /// <summary>
    /// Phase 26.1 TUP-09 (revision 1): emit two-char <c>&lt;&lt;</c> / <c>&gt;&gt;</c> at expression-start positions.
    /// Returns null when the position is not expression-start, when the second char doesn't
    /// match the first (so note-stream `>` accent at <c>| C4q&gt; D4q |</c> falls through to
    /// the single-char `case '&gt;':` arm), or when the input doesn't start with `&lt;` or `&gt;`.
    /// <para>
    /// The expression-start gate admits all predecessor tokens that can legitimately precede
    /// a tuple literal or destructure pattern:
    /// </para>
    /// <list type="bullet">
    ///   <item>Phase 26 D-04 set: <see cref="TokenType.LParen"/>, <see cref="TokenType.Comma"/>,
    ///   <see cref="TokenType.LBracket"/>, <see cref="TokenType.Arrow"/>, <see cref="TokenType.Assign"/>,
    ///   <see cref="TokenType.Pipe"/>, <see cref="TokenType.Semicolon"/>, <see cref="TokenType.Identifier"/>.</item>
    ///   <item>Revision-1 closing delimiters that end a previous expression:
    ///   <see cref="TokenType.RParen"/>, <see cref="TokenType.RBracket"/>, <see cref="TokenType.RBrace"/>.</item>
    ///   <item>Revision-1 literal-end tokens that end a previous statement (so <c>Int x = 5\n&lt;&lt;Int a&gt;&gt; = ...</c> parses):
    ///   <see cref="TokenType.IntLiteral"/>, <see cref="TokenType.FloatLiteral"/>,
    ///   <see cref="TokenType.StringLiteral"/>, <see cref="TokenType.BoolLiteral"/>,
    ///   <see cref="TokenType.NoteLiteral"/>, <see cref="TokenType.ChordLiteral"/>,
    ///   <see cref="TokenType.SymbolLiteral"/>.</item>
    ///   <item>Revision-1 closing tuple from previous expression: <see cref="TokenType.GreaterGreater"/>.</item>
    /// </list>
    /// <para>
    /// LongLiteral/DoubleLiteral are not present in this lexer — large/wide numerics flow
    /// through <see cref="TokenType.IntLiteral"/> / <see cref="TokenType.FloatLiteral"/> with
    /// long/BigInteger/double Value payloads (see ScanNumber/ScanNumberOrSpecialLiteral),
    /// so they're already covered by the IntLiteral/FloatLiteral entries above.
    /// </para>
    /// </summary>
    private Token? TryLexAngleAngle(SourceLocation start, char c)
    {
        if (c != '<' && c != '>') return null;
        if (PeekNext() != c) return null;

        bool isExprStart = _lastEmittedType is null
            // Original Phase 26 D-04 set:
            or TokenType.LParen or TokenType.Comma or TokenType.LBracket
            or TokenType.Arrow or TokenType.Assign or TokenType.Pipe
            or TokenType.Semicolon or TokenType.Identifier
            // Revision 1 — closing delimiters that end a previous expression:
            or TokenType.RParen or TokenType.RBracket or TokenType.RBrace
            // Revision 1 — literal-end tokens that end a previous statement:
            or TokenType.IntLiteral or TokenType.FloatLiteral
            or TokenType.StringLiteral or TokenType.BoolLiteral
            or TokenType.NoteLiteral or TokenType.ChordLiteral
            or TokenType.SymbolLiteral
            // Revision 1 — closing tuple from previous expression:
            or TokenType.GreaterGreater
            // Empty-tuple support: `<<>>` literal evaluates to a 0-arity Tuple, so the
            // closing `>>` immediately following an opening `<<` must lex as a single
            // GreaterGreater token. Also covers the singleton-empty type annotation
            // `Tuple<<>>` (the TypeParser additionally accepts the dual-form for safety).
            or TokenType.LessLess
            // Phase 26.2 ERG-04 (RESEARCH Pitfall 6) — defensive add for music-literal-end positions:
            // a tuple literal can legitimately follow any music-typed value-end (e.g. `<<800Hz, 1200Hz>>`).
            or TokenType.HertzLiteral
            or TokenType.TimeLiteral or TokenType.DecibelLiteral
            or TokenType.CentLiteral or TokenType.SemitoneLiteral;
        if (!isExprStart) return null;

        Advance(); // consume first char
        Advance(); // consume second char
        var tt = c == '<' ? TokenType.LessLess : TokenType.GreaterGreater;
        var lex = c == '<' ? "<<" : ">>";
        return new Token(tt, lex, start, Span: new Span(start, CurrentLocation()));
    }

    private Token? TryLexSignedNumber(SourceLocation start)
    {
        // Phase 26 D-04: expression-start positions only.
        // Music-context keywords (Tempo/Swing/Pan/Gain/ReverbTime) are NOT in this set —
        // their parsers at Parser.cs:450/465/527/542/556 consume Match(TokenType.Minus)
        // directly. RESEARCH Pitfall 1 mitigation.
        // NOTE: Colon is intentionally NOT in this set — proc params (Int: x) are
        // followed by an identifier, never a literal.
        bool isExprStart = _lastEmittedType is null
            or TokenType.LParen
            or TokenType.Comma
            or TokenType.LBracket
            or TokenType.Arrow
            // Phase 36 Plan 36-02 (D-36-11) — named-arg negative-literal support.
            // The named-arg call form `(fn arg=-5)` places `-5` immediately after
            // the Assign token; TokenType.Assign was already in this set as of
            // Phase 26 D-04 for variable-declaration initializers (`Int x = -5`),
            // so named-arg `arg=-5` lexes the negative as a single signed
            // IntLiteral with no additional change to the lexer — verified by
            // NamedArgsParserTests.NegativeLiteralAfterAssign.
            or TokenType.Assign
            or TokenType.Pipe
            or TokenType.Semicolon
            // Phase 26 Wave 0 fact NegativeLiteralLexFacts "after Arrow" position
            // (`5 -> add -3`) places `-3` after Identifier(add) — argument-start
            // position. Including Identifier here lets `func -3` lex as a single
            // signed token so it can flow through ParsePrimary's optional-paren-args.
            or TokenType.Identifier
            // Phase 35 Plan 35-05 (LANG-01): `(match -5 | ... )` places `-5`
            // right after the `match` keyword (scrutinee position). Similarly,
            // `n when -5` could surface a signed literal in a guard, though
            // less common. Both keywords are added to the expression-start
            // set so the lexer produces a single signed-IntLiteral token.
            or TokenType.Match
            or TokenType.When;
        if (!isExprStart) return null;

        int savePos = _position;
        int saveLine = _line;
        int saveCol = _column;

        char sign = Peek();
        if (sign != '+' && sign != '-') return null;
        if (!char.IsDigit(PeekNext())) return null;

        // D-03: '+' at expression-start is silently absorbed — return positive literal.
        bool negative = (sign == '-');
        Advance();   // consume sign

        var sb = new StringBuilder();
        if (negative) sb.Append('-');
        while (!IsAtEnd() && char.IsDigit(Peek()))
            sb.Append(Advance());

        bool isFloat = false;
        if (!IsAtEnd() && Peek() == '.' && char.IsDigit(PeekNext()))
        {
            isFloat = true;
            sb.Append(Advance());                 // consume '.'
            while (!IsAtEnd() && char.IsDigit(Peek()))
                sb.Append(Advance());
        }

        string text = sb.ToString();
        if (isFloat && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dval))
            return new Token(TokenType.FloatLiteral, text, start, dval, Span: new Span(start, CurrentLocation()));
        if (!isFloat)
        {
            if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int ival))
                return new Token(TokenType.IntLiteral, text, start, ival, Span: new Span(start, CurrentLocation()));
            if (long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long lval))
                return new Token(TokenType.IntLiteral, text, start, lval, Span: new Span(start, CurrentLocation()));
            if (System.Numerics.BigInteger.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var bival))
                return new Token(TokenType.IntLiteral, text, start, bival, Span: new Span(start, CurrentLocation()));
        }

        // Parse failure — rewind so SingleChar(Plus/Minus) gets the chance.
        _position = savePos;
        _line = saveLine;
        _column = saveCol;
        return null;
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

        // Phase 26.2 ERG-04: Try "kHz" suffix FIRST (3 chars; must precede Hz to avoid greedy match — RESEARCH Pitfall 4)
        if (!IsAtEnd() && Peek() == 'k' && PeekNext() == 'H' && _position + 2 < _source.Length && _source[_position + 2] == 'z')
        {
            sb.Append(Advance()); // 'k'
            sb.Append(Advance()); // 'H'
            sb.Append(Advance()); // 'z'
            text = sb.ToString();

            string numberPart = text.Substring(0, text.Length - 3);
            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double kHzValue))
            {
                return new Token(TokenType.HertzLiteral, text, start, kHzValue * 1000.0, Span: new Span(start, CurrentLocation()));  // canonical Hz
            }
        }

        // Phase 26.2 ERG-04: Try "Hz" suffix (2 chars) AFTER kHz
        if (!IsAtEnd() && Peek() == 'H' && PeekNext() == 'z')
        {
            sb.Append(Advance()); // 'H'
            sb.Append(Advance()); // 'z'
            text = sb.ToString();

            string numberPart = text.Substring(0, text.Length - 2);
            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hzValue))
            {
                return new Token(TokenType.HertzLiteral, text, start, hzValue, Span: new Span(start, CurrentLocation()));
            }
        }

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
                return new Token(TokenType.SemitoneLiteral, text, start, semitoneValue, Span: new Span(start, CurrentLocation()));
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
                return new Token(TokenType.CentLiteral, text, start, centValue, Span: new Span(start, CurrentLocation()));
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
                return new Token(TokenType.DecibelLiteral, text, start, decibelValue, Span: new Span(start, CurrentLocation()));
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
                return new Token(TokenType.TimeLiteral, text, start, msValue, Span: new Span(start, CurrentLocation()));
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
                return new Token(TokenType.TimeLiteral, text, start, sValue, Span: new Span(start, CurrentLocation()));
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

        // Check for special suffixes (Hz, kHz, ms, s, dB, c) - NOT st because that requires a sign
        if (!IsAtEnd())
        {
            // Phase 26.2 ERG-04: Try "kHz" suffix FIRST (3 chars; must precede Hz to avoid greedy match — RESEARCH Pitfall 4)
            if (Peek() == 'k' && PeekNext() == 'H' && _position + 2 < _source.Length && _source[_position + 2] == 'z')
            {
                sb.Append(Advance()); // 'k'
                sb.Append(Advance()); // 'H'
                sb.Append(Advance()); // 'z'
                var text = sb.ToString();

                if (double.TryParse(numberText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double kHzValue))
                {
                    return new Token(TokenType.HertzLiteral, text, start, kHzValue * 1000.0, Span: new Span(start, CurrentLocation()));  // canonical Hz
                }
            }
            // Phase 26.2 ERG-04: Try "Hz" suffix (2 chars) AFTER kHz
            else if (Peek() == 'H' && PeekNext() == 'z')
            {
                sb.Append(Advance()); // 'H'
                sb.Append(Advance()); // 'z'
                var text = sb.ToString();

                if (double.TryParse(numberText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hzValue))
                {
                    return new Token(TokenType.HertzLiteral, text, start, hzValue, Span: new Span(start, CurrentLocation()));
                }
            }
            // Try "ms" suffix (milliseconds)
            else if (Peek() == 'm' && PeekNext() == 's')
            {
                sb.Append(Advance());
                sb.Append(Advance());
                var text = sb.ToString();

                string numberPart = text.Substring(0, text.Length - 2);
                if (double.TryParse(numberPart, out double msValue))
                {
                    return new Token(TokenType.TimeLiteral, text, start, msValue, Span: new Span(start, CurrentLocation()));
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
                    return new Token(TokenType.DecibelLiteral, text, start, dbValue, Span: new Span(start, CurrentLocation()));
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
                    return new Token(TokenType.CentLiteral, text, start, centValue, Span: new Span(start, CurrentLocation()));
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
                    return new Token(TokenType.TimeLiteral, text, start, sValue, Span: new Span(start, CurrentLocation()));
                }
            }
        }

        // Regular number (int or float)
        if (numberText.Contains('.'))
        {
            var floatValue = double.Parse(numberText, System.Globalization.CultureInfo.InvariantCulture);
            return new Token(TokenType.FloatLiteral, numberText, start, floatValue, Span: new Span(start, CurrentLocation()));
        }
        else
        {
            // Phase 26: int literals that overflow Int32 fall through to IntLiteral
            // with a long-typed Value so the variable-declaration coercion path can
            // widen them to Long/Number. Without this, `Long m = 1000000000000`
            // throws OverflowException at lex time.
            if (int.TryParse(numberText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int intValue))
            {
                return new Token(TokenType.IntLiteral, numberText, start, intValue, Span: new Span(start, CurrentLocation()));
            }
            if (long.TryParse(numberText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long longValue))
            {
                // Lex as IntLiteral with a long Value; the parser/evaluator treats
                // it as a literal whose runtime Value type matches Data's CLR type.
                return new Token(TokenType.IntLiteral, numberText, start, longValue, Span: new Span(start, CurrentLocation()));
            }
            // Fall back to BigInteger for truly huge literals.
            return new Token(TokenType.IntLiteral, numberText, start,
                System.Numerics.BigInteger.Parse(numberText, System.Globalization.CultureInfo.InvariantCulture),
                Span: new Span(start, CurrentLocation()));
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

        // Phase 14 DX-06 (CONTEXT D-07): Pick up any unbounded run of +/- chars
        // following an identifier that begins with a note letter (A-G). 'b' and '#' are
        // already absorbed as part of the identifier scan above; +/- are token boundaries,
        // so they must be peeked explicitly here.
        //
        // Drop the legacy IsDigit(text[1]) gate — bare flats like "Bb" (length 2, no octave
        // digit) must also trigger pickup so later +/- suffixes get glued on.
        // Drop the "double alteration only" bound — the loop is unbounded to support
        // arbitrary compositions like "B+++++".
        //
        // Phase 14 WR-01 fix: Gate the pickup to only note-like shapes so ordinary
        // identifiers beginning with A-G (e.g., foo, attack, bar, decay, enable, flag,
        // gain) do NOT silently glue trailing +/- onto themselves. Note-like shapes are:
        // a single A-G letter, any text containing a digit (octave), or text whose second
        // char is 'b' or '#' (accidental). TryParseNote only accepts these shapes anyway.
        if (text.Length >= 1)
        {
            char firstChar = char.ToUpper(text[0]);
            bool looksNoteLike = firstChar >= 'A' && firstChar <= 'G'
                && (text.Length == 1
                    || text.Any(char.IsDigit)
                    || text[1] == 'b'
                    || text[1] == '#');
            if (looksNoteLike)
            {
                while (!IsAtEnd() && (Peek() == '+' || Peek() == '-'))
                {
                    sb.Append(Advance());
                }
                text = sb.ToString();
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
            "rit" => TokenType.Rit,
            "accel" => TokenType.Accel,
            "pan" => TokenType.Pan,
            "gain" => TokenType.Gain,
            "reverbTime" => TokenType.ReverbTime,
            "voicePool" => TokenType.VoicePool,
            "sustainPedal" => TokenType.SustainPedal,
            "tuning" => TokenType.Tuning,
            "match" => TokenType.Match,
            "when" => TokenType.When,
            "pickup" => TokenType.Pickup,
            "for" => TokenType.For,
            "while" => TokenType.While,
            "break" => TokenType.Break,
            "continue" => TokenType.Continue,
            "in" => TokenType.In,
            "as" => TokenType.As,
            "progression" => TokenType.Progression,
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
            // Phase 14 DX-06 (CONTEXT D-21 + RESEARCH §Regression Risk Analysis):
            // Under the extended NoteType.Parse surface (sum-based alteration scan), inputs
            // that used to error out of TryParseNote may now succeed. Dispatch chord-before-note
            // as defence-in-depth so existing ChordParser symbols always win.
            //
            // ChordParser.IsChordSymbol uses the 's'/'f' accidental convention (Cs, Bf, Fs),
            // NOT 'b'/'#'. Matches include: Dm, Cmaj7, Am7, Bdim, Csmaj, Bfm, Asus4, Gdom7.
            // Plain note literals ("Db4", "Bb", "C4", "F#", "F##4") fail IsChordSymbol and
            // fall through to TryParseNote below.
            // Identifiers like "Bb7" (b-accidental-style, which ChordParser doesn't accept)
            // fall through the chord check and are picked up by TryParseNote as NoteLiteral(B,7,-1).
            if (ChordParser.IsChordSymbol(text))
            {
                return new Token(TokenType.ChordLiteral, text, start, text, Span: new Span(start, CurrentLocation()));
            }

            // Try to parse as Note (A-G followed by optional octave and alteration)
            if (TryParseNote(text, out var noteValue))
            {
                // Phase 21 D-15: when canonicalization happened (text != noteValue —
                // e.g. H4q canonicalized to B4q under enable hAsB;), preserve the
                // composer's original text in OriginalText so diagnostics surface
                // the authored shape. Token.Text always carries the canonical form
                // so renderer/MIDI export consume B-rooted notes unchanged.
                string? originalText = (text != noteValue) ? text : null;
                return new Token(TokenType.NoteLiteral, noteValue, start, noteValue, originalText, Span: new Span(start, CurrentLocation()));
            }

            // Check for note + duration suffix (e.g., C4h, D5q, E3w, F4x for 64th, G5y for 128th)
            // The duration suffix gets consumed as part of the identifier but should be a
            // separate token for the parser's TryParseDurationSuffix.
            if (text.Length >= 3)
            {
                char lastChar = text[^1];
                if (lastChar is 'w' or 'h' or 'q' or 'e' or 's' or 't' or 'x' or 'y')
                {
                    string notePartText = text[..^1];
                    if (TryParseNote(notePartText, out var notePartValue))
                    {
                        // Rewind position by 1 so the duration suffix becomes a separate token
                        _position--;
                        _column--;
                        // Phase 21 D-15 (Pitfall D): inner-call canonicalization path —
                        // when notePartText ("H4") canonicalizes to notePartValue ("B4"),
                        // preserve the original.
                        string? originalText = (notePartText != notePartValue) ? notePartText : null;
                        return new Token(TokenType.NoteLiteral, notePartValue, start, notePartValue, originalText, Span: new Span(start, CurrentLocation()));
                    }
                }
            }

            // Try to parse as Semitone (+/-Nst)
            if (TryParseSemitone(text, out var semitoneValue))
            {
                return new Token(TokenType.SemitoneLiteral, text, start, semitoneValue, Span: new Span(start, CurrentLocation()));
            }

            // Try to parse as Time (Nms or Ns)
            if (TryParseTime(text, out var timeValue, out var timeUnit))
            {
                return new Token(TokenType.TimeLiteral, text, start, timeValue, Span: new Span(start, CurrentLocation()));
            }

            // Try to parse as Decibel (+/-NdB)
            if (TryParseDecibel(text, out var decibelValue))
            {
                return new Token(TokenType.DecibelLiteral, text, start, decibelValue, Span: new Span(start, CurrentLocation()));
            }
        }

        return new Token(type, text, start, value, Span: new Span(start, CurrentLocation()));
    }

    private bool TryParseNote(string text, out string noteValue)
    {
        noteValue = text;

        if (text.Length == 0)
            return false;

        char firstChar = text[0];

        // === Phase 21 D-13: H→B substitution under hAsB pragma ===
        // Pitfall C: bare 'H' (length==1) MUST stay an Identifier so `Int H = 5;`
        // keeps compiling. The standard A-G branch below also rejects 'H' (it's
        // outside the [A,G] range), so this is the only acceptance path for an
        // H-prefixed token.
        if (firstChar == 'H' && _pragmaSet.Has("hAsB") && text.Length > 1)
        {
            var probe = "B" + text[1..];   // canonical
            try
            {
                var (note, octave, alteration) = NoteType.Parse(probe);
                noteValue = probe;          // canonical text returned (B4q etc.)
                return true;
            }
            catch
            {
                // Pitfall E: NoteType.Parse rejects shapes that aren't valid notes
                // (e.g. probe="Bmaj7"). Fall through to return false — Hmaj7 stays
                // an Identifier per D-16.
            }
            return false;
        }

        // Only recognize uppercase note names as note literals (A-G)
        // Lowercase names like c4, d4 are treated as identifiers (variable names)
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
        // Phase 26.1 SYM-01: NOTE that '#' is intentionally NOT a token boundary here.
        // Note literals like `C#4`, `F#4`, chord symbols like `C#m7`, and bare flats like
        // `Bb` rely on `#` being absorbed mid-identifier in ScanIdentifierOrKeyword. The
        // `case '#'` branch in NextToken (above) already handles `#identifier` when `#`
        // is the FIRST character (no preceding identifier text), which is the only context
        // a Symbol literal can occur in.
        return c is '@' or '=' or ':' or '+' or '-' or '*' or '/' or '.'
            or '(' or ')' or '[' or ']' or '{' or '}' or ',' or ';' or '"'
            or '<' or '>' or '|' or '~' or '$';
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
                // Keep the logical line number so the parser's same-line checks
                // treat continued tokens as part of the original line.
                _line--;
                _column = 1;
            }
            else if (c == '\\' && PeekNext() == '\r' && _position + 2 < _source.Length && _source[_position + 2] == '\n')
            {
                // Line continuation: backslash followed by CRLF (Windows line endings)
                Advance(); // Skip backslash
                Advance(); // Skip \r
                Advance(); // Skip \n
                _line--;
                _column = 1;
            }
            else if (c == '/' && PeekNext() == '/')
            {
                // Line comment: skip to end of line
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }
            }
            else if (c == 'N' && IsStartOfLineContent() && _source.Substring(_position).StartsWith("Note:"))
            {
                // Skip comment until end of line
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }
            }
            // Phase 31 REQ-4 (SPEC-4) D-11 Option A: position-sensitive `;` Lisp-style line comment.
            // `;` at column-0 (with optional leading whitespace per IsStartOfLineContent()) is a comment
            // to end-of-line. A `;` mid-line stays a TokenType.Semicolon statement-terminator —
            // every shipping pragma (`enable hAsB;`) and typed declaration (`Int x = 5;`) keeps its
            // current lex behavior. Verified zero column-0 `;` exist in any in-repo .flow file
            // (RESEARCH §Migration Audit), so the Phase 18/25/27/28 byte-identical determinism
            // contracts are preserved by construction.
            else if (c == ';' && IsStartOfLineContent())
            {
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }
            }
            // Phase 31 REQ-4 (SPEC-4): `TODO:` lead-in line comment (mirrors the `Note:` arm above).
            else if (c == 'T' && IsStartOfLineContent() && _source.Substring(_position).StartsWith("TODO:"))
            {
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }
            }
            // Phase 31 REQ-4 (SPEC-4): `FIXME:` lead-in line comment.
            else if (c == 'F' && IsStartOfLineContent() && _source.Substring(_position).StartsWith("FIXME:"))
            {
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

    /// <summary>
    /// Phase 35 LANG-04 Wave 1: capture the current source position as the
    /// END of a span being emitted. The lexer's <c>_line</c> / <c>_column</c>
    /// track the position of the NEXT character to read — which is exactly the
    /// half-open END position we want (one past the last consumed character).
    /// </summary>
    private SourceLocation CurrentLocation() => new SourceLocation(_line, _column, _fileName);

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
