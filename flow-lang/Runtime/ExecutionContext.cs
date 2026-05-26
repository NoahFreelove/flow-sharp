using FlowLang.Diagnostics;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Runtime;

/// <summary>
/// Manages the execution state including the call stack and function registry.
/// </summary>
public class ExecutionContext
{
    private readonly Stack<StackFrame> _callStack = new();
    private readonly ErrorReporter _errorReporter;
    private readonly OverloadResolver _overloadResolver;
    private readonly TextWriter? _diagnosticOutput;
    private int _callDepth = 0;
    private const int MaxCallDepth = 1000;
    private int _maxIterations = 10000;

    /// <summary>
    /// Maximum number of iterations allowed per loop before the iteration guard triggers.
    /// </summary>
    public int MaxIterations
    {
        get => _maxIterations;
        set => _maxIterations = value > 0 ? value : throw new ArgumentException("MaxIterations must be positive");
    }

    // ===== Random Number Generation State =====
    
    public int FixedRandSeed { get; set; } = 0;
    public Random? FixedGen { get; set; }
    public Random? Gen { get; set; }
    public readonly object RandLock = new();

    public Random GetRand(bool fixedRng = false)
    {
        lock (RandLock)
        {
            if (fixedRng)
            {
                if (FixedGen == null)
                {
                    if (FixedRandSeed == 0) FixedRandSeed = Random.Shared.Next();
                    FixedGen = new Random(FixedRandSeed);
                }
                return FixedGen;
            }
            
            if (Gen == null)
            {
                Gen = new Random(Random.Shared.Next());
            }
            return Gen;
        }
    }

    public void ResetGen()
    {
        lock (RandLock)
        {
            FixedGen = new Random(FixedRandSeed);
        }
    }

    public void SetSeed(int seed)
    {
        FixedRandSeed = seed;
        ResetGen();
    }

    public StackFrame CurrentFrame => _callStack.Peek();
    public StackFrame GlobalFrame { get; }
    public InternalFunctionRegistry InternalRegistry { get; }
    /// <summary>
    /// Section registry — keyed by section name.
    ///
    /// <para>
    /// Phase 36 Plan 36-10 (D-36-18 SECT-01) — value type is
    /// <c>List&lt;SectionData&gt;</c> so multiple overloads with the same
    /// name (but different pattern signatures) can coexist. The list
    /// preserves DECLARATION ORDER which the OverloadResolver uses for
    /// tiebreaker stability and the legacy "single-entry per name" path
    /// is preserved as the trivial case (list of one). Zero-arg bare-name
    /// references in song expressions still resolve correctly through the
    /// <see cref="SectionRegistryFlat"/> helper.
    /// </para>
    /// </summary>
    public Dictionary<string, List<SectionData>> SectionRegistry { get; } = new();

    /// <summary>
    /// Phase 36 Plan 36-10 — flat <c>Dictionary&lt;string, SectionData&gt;</c>
    /// view of <see cref="SectionRegistry"/> for downstream consumers
    /// (SongRenderer / MidiExport / SfzSampleCache) that don't yet know
    /// about overloads. Returns the FIRST entry per name — sufficient for
    /// bare-identifier dispatch on zero-arg sections. Parameterized
    /// sections produce materialized SectionData entries under synthetic
    /// keys (<c>name#callsite</c>) at song-evaluation time so the flat
    /// view captures them too.
    /// </summary>
    public Dictionary<string, SectionData> SectionRegistryFlat()
    {
        var flat = new Dictionary<string, SectionData>();
        foreach (var (key, list) in SectionRegistry)
        {
            if (list.Count > 0)
                flat[key] = list[list.Count - 1];  // last-registered wins for legacy bare-name lookup
        }
        return flat;
    }

    /// <summary>
    /// Phase 35 Plan 35-04 TEST-01 — registry of <c>(test "name" body)</c>
    /// invocations accumulated during program evaluation. Each entry's
    /// <c>BodyThunk</c> is forced by <c>FlowLang.StandardLibrary.TestFramework.TestRunner</c>
    /// inside a Snapshot/Restore guard so the per-test mutations enumerated
    /// in RESEARCH §Pitfall 3 do not leak.
    /// </summary>
    public List<FlowLang.StandardLibrary.TestFramework.TestRecord> TestRegistry { get; } = new();

    /// <summary>
    /// Per-context Symbol intern table — guarantees pointer equality for <c>#foo</c> literals
    /// (Phase 26.1 SYM-01). All <c>Value.Symbol(name, ctx)</c> calls with the same name and the
    /// same context return the same <see cref="Value"/> instance, so reference-equality of the
    /// Value wrappers is the canonical Symbol equality check.
    /// </summary>
    public Dictionary<string, Value> SymbolInternTable { get; } = new();

    /// <summary>
    /// Phase 36 Plan 36-01 (D-v1.5-06 / D-36-09) — per-context PRNG registry
    /// keyed by <c>(SourceLocation, generator-name)</c>. All Phase 36 PRNG-driven
    /// primitives (<c>markov</c> / <c>lsystem</c> / <c>cellular</c> / <c>lorenz</c> /
    /// <c>logistic</c> / <c>degrade</c> / <c>sparseSeq</c> / <c>sometimes</c> /
    /// <c>jam</c>) route their unseeded paths through this registry. Reseeded
    /// at every <c>renderSong</c> / <c>writeWav</c> / <c>exportWav</c> boundary
    /// to preserve the two-run cmp-clean determinism contract inherited from
    /// Phase 18/25/27/28/29/33.
    /// </summary>
    public PrngRegistry PrngRegistry { get; } = new();

