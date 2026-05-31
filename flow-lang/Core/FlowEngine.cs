using FlowLang.Audio;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
#if !FLOW_WEB
// Phase 47 D-47-08: SFZ namespace stripped from Web build (Plan 47-01 strip-list).
using FlowLang.StandardLibrary.Audio.Sfz;
#endif
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.StandardLibrary.Generative;
using FlowLang.StandardLibrary.Improv;
using FlowLang.StandardLibrary.Patterns;
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
#if !FLOW_WEB
    // Phase 33 Plan 33-07 — per-engine SFZ sample cache, mirrors _sampleCache lifecycle.
    // Phase 47 D-47-08: SfzSampleCache type stripped from Web build.
    private readonly SfzSampleCache _sfzSampleCache;
#endif
    private readonly ModuleLoader _moduleLoader;
    private readonly TextWriter? _diagnosticOutput;
    private bool _disposed;

    public ErrorReporter ErrorReporter => _errorReporter;
    public RuntimeContext Context => _context;

    /// <summary>
    /// Phase 43 Plan 43-03 — exposes the engine's <see cref="ModuleLoader"/> so
    /// callers (tests + future composer tooling) can seed
    /// <see cref="ModuleLoader.AdditionalSearchPaths"/> before
    /// <see cref="Execute"/> runs. Mirrors the <see cref="AudioManager"/> /
    /// <see cref="SampleCache"/> exposure pattern.
    /// </summary>
    public ModuleLoader ModuleLoader => _moduleLoader;

    /// <summary>
    /// Phase 35 LANG-04 Wave 1 — per-engine registry of source text keyed by
    /// file path (or REPL sentinel <c>&lt;eval&gt;</c> / <c>&lt;stdin&gt;</c> /
    /// <c>&lt;repl&gt;</c>). Populated by <see cref="Execute"/> on every script /
    /// REPL eval BEFORE lexing so the future diagnostic renderer (Wave 2a) can
    /// quote the offending source line without re-reading the file from disk.
    /// </summary>
    public SourceMap SourceMap { get; } = new();

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

#if !FLOW_WEB
    /// <summary>
    /// Phase 33 Plan 33-07 — exposes the active engine's SfzSampleCache to static
    /// renderer code. Mirrors <see cref="CurrentSampleCache"/>'s shape exactly:
    /// set by the FlowEngine constructor, read by <c>SongRenderer.RenderSong</c>'s
    /// new <c>sampler:NAME</c> dispatch branch on entry, cleared in Dispose only
    /// if it still points at this engine's cache instance (back-to-back test
    /// engines guard).
    ///
    /// Phase 47 D-47-08: SFZ subsystem stripped on Web target.
    /// </summary>
    public static SfzSampleCache? CurrentSfzSampleCache { get; private set; }
#endif

    /// <summary>
    /// Phase 33 Plan 33-07 — exposes the active engine's ExecutionContext to
    /// static renderer code so the <c>sampler:NAME</c> dispatch in
    /// <c>SongRenderer.RenderSong</c> can read
    /// <see cref="RuntimeContext.SfzPatchRegistry"/> at render time. Same
    /// single-engine-per-process project convention as <see cref="CurrentSampleCache"/>.
    /// </summary>
    public static RuntimeContext? CurrentExecutionContext { get; private set; }

    /// <summary>
    /// Phase 47 D-47-10: true when this assembly was compiled with
    /// FlowTarget=Web (FLOW_WEB preprocessor symbol active). Compile-time
    /// constant — no runtime mutation. Read by <see cref="Parsing.Parser"/>
    /// (live-block parse-time gate) and <see cref="Runtime.ModuleLoader"/>
    /// (stripped-stdlib advisory) to surface Web-target-aware diagnostics
    /// at parse / load time rather than as silent runtime failures.
    /// </summary>
    public static bool IsWebTarget { get; } =
#if FLOW_WEB
        true;
#else
        false;
