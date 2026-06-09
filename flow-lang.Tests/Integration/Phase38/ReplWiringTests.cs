using System;
using System.Linq;
using System.Threading;
using FlowInterpreter;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Audit 0609 §5.1 — regression coverage for wiring the Phase 38
/// <see cref="ReplLineEditor"/> (PrettyPrompt + flow-lsp Tab completion + Ctrl+R +
/// persistent history) into the production REPL loop.
///
/// The interactive PrettyPrompt path itself cannot be driven by xUnit (no TTY);
/// the real terminal smoke (Tab completion, Ctrl+R, history persistence) is a
/// HUMAN-UAT row. What IS pinned here:
///   1. <see cref="Repl.ShouldUseInteractiveEditor(bool,bool)"/> — the
///      redirected → legacy fallback decision (pure; all 4 combos).
///   2. <see cref="ReplCaretPosition.CaretToPosition"/> — the offset→(line,char)
///      conversion that fixes the line-0 completion-callback bug for multi-line
///      buffers (pure; tested directly).
///   3. ReplLineEditor construction is lazy + safe (constructing it in-process
///      with a temp history file does not throw and yields a working completion
///      pipeline + multi-line position resolution).
/// </summary>
[Collection("FlowScripts")]
public class ReplWiringTests : IDisposable
{
    public ReplWiringTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. Fallback decision — redirected stdin OR stdout disqualifies PrettyPrompt.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldUseInteractiveEditor_BothLive_True()
    {
        // A real interactive console (neither stream redirected) → use the editor.
        Assert.True(Repl.ShouldUseInteractiveEditor(inputRedirected: false, outputRedirected: false));
    }

