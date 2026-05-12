using System;
using System.IO;
using System.Text;
using FlowLang.StandardLibrary.Audio;

namespace FlowLang.Tests.Helpers;

/// <summary>
/// Phase 28 SPEC-8: inverse of <see cref="FlowLang.StandardLibrary.Audio.FileIO.WriteWav"/>.
/// Reads a 16/24/32-bit PCM WAV file (mono or stereo) into an
/// <see cref="AudioBuffer"/> for RMS-windowed regression assertions.
///
/// Format support matches FileIO.WriteWav: RIFF/WAVE container, fmt chunk
/// (PCM only — formatCode 1), data chunk. Unknown chunks (LIST, bext,
/// fact, etc.) are skipped silently with their declared size.
/// </summary>
public static class WavReader
{
    public static AudioBuffer ReadWav(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"WAV file not found: {path}");

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        // ===== RIFF header =====
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
            throw new InvalidDataException("Not a RIFF file (missing 'RIFF' magic)");
        reader.ReadInt32(); // file size - 8 (don't need it; iterate to data chunk)
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
            throw new InvalidDataException("Not a WAVE file (missing 'WAVE' magic)");

        // ===== Walk chunks until 'data' is found =====
        short formatCode = 0;
        short channels = 0;
        int sampleRate = 0;
        short bitsPerSample = 0;
        int dataSize = 0;
        bool fmtSeen = false;
        bool dataSeen = false;
        long dataStart = -1;

        while (stream.Position < stream.Length)
        {
            string chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            int chunkSize = reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                long fmtEnd = stream.Position + chunkSize;
                formatCode = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // byteRate
                reader.ReadInt16(); // blockAlign
                bitsPerSample = reader.ReadInt16();
                fmtSeen = true;
                // Skip any extra bytes in extended fmt chunks (e.g. WAVE_FORMAT_EXTENSIBLE)
                if (stream.Position < fmtEnd)
                    stream.Position = fmtEnd;
            }
            else if (chunkId == "data")
            {
                dataSize = chunkSize;
                dataStart = stream.Position;
                dataSeen = true;
                break; // FileIO.WriteWav writes data last; stop here.
            }
            else
            {
                // Skip unknown chunk (LIST, bext, fact, etc.) — pad to even byte boundary
                int skip = chunkSize + (chunkSize % 2);
                stream.Position += skip;
            }
        }

        if (!fmtSeen) throw new InvalidDataException("Missing 'fmt ' chunk");
        if (!dataSeen) throw new InvalidDataException("Missing 'data' chunk");
        if (formatCode != 1)
            throw new InvalidDataException($"Only PCM (formatCode=1) supported; got {formatCode}");
        if (bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
            throw new InvalidDataException($"Unsupported bit depth: {bitsPerSample}");

        int bytesPerSample = bitsPerSample / 8;
        int frameStride = channels * bytesPerSample;
        int frameCount = dataSize / frameStride;

        var buffer = new AudioBuffer(frameCount, channels, sampleRate);
        stream.Position = dataStart;

        for (int frame = 0; frame < frameCount; frame++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float sample = bitsPerSample switch
                {
                    16 => reader.ReadInt16() / 32768.0f,
                    24 => ReadInt24(reader) / 8388608.0f,
                    32 => reader.ReadInt32() / 2147483648.0f,
                    _ => 0f,
                };
                buffer.SetSample(frame, ch, sample);
            }
        }
        return buffer;
    }

    private static int ReadInt24(BinaryReader reader)
    {
        byte lsb = reader.ReadByte();
        byte mid = reader.ReadByte();
        byte msb = reader.ReadByte();
        // Sign-extend the top byte
        int value = lsb | (mid << 8) | (msb << 16);
        if ((msb & 0x80) != 0) value |= unchecked((int)0xFF000000);
        return value;
    }
}
