using System.Buffers.Binary;
using System.Text;

namespace Flow.Audio;

/// <summary>Worker-only streaming stereo IEEE float32 WAVE output. Owns neither
/// stream nor cursor; the cursor must be fresh and exclusively owned by this call.</summary>
public static class PlaybackWaveWriter
{
    public static void Write(Stream destination, IPreparedAudioPlayback playback,
        CancellationToken cancellation = default, Action<long, long>? progress = null)
        => WriteRange(destination, playback, 0, playback?.TotalFrames ?? 0, cancellation, progress);

    /// <summary>Writes [startFrame,endFrame) from continuous playback. Preroll is
    /// rendered, never sought over, to preserve effect history. Progress measures
    /// all processed frames (including preroll), with endFrame as its total.</summary>
    public static void WriteRange(Stream destination, IPreparedAudioPlayback playback,
        long startFrame, long endFrame, CancellationToken cancellation = default, Action<long, long>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(destination); ArgumentNullException.ThrowIfNull(playback);
        if (!destination.CanWrite) throw new ArgumentException("Output stream is not writable");
        if (playback.PositionFrames != 0 || playback.TotalFrames < 0 || playback.SampleRate is < 1 or > 384000 ||
            playback.MaxBlockFrames is < 1 or > 1048576) throw new ArgumentException("Invalid or non-fresh playback cursor");
        // RIFF size includes WAVE, fmt (18 bytes), fact and data headers.
        if (startFrame < 0 || endFrame < startFrame || endFrame > playback.TotalFrames)
            throw new ArgumentException("Export range is outside prepared playback");
        long bytes = checked((endFrame - startFrame) * 8);
        if (bytes > uint.MaxValue - 50L) throw new ArgumentException("Export exceeds RIFF/WAVE size limit; RF64 is not supported");
        cancellation.ThrowIfCancellationRequested();
        using var writer = new BinaryWriter(destination, Encoding.ASCII, leaveOpen: true);
        writer.Write(0x46464952u); writer.Write((uint)(bytes + 50)); writer.Write(0x45564157u);
        writer.Write(0x20746d66u); writer.Write(18u); writer.Write((ushort)3); writer.Write((ushort)2);
        writer.Write((uint)playback.SampleRate); writer.Write((uint)(playback.SampleRate * 8));
        writer.Write((ushort)8); writer.Write((ushort)32); writer.Write((ushort)0);
        writer.Write(0x74636166u); writer.Write(4u); writer.Write((uint)(endFrame - startFrame));
        writer.Write(0x61746164u); writer.Write((uint)bytes);
        int block = Math.Min(playback.MaxBlockFrames, 4096);
        var samples = new float[block * 2]; var encoded = new byte[block * 8];
        long completed = 0; progress?.Invoke(0, endFrame);
        while (completed < endFrame)
        {
            cancellation.ThrowIfCancellationRequested();
            int frames = (int)Math.Min(block, endFrame - completed);
            int read = playback.Read(samples.AsSpan(0, frames * 2));
            if (read != frames || playback.PositionFrames != completed + frames)
                throw new InvalidDataException("Playback returned an incomplete export block");
            for (int i = 0; i < frames * 2; i++)
            {
                if (!float.IsFinite(samples[i])) throw new InvalidDataException("Cannot export nonfinite audio");
                BinaryPrimitives.WriteSingleLittleEndian(encoded.AsSpan(i * 4, 4), samples[i]);
            }
            int skip = (int)Math.Clamp(startFrame - completed, 0, frames);
            writer.Write(encoded.AsSpan(skip * 8, (frames - skip) * 8)); completed += frames;
            progress?.Invoke(completed, endFrame);
        }
        cancellation.ThrowIfCancellationRequested(); writer.Flush();
    }
}
