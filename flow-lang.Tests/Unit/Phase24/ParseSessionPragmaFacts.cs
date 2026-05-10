using FlowLang.Tests.Unit.Phase17;
using Xunit;

namespace FlowLang.Tests.Unit.Phase24;

/// <summary>
/// Phase 24 Wave 0 (Plan 24-00): pins that flow-lsp ParseSession.Parse runs
/// PragmaScanner.Scan upstream so Program.Pragmas is populated from source.
///
/// RESEARCH F1 / Pitfall 1: pre-Phase-24 ParseSession only called the 2-arg
/// Parser ctor; Pragmas was always PragmaSet.Empty. This was a latent bug
/// that also broke `enable hAsB;` in LSP-edited files (composers saw a
/// spurious "unknown identifier H4q"). Wave 0 widens ParseSession to mirror
/// FlowEngine.Run() lines 66-82, fixing both at once.
///
/// Decisions referenced (24-CONTEXT.md):
///   D-19 — activation gate is `parseResult.Ast.Pragmas.Has("scaleLint")`.
///          Cannot work without this widen.
/// </summary>
public class ParseSessionPragmaFacts
{
    [Fact]
    public void Parse_EnableHAsB_PopulatesPragmas()
    {
        // Uses hAsB (a Phase 21 known pragma) so this Fact does not depend on
        // Plan 24-01's registry add. Pins the pragma-scan widening in isolation.
        var result = LspFixtures.Parse("enable hAsB;\n| C4q |");
        Assert.True(result.Ast.Pragmas.Has("hAsB"),
            "ParseSession.Parse must run PragmaScanner.Scan so Ast.Pragmas reflects source.");
    }

    [Fact]
    public void Parse_NoEnable_PragmasIsEmpty()
    {
        var result = LspFixtures.Parse("key Cmajor { | C4 D4 | }");
        Assert.False(result.Ast.Pragmas.Has("hAsB"));
        Assert.False(result.Ast.Pragmas.Has("scaleLint"));
    }

    [Fact]
    public void Parse_EnableHAsB_LexesH4qAsNoteLiteral()
    {
        // Wave 0 latent-bug regression: ParseSession now honors enable hAsB;
        // pre-Wave-0, the lexer never saw the pragmaSet so H4q lexed as an
        // identifier, surfacing a spurious diagnostic.
        var result = LspFixtures.Parse("enable hAsB;\n| H4q |");
        Assert.Empty(result.Errors);
    }
}
