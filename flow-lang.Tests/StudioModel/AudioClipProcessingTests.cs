using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flow.Audio;
using Flow.Studio.Model;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

[Collection("FlowScripts")]
public class AudioClipProcessingTests
{
    internal static PluginPackage Package()
    {
        const string code = """
            use "@flowDaw"
            proc process (Dict<String, Double>: context, Buffers: inputs)
                AudioGraph level = (dawGain "level" (dawInput "input" 0) (get context "parameter:gain"))
                (dawResult "processed" (dawAudio (dawProcess level inputs)))
            end proc
            """;
        var manifest = PluginManifest.Parse(JsonSerializer.Serialize(new
        {
            ApiVersion = 1, Id = "example.clip-gain", Name = "Clip gain", Author = "Local", License = "Private", Version = "1",
            SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code))), Builder = "process",
            Kind = "OfflineAudio", AudioInputs = 1, AudioOutputs = 1, NoteInput = false, NoteOutput = false,
            MinimumSampleRate = 8000, MaximumSampleRate = 96000, MaximumBlockFrames = 256,
            StateSchema = "1", StatePolicy = "Reset",
            Parameters = new[] { new { Id = "gain", Name = "Gain", Unit = "linear", Minimum = 0, Maximum = 2,
                Default = .5, Scale = "Linear", SmoothingMilliseconds = 0, RequiresRebuild = true, Labels = Array.Empty<string>() } },
            Dependencies = Array.Empty<PluginDependency>()
        }));
        return new(manifest, code, "processed", []);
    }
    internal static (ProjectDocument Document, AudioClip Clip) Create()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var context = new GenerationContext(0, 7, tempo, meter, new Dictionary<string, double> { ["gain"] = 1.8 });
        var track = Guid.NewGuid();
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), context, routing: new([new(track, "Audio")], null)));
        var descriptor = new GeneratorDescriptor(1, Guid.NewGuid(), "generate");
        var ticket = doc.BeginBuild(descriptor, "captured");
        Assert.True(doc.Accept(ticket, new(descriptor.SourceId, ticket.Revision, context, [],
            [new("audio", new PcmAsset(new float[] { .1f, .2f, .3f, .4f, .5f, .6f }, 8000))],
            [new("mix", Flow.Audio.Graph.AudioGraphDefinition.Input("input"))])));
        var bindings = doc.Snapshot.Sources[descriptor.SourceId].Bindings;
        var binding = bindings.Single(b => b.Output.Role == GeneratedRole.Audio).Id;
        var graph = bindings.Single(b => b.Output.Role == GeneratedRole.Graph).Id;
        var clip = new AudioClip(Guid.NewGuid(), track, binding, 2, 1, 4, 8000, new(3));
        doc.Edit("Place", p => new(new(p.Arrangement.Id, tempo, meter, audioClips: [clip,
            new(Guid.NewGuid(), track, binding, 4, 0, 3, 8000)]), context, p.Sources.Values, new(p.Routing.Tracks, graph)));
        return (doc, clip);
    }
    private static GeneratedSourceOutput Build(AudioClipProcessingOperation operation)
    {
        var result = FlowDawGenerator.Build(new(operation.Descriptor, 0, operation.Package.Source, operation.Context,
            TimeSpan.FromSeconds(10), operation.Package, operation.Input), TestContext.Current.CancellationToken);
        Assert.True(result.Status == JobStatus.Succeeded, result.Error); return result.Value!;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturedApplyPreservesPlacementOtherClipsAndContextWithExactUndoRedo(bool withEnvelope)
    {
        var (doc, clip) = Create(); ProjectRenderCommands.Set(doc, new(22050, 128));
        if (withEnvelope) ProjectClipEnvelopeCommands.Set(doc, clip.Id, .5, 1, 1);
        var before = doc.Snapshot; int count = doc.History.UndoCount;
        var operation = AudioClipProcessingOperation.Capture(doc, clip.Id, Package(), new Dictionary<string, double> { ["gain"] = .25 });
        Assert.NotSame(before.Context, operation.Context);
        Assert.True(operation.Accept(Build(operation)));
        var after = doc.Snapshot; var replacement = after.Arrangement.AudioClips[0];
        Assert.Equal(before.RenderSettings, after.RenderSettings);
        Assert.Same(before.Context, after.Context); Assert.Same(before.Arrangement.AudioClips[1], after.Arrangement.AudioClips[1]);
        Assert.Equal(clip.Id, replacement.Id); Assert.Equal(clip.Nudge, replacement.Nudge); Assert.Equal(clip.AnchorQuarters, replacement.AnchorQuarters);
        Assert.Equal(0, replacement.SourceOffsetFrames); Assert.Equal(4, replacement.LengthFrames);
        var source = after.Sources[operation.Descriptor.SourceId]; Assert.Equal(.25, source.PluginValues["gain"]);
        var samples = new float[8]; source.Result.AudioLayers[0].Asset.CopyTo(samples);
        Assert.Equal(withEnvelope ? new float[] { 0, 0, .0625f, .075f, 0, 0, 0, 0 } :
            new float[] { .075f, .1f, .125f, .15f, 0, 0, 0, 0 }, samples);
        Assert.Null(replacement.Envelope);
        Assert.Equal(count + 1, doc.History.UndoCount);
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(after, doc.Snapshot);
        Assert.False(operation.Accept(source.Result));
        string path = Path.Combine(Path.GetTempPath(), $"clip-process-{Guid.NewGuid():N}.flowproject");
        try
        {
            ProjectFile.Save(path, doc); var loaded = ProjectFile.Load(path).Snapshot;
            Assert.Equal(.25, loaded.Sources[source.Descriptor.SourceId].PluginValues["gain"]);
            Assert.Equal(replacement, loaded.Arrangement.AudioClips[0]);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void InvalidResultIsAtomicAndDefaultsAreIndependentOfProjectParameters()
    {
        var (doc, clip) = Create(); var before = doc.Snapshot; int count = doc.History.UndoCount;
        var operation = AudioClipProcessingOperation.Capture(doc, clip.Id, Package());
        Assert.Equal(.5, operation.Context.Parameters["gain"]);
        var valid = Build(operation);
        var empty = new GeneratedSourceOutput(operation.Descriptor.SourceId, 0, operation.Context, [],
            [new("processed", new PcmAsset([], 8000))]);
        Assert.Throws<ArgumentException>(() => operation.Accept(empty));
        var wrongContext = new GeneratedSourceOutput(operation.Descriptor.SourceId, 0, before.Context, [], valid.AudioLayers);
        Assert.Throws<ArgumentException>(() => operation.Accept(wrongContext));
        Assert.Same(before, doc.Snapshot); Assert.Equal(count, doc.History.UndoCount); Assert.True(operation.IsCurrent);
        Assert.True(operation.Accept(valid));
    }
    [Fact]
    public void EditThenUndoCannotReviveAnOperationAndCancelDoesNotEditHistory()
    {
        var (doc, clip) = Create(); var before = doc.Snapshot;
        var operation = AudioClipProcessingOperation.Capture(doc, clip.Id, Package()); var result = Build(operation);
        doc.Edit("Transient", p => new(p.Arrangement, p.Context, p.Sources.Values, p.Routing));
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.False(operation.IsCurrent); Assert.False(operation.Accept(result));
        var cancelled = AudioClipProcessingOperation.Capture(doc, clip.Id, Package()); int count = doc.History.UndoCount;
        cancelled.Cancel(); Assert.False(cancelled.IsCurrent); Assert.Equal(count, doc.History.UndoCount);
        Assert.Throws<ArgumentException>(() => AudioClipProcessingOperation.Capture(doc, clip.Id, Package(), new Dictionary<string, double> { ["unknown"] = 1 }));
    }
}
