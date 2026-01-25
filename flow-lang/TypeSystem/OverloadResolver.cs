using FlowLang.Diagnostics;

namespace FlowLang.TypeSystem;

/// <summary>
/// Resolves function overloads based on argument types and specificity.
/// </summary>
public class OverloadResolver
{
    private readonly ErrorReporter _errorReporter;

    public OverloadResolver(ErrorReporter errorReporter)
    {
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
    }

    /// <summary>
    /// Resolves the best matching overload from a list of candidates.
    /// </summary>
    public FunctionSignature? Resolve(
        string functionName,
        IReadOnlyList<FunctionSignature> candidates,
        IReadOnlyList<FlowType> argTypes,
        Core.SourceLocation? location = null)
    {
        if (candidates.Count == 0)
        {
            _errorReporter.ReportError(
                $"No overloads found for function '{functionName}'",
                location);
            return null;
        }

        // Filter candidates that match the argument types
        var matchingCandidates = candidates
            .Where(sig => sig.Matches(argTypes))
            .ToList();

        if (matchingCandidates.Count == 0)
        {
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
