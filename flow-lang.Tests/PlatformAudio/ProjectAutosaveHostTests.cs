using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Tests.StudioModel;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class ProjectAutosaveHostTests
{
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance() => _ticks += TimeSpan.FromSeconds(31).Ticks;
    }
    private static string DirectoryPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"flow-autosave-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(path); return path;
    }
    private static void Edit(ProjectDocument doc) => doc.Edit("Edit", p => new(p.Arrangement, p.Context, p.Sources.Values,
        new(p.Routing.Tracks.Select(t => t with { Name = t.Name + " edited" }), p.Routing.GraphBinding), p.Assets, p.Automation));
    private static void Finish(ProjectAutosaveHost host)
        => Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsSaving; }, TimeSpan.FromSeconds(10)));

    [Fact]
    public async Task AutosaveIsThrottledPreservesDirtyStateAndCleanSaveRemovesOwnedRecovery()
    {
        string root = DirectoryPath(); string recovery = Path.Combine(root, "recovery.flowproject"), project = Path.Combine(root, "song.flowproject");
        var (doc, _) = AudioClipProcessingTests.Create(); var clock = new Clock();
        try
        {
            await using var host = new ProjectAutosaveHost(doc, recovery, project, clock: clock);
            host.Poll(); Assert.False(host.IsSaving); Assert.False(File.Exists(recovery));
            clock.Advance(); host.Poll(); Finish(host);
            Assert.True(doc.History.IsDirty); Assert.Equal(doc.ChangeVersion, host.LastSavedVersion);
            Assert.Equal(ProjectJson.Serialize(doc.Snapshot), ProjectJson.Serialize(ProjectFile.Load(recovery).Snapshot));
            clock.Advance(); host.Poll(); Assert.False(host.IsSaving);
            ProjectFile.Save(project, doc); clock.Advance(); host.Poll(); Finish(host);
            Assert.False(File.Exists(recovery)); Assert.False(doc.History.IsDirty);
            Assert.Throws<IOException>(() => new ProjectAutosaveHost(doc, recovery, project));
            Assert.Throws<IOException>(() => ProjectRecovery.Discard(recovery));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task CloseJoinsOldWriteAndFlushesLatestCapturedEdit()
    {
        string root = DirectoryPath(); string recovery = Path.Combine(root, "recovery.flowproject");
        var (doc, _) = AudioClipProcessingTests.Create(); var clock = new Clock();
        using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        int writes = 0;
        var host = new ProjectAutosaveHost(doc, recovery, null, null, clock, (path, snapshot) =>
        {
            if (Interlocked.Increment(ref writes) == 1) { started.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(10))); }
            ProjectFile.SaveRecovery(path, snapshot);
        });
        try
        {
            clock.Advance(); host.Poll(); Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Edit(doc); var latest = doc.Snapshot; clock.Advance(); host.Poll(); Assert.Equal(1, writes);
            var closing = host.DisposeAsync().AsTask(); Assert.False(closing.IsCompleted);
            release.Set(); await closing;
            Assert.Equal(2, writes); Assert.True(doc.History.IsDirty);
            Assert.Equal(ProjectJson.Serialize(latest), ProjectJson.Serialize(ProjectFile.Load(recovery).Snapshot));
            Assert.Throws<ObjectDisposedException>(() => host.Poll());
        }
        finally { release.Set(); await host.DisposeAsync(); Directory.Delete(root, true); }
    }
    [Fact]
    public async Task FailedWriteKeepsPreviousRecoveryAndRetriesOnNextInterval()
    {
        string root = DirectoryPath(); string recovery = Path.Combine(root, "recovery.flowproject");
        var (doc, _) = AudioClipProcessingTests.Create(); var clock = new Clock(); int writes = 0;
        try
        {
            await using var host = new ProjectAutosaveHost(doc, recovery, null, null, clock, (path, snapshot) =>
            {
                if (Interlocked.Increment(ref writes) == 2) throw new IOException("Injected failure");
                ProjectFile.SaveRecovery(path, snapshot);
            });
            clock.Advance(); host.Poll(); Finish(host); string previous = File.ReadAllText(recovery);
            Edit(doc); clock.Advance(); host.Poll(); Finish(host);
            Assert.IsType<IOException>(host.LastError); Assert.Equal(previous, File.ReadAllText(recovery));
            host.Poll(); Assert.Equal(2, writes);
            clock.Advance(); host.Poll(); Finish(host); Assert.Null(host.LastError); Assert.Equal(3, writes);
            Assert.Equal(ProjectJson.Serialize(doc.Snapshot), ProjectJson.Serialize(ProjectFile.Load(recovery).Snapshot));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void RecoveryInspectionHandlesCorruptSavedFileAndRestoresDirtyWithoutHistory()
    {
        string root = DirectoryPath(); string recovery = Path.Combine(root, "recovery.flowproject"), project = Path.Combine(root, "song.flowproject");
        var (doc, _) = AudioClipProcessingTests.Create();
        try
        {
            ProjectFile.SaveRecovery(recovery, doc.Snapshot); File.WriteAllText(project, "broken");
            var inspection = ProjectRecovery.Inspect(project, recovery);
            Assert.NotNull(inspection.SavedError); Assert.Null(inspection.RecoveryError); Assert.True(inspection.DiffersFromSaved);
            var restored = ProjectRecovery.Restore(inspection); Assert.True(restored.History.IsDirty); Assert.Equal(0, restored.History.UndoCount);
            Assert.Throws<InvalidOperationException>(() => new ProjectAutosaveHost(restored, recovery, project));
            ProjectFile.Save(project, restored); Assert.False(ProjectRecovery.Inspect(project, recovery).DiffersFromSaved);
            var (other, _) = AudioClipProcessingTests.Create(); ProjectFile.Save(project, other);
            inspection = ProjectRecovery.Inspect(project, recovery); Assert.Null(inspection.Recovery); Assert.NotNull(inspection.RecoveryError);
            Assert.Throws<InvalidOperationException>(() => ProjectRecovery.Restore(inspection));
            Assert.Throws<ArgumentException>(() => new ProjectAutosaveHost(doc, project, project));
            File.WriteAllText(recovery, "broken recovery");
            inspection = ProjectRecovery.Inspect(project, recovery);
            Assert.NotNull(inspection.Saved); Assert.NotNull(inspection.RecoveryError); Assert.Null(inspection.Recovery);
            ProjectRecovery.Discard(recovery); Assert.False(File.Exists(recovery));
        }
        finally { Directory.Delete(root, true); }
    }
}
