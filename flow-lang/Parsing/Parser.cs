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
public partial class Parser
{
    private readonly List<Token> _tokens;
    private readonly ErrorReporter _errorReporter;
    private readonly PragmaSet _pragmaSet;
    private int _current = 0;
    // When true, disables the "identifier followed by literal = function call"
    // heuristic in ParsePrimary. Set while parsing arguments inside (func arg1 arg2).
    private bool _inFuncCallArgs = false;
    private bool _inLoop = false;

    // Bounds for syntax tree depth
    private int _parseDepth = 0;
    private const int MaxParseDepth = 500;

    public Parser(List<Token> tokens, ErrorReporter errorReporter, PragmaSet? pragmaSet = null)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _pragmaSet = pragmaSet ?? PragmaSet.Empty;
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

        return new Program(SourceLocation.Unknown, statements, _pragmaSet);
    }

    private Statement? ParseStatement()
    {
        _parseDepth++;
        if (_parseDepth > MaxParseDepth)
            throw new ParseException($"Maximum nested parse depth of {MaxParseDepth} reached.");

        try
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
            if (Match(TokenType.Rit))
                return ParseMusicalContextStatement(MusicalContextType.Rit);
            if (Match(TokenType.Accel))
                return ParseMusicalContextStatement(MusicalContextType.Accel);
            // Only parse `pan` as a context block when followed by a numeric literal or sign
            // (e.g., `pan 0.5 { ... }` or `pan -1.0 { ... }`), not when used as a variable name.
            if (Check(TokenType.Pan) && _current + 1 < _tokens.Count
                && (_tokens[_current + 1].Type is TokenType.IntLiteral or TokenType.FloatLiteral
                    or TokenType.Minus or TokenType.Plus))
            {
                Advance(); // consume `pan`
                return ParseMusicalContextStatement(MusicalContextType.Pan);
            }
            // Only parse `gain` as a context block when followed by a numeric literal or sign
            // (e.g., `gain 0.5 { ... }`), not when used as a function name.
            if (Check(TokenType.Gain) && _current + 1 < _tokens.Count
                && (_tokens[_current + 1].Type is TokenType.IntLiteral or TokenType.FloatLiteral
                    or TokenType.Minus or TokenType.Plus))
            {
                Advance(); // consume `gain`
                return ParseMusicalContextStatement(MusicalContextType.Gain);
            }
            // Only parse `reverbTime` as a context block when followed by a numeric literal or sign
            // (e.g., `reverbTime 2.5 { ... }`). Per D-03, negative rejection happens INSIDE the case body
            // so the error points at the '-' rather than at '{'.
            if (Check(TokenType.ReverbTime) && _current + 1 < _tokens.Count
                && (_tokens[_current + 1].Type is TokenType.IntLiteral or TokenType.FloatLiteral
                    or TokenType.Minus or TokenType.Plus))
            {
                Advance(); // consume `reverbTime`
                return ParseMusicalContextStatement(MusicalContextType.ReverbTime);
            }

            // Section declaration: section name { ... }
            if (Match(TokenType.Section))
                return ParseSectionDeclaration();

            // Loop constructs
            if (Match(TokenType.For))
                return ParseForStatement();
            if (Match(TokenType.While))
                return ParseWhileStatement();
            if (Match(TokenType.Break))
            {
                if (!_inLoop)
                    throw new ParseException("'break' can only be used inside a loop");
                return new BreakStatement(PreviousToken.Location);
            }
            if (Match(TokenType.Continue))
            {
                if (!_inLoop)
                    throw new ParseException("'continue' can only be used inside a loop");
                return new ContinueStatement(PreviousToken.Location);
            }

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

            // Phase 26 D-15: detect stray arithmetic operators at statement-start.
            // After Phase 26 there are no infix operators, so a leading +/-/*// at
            // statement boundary indicates legacy infix that was abandoned mid-rewrite.
            // ParseUnaryShorthand would otherwise silently absorb the '+' (D-03) or
            // emit (neg IDENT) for `-x`, which masks `1 + 2` style legacy code as
            // a no-op success. Generic 'unexpected token' is the contract per D-15.
            if (Check(TokenType.Star) || Check(TokenType.Slash))
            {
                throw new ParseException(
                    $"Unexpected token '{CurrentToken.Text}' at {CurrentToken.Location} — Phase 26 removed infix arithmetic; use prefix builtins (add)/(sub)/(mul)/(div).");
            }
            // For Plus/Minus, only error when not followed by an identifier (D-01 shorthand)
            // and not followed by a digit (already handled by lexer). The remaining cases
            // are stray operators between value-producing tokens.
            if ((Check(TokenType.Plus) || Check(TokenType.Minus))
                && _current + 1 < _tokens.Count
                && _tokens[_current + 1].Type != TokenType.Identifier)
            {
                throw new ParseException(
                    $"Unexpected token '{CurrentToken.Text}' at {CurrentToken.Location} — Phase 26 removed infix arithmetic; use prefix builtins (add)/(sub)/(mul)/(div).");
            }

            // Expression statement
            var expr = ParseExpression();
            return new ExpressionStatement(expr.Location, expr);
        }
        finally
        {
            _parseDepth--;
        }
    }

    private ProcDeclaration ParseProcDeclaration(bool isInternal)
    {
        var location = PreviousToken.Location;
        // Allow musical context keywords (like 'pan') as procedure names
        string name;
        if (Check(TokenType.Identifier) || Check(TokenType.Pan) || Check(TokenType.Gain)
            || Check(TokenType.Tempo) || Check(TokenType.Swing) || Check(TokenType.Key)
            || Check(TokenType.Timesig))
        {
            name = Advance().Text;
        }
        else
        {
            name = Expect(TokenType.Identifier, "Expected procedure name").Text;
        }

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

            case MusicalContextType.Rit:
            case MusicalContextType.Accel:
            {
                var tempoLoc = CurrentToken.Location;
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(tempoLoc, (int)Advance().Value!);
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(tempoLoc, (double)Advance().Value!);
                else
                    throw new ParseException($"Expected target tempo for {contextType}, got {CurrentToken.Type}");
                break;
            }

            case MusicalContextType.Pan:
            {
                int panSign = 1;
                var panLoc = CurrentToken.Location;
                if (Match(TokenType.Minus)) panSign = -1;
                else if (Match(TokenType.Plus)) panSign = 1;
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(panLoc, panSign * (double)(int)Advance().Value!);
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(panLoc, panSign * (double)Advance().Value!);
                else
                    throw new ParseException($"Expected numeric pan value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
                break;
            }

            case MusicalContextType.Gain:
            {
                int gainSign = 1;
                var gainLoc = CurrentToken.Location;
                if (Match(TokenType.Minus)) gainSign = -1;
                else if (Match(TokenType.Plus)) gainSign = 1;
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(gainLoc, gainSign * (double)(int)Advance().Value!);
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(gainLoc, gainSign * (double)Advance().Value!);
                else
                    throw new ParseException($"Expected numeric gain value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
                break;
            }

            case MusicalContextType.ReverbTime:
            {
                var rtLoc = CurrentToken.Location;
                if (Match(TokenType.Minus))
                    throw new ParseException(
                        $"reverbTime cannot be negative (RT60 is a time in seconds); got '-' at {rtLoc}");
                if (Match(TokenType.Plus)) { /* silent sign noise, accept */ }
                if (Check(TokenType.IntLiteral))
                    value = new LiteralExpression(rtLoc, (double)(int)Advance().Value!);
                else if (Check(TokenType.FloatLiteral))
                    value = new LiteralExpression(rtLoc, (double)Advance().Value!);
                else
                    throw new ParseException(
                        $"Expected numeric reverbTime value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
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

    private ForStatement ParseForStatement()
    {
        var location = PreviousToken.Location;
        var (elementType, nextIndex, isVarArgs) = TypeParser.ParseType(_tokens, _current);
        _current = nextIndex;
        if (isVarArgs)
            elementType = new ArrayType(elementType);
        var varName = Expect(TokenType.Identifier, "Expected variable name in for loop").Text;
        Expect(TokenType.In, "Expected 'in' after variable name in for loop");
        var collection = ParseExpression();
        Expect(TokenType.LBrace, "Expected '{' to begin for loop body");

        var savedInLoop = _inLoop;
        _inLoop = true;
        var body = new List<Statement>();
        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            while (Match(TokenType.Semicolon)) ;
            if (Check(TokenType.RBrace) || IsAtEnd()) break;
            var stmt = ParseStatement();
            if (stmt != null) body.Add(stmt);
            Match(TokenType.Semicolon);
        }
        _inLoop = savedInLoop;

        Expect(TokenType.RBrace, "Expected '}' to close for loop body");
        return new ForStatement(location, elementType, varName, collection, body);
    }

    private WhileStatement ParseWhileStatement()
    {
        var location = PreviousToken.Location;
        var condition = ParseExpression();
        Expect(TokenType.LBrace, "Expected '{' to begin while loop body");

        var savedInLoop = _inLoop;
        _inLoop = true;
        var body = new List<Statement>();
        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            while (Match(TokenType.Semicolon)) ;
            if (Check(TokenType.RBrace) || IsAtEnd()) break;
            var stmt = ParseStatement();
            if (stmt != null) body.Add(stmt);
            Match(TokenType.Semicolon);
        }
        _inLoop = savedInLoop;

        Expect(TokenType.RBrace, "Expected '}' to close while loop body");
        return new WhileStatement(location, condition, body);
    }

    private Expression ParseExpression()
    {
        _parseDepth++;
        if (_parseDepth > MaxParseDepth)
            throw new ParseException($"Maximum nested parse depth of {MaxParseDepth} reached.");

        try
        {
            return ParseFlowExpression();
        }
        finally
        {
            _parseDepth--;
        }
    }

    // Phase 26: arithmetic is now prefix-only; ParseFlowExpression dispatches directly
    // to ParseUnaryShorthand (the post-Phase-26 successor of ParseAdditive/ParseUnary).
    private Expression ParseFlowExpression()
    {
        var left = ParseUnaryShorthand();

        while (Match(TokenType.Arrow))
        {
            var location = PreviousToken.Location;
            var right = ParseUnaryShorthand();

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
                if (!IsAtEnd() && Check(TokenType.LParen)
                    && CurrentToken.Location.Line == varExpr.Location.Line)
                {
                    args.Add(ParseUnaryShorthand());
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

    // Phase 26 (D-01 + D-03): replaces deleted ParseUnary/ParseAdditive/ParseMultiplicative.
    // Arithmetic is now prefix-only via (add)/(sub)/(mul)/(div)/(neg)/(idiv) builtins.
    // The remaining "unary" responsibilities are:
    //   D-03: silently strip leading '+' at expression-start (no node emitted)
    //   D-01: leading '-' followed by an identifier lowers to (neg IDENT)
    private Expression ParseUnaryShorthand()
    {
        // D-03: silently strip '+' at expression-start (no node emitted)
        if (Match(TokenType.Plus))
        {
            // No-op; just continue
        }
        // D-01: '-' followed by identifier → (neg IDENT)
        if (Check(TokenType.Minus) && _current + 1 < _tokens.Count
            && _tokens[_current + 1].Type == TokenType.Identifier)
        {
            var loc = CurrentToken.Location;
            Advance(); // consume '-'
            var name = Advance().Text;
            return new FunctionCallExpression(loc, "neg",
                new List<Expression> { new VariableExpression(loc, name) });
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
                var index = ParseUnaryShorthand();
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
        // Phase 26: IntLiteral may carry an int, long, or BigInteger payload depending
        // on whether the literal overflowed Int32. Pass the boxed Value through —
        // EvaluateLiteral dispatches on the underlying CLR type.
        if (Match(TokenType.IntLiteral))
            return new LiteralExpression(PreviousToken.Location, PreviousToken.Value!);

        if (Match(TokenType.FloatLiteral))
            return new LiteralExpression(PreviousToken.Location, (double)PreviousToken.Value!);

        if (Match(TokenType.StringLiteral))
            return new LiteralExpression(PreviousToken.Location, (string)PreviousToken.Value!);

        if (Match(TokenType.InterpolatedStringStart))
            return ParseInterpolatedString();

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

        // Progression expression: progression | I IV V | or progression voices 4 | I IV V |
        if (Match(TokenType.Progression))
        {
            return ParseProgressionExpression();
        }

        // Pickup note stream: pickup | C4 D4 |
        if (Match(TokenType.Pickup))
        {
            Expect(TokenType.Pipe, "Expected '|' after 'pickup'");
            return ParseNoteStream(isPickup: true);
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
            if ((Check(TokenType.Identifier) || Check(TokenType.Pan) || Check(TokenType.Gain)) && _current + 1 < _tokens.Count
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
            || Match(TokenType.Key) || Match(TokenType.Timesig) || Match(TokenType.Pan)
            || Match(TokenType.Gain))
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
                    args.Add(ParseUnaryShorthand()); // Parse argument expression
                }

                return new FunctionCallExpression(location, name, args);
            }

            // No arguments - it's a variable reference
            return new VariableExpression(location, name);
        }

        throw new ParseException($"Unexpected token {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
    }

    private Expression ParseInterpolatedString()
    {
        var location = PreviousToken.Location;
        var parts = new List<Expression>();

        while (!Check(TokenType.InterpolatedStringEnd) && !IsAtEnd())
        {
            if (Match(TokenType.InterpolatedStringText))
            {
                parts.Add(new LiteralExpression(PreviousToken.Location, (string)PreviousToken.Value!));
            }
            else
            {
                // Parse an expression (the tokens between { and } were already lexed inline)
                parts.Add(ParseExpression());
            }
        }

        Expect(TokenType.InterpolatedStringEnd, "Expected closing '\"' for interpolated string");
        return new InterpolatedStringExpression(location, parts);
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
    /// Parses a progression expression: progression [voices N] | I IV V |
    /// The 'progression' keyword has already been consumed.
    /// </summary>
    private Expression ParseProgressionExpression()
    {
        var location = PreviousToken.Location;

        // Optional: voices N modifier
        int? voiceCount = null;
        if (Check(TokenType.Identifier) && CurrentToken.Text == "voices")
        {
            Advance(); // consume "voices"
            var countToken = Expect(TokenType.IntLiteral, "Expected integer after 'voices'");
            voiceCount = (int)countToken.Value!;
        }

        Expect(TokenType.Pipe, "Expected '|' after 'progression' keyword");

        var chords = new List<ProgressionElement>();

        while (!Check(TokenType.Pipe) && !IsAtEnd())
        {
            var elemLocation = CurrentToken.Location;

            // Roman numerals are lexed as Identifier tokens
            if (!Check(TokenType.Identifier))
            {
                _errorReporter.ReportError(
                    $"Expected roman numeral in progression, got '{CurrentToken.Text}'",
                    CurrentToken.Location);
                Advance(); // skip bad token
                continue;
            }

            var numeralToken = Advance();
            string numeral = numeralToken.Text;

            // Validate it looks like a roman numeral
            if (!ScaleDatabase.IsRomanNumeral(numeral))
            {
                _errorReporter.ReportError(
                    $"'{numeral}' is not a valid roman numeral chord symbol",
                    numeralToken.Location);
            }

            // Optional :N bar count suffix
            int barCount = 1;
            if (Match(TokenType.Colon))
            {
                var countToken = Expect(TokenType.IntLiteral, "Expected integer after ':' in progression element");
                barCount = (int)countToken.Value!;
                if (barCount < 1)
                {
                    _errorReporter.ReportError(
                        "Bar count must be at least 1",
                        countToken.Location);
                    barCount = 1;
                }
            }

            chords.Add(new ProgressionElement(elemLocation, numeral, barCount));
        }

        Expect(TokenType.Pipe, "Expected closing '|' in progression");

        if (chords.Count == 0)
        {
            _errorReporter.ReportError("Progression must contain at least one chord", location);
        }

        return new ProgressionExpression(location, chords, voiceCount);
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
            or TokenType.InterpolatedStringStart
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
            || Check(TokenType.Key) || Check(TokenType.Timesig) || Check(TokenType.Pan)
            || Check(TokenType.Gain))
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
                or TokenType.Dynamics or TokenType.Rit or TokenType.Accel
                or TokenType.Pan or TokenType.Section)
            {
                return;
            }

            Advance();
        }
    }
}
