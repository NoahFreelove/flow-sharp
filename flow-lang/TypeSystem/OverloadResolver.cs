using FlowLang.Diagnostics;

namespace FlowLang.TypeSystem;

/// <summary>
/// Resolves function overloads based on argument types and specificity.
/// </summary>
public class OverloadResolver
{
    private readonly ErrorReporter _errorReporter;
    private readonly TextWriter? _diagnosticOutput;

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
        IReadOnlyDictionary<string, FlowType>? namedArgTypes = null)
    {
        if (candidates.Count == 0)
        {
            _errorReporter.ReportError(
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
            var localReporter = new ErrorReporter();
            FunctionSignature? namedArgCandidate = null;
            IReadOnlyList<FlowType>? reorderedArgTypes = null;

            foreach (var sig in candidates)
            {
                if (sig.IsVarArgs)
                {
                    var firstName = namedArgTypes.Keys.First();
                    localReporter.ReportError(
                        $"named arg '{firstName}' cannot be used with variadic function '{functionName}'",
                        location);
                    continue;
                }
                if (sig.ParameterNames is null)
                {
                    localReporter.ReportError(
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
                        localReporter.ReportError(
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
                bool duplicate = false;
                foreach (var name in namedArgTypes.Keys)
                {
                    int slot = sig.ParameterNames.ToList().IndexOf(name);
                    if (slot < positionalArgTypes.Count)
                    {
                        localReporter.ReportError(
                            $"parameter '{name}' bound by both positional and named argument " +
                            $"in call to '{functionName}'",
                            location);
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;

                // Validate arity: positional + named must cover the whole signature.
                if (positionalArgTypes.Count + namedArgTypes.Count != sig.InputTypes.Count)
                {
                    localReporter.ReportError(
                        $"function '{functionName}' expects {sig.InputTypes.Count} arguments, " +
                        $"got {positionalArgTypes.Count} positional + {namedArgTypes.Count} named",
                        location);
                    continue;
                }

                // Build the re-ordered FlowType[] matching the signature's
                // positional order. Slots 0..positionalArgTypes.Count-1 keep
                // their positional types; the remaining slots are filled from
                // namedArgTypes by parameter-name lookup.
                var reordered = new FlowType[sig.InputTypes.Count];
                for (int i = 0; i < positionalArgTypes.Count; i++)
                    reordered[i] = positionalArgTypes[i];
                bool reorderOk = true;
                foreach (var (name, type) in namedArgTypes)
                {
                    int slot = sig.ParameterNames.ToList().IndexOf(name);
                    if (slot < 0 || slot >= sig.InputTypes.Count)
                    {
                        // Defensive — should have been caught by unknown-name check above.
                        reorderOk = false;
                        break;
                    }
                    reordered[slot] = type;
                }
                if (!reorderOk) continue;

                // First survivor wins — caller is responsible for registering
                // distinct ParameterNames-bearing overloads (the backfill
                // plans 36-03/04 will not produce ambiguous re-registrations).
                namedArgCandidate = sig;
                reorderedArgTypes = reordered;
                break;
            }

            if (namedArgCandidate is null)
            {
                // Flush rejection diagnostics — these are the actionable
                // messages for the composer.
                foreach (var err in localReporter.Errors)
                {
                    _errorReporter.ReportError(err.Message, err.Location);
                }
                return null;
            }

            // Re-ordered arg types flow through the existing
            // specificity-scoring path verbatim (single-candidate fast path).
            argTypes = reorderedArgTypes!;
            candidates = new[] { namedArgCandidate };
        }
        else
        {
            argTypes = positionalArgTypes;
        }

        // Filter candidates that match the argument types
        var matchingCandidates = candidates
            .Where(sig => sig.Matches(argTypes))
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
            _errorReporter.ReportError(
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
            _errorReporter.ReportError(
                $"Ambiguous overload for function '{functionName}' with argument types ({string.Join(", ", argTypes)}). " +
                $"Candidates: {rankedCandidates[0].Signature}, {rankedCandidates[1].Signature}",
                location);
            return null;
        }

        return rankedCandidates[0].Signature;
    }
}
