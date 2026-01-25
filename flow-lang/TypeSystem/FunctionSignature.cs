namespace FlowLang.TypeSystem;

/// <summary>
/// Represents a function signature with input types.
/// </summary>
public record FunctionSignature(
    string Name,
    IReadOnlyList<FlowType> InputTypes,
    bool IsVarArgs = false)
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
                    && !argTypes[i].CanConvertTo(InputTypes[i]))
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
                            && !argTypes[i].CanConvertTo(arrayType.ElementType))
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
                    && !argTypes[i].CanConvertTo(InputTypes[i]))
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
