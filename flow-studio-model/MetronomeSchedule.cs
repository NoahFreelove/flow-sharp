namespace Flow.Studio.Model;

public sealed record MetronomeBeat(long Frame, int Bar, int Beat, bool Accent);
public sealed record CountInSchedule(long Frames, IReadOnlyList<MetronomeBeat> Beats);

/// <summary>Control-side beat scheduling shared by Flow authoring and the host.
/// A beat is the meter denominator unit (6/8 gives six eighth-note clicks).
/// Project playback follows all tempo/meter changes. Count-in freezes tempo/meter
/// at the recording cursor and leaves the project cursor unchanged.</summary>
public static class MetronomeSchedule
{
    public static IReadOnlyList<MetronomeBeat> Between(ProjectTempoMap tempo, ProjectMeterMap meter,
        double startQuarter, double endQuarter, int sampleRate, int maxBeats = 100000)
    {
        ArgumentNullException.ThrowIfNull(tempo); ArgumentNullException.ThrowIfNull(meter);
        if (!double.IsFinite(startQuarter) || !double.IsFinite(endQuarter) || startQuarter < 0 || endQuarter <= startQuarter ||
            sampleRate is < 1 or > 384000 || maxBeats is < 1 or > 100000)
            throw new ArgumentException("Invalid metronome range or budget");
        var result = new List<MetronomeBeat>();
        var position = meter.PositionAt(startQuarter);
        int bar = position.Bar;
        while (true)
        {
            double barStart = meter.QuarterAtBar(bar);
            if (barStart >= endQuarter) break;
            var active = meter.PositionAt(barStart).Meter;
            double unit = 4.0 / active.Denominator;
            int first = checked((int)Math.Max(0, Math.Ceiling((startQuarter - barStart) / unit)));
            for (int beat = first; beat < active.Numerator; beat++)
            {
                double quarter = barStart + beat * unit;
                if (quarter >= endQuarter) break;
                if (result.Count == maxBeats) throw new ArgumentException("Metronome beat budget exceeded");
                result.Add(new(Frame(tempo.SecondsAt(quarter), sampleRate), bar, beat + 1, beat == 0));
            }
            if (bar == int.MaxValue) break;
            double next = meter.QuarterAtBar(bar + 1);
            if (next <= barStart) throw new ArgumentException("Meter exceeds representable beat timing");
            bar++;
        }
        return result.AsReadOnly();
    }

    public static CountInSchedule CountIn(ProjectTempoMap tempo, ProjectMeterMap meter,
        double cursorQuarter, int bars, int sampleRate)
    {
        if (bars is < 1 or > 8 || sampleRate is < 1 or > 384000) throw new ArgumentException("Invalid count-in settings");
        var active = meter.PositionAt(cursorQuarter).Meter;
        var change = tempo.Changes.Last(c => c.Quarter <= cursorQuarter);
        long count = (long)active.Numerator * bars;
        if (count > 100000) throw new ArgumentException("Count-in beat budget exceeded");
        double secondsPerBeat = 60 / change.Bpm * (4.0 / active.Denominator);
        var beats = Enumerable.Range(0, (int)count).Select(i => new MetronomeBeat(
            Frame(i * secondsPerBeat, sampleRate), i / active.Numerator + 1,
            i % active.Numerator + 1, i % active.Numerator == 0)).ToArray();
        long duration = Frame(count * secondsPerBeat, sampleRate);
        if (duration <= 0) throw new ArgumentException("Count-in is shorter than one output frame");
        return new(duration, Array.AsReadOnly(beats));
    }

    private static long Frame(double seconds, int rate)
    {
        double frames = Math.Round(seconds * rate, MidpointRounding.AwayFromZero);
        if (!double.IsFinite(frames) || frames < 0 || frames >= long.MaxValue) throw new ArgumentException("Metronome exceeds frame range");
        return checked((long)frames);
    }
}
