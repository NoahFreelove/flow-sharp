namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a musical note with octave notation (e.g., A4, C3, E0-E10 range).
/// Default octave is 4 (middle octave).
/// </summary>
public sealed class NoteType : FlowType
{
    private NoteType() { }

    public static NoteType Instance { get; } = new();

    public override string Name => "Note";

    public override int GetSpecificity() => 130;

    public override bool IsHashable() => true;

    /// <summary>
    /// Parses a note string like "A4", "C3", "G" (defaults to octave 4) into a note value.
    ///
    /// Phase 14 DX-06 (CONTEXT D-07/D-08/D-09): accepts arbitrary composition of
    /// <c>b</c>/<c>#</c>/<c>+</c>/<c>-</c> on either side of octave digits. Net alteration =
    /// (count of sharps <c>#</c>+<c>+</c>) − (count of flats <c>b</c>+<c>-</c>). Alteration
    /// is any int (not bounded to ±2). Range validation uses post-alteration MIDI value.
    ///
    /// Examples:
    ///   <c>"Db4"</c>   → (D, 4, -1)
    ///   <c>"Bb"</c>    → (B, 4, -1) (default octave 4)
    ///   <c>"C#5"</c>   → (C, 5, +1)
    ///   <c>"F##4"</c>  → (F, 4, +2)
    ///   <c>"Bb-+bbb"</c> → (B, 4, -4)
    ///   <c>"Cb4"</c>   → (C, 4, -1) MIDI 59 = B3, in range
    ///   <c>"Cb0"</c>   → throws ArgumentException (post-alt MIDI 11, below E0=16)
    /// </summary>
    public static (char note, int octave, int alteration) Parse(string noteStr)
        => Parse(noteStr, 4);

    /// <summary>
    /// Same as <see cref="Parse(string)"/> but uses <paramref name="defaultOctave"/> for
    /// bare note letters that omit an explicit octave digit (e.g. <c>"C"</c> → octave
    /// <paramref name="defaultOctave"/>). An explicit digit in <paramref name="noteStr"/>
    /// always wins (e.g. <c>"C5"</c> is octave 5 regardless of the default). This is the
    /// seam through which an <c>octave N { ... }</c> musical-context block reaches bare
    /// letters in a note stream (NoteStreamCompiler passes the frame-resolved
    /// <c>MusicalContext.DefaultOctave</c>, coalescing null to 4).
    /// </summary>
    public static (char note, int octave, int alteration) Parse(string noteStr, int defaultOctave)
    {
        if (string.IsNullOrEmpty(noteStr))
            throw new ArgumentException("Note string cannot be empty");

        char note = char.ToUpper(noteStr[0]);
        if (note < 'A' || note > 'G')
            throw new ArgumentException($"Invalid note: {note}. Must be A-G.");

        // Sum-based scan across the remaining chars (D-07). Three phases:
        //   1. Pre-octave alteration chars (b/#/+/-)
        //   2. Octave digits (contiguous)
        //   3. Post-octave alteration chars (b/#/+/-)
        int sharpCount = 0;
        int flatCount = 0;
        int octave = defaultOctave; // Default octave when no digits present
        int i = 1;

        // Phase 1: pre-octave alterations
        while (i < noteStr.Length && !char.IsDigit(noteStr[i]))
        {
            switch (noteStr[i])
            {
                case '+':
                case '#':
                    sharpCount++;
                    break;
                case '-':
                case 'b':
                    flatCount++;
                    break;
                default:
                    throw new ArgumentException($"Invalid note character '{noteStr[i]}' in {noteStr}");
            }
            i++;
        }

        // Phase 2: octave digits
        int octStart = i;
        while (i < noteStr.Length && char.IsDigit(noteStr[i]))
        {
            i++;
        }
        if (i > octStart)
        {
            octave = int.Parse(noteStr[octStart..i]);
        }

        // Phase 3: post-octave alterations
        while (i < noteStr.Length)
        {
            switch (noteStr[i])
            {
                case '+':
                case '#':
                    sharpCount++;
                    break;
                case '-':
                case 'b':
                    flatCount++;
                    break;
                default:
                    throw new ArgumentException($"Invalid note character '{noteStr[i]}' in {noteStr}");
            }
            i++;
        }

        int alteration = sharpCount - flatCount;

        // Post-alteration MIDI range check (D-09): replaces letter+octave-only IsValidNoteRange.
        // Cb4 (MIDI 59 = B3) is in range; Cb0 (MIDI 11) is below E0 (MIDI 16) and throws.
        int midi = GetNoteValue(note, octave) + alteration;
        int minMidi = GetNoteValue('E', 0);
        int maxMidi = GetNoteValue('E', 10);
        if (midi < minMidi || midi > maxMidi)
        {
            throw new ArgumentException($"Note {noteStr} is out of valid range (E0 to E10)");
        }

        return (note, octave, alteration);
    }

