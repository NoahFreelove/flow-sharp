namespace FlowLang.Tests.Integration.Phase45;

/// <summary>
/// Phase 45 Plan 45-01 — xUnit Category trait constant used on every
/// Phase 45 test class so that <c>dotnet test --filter
/// Category=Phase45</c> selects the entire beat-literal + beat-true-to-sig
/// pragma suite.
///
/// <para>
/// Apply with <c>[Trait("Category", Phase45TestCategory.Phase45)]</c> on the
/// test class. Mirrors the per-phase Category traits documented in
/// <c>.planning/phases/45-beat-literal-syntax-true-to-sig-pragma/45-VALIDATION.md</c>
/// §"Quick run command".
/// </para>
///
/// <para>
/// Anchors the requirement set REQ-BEAT-LEX-01..04, REQ-BEAT-AST-01..04,
/// REQ-BEAT-PRAGMA-01..04, REQ-BEAT-PRAGMA-HYPHEN-01, REQ-BEAT-CONSTRUCTOR-01..02,
/// REQ-BEAT-TEST-01..07 (defined in <c>.planning/REQUIREMENTS.md</c> §"Phase 45
/// Requirements") — every Phase 45 xUnit class should bear this trait so the
/// per-task quick-run loop in <c>45-VALIDATION.md</c> §"Sampling Rate" (~20s
/// feedback latency) works.
/// </para>
/// </summary>
public static class Phase45TestCategory
{
    /// <summary>
    /// Constant value passed to <c>[Trait("Category", ...)]</c> on Phase 45
    /// test classes. Selects this entire phase via
    /// <c>dotnet test --filter Category=Phase45</c>.
    /// </summary>
    public const string Phase45 = "Phase45";
}