#endif

    /// <summary>
    /// Phase 47 D-47-10: false on Web target. `live { ... }` blocks require
    /// <c>System.IO.FileSystemWatcher</c> via <see cref="flow-interpreter"/>'s
    /// LiveReloadManager, which is browser-unavailable. Read by
    /// <see cref="Parsing.Parser"/> at line 220 (TokenType.Live dispatch) —
    /// a parse-time ParseException with Rust-style diagnostic pointing at
    /// the offending source line fires when this property is false.
    /// </summary>
    public static bool SupportsLiveBlocks { get; } = !IsWebTarget;

    public FlowEngine(bool verbose = false) : this(new ErrorReporter(), verbose)
    {
    }

    public FlowEngine(ErrorReporter errorReporter, bool verbose = false)
    {
        _errorReporter = errorReporter;
        _audioManager = new AudioPlaybackManager();
        _sampleCache = new SampleCache();
#if !FLOW_WEB
        // Phase 33 Plan 33-07 — per-engine SFZ sample cache.
        // Phase 47 D-47-08: SFZ subsystem stripped on Web target.
        _sfzSampleCache = new SfzSampleCache();
#endif
        // Publish to the static accessor so SongRenderer (a static class) can find
        // this engine's cache on renderSong entry. Cleared in Dispose.
        CurrentSampleCache = _sampleCache;
#if !FLOW_WEB
        CurrentSfzSampleCache = _sfzSampleCache;
#endif
        _diagnosticOutput = verbose ? Console.Error : null;

        // Create internal function registry and register C# implementations
        var internalRegistry = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterAllImplementations(internalRegistry, _audioManager);

        _context = new RuntimeContext(_errorReporter, internalRegistry, _diagnosticOutput);
        // Phase 33 Plan 33-07 — publish the ExecutionContext to the static
        // accessor so SongRenderer's sampler: dispatch branch can read
        // SfzPatchRegistry at render time. Cleared in Dispose.
        CurrentExecutionContext = _context;
        BuiltInFunctions.RegisterIterationGuard(internalRegistry, _context);
        BuiltInFunctions.RegisterContextDependentFunctions(internalRegistry, _context);
        // Phase 32 Plan 32-04: register (loadScala) overloads + (str Tuning).
        // Phase 44 Plan 44-07: moved post-_context to thread ExecutionContext
        // for strict-mode advisory elevation (unmapped-MIDI-keys).
        ScalaBuiltins.RegisterContextDependent(internalRegistry, _context);
        // Phase 37 Plan 37-01 Task 3 — register the granular builtin (DSP-01).
        // Routes jitter PRNG through ExecutionContext.PrngRegistry keyed by
        // (CurrentCallSite, "granular_offset" | "granular_timing") per
        // D-v1.5-06 + 37-RESEARCH.md Pitfall 8. Same per-engine ownership
        // pattern as PatternFunctions / MarkovFunctions below.
        GranularFunctions.Register(internalRegistry, _context);
        // Phase 37 Plan 37-02 Task 3 — register the stretch + pitchShift
        // builtins (DSP-02 + DSP-03). Both dispatch through StretchEngine /
        // PitchShiftEngine which honor the W4 LOCK knob bag end-to-end
        // (mode + frameSize + hopSize + overlap + transientThreshold +
        // pitchPeriod + windowSize). #auto mode emits the D-37-06 one-shot
        // stderr advisory keyed by (CurrentCallSite, summary).
        StretchFunctions.Register(internalRegistry, _context);
        PitchShiftFunctions.Register(internalRegistry, _context);
        // Phase 36 Plan 36-05 — register the @patterns stdlib's 13 Tidal-style
        // combinators (PAT-01 / PAT-02 / GEN-05). Stochastic combinators
        // (sometimes/degrade/sparseSeq) route their PRNG through
        // ExecutionContext.PrngRegistry — same per-engine ownership as
        // HarmonyFunctions.RegisterContextDependent above.
        PatternFunctions.RegisterContextDependent(internalRegistry, _context);
        // Phase 36 Plan 36-06 — register the @generative stdlib's Markov
        // primitive (GEN-01 / D-36-06 / D-36-07). Unseeded markov/markovGenerate
        // route their PRNG through the same ExecutionContext.PrngRegistry as
        // PatternFunctions; the seeded overloads use new Random(seed) directly.
        MarkovFunctions.RegisterContextDependent(internalRegistry, _context);
        // Phase 36 Plan 36-07 — register the @generative stdlib's L-system
        // primitive (GEN-02 / D-36-06 / D-36-08). Pure deterministic rewrite
        // (no PRNG), so no PrngRegistry routing — but lsystemToSequence invokes
        // a composer-supplied lambda, requiring the context-dependent shape.
        LsystemFunctions.RegisterContextDependent(internalRegistry, _context);
        // Phase 36 Plan 36-08 — register the @generative stdlib's cellular
        // automata primitives (GEN-03 / D-36-08). 1D cellular + cellularSeeded
        // are purely deterministic (single-1-center default); 2D life uses
        // one PRNG-SANCTIONED `new Random(seed)` for the 30%-density initial
        // fill per REQ wording (the seed arg is REQUIRED, so no PrngRegistry
        // routing).
        CellularFunctions.RegisterContextDependent(internalRegistry, _context);
        // Phase 36 Plan 36-09 — register the @generative stdlib's chaos-map
        // primitives (GEN-04 / D-36-08 + D-36-09). Lorenz forward-Euler +
        // logistic recurrence + quantizeToScale bridge. Each primitive
        // derives its single Random from the REQ-mandated seed arg (no
        // PrngRegistry routing — the seed is REQUIRED). Cross-platform FP
        // divergence is documented as platform-specific limitation per
        // D-36-09 / RESEARCH Pitfall 4 — same-platform two-run cmp-clean
        // preserved.
        ChaosFunctions.RegisterContextDependent(internalRegistry, _context);
        // Phase 33 Plan 33-05: wire the SFZ surface — loadSfz(Symbol) +
        // loadSfz(String) + __enableSfzModule(Dict) builtins. All three check
        // ExecutionContext.SfzEnabled at call time, so the registration is
        // safe even when no script imports @sfz (CONTEXT D-10). The
        // __enableSfzModule call inside sfz.flow flips the gate during
        // `use "@sfz"` import.
#if !FLOW_WEB
        // Phase 47 D-47-08: SfzBuiltins.cs is csproj-stripped on Web target
        // (per Plan 47-01). The Register call here would be a missing-type
        // error under FlowTarget=Web; guard so the Web build links. SFZ
        // support is opt-in via `use "@sfz"` even on Desktop, so this guard
        // is composer-invisible. Web-target composers see the charitable
        // advisory from Plan 47-03 Task 3 ModuleLoader gate when they
        // attempt `use "@sfz"`.
        SfzBuiltins.Register(internalRegistry, _context);
#endif
        // Phase 39 Plan 39-01 — register the @notation-io stdlib surface
        // (writeMusicXML / writeLilyPond / abc / mml + __enableNotationIoModule
        // marker). All 4 surface builtins gate on ExecutionContext.NotationIoEnabled
        // (flipped true by the trailing init call in flow-lang/notation-io.flow
        // when a script imports @notation-io). The registration is unconditional —
        // the runtime gate enforces module activation (CONTEXT D-39-01).
        FlowLang.StandardLibrary.Notation.NotationIoBuiltins.Register(internalRegistry, _context);
        // Phase 38 Plan 38-06 — register the @osc stdlib surface
        // (oscSend / oscListen / oscStop / oscBundle / oscSendBundle +
        // __enableOscModule marker). All 5 surface builtins gate on
        // ExecutionContext.OscEnabled (flipped true by the trailing init call
        // in flow-lang/osc.flow when a script imports @osc). Mirrors the
        // Phase 33 SFZ + Phase 39 notation-io pattern. Charitable type-tag
        // inference per D-38-13; per-path drop-newest sample-and-hold rate
        // limit at 200 Hz per D-38-14; bundle nesting depth cap 8 per
        // D-38-15; reference-identity OscHandle lifecycle per D-38-16.
#if !FLOW_WEB
        // Phase 47 D-47-08: OscFunctions.cs is csproj-stripped on Web target
        // (per Plan 47-01). Symmetric with the SfzBuiltins guard above.
        FlowLang.StandardLibrary.Network.OscFunctions.Register(internalRegistry, _context);
#endif
        // Phase 36 Plan 36-11 — register the @improv stdlib surface
        // (registerStyle / listStyles / jam builtins). The jam builtin lives
        // alongside in JamFunctions.RegisterContextDependent, wired below.
        // StyleRegistry.RegisterAndLoadAtEngineInit ALSO loads the shipped
        // baseline packs (jazz/blues/classical) and any user packs under
        // ~/.config/flow/styles/ — last-write-wins per Pitfall 8. We defer
        // the pack-load step until AFTER JamFunctions.RegisterContextDependent
        // has wired `jam` and the std.flow forward-decls have parsed (the
        // packs only need `(dict ...)` + `(registerStyle ...)` which are
        // already registered by BuiltInFunctions + StyleRegistry above).
        StyleRegistry.RegisterBuiltinsOnly(internalRegistry, _context);
        // Phase 36 Plan 36-11 — register the chord-aware Markov `jam` builtin.
        // Routes its unseeded PRNG through ExecutionContext.PrngRegistry keyed
        // by (CurrentCallSite, "jam"); seeded path uses `new Random(seed)`
        // directly (PRNG-SANCTIONED). Reuses MarkovFunctions.TrainMarkov +
        // GenerateMarkov via internal-method exposure.
        JamFunctions.RegisterContextDependent(internalRegistry, _context);
        _moduleLoader = new ModuleLoader(_errorReporter, _diagnosticOutput);
        // REQ-4 (Plan 30-03): seed the loader's AdditionalSearchPaths from the active
        // config singleton. Empty list when no config.toml is loaded — zero-cost no-op
        // for existing scripts. flow-cli's FlowConfigLoader.LoadFromXdg() populates
        // FlowConfig.Active before any FlowEngine is constructed, so the read here
        // sees the user's configured paths at process startup.
        foreach (var p in FlowConfig.ConfiguredStdlibSearchPaths)
            _moduleLoader.AdditionalSearchPaths.Add(p);
        _interpreter = new Interpreter.Interpreter(_context, _errorReporter, _moduleLoader);
        _moduleLoader.ParentInterpreter = _interpreter;

        // Phase 48 fix(48-06) — EXPLICITLY bootstrap the @std builtin SURFACE at
        // engine init. The Flow builtin call surface (print/add/str/createSineTone/
        // play/... — declared as `internal proc` in std.flow, which in turn pulls
        // @collections + @bars) is NOT registered by the C# Register* calls above:
        // those register only IMPLEMENTATIONS keyed by name; the interpreter binds
        // an impl ONLY when a matching `internal proc` surface overload exists
        // (Interpreter.cs:845-848). On Desktop the surface happened to load as a
        // SIDE EFFECT of StyleRegistry.LoadShippedAndUserPacks below (the improv
        // packs `use "@improv"` -> `use "@std"`). That chain is FRAGILE and, on the
        // Web/WASM target, does not fire at all (the style-pack directory does not
        // exist in the Emscripten VFS), so the entire builtin surface was missing
        // in-browser -> `[eval] Function '<name>' not found` for EVERY builtin.
        // See debug session wasm-boot-no-app-bundle (cycle 4). Loading @std here is
        // idempotent (ModuleLoader dedups via _loadedModules), so the later
        // improv-pack `use "@std"` hits AlreadyLoaded and Desktop behavior is
        // unchanged. On Web the embedded-resource fallback in ModuleLoader.LoadModule
        // supplies the .flow source (no host filesystem in the browser).
        _moduleLoader.LoadModule("@std", "<engine-init>", _context);

        // Phase 36 Plan 36-11 — load shipped + user style packs AFTER the
        // interpreter is fully wired. Each pack `use "@improv"` + declares a
        // Dict<Symbol, Value> + calls (registerStyle #name pack), so we need
        // the moduleLoader + parsing/interpretation surface to be ready. Pack
        // loading is charitable — a malformed pack fires a one-shot stderr
        // advisory and CONTINUES; FlowEngine init MUST NOT abort on a bad pack.
        StyleRegistry.LoadShippedAndUserPacks(this, _context);
    }

    /// <summary>
    /// Execute Flow source code.
    /// </summary>
    public bool Execute(string source, string? fileName = null)
    {
        _errorReporter.Clear();

        // Phase 35 LANG-04 Wave 1: register source text into the per-engine
        // SourceMap BEFORE lexing so the future diagnostic renderer (Wave 2a)
        // can quote the offending source line. REPL/eval callers that pass a
        // null fileName register under the <eval> sentinel; subsequent REPL
        // re-evals overwrite the prior entry (no unbounded growth).
        SourceMap.Register(fileName ?? SourceMap.EvalKey, source);

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
            // Phase 44 Plan 44-01 D-02: file-scope strict-mode bit. ApplyStrictPragma
            // mirrors ApplyTuningPragma — top-level Execute is the file-load
            // boundary for the active script; imported modules are handled separately
            // by ModuleLoader.LoadModule's save-set-restore (D-03). Order is
            // tuning-first / strict-second by convention; the two pragmas are
            // independent and neither reads the other's state.
            ApplyStrictPragma(program);
            // Phase 45 D-04 — file-scope beat-true-to-sig bit.
            ApplyBeatTrueToSigPragma(program);

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
    /// Phase 44 Plan 44-01 D-02 / D-03: bridges <c>program.Pragmas</c> →
    /// <see cref="Runtime.ExecutionContext.StrictMode"/> for the top-level
    /// file. Mirrors <see cref="ApplyTuningPragma"/>'s parse-then-set
    /// posture — runs between parse and interpret in <see cref="Execute"/>.
    ///
    /// <para>
    /// Imported files are NOT routed through this method; their file-scope bit
    /// lives in <see cref="ModuleLoader.LoadModule"/>'s save-set-restore around
    /// the imported file's interpreter.Execute call (D-03 — each file's pragma
    /// governs ONLY statements declared in that file).
    /// </para>
    ///
    /// <para>
    /// Unlike <see cref="ApplyTuningPragma"/>, this method overwrites <c>StrictMode</c>
    /// on every Execute (no "persistence" branch): the absence of <c>enable strict;</c>
    /// MUST set StrictMode=false so a prior REPL session's strict bit does not bleed
    /// into a fresh non-strict file's evaluation (Plan 44-10 REPL session flag handles
    /// REPL-level persistence separately).
    /// </para>
    /// </summary>
    private void ApplyStrictPragma(Ast.Program program)
    {
        _context.StrictMode = program.Pragmas.Has("strict");
    }

    /// <summary>
    /// Phase 45 D-04 — bridges <c>program.Pragmas</c> →
    /// <see cref="Runtime.ExecutionContext.BeatTrueToSig"/> for the top-level
    /// file. Mirrors <see cref="ApplyStrictPragma"/>'s parse-then-set posture.
    /// Imported files are handled by <see cref="ModuleLoader.LoadModule"/>'s
    /// save-set-restore (D-04 file-scope semantics).
    ///
    /// <para>
    /// Overwrites on every Execute (no persistence branch): absence of
    /// <c>enable beat-true-to-sig;</c> MUST set the bit to false so a prior REPL
    /// session's pragma does not bleed into a fresh non-pragma file.
    /// </para>
    /// </summary>
    private void ApplyBeatTrueToSigPragma(Ast.Program program)
    {
        _context.BeatTrueToSig = program.Pragmas.Has("beat-true-to-sig");
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

    // ===== Phase 35 Plan 35-04 TEST-01 + TEST-02 — pass-throughs =====

    /// <summary>
    /// Phase 35 Plan 35-04 TEST-01 — read-only view of the test registry
    /// accumulated by (test "name" body) calls during program evaluation.
    /// Consumed by <c>FlowCli.Commands.TestCommand</c> + <c>TestRunner.Run</c>.
    /// </summary>
    public IReadOnlyList<FlowLang.StandardLibrary.TestFramework.TestRecord> TestRegistry
        => _context.TestRegistry;

    /// <summary>
    /// Phase 35 Plan 35-04 TEST-02 — pass-through to <see cref="ExecutionContext.SnapshotState"/>.
    /// </summary>
    public FlowLang.StandardLibrary.TestFramework.TestSnapshot SnapshotState()
        => _context.SnapshotState();

    /// <summary>
    /// Phase 35 Plan 35-04 TEST-02 — pass-through to <see cref="ExecutionContext.RestoreState"/>.
    /// </summary>
    public void RestoreState(FlowLang.StandardLibrary.TestFramework.TestSnapshot snap)
        => _context.RestoreState(snap);

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
#if !FLOW_WEB
            // Phase 33 Plan 33-07 — same back-to-back-engines guard for the
            // SFZ static accessors.
            // Phase 47 D-47-08: SFZ subsystem stripped on Web target.
            if (ReferenceEquals(CurrentSfzSampleCache, _sfzSampleCache))
                CurrentSfzSampleCache = null;
#endif
            if (ReferenceEquals(CurrentExecutionContext, _context))
                CurrentExecutionContext = null;
        }
    }
}
