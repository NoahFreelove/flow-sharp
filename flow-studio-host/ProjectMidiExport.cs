using System.Buffers.Binary;
using System.Text;
using Flow.Studio.Engine;
using Flow.Studio.Model;

namespace Flow.Studio.Host;

public sealed record ProjectMidiExportResult(string Path, int Notes, int TicksPerQuarter, IReadOnlyList<string> Diagnostics);

/// <summary>SMF format 1 export of scheduled project notes. Shared playback lowering
/// supplies note gates; audio clips, DSP, mixer automation and tuning bends are not
/// MIDI instruments. Each DAW track uses an independent MIDI port, with channels
/// allocated to disambiguate same-key overlaps.</summary>
public static class ProjectMidiExport
{
    public const int TicksPerQuarter = 9600;
    private sealed record Event(long Tick, int Priority, byte[] Data);
    public static Task<ProjectMidiExportResult> ExportAsync(ProjectSnapshot snapshot, string projectDirectory,
        string destination, int? sampleRate = null, int? blockFrames = null, bool overwrite = false,
        CancellationToken cancellation = default, Action<int, int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string path = Path.GetFullPath(destination), directory = Path.GetFullPath(projectDirectory);
        return Task.Run(() => Export(snapshot, directory, path, sampleRate ?? snapshot.RenderSettings.SampleRate, blockFrames ?? snapshot.RenderSettings.BlockFrames, overwrite, cancellation, progress), cancellation);
    }
    private static long Tick(double quarters)
    {
        double ticks = Math.Round(quarters * TicksPerQuarter, MidpointRounding.AwayFromZero);
        if (!double.IsFinite(ticks) || ticks < 0 || ticks > 0x0ffffffe) throw new ArgumentException("Project exceeds MIDI tick range");
        return (long)ticks;
    }
    private static ProjectMidiExportResult Export(ProjectSnapshot snapshot, string directory, string path,
        int sampleRate, int blockFrames, bool overwrite, CancellationToken cancellation, Action<int, int>? progress)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!overwrite && File.Exists(path)) throw new IOException("MIDI export destination already exists");
        if (snapshot.Assets.Any(a => Path.GetFullPath(Path.Combine(directory, a.RelativePath)) == path))
            throw new ArgumentException("MIDI export cannot overwrite a project asset");
        var assets = ProjectAudioAssets.Resolve(snapshot, directory, cancellation: cancellation);
        var prepared = ProjectCompiler.Prepare(snapshot, sampleRate, blockFrames, cancellation, assets.Assets);
        if (prepared.Diagnostics.Any(d => d.Code == "missing-source-layer")) throw new InvalidDataException("Cannot export unavailable score clips");
        var diagnostics = assets.Diagnostics.Concat(prepared.Diagnostics.Select(d => d.Message)).ToList();
        diagnostics.Add("MIDI contains note gates and project tempo/meter, not Flow instruments, audio clips, mixer processing or automation. DSP tail time is retained as trailing silence.");
        diagnostics.Add("Each track uses a MIDI port meta-event. Readers ignoring ports may merge independent tracks; no GM instrument/program assignment is inferred.");
        var tracks = new List<byte[]>(); var conductor = new List<Event>();
        foreach (var tempo in snapshot.Arrangement.Tempo.Changes)
        {
            double micros = Math.Round(60000000 / tempo.Bpm, MidpointRounding.AwayFromZero);
            if (micros is < 1 or > 0xffffff) throw new ArgumentException("Project tempo is outside MIDI range");
            int value = (int)micros;
            conductor.Add(new(Tick(tempo.Quarter), 0, [0xff, 0x51, 3, (byte)(value >> 16), (byte)(value >> 8), (byte)value]));
        }
        foreach (var meter in snapshot.Arrangement.Meter.Changes)
        {
            if (meter.Numerator > 255 || meter.Denominator > 32768 || !int.IsPow2(meter.Denominator))
                throw new ArgumentException("Project meter cannot be represented in MIDI");
            conductor.Add(new(Tick(snapshot.Arrangement.Meter.QuarterAtBar(meter.Bar)), 0,
                [0xff, 0x58, 4, (byte)meter.Numerator, (byte)System.Numerics.BitOperations.Log2((uint)meter.Denominator), 24, 8]));
        }
        long endTick = Tick(snapshot.Arrangement.Tempo.QuarterAt((double)prepared.Playback.TotalFrames / sampleRate));
        tracks.Add(Track(conductor, endTick, cancellation));
        int count = 0, rounded = 0, silent = 0;
        progress?.Invoke(0, snapshot.Routing.Tracks.Count);
        for (int index = 0; index < snapshot.Routing.Tracks.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var track = snapshot.Routing.Tracks[index];
            var events = new List<Event> { new(0, -2, [0xff, 0x21, 1, (byte)index]) };
            byte[] name = Encoding.UTF8.GetBytes(track.Name);
            using (var meta = new MemoryStream())
            { meta.Write([0xff, 3]); Variable(meta, name.Length); meta.Write(name); events.Add(new(0, -1, meta.ToArray())); }
            var busyUntil = new long[128, 16];
            foreach (var note in prepared.TrackNotes[track.Id].OrderBy(n => n.StartFrame).ThenBy(n => n.EndFrame))
            {
                cancellation.ThrowIfCancellationRequested();
                if (note.Velocity <= 0) { silent++; continue; }
                double fractionalKey = 69 + 12 * Math.Log2(note.FrequencyHz / 440);
                int key = checked((int)Math.Round(fractionalKey, MidpointRounding.AwayFromZero));
                if (key is < 0 or > 127) throw new ArgumentException("Scheduled pitch is outside MIDI key range");
                if (Math.Abs(fractionalKey - key) > 1e-7) rounded++;
                long on = Tick(snapshot.Arrangement.Tempo.QuarterAt((double)note.StartFrame / sampleRate));
                long off = Math.Max(on + 1, Tick(snapshot.Arrangement.Tempo.QuarterAt((double)note.EndFrame / sampleRate)));
                int channel = 0; while (channel < 16 && busyUntil[key, channel] > on) channel++;
                if (channel == 16) throw new InvalidDataException("MIDI export exceeds 16 simultaneous same-key notes on one track");
                busyUntil[key, channel] = off;
                byte velocity = (byte)Math.Clamp((int)Math.Round(note.Velocity * 127, MidpointRounding.AwayFromZero), 1, 127);
                events.Add(new(on, 1, [(byte)(0x90 | channel), (byte)key, velocity]));
                events.Add(new(off, 0, [(byte)(0x80 | channel), (byte)key, 0])); count++;
            }
            tracks.Add(Track(events, endTick, cancellation)); progress?.Invoke(index + 1, snapshot.Routing.Tracks.Count);
        }
        if (rounded != 0) diagnostics.Add($"Rounded {rounded} tuned pitches to nearest 12-TET MIDI keys; pitch bends are not emitted.");
        if (silent != 0) diagnostics.Add($"Omitted {silent} zero-velocity notes.");
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, $".flow-midi-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write("MThd"u8); U32(output, 6); U16(output, 1); U16(output, tracks.Count); U16(output, TicksPerQuarter);
                foreach (var track in tracks)
                {
                    cancellation.ThrowIfCancellationRequested();
                    output.Write("MTrk"u8); U32(output, (uint)track.Length); output.Write(track);
                }
                output.Flush(flushToDisk: true);
            }
            cancellation.ThrowIfCancellationRequested(); File.Move(temporary, path, overwrite);
            return new(path, count, TicksPerQuarter, diagnostics.AsReadOnly());
        }
        finally { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    private static byte[] Track(IEnumerable<Event> events, long end, CancellationToken cancellation)
    {
        using var stream = new MemoryStream(); long previous = 0;
        foreach (var item in events.OrderBy(e => e.Tick).ThenBy(e => e.Priority))
        { cancellation.ThrowIfCancellationRequested(); Variable(stream, item.Tick - previous); stream.Write(item.Data); previous = item.Tick; }
        Variable(stream, Math.Max(end, previous) - previous); stream.Write([0xff, 0x2f, 0]); return stream.ToArray();
    }
    private static void Variable(Stream stream, long value)
    {
        if (value is < 0 or > 0x0fffffff) throw new ArgumentException("MIDI delta exceeds four bytes");
        Span<byte> bytes = stackalloc byte[4]; int offset = 3; bytes[offset] = (byte)(value & 127);
        while ((value >>= 7) != 0) bytes[--offset] = (byte)((value & 127) | 128);
        stream.Write(bytes[offset..]);
    }
    private static void U16(Stream stream, int value) { Span<byte> bytes = stackalloc byte[2]; BinaryPrimitives.WriteUInt16BigEndian(bytes, checked((ushort)value)); stream.Write(bytes); }
    private static void U32(Stream stream, uint value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(bytes, value); stream.Write(bytes); }
}
