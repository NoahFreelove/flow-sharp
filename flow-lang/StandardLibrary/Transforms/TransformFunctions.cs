using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Transforms;

/// <summary>
/// Registers pattern transform functions for sequences: transpose, invert, retrograde,
/// augment, diminish, up, down, repeat, and concat.
/// </summary>
public static class TransformFunctions
{
    private const int MIDI_MIN = 16;  // E0
    private const int MIDI_MAX = 136; // E10

    public static void Register(InternalFunctionRegistry registry)
    {
        RegisterTranspose(registry);
        RegisterInvert(registry);
        RegisterRetrograde(registry);
        RegisterAugmentDiminish(registry);
        RegisterOctaveShift(registry);
        RegisterRepeat(registry);
        RegisterConcat(registry);
        RegisterDynamicTransforms(registry);
        RegisterTempoTransforms(registry);
        RegisterHumanize(registry);
        RegisterOrnamentTransforms(registry);
    }

    /// <summary>
    /// Phase 22 DX-14 (plan 22-06): registers <c>legato(Sequence, Double)</c> and
    /// <c>portamento(Sequence, Millisecond)</c> articulation transforms. Both set per-note
    /// defaulted-parameter fields (<see cref="MusicalNoteData.DurationOverlap"/>,
    /// <see cref="MusicalNoteData.PortamentoMs"/>) consumed by <c>BarRenderer</c> and
    /// <c>MidiExport</c> at render time.
    ///
    /// Per CONTEXT D-02 + Pitfall 3: onsets are NOT moved. The transforms only stamp
    /// fields onto each note; <c>bar.ToTimeline()</c> already produced the onset positions
    /// before render time. The audio renderer extends durationBeats; MIDI export emits
    /// extended NoteOff + CC65/CC5 bracket events.
    ///
    /// Per CONTEXT line 18 (rollback-independence): each transform calls
    /// <c>note.With(...)</c> naming ONLY the field this plan owns. Sibling 22-05's
    /// <c>OnsetOffset</c> and the other 22-06 slot are preserved by the builder helper's
    /// null-coalesce, so neither transform enumerates fields it doesn't own.
    /// </summary>
    public static void RegisterArticulationTransforms(InternalFunctionRegistry registry)
    {
        // legato(Sequence, Double) -> Sequence
        var legatoSig = new FunctionSignature("legato",
            [SequenceType.Instance, DoubleType.Instance]);
        registry.Register("legato", legatoSig, args =>
        {
            var seq = args[0].As<SequenceData>();
            double overlap = args[1].As<double>();
            return Value.Sequence(TransformNotes(seq, note =>
                note.With(durationOverlap: overlap)));
        });

        // portamento(Sequence, Millisecond) -> Sequence
        var portamentoSig = new FunctionSignature("portamento",
            [SequenceType.Instance, MillisecondType.Instance]);
        registry.Register("portamento", portamentoSig, args =>
        {
            var seq = args[0].As<SequenceData>();
            double ms = args[1].As<double>();   // Millisecond is backed by double
            return Value.Sequence(TransformNotes(seq, note =>
                note.With(portamentoMs: ms)));
        });
    }

