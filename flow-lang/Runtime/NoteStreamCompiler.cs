using FlowLang.Ast.Expressions;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem;  // for Fraction (Phase 18)
using FlowLang.TypeSystem.SpecialTypes;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Runtime;

/// <summary>
/// Compiles a NoteStreamExpression into a SequenceData using the active MusicalContext.
/// Handles auto-fit duration calculation, rest insertion, and bar validation.
/// </summary>
public class NoteStreamCompiler
{
    /// <summary>
    /// Minimum difference between start/end velocities required to trigger interpolation.
    /// </summary>
    private const double VelocityInterpolationTolerance = 0.01;

    /// <summary>
    /// Minimum number of non-rest notes needed between endpoints to perform interpolation.
    /// </summary>
    private const int MinInterpolationNoteCount = 3;

    /// <summary>
    /// Maps duration suffix characters to NoteValue enum values.
    /// w=whole, h=half, q=quarter, e=eighth, s=sixteenth, t=32nd, x=64th, y=128th
    /// </summary>
    private static readonly Dictionary<string, NoteValueType.Value> DurationSuffixMap = new()
    {
        { "w", NoteValueType.Value.WHOLE },
        { "h", NoteValueType.Value.HALF },
        { "q", NoteValueType.Value.QUARTER },
        { "e", NoteValueType.Value.EIGHTH },
        { "s", NoteValueType.Value.SIXTEENTH },
        { "t", NoteValueType.Value.THIRTYSECOND },
        { "x", NoteValueType.Value.SIXTYFOURTH },
        { "y", NoteValueType.Value.ONETWENTYEIGHTH }
    };

    /// <summary>
    /// TUP-05: optional ErrorReporter for emitting Info-severity bar-overflow diagnostics.
    /// When null (e.g., parameterless ctor used by Plan 19-01/19-02 unit Facts), the
    /// validator silently skips diagnostic emission. Per CONTEXT D-17, ReportInfo is
    /// already in the ErrorReporter API (no API change needed).
    /// </summary>
    private readonly FlowLang.Diagnostics.ErrorReporter? _errorReporter;

    /// <summary>
    /// Default ctor — preserves backward compatibility for Plan 19-01/19-02 unit Facts
    /// that construct NoteStreamCompiler with no args. The TUP-05 bar-overflow Info
    /// diagnostic is emitted only when an ErrorReporter is wired via the overload.
    /// </summary>
    public NoteStreamCompiler() : this(null) { }

    /// <summary>
    /// TUP-05 ctor: takes an ErrorReporter for emitting Info-severity bar-overflow
    /// diagnostics. Production-path construction sites (ExpressionEvaluator) pass the
    /// engine's reporter; unit Facts use the parameterless ctor and silently skip
    /// diagnostic emission.
    /// </summary>
    public NoteStreamCompiler(FlowLang.Diagnostics.ErrorReporter? errorReporter)
    {
        _errorReporter = errorReporter;
    }

    /// <summary>
    /// Compiles a NoteStreamExpression into a SequenceData using the given musical context.
    /// </summary>
    public SequenceData Compile(NoteStreamExpression noteStream, MusicalContext context, ExecutionContext? executionContext = null)
    {
        var sequence = new SequenceData();
        var timeSig = context.TimeSignature ?? new TimeSignatureData(4, 4);

        // sweep-0614: resolve the active swing once. Context Swing is in [0.0, 1.0]
        // with 0.5 == straight; the onset-shift math (shared with the `quantize`
        // builtin) uses [-1.0, 1.0] with 0 == straight, so bridge the convention.
        // Short-circuit at exactly straight (or absent) so non-swing renders stay
        // byte-identical / two-run cmp-clean.
        double transformSwing = context.Swing.HasValue ? (context.Swing.Value - 0.5) * 2.0 : 0.0;

        foreach (var bar in noteStream.Bars)
        {
            var barData = CompileBar(bar, timeSig, context, executionContext);
            barData.IsPickup = bar.IsPickup;
            if (transformSwing != 0.0)
                ApplyContextSwing(barData, transformSwing);
            sequence.AddBar(barData);
        }

        return sequence;
    }

    /// <summary>
    /// sweep-0614: applies the active <c>swing N { }</c> context to a compiled bar by
    /// delaying every offbeat eighth-note onset, mirroring the eighth-note swing grid
    /// used by the <c>quantize</c> builtin (TransformFunctions.QuantizeBar — CONTEXT
    /// D-04/D-06). Before this fix <see cref="MusicalContext.Swing"/> was written/cloned
    /// but never read by any render path, so a <c>swing 0.62 { }</c> block produced
    /// straight eighths. The shift is ADDED to each note's existing
    /// <see cref="MusicalNoteData.OnsetOffset"/> (so it composes with tuplets); a later
    /// explicit <c>quantize</c> transform still overwrites it. Recurses into Phase 28
    /// parallel voices so voiced bars swing too.
    /// </summary>
    private static void ApplyContextSwing(BarData bar, double transformSwing)
    {
        var ts = bar.TimeSignature ?? new TimeSignatureData(4, 4);
        // sweep-0614: currentBeat now accumulates GetBeats in QUARTER-note units,
        // so the eighth-note subdivision must also be quarter-units (an eighth is
        // 4/8 = 0.5 quarters in every meter). Previously this was denominator-units
        // (ts.Denominator / 8.0); in 4/4 both forms equal 0.5, so 4/4 swing stays
        // byte-identical while non-4/4 swing now snaps to the correct eighth grid.
        double subdivBeats = 4.0 / 8.0;
        double swingOffset = transformSwing * (subdivBeats / 2.0);

        var notes = bar.MusicalNotes;
        double currentBeat = 0.0;
        // Whether the most recent LEAD note landed on an offbeat eighth — its stacked
        // chord tones inherit the same shift so the chord stays together.
        bool leadOffbeat = false;
        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            // Snap the running onset to the nearest eighth-note slot to decide
            // on/offbeat, then delay the offbeats. Chord tones (isChordTone) ride
            // the leading note's onset, so only the lead note advances the cursor.
            if (!note.IsChordTone)
            {
                int slot = (int)Math.Round(currentBeat / subdivBeats);
                leadOffbeat = (slot % 2 == 1);
                if (leadOffbeat)
                    notes[i] = note.With(onsetOffset: note.OnsetOffset + swingOffset);
                currentBeat += note.GetBeats(ts.Denominator);
            }
            else if (leadOffbeat)
            {
                notes[i] = note.With(onsetOffset: note.OnsetOffset + swingOffset);
            }
        }

