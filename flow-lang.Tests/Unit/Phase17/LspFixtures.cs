using FlowLsp;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Shared test helper — every Phase 17 Fact that needs a ParseResult consumes this.
/// Allocates a fresh ParseSession each call (consistent with the per-request model).
/// </summary>
public static class LspFixtures
{
    public static ParseResult Parse(string source, string? path = null) =>
        new ParseSession().Parse(source, path);
}
