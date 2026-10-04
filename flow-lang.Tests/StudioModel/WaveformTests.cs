using Flow.Audio;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class WaveformTests
{
    [Fact]
    public void LevelsPreserveExtremaAndPartialFinalBucket()
    {
        var asset = new PcmAsset([.1f, -.1f, .8f, -.8f, -.5f, .5f, .3f, -.3f, .9f, -.9f], 8000);
        var pyramid = WaveformPyramid.Build(asset, 2);
        Assert.Equal(3, pyramid.Levels.Count);
        Assert.Equal(new WaveformPeak(.1f, .8f, -.8f, -.1f), pyramid.Levels[0].Peaks[0]);
        Assert.Equal(new WaveformPeak(.9f, .9f, -.9f, -.9f), pyramid.Levels[0].Peaks[2]);
        Assert.Equal(new WaveformPeak(-.5f, .9f, -.9f, .5f), pyramid.Levels[^1].Peaks.Single());
        Assert.Equal(8, pyramid.Levels[^1].FramesPerPeak);
    }
    [Fact]
    public void BudgetCancellationAndEmptyAssetsAreHandled()
    {
        var asset = new PcmAsset(new float[20], 8000);
        Assert.Throws<ArgumentException>(() => WaveformPyramid.Build(asset, 1, 16));
        Assert.Throws<OperationCanceledException>(() => WaveformPyramid.Build(asset, cancellation: new(true)));
        Assert.Empty(WaveformPyramid.Build(new PcmAsset([], 8000)).Levels);
    }
}
