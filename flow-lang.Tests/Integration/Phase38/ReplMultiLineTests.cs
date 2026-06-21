using System;
using FlowInterpreter;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-04 — multi-line continuation logic preserved across the PrettyPrompt
/// swap. The existing paren-balanced lexer detection (Repl.cs:182-208) AND the backslash-
/// at-EOL fallback (Repl.cs:117-119) BOTH continue to drive continuation; PrettyPrompt
/// merely re-routes the input loop through FlowPromptCallbacks per D-38-11.
/// </summary>
[Collection("FlowScripts")]
public class ReplMultiLineTests : IDisposable
{
    public ReplMultiLineTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// Unbalanced parens MUST request continuation. The static helper
    /// <see cref="ReplInputCompleteness.IsInputComplete"/> mirrors the existing
    /// Repl.cs:182-208 paren-balance check (extracted so both the legacy
    /// Console.ReadLine path AND the new PrettyPrompt path call the same logic).
    /// </summary>
    [Fact]
    public void UnbalancedParens_RequestsContinuation()
    {
        var complete = ReplInputCompleteness.IsInputComplete("(add 1");
        Assert.False(complete, "Unbalanced open-paren must request continuation");
    }

    /// <summary>
    /// Backslash-at-EOL MUST request continuation (preserved Repl.cs:117-119 contract).
    /// The static helper recognises trailing backslash and returns false.
    /// </summary>
    [Fact]
    public void BackslashContinuation_RequestsContinuation()
    {
        var complete = ReplInputCompleteness.IsInputComplete("Int x = 5 \\");
        Assert.False(complete, "Backslash-at-EOL must request continuation");
    }

    /// <summary>
    /// Balanced single-line input MUST submit immediately (no continuation).
    /// Regression guard against accidentally requesting continuation for the common case.
    /// </summary>
    [Fact]
    public void BalancedSingleLine_DoesNotRequestContinuation()
    {
        var complete = ReplInputCompleteness.IsInputComplete("(add 1 2)");
        Assert.True(complete, "Balanced single-line input should submit immediately");
    }

    /// <summary>
    /// sweep-0614 (cli-repl-watch): an UNTERMINATED note stream (odd pipe count)
    /// MUST request continuation. Before the fix the completeness check ignored
    /// TokenType.Pipe, judged `Sequence s = | C4q D4q` complete, and the REPL
    /// submitted immediately — the parser charitably truncated the bar and the
    /// composer's intended continuation (`E4 F4 |`) became a separate broken
    /// statement (silent input truncation hidden by charity).
    /// </summary>
    [Fact]
    public void UnterminatedNoteStream_RequestsContinuation()
    {
        Assert.False(
            ReplInputCompleteness.IsInputComplete("Sequence s = | C4q D4q"),
            "Open note stream (one unbalanced '|') must request continuation");
        Assert.False(
            ReplInputCompleteness.IsInputComplete("| C4 D4 E4"),
            "Bare open note stream must request continuation");
    }

    /// <summary>
    /// A balanced note stream (even pipe count) MUST submit immediately — both
    /// the single-line `| ... |` and the fully-closed assignment form. Guards the
    /// pipe-balance fix against false positives (every complete input has an even
    /// pipe count because '|' is exclusively a balanced delimiter in Flow).
    /// </summary>
    [Fact]
    public void BalancedNoteStream_DoesNotRequestContinuation()
    {
        Assert.True(
            ReplInputCompleteness.IsInputComplete("| C4 D4 E4 |"),
            "Closed note stream (balanced '|') should submit immediately");
        Assert.True(
            ReplInputCompleteness.IsInputComplete("Sequence s = | C4q D4q E4q F4q |"),
            "Closed note-stream assignment should submit immediately");
    }

    /// <summary>
    /// quick-260621-n9y — the REPORTED FREEZE. A single-line N-bar stream emits
    /// N+1 pipes (the lexer emits one TokenType.Pipe per `|` INCLUDING the bar
    /// separators), so a 4-bar stream is 5 pipes = odd. The old
    /// `pipeCount % 2 == 0` parity check judged it "incomplete" and the REPL hung
    /// forever in continuation mode. Single-bar streams (2 pipes = even) happened
    /// to pass under both the old parity check and the new scan, which is why the
    /// existing tests did NOT catch the multi-bar freeze. The note-stream-aware
    /// scan now correctly judges any closed multi-bar stream COMPLETE.
    /// </summary>
    [Fact]
    public void MultiBarSingleLineStream_DoesNotRequestContinuation()
    {
        Assert.True(
            ReplInputCompleteness.IsInputComplete(
                "| C4q E4q G4q C5q | D4q F4q A4q D5q | E4q G4q B4q E5q | F4q A4q C5q F5q |"),
            "Reported 4-bar freeze (5 pipes, odd parity) must submit immediately");
        Assert.True(
            ReplInputCompleteness.IsInputComplete("| C4 D4 | E4 F4 |"),
            "Closed two-bar stream (3 pipes, odd parity) must submit immediately");
    }

    /// <summary>
    /// quick-260621-n9y — mid-typing a multi-bar stream must request continuation.
    /// `Sequence s = | C4 | D4` is 2 pipes = even, which the old parity check
    /// wrongly judged "complete" (false positive); the second `|` is a bar
    /// separator (the next token `D4` continues the stream) so the stream is still
    /// open at end of buffer.
    /// </summary>
    [Fact]
    public void MidTypedMultiBarStream_RequestsContinuation()
    {
        Assert.False(
            ReplInputCompleteness.IsInputComplete("Sequence s = | C4 | D4"),
            "Mid-typing across a bar boundary (2 pipes, even parity) must request continuation");
        Assert.False(
            ReplInputCompleteness.IsInputComplete("Sequence s = | C4 D4"),
            "Single open bar mid-typing must request continuation");
    }

    /// <summary>
    /// quick-260621-n9y — a balanced block containing a multi-bar stream must
    /// submit. The stream closes mid-buffer and the trailing `}` after the closing
    /// pipe must NOT re-open inStream (it is not a note-stream token), and the brace
    /// balances. The inner 4-bar stream is 5 pipes (odd) which the old parity check
    /// would have frozen even though every brace was closed. An OPEN block with a
    /// closed stream still requests continuation because the `{` is unbalanced.
    /// </summary>
    [Fact]
    public void BalancedBlockWithMultiBarStream_DoesNotRequestContinuation()
    {
        Assert.True(
            ReplInputCompleteness.IsInputComplete(
                "tempo 120 { Sequence s = | C4q D4q | E4q F4q | G4q A4q | B4q C5q | }"),
            "Balanced block with a multi-bar stream (closing pipe then '}') must submit");
        Assert.False(
            ReplInputCompleteness.IsInputComplete("tempo 120 { Sequence s = | C4 D4 |"),
            "Closed stream inside an unbalanced '{' must request continuation");
    }
}
