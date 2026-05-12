using FlowMidi.Midi;

namespace FlowMidi;

static class Diagnostics
{
    public static void Dump(MidiFile midi)
    {
        Console.Error.WriteLine($"Format: {midi.Format}, TPQN: {midi.TicksPerQuarterNote}, Tracks: {midi.Tracks.Count}");
        Console.Error.WriteLine();

        for (int i = 0; i < midi.Tracks.Count; i++)
        {
            var track = midi.Tracks[i];
            var noteOns = track.Events.OfType<NoteOnEvent>().ToList();
            var tempos = track.Events.OfType<TempoEvent>().ToList();
            var timeSigs = track.Events.OfType<TimeSignatureEvent>().ToList();
            var keySigs = track.Events.OfType<KeySignatureEvent>().ToList();

            Console.Error.WriteLine($"  Track {i}: name=\"{track.Name ?? "(none)"}\" events={track.Events.Count} noteOns={noteOns.Count}");

            if (noteOns.Count > 0)
            {
                int minPitch = noteOns.Min(n => n.Pitch);
                int maxPitch = noteOns.Max(n => n.Pitch);
                int minVel = noteOns.Min(n => n.Velocity);
                int maxVel = noteOns.Max(n => n.Velocity);
                var channels = noteOns.Select(n => n.Channel).Distinct().OrderBy(c => c).ToList();
                Console.Error.WriteLine($"    Pitch range: {PitchName(minPitch)} - {PitchName(maxPitch)} (MIDI {minPitch}-{maxPitch})");
                Console.Error.WriteLine($"    Velocity range: {minVel}-{maxVel}");
                Console.Error.WriteLine($"    Channels: {string.Join(", ", channels)}");
            }

            foreach (var ts in timeSigs.Take(5))
                Console.Error.WriteLine($"    TimeSig @{ts.AbsoluteTick}: {ts.Numerator}/{ts.Denominator}");
            if (timeSigs.Count > 5)
                Console.Error.WriteLine($"    ... and {timeSigs.Count - 5} more time sig events");

            foreach (var te in tempos.Take(5))
                Console.Error.WriteLine($"    Tempo @{te.AbsoluteTick}: {te.Bpm:F1} BPM");
            if (tempos.Count > 5)
                Console.Error.WriteLine($"    ... and {tempos.Count - 5} more tempo events");

            foreach (var ks in keySigs)
                Console.Error.WriteLine($"    KeySig @{ks.AbsoluteTick}: sharps/flats={ks.SharpsFlats} minor={ks.IsMinor}");

            var noteOffs = track.Events.OfType<NoteOffEvent>().ToList();
            var pairs = PairNotes(noteOns, noteOffs);
            Console.Error.WriteLine($"    --- first 60 NoteOn (tick, ch, pitch, vel, duration_ticks) ---");
            foreach (var p in pairs.Take(60))
                Console.Error.WriteLine($"    @{p.OnTick,-6} ch={p.Channel} {PitchName(p.Pitch),-4} v={p.Velocity,-3} dur={p.DurationTicks}");
        }
    }

    record NotePair(long OnTick, int Channel, int Pitch, int Velocity, long DurationTicks);

    static List<NotePair> PairNotes(List<NoteOnEvent> noteOns, List<NoteOffEvent> noteOffs)
    {
        var result = new List<NotePair>();
        foreach (var on in noteOns)
        {
            var off = noteOffs.FirstOrDefault(o => o.Channel == on.Channel && o.Pitch == on.Pitch && o.AbsoluteTick >= on.AbsoluteTick);
            long dur = off != null ? off.AbsoluteTick - on.AbsoluteTick : -1;
            result.Add(new NotePair(on.AbsoluteTick, on.Channel, on.Pitch, on.Velocity, dur));
        }
        return result.OrderBy(p => p.OnTick).ToList();
    }

    static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    static string PitchName(int midi)
    {
        int octave = (midi / 12) - 1;
        int note = midi % 12;
        return $"{NoteNames[note]}{octave}";
    }
}
