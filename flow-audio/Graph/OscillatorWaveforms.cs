namespace Flow.Audio.Graph;

/// <summary>Fixed-cost polynomial edge correction. Reduces discontinuity aliasing;
/// does not promise alias-free arbitrary frequency modulation.</summary>
internal static class OscillatorWaveforms
{
    internal static double Saw(double phase, double step) => 2 * phase - 1 - Edge(phase, Math.Abs(step));
    internal static double Square(double phase, double step)
    {
        double opposite = phase + .5;
        if (opposite >= 1) opposite -= 1;
        return (phase < .5 ? 1 : -1) + Edge(phase, Math.Abs(step)) - Edge(opposite, Math.Abs(step));
    }
    private static double Edge(double phase, double width)
    {
        if (width == 0) return 0;
        if (phase < width)
        { double x = phase / width; return 2 * x - x * x - 1; }
        if (phase > 1 - width)
        { double x = (phase - 1) / width; return x * x + 2 * x + 1; }
        return 0;
    }
}
