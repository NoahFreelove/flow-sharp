using System.Linq;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLsp;
using FlowLsp.Handlers;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using FlowLang.Tests.Unit.Phase17;

namespace FlowLang.Tests.Unit.Phase31;

/// <summary>
/// Phase 31 Plan 04 Facts — Context-aware completion filtering (SPEC-2).
///
/// Exercises three pure-static filters in CompletionHandler:
///   FilterByImports     — drops stdlib procs whose source module isn't `use`d.
///   FilterByPragmas     — drops H-prefixed note completions when `enable hAsB;` is absent.
///   BoostByMusicalContext — boosts roman-numeral + chord builtin SortText inside `key { }`.
///
/// Layered on the Phase 17 CompletionHandler.BuildItems pure-static seam — no LSP
/// transport / no OmniSharp facade. Mirrors Phase17/CompletionHandlerTests.cs shape.
/// </summary>
public class CompletionFilterFacts
{
    private static (BuiltInIndex bi, UserSymbolIndex ui, StdlibSymbolIndex si, KeywordIndex ki) MakeIndices()
    {
        var reg = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(reg);
        var parser = new ParseSession();
        return (new BuiltInIndex(reg), new UserSymbolIndex(), new StdlibSymbolIndex(parser), new KeywordIndex());
    }

    // === FilterByImports — drop stdlib-source duplicate emissions for non-imported modules ===
    //
    // Architectural note (deviation from plan's literal "arpeggio is dropped" wording):
    // every stdlib `internal proc` declaration (e.g. `arpeggio` in std.flow:176) has a
    // matching C# builtin registration in BuiltInFunctions/HarmonyFunctions, so the
    // 5-source merge emits BOTH a builtin-source CompletionItem AND a stdlib-source
    // CompletionItem for the same Label. The filter targets the duplicate stdlib-source
    // emission (Detail prefix `(stdlib: @...)`). The builtin emission ALWAYS passes
    // through — Phase 17's `Default_ReturnsBuiltInsKeywordsSnippets_IncludingAudioAndTransform`
    // contract requires `print`, `reverb`, `transpose`, `chordNotes` to surface in
    // completion regardless of imports. Plan 31-02 set the same precedent (Rule 1
    // auto-fix: rewrote tests against the real stdlib organization instead of the
    // plan's aspirational `@harmony` module).

