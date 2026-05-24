using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowInterpreter;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using PrettyPrompt.Documents;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-04 — REPL Tab-completion behaviour wired through the in-process
/// LSP CompletionHandler.BuildItems() per D-38-12 SIMPLIFICATION FINDING (RESEARCH §G
/// lines 854-929). FlowPromptCallbacks (created in Task 2) routes every Tab through
/// the same static helper the LSP CompletionHandler ships to VSCode — no MemoryStream
/// transport plumbing.
/// </summary>
[Collection("FlowScripts")]
public class ReplCompletionTests : IDisposable
{
    public ReplCompletionTests()
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
    /// Typing <c>(transp</c> and pressing Tab MUST surface a completion item labelled
    /// <c>transpose</c> — the Phase 31 BuiltInIndex contains it via the
    /// RegisterSignaturesOnly registry sweep (BuiltInDocs.cs:81+ ships a doc entry).
    /// </summary>
    [Fact]
    public async Task TabAtPartialIdentifier_ReturnsBuildItemsResults()
    {
        using var editor = new ReplLineEditor(promptText: "> ", continuationPrompt: "... ",
            historyFilePath: System.IO.Path.GetTempFileName());

        const string text = "(transp";
        var items = await editor.GetCompletionItemsForTesting(text, caret: text.Length, CancellationToken.None);

        Assert.NotEmpty(items);
        Assert.Contains(items, i =>
            string.Equals(i.ReplacementText, "transpose", StringComparison.Ordinal) ||
            string.Equals(i.DisplayText.ToString(), "transpose", StringComparison.Ordinal));
    }

    /// <summary>
    /// On an empty prompt line, Tab MUST surface a non-empty merge of keywords + builtins
    /// (the default BuildItems merge per CompletionHandler.cs:126-130). Asserts presence
    /// of at least one well-known keyword (proc / tempo) AND at least one builtin
    /// (play / renderSong) so a regression that drops EITHER source is caught.
    /// </summary>
    [Fact]
    public async Task TabOnEmptyInput_ReturnsKeywordAndBuiltInsList()
    {
        using var editor = new ReplLineEditor(promptText: "> ", continuationPrompt: "... ",
            historyFilePath: System.IO.Path.GetTempFileName());

        var items = (await editor.GetCompletionItemsForTesting(string.Empty, caret: 0, CancellationToken.None)).ToList();

        Assert.NotEmpty(items);

        var labels = items.Select(i => i.ReplacementText).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(labels, l => l == "proc" || l == "tempo");
        Assert.Contains(labels, l => l == "play" || l == "renderSong");
    }
}
