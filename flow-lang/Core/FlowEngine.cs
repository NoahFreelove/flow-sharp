using FlowLang.Audio;
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
/// Owns the <see cref="AudioPlaybackManager"/> for audio playback lifecycle.
/// </summary>
public class FlowEngine : IDisposable
{
    private readonly ErrorReporter _errorReporter;
    private readonly RuntimeContext _context;
    private readonly Interpreter.Interpreter _interpreter;
    private readonly AudioPlaybackManager _audioManager;
    private bool _disposed;

    public ErrorReporter ErrorReporter => _errorReporter;
    public RuntimeContext Context => _context;

    /// <summary>
    /// The audio playback manager for this engine instance.
    /// Shared across REPL evaluations to maintain backend state.
    /// </summary>
    public AudioPlaybackManager AudioManager => _audioManager;

    public FlowEngine()
    {
        _errorReporter = new ErrorReporter();
        _audioManager = new AudioPlaybackManager();

        // Create internal function registry and register C# implementations
        var internalRegistry = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterAllImplementations(internalRegistry, _audioManager);

        _context = new RuntimeContext(_errorReporter, internalRegistry);
        _interpreter = new Interpreter.Interpreter(_context, _errorReporter);
    }

    public FlowEngine(ErrorReporter errorReporter)
    {
        _errorReporter = errorReporter;
        _audioManager = new AudioPlaybackManager();

        // Create internal function registry and register C# implementations
        var internalRegistry = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterAllImplementations(internalRegistry, _audioManager);

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

    /// <summary>
    /// Stops any currently playing audio.
    /// </summary>
    public void StopAudio()
    {
        _audioManager.StopPlayback();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _audioManager.Dispose();
        }
    }
}