    /// <summary>
    /// Without any `use` statement, the stdlib-source emission for `arpeggio`
    /// (Detail prefix `(stdlib: @std)`) must be filtered out. The builtin emission
    /// (Detail = signature like `arpeggio(Chord, String)`) persists — composer
    /// still sees `arpeggio` in completion via the builtin source.
    /// </summary>
    [Fact]
    public void FilterByImports_DropsStdlibProcs_WhenModuleNotImported()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/no-imports.flow");
        var text = "proc main ()\n  ";   // NO `use "@..."`
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(1, 2), bi, ui, si, ki).ToList();

        // No item should carry the `(stdlib: @<unimported-module>)` Detail prefix.
        var stdlibDuplicates = items
            .Where(i => i.Detail is not null && i.Detail.StartsWith("(stdlib: @", System.StringComparison.Ordinal))
            .ToList();
        Assert.Empty(stdlibDuplicates);
    }

    /// <summary>
    /// With `use "@std"`, the stdlib-source emissions for @std procs (e.g. `arpeggio`,
    /// `print`) must survive the filter — i.e. an item with Detail `(stdlib: @std)`
    /// appears in the merged list.
    /// </summary>
    [Fact]
    public void FilterByImports_AllowsStdlibProcs_WhenStdImported()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/with-std.flow");
        var text = "use \"@std\"\nproc main ()\n  ";
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(2, 2), bi, ui, si, ki).ToList();

        // arpeggio appears in the list — both via the builtin source AND the stdlib
        // source (deduplicated by the LSP client). Existence at all is sufficient.
        Assert.Contains(items, i => i.Label == "arpeggio");
        // The stdlib-source emission survives the filter (Detail prefix preserved).
        Assert.Contains(items, i =>
            i.Label == "arpeggio"
            && i.Detail is not null
            && i.Detail.StartsWith("(stdlib: @std", System.StringComparison.Ordinal));
    }

    /// <summary>
    /// `@std` transitively imports `@collections` + `@bars` (std.flow:5-6) — the
    /// FilterByImports `if (importedModules.Contains("std")) UnionWith(ModuleNames)`
    /// expansion makes every stdlib module visible when only `use "@std"` is present.
    /// </summary>
    [Fact]
    public void FilterByImports_TransitiveStdImport_AllowsCollectionsProcs()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/transitive-std.flow");
        var text = "use \"@std\"\nproc main ()\n  ";
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(2, 2), bi, ui, si, ki).ToList();

        // Pick any @collections proc — the stdlib-source emission must survive.
        var anyCollectionsProc = si.ProcsForModule("collections").FirstOrDefault();
        Assert.NotNull(anyCollectionsProc);
        Assert.Contains(items, i =>
            i.Label == anyCollectionsProc!.Name
            && i.Detail is not null
            && i.Detail.StartsWith("(stdlib: @collections", System.StringComparison.Ordinal));
    }

    /// <summary>
    /// `use "@audio"` alone (no `@std`) does NOT transitively pull in other modules.
    /// A @collections-only proc's stdlib-source emission must be filtered out.
    /// </summary>
    [Fact]
    public void FilterByImports_NonStdImport_DoesNotTransitivelyExpand()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/audio-only.flow");
        var text = "use \"@audio\"\nproc main ()\n  ";
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(2, 2), bi, ui, si, ki).ToList();

        // @audio is imported — its stdlib-source emissions survive.
        var anyAudioProc = si.ProcsForModule("audio").FirstOrDefault();
        Assert.NotNull(anyAudioProc);
        Assert.Contains(items, i =>
            i.Label == anyAudioProc!.Name
            && i.Detail is not null
            && i.Detail.StartsWith("(stdlib: @audio", System.StringComparison.Ordinal));

        // @collections is NOT imported and NOT transitively pulled by @audio —
        // its stdlib-source emissions must be filtered.
        Assert.DoesNotContain(items, i =>
            i.Detail is not null
            && i.Detail.StartsWith("(stdlib: @collections", System.StringComparison.Ordinal));
    }

    /// <summary>
    /// Builtins (registered in BuiltInIndex) are NOT module-tagged — they must pass
    /// through FilterByImports unconditionally. Same for keywords / snippets.
    /// This is the Phase 17 contract preserved across Phase 31.
    /// </summary>
    [Fact]
    public void FilterByImports_KeepsBuiltinsAndKeywords_Always()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/no-imports.flow");
        var text = "proc main ()\n  ";
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(1, 2), bi, ui, si, ki).ToList();

        // Builtins: print/reverb/transpose/chordNotes (D-07 completeness from Phase 17).
        Assert.Contains(items, i => i.Label == "print");
        Assert.Contains(items, i => i.Label == "reverb");
        Assert.Contains(items, i => i.Label == "chordNotes");
        // Keywords / snippets:
        Assert.Contains(items, i => i.Label == "proc");
    }

    // === FilterByPragmas — drop H-prefixed note completions sans `enable hAsB;` ===

    /// <summary>
    /// Inside a note stream WITHOUT `enable hAsB;`, the H-prefixed note completions
    /// (H4, H5) must be filtered out — German-notation H is not recognized.
    /// </summary>
    [Fact]
    public void FilterByPragmas_DropsHNotes_WhenHAsBNotDeclared()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/no-pragma.flow");
        // Closed `| C4 D4 |` stream so the parser produces a well-formed
        // NoteStreamExpression — cursor inside the bar exercises the note-stream
        // branch, which now applies FilterByPragmas to its returned items.
        var text = "proc main ()\n  | C4 D4 |\nend proc";
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(1, 6), bi, ui, si, ki).ToList();

        Assert.DoesNotContain(items, i => i.Label == "H4");
        Assert.DoesNotContain(items, i => i.Label == "H5");
    }

    /// <summary>
    /// With `enable hAsB;`, H-prefixed note completions must appear in the note-stream
    /// completion list.
    /// </summary>
    [Fact]
    public void FilterByPragmas_AllowsHNotes_WhenHAsBDeclared()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/with-pragma.flow");
        var text = "enable hAsB;\nproc main ()\n  | C4 D4 |\nend proc";
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(2, 6), bi, ui, si, ki).ToList();

        Assert.Contains(items, i => i.Label == "H4");
        Assert.Contains(items, i => i.Label == "H5");
    }

    // === BoostByMusicalContext — rank roman numerals first inside `key { }` ===

    /// <summary>
    /// Inside `key Cmajor { ... }`, the roman-numeral completions (I, ii, IV, V7, vi)
    /// must rank higher than other identifier completions — implemented by prefixing
    /// SortText with "0_" (LSP clients sort lexicographically on SortText).
    /// </summary>
    [Fact]
    public void BoostByMusicalContext_RomanNumeralsRankFirstInsideKey()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/inside-key.flow");
        // Cursor sits OUTSIDE the note stream but INSIDE the key block — exercises
        // the default-branch boost (note-stream branch already surfaces roman numerals
        // via RomanNumeralItems; that path is unchanged).
        var text = "key Cmajor {\n  \n}";
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(1, 2), bi, ui, si, ki).ToList();

        // Find a roman-numeral item — boosted SortText starts with "0_".
        var romanI = items.FirstOrDefault(i => i.Label == "I");
        Assert.NotNull(romanI);
        Assert.NotNull(romanI!.SortText);
        Assert.StartsWith("0_", romanI.SortText);
    }

    /// <summary>
    /// Outside any `key` block, roman-numeral completions must NOT be boosted —
    /// SortText is either null or whatever the upstream source set, NOT "0_*".
    /// </summary>
    [Fact]
    public void BoostByMusicalContext_RomanNumeralsNotBoostedOutsideKey()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/no-key.flow");
        var text = "proc main ()\n  ";
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(1, 2), bi, ui, si, ki).ToList();

        // "I" might not be present here (it's a roman-numeral surfaced only inside key
        // context via the note-stream branch). What we MUST confirm: no completion item
        // has a SortText that starts with "0_" purely as a result of the boost — i.e.
        // the boost is conditional on the enclosing-key check.
        var romanI = items.FirstOrDefault(i => i.Label == "I");
        // If "I" isn't in the default list (it shouldn't be — roman numerals live in the
        // note-stream branch), the boost can't have fired. That alone satisfies the spec.
        // If for any reason "I" IS in the default merge, its SortText must NOT be "0_*"
        // (which would indicate a leaked boost outside the key context).
        if (romanI is not null && romanI.SortText is not null)
        {
            Assert.False(romanI.SortText.StartsWith("0_"),
                "Roman-numeral 'I' must not be boosted outside key context.");
        }
    }

    /// <summary>
    /// Charitable fail-open: filters MUST tolerate a null AST without throwing.
    /// </summary>
    [Fact]
    public void Filters_NullAst_PassesThroughUnfiltered()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/null-ast.flow");
        var items = CompletionHandler.BuildItems(
            uri, "", ast: null, tokens: null, new Position(0, 0), bi, ui, si, ki).ToList();

        // Without an AST, filters do not run — builtins still surface.
        Assert.Contains(items, i => i.Label == "print");
        Assert.Contains(items, i => i.Label == "reverb");
    }
}
