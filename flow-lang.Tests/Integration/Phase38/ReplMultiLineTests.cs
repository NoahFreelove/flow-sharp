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
}
