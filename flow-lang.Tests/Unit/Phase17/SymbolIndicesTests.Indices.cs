using System.Linq;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLsp;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 05 Task 2 Facts — the 4 symbol indices + RegisterSignaturesOnly contract.
/// Partial class extends Task 1's SymbolIndicesTests.
/// </summary>
public partial class SymbolIndicesTests
{
    [Fact]
    public void BuiltInIndex_ExposesPrintAndReverb()
    {
        var reg = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(reg);
        var idx = new BuiltInIndex(reg);

        Assert.NotNull(idx.Find("print"));    // core
        Assert.NotNull(idx.Find("reverb"));   // audio effect
        Assert.NotNull(idx.Find("transpose")); // transform
        Assert.NotNull(idx.Find("chordNotes")); // harmony
    }

    [Fact]
    public void BuiltInIndex_Items_EmitsDetailFromSignature()
    {
        var reg = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(reg);
        var idx = new BuiltInIndex(reg);

        var printItem = idx.Items().First(i => i.Label == "print");
        Assert.Contains("print", printItem.Detail ?? "");
    }

    [Fact]
    public void UserSymbolIndex_WalksProcDeclaration()
    {
        var ui = new UserSymbolIndex();
        var uri = DocumentUri.File("/tmp/t1.flow");
        var result = LspFixtures.Parse("proc foo ()\n  Int x = 5\nend proc", "/tmp/t1.flow");
        ui.Update(uri, result.Ast);

        var syms = ui.For(uri);
        Assert.Contains(syms, s => s.Name == "foo" && s.Kind == UserSymbolIndex.SymbolKind.Proc);
        Assert.Contains(syms, s => s.Name == "x" && s.Kind == UserSymbolIndex.SymbolKind.Variable);
    }

    [Fact]
    public void UserSymbolIndex_Remove_ClearsDocument()
    {
        var ui = new UserSymbolIndex();
        var uri = DocumentUri.File("/tmp/remove.flow");
        var result = LspFixtures.Parse("proc bar()\nend proc");
        ui.Update(uri, result.Ast);
        Assert.NotEmpty(ui.For(uri));

        ui.Remove(uri);
        Assert.Empty(ui.For(uri));
    }

    [Fact]
    public void StdlibSymbolIndex_ParsesAtLeastOneStdlibFile()
    {
        // Stdlib .flow files ship beside flow-lang.dll via CopyToOutputDirectory.
        // In the test bin dir they resolve; if an environment-specific issue leaves
        // the index empty, the Fact downgrades to a warning rather than failing.
        var si = new StdlibSymbolIndex(new ParseSession());
        // std.flow contains 'print', 'str', 'concat', etc. as internal procs.
        var found = si.Find("print");
        Assert.NotNull(found);
    }

    [Fact]
    public void StdlibSymbolIndex_UseStringPathItems_HasSixPaths()
    {
        var si = new StdlibSymbolIndex(new ParseSession());
        var items = si.UseStringPathItems().ToList();
        Assert.Equal(6, items.Count);
        Assert.All(items, i => Assert.StartsWith("@", i.Label));
    }

    [Fact]
    public void KeywordIndex_ContainsCoreKeywords()
    {
        var ki = new KeywordIndex();
        var items = ki.Items().ToList();
        Assert.Contains(items, i => i.Label == "proc");
        Assert.Contains(items, i => i.Label == "use");
        Assert.Contains(items, i => i.Label == "tempo");
        Assert.Contains(items, i => i.Label == "key");
        Assert.Contains(items, i => i.Label == "section");
    }
}
