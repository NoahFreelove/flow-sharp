using Flow.Audio;
using Flow.Audio.Graph;
using System.Text.Json.Nodes;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.StudioModel;

public class AudioImportTests
{
    private static byte[] Wave(ushort format, ushort channels, ushort bits, byte[] pcm)
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        w.Write(0x46464952u); w.Write((uint)(36 + pcm.Length + (pcm.Length & 1))); w.Write(0x45564157u);
        w.Write(0x20746d66u); w.Write(16u); w.Write(format); w.Write(channels); w.Write(8000u);
        ushort align = (ushort)(channels * bits / 8); w.Write((uint)(8000 * align)); w.Write(align); w.Write(bits);
        w.Write(0x61746164u); w.Write((uint)pcm.Length); w.Write(pcm); if ((pcm.Length & 1) != 0) w.Write((byte)0);
        return stream.ToArray();
    }
    [Theory]
    [InlineData(8)] [InlineData(16)] [InlineData(24)] [InlineData(32)]
    public void IntegerFormatsDecodeNegativeFullScaleAndDuplicateMono(int bits)
    {
        var bytes = new byte[bits / 8]; if (bits != 8) bytes[^1] = 128;
        using var stream = new MemoryStream(Wave(1, 1, (ushort)bits, bytes));
        var asset = WaveAssetReader.Read(stream); var samples = new float[2]; asset.CopyTo(samples);
        Assert.Equal(new float[] { -1, -1 }, samples); Assert.Equal(8000, asset.SampleRate);
    }
    [Fact]
    public void FloatStereoPreservesValuesAndRejectsNonfiniteSamples()
    {
        var bytes = BitConverter.GetBytes(0.25f).Concat(BitConverter.GetBytes(-0.5f)).ToArray();
        var asset = WaveAssetReader.Read(new MemoryStream(Wave(3, 2, 32, bytes)));
        var samples = new float[2]; asset.CopyTo(samples); Assert.Equal(new float[] { .25f, -.5f }, samples);
        Assert.Throws<InvalidDataException>(() => WaveAssetReader.Read(new MemoryStream(Wave(3, 1, 32, BitConverter.GetBytes(float.NaN)))));
    }
    [Fact]
    public void TruncationPartialFramesBudgetAndCancellationAreRejected()
    {
        var bytes = Wave(1, 2, 16, [0, 0, 0, 0]);
        Assert.Throws<InvalidDataException>(() => WaveAssetReader.Read(new MemoryStream(bytes[..^1])));
        Assert.Throws<InvalidDataException>(() => WaveAssetReader.Read(new MemoryStream(Wave(1, 2, 16, [0, 0]))));
        Assert.Throws<InvalidDataException>(() => WaveAssetReader.Read(new MemoryStream(bytes), 4));
        Assert.Throws<OperationCanceledException>(() => WaveAssetReader.Read(new MemoryStream(bytes), cancellation: new(true)));
    }
    [Fact]
    public void AssetHashDetectsChangedAndMissingFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "tone.wav");
        try
        {
            File.WriteAllBytes(path, Wave(1, 1, 16, [0, 64]));
            var imported = AudioAssetFiles.Inspect(directory, "tone.wav", Guid.NewGuid());
            Assert.Equal(1, AudioAssetFiles.Resolve(directory, imported.Reference).Frames);
            File.WriteAllBytes(path, Wave(1, 1, 16, [0, 32]));
            Assert.Throws<InvalidDataException>(() => AudioAssetFiles.Resolve(directory, imported.Reference));
            File.Delete(path);
            Assert.Throws<FileNotFoundException>(() => AudioAssetFiles.Resolve(directory, imported.Reference));
            Assert.Throws<ArgumentException>(() => new AudioAssetReference(Guid.NewGuid(), "../tone.wav", new string('0', 64), 8000, 1));
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void PersistedExternalAssetPlaysAfterReopenAndMissingFileBecomesSilence()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "tone.wav"), Wave(1, 1, 16, [0, 64]));
            var imported = AudioAssetFiles.Inspect(directory, "tone.wav", Guid.NewGuid());
            var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
            var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
            var t = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "graph");
            Assert.True(doc.Accept(t, new(t.Descriptor.SourceId, t.Revision, t.Context, [], graphLayers:
                [new("mix", AudioGraphDefinition.Input("input"))])));
            var graph = doc.Snapshot.Sources.Values.Single().Bindings.Single().Id; var track = Guid.NewGuid();
            doc.Edit("Import audio", p => new(new(p.Arrangement.Id, tempo, meter, audioClips:
                [new(Guid.NewGuid(), track, imported.Reference.Id, 0, 0, 1, 8000)]), p.Context, p.Sources.Values,
                new([new(track, "Audio")], graph), [imported.Reference]));
            var reopened = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
            var resolved = ProjectAudioAssets.Resolve(reopened, directory); Assert.Empty(resolved.Diagnostics);
            var samples = new float[2];
            ProjectCompiler.Prepare(reopened, 8000, 8, externalAssets: resolved.Assets).Playback.Read(samples);
            Assert.Equal(new float[] { .5f, .5f }, samples);
            File.Delete(Path.Combine(directory, "tone.wav"));
            resolved = ProjectAudioAssets.Resolve(reopened, directory); Assert.Single(resolved.Diagnostics);
            var silent = ProjectCompiler.Prepare(reopened, 8000, 8, externalAssets: resolved.Assets);
            silent.Playback.Read(samples); Assert.Equal(new float[] { 0, 0 }, samples);
            Assert.Contains(silent.Diagnostics, d => d.Code == "missing-audio-asset");
            Assert.True(doc.History.Undo()); Assert.Empty(doc.Snapshot.Assets);
            Assert.True(doc.History.Redo()); Assert.Single(doc.Snapshot.Assets);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void VersionOneProjectsMigrateToEmptyExternalAssetList()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var project = new ProjectSnapshot(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter));
        var node = JsonNode.Parse(ProjectJson.Serialize(project))!.AsObject(); node["Version"] = 1; node.Remove("Assets");
        Assert.Empty(ProjectJson.Deserialize(node.ToJsonString()).Assets);
    }

    [Fact]
    public void ManagedImportDeduplicatesAndRelinkIsUndoableWithoutMovingClips()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "input.wav"); File.WriteAllBytes(source, Wave(1, 1, 16, [0, 64, 0, 32]));
            var id = Guid.NewGuid(); var first = ManagedAudioImport.Prepare(source, directory, id);
            var duplicate = ManagedAudioImport.Prepare(source, directory, Guid.NewGuid());
            Assert.Equal(first.Reference.RelativePath, duplicate.Reference.RelativePath);
            Assert.Single(Directory.GetFiles(Path.Combine(directory, "audio-assets")));
            var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
            var track = Guid.NewGuid(); var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter),
                new(0, 1, tempo, meter), routing: new([new(track, "Audio")], null)));
            ProjectAssetCommands.Place(doc, first.Reference, Guid.NewGuid(), track, 16, new(25));
            var clip = doc.Snapshot.Arrangement.AudioClips.Single();
            File.WriteAllBytes(source, Wave(1, 1, 16, [0, 16]));
            var replacement = ManagedAudioImport.Prepare(source, directory, id);
            ProjectAssetCommands.Relink(doc, replacement.Reference);
            Assert.Same(clip, doc.Snapshot.Arrangement.AudioClips.Single());
            Assert.Equal(1, doc.Snapshot.Assets.Single().Frames); Assert.Equal(2, clip.LengthFrames);
            Assert.True(doc.History.Undo()); Assert.Equal(first.Reference, doc.Snapshot.Assets.Single());
            Assert.True(doc.History.Redo()); Assert.Equal(replacement.Reference, doc.Snapshot.Assets.Single());
            File.Delete(source);
            Assert.Equal(1, AudioAssetFiles.Resolve(directory, doc.Snapshot.Assets.Single()).Frames);
            var before = doc.Snapshot;
            Assert.Throws<ArgumentException>(() => ProjectAssetCommands.Relink(doc,
                new(id, replacement.Reference.RelativePath, replacement.Reference.Sha256, 16000, 1)));
            Assert.Same(before, doc.Snapshot);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void FailedManagedImportCleansTemporaryCopy()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "bad.wav"); File.WriteAllBytes(source, [1, 2, 3]);
            Assert.Throws<InvalidDataException>(() => ManagedAudioImport.Prepare(source, directory, Guid.NewGuid()));
            Assert.Empty(Directory.GetFiles(Path.Combine(directory, "audio-assets")));
        }
        finally { Directory.Delete(directory, true); }
    }

}