    /// <summary>
    /// Phase 38 Plan 38-02 (LIVE-01 / D-38-02) — per-context registry of the
    /// composer's active <c>live &lt;quantize&gt; { ... }</c> blocks, keyed by
    /// <see cref="FlowLang.Ast.Statements.LiveBlockStatement.BlockId"/> (FNV-1a
    /// of source location). The <see cref="Interpreter.Interpreter"/>'s
    /// <c>ExecuteLiveBlock</c> branch registers each block on initial run; Plan
    /// 38-03's <c>LiveReloadManager.StagePendingBuffers</c> consumes the
    /// <see cref="LiveBlockRegistry.Snapshot"/> at swap time to diff old vs.
    /// new and stage per-block pending buffers. Mirrors the
    /// <see cref="PrngRegistry"/> shape — singleton-per-context,
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>-backed
    /// for the background-render + audio-thread two-actor pattern.
    /// </summary>
    public LiveBlockRegistry LiveBlockRegistry { get; } = new();

    /// <summary>
    /// Phase 43 Plan 43-02 (D-05 / D-02) — per-context module registry keyed by
    /// the name declared via a top-of-file <c>module &lt;name&gt;</c> statement.
    /// Populated by <c>ModuleLoader</c> at <c>use</c>-time when the loaded file
    /// carries a module declaration as its first non-comment statement (Plan
    /// 43-03 hook). Read at qualified-access dispatch time by
    /// <c>ExpressionEvaluator.EvaluateMemberAccess</c>'s registry-first branch
    /// (also Plan 43-03 per D-02): for <c>math.sin(0.5)</c>, the dispatcher
    /// short-circuits to the registry hit BEFORE falling through to the
    /// existing instance-member path. Files without a <c>module</c> declaration
    /// are absent from this registry per D-01 back-compat — they continue to
    /// expose procs unqualified in the caller's frame.
    ///
    /// <para>
    /// Mirrors the <see cref="LiveBlockRegistry"/> / <see cref="PrngRegistry"/>
    /// shape — singleton-per-context,
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>-backed.
    /// The per-context (NOT process-global) lifetime is load-bearing for
    /// Phase 35 TEST-02 hermetic isolation — a static singleton would leak
    /// across FlowEngine instances. See RESEARCH §Pattern 4 + §"Alternatives
    /// Considered" for rationale.
    /// </para>
    /// </summary>
    public ModuleRegistry ModuleRegistry { get; } = new();

    /// <summary>
    /// Phase 43 Plan 43-03 (D-04) — per-context map of unqualified proc name
    /// → owning module name. Populated by <c>ModuleLoader</c>'s registration
    /// hook (Plan 43-03) immediately AFTER the registry registration step.
    /// Read at the SAME hook on the next module's load: if a proc this module
    /// exports is already owned by a DIFFERENT prior module, the loader fires
    /// the one-shot D-04 last-import-wins shadow advisory via
    /// <c>RenderingDiagnostics.WarnOnce</c> keyed by
    /// <c>module-shadow:&lt;priorOwner&gt;:&lt;newOwner&gt;:&lt;procName&gt;</c>.
    ///
    /// <para>
    /// Last-write-wins on collision (the new module CLAIMS the unqualified
    /// name). Procs from MODULE-LESS files do NOT update this map — those
    /// files don't claim a namespace per D-01, so a module-less proc colliding
    /// with a later module's proc fires no advisory (the existing
    /// <c>StackFrame.DeclareFunction</c> overwrite gives last-import-wins for
    /// the unqualified call resolution naturally).
    /// </para>
    /// </summary>
    public Dictionary<string, string> ProcOwnership { get; } = new();

    /// <summary>
    /// Phase 36 Plan 36-11 (D-36-12, IMPROV-01) — style-pack registry keyed by
    /// Symbol-typed <see cref="Value"/>. Populated at FlowEngine init by
    /// <c>StyleRegistry.LoadAtEngineInit</c> (shipped packs at
    /// <c>flow-lang/improv/styles/*.flow</c> first, then user packs at
    /// <c>~/.config/flow/styles/*.flow</c>; last-write-wins per Pitfall 8). The
    /// <c>(registerStyle #name pack)</c> builtin mutates this dict at runtime.
    /// Each value is a <see cref="DictData"/> holding the composer's rule-pack
    /// content (beat_weights / interval_transitions / rhythmic_template /
    /// articulation_distribution per the contract in
    /// <c>flow-lang/improv/styles/README.md</c>).
    ///
    /// <para>
    /// Keys use the same equality semantics as <see cref="SymbolInternTable"/>
    /// (interned Symbol values → pointer equality), so the standard
    /// <see cref="Value"/>-keyed Dictionary lookup works directly without a
    /// custom comparer.
    /// </para>
    /// </summary>
    public Dictionary<Value, DictData> StyleRegistry { get; } = new();

    /// <summary>
    /// Phase 36 Plan 36-11 — dedup set for the per-style override advisory
    /// emitted by the user-pack load path (Pitfall 8). Tracks Symbol name
    /// strings (not Values) because the user-pack file is loaded into the SAME
    /// <see cref="ExecutionContext"/> as the shipped packs — the override
    /// detection sees the EXISTING entry already in <see cref="StyleRegistry"/>
    /// and fires the advisory keyed by the symbol name string.
    /// </summary>
    public HashSet<string> StyleOverrideAdvisoriesEmitted { get; } = new();

