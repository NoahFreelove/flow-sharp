namespace Flow.Studio.Model;

internal static class Validate
{
    internal static double Finite(double value, string name)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name);
        return value;
    }
    internal static double Nonnegative(double value, string name)
    {
        if (Finite(value, name) < 0) throw new ArgumentOutOfRangeException(name);
        return value;
    }
    internal static double Positive(double value, string name)
    {
        if (Finite(value, name) <= 0) throw new ArgumentOutOfRangeException(name);
        return value;
    }
    internal static Guid Id(Guid value, string name)
    {
        if (value == Guid.Empty) throw new ArgumentException("An ID must not be empty", name);
        return value;
    }
}

public readonly record struct MusicalDuration
{
    public double Quarters { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public MusicalDuration(double quarters) => Quarters = Validate.Positive(quarters, nameof(quarters));
}

public readonly record struct TimeOffset
{
    public double Milliseconds { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public TimeOffset(double milliseconds) => Milliseconds = Validate.Finite(milliseconds, nameof(milliseconds));
    [System.Text.Json.Serialization.JsonIgnore]
    public double Seconds => Milliseconds / 1000;
}

public sealed record TempoChange(double Quarter, double Bpm);
public sealed record MeterChange(int Bar, int Numerator, int Denominator);
public readonly record struct ProjectMeterPosition(int Bar, double BarStartQuarter, MeterChange Meter);

/// <summary>Immutable piecewise-constant tempo. BPM always counts quarter notes.
/// Negative positions extrapolate the first tempo for nudged material before zero.</summary>
public sealed class ProjectTempoMap
{
    public IReadOnlyList<TempoChange> Changes { get; }
    private readonly double[] _seconds;

    public ProjectTempoMap(IEnumerable<TempoChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var values = changes.ToArray();
        if (values.Length == 0 || values[0] is null || values[0].Quarter != 0)
            throw new ArgumentException("Tempo must start at quarter zero", nameof(changes));
        if (values.Length > 4096) throw new ArgumentException("Too many tempo changes", nameof(changes));
        _seconds = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(values[i]);
            Validate.Nonnegative(values[i].Quarter, nameof(changes));
            Validate.Positive(values[i].Bpm, nameof(changes));
            Validate.Positive(60 / values[i].Bpm, nameof(changes));
            Validate.Positive(values[i].Bpm / 60, nameof(changes));
            if (i == 0) continue;
            if (values[i].Quarter <= values[i - 1].Quarter)
                throw new ArgumentException("Tempo changes must be strictly ordered", nameof(changes));
            _seconds[i] = Validate.Finite(_seconds[i - 1] +
                (values[i].Quarter - values[i - 1].Quarter) * (60 / values[i - 1].Bpm), nameof(changes));
            if (_seconds[i] <= _seconds[i - 1])
                throw new ArgumentException("Tempo segments must have representable duration", nameof(changes));
        }
        Changes = Array.AsReadOnly(values);
    }

    public double SecondsAt(double quarter)
    {
        Validate.Finite(quarter, nameof(quarter));
        int index = Find(Changes.Count, i => Changes[i].Quarter <= quarter);
        return Validate.Finite(_seconds[index] + (quarter - Changes[index].Quarter) * (60 / Changes[index].Bpm), nameof(quarter));
    }

    public double QuarterAt(double seconds)
    {
        Validate.Finite(seconds, nameof(seconds));
        int index = Find(_seconds.Length, i => _seconds[i] <= seconds);
        return Validate.Finite(Changes[index].Quarter + (seconds - _seconds[index]) * (Changes[index].Bpm / 60), nameof(seconds));
    }

    // Preparation/control-thread helper; not called in the audio callback.
    private static int Find(int count, Func<int, bool> before)
    {
        int low = 0, high = count - 1;
        while (low < high)
        {
            int middle = low + (high - low + 1) / 2;
            if (before(middle)) low = middle;
            else high = middle - 1;
        }
        return low;
    }
}

/// <summary>Meter changes use one-based bar numbers, so every change is on a bar boundary.</summary>
public sealed class ProjectMeterMap
{
    public IReadOnlyList<MeterChange> Changes { get; }
    private readonly double[] _quarters;

    public ProjectMeterMap(IEnumerable<MeterChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var values = changes.ToArray();
        if (values.Length == 0 || values[0] is null || values[0].Bar != 1)
            throw new ArgumentException("Meter must start at bar one", nameof(changes));
        if (values.Length > 4096) throw new ArgumentException("Too many meter changes", nameof(changes));
        _quarters = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            var meter = values[i];
            ArgumentNullException.ThrowIfNull(meter);
            if (meter.Bar < 1 || meter.Numerator < 1 || meter.Denominator < 1 ||
                (meter.Denominator & (meter.Denominator - 1)) != 0)
                throw new ArgumentException("Meter needs positive numerator and power-of-two denominator", nameof(changes));
            if (i == 0) continue;
            if (meter.Bar <= values[i - 1].Bar)
                throw new ArgumentException("Meter changes must be strictly ordered", nameof(changes));
            _quarters[i] = Validate.Finite(_quarters[i - 1] +
                (meter.Bar - values[i - 1].Bar) * BarLength(values[i - 1]), nameof(changes));
        }
        Changes = Array.AsReadOnly(values);
    }

    public double QuarterAtBar(int bar)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bar, 1);
        int index = 0;
        while (index + 1 < Changes.Count && Changes[index + 1].Bar <= bar) index++;
        return Validate.Finite(_quarters[index] + (bar - Changes[index].Bar) * BarLength(Changes[index]), nameof(bar));
    }

    public ProjectMeterPosition PositionAt(double quarter)
    {
        Validate.Nonnegative(quarter, nameof(quarter));
        int index = 0;
        while (index + 1 < Changes.Count && _quarters[index + 1] <= quarter) index++;
        var meter = Changes[index];
        int bars = checked((int)Math.Floor((quarter - _quarters[index]) / BarLength(meter)));
        int bar = checked(meter.Bar + bars);
        return new(bar, QuarterAtBar(bar), meter);
    }

    private static double BarLength(MeterChange meter) => meter.Numerator * (4.0 / meter.Denominator);
}
