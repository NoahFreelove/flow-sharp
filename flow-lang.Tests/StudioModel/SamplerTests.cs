using Flow.Audio;
using Flow.Studio.Model;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.StudioModel;

[Collection("FlowScripts")]
public class SamplerTests
{
    private static PreparedNotePlayback Make(double frequency = 440) => new(
        [new(Guid.NewGuid(), Guid.NewGuid(), 0, 8, frequency, 1)], 8000, 16, settings:
        new(4, 0, 0, new PcmAsset([1, -1, .5f, -.5f, .25f, -.25f, 0, 0], 8000), 440));
    [Fact]
    public void RootPitchPreservesStereoAndOctaveAdvancesTwiceAsFast()
    {
        var root = new float[16]; Make().Read(root);
        Assert.Equal(1, root[0], 5); Assert.Equal(-1, root[1], 5); Assert.Equal(.5f, root[2], 5);
        Assert.All(root[8..], v => Assert.Equal(0, v));
        var octave = new float[16]; Make(880).Read(octave);
        Assert.Equal(.25f, octave[2], 5); Assert.All(octave[4..], v => Assert.Equal(0, v));
        var lower = new float[16]; Make(220).Read(lower); Assert.Equal(.75f, lower[2], 5);
    }
    [Fact]
    public void SeekRetriggersHeldSampleAndCallbackIsAllocationFree()
    {
        var playback = Make(); var output = new float[16]; playback.Read(output);
        playback.Seek(3); playback.Read(output); Assert.Equal(1, output[0], 5);
        for (int i = 0; i < 100; i++) { playback.Reset(); playback.Read(output); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { playback.Reset(); playback.Read(output); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Throws<ArgumentException>(() => new SineVoiceSettings(Sample: new PcmAsset([], 8000)).Validate());
    }
    [Fact]
    public void NaturallyFinishedSampleFreesVoiceBeforeNextOnset()
    {
        var notes = new[] { new ScheduledNote(Guid.NewGuid(), Guid.NewGuid(), 0, 8, 440),
            new ScheduledNote(Guid.NewGuid(), Guid.NewGuid(), 1, 8, 440) };
        var player = new PreparedNotePlayback(notes, 8000, 16, settings: new(1, 0, 0, new PcmAsset([1, 1], 8000)));
        player.Read(new float[16]); Assert.Equal(0, player.StolenVoices);
    }
#if !FLOW_WEB
    [Fact]
    public async Task FlowSamplerSurvivesWorkerAndProjectSaveWithoutLosingAudio()
    {
        const string source = """
            use "@flowDaw"
            use "@audio"
            proc generate (Dict<String, Double>: context)
                Buffer tone = (createSineTone 0.01 440.0 0.25)
                DawAudio sample = (dawAudio tone)
                DawInstrument instrument = (dawSampler sample 440.0 8 0ms 20ms)
                (dawResult "sample" instrument)
            end proc
            """;
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), source);
        var request = new GeneratorBuildRequest(ticket.Descriptor, ticket.Revision, ticket.Code, ticket.Context, TimeSpan.FromSeconds(20));
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        var result = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(result.Status == JobStatus.Succeeded, result.Error);
        Assert.True(doc.Accept(ticket, result.Value!));
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        var settings = restored.Sources.Values.Single().Result.InstrumentLayers.Single().Instrument;
        Assert.NotNull(settings.Sample); Assert.Equal(440, settings.RootFrequencyHz); Assert.Equal(8, settings.VoiceLimit);
        var original = result.Value!.InstrumentLayers.Single().Instrument;
        var notes = new[] { new ScheduledNote(Guid.NewGuid(), Guid.NewGuid(), 0, 512, 440) };
        var a = new PreparedNotePlayback(notes, 48000, 256, settings: original);
        var b = new PreparedNotePlayback(notes, 48000, 256, settings: settings);
        var expected = new float[512]; var actual = new float[512]; a.Read(expected); b.Read(actual);
        Assert.Equal(expected, actual); Assert.Contains(actual, x => Math.Abs(x) > .001);
    }
#endif

}