    /// <summary>
    /// Phase 36 Plan 36-11 — when <c>true</c>, the <c>registerStyle</c> builtin
    /// suppresses its "user overrides shipped" advisory for the duration of the
    /// flag. Set by <c>StyleRegistry.LoadShippedAndUserPacks</c> during the
    /// shipped-pack phase only — re-registering the same shipped pack between
    /// processes (e.g., back-to-back FlowEngine instances) is NOT an override
    /// from the composer's perspective. The user-pack phase un-sets the flag
    /// so a user pack with the same Symbol as a shipped pack DOES fire the
    /// override advisory once.
    /// </summary>
    public bool SuppressStyleOverrideAdvisory { get; set; } = false;

    /// <summary>
    /// Phase 36 Plan 36-05 — the currently-evaluating builtin call site, set by
    /// <c>ExpressionEvaluator.EvaluateFunctionCall</c> immediately before invoking
    /// the registered C# lambda and cleared (reset to <see cref="SourceLocation.Unknown"/>)
    /// after the lambda returns. Phase 36 stochastic combinators in
    /// <c>StandardLibrary/Patterns/</c> read this to feed
    /// <c>PrngRegistry.GetRandom(callSite, name)</c> — the lambda signature
    /// <c>Func&lt;IReadOnlyList&lt;Value&gt;, Value&gt;</c> does not carry a
    /// SourceLocation argument, and threading a new overload would touch every
    /// existing registration site. Setting a per-context field is the smaller diff.
    ///
    /// <para>
    /// CONTRACT: read this ONLY from inside a builtin lambda body. Outside that
    /// scope it may be stale or <see cref="SourceLocation.Unknown"/>. Builtins
    /// that nest builtin calls (e.g., a combinator that internally invokes
    /// another combinator) will see their own outer call's location for the
    /// duration of the nested execution because the evaluator stack pops back
    /// up — the field is not stack-allocated. This is acceptable for Phase 36
    /// PRNG keying: the OUTER combinator's call site is what determines the
    /// (site, name) PRNG key, which is the determinism contract we want.
    /// </para>
    /// </summary>
    public Core.SourceLocation CurrentCallSite { get; set; } = Core.SourceLocation.Unknown;

    // ===== Phase 33 — SFZ surface =====

    /// <summary>
    /// Phase 33 — flips <c>true</c> when the <c>__enableSfzModule</c> marker
    /// builtin runs (triggered by <c>use "@sfz"</c> in a script). Until then,
    /// <c>loadSfz</c> and the <c>sampler:NAME</c> instrument-string dispatcher
    /// are gated off and raise <c>UndefinedFunctionError</c> /
    /// <c>UnknownInstrumentError</c> respectively. Default <c>false</c>.
    /// </summary>
    public bool SfzEnabled { get; set; } = false;

    /// <summary>
    /// Phase 39 D-39-01 — flips <c>true</c> when the <c>__enableNotationIoModule</c>
    /// marker builtin runs (triggered by <c>use "@notation-io"</c> in a script).
    /// Until then, <c>writeMusicXML</c> / <c>writeLilyPond</c> / <c>abc</c> /
    /// <c>mml</c> are gated off and raise a clear "requires <c>use \"@notation-io\"</c>"
    /// error. Default <c>false</c>.
    /// </summary>
    public bool NotationIoEnabled { get; set; } = false;

    /// <summary>
    /// Phase 38 Plan 38-06 OSC-01 — flips <c>true</c> when the
    /// <c>__enableOscModule</c> marker builtin runs (triggered by
    /// <c>use "@osc"</c> in a script). Until then, <c>oscSend</c> /
    /// <c>oscListen</c> / <c>oscStop</c> / <c>oscBundle</c> /
    /// <c>oscSendBundle</c> are gated off and raise a clear
    /// "requires <c>use \"@osc\"</c>" error. Mirrors the Phase 33
    /// <see cref="SfzEnabled"/> / Phase 39 <see cref="NotationIoEnabled"/>
    /// posture. Default <c>false</c>.
    /// </summary>
    public bool OscEnabled { get; set; } = false;

    /// <summary>
    /// Phase 33 — 19-entry GM-orchestral Symbol → relative-path map populated
    /// from <c>flow-lang/sfz.flow</c> via <c>__enableSfzModule</c> per CONTEXT
    /// D-09 / D-11 (the dict lives in Flow source, not C#, so composers can
    /// inspect / extend it without a C# rebuild). Read by <c>loadSfz(Symbol)</c>
    /// to look up the relative path before joining with <see cref="ResolvedSfzRoot"/>.
    /// Empty until the module imports.
    /// </summary>
    public Dictionary<Value, string> SfzInstruments { get; } = new();

    /// <summary>
    /// Phase 33 — variable-name → patch registry per CONTEXT D-12. Populated by
    /// <c>Interpreter.ExecuteVariableDeclaration</c> (Plan 33-07) when the
    /// declared type is <c>SfzType</c>; the assignment handler writes
    /// <c>(name, sfzValue.As&lt;SfzData&gt;())</c> into this dict alongside the
    /// normal <c>CurrentFrame.SetVariable</c> call. Read by
    /// <c>SongRenderer</c>'s <c>sampler:NAME</c> branch (Plan 33-07) to resolve
    /// the bound patch.
    ///
    /// Per Pitfall 10: last-bound-wins per variable name within an
    /// ExecutionContext — reassigning a same-name variable overwrites the
    /// prior registry entry, matching Flow's variable-shadowing semantics.
    /// </summary>
    public Dictionary<string, FlowLang.StandardLibrary.Audio.Sfz.SfzData> SfzPatchRegistry { get; } = new();

