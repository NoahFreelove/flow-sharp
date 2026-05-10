using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Signal generation functions for synthesizing waveforms.
/// </summary>
public static class SignalGeneration
{
    /// <summary>
    /// Creates a new oscillator state with specified frequency and sample rate.
    /// </summary>
    public static Value CreateOscillatorState(IReadOnlyList<Value> args)
    {
        double frequency = args[0].As<double>();
        int sampleRate = args[1].As<int>();

        var state = new OscillatorState(frequency, sampleRate);
        return Value.OscillatorState(state);
    }

    /// <summary>
    /// Resets the oscillator phase to zero.
    /// </summary>
    public static Value ResetPhase(IReadOnlyList<Value> args)
    {
        var state = args[0].As<OscillatorState>();
        state.ResetPhase();
        return Value.Void();
    }

    /// <summary>
    /// Fills a buffer with a sine wave, maintaining phase continuity.
    /// </summary>
    public static Value GenerateSine(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        var state = args[1].As<OscillatorState>();
        double amplitude = args[2].As<double>();

        for (int frame = 0; frame < buffer.Frames; frame++)
        {
            float sample = (float)(Math.Sin(2 * Math.PI * state.Phase) * amplitude);

            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                buffer.SetSample(frame, ch, sample);
            }

            state.AdvancePhase();
        }

        return Value.Void();
    }

    /// <summary>
    /// Fills a buffer with a sawtooth wave, maintaining phase continuity.
    /// </summary>
    public static Value GenerateSaw(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        var state = args[1].As<OscillatorState>();
        double amplitude = args[2].As<double>();

        for (int frame = 0; frame < buffer.Frames; frame++)
        {
            // Sawtooth: ramps from -1 to 1 linearly
            float sample = (float)((2.0 * state.Phase - 1.0) * amplitude);

            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                buffer.SetSample(frame, ch, sample);
            }

            state.AdvancePhase();
        }

        return Value.Void();
    }

    /// <summary>
    /// Fills a buffer with a square wave, maintaining phase continuity.
    /// </summary>
    public static Value GenerateSquare(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        var state = args[1].As<OscillatorState>();
        double amplitude = args[2].As<double>();

        for (int frame = 0; frame < buffer.Frames; frame++)
        {
            // Square wave: -1 for phase < 0.5, +1 for phase >= 0.5
            float sample = (float)((state.Phase < 0.5 ? -1.0 : 1.0) * amplitude);

            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                buffer.SetSample(frame, ch, sample);
            }

            state.AdvancePhase();
        }

        return Value.Void();
    }

    /// <summary>
    /// Fills a buffer with a triangle wave, maintaining phase continuity.
    /// </summary>
    public static Value GenerateTriangle(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        var state = args[1].As<OscillatorState>();
        double amplitude = args[2].As<double>();

        for (int frame = 0; frame < buffer.Frames; frame++)
        {
            // Triangle: ramps up from -1 to 1 in first half, down from 1 to -1 in second half
            double value;
            if (state.Phase < 0.5)
            {
                value = 4.0 * state.Phase - 1.0;  // -1 to 1 as phase goes 0 to 0.5
            }
            else
            {
                value = 3.0 - 4.0 * state.Phase;  // 1 to -1 as phase goes 0.5 to 1
            }

            float sample = (float)(value * amplitude);

            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                buffer.SetSample(frame, ch, sample);
            }

            state.AdvancePhase();
        }

        return Value.Void();
    }
}
