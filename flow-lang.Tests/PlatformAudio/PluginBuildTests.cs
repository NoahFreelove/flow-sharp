using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flow.Studio.Host;
using Flow.Studio.Engine;
using Flow.Audio.Graph;
using Flow.Studio.Model;
using FlowLang.Hosting;
using FlowLang.Core;
using Xunit;
namespace FlowLang.Tests.PlatformAudio;
[Collection("FlowScripts")]
public class PluginBuildTests
{
    private const string Code = """
        use "@flowDaw"
        proc build (Dict<String, Double>: context)
            (dawResult "effect" (dawGain "level" (dawInput "input" 0) 1.0))
        end proc
        """;
    internal static PluginPackage Package(string code = Code, int inputs = 1)
    {
        string json = JsonSerializer.Serialize(new
        {
            ApiVersion = 1, Id = "example.level", Name = "Level", Author = "Local", License = "Private", Version = "1",
            SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code))), Builder = "build",
            Kind = "AudioEffect", AudioInputs = inputs, AudioOutputs = 1, NoteInput = false, NoteOutput = false,
            MinimumSampleRate = 8000, MaximumSampleRate = 96000, MaximumBlockFrames = 512,
            StateSchema = "v1", StatePolicy = "Reset",
            Parameters = new[] { new { Id = "gain", Name = "Gain", Unit = "linear", Minimum = 0, Maximum = 2, Default = 1,
                Scale = "Linear", SmoothingMilliseconds = 5, RequiresRebuild = false, Labels = Array.Empty<string>() } },
            Dependencies = Array.Empty<PluginDependency>()
        });
        return new(PluginManifest.Parse(json), code, "effect", [new("gain", "level", "gain")]);
    }
    [Fact]
    public async Task InstrumentPackageBuildsPersistsAndAppliesUndoablePublicValues()
    {
        const string code = """
            use "@flowDaw"
            proc build (Dict<String, Double>: context)
                AudioGraph oscillator = (dawSineOsc "osc" (dawInput "pitch" 0))
                AudioGraph envelope = (dawAdsr "env" (dawInput "gate" 1) 2ms 5ms 0.7 20ms)
                AudioGraph voice = (dawMultiply "shaped" oscillator envelope)
                (dawResult "instrument" (dawGraphInstrument (dawGain "level" voice 1.0) 8))
            end proc
            """;
        var effect = Package(code);
        var metadata = System.Text.Json.Nodes.JsonNode.Parse(effect.Manifest.Serialize())!;
        metadata["Kind"] = "Instrument"; metadata["AudioInputs"] = 0; metadata["NoteInput"] = true;
        metadata["Parameters"]![0]!["RequiresRebuild"] = true;
        var plugin = new PluginPackage(PluginManifest.Parse(metadata.ToJsonString()), code, "instrument", effect.Targets);
        var doc = ProjectFactory.Create(); var before = doc.Snapshot;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
        Guid source = Guid.NewGuid(); host.RequestPluginBuild(source, plugin); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        var accepted = doc.Snapshot.Sources[source];
        plugin.ValidateInstrument(accepted.Result, 8000, 128);
        ProjectPluginCommands.SetParameter(doc, source, "gain", .25);
        var preset = PluginPreset.Deserialize(PluginPreset.Capture("Soft instrument", doc.Snapshot.Sources[source]).Serialize());
        ProjectPluginCommands.ResetParameter(doc, source, "gain");
        Assert.True(session.ApplyPluginPreset(source, preset));
        Assert.False(session.ApplyPluginPreset(source, preset));
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        var saved = restored.Sources[source];
        var applied = saved.Plugin!.ValidateInstrument(saved.Result, 8000, 128).ApplyValues(saved.PluginValues);
        Assert.Equal(.25, applied.VoiceGraph!.Nodes.Single(n => n.Id == "level").Parameters["gain"]);
        Assert.Equal(1, accepted.Result.InstrumentLayers.Single().Instrument.VoiceGraph!.Nodes.Single(n => n.Id == "level").Parameters["gain"]);
        Guid binding = saved.Bindings.Single().Id;
        var routed = new ProjectSnapshot(restored.Arrangement, restored.Context, restored.Sources.Values,
            new(restored.Routing.Tracks.Select(t => t with { InstrumentBinding = binding }), restored.Routing.GraphBinding));
        _ = ProjectCompiler.Prepare(routed, 8000, 128);
        Assert.True(doc.History.Undo()); Assert.Empty(doc.Snapshot.Sources[source].PluginValues);
        Assert.True(doc.History.Redo()); Assert.Equal(.25, doc.Snapshot.Sources[source].PluginValues["gain"]);
        var beforeClone = doc.Snapshot;
        Guid clone = session.DuplicatePlugin(source, doc.Snapshot.Routing.Tracks[0].Id);
        var afterClone = doc.Snapshot;
        var cloned = afterClone.Sources[clone];
        Assert.Equal(cloned.Bindings.Single().Id, afterClone.Routing.Tracks[0].InstrumentBinding);
        Assert.NotEqual(binding, cloned.Bindings.Single().Id);
        _ = ProjectCompiler.Prepare(afterClone, 8000, 128);
        Assert.True(doc.History.Undo()); Assert.Same(beforeClone, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(afterClone, doc.Snapshot);
        ProjectPluginCommands.SetParameter(doc, clone, "gain", .5);
        Assert.Equal(.25, doc.Snapshot.Sources[source].PluginValues["gain"]);
        Assert.Equal(.5, doc.Snapshot.Sources[clone].PluginValues["gain"]);
        metadata["Parameters"]![0]!["RequiresRebuild"] = false;
        var invalid = new PluginPackage(PluginManifest.Parse(metadata.ToJsonString()), code, "instrument", effect.Targets);
        invalid.ValidateInstrument(accepted.Result, 8000, 128);
        Assert.Throws<ArgumentException>(() => plugin.ValidateEffect(accepted.Result, 8000, 128));
    }
    [Theory]
    [InlineData("subtractive-instrument")]
    [InlineData("sampled-drum")]
    public async Task SavedInstrumentExamplePreservesRenderedPublicValuesThroughExportAndRebuild(string example)
    {
        string root = Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot, "examples", "plugins");
        var package = PluginPackage.Deserialize(File.ReadAllText(Path.Combine(root, example + ".flowplugin")));
        Assert.Equal(File.ReadAllText(Path.Combine(root, example + ".flow")), package.Source);
        var doc = ProjectFactory.Create();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
        Guid source = Guid.NewGuid(); host.RequestPluginBuild(source, package); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        float[] Render(ProjectSnapshot project)
        {
            var saved = project.Sources[source];
            var settings = saved.Plugin!.ValidateInstrument(saved.Result, 8000, 128).ApplyValues(saved.PluginValues);
            var playback = new Flow.Audio.PreparedNotePlayback([
                new(Guid.NewGuid(), Guid.NewGuid(), 0, 64, 440, .6),
                new(Guid.NewGuid(), Guid.NewGuid(), 16, 80, 660, .4)], 8000, 128, settings: settings);
            var output = new float[256]; playback.Read(output); return output;
        }
        var defaultAudio = Render(doc.Snapshot);
        Assert.True(session.SetPluginParameter(source, "level", .125));
        Assert.False(session.SetPluginParameter(source, "level", .125));
        var expected = Render(doc.Snapshot); Assert.Contains(expected, x => x != 0);
        for (int i = 0; i < expected.Length; i++) Assert.Equal(defaultAudio[i] * .5f, expected[i], 6);
        Assert.True(session.SetPluginParameter(source, "cutoff", 250));
        Assert.True(session.SetPluginParameter(source, "release", 50));
        Finish(host, session);
        expected = Render(doc.Snapshot);
        var reopened = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot)); Assert.Equal(expected, Render(reopened));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowProjectExporter.Export(reopened));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var exported = result.LastValue!.As<ProjectSnapshot>();
        Assert.Equal(ProjectJson.Serialize(reopened), ProjectJson.Serialize(exported)); Assert.Equal(expected, Render(exported));
        host.RequestPluginBuild(source, package); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        Assert.Equal(expected, Render(doc.Snapshot));
        Assert.Equal(3, doc.Snapshot.Sources[source].PluginValues.Count);
        Assert.True(session.ResetPluginParameter(source, "level"));
        Assert.False(session.ResetPluginParameter(source, "level"));
        var beforeInvalid = doc.Snapshot;
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetPluginParameter(source, "level", 2));
        Assert.Same(beforeInvalid, doc.Snapshot);
        Finish(host, session);
    }
    [Fact]
    public async Task InstrumentPreviewChangesAudioCancelsAndCommitsOneAction()
    {
        var package = PluginPackage.Deserialize(File.ReadAllText(Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot,
            "examples/plugins/subtractive-instrument.flowplugin")));
        var doc = ProjectFactory.Create(); var track = doc.Snapshot.Routing.Tracks.Single().Id;
        Guid score = Guid.NewGuid(), instrument = Guid.NewGuid();
        ProjectNoteCommands.CreateBlank(doc, score, Guid.NewGuid(), track, 0, 4);
        var section = doc.Snapshot.Sources[score].Result.ScoreLayers[0].Composition.Placements[0].Section;
        ProjectNoteCommands.EditSequence(doc, score, section.Id, section.Sequences[0].Id, "Draw", s =>
            Flow.Music.Model.Editing.NoteEditing.Add(s, [new(Guid.NewGuid(), "voice", 0, 4, new('A', 4, 0, null, 69, 440))]));
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var generator = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
        generator.RequestPluginBuild(instrument, package); Finish(generator, session);
        Assert.True(generator.LastCompletion!.Accepted, generator.LastCompletion.Error);
        var binding = doc.Snapshot.Sources[instrument].Bindings.Single().Id;
        doc.Edit("Route", p => new(p.Arrangement, p.Context, p.Sources.Values,
            new(p.Routing.Tracks.Select(t => t with { InstrumentBinding = binding }), p.Routing.GraphBinding)));
        session.RequestPreparation(); Finish(generator, session);
        await using var mixer = new ProjectMixerHost(session);
        float[] Render()
        {
            Assert.True(session.Playback.Queue.TrySeek(0)); Assert.True(session.Playback.Queue.TryPlay());
            var samples = new float[256]; session.Playback.Queue.Read(samples); session.Playback.Queue.Read(samples); return samples;
        }
        var original = Render(); Assert.Contains(original, x => x != 0);
        var before = doc.Snapshot; int history = doc.History.UndoCount;
        Assert.True(mixer.TryBeginPluginParameterPreview(instrument, "level"));
        Assert.True(mixer.TryUpdateParameterPreview(.125));
        var preview = Render();
        for (int i = 0; i < preview.Length; i++) Assert.Equal(original[i] * .5f, preview[i], 6);
        Assert.Same(before, doc.Snapshot); mixer.CancelParameterPreview(); Assert.Equal(original, Render());
        Assert.True(mixer.TryBeginPluginParameterPreview(instrument, "level"));
        Assert.True(mixer.TryUpdateParameterPreview(.125)); Assert.True(mixer.TryCommitParameterPreview(out _));
        Finish(generator, session); Assert.Equal(history + 1, doc.History.UndoCount);
        Assert.Equal(.125, doc.Snapshot.Sources[instrument].PluginValues["level"]);
        Assert.Equal(preview, Render());
        Assert.True(doc.History.Undo()); session.RequestPreparation(); Finish(generator, session);
        Assert.Equal(original, Render());
        Assert.Throws<InvalidOperationException>(() => mixer.TryBeginPluginParameterPreview(instrument, "release"));
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), binding, "", "level",
            [new(0, .25), new(.032, .125)], AutomationShape.Step, AutomationTargetKind.PluginParameter));
        session.RequestPreparation(); Finish(generator, session);
        var automated = Render();
        for (int i = 0; i < automated.Length; i++) Assert.Equal(original[i] * .5f, automated[i], 6);
        Assert.Throws<InvalidOperationException>(() => mixer.TryBeginPluginParameterPreview(instrument, "level"));
        Assert.Throws<InvalidOperationException>(() => session.SetPluginParameter(instrument, "level", .75));
        var reopened = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        var prepared = ProjectCompiler.Prepare(reopened, 8000, 128).Playback;
        var afterReopen = new float[256]; prepared.Read(afterReopen); prepared.Read(afterReopen);
        Assert.Equal(automated, afterReopen);
        using var exportEngine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var exported = exportEngine.Evaluate(FlowProjectExporter.Export(reopened));
        Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
        var exportedPlayback = ProjectCompiler.Prepare(exported.LastValue!.As<ProjectSnapshot>(), 8000, 128).Playback;
        exportedPlayback.Read(afterReopen); exportedPlayback.Read(afterReopen); Assert.Equal(automated, afterReopen);
    }
    [Fact]
    public void PublicMultiTargetEffectAutomationPreservesTempoAndExportedAudio()
    {
        const string code = """
            use "@flowDaw"
            proc build (Dict<String, Double>: context)
                (dawResult "effect" (dawGain "second" (dawGain "level" (dawInput "input" 0) 1.0) 1.0))
            end proc
            """;
        var basic = Package(code);
        var plugin = new PluginPackage(basic.Manifest, code, "effect", [new("gain", "level", "gain"), new("gain", "second", "gain")]);
        var tempo = new ProjectTempoMap([new(0, 120), new(2, 60)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "build"), code);
        var built = FlowDawGenerator.Build(new(ticket.Descriptor, ticket.Revision, code, ticket.Context, TimeSpan.FromSeconds(20), plugin));
        Assert.True(built.Status == JobStatus.Succeeded, built.Error);
        Assert.True(doc.Accept(ticket, built.Value!, "Accept plugin", p => p, plugin: plugin));
        var graph = doc.Snapshot.Sources[ticket.Descriptor.SourceId].Bindings.Single().Id;
        var audioTicket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "saved audio");
        Assert.True(doc.Accept(audioTicket, new(audioTicket.Descriptor.SourceId, audioTicket.Revision, audioTicket.Context, [],
            [new("audio", new Flow.Audio.PcmAsset(Enumerable.Repeat(1f, 64000).ToArray(), 8000))])));
        var audio = doc.Snapshot.Sources[audioTicket.Descriptor.SourceId].Bindings.Single().Id; Guid track = Guid.NewGuid();
        doc.Edit("Route", p => new(new(p.Arrangement.Id, tempo, meter, audioClips:
            [new(Guid.NewGuid(), track, audio, 0, 0, 32000, 8000)]), p.Context, p.Sources.Values, new([new(track, "Audio")], graph)));
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), graph, "", "gain", [new(0, 0), new(4, 1)],
            targetKind: AutomationTargetKind.PluginParameter));
        float[] Render(ProjectSnapshot project)
        {
            var playback = ProjectCompiler.Prepare(project, 8000, 256).Playback; var output = new float[64000];
            for (int offset = 0; offset < output.Length; offset += 512)
                playback.Read(output.AsSpan(offset, Math.Min(512, output.Length - offset)));
            return output;
        }
        var expected = Render(doc.Snapshot);
        Assert.Equal(.25f, expected[16000], 6); // q2, 1 second: both gains are .5.
        Assert.Equal(.5625f, expected[32000], 6); // q3, 2 seconds: both gains are .75.
        Assert.Equal(1f, expected[48000], 6);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowProjectExporter.Export(doc.Snapshot));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(expected, Render(result.LastValue!.As<ProjectSnapshot>()));
        var seeked = ProjectCompiler.Prepare(doc.Snapshot, 8000, 256).Playback;
        seeked.Seek(16000); float[] slice = new float[512]; seeked.Read(slice);
        Assert.Equal(expected.Skip(32000).Take(512), slice);
        Assert.Throws<InvalidOperationException>(() => ProjectPluginCommands.SetParameter(doc, ticket.Descriptor.SourceId, "gain", .5));
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), graph, "level", "gain", [new(0, .5)]));
        Assert.Throws<ArgumentException>(() => ProjectCompiler.Prepare(doc.Snapshot, 8000, 256));
    }
    [Fact]
    public void PackagedSampleCapabilityRejectsUndeclaredAndMalformedAssets()
    {
        string root = Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot, "examples/plugins");
        var package = PluginPackage.Deserialize(File.ReadAllText(Path.Combine(root, "sampled-drum.flowplugin")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(root, "assets/kick.wav")), Convert.FromBase64String(package.Dependencies.Single().Base64));
        foreach (bool malformed in new[] { false, true })
        {
            string code = malformed ? package.Source : package.Source.Replace("(dawPluginSample \"kick\")", "(dawPluginSample \"/tmp/undeclared.wav\")");
            var metadata = System.Text.Json.Nodes.JsonNode.Parse(package.Manifest.Serialize())!;
            metadata["SourceSha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
            byte[] bytes = malformed ? Encoding.UTF8.GetBytes("not a WAVE file") : Convert.FromBase64String(package.Dependencies.Single().Base64);
            metadata["Dependencies"]![0]!["Sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var invalid = new PluginPackage(PluginManifest.Parse(metadata.ToJsonString()), code, package.OutputLayer, package.Targets,
                [new("kick", "1", Convert.ToBase64String(bytes))]);
            var context = ProjectFactory.Create().Snapshot.Context;
            var result = FlowDawGenerator.Build(new(new(1, Guid.NewGuid(), "build"), 1, code, context, TimeSpan.FromSeconds(20), invalid));
            Assert.Equal(JobStatus.Failed, result.Status);
            Assert.Contains(malformed ? "RIFF" : "pinned", result.Error);
        }
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var ordinary = engine.Evaluate("use \"@flowDaw\"; (dawPluginSample \"kick\")");
        Assert.False(ordinary.Succeeded);
    }
    private static void Finish(ProjectGeneratorHost host, ProjectPlaybackSession session) =>
        Assert.True(SpinWait.SpinUntil(() => { host.Poll(); return !host.IsBuilding && !session.IsPreparing; }, TimeSpan.FromSeconds(30)));
    [Fact]
    public async Task IsolatedPluginAcceptanceSavesExactPackageAndUndoRestoresPreviousVersion()
    {
        var doc = ProjectFactory.Create(); var before = doc.Snapshot;
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
        var source = Guid.NewGuid(); var plugin = Package();
        host.RequestPluginBuild(source, plugin); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        Assert.Equal(1, doc.History.UndoCount); Assert.Same(plugin, doc.Snapshot.Sources[source].Plugin);
        var saved = ProjectJson.Serialize(doc.Snapshot); var restored = ProjectJson.Deserialize(saved);
        Assert.Equal(plugin.Serialize(), restored.Sources[source].Plugin!.Serialize());
        restored.Sources[source].Plugin!.ValidateEffect(restored.Sources[source].Result, 8000, 128);
        Assert.Equal(saved, ProjectJson.Serialize(restored));
        var pluginBinding = restored.Sources[source].Bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        var routed = new ProjectSnapshot(restored.Arrangement, restored.Context, restored.Sources.Values,
            new(restored.Routing.Tracks, pluginBinding), restored.Assets, restored.Automation);
        _ = ProjectCompiler.Prepare(routed, 8000, 128);
        var mismatched = ProjectGraphConstruction.Replace(routed, source, "effect",
            AudioGraphDefinition.Input("input").Then("level", "flow.gain", new Dictionary<string, double> { ["gain"] = 2 }));
        Assert.Throws<ArgumentException>(() => ProjectCompiler.Prepare(mismatched, 8000, 128));
        using (var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null }))
        {
            var exported = engine.Evaluate(FlowProjectExporter.Export(doc.Snapshot));
            Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
            var reconstructed = exported.LastValue!.As<ProjectSnapshot>().Sources[source];
            Assert.Equal(plugin.Serialize(), reconstructed.Plugin!.Serialize());
            reconstructed.Plugin.ValidateEffect(reconstructed.Result, 8000, 128);
        }
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(plugin, doc.Snapshot.Sources[source].Plugin);
        var accepted = doc.Snapshot;
        host.RequestPluginBuild(source, Package(inputs: 2)); Finish(host, session);
        Assert.False(host.LastCompletion!.Accepted); Assert.Equal(JobStatus.Failed, host.LastCompletion.Status);
        Assert.Same(accepted, doc.Snapshot); Assert.Equal(1, doc.History.UndoCount);
        // An ordinary source edit explicitly removes the previous declaration.
        host.RequestBuild(new(1, source, "build"), Code); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted); Assert.Null(doc.Snapshot.Sources[source].Plugin);
    }
    [Fact]
    public void PackageRejectsTamperingAndMissingPinnedContentsBeforeAnyBuild()
    {
        var plugin = Package(); var restored = PluginPackage.Deserialize(plugin.Serialize());
        Assert.Equal(plugin.Serialize(), restored.Serialize());
        Assert.Throws<ArgumentException>(() => new PluginPackage(plugin.Manifest, Code + "\n", "effect", plugin.Targets));
        Assert.Throws<ArgumentException>(() => new PluginPackage(plugin.Manifest, Code, "effect", []));
        var data = System.Text.Json.Nodes.JsonNode.Parse(plugin.Serialize())!;
        data["Source"] = "invalid replacement";
        Assert.Throws<ArgumentException>(() => PluginPackage.Deserialize(data.ToJsonString()));
    }
    [Fact]
    public async Task PinnedDependenciesBuildInWorkerAndUndeclaredTransitiveImportsFail()
    {
        string code = Code.Replace("use \"@flowDaw\"", "use \"@flowDaw\"\nuse \"helper\"").Replace("(dawGain \"level\" (dawInput \"input\" 0) 1.0)", "(helperGraph 1.0)");
        const string helper = "use \"@flowDaw\"\nproc helperGraph (Double: level)\n    (dawGain \"level\" (dawInput \"input\" 0) level)\nend proc";
        PluginPackage WithHelper(string source)
        {
            var plugin = Package(code); byte[] bytes = Encoding.UTF8.GetBytes(source);
            var manifestJson = System.Text.Json.Nodes.JsonNode.Parse(plugin.Manifest.Serialize())!;
            manifestJson["Dependencies"] = JsonSerializer.SerializeToNode(new[] { new PluginDependency("helper", "1", Convert.ToHexStringLower(SHA256.HashData(bytes))) });
            var manifest = PluginManifest.Parse(manifestJson.ToJsonString());
            Assert.Throws<ArgumentException>(() => new PluginPackage(manifest, code, "effect", plugin.Targets));
            return new(manifest, code, "effect", plugin.Targets, [new("helper", "1", Convert.ToBase64String(bytes))]);
        }
        var withDependency = WithHelper(helper);
        Assert.Equal(withDependency.Serialize(), PluginPackage.Deserialize(withDependency.Serialize()).Serialize());
        var doc = ProjectFactory.Create();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        await using var host = new ProjectGeneratorHost(session, worker);
        var id = Guid.NewGuid(); host.RequestPluginBuild(id, withDependency); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error); Assert.True(worker.LastProcessId > 0);
        var before = doc.Snapshot;
        host.RequestPluginBuild(id, WithHelper("use \"undeclared.flow\"\n" + helper)); Finish(host, session);
        Assert.False(host.LastCompletion!.Accepted); Assert.Contains("not a pinned dependency", host.LastCompletion.Error);
        Assert.Same(before, doc.Snapshot);
    }
    [Fact]
    public async Task PluginImportsCannotFallBackToAnExistingHostFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "flow-plugin-import-" + Guid.NewGuid().ToString("N") + ".flow");
        File.WriteAllText(path, "Note: valid but undeclared host module");
        try
        {
            var doc = ProjectFactory.Create(); var before = doc.Snapshot;
            await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
            await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
            host.RequestPluginBuild(Guid.NewGuid(), Package("use \"" + path + "\"\n" + Code)); Finish(host, session);
            Assert.False(host.LastCompletion!.Accepted); Assert.Contains("not a pinned dependency", host.LastCompletion.Error);
            Assert.Same(before, doc.Snapshot);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PluginInitializerCannotWriteMidiThroughBundledNativeApi()
    {
        string path = Path.Combine(Path.GetTempPath(), "flow-plugin-denied-" + Guid.NewGuid().ToString("N") + ".mid");
        try
        {
            string prefix = "use \"@audio\"\nsection phrase { Sequence melody = | A4q | }\nSong score = [phrase]\n(writeMidi \"" + path + "\" score)\n";
            var doc = ProjectFactory.Create(); var before = doc.Snapshot;
            await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
            await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
            host.RequestPluginBuild(Guid.NewGuid(), Package(prefix + Code)); Finish(host, session);
            Assert.False(host.LastCompletion!.Accepted);
            Assert.Contains("Native function 'writeMidi' is unavailable", host.LastCompletion.Error);
            Assert.False(File.Exists(path)); Assert.Same(before, doc.Snapshot);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PublicComposedEffectExampleBuildsWithLiveValueParameterTarget()
    {
        string code = File.ReadAllText(Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot, "examples", "plugins", "composed-soft-clip.flow"));
        var plugin = PluginPackage.Deserialize(File.ReadAllText(Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot, "examples", "plugins", "composed-soft-clip.flowplugin")));
        Assert.Equal(code, plugin.Source); Assert.Equal(3, plugin.Manifest.Parameters.Count);
        var doc = ProjectFactory.Create();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
        var id = Guid.NewGuid(); host.RequestPluginBuild(id, plugin); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        var graph = doc.Snapshot.Sources[id].Result.GraphLayers.Single().Graph;
        Assert.Contains(graph.Nodes, n => n.DeviceId == "flow.multiply"); Assert.DoesNotContain(graph.Nodes, n => n.DeviceId == "flow.drive");
        var prepared = new PreparedAudioGraph(graph, 8000, 128); float[] input = [.5f, -.5f], output = new float[2];
        prepared.Process(input, output);
        Assert.Equal((float)Math.Tanh(1f + .1f), output[0]); Assert.Equal((float)Math.Tanh(-1f + .1f), output[1]);
    }

    [Fact]
    public async Task VisualGraphEditClonesEffectivePluginValues()
    {
        var doc = ProjectFactory.Create();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
        var id = Guid.NewGuid(); host.RequestPluginBuild(id, Package()); Finish(host, session);
        var binding = doc.Snapshot.Sources[id].Bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        doc.Edit("Route", p => new(p.Arrangement, p.Context, p.Sources.Values, new(p.Routing.Tracks, binding), p.Assets, p.Automation));
        ProjectPluginCommands.SetParameter(doc, id, "gain", .25);
        var request = ProjectMixerAuthoring.BeginInsertEffect(doc, "added", "flow.tanh", "level");
        Assert.True(ProjectMixerAuthoring.Commit(doc, ProjectMixerAuthoring.Prepare(request, TestContext.Current.CancellationToken)));
        var graph = doc.Snapshot.Sources.Values.Single(s => s.IsManagedGraph).Result.GraphLayers.Single().Graph;
        Assert.Equal(.25, graph.Nodes.Single(n => n.Id == "level").Parameters["gain"]);
        Assert.Equal(1, doc.Snapshot.Sources[id].Result.GraphLayers.Single().Graph.Nodes.Single(n => n.Id == "level").Parameters["gain"]);
        Assert.Equal(.25, doc.Snapshot.Sources[id].PluginValues["gain"]);
    }

    [Fact]
    public async Task PublicPreviewCommitsInstanceValueWithoutCloningThePlugin()
    {
        var doc = ProjectFactory.Create();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var generator = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
        var id = Guid.NewGuid(); generator.RequestPluginBuild(id, Package()); Finish(generator, session);
        var plugin = doc.Snapshot.Sources[id].Plugin;
        var binding = doc.Snapshot.Sources[id].Bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        doc.Edit("Route", p => new(p.Arrangement, p.Context, p.Sources.Values, new(p.Routing.Tracks, binding), p.Assets, p.Automation));
        session.RequestPreparation(); Finish(generator, session);
        int history = doc.History.UndoCount;
        await using var mixer = new ProjectMixerHost(session);
        Assert.True(mixer.TryBeginPluginParameterPreview(id, "gain"));
        Assert.True(mixer.TryUpdateParameterPreview(.25)); Assert.Throws<ArgumentOutOfRangeException>(() => mixer.TryUpdateParameterPreview(3));
        Assert.True(mixer.TryCommitParameterPreview(out var requestId));
        Assert.True(SpinWait.SpinUntil(() => { mixer.Poll(); return !session.IsPreparing; }, TimeSpan.FromSeconds(10)));
        Assert.True(mixer.TryTakeCompletion(out var result)); Assert.Equal(requestId, result!.RequestId); Assert.Equal(MixerGestureStatus.Committed, result.Status);
        Assert.Equal(history + 1, doc.History.UndoCount); Assert.Equal(.25, doc.Snapshot.Sources[id].PluginValues["gain"]);
        Assert.Same(plugin, doc.Snapshot.Sources[id].Plugin); Assert.DoesNotContain(doc.Snapshot.Sources.Values, s => s.IsManagedGraph);
        Assert.True(doc.History.Undo()); Assert.Empty(doc.Snapshot.Sources[id].PluginValues);
    }

    [Fact]
    public async Task ModulatedFilterExampleBuildsUnderPluginPolicyWithHzControls()
    {
        string root = Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot, "examples", "plugins");
        var plugin = PluginPackage.Deserialize(File.ReadAllText(Path.Combine(root, "modulated-low-pass.flowplugin")));
        Assert.Equal(File.ReadAllText(Path.Combine(root, "modulated-low-pass.flow")), plugin.Source);
        var doc = ProjectFactory.Create();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
        var id = Guid.NewGuid(); host.RequestPluginBuild(id, plugin); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        Assert.All(plugin.Manifest.Parameters, p => Assert.Equal("Hz", p.Unit));
        var graph = new PreparedAudioGraph(doc.Snapshot.Sources[id].Result.GraphLayers.Single().Graph, 8000, 128);
        Assert.Equal(16000, graph.TailFrames);
        var output = new float[256]; graph.Process(Enumerable.Repeat(.5f, 256).ToArray(), output);
        Assert.All(output, sample => Assert.True(float.IsFinite(sample) && sample > 0 && sample <= .5));
    }

    [Fact]
    public async Task IncompatibleIsolatedReloadReportsFailureAndRetainsAcceptedInstance()
    {
        var doc = ProjectFactory.Create();
        await using var session = new ProjectPlaybackSession(doc, "/tmp", 8000, 128);
        await using var host = new ProjectGeneratorHost(session, ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll")));
        Guid id = Guid.NewGuid(); var original = Package();
        host.RequestPluginBuild(id, original); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        session.SetPluginParameter(id, "gain", .25); Finish(host, session);
        var accepted = doc.Snapshot; int history = doc.History.UndoCount;
        var metadata = System.Text.Json.Nodes.JsonNode.Parse(original.Manifest.Serialize())!;
        metadata["StateSchema"] = "incompatible-v2";
        var incompatible = new PluginPackage(PluginManifest.Parse(metadata.ToJsonString()), original.Source, original.OutputLayer, original.Targets);
        host.RequestPluginBuild(id, incompatible); Finish(host, session);
        Assert.Equal(JobStatus.Failed, host.LastCompletion!.Status); Assert.False(host.LastCompletion.Accepted);
        Assert.Contains("state schema", host.LastCompletion.Error);
        Assert.Same(accepted, doc.Snapshot); Assert.Equal(history, doc.History.UndoCount);
        Assert.Same(original, doc.Snapshot.Sources[id].Plugin);
        // A separate instance is the explicit opt-in path for the changed contract.
        Guid replacement = Guid.NewGuid(); host.RequestPluginBuild(replacement, incompatible); Finish(host, session);
        Assert.True(host.LastCompletion!.Accepted, host.LastCompletion.Error);
        Assert.Equal(.25, doc.Snapshot.Sources[id].PluginValues["gain"]);
        Assert.Empty(doc.Snapshot.Sources[replacement].PluginValues);
    }

}
