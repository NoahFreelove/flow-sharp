using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
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
            var paramName = Expect(TokenType.Identifier, "Expected parameter name").Text;

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
            value = ParseExpression();
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

    private ImportStatement ParseImportStatement()
    {
        var location = PreviousToken.Location;
        var path = Expect(TokenType.StringLiteral, "Expected string literal for import path");
        return new ImportStatement(location, (string)path.Value!);
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
            if (right is VariableExpression varExpr)
            {
                // Convert identifier to function call with left as first argument
                right = new FunctionCallExpression(right.Location, varExpr.Name, [left]);
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
                // Array indexing: arr@index
                var index = ParsePrimary();
                expr = new ArrayIndexExpression(expr.Location, expr, index);
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

        // Parenthesized expression or function call
        if (Match(TokenType.LParen))
        {
            var location = PreviousToken.Location;

            // Check if this is a function call like (func arg1 arg2)
            if (Check(TokenType.Identifier))
            {
                var name = Advance().Text;
                var args = new List<Expression>();

                while (!Check(TokenType.RParen) && !IsAtEnd())
                {
                    args.Add(ParseExpression());
                }

                Expect(TokenType.RParen, "Expected ')' after function arguments");
                return new FunctionCallExpression(location, name, args);
            }

            // Regular parenthesized expression
            var expr = ParseExpression();
            Expect(TokenType.RParen, "Expected ')' after expression");
            return expr;
        }

        // Variable or function call
        if (Match(TokenType.Identifier))
        {
            var name = PreviousToken.Text;
            var location = PreviousToken.Location;

            // Look ahead: if next token starts a simple argument, this is a function call
            // Note: We only support simple arguments (literals, identifiers) for optional parens
            // For complex arguments (parenthesized expressions), use explicit syntax: (func (expr))
            if (IsArgumentStart(CurrentToken.Type))
            {
                var args = new List<Expression>();

                // Parse simple arguments until we hit a terminator or non-argument token
                while (!IsAtEnd() && IsArgumentStart(CurrentToken.Type))
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

    // Helper methods

    private bool IsTypeKeyword(TokenType type)
    {
        if (type is TokenType.Void or TokenType.Int or TokenType.Float
            or TokenType.Long or TokenType.Double or TokenType.String
            or TokenType.Bool or TokenType.Number or TokenType.Buf)
        {
            return true;
        }

        // Check for special types and plural forms (array types)
        if (type == TokenType.Identifier)
        {
            var text = CurrentToken.Text;

            // Special types
            if (text is "Buffer" or "Note" or "Bar" or "Semitone" or "Cent" or "Millisecond" or "Second" or "Decibel" or "Lazy")
                return true;

            // Plural forms (array types like Ints, Strings, etc.)
            if (text.EndsWith("s"))
            {
                var singular = text.Substring(0, text.Length - 1);
                if (singular is "Void" or "Int" or "Float" or "Long" or "Double"
                    or "String" or "Bool" or "Number" or "Buf" or "Buffer"
                    or "Note" or "Bar" or "Semitone" or "Cent" or "Millisecond" or "Second" or "Decibel")
                    return true;
            }
        }

        return false;
    }

    private bool IsArgumentStart(TokenType type)
    {
        // For optional parentheses syntax, only allow literal arguments
        // This avoids ambiguity with identifiers in expressions
        // To pass variables or complex expressions, use explicit syntax: (func arg)
        return type is TokenType.IntLiteral
            or TokenType.FloatLiteral
            or TokenType.StringLiteral
            or TokenType.BoolLiteral
            or TokenType.NoteLiteral
            or TokenType.SemitoneLiteral
            or TokenType.CentLiteral
            or TokenType.TimeLiteral
            or TokenType.DecibelLiteral;
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
                or TokenType.Use or TokenType.Internal)
            {
                return;
            }

            Advance();
        }
    }
}
