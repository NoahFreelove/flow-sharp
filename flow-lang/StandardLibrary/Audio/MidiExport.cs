using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
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
    /// TUP-06 / CONTEXT D-05 / D-USER-E: maximum TPQN supported by Flow's MIDI export.
    /// Songs whose tuplet denominator LCM forces TPQN above this cap raise a clear
    /// composer-facing error — no DAW imports correctly above this in field testing
    /// (per .planning/research/SUMMARY.md). 32767 is the SMF spec hard limit.
    /// </summary>
    private const int MaxTpqn = 9600;

    /// <summary>
    /// Recursive Euclidean GCD. Mirrors Phase 18 Fraction.cs idiom for stylistic
    /// consistency. Used by Lcm to compute requiredTPQN.
    /// </summary>
    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    /// <summary>
    /// Lcm(a, b) = a × b / Gcd(a, b). Two-line helper next to its sole caller.
    /// </summary>
    private static int Lcm(int a, int b) => a / Gcd(a, b) * b;

    /// <summary>
    /// TUP-06: pre-export pass over the Song collecting tuplet denominators from
    /// MusicalNoteData.DurationFraction values. Computes requiredTPQN = LCM(480,
    /// 2 × union(denoms)) per CONTEXT D-05. When zero tuplets are present (no
    /// note has DurationFraction), returns 480 unchanged (CONTEXT D-07 structural
    /// preservation of Phase 18 byte-identical contract for non-tuplet songs).
    ///
    /// When requiredTPQN exceeds MaxTpqn (9600), raises an InvalidOperationException
    /// with the LOCKED message format from CONTEXT D-06. The error fires BEFORE
    /// any DryWetMidi MidiFile allocation or disk I/O — atomic, no partial export.
    /// </summary>
    private static int ComputeRequiredTpqn(SongData song)
    {
        var denominators = new HashSet<int>();
        foreach (var section in song.SectionRegistry.Values)
            foreach (var sequence in section.Sequences.Values)
                foreach (var bar in sequence.Bars)
                    foreach (var note in bar.MusicalNotes)
                        if (note.DurationFraction.HasValue)
                            denominators.Add(note.DurationFraction.Value.Denom);

        // CONTEXT D-07: zero tuplets → TPQN stays at 480 (Phase 18 byte-identical contract)
        if (denominators.Count == 0)
            return TicksPerQuarterNote;

        int requiredTpqn = TicksPerQuarterNote;
        foreach (var d in denominators)
            requiredTpqn = Lcm(requiredTpqn, 2 * d);

        if (requiredTpqn > MaxTpqn)
        {
            var sortedDenoms = denominators.OrderBy(x => x).ToArray();
            throw new InvalidOperationException(
                $"MIDI export requires TPQN={requiredTpqn}, exceeds cap {MaxTpqn} (locked v1.3 D-05). " +
                $"Tuplet ratios in this song: [{string.Join(", ", sortedDenoms)}]");
        }
        return requiredTpqn;
    }

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
    /// Phase 23 Plan 23-03 Task 2: context-dependent registration for <c>writeMidi</c>.
    /// Mirrors <see cref="Harmony.HarmonyFunctions.RegisterContextDependent"/> shape — closure
    /// over <see cref="FlowLang.Runtime.ExecutionContext"/> so <see cref="WriteMidi(IReadOnlyList{Value}, FlowLang.Runtime.ExecutionContext)"/>
    /// can read <c>MusicalContext.Tuning</c> at call time and emit the D-13 one-shot warning
    /// when tuning != EqualTemperament. MIDI bytes themselves are UNCHANGED — still 12-TET.
    /// </summary>
    public static void RegisterContextDependent(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        var writeMidiSignature = new FunctionSignature("writeMidi", [StringType.Instance, SongType.Instance]);
        registry.Register("writeMidi", writeMidiSignature, args => WriteMidi(args, context));
    }

    /// <summary>
    /// Flow-callable entry point: writeMidi(String filepath, Song song) -> Void.
    /// Context-free overload preserved for backwards compat (e.g., direct test invocation,
    /// proxy paths). Phase 23: registration migrated to the 2-arg overload below so writeMidi
    /// can emit the D-13 non-12-TET advisory warning.
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
    /// Phase 23 Plan 23-03 Task 2 / D-13: context-aware overload. Emits a one-shot
    /// stderr warning when called under non-12-TET tuning so composers know that
    /// faithful microtonal MIDI export (per-channel pitch-bend) is deferred to v1.4.
    /// MIDI bytes are UNCHANGED — still 12-TET output. The warning is purely advisory.
    /// </summary>
    public static Value WriteMidi(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var musicalCtx = context.GetMusicalContext();
        if (musicalCtx?.Tuning is TuningSystem activeTuning && activeTuning != TuningSystem.EqualTemperament)
        {
            RenderingDiagnostics.WarnOnce(
                "writemidi-non-equal-temperament",
                "[midi] tuning != equalTemperament; MIDI export emits 12-TET pitches without pitch-bend (faithful microtonal MIDI deferred to v1.4)");
        }
        return WriteMidi(args);
    }

    /// <summary>
    /// Core MIDI export implementation. Creates a multi-track MIDI file:
    /// Track 0 = conductor (tempo, time sig, key sig meta events),
    /// Track 1 = note data from all sections in arrangement order.
    /// </summary>
    private static void ExportMidiInternal(string filepath, SongData song)
    {
        // TUP-06: pre-export pass — auto-elevate TPQN if tuplets demand it,
        // raise cap error before any allocation if requiredTPQN > 9600.
        // Songs with zero tuplets short-circuit to 480 (Phase 18 byte-identical preserved).
        int ticksPerQuarter = ComputeRequiredTpqn(song);

        var midiFile = new MidiFile();
        // ticksPerQuarter is bounded by MaxTpqn (9600) which fits short.MaxValue (32767).
        midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision((short)ticksPerQuarter);

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
            // TUP-06: thread the per-export ticksPerQuarter through so repeat
            // offsets stay aligned when TPQN auto-elevates above 480.
            long sectionLengthTicks = CalculateSectionLengthTicks(sectionData, sectionTimeSigDenom, ticksPerQuarter);

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
                        // Chord-tone support (mirrors BarType.ToTimeline): the leading note of
                        // a chord group advances barTick for the whole slot; subsequent
                        // chord-tones (IsChordTone=true) emit their NoteOn/NoteOff at the
                        // SAVED leadBarTick and do NOT advance barTick. Without this, a chord
                        // [C E G]q would export as a sequential MIDI arpeggio instead of a
                        // simultaneous polyphonic strike.
                        long leadBarTick = barTick;

                        foreach (var note in bar.MusicalNotes)
                        {
                            if (note.IsRest)
                            {
                                // Rests advance position but produce no MIDI events
                                double restBeats = note.GetBeats(barTimeSigDenom);
                                barTick += (long)(restBeats * ticksPerQuarter);
                                continue;
                            }

                            // Choose the tick at which this note's events land.
                            // Chord-tone: stack on the leading tone's tick.
                            // Leading/standalone note: use barTick AND record it as the new lead.
                            long effectiveTick;
                            if (note.IsChordTone)
                            {
                                effectiveTick = leadBarTick;
                            }
                            else
                            {
                                effectiveTick = barTick;
                                leadBarTick = barTick;
                            }

                            int midiNote = PitchConversion.GetMidiNote(
                                note.NoteName, note.Octave, note.Alteration);

                            // Map velocity: Flow 0.0-1.0 -> MIDI 1-127 (vel 0 = note off in MIDI)
                            byte velocity = (byte)Math.Clamp((int)(note.Velocity * 127), 1, 127);

                            double beats = note.GetBeats(barTimeSigDenom);
                            // DX-14 legato: NoteOff lands at extended duration (CONTEXT D-03 — overlapping
                            // events are valid SMF and the receiving DAW mixes them). When DurationOverlap=0
                            // (default) extendedBeats == beats and the export is byte-identical to pre-22-06.
                            double extendedBeats = note.DurationOverlap > 0
                                ? beats * (1.0 + note.DurationOverlap)
                                : beats;
                            long durationTicks = (long)(extendedBeats * ticksPerQuarter);

                            // DX-14 portamento: emit CC65=127 + CC5=mappedValue at note start
                            // (CONTEXT Claude's Discretion). Linear ms->CC5: 0->0, 100->64, 200->127 clamped.
                            // V5 (T-22-V5-22, T-22-V5-23): clamp before SevenBitNumber cast — guards both
                            // upper overflow and negative input.
                            if (note.PortamentoMs > 0.0)
                            {
                                byte cc5Value = (byte)Math.Clamp(
                                    (int)Math.Round(note.PortamentoMs * 127.0 / 200.0), 0, 127);
                                noteEvents.Add(new TimedEvent(
                                    new ControlChangeEvent((SevenBitNumber)65, (SevenBitNumber)127), effectiveTick));
                                noteEvents.Add(new TimedEvent(
                                    new ControlChangeEvent((SevenBitNumber)5, (SevenBitNumber)cc5Value), effectiveTick));
                            }

                            // NoteOn at current position
                            noteEvents.Add(new TimedEvent(
                                new NoteOnEvent((SevenBitNumber)(byte)midiNote, (SevenBitNumber)velocity),
                                effectiveTick));

                            // NoteOff at position + extended duration (for legato — overlap with next note)
                            noteEvents.Add(new TimedEvent(
                                new NoteOffEvent((SevenBitNumber)(byte)midiNote, (SevenBitNumber)0),
                                effectiveTick + durationTicks));

                            // DX-14 portamento: bracket-close at note end (CC65=0).
                            if (note.PortamentoMs > 0.0)
                            {
                                noteEvents.Add(new TimedEvent(
                                    new ControlChangeEvent((SevenBitNumber)65, (SevenBitNumber)0),
                                    effectiveTick + durationTicks));
                            }

                            // CRITICAL (Pitfall 3): advance by ORIGINAL beats, NOT extendedBeats.
                            // This is what makes legato OVERLAP rather than slow the song down.
                            // Chord-tones do NOT advance — the lead already advanced for the slot.
                            if (!note.IsChordTone)
                                barTick += (long)(beats * ticksPerQuarter);
                        }

                        // Advance sequence position by bar duration
                        if (bar.TimeSignature != null)
                        {
                            double barBeats = bar.IsPickup
                                ? bar.GetActualBeats()
                                : bar.TimeSignature.Numerator;
                            seqTick += (long)(barBeats * ticksPerQuarter);
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
    /// the longest sequence's duration. The ticksPerQuarter parameter (TUP-06)
    /// is passed in from ExportMidiInternal so repeat offsets honour the
    /// per-export auto-elevated TPQN rather than the const baseline.
    /// </summary>
    private static long CalculateSectionLengthTicks(SectionData section, int timeSigDenominator, int ticksPerQuarter)
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

        return (long)(maxBeats * ticksPerQuarter);
    }
}
