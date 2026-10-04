using System.Text.Json;
using Flow.Studio.Model;
using FlowLang.Hosting;
using FlowLang.Music;
using Xunit;

namespace FlowLang.Tests.StudioModel;

[Collection("FlowScripts")]
public class GeneratorNativePolicyTests
{
    [Fact]
    public async Task BundledAndInlineStylesWorkWithoutDiscoveringAmbientPacks()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "improv", "styles");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "unexpected-" + Guid.NewGuid() + ".flow");
        try
        {
            await File.WriteAllTextAsync(file, "use \"@improv\"\n(registerStyle #ambient (dict #test 1))", TestContext.Current.CancellationToken);
            using var engine = new FlowLang.Core.FlowEngine(new FlowLang.Core.EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
            GeneratorStyles.Install(engine.Context, TestContext.Current.CancellationToken);
            Assert.Equal(new[] { "blues", "classical", "jazz" }, engine.Context.StyleRegistry.Keys.Select(k => k.As<string>()).OrderBy(n => n));
            Assert.True(engine.Evaluate("use \"@improv\"\n(registerStyle #inline (dict #test 1))\n(listStyles)").Succeeded);
            Assert.Equal(4, engine.Context.StyleRegistry.Count);
            Assert.DoesNotContain(engine.Context.StyleRegistry.Keys, key => key.As<string>() == "ambient");

            var request = Request("use \"@improv\"\n(registerStyle #inline (dict #test 1))");
            request = request with { Source = request.Source.Replace("| A4q C5q E5h |", "(jam | Cmaj7 Am7 Dm7 G7 | #jazz 4 \"Cmajor\" 42 2)") };
            var first = FlowDawGenerator.Build(request);
            var second = FlowDawGenerator.Build(request);
            Assert.True(first.Status == JobStatus.Succeeded, first.Error);
            Assert.True(second.Status == JobStatus.Succeeded, second.Error);
            double[] Notes(GeneratedSourceOutput output) => output.ScoreLayers[0].Composition.Placements
                .SelectMany(p => p.Section.Sequences).SelectMany(s => s.Notes).Select(n => n.Pitch!.FrequencyHz).ToArray();
            Assert.NotEmpty(Notes(first.Value!)); Assert.Equal(Notes(first.Value!), Notes(second.Value!));
#if !FLOW_WEB
            await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
            var isolated = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
            Assert.True(isolated.Status == JobStatus.Succeeded, isolated.Error);
            Assert.Equal(Notes(first.Value!), Notes(isolated.Value!));
#endif
        }
        finally { File.Delete(file); }
    }

    private static GeneratorBuildRequest Request(string prefix) => new(new(1, Guid.NewGuid(), "generate"), 1,
        prefix + "\n" + FlowDawGenerator.Template,
        new(0, 7, new([new(0, 120)]), new([new(1, 4, 4)])), TimeSpan.FromSeconds(10));
    [Fact]
    public async Task FileWriteIsDeniedEvenWithUserDeclaredNativeSurface()
    {
        string path = Path.Combine(Path.GetTempPath(), "flow-native-denied-" + Guid.NewGuid() + ".wav");
        var request = Request($$"""
            use "@flowDaw"
            internal proc writeWav (String: filepath, Buffer: buffer)
            use "@audio"
            (writeWav {{JsonSerializer.Serialize(path)}} (createBuffer 8 2 8000))
            """);
        var direct = FlowDawGenerator.Build(request);
        Assert.Equal(JobStatus.Failed, direct.Status);
        Assert.Contains("unavailable in this host", direct.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(path));
#if !FLOW_WEB
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        var isolated = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Failed, isolated.Status);
        Assert.Contains("unavailable in this host", isolated.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(path));
#else
        await Task.CompletedTask;
#endif
    }
    [Theory]
    [InlineData("(loadWav \"/nonexistent.wav\")")]
    [InlineData("(play (createBuffer 8 2 8000))")]
    [InlineData("(tts \"test\")")]
    public void FileAndDeviceCallsFailBeforeTheirImplementation(string call)
    {
        var result = FlowDawGenerator.Build(Request("use \"@audio\"\n" + call));
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Contains("unavailable in this host", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task FormantAudioMatchesBetweenDirectAndIsolatedGenerators()
    {
        var request = Request("") with { Source = """
            use "@flowDaw"
            use "@audio"
            proc generate (Dict<String, Double>: context)
                (dawResult "voice" (dawAudio (pan (sing "ah" A4 100ms) 0.0)))
            end proc
            """ };
        var direct = FlowDawGenerator.Build(request);
        Assert.True(direct.Status == JobStatus.Succeeded, direct.Error);
        var pcm = direct.Value!.AudioLayers.Single().Asset;
        var samples = new float[checked((int)pcm.Frames * 2)]; pcm.CopyTo(samples);
        Assert.Equal(4410, pcm.Frames); Assert.Contains(samples, sample => sample != 0);
#if !FLOW_WEB
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        var isolated = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(isolated.Status == JobStatus.Succeeded, isolated.Error);
        var actual = new float[samples.Length]; isolated.Value!.AudioLayers.Single().Asset.CopyTo(actual);
        Assert.Equal(samples, actual);
#else
        await Task.CompletedTask;
#endif
    }
    [Fact]
    public void SynthesizedSongRenderingWorksButAmbientSampleLoadingIsExplicitlyRejected()
    {
        string source = """
            use "@flowDaw"
            use "@audio"
            section part { Sequence notes = | A4s | }
            Song song = [part]
            (renderSong song "sine")
            """;
        var valid = FlowDawGenerator.Build(Request(source));
        Assert.True(valid.Status == JobStatus.Succeeded, valid.Error);
        var unavailable = FlowDawGenerator.Build(Request(source.Replace("\"sine\"", "\"piano\"")));
        Assert.Equal(JobStatus.Failed, unavailable.Status);
        Assert.Contains("host-provided assets", unavailable.Error);
    }
}
