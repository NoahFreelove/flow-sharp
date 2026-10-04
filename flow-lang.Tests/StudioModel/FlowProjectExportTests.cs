using Flow.Audio;
using Flow.Music.Model;
using Flow.Audio.Graph;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.StudioModel;
[Collection("FlowScripts")]
public class FlowProjectExportTests
{
    [Fact]
    public void ExportRestoresProjectAndExactAudioWithoutExecutingStoredSource()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "invalid saved code: \"\\\n♭");
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [],
            [new("audio", new PcmAsset([1, -1, .5f, -.5f, .25f, -.25f], 8000))],
            [new("mix", AudioGraphDefinition.Input("input").Then("gain", "flow.gain"))])));
        var bindings = doc.Snapshot.Sources.Values.Single().Bindings; var track = Guid.NewGuid();
        var audio = bindings.Single(b => b.Output.Role == GeneratedRole.Audio).Id;
        var graph = bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        doc.Edit("Place", p => new(new(p.Arrangement.Id, tempo, meter, audioClips:
            [new(Guid.NewGuid(), track, audio, 0, 0, 3, 8000)]), p.Context, p.Sources.Values, new([new(track, "quoted \"track\"")], graph)));
        ProjectNoteCommands.CreateBlank(doc, Guid.NewGuid(), Guid.NewGuid(), track, 0, .001);
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), graph, "gain", "gain", [new(0, .5)]));
        var code = FlowProjectExporter.Export(doc.Snapshot);
        Assert.Contains("dawScoreClip", code); Assert.Contains("dawAudioClip", code);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var evaluated = engine.Evaluate(code); Assert.True(evaluated.Succeeded, string.Join("\n", evaluated.Errors));
        var restored = evaluated.LastValue!.As<ProjectSnapshot>();
        Assert.Equal(ProjectJson.Serialize(doc.Snapshot), ProjectJson.Serialize(restored));
        var expected = new float[32]; var actual = new float[32];
        ProjectCompiler.Prepare(doc.Snapshot, 8000, 16).Playback.Read(expected);
        ProjectCompiler.Prepare(restored, 8000, 16).Playback.Read(actual);
        Assert.Equal(expected, actual); Assert.Equal(.5f, actual[0]);
    }
    [Fact]
    public void FrameCountsBeyondDoublePrecisionRemainExact()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var clip = new AudioClip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, 9007199254740993, 7, 8000, new(-.001));
        var project = new ProjectSnapshot(new(Guid.NewGuid(), tempo, meter, audioClips: [clip]), new(0, 1, tempo, meter));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowProjectExporter.Export(project));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(clip, result.LastValue!.As<ProjectSnapshot>().Arrangement.AudioClips.Single());
    }
    [Fact]
    public void LargeArrangementUsesOneBatchAndSharedClipOperations()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4), new(3, 3, 4)]);
        var track = Guid.NewGuid(); var source = Guid.NewGuid();
        var clips = Enumerable.Range(0, 1024).Select(i => new ScoreClip(Guid.NewGuid(), track, source, "main", i * 4, 0, 4)).ToArray();
        var project = new ProjectSnapshot(new(Guid.NewGuid(), tempo, meter, clips), new(0, 1, tempo, meter));
        var code = FlowProjectExporter.Export(project);
        Assert.Equal(1, code.Split("dawArrange").Length - 1); Assert.DoesNotContain("dawPlaceScore", code);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(code); Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(clips, result.LastValue!.As<ProjectSnapshot>().Arrangement.ScoreClips);
        var operated = engine.Evaluate("""
            DawClip nudged = (dawRelativeOffsetMs clip0 12.5)
            (dawAlignToBar project0 nudged 4)
            """);
        Assert.True(operated.Succeeded, string.Join("\n", operated.Errors));
        var actual = operated.LastValue!.As<ScoreClip>();
        Assert.Equal(ClipOperations.AlignToBar(ClipOperations.RelativeOffsetMs(clips[0], new(12.5)), 4, meter), actual);
        var rightId = Guid.NewGuid();
        var split = engine.Evaluate($"(dawSplitScore clip0 1.0 \"{rightId}\")");
        Assert.True(split.Succeeded, string.Join("\n", split.Errors));
        var pair = split.LastValue!.As<List<FlowLang.Runtime.Value>>();
        var expectedPair = ClipOperations.Split(clips[0], new(1), rightId);
        Assert.Equal(expectedPair.Left, pair[0].As<ScoreClip>()); Assert.Equal(expectedPair.Right, pair[1].As<ScoreClip>());
        var audioId = Guid.NewGuid(); var audioRight = Guid.NewGuid();
        var audioSplit = engine.Evaluate($"""
            DawClip wave = (dawAudioClip "{audioId}" "{track}" "{source}" 0.0 "7" "16000" 8000 12.5)
            (dawSplitAudio project0 wave "8000" "{audioRight}")
            """);
        Assert.True(audioSplit.Succeeded, string.Join("\n", audioSplit.Errors));
        var audioPair = audioSplit.LastValue!.As<List<FlowLang.Runtime.Value>>();
        Assert.Equal(8007, audioPair[1].As<AudioClip>().SourceOffsetFrames);
        Assert.Equal(2, audioPair[1].As<AudioClip>().AnchorQuarters);
        var scoreTrim = engine.Evaluate("(dawTrimScore clip0 1.0 2.0)");
        Assert.True(scoreTrim.Succeeded, string.Join("\n", scoreTrim.Errors));
        Assert.Equal(ClipOperations.Trim(clips[0], 1, 2), scoreTrim.LastValue!.As<ScoreClip>());
        var scoreRepeat = engine.Evaluate($"(dawRepeatScore clip0 (list \"{rightId}\"))");
        Assert.True(scoreRepeat.Succeeded, string.Join("\n", scoreRepeat.Errors));
        Assert.Equal(ClipOperations.Repeat(clips[0], [rightId])[0], scoreRepeat.LastValue!.As<List<FlowLang.Runtime.Value>>().Single().As<ScoreClip>());
        var audioTrim = engine.Evaluate("(dawTrimAudio project0 wave \"8000\" \"8000\")");
        Assert.True(audioTrim.Succeeded, string.Join("\n", audioTrim.Errors));
        Assert.Equal(audioPair[1].As<AudioClip>().SourceOffsetFrames, audioTrim.LastValue!.As<AudioClip>().SourceOffsetFrames);
        var audioRepeat = engine.Evaluate($"(dawRepeatAudio project0 wave (list \"{audioRight}\"))");
        Assert.True(audioRepeat.Succeeded, string.Join("\n", audioRepeat.Errors));
        var repeatedAudio = audioRepeat.LastValue!.As<List<FlowLang.Runtime.Value>>().Single().As<AudioClip>();
        Assert.Equal(4, repeatedAudio.AnchorQuarters); Assert.Equal(7, repeatedAudio.SourceOffsetFrames);
    }

    [Fact]
    public void MultipleGraphExportsExposeEditableDeviceParametersWithoutVariableCollisions()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "saved code");
        var gain = AudioGraphDefinition.Input("input").Then("gain", "flow.gain", new Dictionary<string, double> { ["gain"] = .5 });
        var delay = AudioGraphDefinition.Input("input").Then("delay", "flow.delay", bypassed: true);
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [], graphLayers:
            [new("first", gain), new("second", delay)])));
        var code = FlowProjectExporter.Export(doc.Snapshot);
        Assert.Contains("dawDevice", code); Assert.Contains("dawProjectGraph", code);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(code); Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(ProjectJson.Serialize(doc.Snapshot), ProjectJson.Serialize(result.LastValue!.As<ProjectSnapshot>()));
        using var changedEngine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var changed = changedEngine.Evaluate(code.Replace("\"gain\" 0.5", "\"gain\" 0.25"));
        Assert.True(changed.Succeeded, string.Join("\n", changed.Errors));
        Assert.Equal(.25, changed.LastValue!.As<ProjectSnapshot>().Sources.Values.Single().Result.GraphLayers[0].Graph.Nodes[1].Parameters["gain"]);
    }

    [Fact]
    public void InstrumentsAndRoutingExportAsExplicitConstructionWithExactSamplerAudio()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "saved");
        var sample = new PcmAsset([.1f, -.2f, .5f, -.7f], 8000);
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [], instrumentLayers:
            [new("sine", new(12, 1.5, 33.25)), new("sampler", new(7, .25, 12.5, sample, 432))])));
        var bindings = doc.Snapshot.Sources.Values.Single().Bindings;
        doc.Edit("Route", p => new(p.Arrangement, p.Context, p.Sources.Values, new([
            new(Guid.NewGuid(), "Sine track", bindings.Single(b => b.Output.LayerId == "sine").Id),
            new(Guid.NewGuid(), "Sampler track", bindings.Single(b => b.Output.LayerId == "sampler").Id)], null)));
        var code = FlowProjectExporter.Export(doc.Snapshot);
        Assert.Contains("dawSine 12", code); Assert.Contains("dawSampler", code); Assert.Contains("dawTrack", code);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(code); Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(ProjectJson.Serialize(doc.Snapshot), ProjectJson.Serialize(result.LastValue!.As<ProjectSnapshot>()));
    }

    [Fact]
    public void NotesExportTuningRestsArticulationAndSharedSectionProvenance()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var origin = new SourceOrigin("score\".flow", 2, 3, 4);
        var notes = new[] {
            new NoteEvent(Guid.NewGuid(), "lead", -.125, .5, new('B', 4, -1, -13.25, 70, 421.5), .8,
                NoteArticulation.Tenuto, true, .15, 25, new(1, 2), origin),
            new NoteEvent(Guid.NewGuid(), "lead", 1, .25, null)
        };
        var sequence = new SequenceSnapshot(Guid.NewGuid(), "part", 4, notes, [new(Guid.NewGuid(), 0, 4, 4, 4, false)]);
        var section = new SectionSnapshot(Guid.NewGuid(), "section", new(), [sequence], origin);
        var score = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), section, 2), new(Guid.NewGuid(), section)]);
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "saved");
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [new("main", score)])));
        var code = FlowProjectExporter.Export(doc.Snapshot); Assert.Contains("dawNote", code); Assert.Contains("421.5", code);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(code); Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var restored = result.LastValue!.As<ProjectSnapshot>(); Assert.Equal(ProjectJson.Serialize(doc.Snapshot), ProjectJson.Serialize(restored));
        var placements = restored.Sources.Values.Single().Result.ScoreLayers[0].Composition.Placements;
        Assert.Same(placements[0].Section, placements[1].Section);
    }

}
