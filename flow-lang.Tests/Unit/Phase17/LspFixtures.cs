using FlowLang.StandardLibrary;
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

    /// <summary>
    /// Construct a fresh <see cref="BuiltInIndex"/> from an audio-free registry
    /// (signatures only — Option C / D-07 coverage, no audio backend). Phase 31
    /// Plan 31-08 scope expansion: <c>UndefinedSymbolAnalyzer</c> + the
    /// <see cref="CombinedDiagnosticsPublisher.BuildAll"/> tests consume this.
    /// </summary>
    public static BuiltInIndex BuiltInIndex()
    {
        var registry = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(registry);
        return new BuiltInIndex(registry);
    }
}
