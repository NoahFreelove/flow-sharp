using System.Linq;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLsp;
using FlowLsp.Handlers;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 05 Task 2 Facts — CompletionHandler surface.
/// Exercises BuildItems (the pure inner function) so tests don't need an LSP facade.
/// </summary>
public class CompletionHandlerTests
{
    private static (BuiltInIndex bi, UserSymbolIndex ui, StdlibSymbolIndex si, KeywordIndex ki) MakeIndices()
    {
        var reg = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(reg); // D-07 full coverage, audio-free
        var parser = new ParseSession();
        return (new BuiltInIndex(reg), new UserSymbolIndex(), new StdlibSymbolIndex(parser), new KeywordIndex());
    }

    [Fact]
    public void UseString_ReturnsOnlyStdlibPaths()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/t.flow");
        var text = "use \"@";
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(0, text.Length), bi, ui, si, ki).ToList();

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.StartsWith("@", i.Label));
        // Built-ins must NOT leak into use-string completion:
        Assert.DoesNotContain(items, i => i.Label == "print");
        Assert.DoesNotContain(items, i => i.Label == "reverb");
    }

    [Fact]
    public void Default_ReturnsBuiltInsKeywordsSnippets_IncludingAudioAndTransform()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/t.flow");
        // Plain buffer with no `use "` prefix on the current line — default context.
        var text = "proc main ()\n  ";
        var result = LspFixtures.Parse(text);
        var items = CompletionHandler.BuildItems(
            uri, text, result.Ast, result.Tokens, new Position(1, 2), bi, ui, si, ki).ToList();

        Assert.Contains(items, i => i.Label == "proc");      // keyword OR snippet
        Assert.Contains(items, i => i.Label == "print");     // core builtin
        // D-07 completeness proof — audio/transform/harmony all surface:
        Assert.Contains(items, i => i.Label == "reverb");    // audio builtin
        Assert.Contains(items, i => i.Label == "transpose"); // transform builtin
        Assert.Contains(items, i => i.Label == "chordNotes"); // harmony builtin
    }

    [Fact]
    public void UserProc_AppearsAfterParse()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/userprocs.flow");
        var source = "proc myHelper ()\n  Int x = 5\nend proc";
        var result = LspFixtures.Parse(source, "/userprocs.flow");
        ui.Update(uri, result.Ast);

        var items = CompletionHandler.BuildItems(
            uri, source, result.Ast, result.Tokens, new Position(2, 0), bi, ui, si, ki).ToList();

        Assert.Contains(items, i => i.Label == "myHelper");
    }

    [Fact]
    public void NoteStreamWithKey_ReturnsRomanNumerals()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/ns.flow");
        var source = "key Cmajor {\n  | I IV V7 |\n}";
        var result = LspFixtures.Parse(source);
        // Cursor inside the note stream on line 1.
        var items = CompletionHandler.BuildItems(
            uri, source, result.Ast, result.Tokens, new Position(1, 6), bi, ui, si, ki).ToList();

        Assert.Contains(items, i => i.Label == "I");
        Assert.Contains(items, i => i.Label == "IV");
        Assert.Contains(items, i => i.Label == "V7");
        // Must NOT include proc/keyword/builtin names inside a note stream (D-11).
        Assert.DoesNotContain(items, i => i.Label == "print");
        Assert.DoesNotContain(items, i => i.Label == "proc");
        Assert.DoesNotContain(items, i => i.Label == "reverb");
    }

    [Fact]
    public void NoteStreamWithoutKey_ReturnsNoteLettersAndDurations()
    {
        var (bi, ui, si, ki) = MakeIndices();
        var uri = DocumentUri.File("/ns2.flow");
        var source = "proc main ()\n  | C4 D4 |\nend proc";
        var result = LspFixtures.Parse(source);
        // Cursor inside the note stream on line 1, column 6.
        var items = CompletionHandler.BuildItems(
            uri, source, result.Ast, result.Tokens, new Position(1, 6), bi, ui, si, ki).ToList();

        Assert.Contains(items, i => i.Label == "C");        // note letter
        Assert.Contains(items, i => i.Label == "C4");       // octave-4 note
        Assert.Contains(items, i => i.Label == "q");        // duration
        Assert.Contains(items, i => i.Label == "_");        // rest
        // Must NOT include proc/keyword/builtin names inside a note stream (D-11).
        Assert.DoesNotContain(items, i => i.Label == "print");
        Assert.DoesNotContain(items, i => i.Label == "proc");
    }

    [Fact]
    public void SnippetTemplates_AreSnippetKind()
    {
        var snips = CompletionHandler.SnippetTemplates().ToList();
        Assert.All(snips, s => Assert.Equal(InsertTextFormat.Snippet, s.InsertTextFormat));
        Assert.Contains(snips, s => s.Label == "tempo");
        Assert.Contains(snips, s => s.Label == "key");
        Assert.Contains(snips, s => s.Label == "timesig");
        Assert.Contains(snips, s => s.Label == "proc");
        Assert.Contains(snips, s => s.Label == "section");
    }

    [Fact]
    public void IsInsideUseStringLiteral_DetectsOpenQuote()
    {
        // Cursor sitting after `use "` → true.
        Assert.True(CompletionHandler.IsInsideUseStringLiteral(
            "use \"", new Position(0, 5)));
        // Cursor sitting after `use "@audio"` (closed) → false.
        Assert.False(CompletionHandler.IsInsideUseStringLiteral(
            "use \"@audio\"", new Position(0, 12)));
        // No `use` on this line → false.
        Assert.False(CompletionHandler.IsInsideUseStringLiteral(
            "print \"hi\"", new Position(0, 10)));
    }

    // === WR-04 regression guards — word-boundary `use` detection ===

    /// <summary>
    /// WR-04 primary regression Fact: `misuse "foo` must NOT trigger the
    /// stdlib-path completion branch, because `use` inside `misuse` is not
    /// a standalone keyword.
    /// </summary>
    [Fact]
    public void IsInsideUseStringLiteral_OnWordMisuse_ReturnsFalse()
    {
        // Cursor sitting inside `misuse "f|` — substring match on `use` must be
        // rejected because identifier chars `mis` precede the match.
        var text = "misuse \"foo";
        Assert.False(CompletionHandler.IsInsideUseStringLiteral(
            text, new Position(0, text.Length)));
    }

    [Fact]
    public void IsInsideUseStringLiteral_OnWordAbuser_ReturnsFalse()
    {
        var text = "abuser \"x";
        Assert.False(CompletionHandler.IsInsideUseStringLiteral(
            text, new Position(0, text.Length)));
    }

    [Fact]
    public void IsInsideUseStringLiteral_OnWordUsed_ReturnsFalse()
    {
        // Right-side boundary violation: `used` has an identifier char after `use`.
        var text = "used \"x";
        Assert.False(CompletionHandler.IsInsideUseStringLiteral(
            text, new Position(0, text.Length)));
    }

    [Fact]
    public void IsInsideUseStringLiteral_OnWordHouses_ReturnsFalse()
    {
        var text = "houses \"x";
        Assert.False(CompletionHandler.IsInsideUseStringLiteral(
            text, new Position(0, text.Length)));
    }

    [Fact]
    public void IsInsideUseStringLiteral_OnBareUseKeyword_ReturnsTrue()
    {
        // Positive sanity check — the WR-04 fix must not regress the happy path.
        var text = "use \"@aud";
        Assert.True(CompletionHandler.IsInsideUseStringLiteral(
            text, new Position(0, text.Length)));
    }

    [Fact]
    public void IsInsideUseStringLiteral_MisuseFollowedByUseOnSameLine_UsesStandaloneUse()
    {
        // Line has BOTH `misuse` (rejected) and a standalone `use` (accepted)
        // before the cursor — the standalone use should still gate completion on.
        var text = "misuse foo ; use \"@au";
        Assert.True(CompletionHandler.IsInsideUseStringLiteral(
            text, new Position(0, text.Length)));
    }
}
