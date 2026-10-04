using Flow.Audio;
using Flow.Audio.Graph;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class SampleGraphTests
{
    private static AudioGraphDefinition Definition() => AudioGraphDefinition.Sample("sample", AudioGraphDefinition.Input("pitch"), AudioGraphDefinition.Input("gate", 1), 7);
    [Fact]
    public void StereoSampleInterpolatesPitchAndRetriggersOnlyOnGateEdges()
    {
        var asset = new PcmAsset([0, 0, 1, -1, 0, 0, .5f, -.5f], 8000);
        var graph = new PreparedAudioGraph(Definition(), 8000, 8, samples: new Dictionary<int, PcmAsset> { [7] = asset });
        float[] input = Enumerable.Repeat(220f, 16).Concat(Enumerable.Repeat(1f, 16)).ToArray();
        float[] output = new float[16]; graph.Process(input, output);
        float[] expected = [0, .5f, 1, .5f, 0, .25f, .5f, .25f];
        for (int i = 0; i < 8; i++) { Assert.Equal(expected[i], output[i * 2]); Assert.Equal(-expected[i], output[i * 2 + 1]); }
        graph.Process(input, output); Assert.All(output, x => Assert.Equal(0, x)); // Held gate does not loop.
        input.AsSpan(16).Clear(); graph.Process(input, output);
        input.AsSpan(16).Fill(1); graph.Process(input, output); Assert.Equal(1, output[4]);
        graph.Reset(); input.AsSpan(16).Clear(); graph.Process(input, output); Assert.All(output, x => Assert.Equal(0, x));
        input.AsSpan(16).Fill(1); graph.Process(input, output);
        long before = GC.GetAllocatedBytesForCurrentThread(); graph.Process(input, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Fact]
    public void MissingAssetsRejectAndGraphIdentityRoundTripsWithoutEmbeddingAudio()
    {
        var definition = Definition();
        Assert.Throws<ArgumentException>(() => new PreparedAudioGraph(definition));
        Assert.Equal(AudioGraphJson.Serialize(definition), AudioGraphJson.Serialize(AudioGraphJson.Deserialize(AudioGraphJson.Serialize(definition))));
        Assert.Throws<ArgumentException>(() => new AudioGraphNode("sample", "flow.sample", 1, ["pitch", "gate"],
            new Dictionary<string, double> { ["asset"] = .5 }));
    }

    [Fact]
    public void GraphInstrumentSamplesAreImmutableAndSurviveSavedContent()
    {
        var asset = new PcmAsset([0, 0, 1, -1, .5f, -.5f, .25f, -.25f], 8000);
        var bindings = new Dictionary<int, PcmAsset> { [7] = asset };
        var set = new GraphSampleSet(bindings); bindings.Clear(); Assert.Same(asset, set.Assets[7]);
        var settings = new SineVoiceSettings(VoiceLimit: 2, VoiceGraph: Definition(), GraphSamples: set);
        var tempo = new Flow.Studio.Model.ProjectTempoMap([new(0, 120)]);
        var meter = new Flow.Studio.Model.ProjectMeterMap([new(1, 4, 4)]);
        var context = new Flow.Studio.Model.GenerationContext(0, 1, tempo, meter);
        Guid source = Guid.NewGuid();
        var output = new Flow.Studio.Model.GeneratedSourceOutput(source, 1, context, [], instrumentLayers: [new("sampler", settings)]);
        string json = Flow.Studio.Model.GeneratedContentJson.Serialize(output);
        var restored = Flow.Studio.Model.GeneratedContentJson.Deserialize(json, source, 1, context).InstrumentLayers.Single().Instrument;
        Assert.Equal(json, Flow.Studio.Model.GeneratedContentJson.Serialize(new(source, 1, context, [], instrumentLayers: [new("sampler", restored)])));
        ScheduledNote[] notes = [new(Guid.NewGuid(), Guid.NewGuid(), 0, 8, 220)];
        var originalPlayback = new PreparedNotePlayback(notes, 8000, 8, settings: settings);
        var restoredPlayback = new PreparedNotePlayback(notes, 8000, 8, settings: restored);
        float[] original = new float[16], actual = new float[16]; originalPlayback.Read(original); restoredPlayback.Read(actual);
        Assert.Equal(original, actual); Assert.Contains(actual, x => x != 0);
        Assert.Throws<ArgumentException>(() => (settings with { GraphSamples = null }).Validate());
        Assert.Throws<ArgumentException>(() => new GraphSampleSet([new(256, asset)]));
        Assert.Throws<ArgumentException>(() => new GraphSampleSet([new(7, asset), new(7, asset)]));
        var old = System.Text.Json.Nodes.JsonNode.Parse(json)!; old["Version"] = 3;
        Assert.Throws<System.Text.Json.JsonException>(() => Flow.Studio.Model.GeneratedContentJson.Deserialize(old.ToJsonString(), source, 1, context));
    }
}
