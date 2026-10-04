using Flow.Studio.Model;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class MetronomeScheduleTests
{
    [Fact]
    public void PlaybackBeatsFollowTempoAndMeterWithHalfOpenSelection()
    {
        var tempo = new ProjectTempoMap([new(0, 120), new(2, 60)]);
        var meter = new ProjectMeterMap([new(1, 4, 4), new(2, 6, 8)]);
        var beats = MetronomeSchedule.Between(tempo, meter, 1.5, 7, 1000);
        Assert.Equal(new long[] { 1000, 2000, 3000, 3500, 4000, 4500, 5000, 5500 }, beats.Select(b => b.Frame));
        Assert.Equal(new[] { 3, 4, 1, 2, 3, 4, 5, 6 }, beats.Select(b => b.Beat));
        Assert.Equal(3000, Assert.Single(beats.Where(b => b.Accent)).Frame);
        Assert.Equal(2, meter.PositionAt(4).Bar); Assert.Equal(3, meter.PositionAt(7).Bar);
        Assert.Throws<ArgumentException>(() => MetronomeSchedule.Between(tempo, meter, 0, 7, 1000, maxBeats: 2));
    }

    [Fact]
    public void CountInFreezesCursorTempoMeterAndFinishesAtAnExactBeatBoundary()
    {
        var tempo = new ProjectTempoMap([new(0, 120), new(5, 60)]);
        var meter = new ProjectMeterMap([new(1, 4, 4), new(2, 3, 8)]);
        var count = MetronomeSchedule.CountIn(tempo, meter, 5.25, 2, 1000);
        Assert.Equal(3000, count.Frames);
        Assert.Equal(new long[] { 0, 500, 1000, 1500, 2000, 2500 }, count.Beats.Select(b => b.Frame));
        Assert.Equal(new long[] { 0, 1500 }, count.Beats.Where(b => b.Accent).Select(b => b.Frame));
        Assert.Throws<ArgumentException>(() => MetronomeSchedule.CountIn(tempo, meter, 0, 9, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => meter.PositionAt(-1));
    }
}
