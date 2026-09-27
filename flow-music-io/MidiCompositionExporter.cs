using Flow.Music.Model;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using NoteEvent = Flow.Music.Model.NoteEvent;

namespace Flow.Music.IO;

/// <summary>
/// Writes a detached score as a Standard MIDI File: a conductor track, then one track
/// per sequence name (case-insensitive, first-occurrence order) routed by
/// <see cref="InstrumentRouting"/>. Pitches are the snapshot's 12-TET MIDI keys; tuning
/// cents are not encoded. Ticks round from absolute score positions (onsets before the
/// song start clamp to 0; note-offs round from absolute ends) at 480 TPQN, raised to fit exact tuplet durations and capped
/// at 9,600. The whole file is validated and built before any byte reaches the stream.
/// Ties do not merge notes, matching legacy export.
/// </summary>
public static class MidiCompositionExporter
{
    private const int BaseTicksPerQuarter = 480;
    private const int MaxTicksPerQuarter = 9600;

    public static byte[] ToBytes(CompositionSnapshot composition, CancellationToken cancellation = default)
    {
        var file = Build(composition, cancellation);
        using var stream = new MemoryStream();
        file.Write(stream);
        return stream.ToArray();
    }

    /// <summary>The caller owns the stream; failures before serialization write nothing.</summary>
    public static void Write(CompositionSnapshot composition, Stream destination, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        Build(composition, cancellation).Write(destination);
    }

    private sealed class Track
    {
        public TrackChunk Chunk { get; } = new();
        public List<TimedEvent> Events { get; } = [];
        public required FourBitNumber Channel { get; init; }
    }

    private static MidiFile Build(CompositionSnapshot composition, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(composition);
        cancellation.ThrowIfCancellationRequested();
        int tpq = RequiredTicksPerQuarter(composition, cancellation);
        var file = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision((short)tpq) };
        var conductor = new TrackChunk();
        var conductorEvents = new List<TimedEvent>();
        file.Chunks.Add(conductor);
        var tracks = new Dictionary<string, Track>(StringComparer.OrdinalIgnoreCase);

