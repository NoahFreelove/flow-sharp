using FlowLang.Diagnostics;
using FlowLang.Runtime;

namespace FlowLang.TypeSystem;

/// <summary>
/// Resolves function overloads based on argument types and specificity.
/// </summary>
public class OverloadResolver
{
    private readonly ErrorReporter _errorReporter;
    private readonly TextWriter? _diagnosticOutput;

    /// <summary>
    /// Bundle A (260524-r4o) — shared, lazily-allocated <see cref="ErrorReporter"/>
    /// used by the new <c>Resolve(IReadOnlyList&lt;FunctionOverload&gt;, ..., silent: true)</c>
    /// overload when the caller wants silent probing without per-call allocation.
    /// The reporter's accumulated errors are never read or flushed — callers in
    /// silent mode only care about the resolved signature, not the rejection
    /// reasons. A single shared instance is safe because the silent-path
    /// reporter is fire-and-forget; concurrent probes accumulate errors into
    /// the same buffer, but no consumer reads them.
    /// </summary>
    private ErrorReporter? _silentReporter;
    private ErrorReporter SilentReporter => _silentReporter ??= new ErrorReporter();

    public OverloadResolver(ErrorReporter errorReporter, TextWriter? diagnosticOutput = null)
    {
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _diagnosticOutput = diagnosticOutput;
    }

    /// <summary>
    /// Resolves the best matching overload from a list of candidates.
    ///
    /// <para>
    /// Phase 36 Plan 36-02 (D-36-11): the <paramref name="namedArgTypes"/>
    /// parameter carries Type information for named-argument bindings parsed
    /// from <c>(fn name=value)</c> call surface. When non-null, the resolver
    /// validates each name against <see cref="FunctionSignature.ParameterNames"/>
    /// and constructs a re-ordered positional arg-type list before specificity
    /// scoring runs. Existing call sites pass the parameter as null
    /// (defaulted) — the legacy positional-only path is byte-identical.
    /// </para>
    ///
    /// <para>
    /// Validation order:
    /// <list type="number">
    ///   <item>Varargs + named: rejected (RESEARCH Open Question 2).</item>
    ///   <item>ParameterNames=null + named: rejected with "does not yet
    ///         support named arguments" (RESEARCH Pitfall 5 — the safety
    ///         net for Plans 36-03/04 parallel backfill).</item>
    ///   <item>Unknown name: rejected with the expected-name hint.</item>
    ///   <item>Positional + named target the same slot: rejected.</item>
    ///   <item>Otherwise: re-ordered FlowType[] flows through the existing
    ///         specificity-scoring path verbatim.</item>
    /// </list>
    /// The caller (ExpressionEvaluator) is responsible for re-ordering the
    /// runtime <c>Value[]</c> using the same ParameterNames lookup once a
    /// signature is selected.
    /// </para>
    /// </summary>
    public FunctionSignature? Resolve(
        string functionName,
        IReadOnlyList<FunctionSignature> candidates,
        IReadOnlyList<FlowType> positionalArgTypes,
        Core.SourceLocation? location = null,
        IReadOnlyDictionary<string, FlowType>? namedArgTypes = null,
        bool strictMode = false)
    {
        return ResolveCore(functionName, candidates, positionalArgTypes, location, namedArgTypes, _errorReporter, strictMode);
    }

