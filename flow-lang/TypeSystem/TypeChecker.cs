using FlowLang.Diagnostics;

namespace FlowLang.TypeSystem;

/// <summary>
/// Validates type compatibility and performs type checking.
/// </summary>
public class TypeChecker
{
    private readonly ErrorReporter _errorReporter;

    public TypeChecker(ErrorReporter errorReporter)
    {
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
    }

    /// <summary>
    /// Checks if a value of sourceType can be assigned to targetType.
    /// </summary>
    public bool CheckAssignment(FlowType sourceType, FlowType targetType, Core.SourceLocation? location = null)
    {
        if (sourceType.IsCompatibleWith(targetType))
            return true;

        if (sourceType.CanConvertTo(targetType))
            return true;

        _errorReporter.ReportError(
            $"Cannot assign type '{sourceType}' to '{targetType}'",
            location);

        return false;
    }

    /// <summary>
    /// Checks if the given types match the expected types.
    /// </summary>
    public bool CheckTypes(
        IReadOnlyList<FlowType> actualTypes,
        IReadOnlyList<FlowType> expectedTypes,
        Core.SourceLocation? location = null)
    {
        if (actualTypes.Count != expectedTypes.Count)
        {
            _errorReporter.ReportError(
                $"Expected {expectedTypes.Count} types but got {actualTypes.Count}",
                location);
            return false;
        }

        bool allMatch = true;
        for (int i = 0; i < actualTypes.Count; i++)
        {
            if (!CheckAssignment(actualTypes[i], expectedTypes[i], location))
            {
                allMatch = false;
            }
        }

        return allMatch;
    }

    /// <summary>
    /// Validates that a type is valid in the current context.
    /// </summary>
    public bool ValidateType(FlowType type, Core.SourceLocation? location = null)
    {
        // All types are valid for now
        // This method can be extended for more complex validation rules
        return true;
    }
}
