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
    /// </summary>
    public bool Matches(IReadOnlyList<FlowType> argTypes)
    {
        if (IsVarArgs)
        {
            // For varargs, we need at least the number of fixed parameters
            if (argTypes.Count < InputTypes.Count - 1)
                return false;

            // Check fixed parameters
            for (int i = 0; i < InputTypes.Count - 1; i++)
            {
                if (!argTypes[i].IsCompatibleWith(InputTypes[i])
                    && !argTypes[i].CanConvertTo(InputTypes[i])
                    && !InputTypes[i].IsCompatibleWith(argTypes[i]))
                {
                    return false;
                }
            }

            // Check varargs parameters (if any)
            if (InputTypes.Count > 0)
            {
                var varArgType = InputTypes[^1];
                if (varArgType is ArrayType arrayType)
                {
                    for (int i = InputTypes.Count - 1; i < argTypes.Count; i++)
                    {
                        if (!argTypes[i].IsCompatibleWith(arrayType.ElementType)
                            && !argTypes[i].CanConvertTo(arrayType.ElementType)
                            && !arrayType.ElementType.IsCompatibleWith(argTypes[i]))
                        {
                            return false;
                        }
                    }
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
                if (!argTypes[i].IsCompatibleWith(InputTypes[i])
                    && !argTypes[i].CanConvertTo(InputTypes[i])
                    && !InputTypes[i].IsCompatibleWith(argTypes[i]))
                {
                    return false;
                }
            }

            return true;
        }
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