    /// <summary>
    /// Bundle A (260524-r4o) Task 2 — FunctionOverload-direct overload that
    /// avoids the caller's <c>overloads.Select(o => o.Signature).ToList()</c>
    /// projection AND the subsequent <c>overloads.FirstOrDefault(o => o.Signature == sig)</c>
    /// reverse-lookup. The caller passes the live <see cref="FunctionOverload"/>
    /// list (read-only by contract — see <see cref="StackFrame.GetFunctionOverloads"/>);
    /// this method extracts signatures into a fixed-size <see cref="FunctionSignature"/>
    /// array (one allocation, no growable List, no boxed enumerator) and rescans
    /// the candidates by reference-equality after <see cref="ResolveCore"/> picks
    /// the winning signature.
    ///
    /// <para>
    /// When <paramref name="silent"/> is true, errors are routed to a shared
    /// <see cref="SilentReporter"/> instance whose accumulated errors are never
    /// flushed — used by <c>TryResolveFunction</c> for fire-and-forget probing
    /// without per-call <see cref="ErrorReporter"/> allocation.
    /// </para>
    /// </summary>
    public FunctionOverload? Resolve(
        string functionName,
        IReadOnlyList<FunctionOverload> candidates,
        IReadOnlyList<FlowType> positionalArgTypes,
        Core.SourceLocation? location = null,
        IReadOnlyDictionary<string, FlowType>? namedArgTypes = null,
        bool silent = false,
        bool strictMode = false)
    {
        if (candidates.Count == 0)
        {
            // Match the legacy "No overloads found" diagnostic when not silent.
            if (!silent)
            {
                _errorReporter.ReportError(
                    $"No overloads found for function '{functionName}'",
                    location);
            }
            return null;
        }

        // Single allocation: fixed-size array, no growable List, no LINQ enumerator.
        var signatures = new FunctionSignature[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
            signatures[i] = candidates[i].Signature;

        var reporter = silent ? SilentReporter : _errorReporter;
        var sig = ResolveCore(functionName, signatures, positionalArgTypes, location, namedArgTypes, reporter, strictMode);
        if (sig == null)
            return null;

        // Reference-equality scan: each FunctionOverload owns its FunctionSignature
        // instance (no equality-fallback needed — ResolveCore returns the same
        // FunctionSignature reference that lived in `signatures[i]`).
        for (int i = 0; i < candidates.Count; i++)
        {
            if (ReferenceEquals(candidates[i].Signature, sig))
                return candidates[i];
        }

        // Defensive fallback — should be unreachable if ResolveCore behaves.
        return null;
    }

    /// <summary>
    /// Bundle A (260524-r4o) — shared scoring/named-arg body extracted from the
    /// legacy <see cref="Resolve(string, IReadOnlyList{FunctionSignature}, IReadOnlyList{FlowType}, Core.SourceLocation?, IReadOnlyDictionary{string, FlowType}?)"/>
    /// method so both the legacy FunctionSignature-returning entry point and
    /// the new FunctionOverload-direct overload share it. The
    /// <paramref name="reporter"/> parameter decouples error emission from the
    /// <see cref="_errorReporter"/> field — silent probes pass a shared
    /// fire-and-forget reporter, the legacy path passes <see cref="_errorReporter"/>.
    /// </summary>
    private FunctionSignature? ResolveCore(
        string functionName,
        IReadOnlyList<FunctionSignature> candidates,
        IReadOnlyList<FlowType> positionalArgTypes,
        Core.SourceLocation? location,
        IReadOnlyDictionary<string, FlowType>? namedArgTypes,
        ErrorReporter reporter,
        bool strictMode = false)
    {
        if (candidates.Count == 0)
        {
            reporter.ReportError(
                $"No overloads found for function '{functionName}'",
                location);
            return null;
        }

        // ===========================================================
        // Named-arg dispatch (Phase 36 Plan 36-02 D-36-11)
        // ===========================================================
        //
        // When named args are present, candidates that fail the named-arg
        // validation gates are dropped from the pool BEFORE specificity
        // scoring runs. Multiple-candidate diagnostics aggregate the rejection
        // reasons so the composer sees the most useful one ("unknown
        // parameter X"), not the generic "no matching overload".
        IReadOnlyList<FlowType> argTypes;
        if (namedArgTypes is { Count: > 0 })
        {
            // First, build the list of named-arg-eligible candidates so we
            // can surface the most informative diagnostic when none survive.
            // Per-candidate diagnostics drop into a local error buffer that
            // we only flush to the real reporter if NO candidate survives —
            // otherwise a successful resolve would leak "unknown parameter"
            // chatter from sibling overloads.
            //
            // Bundle A (260524-r4o) Task 3 — lazy-allocate localReporter only
            // on first rejection. The success path (first candidate wins
            // immediately) never allocates a local ErrorReporter.
            ErrorReporter? localReporter = null;
            // sweep-0614: accumulate EVERY candidate that passes the
            // name/duplicate/arity/reorder validation, each paired with its OWN
            // reordered FlowType vector. The previous code broke at the first
            // survivor and collapsed `candidates` to that single signature, so
            // an overload set sharing parameter names + arity but differing by
            // TYPE (e.g. transpose(Sequence, Semitone) vs (Sequence, Cent);
            // db(Int)/db(Double)/...) locked onto whichever was registered
            // first and never tried the others — `(transpose s amount=+50c)`
            // and `(db x=12.0)` then failed "No matching overload" even though
            // a later overload matched exactly. We now run per-slot type
            // matching + specificity ranking against EACH candidate's own
            // reordered vector, mirroring the positional path below.
            var namedArgSurvivors = new List<(FunctionSignature Sig, FlowType[] Reordered)>();

            foreach (var sig in candidates)
            {
                if (sig.IsVarArgs)
                {
                    var firstName = namedArgTypes.Keys.First();
                    (localReporter ??= new ErrorReporter()).ReportError(
                        $"named arg '{firstName}' cannot be used with variadic function '{functionName}'",
                        location);
                    continue;
                }
                if (sig.ParameterNames is null)
                {
                    (localReporter ??= new ErrorReporter()).ReportError(
                        $"function '{functionName}' does not yet support named arguments " +
                        "(parameter names not yet declared on this signature)",
                        location);
                    continue;
                }

                // Validate every named-arg key is recognized.
                bool unknownName = false;
                foreach (var name in namedArgTypes.Keys)
                {
                    if (!sig.ParameterNames.Contains(name))
                    {
                        (localReporter ??= new ErrorReporter()).ReportError(
                            $"unknown parameter '{name}' for function '{functionName}' " +
                            $"(expected: {string.Join(", ", sig.ParameterNames)})",
                            location);
                        unknownName = true;
                        break;
                    }
                }
                if (unknownName) continue;

                // Validate positional slots don't collide with named slots.
                // First positionalArgTypes.Count slots are filled by positionals;
                // a named arg targeting any of those slots is a duplicate-bind.
                //
                // Phase 44 review WR-08: replaced the per-named-arg
                // `sig.ParameterNames.ToList().IndexOf(name)` with an inline
                // linear scan. ParameterNames is already IReadOnlyList<string>
                // — .ToList() allocated a fresh List<string> per named-arg,
                // and IndexOf scanned it linearly. The allocation churn
                // showed up in named-arg dispatch on the audio rendering
                // hot path (no overload-cache coverage for named-arg
                // resolution per ExecutionContext.cs:71-82) and contradicted
                // the "no GC pressure in hot paths" constraint at CLAUDE.md
                // line 285. Inline scan is zero-allocation, same O(K) per
                // named-arg.
                bool duplicate = false;
                foreach (var name in namedArgTypes.Keys)
                {
                    int slot = -1;
                    for (int i = 0; i < sig.ParameterNames.Count; i++)
                    {
                        if (sig.ParameterNames[i] == name) { slot = i; break; }
                    }
                    if (slot < positionalArgTypes.Count)
                    {
                        (localReporter ??= new ErrorReporter()).ReportError(
                            $"parameter '{name}' bound by both positional and named argument " +
                            $"in call to '{functionName}'",
                            location);
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;

                // Validate arity. Without per-parameter defaults the supplied
                // positional + named args must cover the whole signature
                // exactly (legacy contract). jam-named-args (0615): when the
                // signature carries defaults, slots left uncovered by
                // positional-or-named may be default-filled — so the only
                // hard arity errors are "too many args" or "a required
                // (default-less) slot is uncovered". This is what lets the
                // collapsed `jam(over, style, length, key, seed, order)`
                // resolve sparse middle-skip calls like
                // `(jam over=chords style=#jazz seed=42)` (length + key
                // default-filled).
                if (positionalArgTypes.Count + namedArgTypes.Count > sig.InputTypes.Count)
                {
                    (localReporter ??= new ErrorReporter()).ReportError(
                        $"function '{functionName}' expects {sig.InputTypes.Count} arguments, " +
                        $"got {positionalArgTypes.Count} positional + {namedArgTypes.Count} named",
                        location);
                    continue;
                }
                bool hasDefaults = sig.HasParameterDefaults;
                if (positionalArgTypes.Count + namedArgTypes.Count != sig.InputTypes.Count
                    && !hasDefaults)
                {
                    (localReporter ??= new ErrorReporter()).ReportError(
                        $"function '{functionName}' expects {sig.InputTypes.Count} arguments, " +
                        $"got {positionalArgTypes.Count} positional + {namedArgTypes.Count} named",
                        location);
                    continue;
                }

                // Build the re-ordered FlowType[] matching the signature's
                // positional order. Slots 0..positionalArgTypes.Count-1 keep
                // their positional types; the named-arg slots are filled from
                // namedArgTypes by parameter-name lookup. Any slot that ends up
                // covered by neither positional nor named is default-filled
                // (jam-named-args 0615) — its type comes from the registered
                // default Value; a slot with no default and no supplied arg is
                // a required-parameter-uncovered error.
                var reordered = new FlowType[sig.InputTypes.Count];
                var slotCovered = new bool[sig.InputTypes.Count];
                for (int i = 0; i < positionalArgTypes.Count; i++)
                {
                    reordered[i] = positionalArgTypes[i];
                    slotCovered[i] = true;
                }
                bool reorderOk = true;
                foreach (var (name, type) in namedArgTypes)
                {
                    // WR-08: same inline scan as the duplicate-check pass
                    // above; zero-allocation replacement for the previous
                    // `sig.ParameterNames.ToList().IndexOf(name)`.
                    int slot = -1;
                    for (int i = 0; i < sig.ParameterNames.Count; i++)
                    {
                        if (sig.ParameterNames[i] == name) { slot = i; break; }
                    }
                    if (slot < 0 || slot >= sig.InputTypes.Count)
                    {
                        // Defensive — should have been caught by unknown-name check above.
                        reorderOk = false;
                        break;
                    }
                    reordered[slot] = type;
                    slotCovered[slot] = true;
                }
                if (!reorderOk) continue;

                // Default-fill any uncovered slots from the registered defaults.
                bool requiredSlotMissing = false;
                for (int i = 0; i < sig.InputTypes.Count; i++)
                {
                    if (slotCovered[i]) continue;
                    var dflt = sig.DefaultForSlot(i);
                    if (dflt is null)
                    {
                        // A slot with no positional/named arg AND no default —
                        // the call genuinely under-supplies a required param.
                        (localReporter ??= new ErrorReporter()).ReportError(
                            $"function '{functionName}' is missing a value for required parameter " +
                            $"'{sig.ParameterNames[i]}'",
                            location);
                        requiredSlotMissing = true;
                        break;
                    }
                    reordered[i] = dflt.Type;
                }
                if (requiredSlotMissing) continue;

                // sweep-0614: this candidate passed name/duplicate/arity/reorder
                // validation — keep it (with its OWN reordered vector) and try
                // the rest. Type matching + specificity ranking happen below
                // against each survivor's own vector, since different overloads
                // may map the same parameter names to different slots.
                namedArgSurvivors.Add((sig, reordered));
            }

            if (namedArgSurvivors.Count == 0)
            {
                // Flush rejection diagnostics — these are the actionable
                // messages for the composer.
                if (localReporter != null)
                {
                    foreach (var err in localReporter.Errors)
                    {
                        reporter.ReportError(err.Message, err.Location);
                    }
                }
                return null;
            }

            // Per-slot TYPE matching against each survivor's own reordered
            // vector (mirrors the positional `sig.Matches` filter below).
            var namedArgMatches = namedArgSurvivors
                .Where(s => s.Sig.Matches(s.Reordered, strictMode))
                .ToList();

            if (namedArgMatches.Count == 0)
            {
                // None of the name-eligible candidates type-checks. Report
                // against the first survivor's reordered vector so the
                // composer sees concrete argument types.
                reporter.ReportError(
                    $"No matching overload for function '{functionName}' with argument types " +
                    $"({string.Join(", ", namedArgSurvivors[0].Reordered)})",
                    location);
                return null;
            }

            if (namedArgMatches.Count == 1)
            {
                return namedArgMatches[0].Sig;
            }

            // Multiple type-matching survivors — rank by specificity scored
            // against EACH candidate's own reordered vector.
            var rankedNamed = namedArgMatches
                .Select(s => new
                {
                    s.Sig,
                    Specificity = s.Sig.CalculateSpecificity(s.Reordered),
                    s.Reordered
                })
                .OrderByDescending(x => x.Specificity)
                .ToList();

            if (rankedNamed.Count > 1
                && rankedNamed[0].Specificity == rankedNamed[1].Specificity)
            {
                reporter.ReportError(
                    $"Ambiguous overload for function '{functionName}' with argument types " +
                    $"({string.Join(", ", rankedNamed[0].Reordered)}). " +
                    $"Candidates: {rankedNamed[0].Sig}, {rankedNamed[1].Sig}",
                    location);
                return null;
            }

            return rankedNamed[0].Sig;
        }

        argTypes = positionalArgTypes;

        // Filter candidates that match the argument types.
        // Phase 44 Plan 44-03: strictMode drops the two implicit-conversion
        // clauses (numeric widening + inverse music-type widening) per
        // RESEARCH Pitfall 1 — see FunctionSignature.Matches XML doc.
        var matchingCandidates = candidates
            .Where(sig => sig.Matches(argTypes, strictMode))
            .ToList();

        if (matchingCandidates.Count == 0)
        {
            if (_diagnosticOutput != null)
            {
                _diagnosticOutput.WriteLine($"[verbose] Resolving '{functionName}' with args ({string.Join(", ", argTypes)})");
                _diagnosticOutput.WriteLine($"[verbose]   {candidates.Count} candidate(s) checked, none matched");
                foreach (var sig in candidates)
                    _diagnosticOutput.WriteLine($"[verbose]   candidate: {sig}");
            }
            reporter.ReportError(
                $"No matching overload for function '{functionName}' with argument types ({string.Join(", ", argTypes)})",
                location);
            return null;
        }

        if (matchingCandidates.Count == 1)
        {
            return matchingCandidates[0];
        }

        // Multiple matches - rank by specificity
        var rankedCandidates = matchingCandidates
            .Select(sig => new
            {
                Signature = sig,
                Specificity = sig.CalculateSpecificity(argTypes)
            })
            .OrderByDescending(x => x.Specificity)
            .ToList();

        // Check for ambiguous overloads
        if (rankedCandidates.Count > 1
            && rankedCandidates[0].Specificity == rankedCandidates[1].Specificity)
        {
            reporter.ReportError(
                $"Ambiguous overload for function '{functionName}' with argument types ({string.Join(", ", argTypes)}). " +
                $"Candidates: {rankedCandidates[0].Signature}, {rankedCandidates[1].Signature}",
                location);
            return null;
        }

        return rankedCandidates[0].Signature;
    }
}
