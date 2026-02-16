using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Parsing;

/// <summary>
/// Recursive descent parser for the Flow language.
/// </summary>
public class Parser
{
    private readonly List<Token> _tokens;
    private readonly ErrorReporter _errorReporter;
    private int _current = 0;
    // When true, disables the "identifier followed by literal = function call"
    // heuristic in ParsePrimary. Set while parsing arguments inside (func arg1 arg2).
    private bool _inFuncCallArgs = false;

    public Parser(List<Token> tokens, ErrorReporter errorReporter)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
    }

    /// <summary>
    /// Parses the token stream into a Program AST.
    /// </summary>
    public Program Parse()
    {
        var statements = new List<Statement>();

        while (!IsAtEnd())
        {
            try
            {
                // Skip optional semicolons at statement level
                while (Match(TokenType.Semicolon))
                    ; // Consume extra semicolons

                if (IsAtEnd())
                    break;

                var stmt = ParseStatement();
                if (stmt != null)
                    statements.Add(stmt);

                // Optionally consume trailing semicolon after statement
                Match(TokenType.Semicolon);
            }
            catch (ParseException ex)
            {
                _errorReporter.ReportError(ex.Message, CurrentToken.Location);
                Synchronize();
            }
        }

        return new Program(SourceLocation.Unknown, statements);
    }

    private Statement? ParseStatement()
    {
        // Skip comments
        if (Match(TokenType.Comment))
            return null;

        if (Match(TokenType.Proc))
            return ParseProcDeclaration(false);

        if (Match(TokenType.Internal))
        {
            Expect(TokenType.Proc, "Expected 'proc' after 'internal'");
            return ParseProcDeclaration(true);
        }

        if (Match(TokenType.Return))
            return ParseReturnStatement();

        if (Match(TokenType.Use))
            return ParseImportStatement();

        // Musical context blocks
        if (Match(TokenType.Timesig))
            return ParseMusicalContextStatement(MusicalContextType.Timesig);
        if (Match(TokenType.Tempo))
            return ParseMusicalContextStatement(MusicalContextType.Tempo);
        if (Match(TokenType.Swing))
            return ParseMusicalContextStatement(MusicalContextType.Swing);
        if (Match(TokenType.Key))
            return ParseMusicalContextStatement(MusicalContextType.Key);
        if (Match(TokenType.Dynamics))
            return ParseMusicalContextStatement(MusicalContextType.Dynamics);

        // Section declaration: section name { ... }
        if (Match(TokenType.Section))
            return ParseSectionDeclaration();

        // Check for variable declaration: Type identifier =
        if (IsTypeKeyword(CurrentToken.Type))
        {
            return ParseVariableDeclaration();
        }

        // Check for assignment (identifier followed by =)
        if (Check(TokenType.Identifier))
        {
            // Look ahead to distinguish assignment from expression
            int savedPos = _current;
            Advance(); // Skip identifier

            if (Check(TokenType.Assign))
            {
                // It's an assignment: reset and parse
                _current = savedPos;
                return ParseAssignment();
            }

            // Not assignment - reset and parse as expression
            _current = savedPos;
        }

        // Expression statement
        var expr = ParseExpression();
        return new ExpressionStatement(expr.Location, expr);
    }

    private ProcDeclaration ParseProcDeclaration(bool isInternal)
    {
        var location = PreviousToken.Location;
        var name = Expect(TokenType.Identifier, "Expected procedure name").Text;

        var parameters = new List<Parameter>();
        Expect(TokenType.LParen, "Expected '(' after procedure name");

        // Parse parameters: Ints: name (plural varargs) or Type...: name (ellipsis varargs) or Type: name
        while (!Check(TokenType.RParen) && !IsAtEnd())
        {
            var (paramType, nextIndex, isVarArgs) = TypeParser.ParseType(_tokens, _current);
            _current = nextIndex;

            // Check for ellipsis varargs (...) after type (alternative syntax)
            if (Check(TokenType.Ellipsis))
            {
                Advance();
                isVarArgs = true;
            }

            Expect(TokenType.Colon, "Expected ':' after parameter type");
            var paramName = ExpectParameterName().Text;

            parameters.Add(new Parameter(paramName, paramType, isVarArgs));

            if (!Check(TokenType.RParen))
                Expect(TokenType.Comma, "Expected ',' between parameters");
        }

        Expect(TokenType.RParen, "Expected ')' after parameters");

        // Parse body statements until "end proc" or "end"
        var body = new List<Statement>();

        if (!isInternal)
        {
            while (!Check(TokenType.EndProc) && !Check(TokenType.Eof))
            {
                // Skip optional semicolons at statement level
                while (Match(TokenType.Semicolon))
                    ; // Consume extra semicolons

                if (Check(TokenType.EndProc) || Check(TokenType.Eof))
                    break;

                var stmt = ParseStatement();
                if (stmt != null)
                    body.Add(stmt);

                // Optionally consume trailing semicolon after statement
                Match(TokenType.Semicolon);
            }

            Expect(TokenType.EndProc, "Expected 'end' after procedure body");

            // Optionally consume "proc" after "end"
            if (Check(TokenType.Proc))
                Advance();
        }

        return new ProcDeclaration(location, name, parameters, body, isInternal);
    }

    private VariableDeclaration ParseVariableDeclaration()
    {
        var (varType, nextIndex, isVarArgs) = TypeParser.ParseType(_tokens, _current);
        _current = nextIndex;

        // If plural form was used (e.g., "Ints"), treat it as array type in variable declarations
        if (isVarArgs)
        {
            varType = new ArrayType(varType);
        }

        var name = Expect(TokenType.Identifier, "Expected variable name").Text;
        var location = PreviousToken.Location;

        Expression value;

        // Check if there's an initializer
        if (Match(TokenType.Assign))
        {
            // Special case: Song type with [section1 section2*N ...] arrangement syntax
            if (varType is SongType && Check(TokenType.LBracket))
            {
                value = ParseSongExpression();
            }
            else
            {
                value = ParseExpression();
            }
        }
        else
        {
            // No initializer - create default value based on type
            value = CreateDefaultValueExpression(varType, location);
        }

        return new VariableDeclaration(value.Location, varType, name, value);
    }

    private Expression CreateDefaultValueExpression(FlowType type, SourceLocation location)
    {
        object defaultValue = type switch
        {
            IntType => 0,
            FloatType => 0.0,
            LongType => 0L,
            DoubleType => 0.0,
            StringType => "",
            BoolType => false,
            NumberType => System.Numerics.BigInteger.Zero,
            VoidType => null!,
            ArrayType => null!, // Will be handled specially
            _ => null! // For special types like Buffer, Note, etc.
        };

        // Handle array types - create empty array expression via list() call
        if (type is ArrayType arrayType)
        {
            // Create a call to list() with no arguments
            return new FunctionCallExpression(location, "list", new List<Expression>());
        }

        // For null default values (Buffer, custom types), use null literal
        if (defaultValue == null)
        {
            // Return a special marker that will evaluate to a null/void value
            // We'll use 0 as a placeholder and handle conversion at runtime
            return new LiteralExpression(location, 0);
        }

        return new LiteralExpression(location, defaultValue);
    }

    private AssignmentStatement ParseAssignment()
    {
        var name = Expect(TokenType.Identifier, "Expected variable name").Text;
        var location = PreviousToken.Location;

        Expect(TokenType.Assign, "Expected '=' in assignment");

        var value = ParseExpression();

        return new AssignmentStatement(location, name, value);
    }

    private ReturnStatement ParseReturnStatement()
    {
        var location = PreviousToken.Location;
        var value = ParseExpression();
        return new ReturnStatement(location, value);
    }

    private SongExpression ParseSongExpression()
    {
        var location = CurrentToken.Location;
        Expect(TokenType.LBracket, "Expected '[' for song arrangement");

        var sections = new List<SongSectionReference>();

        while (!Check(TokenType.RBracket) && !IsAtEnd())
        {
            var sectionName = Expect(TokenType.Identifier, "Expected section name in song arrangement").Text;
            int repeatCount = 1;

            // Check for repeat: name*N
            if (Match(TokenType.Star))
            {
                var countToken = Expect(TokenType.IntLiteral, "Expected repeat count after '*'");
                repeatCount = (int)countToken.Value!;
            }

            sections.Add(new SongSectionReference(sectionName, repeatCount));
        }

        Expect(TokenType.RBracket, "Expected ']' after song arrangement");

        return new SongExpression(location, sections);
    }

    private SectionDeclaration ParseSectionDeclaration()
    {
        var location = PreviousToken.Location;
        var name = Expect(TokenType.Identifier, "Expected section name").Text;

        Expect(TokenType.LBrace, "Expected '{' after section name");

        var body = new List<Statement>();
        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            while (Match(TokenType.Semicolon)) ;

            if (Check(TokenType.RBrace) || IsAtEnd())
                break;

            var stmt = ParseStatement();
            if (stmt != null)
                body.Add(stmt);

            Match(TokenType.Semicolon);
        }

        Expect(TokenType.RBrace, "Expected '}' after section body");

        return new SectionDeclaration(location, name, body);
    }

    private ImportStatement ParseImportStatement()
    {
        var location = PreviousToken.Location;
        var path = Expect(TokenType.StringLiteral, "Expected string literal for import path");
        return new ImportStatement(location, (string)path.Value!);
    }

    private MusicalContextStatement ParseMusicalContextStatement(MusicalContextType contextType)
    {
        var location = PreviousToken.Location;
        Expression value;
        Expression? value2 = null;

        switch (contextType)
        {
            case MusicalContextType.Timesig:
                // Parse numerator / denominator (e.g., 4/4, 3/4, 7/8)
                value = new LiteralExpression(CurrentToken.Location,
                    (int)Expect(TokenType.IntLiteral, "Expected integer numerator for time signature").Value!);
                Expect(TokenType.Slash, "Expected '/' separator in time signature (e.g., timesig 4/4)");
                value2 = new LiteralExpression(CurrentToken.Location,
                    (int)Expect(TokenType.IntLiteral, "Expected integer denominator for time signature").Value!);
                break;

            case MusicalContextType.Tempo:
            {
                int tempoSign = 1;
                var tempoLoc = CurrentToken.Location;
                if (Match(TokenType.Minus)) tempoSign = -1;
                else if (Match(TokenType.Plus)) tempoSign = 1;
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(tempoLoc, tempoSign * (int)Advance().Value!);
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(tempoLoc, tempoSign * (double)Advance().Value!);
                else
                    throw new ParseException($"Expected numeric tempo value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
                break;
            }

            case MusicalContextType.Swing:
            {
                int swingSign = 1;
                var swingLoc = CurrentToken.Location;
                if (Match(TokenType.Minus)) swingSign = -1;
                else if (Match(TokenType.Plus)) swingSign = 1;
                if (Check(TokenType.IntLiteral))
                {
                    var intToken = Advance();
                    int intVal = swingSign * (int)intToken.Value!;
                    if (Check(TokenType.Identifier) && CurrentToken.Text == "%")
                    {
                        Advance();
                        value = new LiteralExpression(swingLoc, intVal / 100.0);
                    }
                    else
                        value = new LiteralExpression(swingLoc, (double)intVal);
                }
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(swingLoc, swingSign * (double)Advance().Value!);
                else
                    throw new ParseException($"Expected swing value (percentage or float), got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
                break;
            }

            case MusicalContextType.Key:
                // Accept identifier like Cmajor, Aminor, etc.
                var keyToken = Expect(TokenType.Identifier, "Expected key name (e.g., Cmajor, Aminor)");
                value = new LiteralExpression(keyToken.Location, keyToken.Text);
                break;

            case MusicalContextType.Dynamics:
            {
                var dynToken = Expect(TokenType.Identifier, "Expected dynamic level (pp, p, mp, mf, f, ff, fff, ppp)");
                var velocity = TryParseDynamicMarking(dynToken.Text);
                if (!velocity.HasValue)
                {
                    _errorReporter.ReportError(
                        $"Unknown dynamic marking '{dynToken.Text}'. Use: ppp, pp, p, mp, mf, f, ff, fff",
                        dynToken.Location);
                    value = new LiteralExpression(dynToken.Location, 0.63);
                }
                else
                {
                    value = new LiteralExpression(dynToken.Location, velocity.Value);
                }
                break;
            }

            default:
                throw new ParseException($"Unknown musical context type: {contextType}");
        }

        // Expect body block
        Expect(TokenType.LBrace, "Expected '{' to open musical context block");

        var body = new List<Statement>();
        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            while (Match(TokenType.Semicolon)) ; // skip semicolons

            if (Check(TokenType.RBrace) || IsAtEnd())
                break;

            var stmt = ParseStatement();
            if (stmt != null)
                body.Add(stmt);

            Match(TokenType.Semicolon);
        }

        Expect(TokenType.RBrace, "Expected '}' to close musical context block");

        return new MusicalContextStatement(location, contextType, value, value2, body);
    }

    private Expression ParseExpression()
    {
        return ParseFlowExpression();
    }

    // Flow operator has lower precedence than arithmetic
    private Expression ParseFlowExpression()
    {
        var left = ParseAdditive();

        while (Match(TokenType.Arrow))
        {
            var location = PreviousToken.Location;
            var right = ParseAdditive();

            // Transform right side if it's an identifier or function call
            // x -> func becomes func(x)
            // x -> func(arg) becomes func(x, arg)
            // x -> func (expr) becomes func(x, expr) (parenthesized args in flow context)
            if (right is VariableExpression varExpr)
            {
                // Collect a single parenthesized argument after the function name in flow context
                // This supports: x -> concat (expr) -> print
                // Only collect one parenthesized expression to avoid consuming the next statement
                // (e.g., `arr -> each (lambda)\n("next stmt")` should not treat the second line as an arg)
                var args = new List<Expression> { left };
                if (!IsAtEnd() && Check(TokenType.LParen))
                {
                    args.Add(ParseAdditive());
                }
                right = new FunctionCallExpression(right.Location, varExpr.Name, args);
            }
            else if (right is FunctionCallExpression funcCall)
            {
                // Prepend left to function arguments
                var newArgs = new List<Expression> { left };
                newArgs.AddRange(funcCall.Arguments);
                right = funcCall with { Arguments = newArgs };
            }
            else
            {
                // Otherwise just wrap in flow expression
                left = new FlowExpression(location, left, right);
                continue;
            }

            left = right;
        }

        return left;
    }

    private Expression ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (Match(TokenType.Plus, TokenType.Minus))
        {
            var op = PreviousToken.Type == TokenType.Plus
                ? BinaryOperator.Add
                : BinaryOperator.Subtract;
            var location = PreviousToken.Location;
            var right = ParseMultiplicative();
            left = new BinaryExpression(location, left, op, right);
        }

        return left;
    }

    private Expression ParseMultiplicative()
    {
        var left = ParseUnary();

        while (Match(TokenType.Star, TokenType.Slash))
        {
            var op = PreviousToken.Type == TokenType.Star
                ? BinaryOperator.Multiply
                : BinaryOperator.Divide;
            var location = PreviousToken.Location;
            var right = ParseUnary();
            left = new BinaryExpression(location, left, op, right);
        }

        return left;
    }

    private Expression ParseUnary()
    {
        if (Match(TokenType.Minus, TokenType.Plus))
        {
            var op = PreviousToken.Type == TokenType.Minus
                ? BinaryOperator.Subtract
                : BinaryOperator.Add;
            var location = PreviousToken.Location;
            var right = ParseUnary();

            // Unary minus/plus as 0 - x or 0 + x
            var zero = new LiteralExpression(location, 0);
            return new BinaryExpression(location, zero, op, right);
        }

        return ParsePostfix();
    }

    private Expression ParsePostfix()
    {
        var expr = ParsePrimary();

        while (true)
        {
            if (Match(TokenType.At))
            {
                // Array indexing: arr@index (supports unary minus for negative indices)
                var index = ParseUnary();
                expr = new ArrayIndexExpression(expr.Location, expr, index);
            }
            else if (Match(TokenType.Dot))
            {
                // Member access: obj.member
                var memberName = Expect(TokenType.Identifier, "Expected member name after '.'").Text;
                expr = new MemberAccessExpression(expr.Location, expr, memberName);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private Expression ParsePrimary()
    {
        // Lazy expression
        if (Match(TokenType.Lazy))
        {
            var location = PreviousToken.Location;
            Expect(TokenType.LParen, "Expected '(' after 'lazy'");
            var innerExpr = ParseExpression();
            Expect(TokenType.RParen, "Expected ')' after lazy expression");
            return new LazyExpression(location, innerExpr);
        }

        // Literals
        if (Match(TokenType.IntLiteral))
            return new LiteralExpression(PreviousToken.Location, (int)PreviousToken.Value!);

        if (Match(TokenType.FloatLiteral))
            return new LiteralExpression(PreviousToken.Location, (double)PreviousToken.Value!);

        if (Match(TokenType.StringLiteral))
            return new LiteralExpression(PreviousToken.Location, (string)PreviousToken.Value!);

        if (Match(TokenType.BoolLiteral))
            return new LiteralExpression(PreviousToken.Location, (bool)PreviousToken.Value!);

        if (Match(TokenType.NoteLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text);

        if (Match(TokenType.SemitoneLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text);

        if (Match(TokenType.CentLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text);

        if (Match(TokenType.TimeLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text);

        if (Match(TokenType.DecibelLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Text);

        if (Match(TokenType.ChordLiteral))
            return new ChordLiteralExpression(PreviousToken.Location, PreviousToken.Text);

        // Lambda expression: fn Type name, Type name => body
        if (Match(TokenType.Fn))
        {
            return ParseLambdaExpression();
        }

        // Note stream expression: | C4 D4 E4 F4 |
        if (Match(TokenType.Pipe))
        {
            return ParseNoteStream();
        }

        // Array literal [elem1, elem2, ...]
        if (Match(TokenType.LBracket))
        {
            var location = PreviousToken.Location;
            var elements = new List<Expression>();

            while (!Check(TokenType.RBracket) && !IsAtEnd())
            {
                elements.Add(ParseExpression());
                if (!Check(TokenType.RBracket))
                {
                    // Support both comma-separated and space-separated array elements
                    if (Check(TokenType.Comma))
                        Advance(); // consume optional comma
                }
            }

            Expect(TokenType.RBracket, "Expected ']' after array literal");
            return new ArrayLiteralExpression(location, elements);
        }

        // Parenthesized expression or function call
        if (Match(TokenType.LParen))
        {
            var location = PreviousToken.Location;

            // Check if this is a function call like (func arg1 arg2)
            // But NOT if the identifier is followed by -> (that's a parenthesized flow expression)
            if (Check(TokenType.Identifier) && _current + 1 < _tokens.Count
                && _tokens[_current + 1].Type != TokenType.Arrow
                && _tokens[_current + 1].Type != TokenType.Dot
                && _tokens[_current + 1].Type != TokenType.At)
            {
                var name = Advance().Text;
                var args = new List<Expression>();

                // Inside (func ...) args, disable the "identifier literal = function call"
                // heuristic so that (add n 1) parses as add(n, 1), not add(n(1)).
                var savedFlag = _inFuncCallArgs;
                _inFuncCallArgs = true;
                while (!Check(TokenType.RParen) && !IsAtEnd())
                {
                    args.Add(ParseExpression());
                }
                _inFuncCallArgs = savedFlag;

                Expect(TokenType.RParen, "Expected ')' after function arguments");
                return new FunctionCallExpression(location, name, args);
            }

            // Regular parenthesized expression
            var expr = ParseExpression();
            Expect(TokenType.RParen, "Expected ')' after expression");
            return expr;
        }

        // Variable or function call (also allow music context keywords as identifiers)
        if (Match(TokenType.Identifier) || Match(TokenType.Tempo) || Match(TokenType.Swing)
            || Match(TokenType.Key) || Match(TokenType.Timesig))
        {
            var name = PreviousToken.Text;
            var location = PreviousToken.Location;

            // Look ahead: if next token starts a simple argument, this is a function call
            // Note: We only support simple arguments (literals, identifiers) for optional parens
            // For complex arguments (parenthesized expressions), use explicit syntax: (func (expr))
            // Disabled inside (func ...) args to prevent (add n 1) from becoming add(n(1))
            if (!_inFuncCallArgs && IsArgumentStart(CurrentToken.Type)
                && CurrentToken.Location.Line == location.Line)
            {
                var args = new List<Expression>();

                // Parse simple arguments until we hit a terminator or non-argument token
                while (!IsAtEnd() && IsArgumentStart(CurrentToken.Type)
                       && CurrentToken.Location.Line == location.Line)
                {
                    args.Add(ParseUnary()); // Parse argument expression
                }

                return new FunctionCallExpression(location, name, args);
            }

            // No arguments - it's a variable reference
            return new VariableExpression(location, name);
        }

        throw new ParseException($"Unexpected token {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
    }

    private Expression ParseLambdaExpression()
    {
        var location = PreviousToken.Location;
        var parameters = new List<LambdaParameter>();

        // Parse parameters: Type name, Type name => body
        // The fat arrow terminates the parameter list
        while (!Check(TokenType.FatArrow) && !IsAtEnd())
        {
            var (paramType, nextIndex, isVarArgs) = TypeParser.ParseType(_tokens, _current);
            _current = nextIndex;

            var paramName = ExpectParameterName("Expected parameter name in lambda").Text;
            parameters.Add(new LambdaParameter(paramName, paramType));

            if (!Check(TokenType.FatArrow))
                Expect(TokenType.Comma, "Expected ',' between lambda parameters");
        }

        Expect(TokenType.FatArrow, "Expected '=>' in lambda expression");

        List<Statement> body;
        if (Check(TokenType.LParen) && _current + 1 < _tokens.Count && IsTypeKeyword(_tokens[_current + 1].Type))
        {
            // Multi-statement lambda body: ( stmt1 stmt2 ... )
            Advance(); // consume '('
            body = new List<Statement>();
            while (!Check(TokenType.RParen) && !IsAtEnd())
            {
                while (Match(TokenType.Semicolon)) ; // skip semicolons
                if (Check(TokenType.RParen)) break;
                var stmt = ParseStatement();
                if (stmt != null) body.Add(stmt);
                Match(TokenType.Semicolon);
            }
            Expect(TokenType.RParen, "Expected ')' after lambda body");
        }
        else
        {
            // Single-expression body (existing behavior)
            var expr = ParseExpression();
            body = new List<Statement> { new ExpressionStatement(expr.Location, expr) };
        }
        return new LambdaExpression(location, parameters, body);
    }

    /// <summary>
    /// Parses a note stream: | element element ... | element element ... |
    /// The opening | has already been consumed.
    /// </summary>
    private Expression ParseNoteStream()
    {
        var location = PreviousToken.Location;
        var bars = new List<NoteStreamBar>();
        var currentBarElements = new List<NoteStreamElement>();
        double? stickyVelocity = null;

        while (!IsAtEnd())
        {
            // End of bar / end of stream
            if (Match(TokenType.Pipe))
            {
                // Save current bar
                bars.Add(new NoteStreamBar(location, currentBarElements));
                currentBarElements = new List<NoteStreamElement>();
                stickyVelocity = null;

                // Check if this was the final closing pipe
                // A closing pipe is followed by a non-note-stream token
                if (IsAtEnd() || IsEndOfNoteStream())
                    break;

                continue;
            }

            // Rest element: _
            if (Match(TokenType.Underscore))
            {
                var elemLoc = PreviousToken.Location;
                string? durSuffix = TryParseDurationSuffix();
                bool isDotted = durSuffix != null && Match(TokenType.Dot);
                currentBarElements.Add(new RestElement(elemLoc, durSuffix, isDotted));
                continue;
            }

            // Random choice: (? C4 E4 G4) or (?? C4 E4 G4) with optional weights
            if (Check(TokenType.LParen) && !IsAtEnd())
            {
                // Peek ahead: need ( followed by ? or ??
                int savedPos = _current;
                Advance(); // consume (
                if (Check(TokenType.Identifier) && (CurrentToken.Text == "?" || CurrentToken.Text == "??"))
                {
                    var elemLoc = _tokens[savedPos].Location;
                    bool isSeeded = CurrentToken.Text == "??";
                    Advance(); // consume ? or ??
                    var choices = new List<(string Note, int? Weight)>();
                    while (!Check(TokenType.RParen) && !IsAtEnd())
                    {
                        if (Check(TokenType.NoteLiteral))
                        {
                            var noteToken = Advance();
                            int? weight = null;
                            if (Match(TokenType.Colon))
                            {
                                var wt = Expect(TokenType.IntLiteral, "Expected weight after ':'");
                                weight = (int)wt.Value!;
                            }
                            choices.Add((noteToken.Text, weight));
                        }
                        else if (Match(TokenType.Underscore))
                        {
                            // Allow rest _ as a choice
                            int? weight = null;
                            if (Match(TokenType.Colon))
                            {
                                var wt = Expect(TokenType.IntLiteral, "Expected weight after ':'");
                                weight = (int)wt.Value!;
                            }
                            choices.Add(("_", weight));
                        }
                        else
                        {
                            _errorReporter.ReportError($"Expected note or '_' in random choice, got '{CurrentToken.Text}'", CurrentToken.Location);
                            Advance();
                        }
                    }
                    Expect(TokenType.RParen, "Expected ')' after random choice");
                    if (choices.Count == 0) _errorReporter.ReportError("Random choice requires at least one option", elemLoc);
                    string? durSuffix = TryParseDurationSuffix();
                    bool isDotted = durSuffix != null && Match(TokenType.Dot);
                    currentBarElements.Add(new RandomChoiceElement(elemLoc, choices, isSeeded, durSuffix, isDotted));
                    continue;
                }
                else
                {
                    // Not a random choice — rewind
                    _current = savedPos;
                }
            }

            // Chord bracket: [C4 E4 G4]
            if (Match(TokenType.LBracket))
            {
                var elemLoc = PreviousToken.Location;
                var notes = new List<string>();
                while (!Check(TokenType.RBracket) && !IsAtEnd())
                {
                    var noteToken = Expect(TokenType.NoteLiteral, "Expected note literal in chord bracket");
                    notes.Add(noteToken.Text);
                }
                Expect(TokenType.RBracket, "Expected ']' after chord bracket");
                string? durSuffix = TryParseDurationSuffix();
                bool isDotted = durSuffix != null && Match(TokenType.Dot);
                currentBarElements.Add(new ChordElement(elemLoc, notes, durSuffix, isDotted));
                continue;
            }

            // Named chord element in note stream: Cmaj7, Dm, etc.
            if (Check(TokenType.ChordLiteral))
            {
                var chordToken = Advance();
                var elemLoc = chordToken.Location;
                string chordSymbol = chordToken.Text;
                string? durSuffix = TryParseDurationSuffix();
                bool isDotted = durSuffix != null && Match(TokenType.Dot);
                currentBarElements.Add(new NamedChordElement(elemLoc, chordSymbol, durSuffix, isDotted));
                continue;
            }

            // Crescendo/decrescendo span markers (consumed as visual indicators;
            // actual interpolation is handled by NoteStreamCompiler post-processing)
            if (Check(TokenType.Identifier))
            {
                var text = CurrentToken.Text;
                if (text == "cresc" || text == "decresc")
                {
                    Advance();
                    continue;
                }
            }

            // Dynamic marking: pp, p, mp, mf, f, ff, fff, ppp, sfz, fp
            if (Check(TokenType.Identifier))
            {
                var dynVelocity = TryParseDynamicMarking(CurrentToken.Text);
                if (dynVelocity.HasValue)
                {
                    Advance();
                    stickyVelocity = dynVelocity.Value;
                    continue;
                }
            }

            // Note element: C4, C4q, C4q., C4h~
            if (Check(TokenType.NoteLiteral))
            {
                var noteToken = Advance();
                var elemLoc = noteToken.Location;
                string noteName = noteToken.Text;
                string? durSuffix = TryParseDurationSuffix();
                bool isDotted = durSuffix != null && Match(TokenType.Dot);
                bool isTied = Match(TokenType.Tilde);
                double? centOffset = null;
                if (Check(TokenType.CentLiteral))
                {
                    centOffset = (double)Advance().Value!;
                }
                Articulation? articMark = TryParseArticulation();
                currentBarElements.Add(new NoteElement(elemLoc, noteName, durSuffix, isDotted, isTied, centOffset, stickyVelocity, articMark));
                continue;
            }

            // Identifier in note stream: roman numerals or variable references
            if (Check(TokenType.Identifier))
            {
                var identText = CurrentToken.Text;
                if (ScaleDatabase.IsRomanNumeral(identText))
                {
                    var rnToken = Advance();
                    var elemLoc = rnToken.Location;
                    string? durSuffix = TryParseDurationSuffix();
                    bool isDotted = durSuffix != null && Match(TokenType.Dot);
                    currentBarElements.Add(new RomanNumeralElement(elemLoc, identText, durSuffix, isDotted));
                    continue;
                }
                else if (identText is not ("w" or "h" or "q" or "e" or "s" or "t"))
                {
                    // Lowercase-initial identifiers are variable references
                    if (identText.Length > 0 && char.IsLower(identText[0]))
                    {
                        var varToken = Advance();
                        var elemLoc = varToken.Location;
                        string? durSuffix = TryParseDurationSuffix();
                        bool isDotted = durSuffix != null && Match(TokenType.Dot);
                        bool isTied = Match(TokenType.Tilde);
                        double? centOffset = null;
                        if (Check(TokenType.CentLiteral))
                            centOffset = (double)Advance().Value!;
                        currentBarElements.Add(new VariableReferenceElement(
                            elemLoc, identText, durSuffix, isDotted, isTied, centOffset));
                        continue;
                    }
                }
            }

            // If we encounter something unexpected, break out
            break;
        }

        // If we broke out without a closing pipe, the last bar is incomplete but still valid
        if (currentBarElements.Count > 0)
        {
            bars.Add(new NoteStreamBar(location, currentBarElements));
        }

        if (bars.Count == 0)
        {
            _errorReporter.ReportError("Empty note stream", location);
        }

        return new NoteStreamExpression(location, bars);
    }

    /// <summary>
    /// Tries to parse a duration suffix (w, h, q, e, s, t) from the current token.
    /// Returns null if no valid duration suffix is found.
    /// </summary>
    private string? TryParseDurationSuffix()
    {
        if (Check(TokenType.Identifier))
        {
            var text = CurrentToken.Text;
            if (text is "w" or "h" or "q" or "e" or "s" or "t")
            {
                Advance();
                return text;
            }
        }
        return null;
    }

    /// <summary>
    /// Tries to parse an articulation mark after a note element.
    /// Recognizes: > (accent), stacc (staccato), ten (tenuto), marc (marcato).
    /// Returns null if no articulation is found.
    /// </summary>
    private Articulation? TryParseArticulation()
    {
        if (Check(TokenType.GreaterThan))
        {
            Advance();
            return Articulation.Accent;
        }
        if (Check(TokenType.Identifier))
        {
            var text = CurrentToken.Text;
            switch (text)
            {
                case "stacc":
                    Advance();
                    return Articulation.Staccato;
                case "ten":
                    Advance();
                    return Articulation.Tenuto;
                case "marc":
                    Advance();
                    return Articulation.Marcato;
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if the current position looks like the end of a note stream.
    /// Returns true if the next token is not a note-stream element.
    /// </summary>
    private bool IsEndOfNoteStream()
    {
        var type = CurrentToken.Type;
        // Note stream elements are: notes, rests, chord brackets, named chords, pipes
        // Identifiers can be roman numerals inside note streams
        if (type is TokenType.NoteLiteral or TokenType.Underscore
            or TokenType.LBracket or TokenType.Pipe or TokenType.ChordLiteral
            or TokenType.LParen or TokenType.GreaterThan)
            return false;
        // Check if identifier is a roman numeral, dynamic marking, articulation mark, or cresc/decresc
        if (type == TokenType.Identifier && (ScaleDatabase.IsRomanNumeral(CurrentToken.Text) || TryParseDynamicMarking(CurrentToken.Text).HasValue || CurrentToken.Text is "stacc" or "ten" or "marc" or "cresc" or "decresc"))
            return false;
        // Lowercase identifiers are variable references — continue the stream
        if (type == TokenType.Identifier)
        {
            var text = CurrentToken.Text;
            if (text.Length > 0 && char.IsLower(text[0])
                && text is not ("w" or "h" or "q" or "e" or "s" or "t"))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if a token text is a dynamic marking and returns its velocity (0.0-1.0).
    /// Returns null if not a dynamic marking.
    /// </summary>
    private static double? TryParseDynamicMarking(string text)
    {
        return text switch
        {
            "ppp" => 0.125,
            "pp"  => 0.25,
            "p"   => 0.375,
            "mp"  => 0.5,
            "mf"  => 0.625,
            "f"   => 0.75,
            "ff"  => 0.875,
            "fff" => 1.0,
            "sfz" => 0.95,
            "fp"  => 0.75,
            _     => null
        };
    }

    // Helper methods

    private bool IsTypeKeyword(TokenType type)
    {
        if (type is TokenType.Void or TokenType.Int or TokenType.Float
            or TokenType.Long or TokenType.Double or TokenType.String
            or TokenType.Bool or TokenType.Number or TokenType.Buf)
        {
            return true;
        }

        // Function type: (Type, Type => Type)
        if (type == TokenType.LParen && TypeParser.LooksLikeFunctionType(_tokens, _current))
        {
            return true;
        }

        // Check for special types and plural forms (array types)
        if (type == TokenType.Identifier)
        {
            var text = CurrentToken.Text;

            // Special types
            if (text is "Buffer" or "Note" or "Bar" or "Semitone" or "Cent"
                or "Millisecond" or "Second" or "Decibel" or "Lazy"
                or "MusicalNote" or "Function" or "Chord" or "Section" or "Song"
                or "OscillatorState" or "Envelope" or "Beat" or "Voice"
                or "Track" or "NoteValue" or "TimeSignature" or "Sequence")
                return true;

            // Plural forms (array types like Ints, Strings, etc.)
            if (text.EndsWith("s"))
            {
                var singular = text.Substring(0, text.Length - 1);
                if (singular is "Void" or "Int" or "Float" or "Long" or "Double"
                    or "String" or "Bool" or "Number" or "Buf" or "Buffer"
                    or "Note" or "Bar" or "Semitone" or "Cent" or "Millisecond" or "Second" or "Decibel"
                    or "MusicalNote" or "Function" or "Chord" or "Section" or "Song"
                    or "OscillatorState" or "Envelope" or "Beat" or "Voice"
                    or "Track" or "NoteValue" or "TimeSignature" or "Sequence")
                    return true;
            }
        }

        return false;
    }

    private bool IsArgumentStart(TokenType type)
    {
        // For optional parentheses syntax: identifiers and literals can be arguments
        // Note: LParen is intentionally excluded here despite being an unambiguous
        // expression start. Including it would cause identifiers followed by parenthesized
        // expressions to be misinterpreted as function calls (e.g., `xs (fn ...)` inside
        // `(each xs (fn ...))`). Use explicit function call syntax instead: (func (expr))
        return type is TokenType.IntLiteral
            or TokenType.FloatLiteral
            or TokenType.StringLiteral
            or TokenType.BoolLiteral
            or TokenType.NoteLiteral
            or TokenType.SemitoneLiteral
            or TokenType.CentLiteral
            or TokenType.TimeLiteral
            or TokenType.DecibelLiteral
            or TokenType.ChordLiteral
            or TokenType.Identifier;
    }

    private bool Check(TokenType type)
    {
        if (IsAtEnd()) return false;
        return CurrentToken.Type == type;
    }

    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }
        return false;
    }

    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return PreviousToken;
    }

    private Token Expect(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw new ParseException($"{message}. Got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
    }

    private Token ExpectParameterName(string errorMessage = "Expected parameter name")
    {
        if (Check(TokenType.Identifier) || Check(TokenType.Tempo) || Check(TokenType.Swing)
            || Check(TokenType.Key) || Check(TokenType.Timesig))
            return Advance();
        return Expect(TokenType.Identifier, errorMessage);
    }

    private bool IsAtEnd() => CurrentToken.Type == TokenType.Eof;

    private Token CurrentToken => _current < _tokens.Count ? _tokens[_current] : _tokens[^1];
    private Token PreviousToken => _tokens[_current - 1];

    private void Synchronize()
    {
        Advance();

        while (!IsAtEnd())
        {
            if (PreviousToken.Type == TokenType.EndProc) return;

            // Also sync on semicolons
            if (PreviousToken.Type == TokenType.Semicolon) return;

            if (CurrentToken.Type is TokenType.Proc or TokenType.Return
                or TokenType.Use or TokenType.Internal
                or TokenType.Timesig or TokenType.Tempo
                or TokenType.Swing or TokenType.Key
                or TokenType.Dynamics or TokenType.Section)
            {
                return;
            }

            Advance();
        }
    }
}
