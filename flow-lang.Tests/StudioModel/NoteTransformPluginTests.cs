using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flow.Music.Model;
using Flow.Studio.Model;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

[Collection("FlowScripts")]
public class NoteTransformPluginTests
{
    private const string Code = """
        use "@flowDaw"
        proc process (Dict<String, Double>: context, DawNoteSequence: input)
            DawNoteSequence moved = (dawTransposeNotes input 12)
            DawNotes notes = (map (dawNotes moved) (fn DawNote n => (dawNoteVelocity n (get context "parameter:velocity"))))
            (dawResult "notes" (dawNoteSequence notes (dawNoteDuration moved)))
        end proc
        """;
    internal static PluginPackage Package(string code = Code, string layer = "notes")
    {
        var manifest = PluginManifest.Parse(JsonSerializer.Serialize(new
        {
            ApiVersion = 1, Id = "example.note-transform", Name = "Octave and velocity", Author = "Local", License = "Private", Version = "1",
            SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code))), Builder = "process",
            Kind = "NoteTransform", AudioInputs = 0, AudioOutputs = 0, NoteInput = true, NoteOutput = true,
            MinimumSampleRate = 8000, MaximumSampleRate = 96000, MaximumBlockFrames = 256,
            StateSchema = "1", StatePolicy = "Reset",
            Parameters = new[] { new { Id = "velocity", Name = "Velocity", Unit = "linear", Minimum = 0, Maximum = 1,
                Default = .5, Scale = "Linear", SmoothingMilliseconds = 0, RequiresRebuild = true, Labels = Array.Empty<string>() } },
            Dependencies = Array.Empty<PluginDependency>()
        }));
        return new(manifest, code, layer, []);
    }
    private static GeneratorBuildRequest Request(PluginPackage? package = null)
    {
        package ??= Package();
        var note = new NoteEvent(Guid.NewGuid(), "voice", .25, .75, new('A', 4, 0, 17, 69, 444.25), .8,
            NoteArticulation.Legato, true, .1, 4, new(3, 4), new("original.flow", 4, 2, 5));
        return new(new(1, Guid.NewGuid(), "process"), 2, package.Source,
            new(7, 11, new([new(0, 123)]), new([new(1, 4, 4)]), new Dictionary<string, double> { ["velocity"] = .25 }),
            TimeSpan.FromSeconds(10), package, NoteInput: new(4, [note, new(Guid.NewGuid(), "voice", 1, 1, null)]));
    }
    private static SequenceSnapshot Sequence(GeneratedSourceOutput output) => output.ScoreLayers.Single().Composition.Placements.Single().Section.Sequences.Single();
    [Fact]
    public void ProcessorParametersCannotAdvertiseLiveAutomation()
    {
        var package = Package(); var json = System.Text.Json.Nodes.JsonNode.Parse(package.Manifest.Serialize())!;
        json["Parameters"]![0]!["RequiresRebuild"] = false;
        Assert.Throws<ArgumentException>(() => new PluginPackage(PluginManifest.Parse(json.ToJsonString()), package.Source, package.OutputLayer, []));
    }
    [Fact]
    public void PackagedExampleExecutesWithoutNativePluginSpecificCode()
    {
        string root = Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot, "examples", "plugins");
        var package = PluginPackage.Deserialize(File.ReadAllText(Path.Combine(root, "octave-velocity.flowplugin")));
        Assert.Equal(File.ReadAllText(Path.Combine(root, "octave-velocity.flow")), package.Source);
        var result = FlowDawGenerator.Build(Request(package), TestContext.Current.CancellationToken);
        Assert.True(result.Status == JobStatus.Succeeded, result.Error);
        Assert.Equal(888.5, Sequence(result.Value!).Notes[0].Pitch!.FrequencyHz);
    }
    [Fact]
    public void TransformPreservesIdentityTuningAndTimingWhileUsingSharedTransposeAndTypedVelocity()
    {
        var request = Request(); var input = request.NoteInput!;
        Assert.Equal(input.Serialize(), PluginNoteInput.Deserialize(input.Serialize()).Serialize());
        var result = FlowDawGenerator.Build(request, TestContext.Current.CancellationToken);
        Assert.True(result.Status == JobStatus.Succeeded, result.Error);
        var sequence = Sequence(result.Value!); var note = sequence.Notes[0];
        Assert.Equal(4, sequence.DurationQuarters);
        Assert.Equal(input.Notes[0] with { Pitch = note.Pitch, Velocity = .25 }, note);
        Assert.Equal(888.5, note.Pitch!.FrequencyHz); Assert.Equal(81, note.Pitch.MidiKey); Assert.Equal(17, note.Pitch.CentOffset);
        Assert.Null(sequence.Notes[1].Pitch); Assert.Equal(input.Notes[1].Id, sequence.Notes[1].Id);
        Assert.Equal(444.25, input.Notes[0].Pitch!.FrequencyHz); Assert.Equal(.8, input.Notes[0].Velocity);
        var again = FlowDawGenerator.Build(request, TestContext.Current.CancellationToken);
        Assert.Equal(GeneratedContentJson.Serialize(result.Value), GeneratedContentJson.Serialize(again.Value!));
    }
    [Fact]
    public void MissingWrongAndMixedInputAndWrongResultAreRejected()
    {
        var request = Request();
        foreach (var invalid in new[]
        {
            request with { NoteInput = null }, request with { Plugin = null },
            request with { AudioInput = new([new Flow.Audio.PcmAsset(new float[4], 8000)]) },
            Request(Package(layer: "missing")),
            Request(Package(Code.Replace("(dawNoteSequence notes (dawNoteDuration moved))", "(dawInput \"wrong\" 0)")))
        })
        {
            var result = FlowDawGenerator.Build(invalid, TestContext.Current.CancellationToken);
            Assert.Equal(JobStatus.Failed, result.Status); Assert.Null(result.Value);
        }
        var note = request.NoteInput!.Notes[0];
        Assert.Throws<ArgumentException>(() => new PluginNoteInput(4, [note, note]));
        Assert.Throws<ArgumentException>(() => new PluginNoteInput(.5, [note]));
        Assert.Throws<ArgumentException>(() => new PluginNoteInput(4, [note with { Velocity = 2 }]));
        var empty = FlowDawGenerator.Build(request with { NoteInput = new(4, []) }, TestContext.Current.CancellationToken);
        Assert.True(empty.Status == JobStatus.Succeeded, empty.Error); Assert.Empty(Sequence(empty.Value!).Notes);
    }
#if !FLOW_WEB
    [Fact]
    public async Task RealWorkerPreservesNoteMetadataAndReturnsExactDetachedResult()
    {
        var request = Request(); var direct = FlowDawGenerator.Build(request, TestContext.Current.CancellationToken);
        Assert.True(direct.Status == JobStatus.Succeeded, direct.Error);
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        var isolated = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(isolated.Status == JobStatus.Succeeded, isolated.Error);
        Assert.Equal(GeneratedContentJson.Serialize(direct.Value!), GeneratedContentJson.Serialize(isolated.Value!));
        Assert.Same(request.Context, isolated.Value!.Context); Assert.Null(worker.ProcessId);
    }
#endif
}
