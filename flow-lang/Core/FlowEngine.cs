using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using RuntimeContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Core;

/// <summary>
/// Main orchestrator for the Flow language engine.
/// Coordinates lexing, parsing, type checking, and interpretation.
/// </summary>
public class FlowEngine
{
    private readonly ErrorReporter _errorReporter;
    private readonly RuntimeContext _context;
    private readonly Interpreter.Interpreter _interpreter;

    public ErrorReporter ErrorReporter => _errorReporter;
    public RuntimeContext Context => _context;

    public FlowEngine()
    {
        _errorReporter = new ErrorReporter();

        // Create internal function registry and register C# implementations
        var internalRegistry = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterAllImplementations(internalRegistry);

        _context = new RuntimeContext(_errorReporter, internalRegistry);
        _interpreter = new Interpreter.Interpreter(_context, _errorReporter);
    }

    public FlowEngine(ErrorReporter errorReporter)
    {
        _errorReporter = errorReporter;

        // Create internal function registry and register C# implementations
        var internalRegistry = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterAllImplementations(internalRegistry);

        _context = new RuntimeContext(_errorReporter, internalRegistry);
        _interpreter = new Interpreter.Interpreter(_context, _errorReporter);
    }

    /// <summary>
    /// Execute Flow source code.
    /// </summary>
    public bool Execute(string source, string? fileName = null)
    {
        _errorReporter.Clear();

        try
        {
            // 1. Lex source into tokens
            var lexer = new SimpleLexer(source, _errorReporter, fileName);
            var tokens = lexer.Tokenize();

            if (_errorReporter.HasErrors)
                return false;

            // 2. Parse tokens into AST
            var parser = new Parser(tokens, _errorReporter);
            var program = parser.Parse();

            if (_errorReporter.HasErrors)
                return false;

            // 3. Type check AST (skipped for now - types checked at runtime)

            // 4. Interpret AST
            _interpreter.Execute(program);

            return !_errorReporter.HasErrors;
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError($"Unexpected error: {ex.Message}", SourceLocation.Unknown);
            return false;
        }
    }

    /// <summary>
    /// Executes source code and returns the result of the last expression.
    /// </summary>
    public Value? ExecuteExpression(string source, string? fileName = null)
    {
        var success = Execute(source, fileName);

        if (!success)
            return null;

        return _interpreter.GetLastExpressionValue();
    }
}