        double? bpm = null;
        (int, int)? meter = null;
        string? key = null;
        long sectionStart = 0;
        foreach (var occurrence in composition.Timeline())
        {
            cancellation.ThrowIfCancellationRequested();
            RequireDeltaRange(sectionStart);
            var section = occurrence.Section;
            if (bpm != section.Settings.Bpm)
            {
                bpm = section.Settings.Bpm;
                conductorEvents.Add(new(new SetTempoEvent((int)(60_000_000.0 / bpm.Value)), sectionStart));
            }
            // The first sequence with bars supplies the section's meter map.
            var bars = section.Sequences.FirstOrDefault(s => s.Bars.Count > 0)?.Bars ?? [];
            if (meter is null && bars.Count == 0) meter = Meter(4, 4, sectionStart, conductorEvents);
            foreach (var bar in bars)
                if (meter != (bar.Numerator, bar.Denominator))
                    meter = Meter(bar.Numerator, bar.Denominator, Math.Max(0, sectionStart + Ticks(bar.OffsetQuarters, tpq)), conductorEvents);
            if (section.Settings.Key is { } name && !string.Equals(name, key, StringComparison.OrdinalIgnoreCase)
                && KeySignatures.Map.TryGetValue(name, out var signature))
            {
                key = name;
                conductorEvents.Add(new(new KeySignatureEvent(signature.sharpsFlats, signature.minor), sectionStart));
            }

            foreach (var sequence in section.Sequences)
            {
                if (!tracks.TryGetValue(sequence.Name, out var track))
                    tracks.Add(sequence.Name, track = CreateTrack(sequence.Name));
                foreach (var note in sequence.Notes)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (note.Pitch is not null) AddNote(track, note, sectionStart, tpq);
                }
            }
            sectionStart = checked(sectionStart + Ticks(section.DurationQuarters, tpq));
        }
        if (bpm is null) conductorEvents.Add(new(new SetTempoEvent(500_000), 0));
        if (meter is null) Meter(4, 4, 0, conductorEvents);

        using (var manager = conductor.ManageTimedEvents()) manager.Objects.Add(conductorEvents);
        foreach (var track in tracks.Values)
        {
            using (var manager = track.Chunk.ManageTimedEvents()) manager.Objects.Add(track.Events);
            file.Chunks.Add(track.Chunk);
        }
        cancellation.ThrowIfCancellationRequested();
        return file;
    }

    private static (int, int) Meter(int numerator, int denominator, long tick, List<TimedEvent> events)
    {
        if (numerator is < 1 or > 255 || denominator is < 1 or > 255 || !int.IsPow2(denominator))
            throw new ArgumentOutOfRangeException(nameof(numerator), $"Meter {numerator}/{denominator} cannot be written to MIDI");
        RequireDeltaRange(tick);
        // DryWetMidi encodes the literal denominator as a power of two itself.
        events.Add(new(new TimeSignatureEvent((byte)numerator, (byte)denominator), tick));
        return (numerator, denominator);
    }

    private static Track CreateTrack(string sequenceName)
    {
        var (program, channel) = InstrumentRouting.ResolveGmProgram(sequenceName);
        var track = new Track { Channel = (FourBitNumber)channel };
        string display = InstrumentRouting.StripSamplerPrefix(sequenceName);
        if (!string.IsNullOrEmpty(display)) track.Events.Add(new(new SequenceTrackNameEvent(display), 0));
        track.Events.Add(new(new ProgramChangeEvent((SevenBitNumber)program) { Channel = track.Channel }, 0));
        return track;
    }

    private static void AddNote(Track track, NoteEvent note, long sectionStart, int tpq)
    {
        var pitch = note.Pitch!;
        if (pitch.MidiKey is < 0 or > 127)
            throw new ArgumentOutOfRangeException(nameof(note), $"MIDI key {pitch.MidiKey} is outside 0-127");
        if (!double.IsFinite(note.Velocity) || !double.IsFinite(note.DurationOverlap) || !double.IsFinite(note.PortamentoMs))
            throw new ArgumentException("Note velocity, overlap and portamento must be finite", nameof(note));
        double sounding = note.DurationOverlap > 0 ? note.DurationQuarters * (1.0 + note.DurationOverlap) : note.DurationQuarters;
        long on = Math.Max(0, checked(sectionStart + Ticks(note.OffsetQuarters, tpq)));
        long off = Math.Max(on, checked(sectionStart + Ticks(note.OffsetQuarters + sounding, tpq)));
        RequireDeltaRange(off);
        var key = (SevenBitNumber)pitch.MidiKey;
        var velocity = (SevenBitNumber)Math.Clamp((int)(note.Velocity * 127), 1, 127);
        var channel = track.Channel;
        if (note.PortamentoMs > 0)
        {
            // Portamento on (CC65) with a linear 0-200 ms glide time (CC5), closed at note end.
            var glide = (SevenBitNumber)Math.Clamp((int)Math.Round(note.PortamentoMs * 127.0 / 200.0), 0, 127);
            track.Events.Add(new(new ControlChangeEvent((SevenBitNumber)65, (SevenBitNumber)127) { Channel = channel }, on));
            track.Events.Add(new(new ControlChangeEvent((SevenBitNumber)5, glide) { Channel = channel }, on));
        }
        track.Events.Add(new(new NoteOnEvent(key, velocity) { Channel = channel }, on));
        track.Events.Add(new(new NoteOffEvent(key, (SevenBitNumber)0) { Channel = channel }, off));
        if (note.PortamentoMs > 0)
            track.Events.Add(new(new ControlChangeEvent((SevenBitNumber)65, (SevenBitNumber)0) { Channel = channel }, off));
    }

    // Absolute ticks bound every delta, keeping each within the SMF 28-bit variable-length limit.
    private static void RequireDeltaRange(long tick)
    {
        if (tick > 0x0FFF_FFFF)
            throw new InvalidOperationException("Score position exceeds the Standard MIDI File delta-time range");
    }

    private static long Ticks(double quarters, int tpq)
    {
        double ticks = Math.Round(quarters * tpq, MidpointRounding.AwayFromZero);
        if (!double.IsFinite(ticks) || Math.Abs(ticks) >= long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(quarters), "Score position exceeds the MIDI tick range");
        return (long)ticks;
    }

    /// <summary>LCM(480, 2 × each exact-duration denominator) over placed sections.</summary>
    private static int RequiredTicksPerQuarter(CompositionSnapshot composition, CancellationToken cancellation)
    {
        var denominators = new SortedSet<int>();
        foreach (var placement in composition.Placements.Where(p => p.RepeatCount > 0))
            foreach (var sequence in placement.Section.Sequences)
                foreach (var note in sequence.Notes)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (note.ExactDuration is { } exact) denominators.Add(exact.Denominator);
                }
        long tpq = BaseTicksPerQuarter;
        foreach (int denominator in denominators)
        {
            tpq = Lcm(tpq, 2L * denominator);
            if (tpq > MaxTicksPerQuarter)
                throw new InvalidOperationException($"MIDI export requires TPQN={tpq}, exceeds cap {MaxTicksPerQuarter}. " +
                    $"Tuplet ratios in this song: [{string.Join(", ", denominators)}]");
        }
        return (int)tpq;
    }

    private static long Lcm(long a, long b) => a / Gcd(a, b) * b;
    private static long Gcd(long a, long b) => b == 0 ? a : Gcd(b, a % b);
}