    [Theory]
    [InlineData(true, false)]   // piped stdin (scripted/CI/redirected-stdin REPL tests)
    [InlineData(false, true)]   // captured stdout
    [InlineData(true, true)]    // fully redirected
    public void ShouldUseInteractiveEditor_AnyRedirected_FallsBackToLegacy(bool inRedir, bool outRedir)
    {
        // Audit 0609 §5.1 — the whole point of the fallback: any redirection MUST
        // route to the legacy Console.ReadLine path so piped/scripted usage stays
        // byte-identical to pre-wiring behaviour.
        Assert.False(Repl.ShouldUseInteractiveEditor(inputRedirected: inRedir, outputRedirected: outRedir));
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. CaretToPosition — the multi-line completion-callback fix (was line:0).
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CaretToPosition_SingleLine_LineZeroCharIsOffset()
    {
        // The historical single-line case must still map offset → character on line 0.
        var pos = ReplCaretPosition.CaretToPosition("(transp", caret: 7);
        Assert.Equal(0, pos.Line);
        Assert.Equal(7, pos.Character);
    }

    [Fact]
    public void CaretToPosition_CaretAtStart_Origin()
    {
        var pos = ReplCaretPosition.CaretToPosition("anything", caret: 0);
        Assert.Equal(0, pos.Line);
        Assert.Equal(0, pos.Character);
    }

    [Fact]
    public void CaretToPosition_SecondLine_ComputesLineAndPerLineCharacter()
    {
        // THE BUG: a caret offset into the SECOND physical line was previously fed
        // as `new Position(line: 0, character: caret)`, overrunning line 0.
        //   "proc f\n  (g"  →  index map:
        //     line 0 = "proc f"  (offsets 0..5, '\n' at 6)
        //     line 1 = "  (g"    (offsets 7..10)
        // Caret at offset 10 (just after 'g') must be (line 1, character 3).
        const string buf = "proc f\n  (g";
        var pos = ReplCaretPosition.CaretToPosition(buf, caret: 10);
        Assert.Equal(1, pos.Line);
        Assert.Equal(3, pos.Character);
    }

    [Fact]
    public void CaretToPosition_ThirdLine_AccumulatesLineCount()
    {
        const string buf = "a\nbb\nccc";   // lines: "a"(0..0,\n@1) "bb"(2..3,\n@4) "ccc"(5..7)
        var pos = ReplCaretPosition.CaretToPosition(buf, caret: buf.Length); // end of "ccc"
        Assert.Equal(2, pos.Line);
        Assert.Equal(3, pos.Character);
    }

    [Fact]
    public void CaretToPosition_CaretOnNewline_MapsToEndOfTerminatedLine()
    {
        // A caret sitting exactly on the '\n' that ends line 0 belongs to line 0
        // (the newline is the last column of the line it terminates).
        const string buf = "proc f\n  (g";
        var pos = ReplCaretPosition.CaretToPosition(buf, caret: 6); // the '\n' index
        Assert.Equal(0, pos.Line);
        Assert.Equal(6, pos.Character);
    }

    [Fact]
    public void CaretToPosition_EmptyText_Origin()
    {
        var pos = ReplCaretPosition.CaretToPosition(string.Empty, caret: 0);
        Assert.Equal(0, pos.Line);
        Assert.Equal(0, pos.Character);
    }

    [Theory]
    [InlineData(-5)]   // negative
    [InlineData(999)]  // past end
    public void CaretToPosition_OutOfRangeCaret_ClampsCharitably(int caret)
    {
        // Charitable D-v1.5-05 — a stray caret never throws.
        var pos = ReplCaretPosition.CaretToPosition("ab\ncd", caret);
        Assert.True(pos.Line >= 0);
        Assert.True(pos.Character >= 0);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. Lazy/safe construction + the fixed multi-line completion path.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ReplLineEditor_ConstructsWithTempHistory_DoesNotThrow()
    {
        // Construction must be safe (the production loop guards it in a try/catch,
        // but the happy path must succeed with a writable history file).
        var ex = Record.Exception(() =>
        {
            using var editor = new ReplLineEditor(
                promptText: "> ", continuationPrompt: "... ",
                historyFilePath: System.IO.Path.GetTempFileName());
        });
        Assert.Null(ex);
    }

    [Fact]
    public async System.Threading.Tasks.Task Completion_OnSecondLineOfMultiLineBuffer_StillResolves()
    {
        // Drive the editor's real completion pipeline (which now uses
        // CaretToPosition) with a caret that lives on the SECOND line and assert
        // it still returns sensible builtin completions for the partial
        // identifier there.
        using var editor = new ReplLineEditor(
            promptText: "> ", continuationPrompt: "... ",
            historyFilePath: System.IO.Path.GetTempFileName());

        const string buf = "Int x = 5\n(transp";   // caret at end → on line 1, partial "transp"
        var items = await editor.GetCompletionItemsForTesting(buf, caret: buf.Length, CancellationToken.None);

        Assert.NotEmpty(items);
        Assert.Contains(items, i =>
            string.Equals(i.ReplacementText, "transpose", StringComparison.Ordinal) ||
            string.Equals(i.DisplayText.ToString(), "transpose", StringComparison.Ordinal));
    }

    [Fact]
    public async System.Threading.Tasks.Task Completion_InsideUseStringOnSecondLine_ReturnsModulePathsNotBuiltins()
    {
        // Audit 0609 §5.1 fail-before/pass-after — the tight discriminator for the
        // line-0 Position bug. The LSP CompletionHandler's `use "..."` context
        // detection (IsInsideUseStringLiteral) indexes `lines[Position.Line]`. With
        // the OLD `Position(line:0, character:caret)`, a `use "` that begins on the
        // SECOND physical line is invisible — the callback returns the DEFAULT merge
        // (builtins + keywords, NO module-only list). With CaretToPosition it lands
        // on line 1, recognises the open `use "` string, and returns ONLY the
        // module-path items (@std/@audio/...), which contain no bare builtins.
        using var editor = new ReplLineEditor(
            promptText: "> ", continuationPrompt: "... ",
            historyFilePath: System.IO.Path.GetTempFileName());

        const string buf = "Int x = 5\nuse \"@aud";   // line 1 = open `use "` string literal
        var items = await editor.GetCompletionItemsForTesting(buf, caret: buf.Length, CancellationToken.None);

        Assert.NotEmpty(items);
        // Module-path completions are present (the use-string context fired)...
        Assert.Contains(items, i => i.ReplacementText.StartsWith("@", StringComparison.Ordinal));
        // ...and the default-merge builtins/keywords are ABSENT (they would leak in
        // under the buggy line-0 Position because the use-context never matched).
        var labels = items.Select(i => i.ReplacementText).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("transpose", labels);
        Assert.DoesNotContain("proc", labels);
    }
}
