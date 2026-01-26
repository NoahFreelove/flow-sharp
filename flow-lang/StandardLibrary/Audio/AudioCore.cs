using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Represents an audio buffer with sample rate and channel layout metadata.
/// Uses 32-bit float for internal representation (industry standard for VST/AU).
/// </summary>
public class AudioBuffer
{
    /// <summary>
    /// Audio sample data in 32-bit float format.
    /// Layout: Interleaved stereo (LRLRLR...) for multi-channel audio.
    /// </summary>
    public float[] Data { get; }

    /// <summary>
    /// Sample rate in Hz (e.g., 44100, 48000).
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Number of audio channels (1 = mono, 2 = stereo).
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Number of sample frames (independent of channel count).
    /// For stereo, Data.Length = Frames * Channels.
    /// </summary>
    public int Frames { get; }

    public AudioBuffer(int frames, int channels, int sampleRate)
    {
        if (frames < 0) throw new ArgumentException("Frame count cannot be negative", nameof(frames));
        if (channels < 1) throw new ArgumentException("Channel count must be at least 1", nameof(channels));
        if (sampleRate <= 0) throw new ArgumentException("Sample rate must be positive", nameof(sampleRate));

        Frames = frames;
        Channels = channels;
        SampleRate = sampleRate;
        Data = new float[frames * channels];
    }

    /// <summary>
    /// Gets a sample at the specified frame and channel.
    /// </summary>
    public float GetSample(int frame, int channel)
    {
        if (frame < 0 || frame >= Frames)
            throw new ArgumentOutOfRangeException(nameof(frame), $"Frame {frame} out of range [0, {Frames})");
        if (channel < 0 || channel >= Channels)
            throw new ArgumentOutOfRangeException(nameof(channel), $"Channel {channel} out of range [0, {Channels})");

        return Data[frame * Channels + channel];
    }

    /// <summary>
    /// Sets a sample at the specified frame and channel.
    /// </summary>
    public void SetSample(int frame, int channel, float value)
    {
        if (frame < 0 || frame >= Frames)
            throw new ArgumentOutOfRangeException(nameof(frame), $"Frame {frame} out of range [0, {Frames})");
        if (channel < 0 || channel >= Channels)
            throw new ArgumentOutOfRangeException(nameof(channel), $"Channel {channel} out of range [0, {Channels})");

        Data[frame * Channels + channel] = value;
    }

    /// <summary>
    /// Fills the entire buffer with a constant value.
    /// </summary>
    public void Fill(float value)
    {
        Array.Fill(Data, value);
    }
}

/// <summary>
/// Core audio buffer operations.
/// </summary>
public static class AudioCore
{
    /// <summary>
    /// Creates a new audio buffer with the specified parameters.
    /// </summary>
    public static Value CreateBuffer(IReadOnlyList<Value> args)
    {
        int frames = args[0].As<int>();
        int channels = args[1].As<int>();
        int sampleRate = args[2].As<int>();

        var buffer = new AudioBuffer(frames, channels, sampleRate);
        return Value.Buffer(buffer);
    }

    /// <summary>
    /// Gets the number of frames in a buffer.
    /// </summary>
    public static Value GetFrames(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        return Value.Int(buffer.Frames);
    }

    /// <summary>
    /// Gets the number of channels in a buffer.
    /// </summary>
    public static Value GetChannels(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        return Value.Int(buffer.Channels);
    }

    /// <summary>
    /// Gets the sample rate of a buffer.
    /// </summary>
    public static Value GetSampleRate(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        return Value.Int(buffer.SampleRate);
    }

    /// <summary>
    /// Gets a sample at the specified frame and channel.
    /// </summary>
    public static Value GetSample(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        int frame = args[1].As<int>();
        int channel = args[2].As<int>();

        float sample = buffer.GetSample(frame, channel);
        return Value.Float(sample);
    }

    /// <summary>
    /// Sets a sample at the specified frame and channel.
    /// </summary>
    public static Value SetSample(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        int frame = args[1].As<int>();
        int channel = args[2].As<int>();
        float value = (float)args[3].As<double>();

        buffer.SetSample(frame, channel, value);
        return Value.Void();
    }

    /// <summary>
    /// Fills a buffer with a constant value.
    /// </summary>
    public static Value FillBuffer(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        float value = (float)args[1].As<double>();

        buffer.Fill(value);
        return Value.Void();
    }

    /// <summary>
    /// Mixes two buffers together with individual gain controls.
    /// Result is written to a new buffer.
    /// </summary>
    public static Value MixBuffers(IReadOnlyList<Value> args)
    {
        var bufferA = args[0].As<AudioBuffer>();
        var bufferB = args[1].As<AudioBuffer>();
        float gainA = (float)args[2].As<double>();
        float gainB = (float)args[3].As<double>();

        // Validate compatibility
        if (bufferA.SampleRate != bufferB.SampleRate)
            throw new ArgumentException("Buffers must have the same sample rate");
        if (bufferA.Channels != bufferB.Channels)
            throw new ArgumentException("Buffers must have the same channel count");

        // Use the longer buffer's length
        int frames = Math.Max(bufferA.Frames, bufferB.Frames);
        var result = new AudioBuffer(frames, bufferA.Channels, bufferA.SampleRate);

        // Mix samples
        for (int i = 0; i < result.Data.Length; i++)
        {
            float sampleA = i < bufferA.Data.Length ? bufferA.Data[i] : 0f;
            float sampleB = i < bufferB.Data.Length ? bufferB.Data[i] : 0f;
            result.Data[i] = sampleA * gainA + sampleB * gainB;
        }

        return Value.Buffer(result);
    }
}
