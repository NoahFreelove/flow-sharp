using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Audio file I/O operations for exporting AudioBuffer data to disk.
/// </summary>
public static class FileIO
{
    // TPDF dither RNG. Reseeded at the start of each export with a fixed seed
    // for cross-export determinism (Phase 15 Plan 05, ROADMAP criterion #2).
    // Pre-fix: time-based unseeded `new Random()` produced different LSB-level
    // dither bytes on every export — same audio quality, but raw WAV bytes
    // never matched between exports.
    //
    // Within a single export the RNG advances normally so dither still
    // decorrelates per sample (the only property TPDF dither requires); across
    // exports the bytes now reproduce. Plan 15-03 documented the pre-fix
    // behavior as the reason Facts F-02/F-07/F-08 used trailing-RMS and
    // CountDivergentPcmSamples observables instead of raw byte comparison;
    // those observables remain valid but Plan 15-05's byte-identical WAV
    // Fact requires this fix.
    private const int DitherSeed = 0xD17E2;
    private static Random Random = new Random(DitherSeed);

    /// <summary>
    /// Core WAV export implementation.
    /// </summary>
    private static void WriteWavInternal(AudioBuffer buffer, string filepath, int bitDepth)
    {
        // Phase 36 Plan 36-01 (D-v1.5-06 / D-36-09) — reseed PrngRegistry at the
        // WAV-export boundary so any unseeded Phase 36 stochastic primitives
        // upstream of this write produce byte-identical bytes on the next
        // render. Null-safe — direct-API callers that bypass FlowEngine
        // (rare; legacy unit-test entry) skip the reseed harmlessly.
        Core.FlowEngine.CurrentExecutionContext?.PrngRegistry.ResetAtRenderBoundary();

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

        // Ensure parent directory exists (idempotent — no-op if present).
        // Shared by all writeWav overloads via this core helper.
        var dir = Path.GetDirectoryName(filepath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Reset the TPDF dither RNG to its fixed seed at the start of every
        // export so that two consecutive writes of the same buffer produce
        // byte-identical files (Plan 05 ROADMAP criterion #2 / D-18).
        Random = new Random(DitherSeed);

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

    /// <summary>
    /// Writes an AudioBuffer to a WAV file. Primary export function with path-first arg order.
    /// Matches writeMidi convention: writeWav(path, buffer).
    /// </summary>
    public static Value WriteWav(IReadOnlyList<Value> args)
    {
        string filepath = args[0].As<string>();
        var buffer = args[1].As<AudioBuffer>();
        WriteWavInternal(buffer, filepath, 16);
        return Value.Void();
    }

    /// <summary>
    /// Writes an AudioBuffer to a WAV file with specified bit depth. Path-first arg order.
    /// </summary>
    public static Value WriteWavWithBitDepth(IReadOnlyList<Value> args)
    {
        string filepath = args[0].As<string>();
        var buffer = args[1].As<AudioBuffer>();
        int bitDepth = args[2].As<int>();
        WriteWavInternal(buffer, filepath, bitDepth);
        return Value.Void();
    }

    // ===== WAV Loading =====

    /// <summary>
    /// Flow-callable wrapper for loading a WAV file into a Buffer.
    /// </summary>
    public static Value LoadWav(IReadOnlyList<Value> args)
    {
        string filepath = args[0].As<string>();
        var buffer = LoadWavInternal(filepath);
        return Value.Buffer(buffer);
    }

    /// <summary>
    /// DX-15: loadWav(path, semitones) — varispeed pitch shift via linear-interpolation resample.
    /// Positive semitones raise pitch; negative lower. semitones=0 short-circuits to identity.
    /// 12 semitones = ratio 2.0 (one octave up), -12 semitones = ratio 0.5 (one octave down).
    /// Per RESEARCH §Resampler choice: linear interpolation is the v1.3 default; OLA/sinc deferred.
    /// </summary>
    public static Value LoadWavSemitones(IReadOnlyList<Value> args)
    {
        string filepath = args[0].As<string>();
        int semitones = args[1].As<int>();
        var buffer = LoadWavInternal(filepath);
        if (semitones == 0) return Value.Buffer(buffer);   // identity short-circuit
        double ratio = Math.Pow(2.0, semitones / 12.0);
        return Value.Buffer(VarispeedResample(buffer, ratio));
    }

    /// <summary>
    /// DX-15: loadWav(path, ratio) — varispeed pitch shift via linear-interpolation resample.
    /// ratio &gt; 1.0 raises pitch (fewer output frames); ratio &lt; 1.0 lowers pitch (more frames).
    /// ratio == 1.0 short-circuits to identity. ratio &lt;= 0.0 or NaN throws ArgumentException
    /// (threat T-22-V5-09 DoS guard against infinite/NaN frame counts).
    /// </summary>
    public static Value LoadWavRatio(IReadOnlyList<Value> args)
    {
        string filepath = args[0].As<string>();
        double ratio = args[1].As<double>();
        var buffer = LoadWavInternal(filepath);
        if (ratio == 1.0) return Value.Buffer(buffer);     // identity short-circuit
        if (ratio <= 0.0 || double.IsNaN(ratio))
            throw new ArgumentException(
                $"loadWav ratio must be positive and finite (got {ratio})");
        return Value.Buffer(VarispeedResample(buffer, ratio));
    }

    /// <summary>
    /// Linear-interpolation varispeed resample. Output frame count = round(source.Frames / ratio).
    /// ratio &gt; 1.0 produces fewer frames (pitch up); ratio &lt; 1.0 produces more (pitch down).
    /// SampleRate and Channels are preserved (only frame count changes). Algorithm mirrors the
    /// existing <see cref="Resample"/> sample-rate converter; DX-15 reuses the math with an
    /// arbitrary user-supplied ratio instead of srcRate/targetRate.
    /// </summary>
    public static AudioBuffer VarispeedResample(AudioBuffer source, double ratio)
    {
        int newFrames = (int)Math.Round(source.Frames / ratio);
        var result = new AudioBuffer(newFrames, source.Channels, source.SampleRate);
        for (int frame = 0; frame < newFrames; frame++)
        {
            double srcPos = frame * ratio;
            int srcFrame = (int)srcPos;
            float frac = (float)(srcPos - srcFrame);
            for (int ch = 0; ch < source.Channels; ch++)
            {
                float s0 = source.GetSample(Math.Min(srcFrame, source.Frames - 1), ch);
                float s1 = source.GetSample(Math.Min(srcFrame + 1, source.Frames - 1), ch);
                result.SetSample(frame, ch, s0 + frac * (s1 - s0));
            }
        }
        return result;
    }

    /// <summary>
    /// Loads a WAV file from disk and returns an AudioBuffer.
    /// Supports 16-bit, 24-bit, and 32-bit PCM formats.
    /// Resamples to 44100 Hz if the source sample rate differs.
    /// </summary>
    public static AudioBuffer LoadWavInternal(string filepath)
    {
        if (string.IsNullOrWhiteSpace(filepath))
            throw new ArgumentException("Filepath cannot be null or empty", nameof(filepath));
        if (!File.Exists(filepath))
            throw new FileNotFoundException($"WAV file not found: {filepath}", filepath);

        using var fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fileStream);

        // Read RIFF header
        var riffId = new string(reader.ReadChars(4));
        if (riffId != "RIFF")
            throw new InvalidDataException($"Invalid WAV file: expected RIFF header, got '{riffId}'");

        int fileSize = reader.ReadInt32(); // file size - 8

        var waveId = new string(reader.ReadChars(4));
        if (waveId != "WAVE")
            throw new InvalidDataException($"Invalid WAV file: expected WAVE format, got '{waveId}'");

        // Parse chunks (do NOT assume fmt/data order)
        short audioFormat = 0;
        short channels = 0;
        int sampleRate = 0;
        short bitsPerSample = 0;
        bool fmtFound = false;
        float[]? samples = null;

        while (fileStream.Position < fileStream.Length)
        {
            // Read chunk ID and size
            if (fileStream.Length - fileStream.Position < 8)
                break;

            var chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();

            switch (chunkId)
            {
                case "fmt ":
                    audioFormat = reader.ReadInt16();
                    if (audioFormat != 1)
                        throw new InvalidDataException(
                            $"Unsupported WAV format: expected PCM (1), got {audioFormat}");
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    int byteRate = reader.ReadInt32(); // read but unused
                    short blockAlign = reader.ReadInt16(); // read but unused
                    bitsPerSample = reader.ReadInt16();
                    // Skip extra format bytes if chunk is larger than 16
                    if (chunkSize > 16)
                        reader.ReadBytes(chunkSize - 16);
                    fmtFound = true;
                    break;

                case "data":
                    if (!fmtFound)
                        throw new InvalidDataException("WAV file has data chunk before fmt chunk");
                    int totalSamples = chunkSize / (bitsPerSample / 8);
                    samples = ReadSamples(reader, totalSamples, bitsPerSample);
                    break;

                default:
                    // Skip unknown chunks; handle odd chunk sizes with padding
                    int skipBytes = chunkSize;
                    if (chunkSize % 2 != 0)
                        skipBytes++;
                    if (fileStream.Position + skipBytes <= fileStream.Length)
                        reader.ReadBytes(skipBytes);
                    else
                        fileStream.Position = fileStream.Length; // EOF
                    break;
            }
        }

        if (!fmtFound)
            throw new InvalidDataException("WAV file missing fmt chunk");
        if (samples == null)
            throw new InvalidDataException("WAV file missing data chunk");

        // Create AudioBuffer from parsed data
        int frames = samples.Length / channels;
        var buffer = new AudioBuffer(frames, channels, sampleRate);
        Array.Copy(samples, buffer.Data, samples.Length);

        // Resample to 44100 Hz if needed
        if (sampleRate != 44100)
            buffer = Resample(buffer, 44100);

        return buffer;
    }

    /// <summary>
    /// Reads PCM samples from a BinaryReader, converting to float32.
    /// Supports 16-bit, 24-bit, and 32-bit PCM.
    /// </summary>
    private static float[] ReadSamples(BinaryReader reader, int totalSamples, short bitsPerSample)
    {
        var samples = new float[totalSamples];

        switch (bitsPerSample)
        {
            case 16:
                for (int i = 0; i < totalSamples; i++)
                    samples[i] = reader.ReadInt16() / 32768f;
                break;

            case 24:
                for (int i = 0; i < totalSamples; i++)
                {
                    byte b0 = reader.ReadByte();
                    byte b1 = reader.ReadByte();
                    byte b2 = reader.ReadByte();
                    // Assemble 24-bit signed integer (little-endian)
                    int value = b0 | (b1 << 8) | (b2 << 16);
                    // Sign-extend from 24-bit
                    if ((value & 0x800000) != 0)
                        value |= unchecked((int)0xFF000000);
                    samples[i] = value / 8388608f;
                }
                break;

            case 32:
                for (int i = 0; i < totalSamples; i++)
                    samples[i] = reader.ReadInt32() / 2147483648f;
                break;

            default:
                throw new InvalidDataException(
                    $"Unsupported bit depth: {bitsPerSample}. Only 16, 24, and 32-bit PCM are supported.");
        }

        return samples;
    }

    /// <summary>
    /// Resamples an AudioBuffer to a target sample rate using linear interpolation.
    /// </summary>
    public static AudioBuffer Resample(AudioBuffer source, int targetRate)
    {
        if (source.SampleRate == targetRate)
            return source;

        double ratio = (double)source.SampleRate / targetRate;
        int newFrames = (int)(source.Frames / ratio);
        var result = new AudioBuffer(newFrames, source.Channels, targetRate);

        for (int frame = 0; frame < newFrames; frame++)
        {
            double srcPos = frame * ratio;
            int srcFrame = (int)srcPos;
            float frac = (float)(srcPos - srcFrame);

            for (int ch = 0; ch < source.Channels; ch++)
            {
                float s0 = source.GetSample(Math.Min(srcFrame, source.Frames - 1), ch);
                float s1 = source.GetSample(Math.Min(srcFrame + 1, source.Frames - 1), ch);
                result.SetSample(frame, ch, s0 + frac * (s1 - s0));
            }
        }

        return result;
    }
}
