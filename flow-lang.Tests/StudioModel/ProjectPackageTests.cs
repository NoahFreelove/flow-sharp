using Flow.Studio.Engine;
using Flow.Audio.Graph;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class ProjectPackageTests
{
    private static void Wave(string path, short sample)
    {
        using var stream = File.Create(path); using var w = new BinaryWriter(stream);
        w.Write(0x46464952u); w.Write(38u); w.Write(0x45564157u); w.Write(0x20746d66u); w.Write(16u);
        w.Write((ushort)1); w.Write((ushort)1); w.Write(8000u); w.Write(16000u); w.Write((ushort)2); w.Write((ushort)16);
        w.Write(0x61746164u); w.Write(2u); w.Write(sample);
    }
    private static ProjectSnapshot Snapshot(params AudioAssetReference[] assets)
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter), assets: assets));
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "saved graph");
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [], graphLayers:
            [new("mix", AudioGraphDefinition.Input("input"))])));
        var graph = doc.Snapshot.Sources.Values.Single().Bindings.Single().Id; var track = Guid.NewGuid();
        return new(new(doc.Snapshot.Arrangement.Id, tempo, meter, audioClips:
            [new(Guid.NewGuid(), track, assets[0].Id, 0, 0, assets[0].Frames, assets[0].SampleRate)]),
            doc.Snapshot.Context, doc.Snapshot.Sources.Values, new([new(track, "Audio")], graph), assets);
    }
    [Fact]
    public void PackageMovesIndependentlyAndDeduplicatesVerifiedAudio()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "original"); Directory.CreateDirectory(source); Wave(Path.Combine(source, "tone.wav"), 16384);
            var a = AudioAssetFiles.Inspect(source, "tone.wav", Guid.NewGuid()).Reference;
            var b = new AudioAssetReference(Guid.NewGuid(), a.RelativePath, a.Sha256, a.SampleRate, a.Frames);
            var snapshot = Snapshot(a, b); var package = Path.Combine(root, "package");
            var file = ProjectPackage.Create(snapshot, source, package);
            Assert.Equal("tone.wav", snapshot.Assets[0].RelativePath);
            Assert.Single(Directory.GetFiles(Path.Combine(package, "audio-assets")));
            Directory.Delete(source, true);
            var moved = Path.Combine(root, "moved"); Directory.Move(package, moved);
            var loaded = ProjectFile.Load(Path.Combine(moved, Path.GetFileName(file)));
            var resolved = ProjectAudioAssets.Resolve(loaded.Snapshot, moved);
            Assert.Empty(resolved.Diagnostics); Assert.Equal(2, resolved.Assets.Count);
            var samples = new float[2]; resolved.Assets[a.Id].CopyTo(samples); Assert.Equal(new float[] { .5f, .5f }, samples);
            var played = new float[2];
            ProjectCompiler.Prepare(loaded.Snapshot, 8000, 8, externalAssets: resolved.Assets).Playback.Read(played);
            Assert.Equal(samples, played);
            Assert.Equal(a.Id, loaded.Snapshot.Assets[0].Id); Assert.Equal(a.Sha256, loaded.Snapshot.Assets[0].Sha256);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void ChangedMissingBudgetAndExistingDestinationNeverPublishPartialPackage()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "tone.wav"); Wave(source, 16384);
            var asset = AudioAssetFiles.Inspect(root, "tone.wav", Guid.NewGuid()).Reference;
            var snapshot = Snapshot(asset); var target = Path.Combine(root, "package");
            Assert.Throws<InvalidDataException>(() => ProjectPackage.Create(snapshot, root, target, 1));
            Assert.False(Directory.Exists(target));
            Wave(source, 8192);
            Assert.Throws<InvalidDataException>(() => ProjectPackage.Create(snapshot, root, target));
            Assert.False(Directory.Exists(target)); File.Delete(source);
            Assert.Throws<FileNotFoundException>(() => ProjectPackage.Create(snapshot, root, target));
            Assert.Throws<OperationCanceledException>(() => ProjectPackage.Create(snapshot, root, target, cancellation: new(true)));
            Assert.Empty(Directory.GetDirectories(root, ".flow-package-*"));
            Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "keep"), "original");
            Assert.Throws<IOException>(() => ProjectPackage.Create(snapshot, root, target));
            Assert.Equal("original", File.ReadAllText(Path.Combine(target, "keep")));
        }
        finally { Directory.Delete(root, true); }
    }
}