    /// <summary>
    /// Phase 33 — one-shot stderr advisory dedup set, keyed by sentinel strings
    /// of the form <c>sfz:opcode:{patch}:{name}</c>,
    /// <c>sfz:missing:{patch}:{midi}:{vel}</c>, or
    /// <c>sfz:config:sfz_root_missing</c>. Used via the Phase 23/32
    /// <c>RenderingDiagnostics.WarnOnce(key, message)</c> pattern (Plans 33-04 /
    /// 33-05 / 33-06 add the SFZ-specific overload). The dedup is per-context
    /// rather than per-process so each FlowEngine instance gets a fresh slate.
    /// </summary>
    public HashSet<string> SfzDiagnostics { get; } = new();

    /// <summary>
    /// Phase 33 — first-read cache for <c>FlowConfig.Active.SfzRoot</c> per
    /// 33-RESEARCH § Pitfall 2. <see cref="FlowConfig.Active"/> is mutable
    /// (test isolation pollutes the singleton); reading the value once at the
    /// first <c>loadSfz</c> call within a given <see cref="ExecutionContext"/>
    /// and caching here prevents (a) test-order-dependent failures and
    /// (b) script-time config edits from affecting an in-flight render.
    /// <c>null</c> until first read (or until first read returns null).
    /// </summary>
    public string? ResolvedSfzRoot { get; set; } = null;

    /// <summary>
    /// Invoker used to execute userspace functions/lambdas from standard library or engine.
    /// Injected by the interpreter upon creation.
    /// </summary>
    public Interpreter.IFunctionInvoker? Invoker { get; set; }

    /// <summary>
    /// Phase 35 Plan 35-06 (D-v1.5-05) — the active program-level
    /// <see cref="Lexing.PragmaSet"/> exposed as a backup access point. Most
    /// pragma queries from the evaluator should consult the AST node's own
    /// captured PragmaSet (see <see cref="Ast.Expressions.MatchExpression.CapturedPragmas"/>)
    /// because pragmas are PER-FILE (Pitfall 4 / Phase 21 D-06) — an
    /// imported module's pragma set differs from the importer's, and the
    /// AST-attached PragmaSet captures that scope at parse time. This
    /// context-level property is null unless explicitly published by the
    /// top-level <see cref="FlowEngine"/> driver, and reads the importer's
    /// (outer) pragma set when set.
    /// </summary>
    public Lexing.PragmaSet? ProgramPragmaSet { get; set; }

    public ExecutionContext(ErrorReporter errorReporter, InternalFunctionRegistry internalRegistry, TextWriter? diagnosticOutput = null)
    {
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        InternalRegistry = internalRegistry ?? throw new ArgumentNullException(nameof(internalRegistry));
        _diagnosticOutput = diagnosticOutput;
        _overloadResolver = new OverloadResolver(errorReporter, diagnosticOutput);

        // Create and push global frame
        GlobalFrame = new StackFrame();
        _callStack.Push(GlobalFrame);
    }

    /// <summary>
    /// The diagnostic output writer for verbose logging (null when verbose mode is off).
    /// </summary>
    public TextWriter? DiagnosticOutput => _diagnosticOutput;

    /// <summary>
    /// Pushes a new stack frame for a function call.
    /// </summary>
    public void PushFrame()
    {
        _callDepth++;
        if (_callDepth > MaxCallDepth)
            throw new InvalidOperationException($"Stack overflow: maximum call depth of {MaxCallDepth} exceeded");

        var newFrame = new StackFrame(CurrentFrame);
        _callStack.Push(newFrame);
    }

    /// <summary>
    /// Pops the current stack frame after a function returns.
    /// </summary>
    public void PopFrame()
    {
        if (_callStack.Count <= 1)
            throw new InvalidOperationException("Cannot pop global frame");

        _callStack.Pop();
        _callDepth--;
    }

    /// <summary>
    /// Declares a variable in the current frame.
    /// </summary>
    public void DeclareVariable(string name, Value value)
    {
        CurrentFrame.DeclareVariable(name, value);
    }

    /// <summary>
    /// Gets a variable from the current scope or parent scopes.
    /// </summary>
    public Value GetVariable(string name)
    {
        return CurrentFrame.GetVariable(name);
    }

    /// <summary>
    /// Sets a variable in the current scope or parent scopes.
    /// </summary>
    public void SetVariable(string name, Value value)
    {
        CurrentFrame.SetVariable(name, value);
    }

    /// <summary>
    /// Declares a function overload.
    /// </summary>
    public void DeclareFunction(FunctionOverload overload)
    {
        CurrentFrame.DeclareFunction(overload);
    }

    /// <summary>
    /// Resolves a function call to a specific overload.
    ///
    /// <para>
    /// Phase 36 Plan 36-02 (D-36-11): <paramref name="namedArgTypes"/>
    /// forwards named-argument Type info to <see cref="OverloadResolver"/>
    /// when the call surface uses <c>(fn name=value)</c>. Existing callers
    /// pass null (defaulted) — legacy positional-only behavior is preserved.
    /// </para>
    /// </summary>
    public FunctionOverload? ResolveFunction(string name, IReadOnlyList<FlowType> argTypes, Core.SourceLocation? location = null,
        IReadOnlyDictionary<string, FlowType>? namedArgTypes = null)
    {
        var overloads = CurrentFrame.GetFunctionOverloads(name);

        if (overloads.Count == 0)
        {
            _diagnosticOutput?.WriteLine($"[verbose] Function '{name}' not found (0 overloads registered)");
            _errorReporter.ReportError($"Function '{name}' not found", location);
            return null;
        }

        // Bundle A (260524-r4o) Task 2 — FunctionOverload-direct resolve:
        // no Signature projection, no FirstOrDefault reverse-lookup.
        return _overloadResolver.Resolve(name, overloads, argTypes, location, namedArgTypes);
    }

