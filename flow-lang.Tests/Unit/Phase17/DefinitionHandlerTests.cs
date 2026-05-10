using FlowLang.Runtime;
using FlowLsp;
using FlowLsp.Handlers;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 plan 06 Task 2 Facts — DefinitionHandler pure-static helpers.
/// Exercises FindUserDeclaration (AST walk for user symbols) and stdlib path
/// resolution via ModuleLoader.ResolveStdlibPath.
/// </summary>
public class DefinitionHandlerTests
{
    [Fact]
    public void UserProc_FindUserDeclaration_ReturnsLocation()
    {
        var r = LspFixtures.Parse("proc foo ()\n  Int x = 5\nend proc");
        var found = DefinitionHandler.FindUserDeclaration(r.Ast, "foo");
        Assert.NotNull(found);
        Assert.True(found!.Value.Line >= 1);
    }

    [Fact]
    public void UnknownName_ReturnsNull()
    {
        var r = LspFixtures.Parse("proc foo ()\nend proc");
        Assert.Null(DefinitionHandler.FindUserDeclaration(r.Ast, "nonexistent"));
    }

    [Fact]
    public void StdlibImport_ResolvesToPath()
    {
        var path = ModuleLoader.ResolveStdlibPath("@audio");
        Assert.True(System.IO.Path.IsPathRooted(path));
        Assert.EndsWith("audio.flow", path);
    }

    [Fact]
    public void StdlibImport_FileExists_WhenStdlibCopiedToOutput()
    {
        // Rule 2 note: this Fact asserts File.Exists which depends on CopyToOutputDirectory
        // propagating the stdlib .flow files to flow-lang.Tests/bin/... — the project already
        // does this per flow-lang.csproj. If it fails, the CopyToOutputDirectory contract
        // is broken and that's a real regression worth surfacing.
        var path = ModuleLoader.ResolveStdlibPath("@audio");
        Assert.True(System.IO.File.Exists(path), $"Expected stdlib file at {path}");
    }

    [Fact]
    public void UserVariable_FindUserDeclaration_ReturnsLocation()
    {
        var r = LspFixtures.Parse("Int myVar = 42");
        var found = DefinitionHandler.FindUserDeclaration(r.Ast, "myVar");
        Assert.NotNull(found);
    }

    [Fact]
    public void NestedProcInProc_FindsInnerDecl()
    {
        // Flow's `proc ... end proc` syntax — inner proc declared inside outer body.
        var r = LspFixtures.Parse("proc outer ()\n  proc inner ()\n  end proc\nend proc");
        var found = DefinitionHandler.FindUserDeclaration(r.Ast, "inner");
        Assert.NotNull(found);
    }

    // === WR-01 regression guards — word-boundary `use` detection ===

    [Fact]
    public void HasUseKeywordBefore_BareUseKeyword_ReturnsTrue()
    {
        Assert.True(DefinitionHandler.HasUseKeywordBefore("use "));
        Assert.True(DefinitionHandler.HasUseKeywordBefore("  use "));
        Assert.True(DefinitionHandler.HasUseKeywordBefore("use"));
    }

    [Fact]
    public void HasUseKeywordBefore_SubstringMatches_ReturnsFalse()
    {
        // `misuse`, `abuser`, `houses` all contain `use` as a substring but not
        // as a standalone keyword — WR-04 companion concern.
        Assert.False(DefinitionHandler.HasUseKeywordBefore("misuse "));
        Assert.False(DefinitionHandler.HasUseKeywordBefore("abuser "));
        Assert.False(DefinitionHandler.HasUseKeywordBefore("x_use"));
        Assert.False(DefinitionHandler.HasUseKeywordBefore("used"));
    }

    [Fact]
    public void HasUseKeywordBefore_UseFollowedBySpaceOrQuote_ReturnsTrue()
    {
        // The real-world use site: `use "@audio"` → prefix is `use ` before `"`.
        Assert.True(DefinitionHandler.HasUseKeywordBefore("use "));
        // End-of-prefix right after `use` (no trailing space) is still valid —
        // HasUseKeywordBefore only enforces that `use` is NOT embedded in an
        // identifier on either side.
        Assert.True(DefinitionHandler.HasUseKeywordBefore("use"));
    }

    [Fact]
    public void HasUseKeywordBefore_NoUseInPrefix_ReturnsFalse()
    {
        Assert.False(DefinitionHandler.HasUseKeywordBefore(""));
        Assert.False(DefinitionHandler.HasUseKeywordBefore("Int x = 5"));
        Assert.False(DefinitionHandler.HasUseKeywordBefore("String s = "));
    }

    /// <summary>
    /// WR-01 regression Fact: the stdlib-jump path is gated on (a) cursor inside
    /// the `"@..."` span AND (b) `use` keyword before the opening `"`. A plain
    /// string literal assigning `"@notation"` must NOT trigger the stdlib jump,
    /// even though the `"@` pattern exists on the line.
    /// </summary>
    [Fact]
    public void NonUseStringWithAtPrefix_DoesNotTriggerStdlibJump()
    {
        // The helper HasUseKeywordBefore enforces the gate. Verify with the
        // exact prefix the stdlib-jump branch computes.
        var lineStr = "String s = \"@notation\"";
        var atIdx = lineStr.IndexOf("\"@", System.StringComparison.Ordinal);
        Assert.True(atIdx > 0);
        var prefix = lineStr.Substring(0, atIdx);
        Assert.False(DefinitionHandler.HasUseKeywordBefore(prefix));
    }

    [Fact]
    public void UseImportStringLiteral_TriggersStdlibJump()
    {
        var lineStr = "use \"@audio\"";
        var atIdx = lineStr.IndexOf("\"@", System.StringComparison.Ordinal);
        Assert.True(atIdx > 0);
        var prefix = lineStr.Substring(0, atIdx);
        Assert.True(DefinitionHandler.HasUseKeywordBefore(prefix));
    }
}
