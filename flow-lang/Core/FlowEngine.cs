using FlowLang.Audio;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
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
    private readonly SampleCache _sampleCache;
    private readonly TextWriter? _diagnosticOutput;
    private bool _disposed;

    public ErrorReporter ErrorReporter => _errorReporter;
    public RuntimeContext Context => _context;

    /// <summary>
    /// The audio playback manager for this engine instance.
    /// Shared across REPL evaluations to maintain backend state.
    /// </summary>
    public AudioPlaybackManager AudioManager => _audioManager;

    /// <summary>
    /// Phase 29 REQ-4 — per-engine cache for bundled instrument samples.
    /// Lifetime = engine lifetime (SPEC D-15). Eager-loaded on the first
    /// <c>renderSong</c> call against any given (song, instrument) pair;
    /// subsequent renders in the same engine reuse the cached buffers.
    /// </summary>
    public SampleCache SampleCache => _sampleCache;

    /// <summary>
    /// Phase 29 — exposes the active engine's SampleCache to static renderer code
    /// (<c>SongRenderer.RenderSong</c> is a static method). Set by the FlowEngine
    /// constructor; read by <c>SongRenderer.RenderSong</c> on entry to trigger
    /// eager-load. Single-engine-per-process is a project convention (per
    /// <c>SynthUtils.ResetNoiseRng</c>'s identical static-mutable-state precedent);
    /// if concurrent-engine support is required in v1.5, refactor to thread the
    /// cache through ExecutionContext.
    /// </summary>
    public static SampleCache? CurrentSampleCache { get; private set; }

    public FlowEngine(bool verbose = false) : this(new ErrorReporter(), verbose)
    {
    }

    public FlowEngine(ErrorReporter errorReporter, bool verbose = false)
    {
        _errorReporter = errorReporter;
        _audioManager = new AudioPlaybackManager();
        _sampleCache = new SampleCache();
        // Publish to the static accessor so SongRenderer (a static class) can find
        // this engine's cache on renderSong entry. Cleared in Dispose.
        CurrentSampleCache = _sampleCache;
        _diagnosticOutput = verbose ? Console.Error : null;

        // Create internal function registry and register C# implementations
        var internalRegistry = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterAllImplementations(internalRegistry, _audioManager);
        // Phase 32 Plan 32-04: register (loadScala) overloads + (str Tuning).
        ScalaBuiltins.Register(internalRegistry);

        _context = new RuntimeContext(_errorReporter, internalRegistry, _diagnosticOutput);
        BuiltInFunctions.RegisterIterationGuard(internalRegistry, _context);
        BuiltInFunctions.RegisterContextDependentFunctions(internalRegistry, _context);
        var moduleLoader = new ModuleLoader(_errorReporter, _diagnosticOutput);
        // REQ-4 (Plan 30-03): seed the loader's AdditionalSearchPaths from the active
        // config singleton. Empty list when no config.toml is loaded — zero-cost no-op
        // for existing scripts. flow-cli's FlowConfigLoader.LoadFromXdg() populates
        // FlowConfig.Active before any FlowEngine is constructed, so the read here
        // sees the user's configured paths at process startup.
        foreach (var p in FlowConfig.ConfiguredStdlibSearchPaths)
            moduleLoader.AdditionalSearchPaths.Add(p);
        _interpreter = new Interpreter.Interpreter(_context, _errorReporter, moduleLoader);
        moduleLoader.ParentInterpreter = _interpreter;
    }

    /// <summary>
    /// Execute Flow source code.
    /// </summary>
    public bool Execute(string source, string? fileName = null)
    {
        _errorReporter.Clear();

        try
        {
            // 0. Pre-lex: extract file-scope pragmas (Phase 21 D-01).
            //    Fast path returns the original string reference unchanged when
            //    no `enable` substring is present — preserves Phase 18 byte-identical
            //    determinism for legacy .flow files (Pitfall F mitigation).
            var (pragmaSet, transformedSource) = PragmaScanner.Scan(source, fileName, _errorReporter);
            if (_errorReporter.HasErrors)
                return false;

            // 1. Lex transformed source into tokens (pragmaSet wired for Plan 21-02).
            var lexer = new SimpleLexer(transformedSource, _errorReporter, fileName, pragmaSet);
            var tokens = lexer.Tokenize();

            if (_errorReporter.HasErrors)
                return false;

            // 2. Parse tokens into AST (pragmaSet attached to Program per D-08).
            var parser = new Parser(tokens, _errorReporter, pragmaSet);
            var program = parser.Parse();

            if (_errorReporter.HasErrors)
                return false;

            // 3. Type check AST (skipped for now - types checked at runtime)

            _diagnosticOutput?.WriteLine($"[verbose] Executing {fileName ?? "<eval>"}");

            // Phase 23 D-06/D-07 + Phase 32 D-12/D-14 + Pitfall 2: resolve tuning pragmas to
            // GlobalFrame.MusicalContext.TuningStack (bottom frame) before interpretation.
            // D-07 / D-08 REPL persistence: pragma absence does NOT reset the file-scope
            // frame (no-op when no tuning pragma present). Block-form pushes (Plan 32-06)
            // are popped at REPL eval boundary via ResetBlockTuningStack so a leaked
            // `tuning t { ...` (unclosed) does not bleed into subsequent REPL inputs.
            _context.ResetBlockTuningStack();
            ApplyTuningPragma(program);

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
    /// Phase 23 D-06 + Phase 32 D-12 (Pitfall 2): bridges <c>program.Pragmas</c> →
    /// <c>_context.SetFileScopeTuning(<see cref="RenderTuning"/>)</c> exactly once between
    /// parse and interpret. Only one of the three tuning pragmas can be active per program;
    /// the closed-set registry guarantees unknown names errored out at the PragmaScanner
    /// stage. D-07 / D-08 REPL persistence: when no tuning pragma is present, this method
    /// leaves the existing bottom-of-stack file-scope frame untouched (no <c>SetFileScopeTuning</c>
    /// call). The companion <c>ResetBlockTuningStack</c> call at the start of
    /// <see cref="Execute"/> pops any leaked block frames from a prior REPL eval (D-14).
    /// </summary>
    private void ApplyTuningPragma(Ast.Program program)
    {
        if (program.Pragmas.Has("justIntonation"))
            _context.SetFileScopeTuning(BuildPragmaTuning(TuningSystem.JustIntonation));
        else if (program.Pragmas.Has("pythagorean"))
            _context.SetFileScopeTuning(BuildPragmaTuning(TuningSystem.Pythagorean));
        else if (program.Pragmas.Has("equalTemperament"))
            _context.SetFileScopeTuning(BuildPragmaTuning(TuningSystem.EqualTemperament));
        // else: D-07 / D-08 persistence — leave previous file-scope frame untouched.
    }

    /// <summary>
    /// Phase 32 Plan 32-05: produces the file-scope <see cref="RenderTuning"/> for a Phase 23
    /// tuning pragma. File-scope pragmas precede any key context, so the tonic + mode are
    /// the SongRenderer.ResolveRenderTuning silent-default values (D-02: C major, tonic
    /// ('C', 0)). Per-section <c>key X { ... }</c> blocks REPLACE this resolution at render
    /// time via the existing <see cref="StandardLibrary.Audio.SongRenderer.ResolveRenderTuning"/>
    /// path — this builder just provides a sensible Phase-32-compatible default carrying
    /// the same Phase 23 TuningSystem enum.
    /// </summary>
    private static RenderTuning BuildPragmaTuning(TuningSystem system) =>
        new RenderTuning(system, Mode.Major, 'C', 0);

    /// <summary>
    /// Returns the result of the last evaluated expression from the previous script execution.
    /// </summary>
    public Value? GetLastExpressionResult() => _interpreter.GetLastExpressionValue();

    /// <summary>
    /// Executes entire source code script and returns the result of the last expression.
    /// </summary>
    public Value? ExecuteScriptAndGetResult(string source, string? fileName = null)
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
            // Clear the static accessor only if it still points to this engine —
            // guards against test code that constructs engines back-to-back where
            // the next engine may already have overwritten CurrentSampleCache.
            if (ReferenceEquals(CurrentSampleCache, _sampleCache))
                CurrentSampleCache = null;
        }
    }
}
