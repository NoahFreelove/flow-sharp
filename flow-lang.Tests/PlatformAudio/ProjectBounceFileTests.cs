using Flow.Audio;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Tests.StudioModel;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class ProjectBounceFileTests
{
    [Fact]
    public async Task BounceMatchesPreparedProjectAndKeepsCapturedDocumentUnchanged()
    {
        var (doc, _) = NoteClipProcessingTests.Create();
        long dryFrames = ProjectCompiler.Prepare(doc.Snapshot, 8000, 16).Playback.TotalFrames;
        var graphSource = doc.Snapshot.Sources.Values.Single(s => s.Result.GraphLayers.Count != 0);
        var graph = Flow.Audio.Graph.AudioGraphDefinition.Input("input").Then("level", "flow.gain")
            .Then("echo", "flow.delay", new Dictionary<string, double> { ["timeMs"] = 10, ["repeats"] = 2, ["feedback"] = .5, ["wet"] = 1 });
        doc.Edit("Effects", p => ProjectGraphConstruction.Replace(p, graphSource.Descriptor.SourceId, "mix", graph));
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), doc.Snapshot.Routing.GraphBinding!.Value, "level", "gain", [new(0, .25), new(4, .75)]));
        var snapshot = doc.Snapshot;
        string directory = Path.Combine(Path.GetTempPath(), $"flow-bounce-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "mix.wav");
        try
        {
            long progress = -1;
            var result = await ProjectBounceFile.ExportAsync(snapshot, directory, path, 8000, 16,
                cancellation: TestContext.Current.CancellationToken, progress: (done, total) => { Assert.InRange(done, progress, total); progress = done; });
            using var stream = File.OpenRead(path); var asset = WaveAssetReader.Read(stream);
            Assert.Equal(result.Frames, asset.Frames); Assert.Equal(result.Frames, progress); Assert.Equal(8000, asset.SampleRate);
            Assert.True(result.Frames > dryFrames);
            var expectedPlayback = ProjectCompiler.Prepare(snapshot, 8000, 16).Playback;
            var expected = new float[asset.Frames * 2]; var actual = new float[expected.Length]; asset.CopyTo(actual);
            for (int i = 0; i < expected.Length; i += 32) expectedPlayback.Read(expected.AsSpan(i, Math.Min(32, expected.Length - i)));
            Assert.Equal(expected, actual); Assert.Contains(actual, n => n != 0); Assert.Same(snapshot, doc.Snapshot);
            // Unaligned boundaries exercise both a partly discarded first block
            // and a partial last block, with delay/automation history intact.
            long start = 123, end = result.Frames - 13, rangeProgress = -1;
            string rangePath = Path.Combine(directory, "range.wav");
            var selected = await ProjectBounceFile.ExportRangeAsync(snapshot, directory, rangePath, new(start, end),
                8000, 16, cancellation: TestContext.Current.CancellationToken,
                progress: (done, total) => { Assert.Equal(end, total); Assert.InRange(done, rangeProgress, total); rangeProgress = done; });
            using var rangeStream = File.OpenRead(rangePath); var rangeAsset = WaveAssetReader.Read(rangeStream);
            var rangeSamples = new float[rangeAsset.Frames * 2]; rangeAsset.CopyTo(rangeSamples);
            Assert.Equal(expected.AsSpan((int)start * 2, (int)(end - start) * 2).ToArray(), rangeSamples);
            Assert.Equal(end - start, selected.Frames); Assert.Equal(end, rangeProgress);
            Assert.Same(snapshot, doc.Snapshot);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task CancelledOverwritePreservesExistingFileAndCleansTemporaryOutput()
    {
        var (doc, _) = NoteClipProcessingTests.Create();
        string directory = Path.Combine(Path.GetTempPath(), $"flow-bounce-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "mix.wav"); File.WriteAllText(path, "original");
        try
        {
            using var cancellation = new CancellationTokenSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProjectBounceFile.ExportAsync(doc.Snapshot, directory, path,
                8000, 16, overwrite: true, cancellation.Token, (done, _) => { if (done > 0) cancellation.Cancel(); }));
            Assert.Equal("original", File.ReadAllText(path)); Assert.Single(Directory.GetFiles(directory));
            using var prerollCancellation = new CancellationTokenSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProjectBounceFile.ExportRangeAsync(doc.Snapshot, directory,
                path, new(1000, 2000), 8000, 16, overwrite: true, prerollCancellation.Token,
                (done, _) => { if (done > 0) { Assert.True(done < 1000); prerollCancellation.Cancel(); } }));
            Assert.Equal("original", File.ReadAllText(path)); Assert.Single(Directory.GetFiles(directory));
            await Assert.ThrowsAsync<ArgumentException>(() => ProjectBounceFile.ExportRangeAsync(doc.Snapshot, directory,
                path, new(0, long.MaxValue), 8000, 16, overwrite: true, cancellation: TestContext.Current.CancellationToken));
            Assert.Equal("original", File.ReadAllText(path)); Assert.Single(Directory.GetFiles(directory));
            await Assert.ThrowsAsync<IOException>(() => ProjectBounceFile.ExportAsync(doc.Snapshot, directory, path,
                cancellation: TestContext.Current.CancellationToken));
            Assert.Equal("original", File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void MusicalSelectionUsesTheWholeTempoMapAndRejectsCollapsedRanges()
    {
        var tempo = new ProjectTempoMap([new(0, 120), new(2, 60)]);
        Assert.Equal(new ProjectExportRange(500, 3000), ProjectExportRange.FromQuarters(tempo, 1, 4, 1000));
        Assert.Throws<ArgumentException>(() => ProjectExportRange.FromQuarters(tempo, 0, .00001, 1000));
        Assert.Throws<ArgumentException>(() => new ProjectExportRange(5, 5));
        Assert.Throws<ArgumentException>(() => ProjectExportRange.FromQuarters(tempo, double.NaN, 4, 1000));
    }
    [Fact]
    public async Task MissingClipSourceFailsWithoutPublishingAFile()
    {
        var (doc, clip) = NoteClipProcessingTests.Create();
        doc.Edit("Missing source", p => new(p.Arrangement, p.Context,
            p.Sources.Values.Where(s => s.Descriptor.SourceId != clip.SourceId), p.Routing));
        string directory = Path.Combine(Path.GetTempPath(), $"flow-bounce-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(directory);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => ProjectBounceFile.ExportAsync(doc.Snapshot, directory,
                Path.Combine(directory, "mix.wav"), 8000, 16, cancellation: TestContext.Current.CancellationToken));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }
}
