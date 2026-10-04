using System.Diagnostics;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.PlatformAudio;

[Collection("FlowScripts")]
public class ProjectGeneratorHostTests
{
    [Fact]
    public async Task GrantedSampleExampleBuildsRoutesAndReopensWithIdenticalPlayback()
    {
        string root = Path.Combine(Path.GetTempPath(), "flow-sample-example-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var (doc, _) = Create();
            await ProjectBounceFile.ExportAsync(doc.Snapshot, root, Path.Combine(root, "sample.wav"), 8000, 256);
            var imported = AudioAssetFiles.Inspect(root, "sample.wav", Guid.NewGuid());
            doc.Edit("Import sample", p => new(p.Arrangement, p.Context, p.Sources.Values, p.Routing,
                [imported.Reference], p.Automation, p.RenderSettings));
            string code = File.ReadAllText(Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot,
                "examples/instruments/project-sample.flow"))
                .Replace("11111111-1111-1111-1111-111111111111", imported.Reference.Id.ToString());
            var descriptor = new GeneratorDescriptor(1, Guid.NewGuid(), "generate");
            await using var session = new ProjectPlaybackSession(doc, root, 8000, 256);
            await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Interpreter));
            host.RequestBuildWithAssets(descriptor, code, [imported.Reference.Id]); Finish(host, session);
            Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
            var bindings = doc.Snapshot.Sources[descriptor.SourceId].Bindings;
            var track = Guid.NewGuid();
            doc.Edit("Route sample example", p => new(new(p.Arrangement.Id, p.Context.Tempo, p.Context.Meter,
                [new(Guid.NewGuid(), track, descriptor.SourceId, "main", 0, 0, 4)]), p.Context, p.Sources.Values,
                new([new(track, "Sampler", bindings.Single(b => b.Output.Role == GeneratedRole.Instrument).Id)],
                    bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id), p.Assets, p.Automation, p.RenderSettings));
            string saved = Path.Combine(root, "example.flowproject");
            ProjectFile.Save(saved, doc);
            var reopened = ProjectFile.Load(saved);
            Assert.Equal(new[] { imported.Reference.Id }, reopened.Snapshot.Sources[descriptor.SourceId].AssetGrants);
            var original = ProjectCompiler.Prepare(doc.Snapshot, 8000, 256).Playback;
            var restored = ProjectCompiler.Prepare(reopened.Snapshot, 8000, 256).Playback;
            float[] expected = new float[512], actual = new float[512];
            Assert.Equal(original.TotalFrames, restored.TotalFrames);
            bool audible = false;
            for (long frame = 0; frame < original.TotalFrames; frame += 256)
            {
                original.Read(expected); restored.Read(actual);
                audible |= actual.Any(value => Math.Abs(value) > .001f);
                Assert.Equal(expected, actual);
            }
            Assert.True(audible);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SelectedAssetIsHashCheckedAndEditsInvalidateCapturedBuild()
    {
        string root = Path.Combine(Path.GetTempPath(), "flow-generator-assets-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var (doc, _) = Create();
            string path = Path.Combine(root, "sample.wav");
            await ProjectBounceFile.ExportAsync(doc.Snapshot, root, path, 8000, 16);
            var imported = AudioAssetFiles.Inspect(root, "sample.wav", Guid.NewGuid());
            doc.Edit("Import sample", p => new(p.Arrangement, p.Context, p.Sources.Values, p.Routing, [imported.Reference], p.Automation, p.RenderSettings));
            await using var session = new ProjectPlaybackSession(doc, root, 8000, 16);
            await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Interpreter));
            var descriptor = new GeneratorDescriptor(1, Guid.NewGuid(), "generate");
            string code = $$"""
                use "@flowDaw"
                proc generate (Dict<String, Double>: context)
                    (dawResult "sample" (dawAssetSample "{{imported.Reference.Id}}"))
                end proc
                """;
            host.RequestBuildWithAssets(descriptor, code, [imported.Reference.Id]); Finish(host, session);
            Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
            var before = doc.Snapshot;
            Assert.Equal(new[] { imported.Reference.Id }, before.Sources[descriptor.SourceId].AssetGrants);
            var reopened = ProjectJson.Deserialize(ProjectJson.Serialize(before));
            Assert.Equal(before.Sources[descriptor.SourceId].AssetGrants, reopened.Sources[descriptor.SourceId].AssetGrants);
            using var engine = new FlowLang.Core.FlowEngine(new FlowLang.Core.EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
            var exported = engine.Evaluate(FlowProjectExporter.Export(before));
            Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
            Assert.Equal(before.Sources[descriptor.SourceId].AssetGrants, exported.LastValue!.As<ProjectSnapshot>().Sources[descriptor.SourceId].AssetGrants);
            var authored = engine.Evaluate($"(dawAssetGrants project0 \"{descriptor.SourceId}\" (list \"{imported.Reference.Id}\"))");
            Assert.True(authored.Succeeded, string.Join("\n", authored.Errors));
            Assert.Equal(imported.Audio.Frames, before.Sources[descriptor.SourceId].Result.AudioLayers.Single().Asset.Frames);
            host.RequestBuild(descriptor, code); Finish(host, session);
            Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
            before = doc.Snapshot;
            host.RequestBuild(descriptor, code);
            ProjectRenderCommands.Set(doc, new(22050, 128));
            Assert.True(doc.History.Undo()); // ABA still invalidates the captured request.
            Finish(host, session); Assert.False(host.LastCompletion!.Accepted);
            Assert.Equal(JobStatus.Superseded, host.LastCompletion.Status); Assert.Same(before, doc.Snapshot);
            File.WriteAllText(path, "changed");
            host.RequestBuild(descriptor, code); Finish(host, session);
            Assert.Equal(JobStatus.Failed, host.LastCompletion!.Status); Assert.Same(before, doc.Snapshot);
            ProjectAssetGrantCommands.Set(doc, descriptor.SourceId, []);
            Assert.Empty(doc.Snapshot.Sources[descriptor.SourceId].AssetGrants);
            Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
            Assert.True(doc.History.Redo());
            Assert.Throws<ArgumentException>(() => ProjectAssetGrantCommands.Set(doc, descriptor.SourceId, [Guid.NewGuid()]));
        }
        finally { Directory.Delete(root, true); }
    }
    private static string Interpreter => Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll");
    private const string Code = """
        use "@flowDaw"
        proc generate (Dict<String, Double>: context)
            section phrase { Sequence melody = | A4q C5q E5h | }
            Song score = [phrase]
            (dawCombine (dawResult "main" score) (dawResult "mix" (dawInput "input" 0)))
        end proc
        """;
    private static (ProjectDocument Document, GeneratorDescriptor Descriptor) Create()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var descriptor = new GeneratorDescriptor(1, Guid.NewGuid(), "generate");
        var ticket = doc.BeginBuild(descriptor, Code);
        var built = FlowDawGenerator.Build(new(descriptor, ticket.Revision, Code, ticket.Context, TimeSpan.FromSeconds(20)));
        Assert.True(built.Status == JobStatus.Succeeded, built.Error); Assert.True(doc.Accept(ticket, built.Value!));
        var graph = doc.Snapshot.Sources[descriptor.SourceId].Bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        var track = Guid.NewGuid();
        doc.Edit("Place", p => new(new(p.Arrangement.Id, tempo, meter,
            [new(Guid.NewGuid(), track, descriptor.SourceId, "main", 0, 0, 4)]), p.Context, p.Sources.Values, new([new(track, "Track")], graph)));
        return (doc, descriptor);
    }
    private static void Finish(ProjectGeneratorHost host, ProjectPlaybackSession session)
    {
        Assert.True(SpinWait.SpinUntil(() =>
        {
            host.Poll(); return !host.IsBuilding && !session.IsPreparing;
        }, TimeSpan.FromSeconds(30)));
    }
    [Fact]
    public async Task IsolatedEditAcceptsOneActionPreparesPlaybackAndSurvivesSaveUndoAndFailure()
    {
        var (doc, descriptor) = Create(); int history = doc.History.UndoCount;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Interpreter));
        string changed = Code.Replace("A4q", "B4q"); host.RequestBuild(descriptor, changed); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        Assert.Equal(history + 1, doc.History.UndoCount); Assert.Equal(changed, doc.Snapshot.Sources[descriptor.SourceId].Code);
        Assert.Same(doc.Snapshot, session.Playback.ActiveSnapshot);
        var accepted = doc.Snapshot;
        string path = Path.Combine(Path.GetTempPath(), $"flow-host-{Guid.NewGuid():N}.flowproject");
        try
        {
            ProjectFile.Save(path, doc); var reopened = ProjectFile.Load(path);
            var expected = ProjectCompiler.Prepare(reopened.Snapshot, 8000, 16).Playback;
            var live = new float[32]; var offline = new float[32];
            Assert.True(session.Playback.Queue.TryPlay()); session.Playback.Queue.Read(live); expected.Read(offline);
            Assert.Equal(offline, live); Assert.Contains(live, x => x != 0);
        }
        finally { File.Delete(path); }
        host.RequestBuild(descriptor, "invalid Flow source"); Finish(host, session);
        Assert.Equal(JobStatus.Failed, host.LastCompletion!.Status); Assert.False(host.LastCompletion.Accepted);
        Assert.Same(accepted, doc.Snapshot); Assert.Equal(history + 1, doc.History.UndoCount);
        Assert.True(doc.History.Undo()); session.RequestPreparation(); Finish(host, session);
        Assert.Equal(Code, doc.Snapshot.Sources[descriptor.SourceId].Code);
        Assert.Same(doc.Snapshot, session.Playback.ActiveSnapshot);
    }
    [Fact]
    public async Task RapidSameSourceEditsCancelOldProcessAndCoalescePendingWork()
    {
        var (doc, descriptor) = Create(); int starts = 0; int history = doc.History.UndoCount;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        var worker = new ProcessGeneratorWorker(() => Interlocked.Increment(ref starts) == 1
            ? new ProcessStartInfo("sleep") { ArgumentList = { "60" } }
            : new ProcessStartInfo("dotnet") { ArgumentList = { Interpreter, "--daw-worker" } });
        await using var host = new ProjectGeneratorHost(session, worker);
        host.RequestBuild(descriptor, Code);
        Assert.True(SpinWait.SpinUntil(() => worker.ProcessId.HasValue, TimeSpan.FromSeconds(5)));
        for (int i = 0; i < 100; i++) host.RequestBuild(descriptor, Code.Replace("A4q", "B4q"));
        Assert.Equal(1, host.PendingCount); Finish(host, session);
        Assert.Equal(2, starts); Assert.Equal(1, worker.Kills); Assert.Null(worker.ProcessId);
        Assert.True(host.LastCompletion!.Accepted); Assert.Equal(history + 1, doc.History.UndoCount);
        Assert.Same(doc.Snapshot, session.Playback.ActiveSnapshot);
    }
    [Fact]
    public async Task UndoWhileBuildRunsPreventsLateAcceptance()
    {
        var (doc, descriptor) = Create();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Interpreter));
        host.RequestBuild(descriptor, Code.Replace("A4q", "B4q"));
        Assert.True(doc.History.Undo()); var undone = doc.Snapshot;
        session.RequestPreparation(); Finish(host, session);
        Assert.Equal(JobStatus.Superseded, host.LastCompletion!.Status);
        Assert.False(host.LastCompletion.Accepted); Assert.Same(undone, doc.Snapshot);
    }
    [Fact]
    public void DiscardingDraftCannotInvalidateNewerBuild()
    {
        var (doc, descriptor) = Create();
        var old = doc.BeginBuild(descriptor, Code); var current = doc.BeginBuild(descriptor, Code);
        Assert.False(doc.DiscardBuild(old)); Assert.True(doc.DiscardBuild(current));
        Assert.False(doc.DiscardBuild(current));
    }
    [Fact]
    public async Task PendingSourceBudgetAndShutdownBoundWorkWithoutDocumentChanges()
    {
        var (doc, descriptor) = Create(); var initial = doc.Snapshot;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 16);
        var worker = new ProcessGeneratorWorker(() => new ProcessStartInfo("sleep") { ArgumentList = { "60" } });
        var host = new ProjectGeneratorHost(session, worker);
        try
        {
            host.RequestBuild(descriptor, Code);
            for (int i = 0; i < ProjectGeneratorHost.MaxPendingSources; i++) host.RequestBuild(new(1, Guid.NewGuid(), "generate"), Code);
            Assert.Throws<InvalidOperationException>(() => host.RequestBuild(new(1, Guid.NewGuid(), "generate"), Code));
            await host.DisposeAsync(); Assert.Null(worker.ProcessId); Assert.False(host.IsBuilding);
            Assert.Same(initial, doc.Snapshot);
            Assert.Throws<ObjectDisposedException>(() => host.Poll());
            Assert.Throws<ObjectDisposedException>(() => host.RequestBuild(descriptor, Code));
        }
        finally { await host.DisposeAsync(); }
    }
}