    /// <summary>
    /// Resolves the current musical context by walking the stack from top to bottom.
    /// First non-null value for each property wins. Uses defaults for any unresolved properties.
    /// Defaults: 4/4 time signature, 120 BPM, 0.5 swing (straight), no key.
    ///
    /// Phase 32 D-12: <see cref="MusicalContext.TuningStack"/> resolution walks frames
    /// top-to-bottom and copies the FIRST non-empty stack onto the resolved context.
    /// This preserves the existing innermost-wins semantic of the other ??= fields:
    /// readers consume <see cref="MusicalContext.ActiveTuning"/> on the returned
    /// context, which peeks the resolved stack's top frame.
    /// </summary>
    public MusicalContext GetMusicalContext()
    {
        var resolved = new MusicalContext();
        bool tuningResolved = false;
        foreach (var frame in _callStack)
        {
            if (frame.MusicalContext != null)
            {
                resolved.TimeSignature ??= frame.MusicalContext.TimeSignature;
                resolved.Tempo ??= frame.MusicalContext.Tempo;
                resolved.Swing ??= frame.MusicalContext.Swing;
                resolved.Key ??= frame.MusicalContext.Key;
                resolved.Velocity ??= frame.MusicalContext.Velocity;
                resolved.Pan ??= frame.MusicalContext.Pan;
                resolved.Gain ??= frame.MusicalContext.Gain;
                resolved.ReverbTime ??= frame.MusicalContext.ReverbTime;
                // Phase 32 D-12 (supersedes Phase 23 D-05): Tuning is a push/pop stack.
                // Resolution walks frames top-to-bottom and adopts the first non-empty
                // stack encountered. Innermost-frame-wins matches the existing ??= shape
                // of the other fields. File-scope pragmas live on GlobalFrame.MusicalContext;
                // block forms (Plan 32-06) push above on the innermost frame, so a hit
                // higher in the call stack naturally wins.
                if (!tuningResolved && frame.MusicalContext.TuningStack.Count > 0)
                {
                    // Reference-share the stack — readers consume ActiveTuning (peek),
                    // not the stack instance, so aliasing is safe + cheaper than cloning.
                    foreach (var rt in new Stack<StandardLibrary.Audio.Tuning.RenderTuning>(frame.MusicalContext.TuningStack))
                        resolved.TuningStack.Push(rt);
                    tuningResolved = true;
                }
                // Phase 28 SPEC-7: voice pool size inherits via the same ??= chain.
                // null means "no override" — SequenceRenderer.RenderSequenceToVoicesWithPool
                // applies the SPEC-7 locked default of 32 at the render call.
                resolved.VoicePoolSize ??= frame.MusicalContext.VoicePoolSize;
            }
            if (resolved.TimeSignature != null && resolved.Tempo != null
                && resolved.Swing != null && resolved.Key != null
                && resolved.Velocity != null && resolved.Pan != null
                && resolved.Gain != null && resolved.ReverbTime != null
                && tuningResolved && resolved.VoicePoolSize != null)
                break;
        }
        // REQ-4 (Plan 30-03): three-tier fallback for Tempo + TimeSignature.
        //   1. Call-stack-resolved value (active tempo/timesig block) — already
        //      consumed in the ??= chain above.
        //   2. FlowConfig.Active (~/.config/flow/config.toml override) — this layer.
        //   3. Hard-coded baked default (120 BPM / 4/4) — final fallback.
        // Swing has no config knob in SPEC-4 so it skips tier 2.
        resolved.Tempo ??= FlowConfig.Active.DefaultTempo.HasValue
            ? (double)FlowConfig.Active.DefaultTempo.Value
            : 120.0;
        resolved.TimeSignature ??= ParseTimesigOrDefault(FlowConfig.Active.DefaultTimesig);
        resolved.Swing ??= 0.5;
        return resolved;
    }

