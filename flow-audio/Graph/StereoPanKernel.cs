namespace Flow.Audio.Graph;

/// <summary>Shared constant-power law used by legacy Flow buffers and prepared graphs.</summary>
public static class StereoPanKernel
{
    public static void Gains(float pan, out float left, out float right)
    {
        float angle = (Math.Clamp(pan, -1f, 1f) + 1f) * 0.25f * MathF.PI;
        left = MathF.Cos(angle);
        right = MathF.Sin(angle);
    }
}
