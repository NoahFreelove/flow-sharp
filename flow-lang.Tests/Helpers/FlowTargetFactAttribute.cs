namespace FlowLang.Tests.Helpers;

/// <summary>
/// Phase 47 D-47-13: xUnit FactAttribute subclass that skips the test unless
/// the current build's FlowTarget matches one of the supplied tokens.
///
/// Usage:
///   <code>[FlowTargetFact("Desktop")]</code> — Desktop-only test
///   <code>[FlowTargetFact("Web")]</code>     — Web-only test
///   <code>[FlowTargetFact("Desktop", "Web")]</code> — cross-target test
///
/// The current target is resolved via the FLOW_WEB preprocessor symbol
/// propagated from flow-lang.csproj (Plan 47-01). The test project's
/// ProjectReference inherits MSBuild properties; running
/// <c>dotnet test -p:FlowTarget=Web</c> propagates the define to the test
/// assembly automatically (DefineConstants travels via PropertyGroup
/// inheritance).
///
/// When the current target is not in the supplied set, the attribute sets
/// the inherited <see cref="Xunit.FactAttribute.Skip"/> property; xUnit
/// then reports the test as skipped with the documented reason rather than
/// running it.
/// </summary>
public sealed class FlowTargetFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// Compile-time-determined token identifying which target this test
    /// assembly was built for. Constant-folded by the C# compiler so callers
    /// can branch on it without runtime cost.
    /// </summary>
    public const string CurrentTarget =
#if FLOW_WEB
        "Web";
#else
        "Desktop";
#endif

    public FlowTargetFactAttribute(params string[] targets)
    {
        if (targets is null || targets.Length == 0)
            throw new ArgumentException(
                "FlowTargetFact requires at least one target token (\"Desktop\" or \"Web\")",
                nameof(targets));

        if (Array.IndexOf(targets, CurrentTarget) < 0)
        {
            Skip = $"Skipped on {CurrentTarget} — test runs under: {string.Join(", ", targets)}";
        }
    }
}
