using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.TypeSystem;

/// <summary>
/// Represents a function signature with input types.
///
/// <para>
/// Phase 36 Plan 36-02 (D-36-11): <see cref="ParameterNames"/> is the
/// defaulted-positional extension carrying parameter labels for the
/// universal named-argument call surface `(fn name=value)`. The field
/// is nullable — signatures registered WITHOUT names remain functional
/// with positional-only calls (backward-compatible safety net for the
/// parallel backfill in Plans 36-03 + 36-04 across ~350 builtin sites).
/// When a named-arg call targets a signature with null
/// <see cref="ParameterNames"/>, <see cref="OverloadResolver"/> raises a
/// graceful advisory rather than misbehaving (RESEARCH Pitfall 5).
/// </para>
///
/// <para>
/// Equality semantics: <see cref="ParameterNames"/> is intentionally
/// excluded from <see cref="Equals(FunctionSignature?)"/> and
/// <see cref="GetHashCode"/>. The resolver does name-based lookup against
/// the field, not signature deduplication; two signatures that differ
/// ONLY in parameter names (e.g., during the backfill window when a
/// pre-Phase-36 anonymous overload is re-registered with names) remain
/// equal under the content-equality contract Phase 26 introduced. This
/// keeps the SignatureSet de-dup behavior in
/// <see cref="StandardLibrary.InternalFunctionRegistry"/> stable.
/// </para>
/// </summary>
public record FunctionSignature(
    string Name,
    IReadOnlyList<FlowType> InputTypes,
    bool IsVarArgs = false,
    IReadOnlyList<string>? ParameterNames = null)
{
    public override string ToString()
    {
        var inputs = IsVarArgs
            ? $"{string.Join(", ", InputTypes)}..."
            : string.Join(", ", InputTypes);

        return $"{Name}({inputs})";
    }

    /// <summary>
    /// Custom equality to compare InputTypes by content, not reference.
    /// ParameterNames is intentionally excluded — see class doc.
    /// </summary>
    public virtual bool Equals(FunctionSignature? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name == other.Name
            && IsVarArgs == other.IsVarArgs
            && InputTypes.Count == other.InputTypes.Count
            && InputTypes.SequenceEqual(other.InputTypes);
    }

    /// <summary>
    /// Custom hash code based on content.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(IsVarArgs);
        foreach (var type in InputTypes)
        {
            hash.Add(type);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Checks if the given argument types match this signature.
    ///
    /// <para>
    /// Phase 44 Plan 44-03 (D-01 + RESEARCH Pitfall 1): when
    /// <paramref name="strictMode"/> is <c>true</c>, the two implicit-conversion
    /// clauses are DROPPED from the per-slot acceptance test:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>argTypes[i].CanConvertTo(InputTypes[i])</c> — numeric
    ///         widening (Int → Long → Float → Double). The naive "+100 tier"
    ///         clause.</item>
    ///   <item><c>InputTypes[i].IsCompatibleWith(argTypes[i])</c> — inverse
    ///         direction music-type widening (e.g.,
    ///         <c>Semitone.IsCompatibleWith(Int) = true</c> makes
    ///         <c>(transpose seq 2)</c> match
    ///         <c>transpose(Sequence, Semitone)</c> in non-strict). This is
    ///         the most-dangerous Pitfall 1 landmine — a naive read of
    ///         "+100 tier" would miss this clause and silently leave strict
    ///         test fixtures passing on the inverse-direction path.</item>
    /// </list>
    /// <para>
    /// Strict mode preserves the exact (+1000) tier
    /// (<c>argTypes[i].Equals(InputTypes[i])</c>) and the compatible (+500)
    /// tier (<c>argTypes[i].IsCompatibleWith(InputTypes[i])</c>). Scoring is
    /// UNCHANGED (see <see cref="CalculateSpecificity"/>) — strict only
    /// filters acceptance, not scoring. This preserves the ambiguous-overload
    /// diagnostic in <see cref="OverloadResolver"/> (Pattern 4 rationale).
    /// </para>
    /// <para>
    /// Default <c>strictMode = false</c> preserves byte-identical behavior at
    /// every existing call site; <see cref="OverloadResolver.Resolve"/>
    /// threads the bit through from <see cref="Runtime.ExecutionContext.ResolveFunction"/>
    /// which reads <see cref="Runtime.ExecutionContext.StrictMode"/> (the
    /// currently-executing frame's strict bit, set by Plan 44-02's per-proc
    /// push/pop in <c>Interpreter.ExecuteUserFunctionWithCaptures</c>).
    /// </para>
    /// </summary>
    public bool Matches(IReadOnlyList<FlowType> argTypes, bool strictMode = false)
    {
        if (IsVarArgs)
        {
            // For varargs, we need at least the number of fixed parameters
            if (argTypes.Count < InputTypes.Count - 1)
                return false;

            // Check fixed parameters
            for (int i = 0; i < InputTypes.Count - 1; i++)
            {
                if (!SlotMatches(argTypes[i], InputTypes[i], strictMode))
                    return false;
            }

            // Check varargs parameters (if any).
            // Audit 2026-06-09 §2.9: the vararg slot's element type is either the
            // ArrayType's element (builtins registered with T[]) or the bare last
            // InputType itself (user procs register `T...: xs` with T; VoidType is
            // the explicit any-type wildcard — see SlotMatches). The non-ArrayType
            // arm previously skipped validation entirely, so every user-defined
            // varargs proc accepted arbitrarily-typed trailing arguments that
            // exploded later as internal cast errors instead of a composer-facing
            // "no matching overload". Mirrors CalculateSpecificity's fallback.
            if (InputTypes.Count > 0)
            {
                var varArgType = InputTypes[^1];
                var elementType = (varArgType as ArrayType)?.ElementType ?? varArgType;
                for (int i = InputTypes.Count - 1; i < argTypes.Count; i++)
                {
                    if (!SlotMatches(argTypes[i], elementType, strictMode))
                        return false;
                }
            }

            return true;
        }
        else
        {
            // Fixed parameter count
            if (argTypes.Count != InputTypes.Count)
                return false;

            for (int i = 0; i < InputTypes.Count; i++)
            {
                if (!SlotMatches(argTypes[i], InputTypes[i], strictMode))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Phase 44 Plan 44-03 — shared per-slot acceptance helper.
    /// <para>
    /// Non-strict accepts when ANY of three clauses holds:
    /// <c>IsCompatibleWith</c> (compatible +500 tier),
    /// <c>CanConvertTo</c> (numeric widening clause a),
    /// inverse <c>IsCompatibleWith</c> (music-type widening clause b).
    /// </para>
    /// <para>
    /// Strict accepts ONLY when arg equals param OR
    /// <c>arg.IsCompatibleWith(param)</c>. Both implicit-conversion clauses
    /// (CanConvertTo + inverse IsCompatibleWith) are dropped. See
    /// <see cref="Matches(IReadOnlyList{FlowType}, bool)"/> XML doc for the
    /// full Pitfall 1 rationale.
    /// </para>
    /// <para>
    /// EXCEPTION (Plan 44-08 integration) — <see cref="VoidType"/> as the PARAM
    /// type is an explicit wildcard surface ("accept any arg type, dispatch to
    /// the handler that decides"), not an implicit conversion. Plan 44-08's
    /// strict-aware <c>print</c> / <c>if</c> / <c>not</c> / <c>and</c> / <c>or</c>
    /// overloads register with <c>Void</c> param types so the handler can read
    /// <see cref="Runtime.ExecutionContext.CallerStrictMode"/> and emit the
    /// canonical <c>[strict]</c> error. Without this escape, strict overload
    /// resolution would drop the Void wildcard (the inverse-direction clause
    /// <c>VoidType.IsCompatibleWith(IntType)</c> is what makes it match) and
    /// the handler would never get a chance to fire — producing "No matching
    /// overload" instead of the intended strict diagnostic.
    /// </para>
    /// </summary>
    private static bool SlotMatches(FlowType argType, FlowType paramType, bool strictMode)
    {
        // Plan 44-08 integration: Void on the PARAM side is an explicit wildcard,
        // accepted in BOTH modes. See XML doc above.
        if (paramType is VoidType) return true;

        bool exactOrCompat = argType.Equals(paramType)
                          || argType.IsCompatibleWith(paramType);
        if (strictMode)
        {
            return exactOrCompat;
        }
        return exactOrCompat
            || argType.CanConvertTo(paramType)
            || paramType.IsCompatibleWith(argType);
    }

    /// <summary>
    /// Calculates a specificity score for overload resolution.
    /// </summary>
    public int CalculateSpecificity(IReadOnlyList<FlowType> argTypes)
    {
        int score = 0;

        int compareCount = Math.Min(argTypes.Count, InputTypes.Count);

        for (int i = 0; i < compareCount; i++)
        {
            var argType = argTypes[i];
            var paramType = IsVarArgs && i >= InputTypes.Count - 1
                ? (InputTypes[^1] as ArrayType)?.ElementType ?? InputTypes[^1]
                : InputTypes[i];

            if (argType.Equals(paramType))
            {
                // Exact match - highest score
                score += 1000;
            }
            else if (argType.IsCompatibleWith(paramType))
            {
                // Compatible - medium score
                score += 500;
            }
            else if (argType.CanConvertTo(paramType))
            {
                // Convertible - low score
                score += 100;
            }
        }

        // Penalize varargs slightly
        if (IsVarArgs)
            score -= 10;

        return score;
    }
}
