using System.Collections.Concurrent;
using FlowLsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 03 — DocumentManager Facts.
///
/// Task 1 covers the debounce + cancel + HasDocument contract. Task 2 extends this
/// class (via <c>DocumentManagerTests.CloseRace.cs</c>) with two Facts that exercise
/// the close-race guard end-to-end through the ParseSession + IDiagnosticsPublisher
/// wiring Program.cs applies.
/// </summary>
public partial class DocumentManagerTests
{
    private sealed class Recorder
    {
        public readonly ConcurrentQueue<(string Text, bool Cancelled)> Calls = new();
        public Task Record(DocumentUri uri, string text, CancellationToken ct)
        {
            Calls.Enqueue((text, ct.IsCancellationRequested));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Update_AfterDebounce_InvokesOnParse()
    {
        var r = new Recorder();
        var dm = new DocumentManager(r.Record);
        var uri = DocumentUri.File("/test.flow");

        dm.Update(uri, "hello");
        await Task.Delay(300);

        Assert.Single(r.Calls);
        Assert.True(r.Calls.TryPeek(out var call));
        Assert.Equal("hello", call.Text);
    }

    [Fact]
    public async Task RapidUpdates_CancelPriorInFlight()
    {
        var r = new Recorder();
        var dm = new DocumentManager(r.Record);
        var uri = DocumentUri.File("/test.flow");

        dm.Update(uri, "a");
        dm.Update(uri, "b");
        dm.Update(uri, "c");
        await Task.Delay(300);

        // At most the last text may have fired; earlier work cancelled before debounce expired.
        Assert.True(r.Calls.Count <= 1, $"Expected ≤1 callback, got {r.Calls.Count}");
        if (r.Calls.TryPeek(out var call))
            Assert.Equal("c", call.Text);
    }

    [Fact]
    public async Task Close_CancelsPendingParse()
    {
        var r = new Recorder();
        var dm = new DocumentManager(r.Record);
        var uri = DocumentUri.File("/test.flow");

        dm.Update(uri, "hello");
        dm.Close(uri);
        await Task.Delay(300);

        Assert.Empty(r.Calls);
    }

    [Fact]
    public void HasDocument_TrueAfterUpdate_FalseAfterClose_FalseForNeverOpened()
    {
        var r = new Recorder();
        var dm = new DocumentManager(r.Record);
        var uri = DocumentUri.File("/test.flow");

        Assert.False(dm.HasDocument(uri));   // never opened
        dm.Update(uri, "hello");
        Assert.True(dm.HasDocument(uri));    // open
        dm.Close(uri);
        Assert.False(dm.HasDocument(uri));   // closed
    }
}