    /// <summary>
    /// Converts a note and octave to a MIDI-like note number for range validation.
    /// Public so tests and helpers outside NoteType can compute ranges without duplicating
    /// the chromatic mapping.
    /// </summary>
    public static int GetNoteValue(char note, int octave)
    {
        int noteOffset = note switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => throw new ArgumentException($"Invalid note: {note}")
        };

        return (octave + 1) * 12 + noteOffset; // C0 = 12
    }

    public static int ToMidiNote(char note, int octave, int alteration)
    {
        return GetNoteValue(note, octave) + alteration;
    }

    public static (char note, int octave, int alteration) FromMidiNote(int midiNote)
    {
        if (midiNote < 12 || midiNote > 127) throw new ArgumentOutOfRangeException(nameof(midiNote));
        int octave = (midiNote / 12) - 1;
        int noteNum = midiNote % 12;

        return noteNum switch
        {
            0 => ('C', octave, 0),
            1 => ('C', octave, 1),
            2 => ('D', octave, 0),
            3 => ('D', octave, 1),
            4 => ('E', octave, 0),
            5 => ('F', octave, 0),
            6 => ('F', octave, 1),
            7 => ('G', octave, 0),
            8 => ('G', octave, 1),
            9 => ('A', octave, 0),
            10 => ('A', octave, 1),
            11 => ('B', octave, 0),
            _ => throw new InvalidOperationException()
        };
    }

    /// <summary>
    /// Formats a note value back to string representation.
    ///
    /// Phase 14 DX-06 (CONTEXT D-08): run-based emission for any int alteration.
    /// Canonical shape: <c>{letter}{octave}{'+' * n | '-' * |n|}</c>. Round-trip invariant
    /// <c>Parse(Format(x)) == x</c> holds for every integer alteration (bounded only by the
    /// post-alteration MIDI range check in Parse).
    /// </summary>
    public static string Format(char note, int octave, int alteration)
    {
        string altStr;
        if (alteration == 0)
        {
            altStr = "";
        }
        else if (alteration > 0)
        {
            altStr = new string('+', alteration);
        }
        else
        {
            altStr = new string('-', -alteration);
        }

        return $"{note}{octave}{altStr}";
    }
}

/// <summary>
/// Articulation affects how a note's envelope is shaped.
/// Phase 28 (SPEC-3): Legato is a first-class articulation value here, separate from the
/// Phase 22 legato() transform which adjusts DurationOverlap. The Articulation.Legato value
/// is what `leg` after a note in a `|...|` stream produces; renderers extend its sounding
/// duration ~110% with a soft crossfade (BarRenderer applies the duration multiplier; per-synth
/// envelopes apply the soft release).
/// </summary>
public enum Articulation
{
    Normal,     // Default envelope
    Staccato,   // Short, detached (~50% duration)
    Tenuto,     // Full sustain, held to full value
    Marcato,    // Accented + slightly shortened
    Accent,     // Velocity bump, normal duration
    Sforzando,  // Sudden loud spike, then return to previous dynamic
    Legato      // Phase 28: extended duration (~110%) with soft crossfade into next note
}

/// <summary>
/// Represents a musical note with pitch, duration, and rest information for classical composition.
/// </summary>
public class MusicalNoteData
{
    public char NoteName { get; }
    public int Octave { get; }
    public int Alteration { get; }
    public int? DurationValue { get; }
    public bool IsRest { get; }
    public double? CentOffset { get; }
    public bool IsTied { get; }
    public bool IsDotted { get; }
    public double Velocity { get; }
    public Articulation Articulation { get; }

    /// <summary>
    /// Optional source location for timeline mapping (editor live highlighting).
    /// </summary>
    public FlowLang.Core.SourceLocation? SourceLocation { get; }

    /// <summary>
    /// Length of the original source token (e.g., "C4" = 2, "C4q." = 4) for precise highlighting.
    /// </summary>
    public int SourceLength { get; }

