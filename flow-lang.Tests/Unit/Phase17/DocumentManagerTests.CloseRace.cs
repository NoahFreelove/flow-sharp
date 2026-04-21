using FlowLang.Diagnostics;
using FlowLsp;
using FlowLsp.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 03 Task 2 — close-race guard Facts.
///
/// These extend <see cref="DocumentManagerTests"/> (partial class) with end-to-end
/// tests that mirror flow-lsp/Program.cs's onParse wiring: ParseSession + the
/// HasDocument-guarded publish branch. They prove:
///
///   CloseCancelsPendingDiagnostics_NoPublishAfterClose — Close during the debounce
///     window suppresses the subsequent Publish (T-17-12 regression gate).
///
///   OpenThenUpdate_PublishesAfterDebounce — control case: without Close, Publish
///     fires exactly once. Proves the close-race Fact is not trivially passing due
///     to an unrelated Publish-silencing path.
/// </summary>
public partial class DocumentManagerTests
{
    private sealed class RecordingPublisher : IDiagnosticsPublisher
    {
        public int PublishCount { get; private set; }
        public void Publish(DocumentUri uri, IReadOnlyList<FlowError> errors) => PublishCount++;
    }

    [Fact]
    public async Task CloseCancelsPendingDiagnostics_NoPublishAfterClose()
    {
        // Construct the DocumentManager with the SAME close-race-guarded callback wiring
        // that flow-lsp/Program.cs uses, to prove the guard works end-to-end.
        var publisher = new RecordingPublisher();
        var parser = new ParseSession();
        DocumentManager? dm = null;
        dm = new DocumentManager((uri, text, ct) =>
        {
            if (ct.IsCancellationRequested) return Task.CompletedTask;
            var result = parser.Parse(text, null);
            if (dm!.HasDocument(uri))   // <-- the close-race guard under test
                publisher.Publish(uri, result.Errors);
            return Task.CompletedTask;
        });

        var docUri = DocumentUri.File("/test.flow");
        dm.Update(docUri, "proc greet()\n    (print \"hi\")\nend proc");   // arm debounce
        dm.Close(docUri);                                                    // close BEFORE debounce fires
        await Task.Delay(300);                                               // past debounce window

        Assert.Equal(0, publisher.PublishCount);                             // guard held — no stale Publish
    }

    [Fact]
    public async Task OpenThenUpdate_PublishesAfterDebounce()
    {
        // Control case — without Close, the publish DOES fire. Proves
        // CloseCancelsPendingDiagnostics is not trivially passing because
        // some unrelated path silenced Publish.
        var publisher = new RecordingPublisher();
        var parser = new ParseSession();
        DocumentManager? dm = null;
        dm = new DocumentManager((uri, text, ct) =>
        {
            if (ct.IsCancellationRequested) return Task.CompletedTask;
            var result = parser.Parse(text, null);
            if (dm!.HasDocument(uri))
                publisher.Publish(uri, result.Errors);
            return Task.CompletedTask;
        });

        var docUri = DocumentUri.File("/test.flow");
        dm.Update(docUri, "proc greet()\n    (print \"hi\")\nend proc");
        await Task.Delay(300);

        Assert.Equal(1, publisher.PublishCount);
    }

    /// <summary>
    /// Discrimination Fact: force the race window where the debounce has already
    /// expired (parse is running) and Close arrives BEFORE the publish step. A
    /// slow-parse TaskCompletionSource simulates a parse in-flight; Close fires
    /// between delay expiry and publish; the HasDocument guard must gate Publish.
    ///
    /// This is the narrow case the CTS-cancel path at ScheduleParseAsync does NOT
    /// cover — by the time ct is observed for cancellation inside the callback,
    /// the parse has already completed and only the HasDocument check can prevent
    /// a stale Publish.
    /// </summary>
    [Fact]
    public async Task ParseCompletingConcurrentlyWithClose_DoesNotPublishWhenGuarded()
    {
        var publisher = new RecordingPublisher();
        var parseGate = new TaskCompletionSource();
        var parseStarted = new TaskCompletionSource();
        DocumentManager? dm = null;
        dm = new DocumentManager(async (uri, text, ct) =>
        {
            parseStarted.TrySetResult();
            await parseGate.Task;                 // block until the test releases
            if (dm!.HasDocument(uri))             // close-race guard under test
                publisher.Publish(uri, Array.Empty<FlowError>());
        });

        var docUri = DocumentUri.File("/test.flow");
        dm.Update(docUri, "any");
        // Wait until parse callback has begun (past ScheduleParseAsync's ct check).
        await parseStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        dm.Close(docUri);                         // close WHILE parse is in-flight
        parseGate.SetResult();                    // release the parse → Publish branch evaluates
        await Task.Delay(100);                    // give the callback time to complete

        Assert.Equal(0, publisher.PublishCount);  // guard suppressed the Publish
    }
}
