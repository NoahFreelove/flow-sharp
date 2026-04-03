using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Exports a Flow Song to a Standard MIDI File (.mid) using DryWetMidi.
/// Walks the SongData hierarchy (sections -> sequences -> bars -> notes)
/// and produces MIDI events with correct tempo, time signature, key signature,
/// velocity mapping, and tick-based durations.
/// </summary>
public static class MidiExport
{
    private const int TicksPerQuarterNote = 480;

    /// <summary>
    /// Key signature lookup: Flow key string -> (sharps/flats, minor flag).
    /// MIDI encodes sharps as positive, flats as negative; minor = 1.
    /// </summary>
    private static readonly Dictionary<string, (sbyte sharpsFlats, byte minor)> KeySignatureMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Major keys
            ["Cmajor"] = (0, 0),
            ["Gmajor"] = (1, 0),
            ["Dmajor"] = (2, 0),
            ["Amajor"] = (3, 0),
            ["Emajor"] = (4, 0),
            ["Bmajor"] = (5, 0),
            ["Fsharpmajor"] = (6, 0),
            ["Csharpmajor"] = (7, 0),
            ["Fmajor"] = (-1, 0),
            ["Bbmajor"] = (-2, 0),
            ["Ebmajor"] = (-3, 0),
            ["Abmajor"] = (-4, 0),
            ["Dbmajor"] = (-5, 0),
            ["Gbmajor"] = (-6, 0),
            // Minor keys
            ["Aminor"] = (0, 1),
            ["Eminor"] = (1, 1),
            ["Bminor"] = (2, 1),
            ["Fsharpminor"] = (3, 1),
            ["Csharpminor"] = (4, 1),
            ["Gsharpminor"] = (5, 1),
            ["Dsharpminor"] = (6, 1),
            ["Asharpminor"] = (7, 1),
            ["Dminor"] = (-1, 1),
            ["Gminor"] = (-2, 1),
            ["Cminor"] = (-3, 1),
            ["Fminor"] = (-4, 1),
            ["Bbminor"] = (-5, 1),
            ["Ebminor"] = (-6, 1),
            // Enharmonic equivalents for keys in ValidKeys not covered above
            ["Dsharpmajor"] = (-3, 0),  // enharmonic with Eb major
            ["Gsharpmajor"] = (-4, 0),  // enharmonic with Ab major
            ["Asharpmajor"] = (-2, 0),  // enharmonic with Bb major
            ["Dbminor"] = (-5, 1),      // enharmonic with C# minor
            ["Gbminor"] = (-6, 1),      // enharmonic with F# minor
            ["Abminor"] = (-4, 1),      // enharmonic with G# minor
        };

    /// <summary>
    /// Flow-callable entry point: writeMidi(String filepath, Song song) -> Void.
    /// </summary>
    public static Value WriteMidi(IReadOnlyList<Value> args)
    {
        string filepath = args[0].As<string>();
        var song = args[1].As<SongData>();

        if (string.IsNullOrWhiteSpace(filepath))
            throw new ArgumentException("MIDI filepath cannot be null or empty");

        ExportMidiInternal(filepath, song);
        return Value.Void();
    }

    /// <summary>
    /// Core MIDI export implementation. Creates a multi-track MIDI file:
    /// Track 0 = conductor (tempo, time sig, key sig meta events),
    /// Track 1 = note data from all sections in arrangement order.
    /// </summary>
    private static void ExportMidiInternal(string filepath, SongData song)
    {
        var midiFile = new MidiFile();
        midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote);

        // Determine global context from the first section
        double bpm = 120.0;
        int timeSigNumerator = 4;
        int timeSigDenominator = 4;
        string? key = null;

        if (song.Sections.Count > 0)
        {
            var firstSectionRef = song.Sections[0];
            if (song.SectionRegistry.TryGetValue(firstSectionRef.Name, out var firstSection))
            {
                var ctx = firstSection.Context;
                if (ctx != null)
                {
                    bpm = ctx.Tempo ?? bpm;
                    if (ctx.TimeSignature != null)
                    {
                        timeSigNumerator = ctx.TimeSignature.Numerator;
                        timeSigDenominator = ctx.TimeSignature.Denominator;
                    }
                    key = ctx.Key;
                }
            }
        }

        // Track 0: Conductor track with meta events
        var conductorChunk = new TrackChunk();
        var conductorEvents = new List<TimedEvent>();

        // Set tempo: microseconds per beat = 60,000,000 / BPM
        int microsPerBeat = (int)(60_000_000.0 / bpm);
        conductorEvents.Add(new TimedEvent(
            new SetTempoEvent(microsPerBeat), 0));

        // Set time signature: denominator encoded as power of 2
        byte midiDenominator = (byte)Math.Log2(timeSigDenominator);
        conductorEvents.Add(new TimedEvent(
            new TimeSignatureEvent((byte)timeSigNumerator, midiDenominator), 0));

        // Set key signature if available
        if (key != null && KeySignatureMap.TryGetValue(key, out var keySig))
        {
            conductorEvents.Add(new TimedEvent(
                new KeySignatureEvent(keySig.sharpsFlats, keySig.minor), 0));
        }

        using (var manager = conductorChunk.ManageTimedEvents())
        {
            manager.Objects.Add(conductorEvents);
        }
        midiFile.Chunks.Add(conductorChunk);

        // Track 1: Note events from all sections
        var noteTrackChunk = new TrackChunk();
        var noteEvents = new List<TimedEvent>();

        // Default to piano (GM program 0)
        noteEvents.Add(new TimedEvent(
            new ProgramChangeEvent((SevenBitNumber)0), 0));

        long absoluteTick = 0;

        foreach (var sectionRef in song.Sections)
        {
            if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var sectionData))
                continue;

            // Get section-specific time signature denominator for beat calculation
            int sectionTimeSigDenom = timeSigDenominator;
            if (sectionData.Context?.TimeSignature != null)
                sectionTimeSigDenom = sectionData.Context.TimeSignature.Denominator;

            // Calculate section length in ticks for repeat offset
            long sectionLengthTicks = CalculateSectionLengthTicks(sectionData, sectionTimeSigDenom);

            for (int repeat = 0; repeat < sectionRef.RepeatCount; repeat++)
            {
                long sectionStartTick = absoluteTick;

                foreach (var (seqName, sequence) in sectionData.Sequences)
                {
                    long seqTick = sectionStartTick;

                    foreach (var bar in sequence.Bars)
                    {
                        int barTimeSigDenom = bar.TimeSignature?.Denominator ?? sectionTimeSigDenom;
                        long barTick = seqTick;

                        foreach (var note in bar.MusicalNotes)
                        {
                            if (note.IsRest)
                            {
                                // Rests advance position but produce no MIDI events
                                double restBeats = note.GetBeats(barTimeSigDenom);
                                barTick += (long)(restBeats * TicksPerQuarterNote);
                                continue;
                            }

                            int midiNote = PitchConversion.GetMidiNote(
                                note.NoteName, note.Octave, note.Alteration);

                            // Map velocity: Flow 0.0-1.0 -> MIDI 1-127 (vel 0 = note off in MIDI)
                            byte velocity = (byte)Math.Clamp((int)(note.Velocity * 127), 1, 127);

                            double beats = note.GetBeats(barTimeSigDenom);
                            long durationTicks = (long)(beats * TicksPerQuarterNote);

                            // NoteOn at current position
                            noteEvents.Add(new TimedEvent(
                                new NoteOnEvent((SevenBitNumber)(byte)midiNote, (SevenBitNumber)velocity),
                                barTick));

                            // NoteOff at position + duration
                            noteEvents.Add(new TimedEvent(
                                new NoteOffEvent((SevenBitNumber)(byte)midiNote, (SevenBitNumber)0),
                                barTick + durationTicks));

                            barTick += durationTicks;
                        }

                        // Advance sequence position by bar duration
                        if (bar.TimeSignature != null)
                        {
                            double barBeats = bar.IsPickup
                                ? bar.GetActualBeats()
                                : bar.TimeSignature.Numerator;
                            seqTick += (long)(barBeats * TicksPerQuarterNote);
                        }
                    }
                }

                absoluteTick += sectionLengthTicks;
            }
        }

        using (var manager = noteTrackChunk.ManageTimedEvents())
        {
            manager.Objects.Add(noteEvents);
        }
        midiFile.Chunks.Add(noteTrackChunk);

        // Write the MIDI file to disk
        midiFile.Write(filepath, overwriteFile: true);
    }

    /// <summary>
    /// Calculates the total length of a section in MIDI ticks by summing
    /// the longest sequence's duration.
    /// </summary>
    private static long CalculateSectionLengthTicks(SectionData section, int timeSigDenominator)
    {
        double maxBeats = 0;

        foreach (var (name, sequence) in section.Sequences)
        {
            double seqBeats = 0;
            foreach (var bar in sequence.Bars)
            {
                if (bar.TimeSignature != null)
                {
                    seqBeats += bar.IsPickup
                        ? bar.GetActualBeats()
                        : bar.TimeSignature.Numerator;
                }
            }
            if (seqBeats > maxBeats)
                maxBeats = seqBeats;
        }

        return (long)(maxBeats * TicksPerQuarterNote);
    }
}