    /// <summary>
    /// Optional rational duration override (FRAC-02). When set, OVERRIDES the DurationValue
    /// enum + IsDotted multiplier in GetBeats. When null, the existing power-of-2 enum path
    /// runs unchanged. Phase 18 ships this field DORMANT — no lexer/parser code path produces
    /// a non-null value yet (Phase 19 tuplets feed it). Per D-USER-04, all existing .flow
    /// scripts must remain byte-identical because every call site leaves this null.
    ///
    /// Units: quarter-note units (matches music21 DurationTuple convention). To convert to
    /// beats for a time signature with denominator D: beats = quarterNotes × (D / 4).
    /// </summary>
    public FlowLang.TypeSystem.Fraction? DurationFraction { get; }

    /// <summary>
    /// Phase 22 DX-13 quantize: per-note onset shift in beats, added by bar.ToTimeline()
    /// to the accumulated onset position. Default 0.0 = onset stays at sequential position.
    /// Used by quantize() to snap onsets to a grid without rebuilding the bar list.
    /// </summary>
    public double OnsetOffset { get; }

    /// <summary>
    /// Phase 22 DX-14 legato: render-time duration extension factor. 0.0 = no extension;
    /// 0.5 = play 1.5× longer; 1.0 = play 2× longer. Read by BarRenderer + MidiExport
    /// AFTER bar.ToTimeline() produces onsets, so onsets are NOT moved (CONTEXT D-02).
    /// Polyphonic mix in SongRenderer handles overlapping voices automatically.
    /// </summary>
    public double DurationOverlap { get; }

    /// <summary>
    /// Phase 22 DX-14 portamento: glide time in milliseconds for MIDI CC5 mapping. 0.0 = no
    /// portamento. MidiExport emits CC65=127 + CC5=mappedValue at note start, CC65=0 at note
    /// end (per-note bracket). Linear ms→CC5: 0→0, 100→64, 200→127 clamped (CONTEXT Claude's
    /// Discretion). Audio renderer ignores this field — portamento is MIDI-only in v1.3.
    /// </summary>
    public double PortamentoMs { get; }

    /// <summary>
    /// True when this note is a non-leading tone of a polyphonic chord literal
    /// (e.g. the E and G of <c>[C E G]q</c>) emitted by NoteStreamCompiler.
    /// The leading tone of a chord (the first one in source order) keeps
    /// IsChordTone = false — it advances the bar's beat cursor and the
    /// remaining tones share its onset offset.
    ///
    /// Why this exists: chord literals expand to a flat <c>List&lt;MusicalNoteData&gt;</c>,
    /// and BarType.ToTimeline() / BarType.GetActualBeats() / MidiExport need
    /// to know which entries are stacked-at-the-same-offset (no cursor advance)
    /// vs. sequential. Default false preserves all non-chord paths
    /// (arpeggio() builtin, transforms, plain note streams).
    /// </summary>
    public bool IsChordTone { get; }

    public MusicalNoteData(char noteName, int octave, int alteration, int? durationValue, bool isRest, double? centOffset = null, bool isTied = false, double velocity = 0.63, Articulation articulation = Articulation.Normal, bool isDotted = false, FlowLang.Core.SourceLocation? sourceLocation = null, int sourceLength = 0, FlowLang.TypeSystem.Fraction? durationFraction = null, double onsetOffset = 0.0, double durationOverlap = 0.0, double portamentoMs = 0.0, bool isChordTone = false)
    {
        NoteName = noteName;
        Octave = octave;
        Alteration = alteration;
        DurationValue = durationValue;
        IsRest = isRest;
        CentOffset = centOffset;
        IsTied = isTied;
        IsDotted = isDotted;
        Velocity = Math.Clamp(velocity, 0.0, 1.0);
        Articulation = articulation;
        SourceLocation = sourceLocation;
        SourceLength = sourceLength;
        DurationFraction = durationFraction;
        OnsetOffset = onsetOffset;
        DurationOverlap = durationOverlap;
        PortamentoMs = portamentoMs;
        IsChordTone = isChordTone;
    }

