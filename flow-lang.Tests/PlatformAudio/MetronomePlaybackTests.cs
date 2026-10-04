using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Engine;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class MetronomePlaybackTests
{
    private static float[] Render(PreparedNotePlayback playback)
    {
        var samples = new float[playback.TotalFrames * 2];
        for (int offset = 0; offset < samples.Length; offset += playback.MaxBlockFrames * 2)
            playback.Read(samples.AsSpan(offset, Math.Min(playback.MaxBlockFrames * 2, samples.Length - offset)));
        return samples;
    }

    [Fact]
    public void CountInClicksMatchRebuiltFlowGraphAndRemainSilentBetweenBeats()
    {
        var schedule = MetronomeSchedule.CountIn(new([new(0, 120)]), new([new(1, 3, 4)]), 0, 1, 8000);
        var prepared = MetronomePlayback.Prepare(schedule.Beats, schedule.Frames, 8000, 32);
        var samples = Render(prepared);
        Assert.Equal(12000, prepared.TotalFrames);
        Assert.Contains(samples[..400], value => value != 0);
        Assert.All(samples[400..8000], value => Assert.Equal(0, value));
        Assert.Contains(samples[8000..8400], value => value != 0);
        Assert.False(samples[..400].SequenceEqual(samples[8000..8400])); // Accent changes pitch.
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var evaluated = engine.Evaluate(FlowGraphExporter.Export(MetronomePlayback.InstrumentGraph()));
        Assert.True(evaluated.Succeeded, string.Join("\n", evaluated.Errors));
        Assert.Equal(samples, Render(MetronomePlayback.Prepare(schedule.Beats, schedule.Frames,
            evaluated.LastValue!.As<AudioGraphDefinition>(), 8000, 32)));
        prepared.Seek(4000); var block = new float[64]; prepared.Read(block);
        Assert.Equal(samples[8000..8064], block);
        Assert.All(Render(MetronomePlayback.Prepare(schedule.Beats, schedule.Frames, 8000, 32, 0)), value => Assert.Equal(0, value));
    }
}
