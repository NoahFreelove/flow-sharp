using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Helper functions for buffer manipulation.
/// </summary>
public static class BufferHelpers
{
    /// <summary>
    /// Creates a copy of a buffer.
    /// </summary>
    public static Value CopyBuffer(IReadOnlyList<Value> args)
    {
        var source = args[0].As<AudioBuffer>();

        var copy = new AudioBuffer(source.Frames, source.Channels, source.SampleRate);
        Array.Copy(source.Data, copy.Data, source.Data.Length);

        return Value.Buffer(copy);
    }

    /// <summary>
    /// Extracts a slice from a buffer [startFrame, endFrame).
    /// </summary>
    public static Value SliceBuffer(IReadOnlyList<Value> args)
    {
        var source = args[0].As<AudioBuffer>();
        int startFrame = args[1].As<int>();
        int endFrame = args[2].As<int>();

        if (startFrame < 0 || startFrame >= source.Frames)
            throw new ArgumentOutOfRangeException(nameof(startFrame), $"Start frame {startFrame} out of range [0, {source.Frames})");
        if (endFrame < startFrame || endFrame > source.Frames)
            throw new ArgumentOutOfRangeException(nameof(endFrame), $"End frame {endFrame} must be between {startFrame} and {source.Frames}");

        int sliceFrames = endFrame - startFrame;
        var slice = new AudioBuffer(sliceFrames, source.Channels, source.SampleRate);

        for (int frame = 0; frame < sliceFrames; frame++)
        {
            for (int ch = 0; ch < source.Channels; ch++)
            {
                float sample = source.GetSample(startFrame + frame, ch);
                slice.SetSample(frame, ch, sample);
            }
        }

        return Value.Buffer(slice);
    }

    /// <summary>
    /// Appends two buffers together.
    /// Buffers must have the same channel count and sample rate.
    /// </summary>
    public static Value AppendBuffers(IReadOnlyList<Value> args)
    {
        var bufferA = args[0].As<AudioBuffer>();
        var bufferB = args[1].As<AudioBuffer>();

        if (bufferA.SampleRate != bufferB.SampleRate)
            throw new ArgumentException("Buffers must have the same sample rate");
        if (bufferA.Channels != bufferB.Channels)
            throw new ArgumentException("Buffers must have the same channel count");

        int totalFrames = bufferA.Frames + bufferB.Frames;
        var result = new AudioBuffer(totalFrames, bufferA.Channels, bufferA.SampleRate);

        // Copy buffer A
        for (int frame = 0; frame < bufferA.Frames; frame++)
        {
            for (int ch = 0; ch < bufferA.Channels; ch++)
            {
                float sample = bufferA.GetSample(frame, ch);
                result.SetSample(frame, ch, sample);
            }
        }

        // Copy buffer B
        for (int frame = 0; frame < bufferB.Frames; frame++)
        {
            for (int ch = 0; ch < bufferB.Channels; ch++)
            {
                float sample = bufferB.GetSample(frame, ch);
                result.SetSample(bufferA.Frames + frame, ch, sample);
            }
        }

        return Value.Buffer(result);
    }

    /// <summary>
    /// Scales all samples in a buffer by a gain factor (in-place).
    /// </summary>
    public static Value ScaleBuffer(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        float gain = (float)args[1].As<double>();

        for (int frame = 0; frame < buffer.Frames; frame++)
        {
            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                float sample = buffer.GetSample(frame, ch);
                buffer.SetSample(frame, ch, sample * gain);
            }
        }

        return Value.Void();
    }
}
