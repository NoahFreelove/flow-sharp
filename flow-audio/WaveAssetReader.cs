using System.Buffers.Binary;
namespace Flow.Audio;

/// <summary>Worker-only bounded RIFF/WAVE decoder. Supports mono/stereo PCM
/// 8/16/24/32-bit and IEEE float32; mono is duplicated explicitly to stereo.
/// Compressed, extensible and multichannel files require a separate importer.</summary>
public static class WaveAssetReader
{
    public static PcmAsset Read(Stream stream, long maxDecodedBytes = 256 * 1024 * 1024,
        CancellationToken cancellation = default)
    {
        if (!stream.CanRead || !stream.CanSeek) throw new ArgumentException("WAVE input must be readable and seekable");
        cancellation.ThrowIfCancellationRequested();
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        long start = stream.Position;
        if (stream.Length - start < 12 || reader.ReadUInt32() != 0x46464952) throw new InvalidDataException("Missing RIFF header");
        long end = checked(start + 8 + reader.ReadUInt32());
        if (end > stream.Length || end < start + 12 || reader.ReadUInt32() != 0x45564157) throw new InvalidDataException("Invalid WAVE extent");
        ushort format = 0, channels = 0, bits = 0, align = 0; uint rate = 0;
        long dataPosition = -1, dataBytes = 0; bool hasFormat = false;
        int chunks = 0;
        while (stream.Position < end)
        {
            cancellation.ThrowIfCancellationRequested();
            if (++chunks > 4096 || end - stream.Position < 8) throw new InvalidDataException("Invalid WAVE chunk table");
            uint id = reader.ReadUInt32(), size = reader.ReadUInt32();
            long next = checked(stream.Position + size + (size & 1));
            if (next > end) throw new InvalidDataException("Truncated WAVE chunk");
            if (id == 0x20746d66)
            {
                if (hasFormat || size < 16) throw new InvalidDataException("Invalid WAVE format chunk");
                hasFormat = true; format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadUInt32();
                uint byteRate = reader.ReadUInt32(); align = reader.ReadUInt16(); bits = reader.ReadUInt16();
                if (channels is < 1 or > 2 || rate is < 1 or > 384000 ||
                    !(format == 1 && bits is 8 or 16 or 24 or 32 || format == 3 && bits == 32) ||
                    align != channels * (bits / 8) || byteRate != rate * align)
                    throw new InvalidDataException("Unsupported or inconsistent WAVE format");
            }
            else if (id == 0x61746164)
            {
                if (dataPosition >= 0) throw new InvalidDataException("Multiple WAVE data chunks are unsupported");
                dataPosition = stream.Position; dataBytes = size;
            }
            stream.Position = next;
        }
        if (!hasFormat || dataPosition < 0 || dataBytes % align != 0) throw new InvalidDataException("Missing or partial WAVE frames");
        long frames = dataBytes / align, decodedBytes = checked(frames * 8);
        if (maxDecodedBytes <= 0 || decodedBytes > maxDecodedBytes || frames > int.MaxValue / 2)
            throw new InvalidDataException("WAVE exceeds decoded audio budget");
        var samples = new float[(int)frames * 2];
        var block = new byte[align * 4096];
        stream.Position = dataPosition;
        for (int frame = 0; frame < frames;)
        {
            cancellation.ThrowIfCancellationRequested();
            int count = (int)Math.Min(4096, frames - frame);
            stream.ReadExactly(block.AsSpan(0, count * align));
            for (int i = 0; i < count; i++)
            {
                var input = block.AsSpan(i * align, align);
                float left = Decode(input, format, bits);
                float right = channels == 1 ? left : Decode(input[(bits / 8)..], format, bits);
                if (!float.IsFinite(left) || !float.IsFinite(right)) throw new InvalidDataException("Nonfinite WAVE samples");
                samples[(frame + i) * 2] = left; samples[(frame + i) * 2 + 1] = right;
            }
            frame += count;
        }
        return new(samples, (int)rate, maxDecodedBytes);
    }
    private static float Decode(ReadOnlySpan<byte> input, ushort format, ushort bits)
    {
        if (format == 3) return BinaryPrimitives.ReadSingleLittleEndian(input);
        return bits switch
        {
            8 => (input[0] - 128) / 128f,
            16 => BinaryPrimitives.ReadInt16LittleEndian(input) / 32768f,
            24 => ((input[0] | input[1] << 8 | input[2] << 16) << 8 >> 8) / 8388608f,
            32 => (float)(BinaryPrimitives.ReadInt32LittleEndian(input) / 2147483648.0),
            _ => throw new InvalidDataException("Unsupported sample encoding")
        };
    }
}
