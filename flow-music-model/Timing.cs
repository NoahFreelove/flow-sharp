namespace Flow.Music.Model;

/// <summary>BPM is quarters per minute in every meter. Negative positions are allowed.</summary>
public static class Timing
{
    public static double QuartersToSeconds(double quarters, double bpm)
    {
        RequirePositive(bpm, nameof(bpm));
        if (!double.IsFinite(quarters)) throw new ArgumentOutOfRangeException(nameof(quarters));
        return quarters * (60.0 / bpm);
    }

    public static double SecondsToQuarters(double seconds, double bpm)
    {
        RequirePositive(bpm, nameof(bpm));
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        return seconds * (bpm / 60.0);
    }

    internal static void RequireNonnegative(double value, string parameter)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(parameter);
    }

    internal static void RequirePositive(double value, string parameter)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(parameter);
    }
}
