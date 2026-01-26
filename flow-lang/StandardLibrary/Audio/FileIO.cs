using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Audio file I/O operations for exporting AudioBuffer data to disk.
/// </summary>
public static class FileIO
{
    private static readonly Random Random = new Random();

    /// <summary>
    /// Exports an AudioBuffer to a WAV file with default 16-bit PCM format.
    /// </summary>
    public static Value ExportWav(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        string filepath = args[1].As<string>();

        ExportWavInternal(buffer, filepath, 16);
        return Value.Void();
    }

    /// <summary>
    /// Exports an AudioBuffer to a WAV file with specified bit depth.
    /// </summary>
    public static Value ExportWavWithBitDepth(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        string filepath = args[1].As<string>();
        int bitDepth = args[2].As<int>();

        ExportWavInternal(buffer, filepath, bitDepth);
        return Value.Void();
    }

    /// <summary>
    /// Core WAV export implementation.
    /// </summary>
    private static void ExportWavInternal(AudioBuffer buffer, string filepath, int bitDepth)
    {
        // Validate inputs
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (string.IsNullOrWhiteSpace(filepath))
            throw new ArgumentException("Filepath cannot be null or empty", nameof(filepath));
        if (bitDepth != 16 && bitDepth != 24 && bitDepth != 32)
            throw new ArgumentException($"Bit depth must be 16, 24, or 32 (got {bitDepth})", nameof(bitDepth));

        // Calculate file sizes
        int bytesPerSample = bitDepth / 8;
        int dataSize = buffer.Frames * buffer.Channels * bytesPerSample;
        int fileSize = 36 + dataSize; // 44 bytes header - 8 bytes = 36

        // Write WAV file
        using var fileStream = new FileStream(filepath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fileStream);

        WriteRiffHeader(writer, fileSize);
        WriteFmtChunk(writer, buffer, bitDepth, bytesPerSample);
        WriteDataChunk(writer, buffer, bitDepth, bytesPerSample);
    }

    /// <summary>
    /// Writes the RIFF header (12 bytes).
    /// </summary>
    private static void WriteRiffHeader(BinaryWriter writer, int fileSize)
    {
        writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
        writer.Write(fileSize); // File size - 8
        writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
    }

    /// <summary>
    /// Writes the fmt chunk (24 bytes).
    /// </summary>
    private static void WriteFmtChunk(BinaryWriter writer, AudioBuffer buffer, int bitDepth, int bytesPerSample)
    {
        short formatCode = 1; // PCM
        short channels = (short)buffer.Channels;
        int sampleRate = buffer.SampleRate;
        int byteRate = sampleRate * channels * bytesPerSample;
        short blockAlign = (short)(channels * bytesPerSample);
        short bitsPerSample = (short)bitDepth;

        writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
        writer.Write(16); // Chunk size (16 for PCM)
        writer.Write(formatCode);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
    }

    /// <summary>
    /// Writes the data chunk header and sample data.
    /// </summary>
    private static void WriteDataChunk(BinaryWriter writer, AudioBuffer buffer, int bitDepth, int bytesPerSample)
    {
        int dataSize = buffer.Frames * buffer.Channels * bytesPerSample;

        writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
        writer.Write(dataSize);

        WriteSamples(writer, buffer, bitDepth);
    }

    /// <summary>
    /// Writes all samples to the data chunk, converting from float32 to the target bit depth.
    /// </summary>
    private static void WriteSamples(BinaryWriter writer, AudioBuffer buffer, int bitDepth)
    {
        switch (bitDepth)
        {
            case 16:
                for (int i = 0; i < buffer.Data.Length; i++)
                {
                    short sample = FloatToInt16(buffer.Data[i]);
                    writer.Write(sample);
                }
                break;

            case 24:
                for (int i = 0; i < buffer.Data.Length; i++)
                {
                    WriteInt24(writer, buffer.Data[i]);
                }
                break;

            case 32:
                for (int i = 0; i < buffer.Data.Length; i++)
                {
                    int sample = FloatToInt32(buffer.Data[i]);
                    writer.Write(sample);
                }
                break;
        }
    }

    /// <summary>
    /// Converts a float32 sample to int16 with TPDF dithering.
    /// </summary>
    private static short FloatToInt16(float sample)
    {
        // Clamp sample to valid range
        sample = ClampSample(sample);

        // Add TPDF dither (1 LSB amplitude in float space)
        float dither = GenerateTpdfDither() / 32768.0f;
        sample += dither;

        // Scale to int16 range and round
        float scaled = sample * 32767.0f;
        int rounded = (int)Math.Round(scaled);

        // Clamp to int16 range
        return (short)Math.Clamp(rounded, short.MinValue, short.MaxValue);
    }

    /// <summary>
    /// Writes a float32 sample as int24 (3 bytes) with TPDF dithering.
    /// </summary>
    private static void WriteInt24(BinaryWriter writer, float sample)
    {
        // Clamp sample to valid range
        sample = ClampSample(sample);

        // Add TPDF dither (1 LSB amplitude in float space)
        float dither = GenerateTpdfDither() / 8388608.0f;
        sample += dither;

        // Scale to int24 range and round
        float scaled = sample * 8388607.0f;
        int rounded = (int)Math.Round(scaled);

        // Clamp to int24 range
        int clamped = Math.Clamp(rounded, -8388608, 8388607);

        // Write as 3 bytes (little-endian)
        byte lsb = (byte)(clamped & 0xFF);
        byte mid = (byte)((clamped >> 8) & 0xFF);
        byte msb = (byte)((clamped >> 16) & 0xFF);

        writer.Write(lsb);
        writer.Write(mid);
        writer.Write(msb);
    }

    /// <summary>
    /// Converts a float32 sample to int32 (no dithering needed - quantization negligible).
    /// </summary>
    private static int FloatToInt32(float sample)
    {
        // Clamp sample to valid range
        sample = ClampSample(sample);

        // Scale to int32 range and round (no dithering needed)
        double scaled = sample * 2147483647.0;
        long rounded = (long)Math.Round(scaled);

        // Clamp to int32 range
        return (int)Math.Clamp(rounded, int.MinValue, int.MaxValue);
    }

    /// <summary>
    /// Generates TPDF (Triangular Probability Density Function) dither noise.
    /// Returns a value in the range [-1, 1] with triangular distribution.
    /// </summary>
    private static float GenerateTpdfDither()
    {
        // Generate two uniform random values in [-1, 1]
        float r1 = (float)(Random.NextDouble() * 2.0 - 1.0);
        float r2 = (float)(Random.NextDouble() * 2.0 - 1.0);

        // Sum creates triangular distribution
        return r1 + r2;
    }

    /// <summary>
    /// Clamps a sample to [-1.0, 1.0] and handles NaN/Infinity.
    /// </summary>
    private static float ClampSample(float sample)
    {
        if (float.IsNaN(sample))
            return 0.0f;
        if (float.IsPositiveInfinity(sample))
            return 1.0f;
        if (float.IsNegativeInfinity(sample))
            return -1.0f;

        return Math.Clamp(sample, -1.0f, 1.0f);
    }
}