        if (bar.ParallelVoices != null)
            foreach (var voice in bar.ParallelVoices)
                ApplyContextSwing(voice, transformSwing);
    }

    /// <summary>
    /// Compiles a single bar of note stream elements into a BarData.
    /// </summary>
    private BarData CompileBar(NoteStreamBar bar, TimeSignatureData timeSig, MusicalContext context, ExecutionContext? executionContext)
    {
        var musicalNotes = new List<MusicalNoteData>();
        // Phase 28 (SPEC-1): accumulator for `{voice ...}` blocks encountered in this bar.
        // Local-only — no compiler instance state, so re-entrant calls (nested compiles, tests)
        // remain isolated.
        List<BarData>? parallelVoices = null;

        if (bar.Elements.Count == 0)
        {
            // Empty bar: create a whole-bar rest
            var restNote = new MusicalNoteData(' ', 0, 0, (int)NoteValueType.Value.WHOLE, isRest: true);
            musicalNotes.Add(restNote);
            return new BarData(musicalNotes, timeSig);
        }

        // Determine auto-fit duration for elements without explicit durations
        var autoFitDuration = CalculateAutoFitDuration(bar.Elements, timeSig);

        foreach (var element in bar.Elements)
        {
            switch (element)
            {
                case NoteElement note:
                    musicalNotes.Add(CompileNoteElement(note, autoFitDuration, context));
                    break;

                case RestElement rest:
                    musicalNotes.Add(CompileRestElement(rest, autoFitDuration));
                    break;

                case ChordElement chord:
                    // Expand chord to individual notes (all with same duration, played simultaneously)
                    foreach (var chordNote in CompileChordElement(chord, autoFitDuration))
                    {
                        musicalNotes.Add(chordNote);
                    }
                    break;

                case NamedChordElement namedChord:
                    foreach (var chordNote in CompileNamedChordElement(namedChord, autoFitDuration))
                    {
                        musicalNotes.Add(chordNote);
                    }
                    break;

                case RomanNumeralElement romanNumeral:
                    foreach (var chordNote in CompileRomanNumeralElement(romanNumeral, autoFitDuration, context))
                    {
                        musicalNotes.Add(chordNote);
                    }
                    break;

                case RandomChoiceElement choice:
                    musicalNotes.Add(CompileRandomChoiceElement(choice, autoFitDuration, executionContext));
                    break;

                case VariableReferenceElement varRef:
                    musicalNotes.Add(CompileVariableReferenceElement(varRef, autoFitDuration, executionContext));
                    break;

                case GhostNoteElement ghost:
                {
                    var (name, octave, alteration) = NoteType.Parse(ghost.NoteName);
                    // Ghost notes default to sixteenth duration (short, ornamental)
                    int? dv = ghost.DurationSuffix != null
                        ? ResolveDuration(ghost.DurationSuffix, autoFitDuration)
                        : (int)NoteValueType.Value.SIXTEENTH;
                    musicalNotes.Add(new MusicalNoteData(name, octave, alteration, dv,
                        isRest: false, velocity: 0.15, sourceLocation: ghost.Location, sourceLength: CalcSourceLength(ghost)));
                    break;
                }

                case GraceNoteElement grace:
                {
                    var (name, octave, alteration) = NoteType.Parse(grace.NoteName);
                    musicalNotes.Add(new MusicalNoteData(name, octave, alteration,
                        (int)NoteValueType.Value.THIRTYSECOND, isRest: false, velocity: 0.5,
                        sourceLocation: grace.Location, sourceLength: CalcSourceLength(grace)));
                    break;
                }

                case TupletElement tuplet:
                    CompileTupletElement(tuplet, context, executionContext, musicalNotes, new Fraction(1, 1));
                    break;

                case VoiceBlockElement voiceBlock:
                {
                    // Phase 28 (SPEC-1): each `{voice ...}` block compiles to a separate
                    // BarData hung off the parent bar's ParallelVoices. The voice's notes
                    // share the parent bar's onset (0); BarRenderer / SongRenderer mix the
                    // resulting buffers additively.
                    var voiceBar = CompileVoiceBlock(voiceBlock, timeSig, context, executionContext);
                    parallelVoices ??= new List<BarData>();
                    parallelVoices.Add(voiceBar);
                    break;
                }
            }
        }

        // Post-process: if notes have varying velocities, smooth-interpolate between them
        // This handles the common case of: | pp C4 cresc D4 E4 ff F4 |
        // where D4 and E4 should get interpolated velocities
        InterpolateVelocities(musicalNotes);

        // TUP-05: bar-fit validator runs only when at least one note has non-null
        // DurationFraction (i.e. bar contains a tuplet or per-note /N or /X:Y).
        // Existing CalculateAutoFitDuration path runs on the input element list above;
        // this validator runs on the OUTPUT MusicalNoteData list and is purely additive —
        // preserves Phase 18 byte-identical contract for non-tuplet bars (Pitfall 2 mitigation).
        if (musicalNotes.Any(n => n.DurationFraction.HasValue))
        {
            ValidateBarFit(musicalNotes, timeSig, bar.Location);
        }

        // Phase 28 (SPEC-1): if a bar had no other elements but only voice blocks, its
        // musicalNotes list is empty. Insert a whole-bar rest so the lead-bar timeline
        // still spans the bar's full duration; ParallelVoices carry the actual audible
        // content. This mirrors the empty-bar path above (BarData expects ≥1 note).
        if (musicalNotes.Count == 0 && parallelVoices != null && parallelVoices.Count > 0)
        {
            musicalNotes.Add(new MusicalNoteData(' ', 0, 0, (int)NoteValueType.Value.WHOLE, isRest: true));
        }

        var result = new BarData(musicalNotes, timeSig);
        if (parallelVoices != null)
        {
            result.ParallelVoices = parallelVoices;
        }
        return result;
    }

    /// <summary>
    /// Phase 28 (SPEC-1): compiles a VoiceBlockElement into its own BarData.
    /// Mirrors the element loop in CompileBar but drives off pre-parsed children
    /// rather than NoteStreamBar.Elements. Auto-fit duration is calculated from the
    /// voice block's own children so each voice fills its own time independently.
    /// Voice blocks may not contain other voice blocks (rejected at parse time).
    /// </summary>
    private BarData CompileVoiceBlock(VoiceBlockElement voiceBlock, TimeSignatureData timeSig, MusicalContext context, ExecutionContext? executionContext)
    {
        var musicalNotes = new List<MusicalNoteData>();

        if (voiceBlock.Children.Count == 0)
        {
            var restNote = new MusicalNoteData(' ', 0, 0, (int)NoteValueType.Value.WHOLE, isRest: true);
            musicalNotes.Add(restNote);
            return new BarData(musicalNotes, timeSig);
        }

        var autoFitDuration = CalculateAutoFitDuration(voiceBlock.Children, timeSig);

        foreach (var element in voiceBlock.Children)
        {
            switch (element)
            {
                case NoteElement note:
                    musicalNotes.Add(CompileNoteElement(note, autoFitDuration, context));
                    break;

                case RestElement rest:
                    musicalNotes.Add(CompileRestElement(rest, autoFitDuration));
                    break;

                case ChordElement chord:
                    foreach (var chordNote in CompileChordElement(chord, autoFitDuration))
                    {
                        musicalNotes.Add(chordNote);
                    }
                    break;

                case NamedChordElement namedChord:
                    foreach (var chordNote in CompileNamedChordElement(namedChord, autoFitDuration))
                    {
                        musicalNotes.Add(chordNote);
                    }
                    break;

                case RomanNumeralElement romanNumeral:
                    foreach (var chordNote in CompileRomanNumeralElement(romanNumeral, autoFitDuration, context))
                    {
                        musicalNotes.Add(chordNote);
                    }
                    break;

                case RandomChoiceElement choice:
                    musicalNotes.Add(CompileRandomChoiceElement(choice, autoFitDuration, executionContext));
                    break;

                case VariableReferenceElement varRef:
                    musicalNotes.Add(CompileVariableReferenceElement(varRef, autoFitDuration, executionContext));
                    break;

                case TupletElement tuplet:
                    CompileTupletElement(tuplet, context, executionContext, musicalNotes, new Fraction(1, 1));
                    break;

                // VoiceBlockElement intentionally not handled — ParseVoiceBlockChildren rejects nesting.
            }
        }

        InterpolateVelocities(musicalNotes);

        if (musicalNotes.Any(n => n.DurationFraction.HasValue))
        {
            ValidateBarFit(musicalNotes, timeSig, voiceBlock.Location);
        }

        return new BarData(musicalNotes, timeSig);
    }

    /// <summary>
    /// TUP-05 bar-fit validator. Walks emitted MusicalNoteData accumulating a
    /// Fraction running sum (in quarter-units). On overflow, truncates the
    /// boundary-crossing element to fit the remaining slot AND drops all
    /// subsequent emitted notes from the bar. Emits one Info-severity
    /// diagnostic per overflowing bar via ErrorReporter (CONTEXT D-17).
    ///
    /// Per CONTEXT D-03 (locked algorithm + charitable-interpretation memory):
    /// "music > rigid correctness" — overflow is silent-truncate-with-Info,
    /// NOT a hard error. Preserves byte-identical determinism (same input
    /// always yields same truncation).
    ///
    /// Refinement of D-03's literal "truncate-to-zero" edge case: when
    /// remaining capacity is exactly zero (boundary lands on the last
    /// fitting note), DROP the overflowing element instead of emitting a
    /// zero-duration note. The Info diagnostic still fires — composer
    /// gets the same feedback, output stays clean.
    /// </summary>
    private void ValidateBarFit(
        List<MusicalNoteData> musicalNotes,
        TimeSignatureData timeSig,
        FlowLang.Core.SourceLocation? barLocation)
    {
        // Bar capacity in quarter-units: numerator × 4 / denominator.
        // 4/4 → 4/1 = 4 quarters; 6/8 → 24/8 = 3 quarters; 3/4 → 12/4 = 3 quarters.
        Fraction barCapacity = new Fraction(timeSig.Numerator * 4, timeSig.Denominator);

        Fraction sum = new Fraction(0, 1);
        int? truncateAt = null;
        Fraction overflowSum = sum;  // for diagnostic message

        for (int i = 0; i < musicalNotes.Count; i++)
        {
            // Chord-tones share the leading tone's slot; they must not contribute
            // to the running sum (otherwise a tuplet bar containing a chord would
            // false-overflow and be truncated). See MusicalNoteData.IsChordTone.
            if (musicalNotes[i].IsChordTone)
                continue;

            Fraction noteFrac = NoteAsBarFraction(musicalNotes[i], timeSig);
            Fraction nextSum = sum + noteFrac;
            // Strictly greater check (== bar capacity is exact-fit; preserve all elements).
            if (nextSum > barCapacity)
            {
                truncateAt = i;
                overflowSum = nextSum;
                break;
            }
            sum = nextSum;
        }

        if (truncateAt is null)
            return;  // exact fit OR underflow — no diagnostic

        int boundaryIdx = truncateAt.Value;

        // Compute remaining capacity = barCapacity - sum.
        // Phase 18 Fraction has no operator-, but +(...) handles negation via sign-on-numerator.
        Fraction remaining = barCapacity + new Fraction(-sum.Num, sum.Denom);

        // Refinement: zero remaining → drop the boundary element + everything after.
        // Non-zero remaining → truncate boundary element to `remaining` + drop everything after.
        if (remaining.Num == 0)
        {
            // Drop boundary element + tail
            musicalNotes.RemoveRange(boundaryIdx, musicalNotes.Count - boundaryIdx);
        }
        else
        {
            // Truncate boundary element: replace with same-fields copy + new DurationFraction
            var b = musicalNotes[boundaryIdx];
            musicalNotes[boundaryIdx] = new MusicalNoteData(
                b.NoteName, b.Octave, b.Alteration, b.DurationValue, b.IsRest,
                b.CentOffset, b.IsTied, b.Velocity, b.Articulation, b.IsDotted,
                b.SourceLocation, b.SourceLength,
                durationFraction: remaining);

            // Drop everything after the boundary
            int dropCount = musicalNotes.Count - (boundaryIdx + 1);
            if (dropCount > 0)
                musicalNotes.RemoveRange(boundaryIdx + 1, dropCount);
        }

        // Emit Info diagnostic naming the overflow ratio
        _errorReporter?.ReportInfo(
            $"Bar overflow: sum {overflowSum} exceeds time-signature {barCapacity}; truncated to fit",
            barLocation);
    }

    /// <summary>
    /// Helper for ValidateBarFit. Computes a transient Fraction representation of a note's
    /// duration in quarter-units. Uses DurationFraction directly when set; otherwise converts
    /// the enum DurationValue + IsDotted multiplier to a Fraction WITHOUT mutating the note.
    /// Phase 18 byte-identical contract preserved — this helper is read-only.
    /// </summary>
    private static Fraction NoteAsBarFraction(MusicalNoteData note, TimeSignatureData timeSig)
    {
        if (note.DurationFraction.HasValue)
            return note.DurationFraction.Value;

        if (!note.DurationValue.HasValue)
            return new Fraction(0, 1);

        var enumVal = (NoteValueType.Value)note.DurationValue.Value;
        Fraction baseFrac = enumVal switch
        {
            NoteValueType.Value.WHOLE        => new Fraction(4, 1),
            NoteValueType.Value.HALF         => new Fraction(2, 1),
            NoteValueType.Value.QUARTER      => new Fraction(1, 1),
            NoteValueType.Value.EIGHTH       => new Fraction(1, 2),
            NoteValueType.Value.SIXTEENTH    => new Fraction(1, 4),
            NoteValueType.Value.THIRTYSECOND => new Fraction(1, 8),
            _ => new Fraction(1, 1),
        };
        return note.IsDotted ? baseFrac * new Fraction(3, 2) : baseFrac;
    }

    /// <summary>
    /// Interpolates velocities for notes that don't have explicit velocity markings.
    /// Finds notes with explicit (different) velocities and linearly interpolates between them.
    /// </summary>
    private static void InterpolateVelocities(List<MusicalNoteData> notes)
    {
        if (notes.Count < 3) return;

        // Find unique velocities among non-rest notes
        var nonRestVelocities = notes.Where(n => !n.IsRest).Select(n => n.Velocity).Distinct().ToList();
        if (nonRestVelocities.Count < 2) return; // All same velocity, nothing to interpolate

        // Simple linear interpolation from first non-rest to last non-rest
        int firstIdx = -1, lastIdx = -1;
        for (int i = 0; i < notes.Count; i++)
        {
            if (!notes[i].IsRest)
            {
                if (firstIdx == -1) firstIdx = i;
                lastIdx = i;
            }
        }

        if (firstIdx == lastIdx) return;

        double startVel = notes[firstIdx].Velocity;
        double endVel = notes[lastIdx].Velocity;
        if (Math.Abs(startVel - endVel) < VelocityInterpolationTolerance) return;

        // Count non-rest notes for interpolation
        int nonRestCount = 0;
        for (int i = firstIdx; i <= lastIdx; i++)
            if (!notes[i].IsRest) nonRestCount++;

        if (nonRestCount < MinInterpolationNoteCount) return;

        int noteIdx = 0;
        for (int i = firstIdx; i <= lastIdx; i++)
        {
            if (notes[i].IsRest) continue;

            // First and last keep their explicit velocities
            if (noteIdx > 0 && noteIdx < nonRestCount - 1)
            {
                double t = (double)noteIdx / (nonRestCount - 1);
                double vel = Math.Clamp(startVel + t * (endVel - startVel), 0.0, 1.0);
                // Rebuild via With(...) so the velocity override is the ONLY change.
                // The 12-arg ctor used to be called here, which silently reset the 5
                // trailing fields — IsChordTone / DurationFraction / OnsetOffset /
                // DurationOverlap / PortamentoMs — to their defaults. Dropping
                // IsChordTone in particular made interpolated chord tones advance the
                // bar cursor in BarType.ToTimeline, doubling any bar that mixed a
                // dynamic (→ velocity variation → interpolation runs) with chords.
                // Same drop-on-reconstruct pattern the 2026-06-09 audit fixed for
                // transforms (NoteType.cs With(...) docs); this path was missed.
                notes[i] = notes[i].With(velocity: vel);
            }
            noteIdx++;
        }
    }

    /// <summary>
    /// Calculates the auto-fit NoteValue for elements without explicit durations.
    /// Divides the bar's total beats evenly among auto-fit elements.
    /// </summary>
    private NoteValueType.Value? CalculateAutoFitDuration(
        IReadOnlyList<NoteStreamElement> elements, TimeSignatureData timeSig)
    {
        // Count elements with and without explicit durations
        int autoFitCount = 0;
        double explicitBeats = 0;

        foreach (var elem in elements)
        {
            // Ghost notes (without explicit duration) and grace notes use fixed short durations
            // and should not participate in auto-fit calculation. They're ornamental.
            if (elem is GraceNoteElement)
            {
                // Grace notes always use 32nd — count as explicit
                explicitBeats += NoteValueType.ToFraction(NoteValueType.Value.THIRTYSECOND) * timeSig.Denominator;
                continue;
            }

            if (elem is GhostNoteElement ghostElem && ghostElem.DurationSuffix == null)
            {
                // Ghost notes without explicit duration use sixteenth — count as explicit
                explicitBeats += NoteValueType.ToFraction(NoteValueType.Value.SIXTEENTH) * timeSig.Denominator;
                continue;
            }

            string? durSuffix = elem switch
            {
                NoteElement n => n.DurationSuffix,
                RestElement r => r.DurationSuffix,
                ChordElement c => c.DurationSuffix,
                NamedChordElement nc => nc.DurationSuffix,
                RomanNumeralElement rn => rn.DurationSuffix,
                RandomChoiceElement rc => rc.DurationSuffix,
                VariableReferenceElement vr => vr.DurationSuffix,
                GhostNoteElement g => g.DurationSuffix,
                _ => null
            };

            bool isDotted = elem switch
            {
                NoteElement n => n.IsDotted,
                RestElement r => r.IsDotted,
                ChordElement c => c.IsDotted,
                NamedChordElement nc => nc.IsDotted,
                RomanNumeralElement rn => rn.IsDotted,
                RandomChoiceElement rc => rc.IsDotted,
                VariableReferenceElement vr => vr.IsDotted,
                GhostNoteElement g => g.IsDotted,
                _ => false
            };

            if (durSuffix != null && DurationSuffixMap.TryGetValue(durSuffix, out var noteVal))
            {
                double fraction = NoteValueType.ToFraction(noteVal);
                if (isDotted) fraction *= 1.5;
                explicitBeats += fraction * timeSig.Denominator;
            }
            else
            {
                autoFitCount++;
            }
        }

        if (autoFitCount == 0)
            return null; // All elements have explicit durations

        // Calculate remaining beats for auto-fit elements
        double totalBeats = timeSig.Numerator;
        double remainingBeats = totalBeats - explicitBeats;
        if (remainingBeats <= 0)
            remainingBeats = totalBeats; // If overflow, use full bar

        double beatsPerNote = remainingBeats / autoFitCount;

        // Map to closest NoteValue
        return FindClosestNoteValue(beatsPerNote, timeSig.Denominator);
    }

    /// <summary>
    /// TUP-01/02/03 — recursively compile a TupletElement into MusicalNoteData with
    /// rational DurationFraction values. Multiplies outerScale through nested ratios
    /// using Phase 18 Fraction arithmetic (no double drift — Pitfall 1).
    ///
    /// DurationFraction is in quarter-note units (music21 convention; matches Phase 18
    /// 18-02 SUMMARY pin).
    ///
    /// Math (verified against SPEC TUP-03 acceptance):
    ///   bracketSpan    = suffixFrac × outerScale          // total quarter-units this bracket spans
    ///   perChildSlot   = bracketSpan × (1 / Numerator)    // per-leaf-child duration
    ///   nestedOuterScale = perChildSlot                   // ONE outer slot's quarter-size,
    ///                                                     //   passed to nested tuplet as its outerScale
    ///
    /// Worked example for | {3:2 C4 {3:2 D4 E4 F4}q G4}h |:
    ///   outer call: outerScale=1/1, suffix=h (2/1 quarter), bracketSpan=2/1, perChildSlot=2/3
    ///     C4 → 2/3 quarter (= 1/6 whole)  ✓
    ///     G4 → 2/3 quarter (= 1/6 whole)  ✓
    ///   nested call (middle outer slot): outerScale=2/3, suffix=q (1/1), bracketSpan=2/3,
    ///                                    perChildSlot=2/9
    ///     D4/E4/F4 → 2/9 quarter each (= 1/18 whole)  ✓
    ///   sum = 2/3 + 2/9 + 2/9 + 2/9 + 2/3 = 6/9 + 6/9 = 12/9... wait: 2·(2/3) + 3·(2/9)
    ///       = 4/3 + 6/9 = 12/9 + 6/9 = 18/9 = 2 quarters = one half ✓ matches outer h suffix.
    /// </summary>
    private void CompileTupletElement(
        TupletElement tuplet,
        MusicalContext context,
        ExecutionContext? executionContext,
        List<MusicalNoteData> output,
        Fraction outerScale)
    {
        // Outer suffix → fraction-of-quarter-note for this entire bracket.
        Fraction suffixFrac = SuffixToQuarterFraction(tuplet.DurationSuffix, tuplet.IsDotted);

        // Total span this bracket covers (in quarter-units), AFTER any outer ratios.
        Fraction bracketSpan = suffixFrac * outerScale;

        // Per-leaf-child slot: bracketSpan / Numerator.
        Fraction perChildSlot = bracketSpan * new Fraction(1, tuplet.Numerator);

        // Outer-scale passed DOWN to a nested TupletElement child = the quarter-size of
        // ONE OUTER SLOT (= perChildSlot). The nested SuffixToQuarterFraction is then sized
        // correctly relative to ONE slot of THIS bracket.
        Fraction nestedOuterScale = perChildSlot;

        foreach (var child in tuplet.Children)
        {
            switch (child)
            {
                case TupletElement nestedTuplet:
                    CompileTupletElement(nestedTuplet, context, executionContext, output, nestedOuterScale);
                    break;

                case NoteElement note:
                {
                    var (name, octave, alteration) = NoteType.Parse(note.NoteName);
                    output.Add(new MusicalNoteData(
                        name, octave, alteration,
                        durationValue: (int)NoteValueType.Value.QUARTER,  // best-effort enum mirror; rational override applies
                        isRest: false,
                        centOffset: note.CentOffset,
                        isTied: note.IsTied,
                        velocity: note.Velocity ?? 0.63,
                        articulation: note.ArticulationMark ?? Articulation.Normal,
                        isDotted: note.IsDotted,
                        sourceLocation: note.Location,
                        sourceLength: 0,
                        durationFraction: perChildSlot));
                    break;
                }

                case RestElement rest:
                {
                    output.Add(new MusicalNoteData(
                        ' ', 0, 0,
                        durationValue: (int)NoteValueType.Value.QUARTER,
                        isRest: true,
                        durationFraction: perChildSlot));
                    break;
                }

                case NamedChordElement namedChord:
                {
                    // Each chord-tone gets the same per-slot duration (chord plays simultaneously).
                    // The IsChordTone flag set by CompileNamedChordElement must propagate through
                    // this re-wrap so BarType.ToTimeline() stacks tones on the lead onset.
                    foreach (var chordNote in CompileNamedChordElement(namedChord, NoteValueType.Value.QUARTER))
                    {
                        output.Add(new MusicalNoteData(
                            chordNote.NoteName, chordNote.Octave, chordNote.Alteration,
                            chordNote.DurationValue, chordNote.IsRest,
                            chordNote.CentOffset, chordNote.IsTied, chordNote.Velocity,
                            chordNote.Articulation, chordNote.IsDotted,
                            chordNote.SourceLocation, chordNote.SourceLength,
                            durationFraction: perChildSlot,
                            isChordTone: chordNote.IsChordTone));
                    }
                    break;
                }

                default:
                    // Fallback: treat any other element as a rest with the per-slot duration.
                    // ParseTupletChildren currently restricts children to TupletElement / NoteElement /
                    // RestElement / NamedChordElement; this default branch defensively handles future
                    // expansion without dropping content silently.
                    output.Add(new MusicalNoteData(
                        ' ', 0, 0,
                        durationValue: (int)NoteValueType.Value.QUARTER,
                        isRest: true,
                        durationFraction: perChildSlot));
                    break;
            }
        }
    }

    /// <summary>
    /// Maps a duration suffix to its Fraction value in quarter-note units.
    /// w=4q, h=2q, q=1q, e=1/2q, s=1/4q, t=1/8q. Dotted multiplier × 3/2.
    /// Mirrors DurationSuffixMap layout (line 29) — same vocabulary.
    /// </summary>
    private static Fraction SuffixToQuarterFraction(string suffix, bool isDotted)
    {
        Fraction f = suffix switch
        {
            "w" => new Fraction(4, 1),
            "h" => new Fraction(2, 1),
            "q" => new Fraction(1, 1),
            "e" => new Fraction(1, 2),
            "s" => new Fraction(1, 4),
            "t" => new Fraction(1, 8),
            _ => new Fraction(1, 1),  // fallback: quarter (matches recovery path in parser)
        };
        return isDotted ? f * new Fraction(3, 2) : f;
    }

    /// <summary>
    /// Helper overload: dispatch a NamedChordElement using a NoteValueType.Value? (the auto-fit shape).
    /// Bridges the existing CompileNamedChordElement signature that takes NoteValueType.Value? (optional).
    /// </summary>
    private List<MusicalNoteData> CompileNamedChordElement(NamedChordElement namedChord, NoteValueType.Value defaultValue)
        => CompileNamedChordElement(namedChord, (NoteValueType.Value?)defaultValue);

    /// <summary>
    /// Finds the closest NoteValue enum for a given number of beats.
    /// </summary>
    private NoteValueType.Value FindClosestNoteValue(double beats, int timeSigDenominator)
    {
        // Convert beats to fraction of a whole note
        double fraction = beats / timeSigDenominator;

        // Find the closest NoteValue
        var values = new[]
        {
            (NoteValueType.Value.WHOLE, 1.0),
            (NoteValueType.Value.HALF, 0.5),
            (NoteValueType.Value.QUARTER, 0.25),
            (NoteValueType.Value.EIGHTH, 0.125),
            (NoteValueType.Value.SIXTEENTH, 0.0625),
            (NoteValueType.Value.THIRTYSECOND, 0.03125)
        };

        NoteValueType.Value closest = NoteValueType.Value.QUARTER;
        double closestDiff = double.MaxValue;

        foreach (var (noteVal, noteFraction) in values)
        {
            double diff = Math.Abs(noteFraction - fraction);
            if (diff < closestDiff)
            {
                closestDiff = diff;
                closest = noteVal;
            }
        }

        return closest;
    }

    /// <summary>
    /// Compiles a NoteElement into a MusicalNoteData.
    /// TUP-04 / TUP-08: when note.TupletRatio.HasValue, computes a rational DurationFraction
    /// override in quarter-note units (music21 convention).
    ///   y == 1 sentinel  → TUP-04 (C4/N): DurationFraction = Fraction(4, X) quarter (= 1/N whole)
    ///   y != 1           → TUP-08 (C4/X:Y[suffix]): DurationFraction =
    ///                       SuffixToQuarterFraction(suffix or "q") × Fraction(1, X) quarter
    /// </summary>
    private MusicalNoteData CompileNoteElement(NoteElement note, NoteValueType.Value? autoFitDuration, MusicalContext context)
    {
        var (noteName, octave, alteration) = NoteType.Parse(note.NoteName);
        int? durationValue;

        if (note.DurationSuffix != null && DurationSuffixMap.TryGetValue(note.DurationSuffix, out var noteVal))
        {
            durationValue = (int)noteVal;
        }
        else if (autoFitDuration != null)
        {
            durationValue = (int)autoFitDuration.Value;
        }
        else
        {
            durationValue = (int)NoteValueType.Value.QUARTER; // Default to quarter note
        }

        // === TUP-04 / TUP-08 per-note rational duration ===
        Fraction? durationFraction = null;
        if (note.TupletRatio.HasValue)
        {
            var (x, y) = note.TupletRatio.Value;
            if (y == 1)
            {
                // TUP-04: C4/N — DurationFraction = 1/N whole = 4/N quarter
                durationFraction = new Fraction(4, x);
            }
            else
            {
                // TUP-08: C4/X:Y[suffix] — DurationFraction = SuffixToQuarterFraction(suffix) × 1/X
                // suffix defaults to "q" when absent (per SPEC TUP-08 "default level: quarter").
                string suffixForFraction = note.DurationSuffix ?? "q";
                Fraction suffixFrac = SuffixToQuarterFraction(suffixForFraction, note.IsDotted);
                durationFraction = suffixFrac * new Fraction(1, x);
            }
        }

        // Determine velocity: note-level override > context velocity > default mf
        double velocity = note.Velocity ?? context.Velocity ?? 0.63;

        // Phase 28 locked velocity adjustments (SPEC-4):
        //   Accent +0.30, Marcato +0.30, Sforzando handled by envelope shaper (Plan 28-03 —
        //   no scalar boost here), Legato/Tenuto/Staccato/Normal velocity unchanged.
        // Behavioral change: prior code set `velocity = 0.95` for Sforzando, which clobbered
        // the composer's intended velocity. SPEC-4 instead routes Sforzando through a
        // time-varying envelope spike at the synth layer (GenerateArticulationADSR in Plan
        // 28-03), so the composer's base velocity passes through here unchanged. The Accent
        // +0.30 also replaces the previous +0.20 to match the locked SPEC-4 constants.
        var articulation = note.ArticulationMark ?? Articulation.Normal;
        switch (articulation)
        {
            case Articulation.Accent:
            case Articulation.Marcato:
                velocity = Math.Min(velocity + 0.30, 1.0);
                break;
            // Sforzando: NO scalar velocity bump — envelope shaper applies time-varying spike at synth layer.
            // Legato, Tenuto, Staccato, Normal: velocity unchanged.
        }

        return new MusicalNoteData(noteName, octave, alteration, durationValue, isRest: false,
            centOffset: note.CentOffset, isTied: note.IsTied,
            velocity: velocity, articulation: articulation,
            isDotted: note.IsDotted, sourceLocation: note.Location, sourceLength: CalcSourceLength(note),
            durationFraction: durationFraction);
    }

    /// <summary>
    /// Compiles a RestElement into a MusicalNoteData with IsRest=true.
    /// </summary>
    private MusicalNoteData CompileRestElement(RestElement rest, NoteValueType.Value? autoFitDuration)
    {
        int? durationValue;

        if (rest.DurationSuffix != null && DurationSuffixMap.TryGetValue(rest.DurationSuffix, out var noteVal))
        {
            durationValue = (int)noteVal;
        }
        else if (autoFitDuration != null)
        {
            durationValue = (int)autoFitDuration.Value;
        }
        else
        {
            durationValue = (int)NoteValueType.Value.QUARTER;
        }

        return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, isDotted: rest.IsDotted, sourceLocation: rest.Location, sourceLength: CalcSourceLength(rest));
    }

    /// <summary>
    /// Compiles a ChordElement into multiple MusicalNoteData (one per note in the chord).
    /// </summary>
    private List<MusicalNoteData> CompileChordElement(ChordElement chord, NoteValueType.Value? autoFitDuration)
    {
        var notes = new List<MusicalNoteData>();
        int? durationValue;

        if (chord.DurationSuffix != null && DurationSuffixMap.TryGetValue(chord.DurationSuffix, out var noteVal))
        {
            durationValue = (int)noteVal;
        }
        else if (autoFitDuration != null)
        {
            durationValue = (int)autoFitDuration.Value;
        }
        else
        {
            durationValue = (int)NoteValueType.Value.QUARTER;
        }

        int chordLen = CalcSourceLength(chord);
        bool first = true;
        foreach (var noteName in chord.Notes)
        {
            var (name, octave, alteration) = NoteType.Parse(noteName);
            // First chord-tone is the "lead" — it advances the bar's beat cursor.
            // Remaining tones share its onset (IsChordTone=true) so the chord
            // plays as one polyphonic strike, not as an arpeggio across bar
            // beats. See MusicalNoteData.IsChordTone and BarType.ToTimeline.
            notes.Add(new MusicalNoteData(name, octave, alteration, durationValue, isRest: false, isTied: chord.IsTied, isDotted: chord.IsDotted, sourceLocation: chord.Location, sourceLength: chordLen, isChordTone: !first));
            first = false;
        }

        return notes;
    }

    /// <summary>
    /// Compiles a NamedChordElement (e.g., Cmaj7) into multiple MusicalNoteData.
    /// </summary>
    private List<MusicalNoteData> CompileNamedChordElement(NamedChordElement namedChord, NoteValueType.Value? autoFitDuration)
    {
        var notes = new List<MusicalNoteData>();
        int? durationValue = ResolveDuration(namedChord.DurationSuffix, autoFitDuration);

        if (!ChordParser.TryParse(namedChord.ChordSymbol, out var chordData) || chordData == null)
        {
            // Invalid chord — insert a rest as fallback
            notes.Add(new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, isDotted: namedChord.IsDotted, sourceLocation: namedChord.Location, sourceLength: CalcSourceLength(namedChord)));
            return notes;
        }

        int ncLen = CalcSourceLength(namedChord);
        bool firstNamed = true;
        foreach (var noteName in chordData.NoteNames)
        {
            var (name, octave, alteration) = NoteType.Parse(noteName);
            // See CompileChordElement above: first tone leads, rest stack on its onset.
            notes.Add(new MusicalNoteData(name, octave, alteration, durationValue, isRest: false, isTied: namedChord.IsTied, isDotted: namedChord.IsDotted, sourceLocation: namedChord.Location, sourceLength: ncLen, isChordTone: !firstNamed));
            firstNamed = false;
        }

        return notes;
    }

    /// <summary>
    /// Compiles a RomanNumeralElement into multiple MusicalNoteData using the active key context.
    /// </summary>
    private List<MusicalNoteData> CompileRomanNumeralElement(
        RomanNumeralElement romanNumeral, NoteValueType.Value? autoFitDuration, MusicalContext context)
    {
        var notes = new List<MusicalNoteData>();
        int? durationValue = ResolveDuration(romanNumeral.DurationSuffix, autoFitDuration);

        int rnLen = CalcSourceLength(romanNumeral);
        if (context.Key == null)
        {
            // No key context — insert a rest as fallback
            notes.Add(new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, isDotted: romanNumeral.IsDotted, sourceLocation: romanNumeral.Location, sourceLength: rnLen));
            return notes;
        }

        var chordData = ScaleDatabase.ResolveRomanNumeral(romanNumeral.Numeral, context.Key);
        if (chordData == null)
        {
            // sweep-0614: surface the silent-rest fallback so a composer whose
            // numeral fails to resolve in the active key sees WHY notes vanished
            // instead of getting a mysteriously empty bar. One-shot per (numeral, key).
            FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
                $"harmony-rn-unresolved:{romanNumeral.Numeral}:{context.Key}",
                $"[harmony] roman numeral '{romanNumeral.Numeral}' unresolved in key '{context.Key}' — rendered as rest");
            notes.Add(new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, isDotted: romanNumeral.IsDotted, sourceLocation: romanNumeral.Location, sourceLength: rnLen));
            return notes;
        }

        bool firstRoman = true;
        foreach (var noteName in chordData.NoteNames)
        {
            var (name, octave, alteration) = NoteType.Parse(noteName);
            // See CompileChordElement above: first tone leads, rest stack on its onset.
            notes.Add(new MusicalNoteData(name, octave, alteration, durationValue, isRest: false, isDotted: romanNumeral.IsDotted, sourceLocation: romanNumeral.Location, sourceLength: rnLen, isChordTone: !firstRoman));
            firstRoman = false;
        }

        return notes;
    }

    /// <summary>
    /// Resolves a duration suffix to a NoteValue, falling back to autoFitDuration or quarter note.
    /// </summary>
    private int? ResolveDuration(string? durationSuffix, NoteValueType.Value? autoFitDuration)
    {
        if (durationSuffix != null && DurationSuffixMap.TryGetValue(durationSuffix, out var noteVal))
            return (int)noteVal;
        if (autoFitDuration != null)
            return (int)autoFitDuration.Value;
        return (int)NoteValueType.Value.QUARTER;
    }

    private float GetRandomFloat(bool isSeeded, ExecutionContext? context)
    {
        if (context != null)
            return context.GetRand(isSeeded).NextSingle();
        return Random.Shared.NextSingle();
    }

    /// <summary>
    /// Compiles a RandomChoiceElement by selecting one note randomly from the choice set.
    /// </summary>
    private MusicalNoteData CompileRandomChoiceElement(RandomChoiceElement choice, NoteValueType.Value? autoFitDuration, ExecutionContext? executionContext)
    {
        int? durationValue = ResolveDuration(choice.DurationSuffix, autoFitDuration);

        // Select a note from the choices
        string selectedNote;
        bool hasWeights = choice.Choices.Any(c => c.Weight.HasValue);

        if (hasWeights)
        {
            // Weighted random selection
            int totalWeight = choice.Choices.Sum(c => c.Weight ?? 0);
            if (totalWeight <= 0)
            {
                Console.Error.WriteLine("Warning: random choice weights sum to 0, using uniform selection");
                hasWeights = false;
            }
            else
            {
                if (totalWeight != 100)
                {
                    Console.Error.WriteLine($"Warning: random choice weights sum to {totalWeight}, not 100. Normalizing.");
                }
                float rand = GetRandomFloat(choice.IsSeeded, executionContext) * totalWeight;
                float cumulative = 0;
                selectedNote = choice.Choices[^1].Note; // Default to last
                foreach (var (note, weight) in choice.Choices)
                {
                    cumulative += weight ?? 0;
                    if (rand < cumulative)
                    {
                        selectedNote = note;
                        break;
                    }
                }
                return CreateNoteFromChoice(selectedNote, durationValue, choice.IsDotted, choice.Location, CalcSourceLength(choice));
            }
        }

        // Uniform random selection
        int index = (int)(GetRandomFloat(choice.IsSeeded, executionContext) * choice.Choices.Count);
        index = Math.Clamp(index, 0, choice.Choices.Count - 1);
        selectedNote = choice.Choices[index].Note;
        return CreateNoteFromChoice(selectedNote, durationValue, choice.IsDotted, choice.Location, CalcSourceLength(choice));
    }

    /// <summary>
    /// Compiles a VariableReferenceElement by resolving the variable from the execution context.
    /// Supports Note (string) and MusicalNote (MusicalNoteData) variable types.
    /// Falls back to a rest on error (undefined variable, wrong type).
    /// </summary>
    private MusicalNoteData CompileVariableReferenceElement(
        VariableReferenceElement varRef, NoteValueType.Value? autoFitDuration, ExecutionContext? executionContext)
    {
        int? durationValue = ResolveDuration(varRef.DurationSuffix, autoFitDuration);

        int vrLen = CalcSourceLength(varRef);
        if (executionContext == null)
        {
            Console.Error.WriteLine($"Warning: cannot resolve variable '{varRef.VariableName}' in note stream (no execution context)");
            return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, sourceLocation: varRef.Location, sourceLength: vrLen);
        }

        Value value;
        try
        {
            value = executionContext.GetVariable(varRef.VariableName);
        }
        catch (InvalidOperationException)
        {
            Console.Error.WriteLine($"Warning: undefined variable '{varRef.VariableName}' in note stream, inserting rest");
            return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, sourceLocation: varRef.Location, sourceLength: vrLen);
        }

        // Handle Note type (string like "C4", "D#5")
        if (value.Type is NoteType && value.Data is string noteStr)
        {
            try
            {
                var (noteName, octave, alteration) = NoteType.Parse(noteStr);
                return new MusicalNoteData(noteName, octave, alteration, durationValue, isRest: false,
                    centOffset: varRef.CentOffset, isTied: varRef.IsTied, isDotted: varRef.IsDotted, sourceLocation: varRef.Location, sourceLength: vrLen);
            }
            catch
            {
                Console.Error.WriteLine($"Warning: variable '{varRef.VariableName}' has invalid note value '{noteStr}', inserting rest");
                return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, sourceLocation: varRef.Location, sourceLength: vrLen);
            }
        }

        // Handle MusicalNote type (MusicalNoteData)
        if (value.Data is MusicalNoteData musicalNote)
        {
            // Use stream-level duration/modifiers if provided, otherwise use the MusicalNote's own values
            int? finalDuration = varRef.DurationSuffix != null
                ? ResolveDuration(varRef.DurationSuffix, autoFitDuration)
                : musicalNote.DurationValue ?? durationValue;
            bool finalDotted = varRef.DurationSuffix != null ? varRef.IsDotted : musicalNote.IsDotted;
            bool finalTied = varRef.IsTied || musicalNote.IsTied;
            double? finalCentOffset = varRef.CentOffset ?? musicalNote.CentOffset;

            return new MusicalNoteData(musicalNote.NoteName, musicalNote.Octave, musicalNote.Alteration,
                finalDuration, isRest: musicalNote.IsRest,
                centOffset: finalCentOffset, isTied: finalTied, isDotted: finalDotted,
                velocity: musicalNote.Velocity, articulation: musicalNote.Articulation,
                sourceLocation: varRef.Location, sourceLength: vrLen);
        }

        Console.Error.WriteLine($"Warning: variable '{varRef.VariableName}' is type {value.Type.Name}, expected Note or MusicalNote, inserting rest");
        return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, sourceLocation: varRef.Location, sourceLength: vrLen);
    }

    private static MusicalNoteData CreateNoteFromChoice(string noteStr, int? durationValue, bool isDotted = false, Core.SourceLocation? sourceLocation = null, int sourceLength = 0)
    {
        if (noteStr == "_")
            return new MusicalNoteData(' ', 0, 0, durationValue, isRest: true, isDotted: isDotted, sourceLocation: sourceLocation, sourceLength: sourceLength);

        var (name, octave, alteration) = NoteType.Parse(noteStr);
        return new MusicalNoteData(name, octave, alteration, durationValue, isRest: false, isDotted: isDotted, sourceLocation: sourceLocation, sourceLength: sourceLength);
    }

    /// <summary>
    /// Calculates the source text length for a note stream element.
    /// </summary>
    private static int CalcSourceLength(NoteStreamElement element)
    {
        return element switch
        {
            NoteElement n => n.NoteName.Length
                + (n.DurationSuffix?.Length ?? 0)
                + (n.IsDotted ? 1 : 0)
                + (n.IsTied ? 1 : 0),
            RestElement r => 1 // "_"
                + (r.DurationSuffix?.Length ?? 0)
                + (r.IsDotted ? 1 : 0),
            ChordElement c => c.Notes.Sum(n => n.Length) + c.Notes.Count - 1 + 2 // [C4 E4 G4] brackets + spaces
                + (c.DurationSuffix?.Length ?? 0)
                + (c.IsDotted ? 1 : 0),
            NamedChordElement nc => nc.ChordSymbol.Length
                + (nc.DurationSuffix?.Length ?? 0)
                + (nc.IsDotted ? 1 : 0)
                + (nc.IsTied ? 1 : 0),
            RomanNumeralElement rn => rn.Numeral.Length
                + (rn.DurationSuffix?.Length ?? 0)
                + (rn.IsDotted ? 1 : 0),
            VariableReferenceElement vr => vr.VariableName.Length
                + (vr.DurationSuffix?.Length ?? 0)
                + (vr.IsDotted ? 1 : 0)
                + (vr.IsTied ? 1 : 0),
            GhostNoteElement g => "(ghost )".Length + g.NoteName.Length
                + (g.DurationSuffix?.Length ?? 0),
            GraceNoteElement gr => "(grace )".Length + gr.NoteName.Length,
            RandomChoiceElement rc => rc.Choices.Sum(c => c.Note.Length + (c.Weight.HasValue ? c.Weight.Value.ToString().Length + 1 : 0)) + rc.Choices.Count - 1 + 4, // (? ... )
            _ => 2
        };
    }
}
