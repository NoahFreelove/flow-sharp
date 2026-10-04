using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;
namespace FlowLang.Tests.StudioModel;
[Collection("FlowScripts")]
public class SampleFlowExportTests
{
    [Fact]
    public void PublicSampleConstructionAndProjectExportPreserveAssetsAndAudio()
    {
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var asset = new PcmAsset([0, 0, 1, -1, .5f, -.5f, 0, 0], 8000);
        engine.Context.DeclareVariable("sample", new Value(asset, DawAudioType.Instance));
        var result = engine.Evaluate("""
            use "@flowDaw"
            AudioGraph pitch = (dawInput "pitch" 0)
            AudioGraph gate = (dawInput "gate" 1)
            AudioGraph reader = (dawSample "reader" pitch gate 7 440.0)
            Dict<Int, DawAudio> assets = (dict 7 sample)
            (dawSampleInstrument reader 4 assets)
            """);
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var settings = result.LastValue!.As<SineVoiceSettings>();
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "saved");
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [], instrumentLayers: [new("sampler", settings)])));
        string code = FlowProjectExporter.Export(doc.Snapshot); Assert.Contains("dawSampleInstrument", code);
        var exported = engine.Evaluate(code); Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
        var restored = exported.LastValue!.As<ProjectSnapshot>();
        Assert.Equal(ProjectJson.Serialize(doc.Snapshot), ProjectJson.Serialize(restored));
        ScheduledNote[] notes = [new(Guid.NewGuid(), Guid.NewGuid(), 0, 8, 220)];
        var a = new PreparedNotePlayback(notes, 8000, 8, settings: settings);
        var b = new PreparedNotePlayback(notes, 8000, 8, settings: restored.Sources.Values.Single().Result.InstrumentLayers.Single().Instrument);
        float[] expected = new float[16], actual = new float[16]; a.Read(expected); b.Read(actual);
        Assert.Equal(expected, actual); Assert.Contains(actual, x => x != 0);
    }
}