    /// <summary>
    /// Phase 22 DX-13: registers <c>quantize(Sequence, NoteValue, strength, swing)</c> which
    /// reads <see cref="MusicalContext.TimeSignature"/> from the active context for grid alignment.
    ///
    /// Per Pitfall 9 (CRITICAL byte-identical regression gate): <c>strength=0</c> + <c>swing=0</c>
    /// short-circuits to identity (returns the input <c>SequenceData</c> unchanged) BEFORE any
    /// allocation. A single byte difference at strength=0 would break the ByteIdenticalTutorial /
    /// ByteIdenticalShowcase / EuclideanByteIdentical regression gate.
    ///
    /// Per V5 input validation (T-22-V5-17, T-22-V5-18) and CLAUDE.md charitable interpretation
    /// memory: strength is clamped to [0, 1] and swing is clamped to [-1, 1]. Out-of-range inputs
    /// are silently corrected — no exception, no error.
    ///
    /// Per CONTEXT D-04..D-06: linear swing offset = swing × (subdivBeats / 2), signed
    /// (positive = drag offbeat later, negative = push earlier), applied to every other
    /// subdivision at the requested resolution.
    /// </summary>
    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var quantizeSig = new FunctionSignature("quantize",
            [SequenceType.Instance, NoteValueType.Instance, DoubleType.Instance, DoubleType.Instance]);
        registry.Register("quantize", quantizeSig, args =>
        {
            var seq = args[0].As<SequenceData>();
            int resEnum = args[1].As<int>();
            // V5: clamp out-of-range inputs (charitable D-07; threats T-22-V5-17, T-22-V5-18).
            double strength = Math.Clamp(args[2].As<double>(), 0.0, 1.0);
            double swing = Math.Clamp(args[3].As<double>(), -1.0, 1.0);

            // Pitfall 9 — byte-identical regression gate. strength=0 + swing=0 MUST return the
            // input sequence object reference unchanged, or the ByteIdentical gate breaks.
            if (strength == 0.0 && swing == 0.0) return Value.Sequence(seq);

            var timesig = context.GetMusicalContext().TimeSignature
                ?? new TimeSignatureData(4, 4);
            return Value.Sequence(QuantizeSequence(seq, (NoteValueType.Value)resEnum,
                strength, swing, timesig));
        });
    }

    /// <summary>
    /// DX-13 implementation: walks each bar's notes sequentially, computes the nearest grid
    /// target at the requested resolution (with optional swing shift on every other
    /// subdivision), and stores the per-note onset displacement in <c>note.OnsetOffset</c>
    /// via the <c>With(...)</c> builder helper. <c>BarType.ToTimeline</c> later adds this
    /// offset to the emitted onset position so audio renderer + MIDI export both honor
    /// quantization without parallel rebuild paths.
    ///
    /// Uses <c>note.With(onsetOffset: …)</c> rather than the full ctor — this is intentional
    /// per Phase 22 CONTEXT line 18 (independent shippability): future Phase 22 plans
    /// (e.g. 22-06 legato/portamento) will append more defaulted fields, and this transform
    /// must not enumerate fields it doesn't own.
    /// </summary>
    private static SequenceData QuantizeSequence(
        SequenceData seq, NoteValueType.Value resolution,
        double strength, double swing, TimeSignatureData timesig)
    {
        // resolution → subdivision length in beats (where 1 beat == 1/timesig.Denominator whole).
        // QUARTER at 4/4 = 1 beat per subdivision; EIGHTH at 4/4 = 0.5 beats; SIXTEENTH = 0.25 beats.
        double subdivBeats = NoteValueToBeats(resolution, timesig.Denominator);
        // CONTEXT D-04: linear swing offset = swing × (subdivBeats / 2).
        double swingOffset = swing * (subdivBeats / 2.0);

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            double currentBeat = 0.0;
            int subdivIdx = 0;
            foreach (var note in bar.MusicalNotes)
            {
                double targetGrid = Math.Round(currentBeat / subdivBeats) * subdivBeats;
                // CONTEXT D-06: every other subdivision (the offbeat) receives the swing shift.
                if (subdivIdx % 2 == 1) targetGrid += swingOffset;

                // strength=1 hard-snap; strength=0 no shift; linear interpolation between.
                double snappedBeat = currentBeat + strength * (targetGrid - currentBeat);
                double onsetShift = snappedBeat - currentBeat;

                // Builder-helper rebuild — preserves all other fields, even ones added by
                // future Phase 22 plans, without naming them here. Rollback-independent.
                newNotes.Add(note.With(onsetOffset: onsetShift));

                currentBeat += note.GetBeats(timesig.Denominator);
                subdivIdx++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature ?? timesig));
        }
        return result;
    }

    /// <summary>
    /// DX-13: converts a NoteValue resolution into beats per subdivision for the active
    /// time signature. Charitable D-07: out-of-range enum values fall through to the
    /// default arm and are treated as a quarter note (no exception, no crash).
    /// </summary>
    private static double NoteValueToBeats(NoteValueType.Value nv, int denom)
    {
        // 1 beat == 1/denom whole. So WHOLE = denom beats; QUARTER = denom/4 beats.
        double whole = denom;
        return nv switch
        {
            NoteValueType.Value.WHOLE        => whole,
            NoteValueType.Value.HALF         => whole / 2,
            NoteValueType.Value.QUARTER      => whole / 4,
            NoteValueType.Value.EIGHTH       => whole / 8,
            NoteValueType.Value.SIXTEENTH    => whole / 16,
            NoteValueType.Value.THIRTYSECOND => whole / 32,
            _                                => whole / 4,
        };
    }

    // ===== MIDI Helpers =====

    private static int ToMidi(char noteName, int octave, int alteration)
    {
        int noteOffset = noteName switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
            'G' => 7, 'A' => 9, 'B' => 11,
            _ => throw new ArgumentException($"Invalid note name: {noteName}")
        };
        return (octave + 1) * 12 + noteOffset + alteration;
    }

    private static (char NoteName, int Octave, int Alteration) FromMidi(int midi)
    {
        int octave = (midi / 12) - 1;
        int pitchClass = midi % 12;
        if (pitchClass < 0) { pitchClass += 12; octave--; }

        // Map chromatic pitches to naturals + alteration (sharps only)
        var (noteName, alteration) = pitchClass switch
        {
            0  => ('C', 0),
            1  => ('C', 1),
            2  => ('D', 0),
            3  => ('D', 1),
            4  => ('E', 0),
            5  => ('F', 0),
            6  => ('F', 1),
            7  => ('G', 0),
            8  => ('G', 1),
            9  => ('A', 0),
            10 => ('A', 1),
            11 => ('B', 0),
            _  => ('C', 0)
        };

        return (noteName, octave, alteration);
    }

    /// <summary>
    /// Applies a transform function to every note in every bar, producing a new SequenceData.
    /// </summary>
    private static SequenceData TransformNotes(SequenceData seq, Func<MusicalNoteData, MusicalNoteData> transform)
    {
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                newNotes.Add(transform(note));
            }
            var newBar = new BarData(newNotes, bar.TimeSignature!);
            result.AddBar(newBar);
        }
        return result;
    }

    // ===== Transpose =====

    private static void RegisterTranspose(InternalFunctionRegistry registry)
    {
        // transpose(Sequence, Semitone)
        var transposeSemitoneSig = new FunctionSignature("transpose",
            [SequenceType.Instance, SemitoneType.Instance]);
        registry.Register("transpose", transposeSemitoneSig, TransposeSemitone);

        // transpose(Sequence, Cent)
        var transposeCentSig = new FunctionSignature("transpose",
            [SequenceType.Instance, CentType.Instance]);
        registry.Register("transpose", transposeCentSig, TransposeCent);
    }

    /// <remarks>
    /// Phase 23 D-12 / MICR-02 caveat: under non-12-TET tunings (justIntonation,
    /// pythagorean), transpose may silently respell notes at enharmonic junctions
    /// (e.g., F#4 → Gb4 round-trip), producing an audible ~21 cent shift in the
    /// rendered output even though the MIDI number is preserved. Transforms remain
    /// MIDI-based by design (MICR-02): same MIDI numbers under all 3 tunings; only
    /// the rendered Hz differ. A strict-mode <c>transposePreserveSpelling</c> escape
    /// hatch is documented as a v1.4 candidate — see CONTEXT.md D-12 +
    /// REQUIREMENTS.md "Future Requirements".
    /// </remarks>
    private static Value TransposeSemitone(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int semitones = args[1].As<int>();

        var result = TransformNotes(seq, note =>
        {
            if (note.IsRest) return note;

            int midi = ToMidi(note.NoteName, note.Octave, note.Alteration) + semitones;

            if (midi < MIDI_MIN || midi > MIDI_MAX)
            {
                int clamped = Math.Clamp(midi, MIDI_MIN, MIDI_MAX);
                Console.Error.WriteLine(
                    $"Warning: transpose would put {NoteType.Format(note.NoteName, note.Octave, note.Alteration)} " +
                    $"out of range (MIDI {midi}), clamping to MIDI {clamped}");
                midi = clamped;
            }

            var (name, oct, alt) = FromMidi(midi);
            return new MusicalNoteData(name, oct, alt, note.DurationValue, isRest: false, note.CentOffset, note.IsTied, note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength);
        });

        return Value.Sequence(result);
    }

    private static Value TransposeCent(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double cents = args[1].As<double>();
        int semitones = (int)Math.Round(cents / 100.0);

        if (Math.Abs(cents - semitones * 100.0) > 0.01)
        {
            Console.Error.WriteLine(
                $"Warning: transpose by {cents}c rounded to {semitones} semitones (not an exact multiple of 100c)");
        }

        // Delegate to semitone transpose
        return TransposeSemitone([args[0], Value.Semitone(semitones)]);
    }

    // ===== Invert =====

    private static void RegisterInvert(InternalFunctionRegistry registry)
    {
        var invertSig = new FunctionSignature("invert",
            [SequenceType.Instance]);
        registry.Register("invert", invertSig, Invert);
    }

    private static Value Invert(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        // Find the first non-rest note across all bars (the axis)
        int? axisMidi = null;
        foreach (var bar in seq.Bars)
        {
            foreach (var note in bar.MusicalNotes)
            {
                if (!note.IsRest)
                {
                    axisMidi = ToMidi(note.NoteName, note.Octave, note.Alteration);
                    break;
                }
            }
            if (axisMidi.HasValue) break;
        }

        // If no notes found (all rests), return a clone
        if (!axisMidi.HasValue)
            return Value.Sequence(TransformNotes(seq, n => n));

        int axis = axisMidi.Value;
        var result = TransformNotes(seq, note =>
        {
            if (note.IsRest) return note;

            int midi = ToMidi(note.NoteName, note.Octave, note.Alteration);
            int inverted = 2 * axis - midi;
            inverted = Math.Clamp(inverted, MIDI_MIN, MIDI_MAX);

            var (name, oct, alt) = FromMidi(inverted);
            return new MusicalNoteData(name, oct, alt, note.DurationValue, isRest: false, note.CentOffset, note.IsTied, note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength);
        });

        return Value.Sequence(result);
    }

    // ===== Retrograde =====

    private static void RegisterRetrograde(InternalFunctionRegistry registry)
    {
        var retrogradeSig = new FunctionSignature("retrograde",
            [SequenceType.Instance]);
        registry.Register("retrograde", retrogradeSig, Retrograde);
    }

    private static Value Retrograde(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        // True retrograde: reverse both the bar order AND the notes within each bar
        var reversedBars = new List<BarData>();
        foreach (var bar in seq.Bars)
        {
            var reversedNotes = new List<MusicalNoteData>(bar.MusicalNotes);
            reversedNotes.Reverse();
            reversedBars.Add(new BarData(reversedNotes, bar.TimeSignature!));
        }
        reversedBars.Reverse();

        var result = new SequenceData();
        foreach (var bar in reversedBars)
            result.AddBar(bar);
        return Value.Sequence(result);
    }

    // ===== Augment / Diminish =====

    private static void RegisterAugmentDiminish(InternalFunctionRegistry registry)
    {
        var augmentSig = new FunctionSignature("augment",
            [SequenceType.Instance]);
        registry.Register("augment", augmentSig, Augment);

        var diminishSig = new FunctionSignature("diminish",
            [SequenceType.Instance]);
        registry.Register("diminish", diminishSig, Diminish);
    }

    // AUDIT-VERIFIED 2026-04-18: C5 — augment correct (lengthens); observed A=#### vs Q=## columns in visualize (tests/spike/c5-augment-diminish.flow)
    // AUDIT-VERIFIED 2026-04-26: re-validated against tuplet sequences (Phase 19 TUP-07)
    //   — see flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs (rational doubling pinned via Fraction(2, 1) × f)
    private static Value Augment(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        var result = TransformNotes(seq, note =>
        {
            // TUP-07: rational branch — when DurationFraction is set, double it via Phase 18 Fraction(2,1) × f.
            // Existing enum path runs verbatim when DurationFraction is null (Phase 18 byte-identical contract).
            if (note.DurationFraction.HasValue)
            {
                var doubled = note.DurationFraction.Value * new Fraction(2, 1);
                return new MusicalNoteData(
                    note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    note.Velocity, note.Articulation, note.IsDotted,
                    note.SourceLocation, note.SourceLength,
                    durationFraction: doubled);
            }

            if (!note.DurationValue.HasValue) return note;

            int newDur = note.DurationValue.Value - 1; // toward WHOLE=0
            if (newDur < (int)NoteValueType.Value.WHOLE)
            {
                Console.Error.WriteLine("Warning: augment clamped duration at whole note");
                newDur = (int)NoteValueType.Value.WHOLE;
            }

            return new MusicalNoteData(note.NoteName, note.Octave, note.Alteration, newDur, note.IsRest, note.CentOffset, note.IsTied, note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength);
        });

        return Value.Sequence(result);
    }

    // AUDIT-VERIFIED 2026-04-18: C5 — diminish correct (shortens); observed D=# vs Q=## columns in visualize (tests/spike/c5-augment-diminish.flow)
    // AUDIT-VERIFIED 2026-04-26: re-validated against tuplet sequences (Phase 19 TUP-07)
    //   — see flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs (rational halving pinned via Fraction(1, 2) × f)
    private static Value Diminish(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();

        var result = TransformNotes(seq, note =>
        {
            // TUP-07: rational branch — when DurationFraction is set, halve it via Phase 18 Fraction(1,2) × f.
            // Existing enum path runs verbatim when DurationFraction is null (Phase 18 byte-identical contract).
            if (note.DurationFraction.HasValue)
            {
                var halved = note.DurationFraction.Value * new Fraction(1, 2);
                return new MusicalNoteData(
                    note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    note.Velocity, note.Articulation, note.IsDotted,
                    note.SourceLocation, note.SourceLength,
                    durationFraction: halved);
            }

            if (!note.DurationValue.HasValue) return note;

            int newDur = note.DurationValue.Value + 1; // toward THIRTYSECOND=5
            if (newDur > (int)NoteValueType.Value.THIRTYSECOND)
            {
                Console.Error.WriteLine("Warning: diminish clamped duration at thirty-second note");
                newDur = (int)NoteValueType.Value.THIRTYSECOND;
            }

            return new MusicalNoteData(note.NoteName, note.Octave, note.Alteration, newDur, note.IsRest, note.CentOffset, note.IsTied, note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength);
        });

        return Value.Sequence(result);
    }

    // ===== Test wrappers =====
    // Public test wrappers expose the private dispatch for direct C# unit tests
    // (per Phase 19 Plan 19-05 TUP-07 — flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs).
    // Production callers continue routing through the registry's `augment` / `diminish` signatures.
    public static SequenceData AugmentForTesting(SequenceData seq) =>
        Augment(new List<Value> { Value.Sequence(seq) }).As<SequenceData>();

    public static SequenceData DiminishForTesting(SequenceData seq) =>
        Diminish(new List<Value> { Value.Sequence(seq) }).As<SequenceData>();

    // ===== Octave Shift =====

    private static void RegisterOctaveShift(InternalFunctionRegistry registry)
    {
        var upSig = new FunctionSignature("up",
            [SequenceType.Instance, IntType.Instance]);
        registry.Register("up", upSig, OctaveUp);

        var downSig = new FunctionSignature("down",
            [SequenceType.Instance, IntType.Instance]);
        registry.Register("down", downSig, OctaveDown);
    }

    private static Value OctaveUp(IReadOnlyList<Value> args)
    {
        int octaves = args[1].As<int>();
        return TransposeSemitone([args[0], Value.Semitone(octaves * 12)]);
    }

    private static Value OctaveDown(IReadOnlyList<Value> args)
    {
        int octaves = args[1].As<int>();
        return TransposeSemitone([args[0], Value.Semitone(-octaves * 12)]);
    }

    // ===== Repeat =====

    private static void RegisterRepeat(InternalFunctionRegistry registry)
    {
        // repeat(Sequence, Int)
        var repeatSig = new FunctionSignature("repeat",
            [SequenceType.Instance, IntType.Instance]);
        registry.Register("repeat", repeatSig, Repeat);

        // repeat(Sequence, Int, Semitone)
        var repeatTransposeSig = new FunctionSignature("repeat",
            [SequenceType.Instance, IntType.Instance, SemitoneType.Instance]);
        registry.Register("repeat", repeatTransposeSig, RepeatTranspose);
    }

    private static Value Repeat(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int times = args[1].As<int>();

        var result = new SequenceData();
        for (int i = 0; i < times; i++)
        {
            foreach (var bar in seq.Bars)
            {
                var newBar = new BarData(new List<MusicalNoteData>(bar.MusicalNotes), bar.TimeSignature!);
                result.AddBar(newBar);
            }
        }
        return Value.Sequence(result);
    }

    private static Value RepeatTranspose(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int times = args[1].As<int>();
        int semitones = args[2].As<int>();

        var result = new SequenceData();
        for (int i = 0; i < times; i++)
        {
            int cumulativeTranspose = i * semitones;
            foreach (var bar in seq.Bars)
            {
                var newNotes = new List<MusicalNoteData>();
                foreach (var note in bar.MusicalNotes)
                {
                    if (note.IsRest)
                    {
                        newNotes.Add(note);
                        continue;
                    }

                    int midi = ToMidi(note.NoteName, note.Octave, note.Alteration) + cumulativeTranspose;
                    midi = Math.Clamp(midi, MIDI_MIN, MIDI_MAX);
                    var (name, oct, alt) = FromMidi(midi);
                    newNotes.Add(new MusicalNoteData(name, oct, alt, note.DurationValue, isRest: false, note.CentOffset, note.IsTied, note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                }
                var newBar = new BarData(newNotes, bar.TimeSignature!);
                result.AddBar(newBar);
            }
        }
        return Value.Sequence(result);
    }

    // ===== Concat =====

    private static void RegisterConcat(InternalFunctionRegistry registry)
    {
        var concatSig = new FunctionSignature("concat",
            [SequenceType.Instance, SequenceType.Instance]);
        registry.Register("concat", concatSig, ConcatSequences);
    }

    private static Value ConcatSequences(IReadOnlyList<Value> args)
    {
        var seqA = args[0].As<SequenceData>();
        var seqB = args[1].As<SequenceData>();

        var result = new SequenceData();
        foreach (var bar in seqA.Bars)
        {
            var newBar = new BarData(new List<MusicalNoteData>(bar.MusicalNotes), bar.TimeSignature!);
            result.AddBar(newBar);
        }
        foreach (var bar in seqB.Bars)
        {
            var newBar = new BarData(new List<MusicalNoteData>(bar.MusicalNotes), bar.TimeSignature!);
            result.AddBar(newBar);
        }
        return Value.Sequence(result);
    }

    // ===== Dynamic Transforms =====

    private static void RegisterDynamicTransforms(InternalFunctionRegistry registry)
    {
        var crescSig = new FunctionSignature("crescendo",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance]);
        registry.Register("crescendo", crescSig, Crescendo);

        var decrescSig = new FunctionSignature("decrescendo",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance]);
        registry.Register("decrescendo", decrescSig, Decrescendo);

        var swellSig = new FunctionSignature("swell",
            [SequenceType.Instance, DoubleType.Instance, DoubleType.Instance]);
        registry.Register("swell", swellSig, Swell);
    }

    private static Value Crescendo(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double startVel = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
        double endVel = Math.Clamp(args[2].As<double>(), 0.0, 1.0);
        return Value.Sequence(ApplyVelocityGradient(seq, startVel, endVel));
    }

    private static Value Decrescendo(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double startVel = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
        double endVel = Math.Clamp(args[2].As<double>(), 0.0, 1.0);
        // Reverse the velocity gradient: decrescendo goes from endVel down to startVel
        return Value.Sequence(ApplyVelocityGradient(seq, endVel, startVel));
    }

    private static Value Swell(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double edgeVel = Math.Clamp(args[1].As<double>(), 0.0, 1.0);
        double peakVel = Math.Clamp(args[2].As<double>(), 0.0, 1.0);

        int totalNotes = 0;
        foreach (var bar in seq.Bars)
            foreach (var note in bar.MusicalNotes)
                if (!note.IsRest) totalNotes++;

        if (totalNotes <= 1)
            return Value.Sequence(seq);

        int midpoint = totalNotes / 2;
        int noteIndex = 0;

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest)
                {
                    newNotes.Add(note);
                    continue;
                }

                double t;
                int descendLength = totalNotes - 1 - midpoint;
                if (noteIndex <= midpoint && midpoint > 0)
                    t = (double)noteIndex / midpoint;
                else if (descendLength > 0)
                    t = 1.0 - ((double)(noteIndex - midpoint) / descendLength);
                else
                    t = 1.0;

                double velocity = Math.Clamp(edgeVel + t * (peakVel - edgeVel), 0.0, 1.0);

                newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                noteIndex++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    private static SequenceData ApplyVelocityGradient(SequenceData seq, double startVel, double endVel)
    {
        int totalNotes = 0;
        foreach (var bar in seq.Bars)
            foreach (var note in bar.MusicalNotes)
                if (!note.IsRest) totalNotes++;

        if (totalNotes <= 1)
        {
            return TransformNotes(seq, note =>
            {
                if (note.IsRest) return note;
                return new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    startVel, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength);
            });
        }

        int noteIndex = 0;
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest)
                {
                    newNotes.Add(note);
                    continue;
                }

                double t = (double)noteIndex / (totalNotes - 1);
                double velocity = Math.Clamp(startVel + t * (endVel - startVel), 0.0, 1.0);

                newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                noteIndex++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return result;
    }

    // ===== Tempo Transforms =====

    private static void RegisterTempoTransforms(InternalFunctionRegistry registry)
    {
        var ritSig = new FunctionSignature("ritardando",
            [SequenceType.Instance, DoubleType.Instance]);
        registry.Register("ritardando", ritSig, RitardandoTransform);

        var accelSig = new FunctionSignature("accelerando",
            [SequenceType.Instance, DoubleType.Instance]);
        registry.Register("accelerando", accelSig, AccelerandoTransform);

        var fermataSig = new FunctionSignature("fermata",
            [SequenceType.Instance, IntType.Instance]);
        registry.Register("fermata", fermataSig, FermataTransform);
    }

    /// <summary>
    /// Ritardando: progressively stretch note durations. Amount 0.5 = last note 1.5x duration.
    /// We approximate by adjusting velocity downward for later notes (lower velocity sounds
    /// "slower" perceptually).
    /// </summary>
    private static Value RitardandoTransform(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);

        int totalNotes = 0;
        foreach (var bar in seq.Bars)
            foreach (var note in bar.MusicalNotes)
                if (!note.IsRest) totalNotes++;

        if (totalNotes <= 1) return Value.Sequence(seq);

        int noteIndex = 0;
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest) { newNotes.Add(note); continue; }

                double t = (double)noteIndex / (totalNotes - 1);
                // Reduce velocity slightly for rit feel (later = softer = perceived slower)
                double velReduction = t * amount * 0.3;
                double newVel = Math.Clamp(note.Velocity - velReduction, 0.05, 1.0);

                newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    newVel, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                noteIndex++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    private static Value AccelerandoTransform(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);

        int totalNotes = 0;
        foreach (var bar in seq.Bars)
            foreach (var note in bar.MusicalNotes)
                if (!note.IsRest) totalNotes++;

        if (totalNotes <= 1) return Value.Sequence(seq);

        int noteIndex = 0;
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest) { newNotes.Add(note); continue; }

                double t = (double)noteIndex / (totalNotes - 1);
                // Increase velocity slightly for accel feel (later = louder = perceived faster)
                double velBoost = t * amount * 0.3;
                double newVel = Math.Clamp(note.Velocity + velBoost, 0.05, 1.0);

                newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    newVel, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                noteIndex++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    /// <summary>
    /// Fermata: hold the note at the given index for 2x its normal duration (move to next
    /// larger duration value).
    /// </summary>
    private static Value FermataTransform(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int targetIdx = args[1].As<int>();

        int noteIndex = 0;
        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (!note.IsRest && noteIndex == targetIdx && note.DurationValue.HasValue)
                {
                    // Augment: move to next larger duration (e.g. quarter -> half)
                    int newDur = Math.Max(note.DurationValue.Value - 1, (int)NoteValueType.Value.WHOLE);
                    newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                        newDur, note.IsRest, note.CentOffset, note.IsTied,
                        note.Velocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
                }
                else
                {
                    newNotes.Add(note);
                }
                if (!note.IsRest) noteIndex++;
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    // ===== Humanize =====

    private static void RegisterHumanize(InternalFunctionRegistry registry)
    {
        var humanizeSig = new FunctionSignature("humanize",
            [SequenceType.Instance, DoubleType.Instance]);
        registry.Register("humanize", humanizeSig, Humanize);
    }

    private static readonly Random HumanizeRng = new();

    private static Value Humanize(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest)
                {
                    newNotes.Add(note);
                    continue;
                }

                // Velocity jitter: random variation scaled by amount
                double velJitter = (HumanizeRng.NextDouble() * 2.0 - 1.0) * amount * 0.2;
                double newVelocity = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0);

                newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                    newVelocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    // ===== Ornament Transforms (Trill / Tremolo) =====

    private static void RegisterOrnamentTransforms(InternalFunctionRegistry registry)
    {
        var trillSig = new FunctionSignature("trill",
            [SequenceType.Instance, SemitoneType.Instance]);
        registry.Register("trill", trillSig, Trill);

        var tremSig = new FunctionSignature("tremolo",
            [SequenceType.Instance, IntType.Instance]);
        registry.Register("tremolo", tremSig, Tremolo);
    }

    private static Value Trill(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int semitones = args[1].As<int>();

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest || !note.DurationValue.HasValue)
                {
                    newNotes.Add(note);
                    continue;
                }

                // Split into rapid alternation: note -> upper -> note -> upper
                int trillDur = Math.Min(note.DurationValue.Value + 2, (int)NoteValueType.Value.THIRTYSECOND);
                int midi = ToMidi(note.NoteName, note.Octave, note.Alteration);
                int upperMidi = Math.Clamp(midi + semitones, MIDI_MIN, MIDI_MAX);
                var (upperName, upperOct, upperAlt) = FromMidi(upperMidi);

                // 4 alternations
                for (int i = 0; i < 4; i++)
                {
                    if (i % 2 == 0)
                        newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                            trillDur, false, note.CentOffset, false, note.Velocity, note.Articulation, sourceLocation: note.SourceLocation, sourceLength: note.SourceLength));
                    else
                        newNotes.Add(new MusicalNoteData(upperName, upperOct, upperAlt,
                            trillDur, false, velocity: note.Velocity, sourceLocation: note.SourceLocation, sourceLength: note.SourceLength));
                }
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }

    private static Value Tremolo(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        int reps = Math.Clamp(args[1].As<int>(), 1, 16);

        var result = new SequenceData();
        foreach (var bar in seq.Bars)
        {
            var newNotes = new List<MusicalNoteData>();
            foreach (var note in bar.MusicalNotes)
            {
                if (note.IsRest || !note.DurationValue.HasValue)
                {
                    newNotes.Add(note);
                    continue;
                }

                // Subdivide: use a smaller duration for each repetition
                int subDur = Math.Min(note.DurationValue.Value + 2, (int)NoteValueType.Value.THIRTYSECOND);
                for (int i = 0; i < reps; i++)
                {
                    newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                        subDur, false, note.CentOffset, false, note.Velocity, note.Articulation, sourceLocation: note.SourceLocation, sourceLength: note.SourceLength));
                }
            }
            result.AddBar(new BarData(newNotes, bar.TimeSignature!));
        }
        return Value.Sequence(result);
    }
}
