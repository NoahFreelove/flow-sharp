using Flow.Audio;
using Flow.Studio.Model;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class GeneratorAssetTests
{
    [Fact]
    public async Task CapturedSamplesReturnIdenticalPcmInDirectAndIsolatedBuilds()
    {
        var id = Guid.NewGuid(); float[] samples = [.25f, -.25f, .5f, -.5f];
        var assets = new GeneratorAssets(new Dictionary<Guid, PcmAsset> { [id] = new(samples, 8000) });
        var restored = GeneratorAssets.Deserialize(assets.Serialize());
        var request = new GeneratorBuildRequest(new(1, Guid.NewGuid(), "generate"), 1, $$"""
            use "@flowDaw"
            proc generate (Dict<String, Double>: context)
                (dawResult "sample" (dawAssetSample "{{id}}"))
            end proc
            """, new(0, 7, new([new(0, 120)]), new([new(1, 4, 4)])), TimeSpan.FromSeconds(10), Assets: restored);
        var direct = FlowDawGenerator.Build(request);
        Assert.True(direct.Status == JobStatus.Succeeded, direct.Error);
        float[] actual = new float[4]; direct.Value!.AudioLayers.Single().Asset.CopyTo(actual);
        Assert.Equal(samples, actual);
#if !FLOW_WEB
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        var isolated = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(isolated.Status == JobStatus.Succeeded, isolated.Error);
        isolated.Value!.AudioLayers.Single().Asset.CopyTo(actual); Assert.Equal(samples, actual);
#else
        await Task.CompletedTask;
#endif
        var unavailable = FlowDawGenerator.Build(request with { Assets = null });
        Assert.Equal(JobStatus.Failed, unavailable.Status); Assert.Contains("not supplied", unavailable.Error);
    }
    [Fact]
    public void AssetSetCapturesIdentityAndEnforcesBounds()
    {
        var id = Guid.NewGuid(); var pcm = new PcmAsset([.1f, .2f], 8000);
        var input = new Dictionary<Guid, PcmAsset> { [id] = pcm }; var captured = new GeneratorAssets(input);
        input.Clear(); Assert.Same(pcm, captured.Samples[id]);
        Assert.Throws<ArgumentException>(() => new GeneratorAssets([new(Guid.Empty, pcm)]));
        Assert.Throws<ArgumentException>(() => new GeneratorAssets([new(id, pcm), new(id, pcm)]));
        Assert.Throws<ArgumentException>(() => new GeneratorAssets(Enumerable.Range(0, 33).Select(_ => new KeyValuePair<Guid, PcmAsset>(Guid.NewGuid(), pcm))));
    }
}
