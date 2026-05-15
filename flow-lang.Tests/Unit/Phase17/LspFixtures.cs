using FlowLsp;
using FlowLsp.Symbols;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Shared test helper — every Phase 17 Fact that needs a ParseResult consumes this.
/// Allocates a fresh ParseSession each call (consistent with the per-request model).
///
/// Phase 31 plan 31-01 Task 2 adds <see cref="StdlibIndex"/> so Phase 31 fact
/// classes (UnusedImportAnalyzerFacts, CompletionFilterFacts) can construct the
/// index without re-implementing the local <c>Indices()</c> helper pattern from
/// HoverHandlerTests.cs:18-23.
/// </summary>
public static class LspFixtures
{
    public static ParseResult Parse(string source, string? path = null) =>
        new ParseSession().Parse(source, path);

    /// <summary>
    /// Construct a fresh <see cref="StdlibSymbolIndex"/> backed by a new
    /// <see cref="ParseSession"/>. Phase 31 analyzers (<c>UnusedImportAnalyzer</c>)
    /// and the <c>CompletionHandler.FilterByImports</c> filter both consume this.
    /// </summary>
    public static StdlibSymbolIndex StdlibIndex() => new(new ParseSession());
}
