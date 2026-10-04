using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.StudioModel;
[Collection("FlowScripts")]
public class PluginParameterTests
{
    private static PluginPackage Example() => PluginPackage.Deserialize(File.ReadAllText(Path.Combine(
        FlowLang.Tests.Characterization.Snapshot.RepoRoot, "examples", "plugins", "composed-soft-clip.flowplugin")));
    private static (ProjectDocument Document, Guid Source) Create(PluginPackage? plugin = null)
    {
        plugin ??= Example(); var (doc, _) = ProjectPlaybackCoordinatorTests.Create(); var id = Guid.NewGuid();
        Accept(doc, id, plugin); return (doc, id);
    }
    private static void Accept(ProjectDocument doc, Guid id, PluginPackage plugin)
    {
        var ticket = doc.BeginBuild(new(1, id, plugin.Manifest.Builder), plugin.Source);
        var built = FlowDawGenerator.Build(new(ticket.Descriptor, ticket.Revision, ticket.Code, ticket.Context, TimeSpan.FromSeconds(20), plugin), TestContext.Current.CancellationToken);
        Assert.True(built.Status == JobStatus.Succeeded, built.Error);
        plugin.ValidateEffect(built.Value!, 8000, 128);
        Assert.True(doc.Accept(ticket, built.Value!, "Plugin", p => new(p.Arrangement, p.Context, p.Sources.Values,
            new(p.Routing.Tracks, p.Sources[id].Bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id), p.Assets, p.Automation), plugin: plugin));
    }
    private static float[] Render(ProjectSnapshot p)
    { var block = new float[256]; ProjectCompiler.Prepare(p, 8000, 128).Playback.Read(block); return block; }
    private static PluginPackage Revision(PluginPackage original, Action<JsonNode> edit, string? code = null,
        IEnumerable<PluginParameterTarget>? targets = null)
    {
        code ??= original.Source;
        var json = JsonNode.Parse(original.Manifest.Serialize())!;
        edit(json); json["Version"] = "reload-test";
        json["SourceSha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        return new(PluginManifest.Parse(json.ToJsonString()), code, original.OutputLayer, targets ?? original.Targets, original.Dependencies);
    }
    [Theory]
    [InlineData("unit")]
    [InlineData("range")]
    [InlineData("scale")]
    [InlineData("schema")]
    [InlineData("identity")]
    public void IncompatiblePluginReloadDoesNotReplaceAcceptedValuesHistoryOrSound(string change)
    {
        var (doc, id) = Create(); ProjectPluginCommands.SetParameter(doc, id, "drive", 4);
        var before = doc.Snapshot; int history = doc.History.UndoCount; var audio = Render(before);
        var next = Revision(before.Sources[id].Plugin!, json =>
        {
            var drive = json["Parameters"]!.AsArray().Single(p => p!["Id"]!.GetValue<string>() == "drive")!;
            if (change == "unit") drive["Unit"] = "dB";
            if (change == "range") drive["Maximum"] = 64;
            if (change == "scale") drive["Scale"] = "Logarithmic";
            if (change == "schema") json["StateSchema"] = "changed";
            if (change == "identity") json["Id"] = "different.plugin";
        });
        Assert.Throws<InvalidOperationException>(() => Accept(doc, id, next));
        Assert.Same(before, doc.Snapshot); Assert.Equal(history, doc.History.UndoCount);
        Assert.Equal(audio, Render(doc.Snapshot));
    }
    [Fact]
    public void CompatibleReloadPreservesImplicitDefaultsAndSupportsExplicitResetToNewDefault()
    {
        var (doc, id) = Create(); var before = doc.Snapshot; var expected = Render(before);
        var original = before.Sources[id].Plugin!;
        var next = Revision(original, json =>
            json["Parameters"]!.AsArray().Single(p => p!["Id"]!.GetValue<string>() == "level")!["Default"] = 1.5,
            original.Source.Replace("(dawValue \"level\" 1.0)", "(dawValue \"level\" 1.5)"));
        Accept(doc, id, next); var accepted = doc.Snapshot;
        Assert.Equal(1, accepted.Sources[id].PluginValues["level"]);
        Assert.Equal(before.Sources[id].Bindings, accepted.Sources[id].Bindings);
        Assert.Equal(expected, Render(accepted));
        Assert.Equal(expected, Render(ProjectJson.Deserialize(ProjectJson.Serialize(accepted))));
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(accepted, doc.Snapshot);
        ProjectPluginCommands.ResetParameter(doc, id, "level");
        Assert.Equal(expected.Select(x => x * 1.5f), Render(doc.Snapshot));
    }
    [Fact]
    public void PublicAutomationFollowsReloadedTargetsButDirectAutomationCannotBeSilentlyRetargeted()
    {
        var (doc, id) = Create(); var original = doc.Snapshot.Sources[id].Plugin!;
        var next = Revision(original, _ => { }, original.Source.Replace("\"level\"", "\"volume\""),
            original.Targets.Select(t => t.NodeId == "level" ? t with { NodeId = "volume" } : t));
        Guid binding = doc.Snapshot.Routing.GraphBinding!.Value;
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), binding, "", "level", [new(0, .25)],
            targetKind: AutomationTargetKind.PluginParameter));
        var before = Render(doc.Snapshot);
        Accept(doc, id, next); Assert.Equal(before, Render(doc.Snapshot));
        var (direct, other) = Create();
        ProjectAutomationCommands.Set(direct, new(Guid.NewGuid(), direct.Snapshot.Routing.GraphBinding!.Value,
            "level", "value", [new(0, .25)]));
        var unchanged = direct.Snapshot;
        Assert.Throws<InvalidOperationException>(() => Accept(direct, other, next));
        Assert.Same(unchanged, direct.Snapshot);
    }
    [Fact]
    public void PackagedEffectCompositionResolvesSavedValuesAndBindingIdentity()
    {
        var (doc, id) = Create(); ProjectPluginCommands.SetParameter(doc, id, "level", .25);
        var effect = BoundEffect.FromSource(doc.Snapshot.Sources[id], 8000, 128);
        Assert.Equal(doc.Snapshot.Routing.GraphBinding, effect.Binding);
        var composed = EffectChainCompiler.Compose(Flow.Audio.Graph.AudioGraphDefinition.Input("input"),
            new Dictionary<int, IReadOnlyList<BoundEffect>>(), [effect]);
        var prepared = new Flow.Audio.Graph.PreparedAudioGraph(composed.Graph, 8000, 128);
        var output = new float[256]; prepared.Process(Enumerable.Repeat(.5f, 256).ToArray(), output);
        Assert.Equal(Render(doc.Snapshot), output);
    }
    [Fact]
    public void PersistedEffectChainPreservesValuesOrderBypassAutomationAndFlowExport()
    {
        var (doc, source) = Create(); ProjectRenderCommands.Set(doc, new(22050, 128)); var initial = doc.Snapshot;
        Guid first = ProjectPluginInstanceCommands.Duplicate(doc, source);
        Guid second = ProjectPluginInstanceCommands.Duplicate(doc, source);
        ProjectPluginCommands.SetParameter(doc, first, "level", .25);
        ProjectPluginCommands.SetParameter(doc, second, "drive", 4);
        Guid a = doc.Snapshot.Sources[first].Bindings.Single().Id, b = doc.Snapshot.Sources[second].Bindings.Single().Id;
        var before = doc.Snapshot; int history = doc.History.UndoCount;
        ProjectEffectCommands.Set(doc, [new(a), new(b)]); var applied = doc.Snapshot; var expected = Render(applied);
        Assert.Equal(initial.RenderSettings, applied.RenderSettings);
        Assert.Equal(history + 1, doc.History.UndoCount);
        ProjectEffectCommands.Set(doc, [new(a), new(b)]); Assert.Same(applied, doc.Snapshot);
        Assert.Throws<InvalidOperationException>(() => ProjectPluginInstanceCommands.Remove(doc, first));
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(applied, doc.Snapshot);
        Assert.Equal(expected, Render(ProjectJson.Deserialize(ProjectJson.Serialize(applied))));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        string exported = FlowProjectExporter.Export(applied); Assert.Contains("dawInsertEffect", exported);
        var restored = engine.Evaluate(exported); Assert.True(restored.Succeeded, string.Join("\n", restored.Errors));
        var rebuilt = restored.LastValue!.As<ProjectSnapshot>();
        Assert.Equal(applied.Routing.Effects, rebuilt.Routing.Effects); Assert.Equal(expected, Render(rebuilt));
        ProjectEffectCommands.Set(doc, [new(b), new(a)]); Assert.NotEqual(expected, Render(doc.Snapshot));
        ProjectEffectCommands.Set(doc, [new(a, Bypassed: true), new(b, Bypassed: true)]);
        Assert.Equal(Render(initial), Render(doc.Snapshot));
        ProjectEffectCommands.Set(doc, [new(a)]);
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), a, "", "level", [new(0, 0)], targetKind: AutomationTargetKind.PluginParameter));
        Assert.All(Render(doc.Snapshot), sample => Assert.Equal(0, sample));
        var track = doc.Snapshot.Routing.Tracks[0].Id;
        ProjectTrackCommands.Rename(doc, track, "Renamed"); Assert.Single(doc.Snapshot.Routing.Effects);
        ProjectTrackCommands.Reorder(doc, [track]); Assert.Single(doc.Snapshot.Routing.Effects);
    }
    [Fact]
    public void TrackInsertPrecedesMixerAndSupportsMappedPublicPreview()
    {
        var (doc, source) = Create(); Guid clone = ProjectPluginInstanceCommands.Duplicate(doc, source);
        Guid binding = doc.Snapshot.Sources[clone].Bindings.Single().Id, track = doc.Snapshot.Routing.Tracks[0].Id;
        ProjectEffectCommands.Set(doc, [new(binding, track)]);
        var playback = new ProjectPlaybackCoordinator(doc, "/tmp", 8000, 128);
        Assert.True(playback.TryBeginPluginParameterPreview(clone, "level", out var preview));
        Assert.True(playback.TryUpdateParameterPreview(preview!, 0)); playback.Queue.TryPlay();
        var block = new float[256]; playback.Queue.Read(block); playback.Queue.Read(block);
        // The downstream master plugin adds its own 0.1 bias to the silenced track.
        Assert.All(block, sample => Assert.Equal((float)Math.Tanh(.1), sample, 6));
        playback.CancelParameterPreview(preview!); playback.Queue.Read(block); playback.Queue.Read(block);
        Assert.Equal(Render(doc.Snapshot), block);
        ProjectEffectCommands.Set(doc, [new(binding, track, true)]);
        var bypassed = new ProjectPlaybackCoordinator(doc, "/tmp", 8000, 128);
        Assert.False(bypassed.TryBeginPluginParameterPreview(clone, "level", out _));
        ProjectTrackCommands.Remove(doc, [track]); Assert.Empty(doc.Snapshot.Routing.Effects);
        Assert.True(doc.History.Undo()); Assert.Single(doc.Snapshot.Routing.Effects);
    }
    [Fact]
    public void InvalidEffectRoutingIsAtomicAndSchemaCannotSilentlyDropInserts()
    {
        var (doc, source) = Create(); Guid clone = ProjectPluginInstanceCommands.Duplicate(doc, source);
        Guid binding = doc.Snapshot.Sources[clone].Bindings.Single().Id;
        var before = doc.Snapshot;
        Assert.Throws<ArgumentException>(() => ProjectEffectCommands.Set(doc, [new(binding), new(binding)]));
        Assert.Throws<ArgumentException>(() => ProjectEffectCommands.Set(doc, [new(binding, Guid.NewGuid())]));
        Assert.Throws<ArgumentException>(() => ProjectEffectCommands.Set(doc, [new(Guid.NewGuid())]));
        Assert.Same(before, doc.Snapshot);
        ProjectEffectCommands.Set(doc, [new(binding)]);
        var wire = JsonNode.Parse(ProjectJson.Serialize(doc.Snapshot))!; wire["Version"] = 10;
        Assert.Throws<System.Text.Json.JsonException>(() => ProjectJson.Deserialize(wire.ToJsonString()));
        wire["Version"] = 11; wire.AsObject().Remove("Effects");
        Assert.Throws<System.Text.Json.JsonException>(() => ProjectJson.Deserialize(wire.ToJsonString()));
    }
    [Fact]
    public void MissingSavedEffectRetainsLastGoodPlaybackAndAutomatedInsertRejectsPreview()
    {
        var (doc, source) = Create(); Guid clone = ProjectPluginInstanceCommands.Duplicate(doc, source);
        Guid binding = doc.Snapshot.Sources[clone].Bindings.Single().Id;
        ProjectEffectCommands.Set(doc, [new(binding)]);
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), binding, "", "level", [new(0, .25)],
            targetKind: AutomationTargetKind.PluginParameter));
        var expected = Render(doc.Snapshot);
        var playback = new ProjectPlaybackCoordinator(doc, "/tmp", 8000, 128);
        Assert.Throws<InvalidOperationException>(() => playback.TryBeginPluginParameterPreview(clone, "level", out _));
        doc.Edit("Unresolved insert", p => new(p.Arrangement, p.Context, p.Sources.Values,
            new(p.Routing.Tracks, p.Routing.GraphBinding, [new(Guid.NewGuid())]), p.Assets, p.Automation));
        // Loading keeps repairable references; preparation refuses to publish them.
        var reopened = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot)); Assert.Single(reopened.Routing.Effects);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var restored = engine.Evaluate(FlowProjectExporter.Export(reopened));
        Assert.True(restored.Succeeded, string.Join("\n", restored.Errors));
        Assert.Equal(reopened.Routing.Effects, restored.LastValue!.As<ProjectSnapshot>().Routing.Effects);
        var request = playback.BeginPreparation();
        var error = Assert.Throws<InvalidOperationException>(() => ProjectPlaybackCoordinator.Prepare(request, "/tmp"));
        Assert.True(playback.Fail(request, error.Message)); Assert.False(playback.TryPublish());
        Assert.True(playback.Queue.TryPlay()); var block = new float[256]; playback.Queue.Read(block);
        Assert.Equal(expected, block);
    }
    [Fact]
    public void DuplicateOwnsValuesBindingsAndAutomationWithStableUndoRedoIdentity()
    {
        var (doc, id) = Create(); ProjectPluginCommands.SetParameter(doc, id, "drive", 4);
        var binding = doc.Snapshot.Routing.GraphBinding!.Value;
        var lane = new ProjectAutomationLane(Guid.NewGuid(), binding, "", "level", [new(0, .25)],
            targetKind: AutomationTargetKind.PluginParameter);
        ProjectAutomationCommands.Set(doc, lane);
        var before = doc.Snapshot; int history = doc.History.UndoCount;
        Guid clone = ProjectPluginInstanceCommands.Duplicate(doc, id);
        var after = doc.Snapshot; var copied = after.Sources[clone];
        Assert.Equal(history + 1, doc.History.UndoCount);
        Assert.Same(before.Sources[id], after.Sources[id]);
        Assert.Equal(clone, copied.Result.SourceId);
        Assert.Equal(4, copied.PluginValues["drive"]);
        Assert.DoesNotContain(copied.Bindings.Single().Id, before.Sources[id].Bindings.Select(b => b.Id));
        var clonedLane = after.Automation.Single(l => l.Id != lane.Id);
        Assert.Equal(copied.Bindings.Single().Id, clonedLane.GraphBinding);
        Assert.Equal(lane.Points, clonedLane.Points); Assert.Equal(lane.TargetKind, clonedLane.TargetKind);
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(after, doc.Snapshot);
        ProjectPluginCommands.SetParameter(doc, clone, "drive", 2);
        Assert.Equal(4, doc.Snapshot.Sources[id].PluginValues["drive"]);
        Assert.Equal(2, doc.Snapshot.Sources[clone].PluginValues["drive"]);
        var acceptedCopy = doc.Snapshot.Sources[clone];
        Accept(doc, id, doc.Snapshot.Sources[id].Plugin!);
        Assert.Same(acceptedCopy, doc.Snapshot.Sources[clone]);
        var reopened = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.Equal(copied.Bindings, reopened.Sources[clone].Bindings);
        Assert.Equal(2, reopened.Sources[clone].PluginValues["drive"]);
        Assert.Equal(clonedLane.Id, reopened.Automation.Single(l => l.GraphBinding == clonedLane.GraphBinding).Id);
        // Route the copied effect through the same input graph; automation stays bound to its copy.
        var routed = new ProjectSnapshot(reopened.Arrangement, reopened.Context, reopened.Sources.Values,
            new(reopened.Routing.Tracks, copied.Bindings.Single().Id), reopened.Assets, reopened.Automation);
        var expected = Render(routed);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var export = engine.Evaluate(FlowProjectExporter.Export(routed));
        Assert.True(export.Succeeded, string.Join("\n", export.Errors));
        Assert.Equal(expected, Render(export.LastValue!.As<ProjectSnapshot>()));
        Guid staticClone = ProjectPluginInstanceCommands.Duplicate(doc, id, copyAutomation: false);
        Assert.DoesNotContain(doc.Snapshot.Automation, l => doc.Snapshot.Sources[staticClone].Bindings.Any(b => b.Id == l.GraphBinding));
    }
    [Fact]
    public void InvalidInstanceAssignmentDoesNotMutateProject()
    {
        var (doc, id) = Create(); var before = doc.Snapshot; int count = doc.History.UndoCount;
        Assert.Throws<ArgumentException>(() => ProjectPluginInstanceCommands.Duplicate(doc, id, before.Routing.Tracks[0].Id));
        Assert.Same(before, doc.Snapshot); Assert.Equal(count, doc.History.UndoCount);
    }
    [Fact]
    public void RemovingUnusedInstanceRemovesItsAutomationAndInvalidatesPendingBuild()
    {
        var (doc, id) = Create();
        var binding = doc.Snapshot.Routing.GraphBinding!.Value;
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), binding, "", "level", [new(0, .25)],
            targetKind: AutomationTargetKind.PluginParameter));
        var before = doc.Snapshot;
        Assert.Throws<InvalidOperationException>(() => ProjectPluginInstanceCommands.Remove(doc, id));
        Assert.Same(before, doc.Snapshot);
        Guid clone = ProjectPluginInstanceCommands.Duplicate(doc, id);
        var source = doc.Snapshot.Sources[clone]; var cloned = doc.Snapshot;
        var pending = doc.BeginBuild(source.Descriptor, source.Code);
        var result = new GeneratedSourceOutput(clone, pending.Revision, pending.Context, [],
            graphLayers: source.Result.GraphLayers);
        ProjectPluginInstanceCommands.Remove(doc, clone);
        Assert.False(doc.Snapshot.Sources.ContainsKey(clone)); Assert.Single(doc.Snapshot.Automation);
        Assert.True(doc.History.Undo()); Assert.Same(cloned, doc.Snapshot);
        Assert.False(doc.Accept(pending, result, "Stale build", p => p, plugin: source.Plugin));
        Assert.True(doc.History.Redo()); Assert.False(doc.Snapshot.Sources.ContainsKey(clone));
    }
    [Fact]
    public void PresetTransfersValuesBetweenInstancesAndRoundTripsSoundWithOneUndo()
    {
        var (doc, id) = Create();
        ProjectPluginCommands.SetParameter(doc, id, "level", .25);
        ProjectPluginCommands.SetParameter(doc, id, "drive", 4);
        var expected = Render(doc.Snapshot);
        var captured = PluginPreset.Capture("Quiet drive", doc.Snapshot.Sources[id]);
        var preset = PluginPreset.Deserialize(captured.Serialize());
        Assert.Equal(doc.Snapshot.Sources[id].Plugin!.Manifest.Parameters.Count, preset.Values.Count);
        var (other, otherId) = Create(); var before = other.Snapshot; int history = other.History.UndoCount;
        Assert.True(ProjectPluginCommands.ApplyPreset(other, otherId, preset));
        var applied = other.Snapshot;
        Assert.Equal(history + 1, other.History.UndoCount);
        Assert.Same(before.Sources[otherId].Result, applied.Sources[otherId].Result);
        Assert.Equal(expected, Render(applied));
        Assert.Equal(expected, Render(ProjectJson.Deserialize(ProjectJson.Serialize(applied))));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var restored = engine.Evaluate(FlowProjectExporter.Export(applied));
        Assert.True(restored.Succeeded, string.Join("\n", restored.Errors));
        Assert.Equal(expected, Render(restored.LastValue!.As<ProjectSnapshot>()));
        Assert.False(ProjectPluginCommands.ApplyPreset(other, otherId, preset));
        Assert.Equal(history + 1, other.History.UndoCount);
        Assert.True(other.History.Undo()); Assert.Same(before, other.Snapshot);
        Assert.True(other.History.Redo()); Assert.Same(applied, other.Snapshot);
        Assert.True(ProjectPluginCommands.ApplyPreset(other, otherId, PluginPreset.Capture("Default", before.Sources[otherId])));
        Assert.Empty(other.Snapshot.Sources[otherId].PluginValues);
        Assert.Equal(expected, Render(doc.Snapshot)); // Independent source instance.
    }
    [Fact]
    public void PresetRejectsAutomationConflictsWithoutPartiallyApplyingOtherValues()
    {
        var (doc, id) = Create(); var defaults = PluginPreset.Capture("Default", doc.Snapshot.Sources[id]);
        ProjectPluginCommands.SetParameter(doc, id, "level", .25);
        ProjectPluginCommands.SetParameter(doc, id, "drive", 4);
        var target = doc.Snapshot.Sources[id].Plugin!.Targets.First(t => t.ParameterId == "level");
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), doc.Snapshot.Routing.GraphBinding!.Value,
            target.NodeId, target.DeviceParameterId, [new(0, .5)]));
        var before = doc.Snapshot; int history = doc.History.UndoCount;
        Assert.Throws<InvalidOperationException>(() => ProjectPluginCommands.ApplyPreset(doc, id, defaults));
        Assert.Same(before, doc.Snapshot); Assert.Equal(history, doc.History.UndoCount);
        Assert.False(ProjectPluginCommands.ApplyPreset(doc, id, PluginPreset.Capture("Same", before.Sources[id])));
    }
    [Fact]
    public void PresetRejectsMalformedValuesAndDifferentPackageRevisions()
    {
        var (doc, id) = Create(); var before = doc.Snapshot;
        string json = PluginPreset.Capture("Saved", before.Sources[id]).Serialize();
        foreach (string change in new[] { "missing", "extra", "range", "revision" })
        {
            var wire = JsonNode.Parse(json)!;
            if (change == "missing") wire["Values"]!.AsObject().Remove("level");
            if (change == "extra") wire["Values"]!["extra"] = 1;
            if (change == "range") wire["Values"]!["level"] = -1;
            if (change == "revision") wire["PackageSha256"] = new string('0', 64);
            var invalid = PluginPreset.Deserialize(wire.ToJsonString());
            Assert.ThrowsAny<ArgumentException>(() => ProjectPluginCommands.ApplyPreset(doc, id, invalid));
            Assert.Same(before, doc.Snapshot);
        }
        Assert.Throws<ArgumentException>(() => PluginPreset.Deserialize(json.Replace("\"Version\":1", "\"Version\":1,\"Version\":1")));
        var changedManifest = JsonNode.Parse(Example().Manifest.Serialize())!; changedManifest["Version"] = "2";
        var package = Example();
        var revision = new PluginPackage(PluginManifest.Parse(changedManifest.ToJsonString()), package.Source,
            package.OutputLayer, package.Targets, package.Dependencies);
        Assert.Throws<ArgumentException>(() => PluginPreset.Deserialize(json).ValidateFor(revision));
    }
    [Fact]
    public void SavedValuesChangeSoundWithoutChangingDefinitionAndSurviveExportUndoAndReset()
    {
        var (doc, id) = Create(); var before = doc.Snapshot; var original = Render(before); int count = doc.History.UndoCount;
        ProjectPluginCommands.SetParameter(doc, id, "level", .25);
        var changed = doc.Snapshot; Assert.Equal(count + 1, doc.History.UndoCount);
        Assert.Same(before.Sources[id].Plugin, changed.Sources[id].Plugin); Assert.Same(before.Sources[id].Result, changed.Sources[id].Result);
        Assert.Equal(original.Select(x => x * .25f), Render(changed));
        Assert.Equal(Render(changed), Render(ProjectJson.Deserialize(ProjectJson.Serialize(changed))));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var restored = engine.Evaluate(FlowProjectExporter.Export(changed)); Assert.True(restored.Succeeded, string.Join("\n", restored.Errors));
        Assert.Equal(Render(changed), Render(restored.LastValue!.As<ProjectSnapshot>()));
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot); Assert.Equal(original, Render(doc.Snapshot));
        Assert.True(doc.History.Redo()); ProjectPluginCommands.ResetParameter(doc, id, "level"); Assert.Equal(original, Render(doc.Snapshot));
        Assert.Empty(doc.Snapshot.Sources[id].PluginValues);
    }
    [Fact]
    public void PublicParameterFansOutAtomicallyAndAutomationPreventsManualOverride()
    {
        const string code = "use \"@flowDaw\"\nproc build (Dict<String, Double>: context)\n    (dawResult \"effect\" (dawGain \"second\" (dawGain \"first\" (dawInput \"input\" 0) 1.0) 1.0))\nend proc";
        var json = JsonNode.Parse(Example().Manifest.Serialize())!;
        var level = json["Parameters"]!.AsArray().Single(p => p!["Id"]!.GetValue<string>() == "level")!.DeepClone();
        json["Parameters"] = new JsonArray(level); json["SourceSha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        var plugin = new PluginPackage(PluginManifest.Parse(json.ToJsonString()), code, "effect", [new("level", "first", "gain"), new("level", "second", "gain")]);
        var (doc, id) = Create(plugin); int history = doc.History.UndoCount;
        var playback = new ProjectPlaybackCoordinator(doc, "/tmp", 8000, 128);
        Assert.True(playback.TryBeginPluginParameterPreview(id, "level", out var preview));
        Assert.True(playback.TryUpdateParameterPreview(preview!, .5)); playback.Queue.TryPlay();
        var output = new float[256]; playback.Queue.Read(output); playback.Queue.Read(output);
        Assert.All(output, sample => Assert.Equal(.125f, sample));
        playback.CancelParameterPreview(preview!); playback.Queue.Read(output); playback.Queue.Read(output);
        Assert.All(output, sample => Assert.Equal(.5f, sample)); Assert.Equal(history, doc.History.UndoCount);
        ProjectPluginCommands.SetParameter(doc, id, "level", .5); Assert.Equal(history + 1, doc.History.UndoCount);
        Assert.All(Render(doc.Snapshot), sample => Assert.Equal(.125f, sample));
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), doc.Snapshot.Routing.GraphBinding!.Value, "second", "gain", [new(0, .5)]));
        var before = doc.Snapshot;
        Assert.Throws<InvalidOperationException>(() => ProjectPluginCommands.SetParameter(doc, id, "level", .25));
        Assert.Same(before, doc.Snapshot);
    }
    [Fact]
    public void StablePublicValuesSurviveRebuildAndInvalidEditsRemainAtomic()
    {
        var (doc, id) = Create(); ProjectPluginCommands.SetParameter(doc, id, "drive", 4);
        var before = doc.Snapshot;
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectPluginCommands.SetParameter(doc, id, "drive", 100));
        Assert.Throws<ArgumentException>(() => ProjectPluginCommands.SetParameter(doc, id, "missing", 1)); Assert.Same(before, doc.Snapshot);
        Accept(doc, id, doc.Snapshot.Sources[id].Plugin!);
        Assert.Equal(4, doc.Snapshot.Sources[id].PluginValues["drive"]); Assert.Equal(Render(before), Render(doc.Snapshot));
    }
    [Fact]
    public void PreviewCancelRestoresSavedInstanceValueRatherThanManifestDefault()
    {
        var (doc, id) = Create(); ProjectPluginCommands.SetParameter(doc, id, "level", .25);
        var playback = new ProjectPlaybackCoordinator(doc, "/tmp", 8000, 128);
        var expected = Render(doc.Snapshot); var output = new float[256];
        Assert.True(playback.TryBeginParameterPreview("level", "value", out var preview));
        Assert.True(playback.TryUpdateParameterPreview(preview!, 0)); playback.Queue.TryPlay();
        playback.Queue.Read(output); playback.Queue.Read(output); Assert.All(output, x => Assert.Equal(0, x));
        playback.CancelParameterPreview(preview!); playback.Queue.Read(output); playback.Queue.Read(output);
        Assert.Equal(expected, output); Assert.Equal(.25, doc.Snapshot.Sources[id].PluginValues["level"]);
    }

}
