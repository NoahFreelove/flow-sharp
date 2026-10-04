using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Engine;
using Flow.Studio.Host;
using Flow.Studio.Model;
using FlowLang.Tests.StudioModel;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

[Collection("FlowScripts")]
public class ProjectStemFilesTests
{
    private static ProjectSnapshot Project(bool nonlinear = false)
    {
        var (doc, clip) = AudioClipProcessingTests.Create(); var second = Guid.NewGuid();
        var source = doc.Snapshot.Sources.Values.Single();
        var graph = AudioGraphDefinition.Mix("mix", AudioGraphDefinition.Input("one", 0), AudioGraphDefinition.Input("two", 1));
        if (nonlinear) graph = graph.Then("master", "flow.tanh");
        doc.Edit("Two tracks", p => new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
            audioClips: p.Arrangement.AudioClips.Append(new(Guid.NewGuid(), second, clip.SourceId, 2, 0, 3, 8000, clip.Nudge))),
            p.Context, p.Sources.Values, new(p.Routing.Tracks.Append(new(second, "../../Untrusted track name", InputBus: 1)), p.Routing.GraphBinding)));
        return ProjectGraphConstruction.Replace(doc.Snapshot, source.Descriptor.SourceId, "mix", graph);
    }
    private static float[] Samples(string path)
    {
        using var stream = File.OpenRead(path); var asset = WaveAssetReader.Read(stream);
        var samples = new float[asset.Frames * 2]; asset.CopyTo(samples); return samples;
    }
    private static float[] Render(ProjectSnapshot snapshot, Guid? solo = null, long minimumFrames = 0)
    {
        var prepared = ProjectCompiler.Prepare(snapshot, 8000, 16, soloTrack: solo, minimumFrames: minimumFrames).Playback;
        var samples = new float[checked((int)prepared.TotalFrames * 2)];
        for (int i = 0; i < samples.Length; i += 32) prepared.Read(samples.AsSpan(i, Math.Min(32, samples.Length - i)));
        return samples;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StemsShareTimelineMatchSoloPlaybackAndDocumentNonlinearMasterSemantics(bool nonlinear)
    {
        var snapshot = Project(nonlinear); var mix = Render(snapshot);
        string root = Path.Combine(Path.GetTempPath(), $"flow-stems-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        string output = Path.Combine(root, "stems");
        try
        {
            var result = await ProjectStemFiles.ExportAsync(snapshot, root, output, 8000, 16, TestContext.Current.CancellationToken);
            Assert.Equal(2, result.Stems.Count); Assert.Equal(mix.Length / 2, result.Frames);
            var sum = new float[mix.Length];
            foreach (var stem in result.Stems)
            {
                Assert.Equal(Path.GetFileName(stem.FileName), stem.FileName);
                var samples = Samples(Path.Combine(output, stem.FileName)); Assert.Equal(mix.Length, samples.Length);
                Assert.Equal(Render(snapshot, stem.TrackId, result.Frames), samples);
                Assert.Contains(samples, value => value != 0);
                for (int i = 0; i < sum.Length; i++) sum[i] += samples[i];
            }
            if (nonlinear) Assert.True(sum.Zip(mix).Any(pair => Math.Abs(pair.First - pair.Second) > .001));
            else Assert.Equal(mix, sum);
            Assert.Contains(ProjectStemFiles.ProcessingMode, File.ReadAllText(Path.Combine(output, "stems.json")));
            Assert.Single(Directory.GetDirectories(root));
            await Assert.ThrowsAsync<IOException>(() => ProjectStemFiles.ExportAsync(snapshot, root, output, 8000, 16, TestContext.Current.CancellationToken));
            Assert.Equal(3, Directory.GetFiles(output).Length);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task SelectedTracksAreCapturedValidatedAndExportedWithoutOtherFiles()
    {
        var snapshot = Project(); var chosen = snapshot.Routing.Tracks[1].Id;
        var selection = new List<Guid> { chosen };
        string root = Path.Combine(Path.GetTempPath(), $"flow-stems-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        try
        {
            string output = Path.Combine(root, "selected");
            var task = ProjectStemFiles.ExportAsync(snapshot, root, output, 8000, 16,
                TestContext.Current.CancellationToken, selectedTracks: selection);
            selection.Clear();
            var result = await task;
            Assert.Equal(chosen, Assert.Single(result.Stems).TrackId);
            Assert.Equal(2, Directory.GetFiles(output).Length);
            Assert.Equal(Render(snapshot, chosen, result.Frames), Samples(Path.Combine(output, result.Stems[0].FileName)));
            foreach (var invalid in new[] { Array.Empty<Guid>(), new[] { chosen, chosen }, new[] { Guid.NewGuid() } })
                await Assert.ThrowsAsync<ArgumentException>(() => ProjectStemFiles.ExportAsync(snapshot, root, Path.Combine(root, "invalid"), selectedTracks: invalid));
            Assert.False(Directory.Exists(Path.Combine(root, "invalid")));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task OtherTrackAutonomousInsertDoesNotLeakAndItsTailSetsCommonLength()
    {
        var doc = new ProjectDocument(Project());
        long dryFrames = ProjectCompiler.Prepare(doc.Snapshot, 8000, 16).Playback.TotalFrames;
        var package = PluginBuildTests.Package("""
            use "@flowDaw"
            proc build (Dict<String, Double>: context)
                AudioGraph signal = (dawMix "sum" (dawInput "input" 0) (dawValue "dc" 0.2))
                (dawResult "effect" (dawGain "level" (dawDelay "echo" signal 10ms 2 0.5 0.5) 1.0))
            end proc
            """);
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "build"), package.Source);
        var built = FlowDawGenerator.Build(new(ticket.Descriptor, ticket.Revision, ticket.Code, ticket.Context, TimeSpan.FromSeconds(10), package), TestContext.Current.CancellationToken);
        Assert.True(built.Status == JobStatus.Succeeded, built.Error);
        Assert.True(doc.Accept(ticket, built.Value!, "Effect", p => p, plugin: package));
        var binding = doc.Snapshot.Sources[ticket.Descriptor.SourceId].Bindings.Single().Id;
        var second = doc.Snapshot.Routing.Tracks[1].Id;
        ProjectEffectCommands.Set(doc, [new(binding, second)]);
        ProjectAutomationCommands.Set(doc, new(Guid.NewGuid(), binding, "level", "gain", [new(0, .5)]));
        string root = Path.Combine(Path.GetTempPath(), $"flow-stems-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        try
        {
            var result = await ProjectStemFiles.ExportAsync(doc.Snapshot, root, Path.Combine(root, "stems"), 8000, 16, TestContext.Current.CancellationToken);
            var firstSamples = Samples(Path.Combine(result.Directory, result.Stems[0].FileName));
            var secondSamples = Samples(Path.Combine(result.Directory, result.Stems[1].FileName));
            Assert.Equal(firstSamples.Length, secondSamples.Length);
            Assert.True(result.Frames > dryFrames);
            Assert.Equal(0, firstSamples[0]); Assert.Equal(.05f, secondSamples[0], 6);
            ProjectTrackCommands.SetMuted(doc, second, true);
            var muted = Render(doc.Snapshot);
            Assert.Equal(firstSamples, muted);
            var mutedStem = Render(doc.Snapshot, second, result.Frames);
            Assert.All(mutedStem, value => Assert.Equal(0, value));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task CancelDuringSecondStemRemovesWholeUnpublishedSet()
    {
        var snapshot = Project();
        string root = Path.Combine(Path.GetTempPath(), $"flow-stems-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        try
        {
            using var cancellation = new CancellationTokenSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProjectStemFiles.ExportAsync(snapshot, root, Path.Combine(root, "stems"),
                8000, 16, cancellation.Token, p => { if (p.TrackIndex == 1 && p.Frames > 0) cancellation.Cancel(); }));
            Assert.Empty(Directory.GetFileSystemEntries(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
