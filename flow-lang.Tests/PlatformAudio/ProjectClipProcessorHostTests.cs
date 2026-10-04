using System.Diagnostics;
using Flow.Studio.Host;
using FlowLang.Hosting;
using FlowLang.Tests.StudioModel;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

[Collection("FlowScripts")]
public class ProjectClipProcessorHostTests
{
    [Fact]
    public async Task NoteWorkerAppliesTransposedClipAndPublishesPlayback()
    {
        var (doc, clip) = NoteClipProcessingTests.Create(); int count = doc.History.UndoCount;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        await using var host = new ProjectClipProcessorHost(session, worker);
        host.ProcessNotes(clip.Id, NoteTransformPluginTests.Package());
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsProcessing && !session.IsPreparing; }, TimeSpan.FromSeconds(30)));
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        Assert.Equal(count + 1, doc.History.UndoCount); Assert.Null(worker.ProcessId);
        var source = doc.Snapshot.Sources[host.LastCompletion.SourceId];
        Assert.All(source.Result.ScoreLayers[0].Composition.Placements, p =>
        {
            Assert.Equal(.35, p.Section.Settings.Gain);
            Assert.All(p.Section.Sequences[0].Notes, n => Assert.Equal(880, n.Pitch!.FrequencyHz));
        });
        Assert.Same(doc.Snapshot, session.Playback.ActiveSnapshot);
    }
    [Fact]
    public async Task RealWorkerAppliesOneActionAndPublishesPreparedResult()
    {
        var (doc, clip) = AudioClipProcessingTests.Create(); int count = doc.History.UndoCount;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        await using var host = new ProjectClipProcessorHost(session, worker);
        host.ProcessAudio(clip.Id, AudioClipProcessingTests.Package());
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsProcessing && !session.IsPreparing; }, TimeSpan.FromSeconds(30)));
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        Assert.Equal(count + 1, doc.History.UndoCount); Assert.Null(worker.ProcessId);
        Assert.Same(doc.Snapshot, session.Playback.ActiveSnapshot);
    }
    [Fact]
    public async Task DisposalCancelsAndJoinsWithoutAcceptingResult()
    {
        var (doc, clip) = AudioClipProcessingTests.Create(); var before = doc.Snapshot;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        var worker = new ProcessGeneratorWorker(() => new ProcessStartInfo("sleep") { ArgumentList = { "60" } });
        await using var host = new ProjectClipProcessorHost(session, worker);
        host.ProcessAudio(clip.Id, AudioClipProcessingTests.Package());
        Assert.True(SpinWait.SpinUntil(() => worker.ProcessId.HasValue, TimeSpan.FromSeconds(5)));
        await host.DisposeAsync();
        Assert.False(host.IsProcessing); Assert.Null(worker.ProcessId); Assert.Same(before, doc.Snapshot);
        Assert.Throws<ObjectDisposedException>(() => host.Poll());
    }
    [Fact]
    public async Task CancelJoinsWorkerAndNeverMutatesDocument()
    {
        var (doc, clip) = AudioClipProcessingTests.Create(); var before = doc.Snapshot;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        var worker = new ProcessGeneratorWorker(() => new ProcessStartInfo("sleep") { ArgumentList = { "60" } });
        await using var host = new ProjectClipProcessorHost(session, worker);
        host.ProcessAudio(clip.Id, AudioClipProcessingTests.Package());
        Assert.True(SpinWait.SpinUntil(() => worker.ProcessId.HasValue, TimeSpan.FromSeconds(5)));
        Assert.Throws<InvalidOperationException>(() => host.ProcessAudio(clip.Id, AudioClipProcessingTests.Package()));
        host.Cancel();
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsProcessing; }, TimeSpan.FromSeconds(10)));
        Assert.False(host.LastCompletion!.Accepted); Assert.Equal(JobStatus.Superseded, host.LastCompletion.Status);
        Assert.Same(before, doc.Snapshot); Assert.Null(worker.ProcessId); Assert.Equal(1, worker.Kills);
    }
}