    /// <summary>
    /// REQ-4 (Plan 30-03): parse the <c>default_timesig</c> config string ("N/M") into
    /// a <see cref="TypeSystem.SpecialTypes.TimeSignatureData"/>. Charitable per
    /// CLAUDE.md feedback_charitable_interpretation memory:
    ///   - null / whitespace -> 4/4 silently
    ///   - malformed (not "N/M" with positive integers AND power-of-2 denominator)
    ///     -> 4/4 + single stderr Warning at first encounter. The static guard
    ///     <see cref="_timesigWarningEmitted"/> avoids spamming the warning on every
    ///     <see cref="GetMusicalContext"/> call (note streams + bars + songs all hit
    ///     this code path).
    /// </summary>
    private static TypeSystem.SpecialTypes.TimeSignatureData ParseTimesigOrDefault(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return new TypeSystem.SpecialTypes.TimeSignatureData(4, 4);
        var parts = config.Split('/');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var num) && num > 0
            && int.TryParse(parts[1], out var den) && den > 0
            // TimeSignatureData constructor validates denominator-is-power-of-2;
            // pre-check here so the throw becomes a charitable fallback instead.
            && (den & (den - 1)) == 0)
        {
            return new TypeSystem.SpecialTypes.TimeSignatureData(num, den);
        }
        if (!_timesigWarningEmitted)
        {
            Console.Error.WriteLine(
                $"Warning: malformed default_timesig in config.toml: \"{config}\" — falling back to 4/4.");
            _timesigWarningEmitted = true;
        }
        return new TypeSystem.SpecialTypes.TimeSignatureData(4, 4);
    }

    // Test-only access: reset the one-shot warning latch so successive tests can
    // each independently assert the malformed-timesig path. Intentionally internal-
    // scoped through reflection-free static reset — production code never touches it.
    private static bool _timesigWarningEmitted = false;
    internal static void ResetTimesigWarningLatchForTests() => _timesigWarningEmitted = false;

    /// <summary>
    /// Phase 32 D-12 transitional shim: bridges Phase 23's
    /// <c>SetTuning(TuningSystem?)</c> callers through to <see cref="SetFileScopeTuning"/>.
    /// Marked <see cref="ObsoleteAttribute"/> so any unmigrated FlowEngine pragma bridge
    /// surfaces as a compile warning; replaced by <see cref="SetFileScopeTuning"/> in
    /// Plan 32-05 Task 2 (FlowEngine.ApplyTuningPragma builds a <see cref="RenderTuning"/>
    /// from the pragma name). Will be removed after Plan 32-06 lands.
    /// </summary>
    [Obsolete("Phase 32 D-12: use SetFileScopeTuning(RenderTuning). Scheduled for removal after Plan 32-06 lands.")]
    public void SetTuning(TuningSystem? tuning)
    {
        if (tuning is null) return; // D-07: no-op on null — preserve previous REPL state.
        // Use the same defaults SongRenderer.ResolveRenderTuning would fall back to when
        // no key context exists (D-02 silent C-major default): tonic = ('C', 0),
        // mode = Major. The full key-aware resolution happens at section render time.
        SetFileScopeTuning(new RenderTuning(tuning.Value, Mode.Major, 'C', 0));
    }

    /// <summary>
    /// Phase 32 D-12 (supersedes Phase 23 D-06/D-07 <c>SetTuning(TuningSystem?)</c>):
    /// REPLACES the bottom-of-stack file-scope pragma frame on
    /// <see cref="StackFrame.MusicalContext"/>.<see cref="MusicalContext.TuningStack"/>
    /// of the global frame. Called by <see cref="FlowLang.Core.FlowEngine"/>'s pragma
    /// bridge once between parse and interpret.
    ///
    /// Algorithm (Pitfall 2 — bottom frame is sticky across REPL evals):
    /// <list type="number">
    ///   <item>Pop any block frames above the file-scope frame (defensive — if
    ///   <c>ResetBlockTuningStack</c> wasn't called at the prior REPL boundary,
    ///   the global frame's stack should still only carry the bottom pragma frame).</item>
    ///   <item>Pop the existing file-scope frame (if any).</item>
    ///   <item>Push the new <paramref name="renderTuning"/> as the new bottom frame.</item>
    /// </list>
    /// Net result: <c>GlobalFrame.MusicalContext.TuningStack.Count == 1</c>, containing
    /// the new file-scope tuning. D-08 REPL stickiness (carried over from Phase 23):
    /// FlowEngine's <c>ApplyTuningPragma</c> only calls this when a tuning pragma is
    /// actually present in the parsed program; absent-pragma case leaves the previous
    /// frame untouched.
    /// </summary>
    public void SetFileScopeTuning(RenderTuning renderTuning)
    {
        if (GlobalFrame.MusicalContext == null)
            GlobalFrame.MusicalContext = new MusicalContext();
        var stack = GlobalFrame.MusicalContext.TuningStack;
        while (stack.Count > 0)
            stack.Pop();
        stack.Push(renderTuning);
    }

    /// <summary>
    /// Phase 32 D-12 + Plan 32-06 entry: pushes a <see cref="RenderTuning"/> onto the
    /// topmost (current-frame) <see cref="MusicalContext.TuningStack"/>. Used by the
    /// <c>tuning t { ... }</c> block interpreter case to layer a block tuning above
    /// the file-scope pragma frame. The paired <see cref="PopTuning"/> is invoked in
    /// the block's exit (try/finally per Plan 32-06).
    /// </summary>
    public void PushTuning(RenderTuning renderTuning)
    {
        if (CurrentFrame.MusicalContext == null)
            CurrentFrame.MusicalContext = new MusicalContext();
        CurrentFrame.MusicalContext.TuningStack.Push(renderTuning);
    }

    /// <summary>
    /// Phase 32 D-12 + Plan 32-06 exit: pops the topmost frame's
    /// <see cref="MusicalContext.TuningStack"/>. Throws <see cref="InvalidOperationException"/>
    /// when the stack is empty — defensive guard that should never fire if push/pop pairs
    /// are balanced via try/finally per Plan 32-06.
    /// </summary>
    public void PopTuning()
    {
        if (CurrentFrame.MusicalContext == null || CurrentFrame.MusicalContext.TuningStack.Count == 0)
            throw new InvalidOperationException(
                "PopTuning called with an empty TuningStack — push/pop must be balanced (Phase 32 D-12).");
        CurrentFrame.MusicalContext.TuningStack.Pop();
    }

    /// <summary>
    /// Phase 32 D-14 + Pitfall 2 — REPL eval boundary hook: pops the global frame's
    /// <see cref="MusicalContext.TuningStack"/> down to at most ONE entry (the file-scope
    /// pragma frame). Block-form pushes above the pragma frame are ephemeral per D-14;
    /// the pragma frame stays sticky across REPL evals per Phase 23 D-08 carried forward.
    /// Called by the REPL eval boundary in <see cref="FlowLang.Core.FlowEngine"/>.
    ///
    /// Cardinality contract: after this call, <c>GlobalFrame.MusicalContext.TuningStack.Count</c>
    /// is <c>≤ 1</c> — exactly the file-scope pragma frame if one was pushed; empty otherwise.
    /// </summary>
    public void ResetBlockTuningStack()
    {
        if (GlobalFrame.MusicalContext == null) return;
        var stack = GlobalFrame.MusicalContext.TuningStack;
        while (stack.Count > 1)
            stack.Pop();
    }

    /// <summary>
    /// Tries to resolve a function without reporting errors (for probing).
    ///
    /// <para>
    /// Phase 36 Plan 36-02 (D-36-11): <paramref name="namedArgTypes"/>
    /// forwards named-arg info to <see cref="OverloadResolver"/> without
    /// surfacing rejection diagnostics. The Interpreter's main resolution
    /// path calls this first to probe; only on null does it fall back to
    /// <see cref="ResolveFunction"/> for the error-reporting pass.
    /// </para>
    /// </summary>
    public FunctionOverload? TryResolveFunction(string name, IReadOnlyList<FlowType> argTypes,
        IReadOnlyDictionary<string, FlowType>? namedArgTypes = null)
    {
        var overloads = CurrentFrame.GetFunctionOverloads(name);

        if (overloads.Count == 0)
            return null;

        var signatures = overloads.Select(o => o.Signature).ToList();

        // Create a temporary error reporter that doesn't actually report
        var tempReporter = new ErrorReporter();
        var tempResolver = new OverloadResolver(tempReporter);
        var signature = tempResolver.Resolve(name, signatures, argTypes, null, namedArgTypes);

        if (signature == null)
            return null;

        return overloads.FirstOrDefault(o => o.Signature == signature);
    }

    // ===================================================================
    // Phase 35 Plan 35-04 TEST-02 — hermetic-isolation surface
    // ===================================================================
    //
    // SnapshotState / RestoreState capture the 11+ mutable surfaces
    // enumerated in 35-RESEARCH.md §Pitfall 3 so the TestRunner can
    // wrap each (test "name" body) invocation in a clean slate.
    //
    // Per RESEARCH Notable Departures: NO reflection. Every captured
    // field has an explicit read/restore site below — adding a new
    // mutable surface to the engine requires touching THREE places
    // (TestSnapshot record + SnapshotState + RestoreState). This is
    // intentional: the explicit list makes leak audits possible.
    //
    // NOT reset (per RESEARCH Assumption A8): AudioPlaybackManager —
    // tests must not trigger live (play ...) playback. Documented in
    // the SUMMARY as a follow-up CLAUDE.md edit.

    /// <summary>
    /// Phase 35 Plan 35-04 TEST-02 — captures the 11+ mutable state
    /// surfaces into an immutable <see cref="FlowLang.StandardLibrary.TestFramework.TestSnapshot"/>
    /// record. Called by <c>TestRunner</c> before each test body runs.
    /// </summary>
    public FlowLang.StandardLibrary.TestFramework.TestSnapshot SnapshotState()
    {
        return new FlowLang.StandardLibrary.TestFramework.TestSnapshot
        {
            // 1-3. Global frame variables, test registry size, section registry.
            GlobalVariables = GlobalFrame.SnapshotLocalVariables(),
            TestRegistryCount = TestRegistry.Count,
            // Phase 36 Plan 36-10 — deep-copy each per-name list so post-snapshot
            // mutations on the live registry don't leak into the snapshot.
            SectionRegistry = SectionRegistry.ToDictionary(
                kvp => kvp.Key,
                kvp => new List<SectionData>(kvp.Value)),

            // 4. Phase 26.1 — Symbol intern table.
            SymbolInternTable = new Dictionary<string, Value>(SymbolInternTable),

            // 5. PRNG state — FixedRandSeed + FixedGen + Gen. Random itself is
            //    mutable; we capture the references AND the seed. Restore
            //    rebuilds FixedGen from the captured seed so the next draw
            //    is identical to the pre-snapshot draw.
            FixedRandSeed = FixedRandSeed,
            FixedGen = FixedGen,
            Gen = Gen,

            // 6. Musical-context stack — clone the global frame's
            //    MusicalContext (carries tuning stack + tempo + key + ...).
            GlobalFrameMusicalContext = GlobalFrame.MusicalContext?.Clone(),

            // 7-10. Phase 33 SFZ statics.
            SfzEnabled = SfzEnabled,
            SfzInstruments = new Dictionary<Value, string>(SfzInstruments),
            SfzPatchRegistry =
                new Dictionary<string, FlowLang.StandardLibrary.Audio.Sfz.SfzData>(SfzPatchRegistry),
            SfzDiagnostics = new HashSet<string>(SfzDiagnostics),
            ResolvedSfzRoot = ResolvedSfzRoot,

            // 10b. Phase 39 — notation-io module gate.
            NotationIoEnabled = NotationIoEnabled,

            // 10c. Phase 38 — OSC module gate (Plan 38-06 OSC-01).
            OscEnabled = OscEnabled,

            // 11. FlowConfig.Active singleton.
            FlowConfigActive = FlowConfig.Active,

            // 12. Phase 36 Plan 36-01 — PrngRegistry cache snapshot.
            //     Captures the (SourceLocation, name) → Random map by reference;
            //     RestoreFromSnapshot below repopulates the live registry from
            //     this dict so post-snapshot draws on the same Random instances
            //     replay the SAME values they would have drawn before mutation.
            PrngRegistryState = PrngRegistry.SnapshotForTesting(),

            // 13. Phase 36 Plan 36-11 — StyleRegistry snapshot. Value keys are
            //     interned Symbol values (pointer-equality), DictData values
            //     are immutable copies, so a shallow copy is faithful. The
            //     override-advisory dedup set is also captured so post-test
            //     reload (a test that resets and re-loads packs) sees a clean
            //     advisory slate.
            StyleRegistryState = new Dictionary<Value, DictData>(StyleRegistry),
            StyleOverrideAdvisoriesEmitted = new HashSet<string>(StyleOverrideAdvisoriesEmitted),
        };
    }

    /// <summary>
    /// Phase 35 Plan 35-04 TEST-02 — reinstates the captured state. Static
    /// mutable surfaces with explicit reset hooks (SynthUtils.Rng,
    /// RenderingDiagnostics._emitted) are reset via their existing hooks;
    /// surfaces without one are clear-and-repopulate from the snapshot.
    /// </summary>
    public void RestoreState(FlowLang.StandardLibrary.TestFramework.TestSnapshot snap)
    {
        // 1. Global frame variables.
        GlobalFrame.RestoreLocalVariables(snap.GlobalVariables);

        // 2. TestRegistry — pop any tests appended after the snapshot.
        //    (Test bodies may call (test "nested" ...) which would mutate
        //    the registry; we drop those entries to keep the next test's
        //    snapshot count identical to the pre-test count.)
        while (TestRegistry.Count > snap.TestRegistryCount)
            TestRegistry.RemoveAt(TestRegistry.Count - 1);

        // 3. SectionRegistry — Phase 36 Plan 36-10 list-of-overloads shape.
        SectionRegistry.Clear();
        foreach (var (k, v) in snap.SectionRegistry)
            SectionRegistry[k] = new List<SectionData>(v);

        // 4. SymbolInternTable.
        SymbolInternTable.Clear();
        foreach (var (k, v) in snap.SymbolInternTable)
            SymbolInternTable[k] = v;

        // 5. PRNG state.
        lock (RandLock)
        {
            FixedRandSeed = snap.FixedRandSeed;
            // Rebuild FixedGen from the captured seed so the next draw is
            // identical to the pre-snapshot draw. Capturing the Random
            // reference alone is insufficient — it may have been advanced
            // by the test body, and Random has no public restart API.
            FixedGen = new Random(snap.FixedRandSeed);
            // Gen has no seed surface — null it so the next GetRand re-seeds
            // from Random.Shared (matches the constructor's lazy-init path).
            Gen = snap.Gen;
        }

        // 6. Musical-context stack on the global frame.
        GlobalFrame.MusicalContext = snap.GlobalFrameMusicalContext;

        // 7-10. Phase 33 SFZ statics.
        SfzEnabled = snap.SfzEnabled;
        SfzInstruments.Clear();
        foreach (var (k, v) in snap.SfzInstruments)
            SfzInstruments[k] = v;
        SfzPatchRegistry.Clear();
        foreach (var (k, v) in snap.SfzPatchRegistry)
            SfzPatchRegistry[k] = v;
        SfzDiagnostics.Clear();
        foreach (var k in snap.SfzDiagnostics)
            SfzDiagnostics.Add(k);
        ResolvedSfzRoot = snap.ResolvedSfzRoot;

        // 10b. Phase 39 — notation-io module gate restore.
        NotationIoEnabled = snap.NotationIoEnabled;

        // 10c. Phase 38 — OSC module gate restore (Plan 38-06 OSC-01).
        OscEnabled = snap.OscEnabled;

        // 11. FlowConfig.Active singleton.
        FlowConfig.Active = snap.FlowConfigActive;

        // 12. Phase 36 Plan 36-01 — PrngRegistry restore. Null-guard preserves
        //     backward compatibility with pre-Phase-36 TestSnapshots that don't
        //     populate PrngRegistryState (Threat T-36-03 mitigation).
        if (snap.PrngRegistryState != null)
            PrngRegistry.RestoreFromSnapshot(snap.PrngRegistryState);

        // 13. Phase 36 Plan 36-11 — StyleRegistry restore. Same null-guard
        //     posture as #12. Clear + repopulate so the registry exactly
        //     matches the snapshot (in-test (registerStyle ...) calls do not
        //     leak across tests).
        if (snap.StyleRegistryState != null)
        {
            StyleRegistry.Clear();
            foreach (var kv in snap.StyleRegistryState)
                StyleRegistry[kv.Key] = kv.Value;
        }
        if (snap.StyleOverrideAdvisoriesEmitted != null)
        {
            StyleOverrideAdvisoriesEmitted.Clear();
            foreach (var key in snap.StyleOverrideAdvisoriesEmitted)
                StyleOverrideAdvisoriesEmitted.Add(key);
        }

        // Static reset hooks for mutable singletons without snapshot fields.
        // Per RESEARCH §Pitfall 3 — these existing hooks were added by prior
        // phases (Phase 23 / Phase 32 / Phase 33) for the same hermetic-test
        // purpose. We piggyback on them rather than maintaining duplicates.
        FlowLang.StandardLibrary.Audio.Synthesizers.SynthUtils.ResetNoiseRng();
        FlowLang.Diagnostics.RenderingDiagnostics.ResetForTesting();
    }
}
