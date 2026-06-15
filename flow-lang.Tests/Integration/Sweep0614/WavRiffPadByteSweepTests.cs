using System;
using System.IO;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Sweep0614;

/// <summary>
/// Regression coverage for the sweep-2026-06-14 "audio-dsp" group, WAV writer
/// RIFF pad byte defect.
///
/// <para>
/// The RIFF spec requires every chunk to end on an even byte boundary; an odd
/// chunk is followed by a single 0x00 pad byte that is NOT counted in the
/// chunk's own size field. The only supported bit depth that can produce an odd
/// data chunk is 24-bit (3 bytes/sample) with an odd total sample count.
/// </para>
///
/// <para>
/// Pre-fix <c>WriteDataChunk</c> wrote the samples and returned without the pad
/// byte, and the RIFF top-level size field omitted it too — so a 3-frame mono
/// 24-bit export was 53 bytes (odd, non-conformant). Flow's own reader already
/// compensates for the pad (LoadWavInternal:429-431), so the writer was
/// asymmetric with the reader and strict third-party parsers could misalign on
/// a following chunk.
/// </para>
/// </summary>
public class WavRiffPadByteSweepTests
{
    /// <summary>
    /// A 3-frame mono 24-bit buffer has a 9-byte data chunk (odd). The file
    /// must end on an even boundary with a single trailing 0x00 pad byte, and
    /// the RIFF top-level size field must account for the pad.
    /// </summary>
    [Fact]
    public void WriteWav_Odd24BitDataChunk_EmitsRiffPadByte()
    {
        // 3 frames * 1 channel * 3 bytes = 9-byte (odd) data chunk.
        var buffer = new AudioBuffer(3, 1, 44100);
        buffer.Data[0] = 0.10f;
        buffer.Data[1] = -0.20f;
        buffer.Data[2] = 0.30f;

        string path = Path.Combine(Path.GetTempPath(),
            $"flow_riffpad_{Guid.NewGuid():N}.wav");
        try
        {
            FileIO.WriteWavWithBitDepth(new[]
            {
                Value.String(path),
                Value.Buffer(buffer),
                Value.Int(24),
            });

            byte[] bytes = File.ReadAllBytes(path);

            // 44-byte header + 9 data bytes + 1 pad byte = 54 (even).
            Assert.Equal(54, bytes.Length);
            Assert.Equal(0, bytes.Length % 2);

            // Last byte is the 0x00 pad.
            Assert.Equal(0, bytes[^1]);

            // 'data' chunk id at offset 36, size field at offset 40 = 9 (true
            // sample byte count, NOT including the pad).
            Assert.Equal((byte)'d', bytes[36]);
            Assert.Equal((byte)'a', bytes[37]);
            Assert.Equal((byte)'t', bytes[38]);
            Assert.Equal((byte)'a', bytes[39]);
            int dataChunkSize = BitConverter.ToInt32(bytes, 40);
            Assert.Equal(9, dataChunkSize);

            // RIFF top-level size at offset 4 = fileLength - 8 = 46, and MUST
            // include the pad byte (36 + 9 + 1).
            int riffSize = BitConverter.ToInt32(bytes, 4);
            Assert.Equal(bytes.Length - 8, riffSize);
            Assert.Equal(46, riffSize);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// Flow's own reader round-trips the padded odd 24-bit file back to the
    /// original frame count and samples — proving writer/reader symmetry.
    /// </summary>
    [Fact]
    public void WriteThenReadWav_Odd24Bit_RoundTripsFrameCount()
    {
        var buffer = new AudioBuffer(3, 1, 44100);
        buffer.Data[0] = 0.10f;
        buffer.Data[1] = -0.20f;
        buffer.Data[2] = 0.30f;

        string path = Path.Combine(Path.GetTempPath(),
            $"flow_riffpad_rt_{Guid.NewGuid():N}.wav");
        try
        {
            FileIO.WriteWavWithBitDepth(new[]
            {
                Value.String(path),
                Value.Buffer(buffer),
                Value.Int(24),
            });

            var read = FileIO.LoadWavInternal(path);
            Assert.Equal(3, read.Frames);
            Assert.Equal(1, read.Channels);
            // 24-bit quantization tolerance is ~1/8388608; loose float check.
            Assert.True(Math.Abs(read.Data[0] - 0.10f) < 1e-3f);
            Assert.True(Math.Abs(read.Data[1] - (-0.20f)) < 1e-3f);
            Assert.True(Math.Abs(read.Data[2] - 0.30f) < 1e-3f);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// Two consecutive writes of the same odd 24-bit buffer are byte-identical
    /// (the pad is a constant 0x00 and the dither RNG reseeds per export) —
    /// two-run cmp-clean determinism preserved.
    /// </summary>
    [Fact]
    public void WriteWav_Odd24Bit_ByteIdenticalAcrossTwoWrites()
    {
        var buffer = new AudioBuffer(3, 1, 44100);
        buffer.Data[0] = 0.10f;
        buffer.Data[1] = -0.20f;
        buffer.Data[2] = 0.30f;

        string a = Path.Combine(Path.GetTempPath(), $"flow_riffpad_a_{Guid.NewGuid():N}.wav");
        string b = Path.Combine(Path.GetTempPath(), $"flow_riffpad_b_{Guid.NewGuid():N}.wav");
        try
        {
            FileIO.WriteWavWithBitDepth(new[] { Value.String(a), Value.Buffer(buffer), Value.Int(24) });
            FileIO.WriteWavWithBitDepth(new[] { Value.String(b), Value.Buffer(buffer), Value.Int(24) });
            Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(b));
        }
        finally
        {
            if (File.Exists(a)) File.Delete(a);
            if (File.Exists(b)) File.Delete(b);
        }
    }

    /// <summary>
    /// Even-size data chunks (16-bit, and even-frame 24-bit) must NOT gain a
    /// pad byte — guard against over-padding.
    /// </summary>
    [Fact]
    public void WriteWav_EvenDataChunk_NoPadByte()
    {
        // 16-bit is always even (2 bytes/sample). 3 frames * 2 = 6 bytes.
        var buf16 = new AudioBuffer(3, 1, 44100);
        buf16.Data[0] = 0.1f; buf16.Data[1] = -0.2f; buf16.Data[2] = 0.3f;

        // Even-frame 24-bit: 2 frames * 3 = 6 bytes (even).
        var buf24 = new AudioBuffer(2, 1, 44100);
        buf24.Data[0] = 0.1f; buf24.Data[1] = -0.2f;

        string p16 = Path.Combine(Path.GetTempPath(), $"flow_even16_{Guid.NewGuid():N}.wav");
        string p24 = Path.Combine(Path.GetTempPath(), $"flow_even24_{Guid.NewGuid():N}.wav");
        try
        {
            FileIO.WriteWavWithBitDepth(new[] { Value.String(p16), Value.Buffer(buf16), Value.Int(16) });
            FileIO.WriteWavWithBitDepth(new[] { Value.String(p24), Value.Buffer(buf24), Value.Int(24) });

            // 44 header + 6 data = 50 (even, no pad).
            Assert.Equal(50, new FileInfo(p16).Length);
            Assert.Equal(50, new FileInfo(p24).Length);
        }
        finally
        {
            if (File.Exists(p16)) File.Delete(p16);
            if (File.Exists(p24)) File.Delete(p24);
        }
    }
}
