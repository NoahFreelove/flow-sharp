using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using Flow.Music.Model;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class AudioClipPlaybackTests
{
    [Fact]
    public void AssetOwnsInputAndAudioWindowKeepsExactSamples()
    {
        float[] data = [1, -1, 2, -2, 3, -3, 4, -4];
        var asset = new PcmAsset(data, 8000); data[2] = 99;
        var playback = new PreparedPcmPlayback([new(Guid.NewGuid(), asset, 2, 5, 1, 3)], 8000, 16);
        var output = new float[14]; playback.Read(output);
        Assert.Equal(new float[] { 0, 0, 0, 0, 2, -2, 3, -3, 4, -4, 0, 0, 0, 0 }, output);
        playback.Seek(3); playback.Read(output.AsSpan(0, 4));
        Assert.Equal(new float[] { 3, -3, 4, -4 }, output[..4]);
    }
    [Fact]
    public void NegativeStartAndShortenedAssetProduceCorrectCropAndSilence()
    {
        var asset = new PcmAsset([1, 1, 2, 2, 3, 3], 8000);
        var playback = new PreparedPcmPlayback([new(Guid.NewGuid(), asset, -1, 4, 0, 5)], 8000, 8);
        var output = new float[8]; playback.Read(output);
        Assert.Equal(new float[] { 2, 2, 3, 3, 0, 0, 0, 0 }, output);
    }
    [Fact]
    public void LinearConversionHasExplicitDurationAndWindowBoundary()
    {
        var asset = new PcmAsset([0, 0, 1, 1, 2, 2], 8000);
        var playback = new PreparedPcmPlayback([new(Guid.NewGuid(), asset, 0, 6, 0, 3)], 16000, 8);
        var output = new float[12]; playback.Read(output);
        Assert.Equal(new float[] { 0, 0, 0.5f, 0.5f, 1, 1, 1.5f, 1.5f, 2, 2, 1, 1 }, output);
    }
    [Fact]
    public void SplitAudioAcrossTempoChangePreservesSamplesAtMatchingRate()
    {
        var assetId = Guid.NewGuid(); var track = Guid.NewGuid();
        var asset = new PcmAsset(Enumerable.Range(0, 64000).Select(i => (float)Math.Sin(i * 0.001)).ToArray(), 8000);
        var tempo = new ProjectTempoMap([new(0, 120), new(2, 60)]);
        var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var clip = new AudioClip(Guid.NewGuid(), track, assetId, 1, 0, 32000, 8000, new(25));
        var (left, right) = ClipOperations.SplitFrames(clip, 16000, tempo, Guid.NewGuid());
        PreparedArrangement Make(params AudioClip[] clips) => ArrangementCompiler.Prepare(
            new(Guid.NewGuid(), tempo, meter, audioClips: clips), new Dictionary<(Guid, string), CompositionSnapshot>(),
            [track], AudioGraphDefinition.Input("track"), 8000, 128, audioAssets: new Dictionary<Guid, PcmAsset> { [assetId] = asset });
        var whole = Make(clip); var split = Make(left, right);
        Assert.Equal(whole.Playback.TotalFrames, split.Playback.TotalFrames);
        var a = new float[256]; var b = new float[256];
        while (whole.Playback.PositionFrames < whole.Playback.TotalFrames)
        {
            whole.Playback.Read(a); split.Playback.Read(b); Assert.Equal(a, b);
        }
    }
    [Fact]
    public void MissingAudioHasDiagnosticAndRetainsVisibleDuration()
    {
        var track = Guid.NewGuid();
        var clip = new AudioClip(Guid.NewGuid(), track, Guid.NewGuid(), 0, 0, 8000, 8000);
        var result = ArrangementCompiler.Prepare(new(Guid.NewGuid(), new([new(0, 120)]), new([new(1, 4, 4)]), audioClips: [clip]),
            new Dictionary<(Guid, string), CompositionSnapshot>(), [track], AudioGraphDefinition.Input("track"), 8000, 128);
        Assert.Contains(result.Diagnostics, d => d.Code == "missing-audio-asset");
        Assert.Equal(8000, result.Playback.TotalFrames);
    }
    [Fact]
    public void OverlapBudgetAndAllocationFreeSeekAreEnforced()
    {
        var asset = new PcmAsset(new float[2000], 8000);
        var clips = Enumerable.Range(0, 8).Select(i => new ScheduledAudioClip(Guid.NewGuid(), asset, i, 1000, 0, 1000)).ToArray();
        Assert.Throws<ArgumentException>(() => new PreparedPcmPlayback(clips, 8000, 128, maxOverlap: 4));
        var playback = new PreparedPcmPlayback(clips, 8000, 128);
        var output = new float[256];
        for (int i = 0; i < 20; i++) { playback.Seek(300); playback.Read(output); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { playback.Seek(300); playback.Read(output); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Fact]
    public void FlowBufferCanBecomeAnImmutableClipAsset()
    {
        using var engine = new FlowLang.Core.FlowEngine(new FlowLang.Core.EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var buffer = new FlowLang.StandardLibrary.Audio.AudioBuffer(2, 2, 8000);
        Array.Fill(buffer.Data, 0.25f);
        engine.Context.DeclareVariable("rendered", FlowLang.TypeSystem.SpecialTypes.MusicValue.Buffer(buffer));
        var result = engine.Evaluate("use \"@flowDaw\"; DawAudio asset = (dawAudio rendered); asset");
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var asset = result.LastValue!.As<PcmAsset>();
        Array.Clear(buffer.Data);
        var copy = new float[4]; asset.CopyTo(copy);
        Assert.All(copy, value => Assert.Equal(0.25f, value));
    }

    [Fact]
    public void MixedNoteAndAudioSourcesShareATrackWithoutChangingEither()
    {
        var notes = new PreparedNotePlayback([new(Guid.NewGuid(), Guid.NewGuid(), 0, 10, 440)], 8000, 16, settings: new(1, 0, 0));
        var reference = new PreparedNotePlayback(notes.Notes, 8000, 16, settings: new(1, 0, 0));
        var asset = new PcmAsset(Enumerable.Repeat(0.25f, 20).ToArray(), 8000);
        var mixed = new PreparedMixedPlayback(notes, new PreparedPcmPlayback([new(Guid.NewGuid(), asset, 0, 10, 0, 10)], 8000, 16));
        var expected = new float[20]; reference.Read(expected);
        for (int i = 0; i < expected.Length; i++) expected[i] += 0.25f;
        var actual = new float[20]; mixed.Read(actual);
        Assert.Equal(expected, actual);
        mixed.Seek(0); mixed.Read(actual); Assert.Equal(expected, actual);
    }
}
