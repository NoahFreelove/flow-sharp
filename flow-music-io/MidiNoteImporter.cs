using System.Buffers.Binary;
using System.Text;
using Flow.Music.Model;

namespace Flow.Music.IO;

public sealed record ImportedMidiTempo(double Quarter, double Bpm);
public sealed record ImportedMidiMeter(double Quarter, int Numerator, int Denominator);
public sealed record ImportedMidiPart(int Track, int Channel, string Name, double DurationQuarters, IReadOnlyList<NoteEvent> Notes);
public sealed record ImportedMidiNotes(int TicksPerQuarter, IReadOnlyList<ImportedMidiPart> Parts,
    IReadOnlyList<ImportedMidiTempo> Tempo, IReadOnlyList<ImportedMidiMeter> Meter, IReadOnlyList<string> Diagnostics);

/// <summary>Bounded SMF format 0/1 note import. File tempo/meter are returned as
/// metadata, never applied to a project implicitly. Same-key overlaps pair FIFO.
/// Controllers/programs/pitch bends are diagnosed, not interpreted as Flow devices.</summary>
public static class MidiNoteImporter
{
    private static readonly string[] Spellings = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    public const int MaxBytes = 16 * 1024 * 1024, MaxNotes = 100000, MaxEvents = 1000000;
    public static ImportedMidiNotes Read(byte[] data, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length > MaxBytes) throw new InvalidDataException("MIDI exceeds 16 MiB import budget");
        cancellation.ThrowIfCancellationRequested();
        var reader = new Reader(data);
        if (reader.U32() != 0x4d546864) throw new InvalidDataException("Missing MIDI header");
        int headerEnd = reader.End(reader.U32());
        if (headerEnd - reader.Position < 6) throw new InvalidDataException("Short MIDI header");
        int format = reader.U16(), tracks = reader.U16(), ppq = reader.U16();
        if (format is not (0 or 1) || tracks is < 1 or > 256 || format == 0 && tracks != 1)
            throw new InvalidDataException("Import requires SMF format 0/1 with 1–256 tracks");
        if (ppq == 0 || (ppq & 0x8000) != 0) throw new NotSupportedException("Import requires positive ticks per quarter; SMPTE division is unsupported");
        reader.Position = headerEnd;
        var parts = new List<ImportedMidiPart>(); var tempo = new List<ImportedMidiTempo>(); var meter = new List<ImportedMidiMeter>();
        int events = 0, noteCount = 0, ignored = 0, unmatched = 0, unfinished = 0;
        for (int track = 0; track < tracks; track++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (reader.U32() != 0x4d54726b) throw new InvalidDataException("Missing MIDI track chunk");
            int end = reader.End(reader.U32()); reader.Limit = end;
            long tick = 0; int running = 0; bool ended = false; string name = $"MIDI track {track + 1}";
            var active = new Dictionary<(int Channel, int Pitch), Queue<(long Tick, int Velocity)>>();
            var notes = new Dictionary<int, List<NoteEvent>>();
            void Finish(int channel, int pitch, long start, long stop, int velocity)
            {
                cancellation.ThrowIfCancellationRequested();
                string spelling = Spellings[pitch % 12];
                if (!notes.TryGetValue(channel, out var list)) notes.Add(channel, list = []);
                long duration = stop - start;
                list.Add(new(Guid.NewGuid(), $"midi:{track}:{channel}", (double)start / ppq, (double)duration / ppq,
                    new(spelling[0], pitch / 12 - 1, spelling.Length - 1, 0, pitch, 440 * Math.Pow(2, (pitch - 69) / 12.0)), velocity / 127.0,
                    ExactDuration: duration <= int.MaxValue ? new((int)duration, ppq) : null));
            }
            while (reader.Position < end)
            {
                cancellation.ThrowIfCancellationRequested();
                if (++events > MaxEvents) throw new InvalidDataException("MIDI event budget exceeded");
                tick = checked(tick + reader.Variable());
                int status = reader.Peek();
                if (status >= 128) reader.Byte(); else status = running;
                if (status == 0) throw new InvalidDataException("MIDI data without running status");
                if (status == 0xff)
                {
                    running = 0; int type = reader.Byte(); int length = checked((int)reader.Variable()); int next = reader.End((uint)length);
                    if (type == 0x2f)
                    {
                        if (length != 0 || next != end) throw new InvalidDataException("Invalid MIDI end-of-track");
                        ended = true; break;
                    }
                    if (type == 3) name = Encoding.UTF8.GetString(data, reader.Position, Math.Min(length, 1024));
                    else if (type == 0x51)
                    {
                        if (length != 3) throw new InvalidDataException("Invalid MIDI tempo");
                        int micros = reader.Byte() << 16 | reader.Byte() << 8 | reader.Byte();
                        if (micros == 0 || tempo.Count == 4096) throw new InvalidDataException("Invalid MIDI tempo or map budget");
                        tempo.Add(new((double)tick / ppq, 60000000.0 / micros));
                    }
                    else if (type == 0x58)
                    {
                        if (length != 4) throw new InvalidDataException("Invalid MIDI meter");
                        int numerator = reader.Byte(), exponent = reader.Byte();
                        if (numerator == 0 || exponent > 15 || meter.Count == 4096) throw new InvalidDataException("Invalid MIDI meter or map budget");
                        meter.Add(new((double)tick / ppq, numerator, 1 << exponent));
                    }
                    else ignored++;
                    reader.Position = next; continue;
                }
                if (status is 0xf0 or 0xf7)
                { running = 0; int next = reader.End(reader.Variable()); reader.Position = next; ignored++; continue; }
                if (status is < 0x80 or > 0xef) throw new InvalidDataException("Unsupported MIDI system event");
                running = status; int kind = status >> 4, channel = status & 15;
                int first = reader.DataByte(), second = kind is 0xc or 0xd ? 0 : reader.DataByte();
                if (kind == 9 && second != 0)
                {
                    if (++noteCount > MaxNotes) throw new InvalidDataException("MIDI note budget exceeded");
                    if (!active.TryGetValue((channel, first), out var queue)) active.Add((channel, first), queue = new());
                    queue.Enqueue((tick, second));
                }
                else if (kind == 8 || kind == 9)
                {
                    if (active.TryGetValue((channel, first), out var queue) && queue.TryDequeue(out var on)) Finish(channel, first, on.Tick, tick, on.Velocity);
                    else unmatched++;
                }
                else ignored++;
            }
            if (!ended) throw new InvalidDataException("MIDI track has no end-of-track event");
            foreach (var pair in active)
                foreach (var on in pair.Value) { Finish(pair.Key.Channel, pair.Key.Pitch, on.Tick, tick, on.Velocity); unfinished++; }
            foreach (var pair in notes.OrderBy(p => p.Key))
                parts.Add(new(track, pair.Key, name.Length > 256 ? name[..256] : name, (double)tick / ppq,
                    Array.AsReadOnly(pair.Value.OrderBy(n => n.OffsetQuarters).ToArray())));
            reader.Position = end; reader.Limit = data.Length;
        }
        if (reader.Position != data.Length) throw new InvalidDataException("Unexpected data after MIDI tracks");
        var diagnostics = new List<string>();
        if (ignored > 0) diagnostics.Add($"{ignored} controller/program/bend, SysEx or other metadata events were not applied to notes or devices (including sustain).");
        if (unmatched > 0) diagnostics.Add($"Ignored {unmatched} unmatched note-offs.");
        if (unfinished > 0) diagnostics.Add($"Closed {unfinished} unfinished notes at their track end.");
        return new(ppq, parts.AsReadOnly(), Array.AsReadOnly(tempo.OrderBy(t => t.Quarter).ToArray()),
            Array.AsReadOnly(meter.OrderBy(m => m.Quarter).ToArray()), diagnostics.AsReadOnly());
    }

    private sealed class Reader(byte[] data)
    {
        public int Position;
        public int Limit = data.Length;
        public int Peek() { if (Position >= Limit) throw new InvalidDataException("Truncated MIDI event"); return data[Position]; }
        public int Byte() { int value = Peek(); Position++; return value; }
        public int DataByte() { int value = Byte(); return value < 128 ? value : throw new InvalidDataException("Invalid MIDI data byte"); }
        public int End(uint count) => count <= Limit - Position ? Position + (int)count : throw new InvalidDataException("MIDI chunk exceeds input");
        public int U16() => Byte() << 8 | Byte();
        public uint U32() { int end = End(4); uint value = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(Position, 4)); Position = end; return value; }
        public uint Variable()
        {
            uint value = 0;
            for (int i = 0; i < 4; i++) { int next = Byte(); value = value << 7 | (uint)(next & 127); if (next < 128) return value; }
            throw new InvalidDataException("MIDI variable integer exceeds four bytes");
        }
    }
}
