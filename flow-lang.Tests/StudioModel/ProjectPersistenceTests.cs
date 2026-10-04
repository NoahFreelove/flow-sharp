using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using System.Text.Json.Nodes;
using Xunit;
namespace FlowLang.Tests.StudioModel;

public class ProjectPersistenceTests
{
    private static ProjectDocument Create()
    {
        var tempo = new ProjectTempoMap([new(0, 120), new(8, 90)]);
        var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(7, 42, tempo, meter)));
        var id = Guid.NewGuid(); var track = Guid.NewGuid();
        var ticket = doc.BeginBuild(new(1, id, "generate"), "do not execute this on load");
        Assert.True(doc.Accept(ticket, new(id, ticket.Revision, ticket.Context, [],
            [new("main", new PcmAsset([0.5f, -0.5f, 0.25f, -0.25f], 8000))],
            [new("mix", AudioGraphDefinition.Input("input").Then("gain", "flow.gain", new Dictionary<string, double> { ["gain"] = 0.5 }))])));
        var bindings = doc.Snapshot.Sources[id].Bindings;
        var audio = bindings.Single(b => b.Output.Role == GeneratedRole.Audio).Id;
        var graph = bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        doc.Edit("Route", s => new(new(s.Arrangement.Id, tempo, meter, audioClips:
            [new(Guid.NewGuid(), track, audio, 0, 0, 2, 8000)]), s.Context, s.Sources.Values,
            new([new(track, "Generated audio")], graph)));
        return doc;
    }
    private static float[] Render(ProjectSnapshot snapshot)
    {
        var playback = ProjectCompiler.Prepare(snapshot, 8000, 8).Playback;
        var samples = new float[4]; playback.Read(samples); return samples;
    }
    [Fact]
    public void SaveReopenRestoresBindingsCodeRoutingAndExactPlaybackWithoutExecution()
    {
        var doc = Create(); var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".flowproject");
        try
        {
            Assert.True(doc.History.IsDirty); ProjectFile.Save(path, doc); Assert.False(doc.History.IsDirty);
            var loaded = ProjectFile.Load(path);
            Assert.Equal(ProjectJson.Serialize(doc.Snapshot), ProjectJson.Serialize(loaded.Snapshot));
            Assert.Equal(Render(doc.Snapshot), Render(loaded.Snapshot));
            Assert.Equal(new float[] { .25f, -.25f, .125f, -.125f }, Render(loaded.Snapshot));
            Assert.False(loaded.History.IsDirty); Assert.Equal(0, loaded.History.UndoCount);
            var source = loaded.Snapshot.Sources.Values.Single();
            Assert.True(loaded.BeginBuild(source.Descriptor, source.Code).Revision > source.Result.SourceRevision);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void CorruptBindingAndFutureSchemaAreRejected()
    {
        var json = ProjectJson.Serialize(Create().Snapshot);
        var node = JsonNode.Parse(json)!; node["Version"] = 999;
        Assert.ThrowsAny<Exception>(() => ProjectJson.Deserialize(node.ToJsonString()));
        node = JsonNode.Parse(json)!;
        node["Sources"]![0]!["Bindings"]![0]!["Available"] = false;
        Assert.ThrowsAny<Exception>(() => ProjectJson.Deserialize(node.ToJsonString()));
        node = JsonNode.Parse(json)!;
        node["Sources"]![0]!["Bindings"]![1]!["Id"] = node["Sources"]![0]!["Bindings"]![0]!["Id"]!.GetValue<string>();
        Assert.ThrowsAny<Exception>(() => ProjectJson.Deserialize(node.ToJsonString()));
    }
    [Fact]
    public void FailedSaveKeepsDirtyStateAndOriginalDestination()
    {
        var doc = Create(); var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            Assert.ThrowsAny<IOException>(() => ProjectFile.Save(directory, doc));
            Assert.True(doc.History.IsDirty); Assert.True(Directory.Exists(directory));
        }
        finally { Directory.Delete(directory); }
    }
    [Fact]
    public void RegenerationRetainsPersistedRoutingAndOldBuildContext()
    {
        var doc = Create(); var routing = doc.Snapshot.Routing;
        var old = doc.Snapshot.Sources.Values.Single();
        doc.Edit("Change seed", s => new(s.Arrangement, new(8, 90, s.Arrangement.Tempo, s.Arrangement.Meter), s.Sources.Values, s.Routing));
        var reopened = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.Equal(42, reopened.Sources.Values.Single().Result.Context.Seed);
        Assert.Equal(90, reopened.Context.Seed);
        var t = doc.BeginBuild(old.Descriptor, "updated");
        Assert.True(doc.Accept(t, new(t.Descriptor.SourceId, t.Revision, t.Context, [], old.Result.AudioLayers, old.Result.GraphLayers)));
        Assert.Same(routing, doc.Snapshot.Routing);
    }
    [Fact]
    public void RecoverySnapshotDoesNotClearWorkingDirtyState()
    {
        var doc = Create(); var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".recovery");
        try
        {
            ProjectFile.SaveRecovery(path, doc.Snapshot);
            Assert.True(doc.History.IsDirty);
            Assert.Equal(Render(doc.Snapshot), Render(ProjectFile.Load(path).Snapshot));
        }
        finally { File.Delete(path); }
    }

}