    /// <summary>
    /// Phase 22 DX-13/DX-14 builder helper: returns a copy of this note with selected fields
    /// overridden. Each Phase 22 plan that adds a defaulted-parameter field also extends this
    /// helper with a matching nullable optional parameter. Transforms (quantize/legato/portamento)
    /// rebuild notes via With(...) instead of the full ctor so they don't enumerate fields they
    /// don't own — preserves rollback-independence per Phase 22 CONTEXT line 18.
    ///
    /// Plan 22-05 owns the <c>onsetOffset</c> slot; plan 22-06 (DX-14) appends
    /// <c>durationOverlap</c> and <c>portamentoMs</c> slots. Each plan's transforms name only
    /// the fields they own — null-coalesce passes existing values through unchanged so any
    /// single plan can roll back without breaking siblings.
    /// </summary>
    public MusicalNoteData With(
        double? onsetOffset = null,
        double? durationOverlap = null,
        double? portamentoMs = null,
        double? velocity = null,              // PHASE 25 (DEFER-06): velocity slot
        // Audit 2026-06-09 §4.2 pitch + duration slots: transforms that rebuild
        // notes (transpose / invert / augment / diminish / trill / tremolo /
        // fermata / repeat-transpose) used to call the 12-arg ctor and silently
        // drop the trailing five fields (IsChordTone / DurationFraction /
        // OnsetOffset / DurationOverlap / PortamentoMs) — re-arpeggiating chord
        // brackets, flattening tuplets, undoing quantize/legato/portamento.
        // Routing every rebuild through With(…) preserves untouched fields by
        // construction. Each slot is null = "keep existing"; pass a value to
        // override. (CentOffset is itself nullable, so the null-means-keep
        // convention can't clear it to null — no transform needs that.)
        char? noteName = null,
        int? octave = null,
        int? alteration = null,
        int? durationValue = null,
        double? centOffset = null,
        Articulation? articulation = null,
        bool? isDotted = null,
        bool? isTied = null,
        FlowLang.TypeSystem.Fraction? durationFraction = null,
        bool? isChordTone = null)
    {
        return new MusicalNoteData(
            noteName ?? NoteName,
            octave ?? Octave,
            alteration ?? Alteration,
            durationValue ?? DurationValue,
            IsRest,
            centOffset ?? CentOffset,
            isTied ?? IsTied,
            velocity ?? Velocity,             // PHASE 25 (DEFER-06): velocity override
            articulation ?? Articulation,
            isDotted ?? IsDotted,
            SourceLocation, SourceLength,
            durationFraction ?? DurationFraction,
            onsetOffset: onsetOffset ?? OnsetOffset,
            durationOverlap: durationOverlap ?? DurationOverlap,
            portamentoMs: portamentoMs ?? PortamentoMs,
            isChordTone: isChordTone ?? IsChordTone);
    }

    /// <summary>
    /// Calculates the duration of this note in QUARTER-NOTE units.
    ///
    /// sweep-0614: this returns quarter-note units (1 quarter = 1.0), NOT
    /// denominator-unit beats. Every wall-clock (SynthUtils.BeatsToSeconds,
    /// SongRenderer.secondsPerBeat) and tick (MidiExport ticksPerQuarter)
    /// conversion treats BPM as quarters-per-minute, so GetBeats MUST be
    /// quarter-relative for non-4/4 meters to render at the correct speed.
    /// (Prior to this fix the power-of-2 path returned denominator-units, which
    /// is accidentally correct only for 4/4 — 6/8 rendered 2× too slow, 2/2 2×
    /// too fast.) The <paramref name="timeSigDenominator"/> parameter is retained
    /// for signature compatibility but no longer scales the result: a quarter note
    /// is 1.0 quarters in every meter.
    /// </summary>
    public double GetBeats(int timeSigDenominator)
    {
        if (DurationFraction.HasValue)
        {
            // FRAC-02 rational override path. DurationFraction is already stored in
            // quarter-note units (music21 convention; e.g. a tuplet leaf of 2/3
            // quarter). Quarter-units is exactly what every consumer now expects,
            // so return it verbatim — no per-meter rescale.
            var f = DurationFraction.Value;
            return (double)f.Num / f.Denom;
        }

        if (!DurationValue.HasValue)
            return 1.0; // Default to 1 quarter if no duration specified

        // ToFraction is fraction-of-a-WHOLE-note (quarter=0.25); × 4 → quarter-units.
        double fraction = NoteValueType.ToFraction((NoteValueType.Value)DurationValue.Value);
        if (IsDotted) fraction *= 1.5;
        return fraction * 4.0;
    }

    public override string ToString()
    {
        if (IsRest)
        {
            string durationName = DurationValue.HasValue
                ? NoteValueType.Format((NoteValueType.Value)DurationValue.Value)
                : "quarter";
            return $"{durationName}Rest";
        }

        string noteStr = NoteType.Format(NoteName, Octave, Alteration);
        string durationName2 = DurationValue.HasValue
            ? NoteValueType.Format((NoteValueType.Value)DurationValue.Value)
            : "quarter";
        return $"{durationName2}({noteStr})";
    }
}
