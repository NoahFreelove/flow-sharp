namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-00 — xUnit Category trait constant used on every
/// Phase 44 test class so that <c>dotnet test --filter
/// Category=Phase44</c> selects the entire strict-mode suite.
///
/// <para>
/// Apply with <c>[Trait("Category", Phase44TestCategory.Phase44)]</c> on the
/// test class. Mirrors the per-phase Category traits documented in
/// <c>.planning/phases/44-strict-mode/44-VALIDATION.md</c> §"Quick run command".
/// </para>
///
/// <para>
/// Anchors the requirement set REQ-STRICT-01..15 (defined in
/// <c>.planning/REQUIREMENTS.md</c> §"Phase 44 Requirements") — every Phase 44
/// xUnit class should bear this trait so the per-task quick-run loop in
/// <c>44-VALIDATION.md</c> §"Sampling Rate" (~30s feedback latency) works.
/// </para>
/// </summary>
public static class Phase44TestCategory
{
    /// <summary>
    /// Constant value passed to <c>[Trait("Category", ...)]</c> on Phase 44
    /// test classes. Selects this entire phase via
    /// <c>dotnet test --filter Category=Phase44</c>.
    /// </summary>
    public const string Phase44 = "Phase44";
}
