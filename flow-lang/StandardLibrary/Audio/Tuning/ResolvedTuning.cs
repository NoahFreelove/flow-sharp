using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Phase 32 Plan 32-03 runtime tuning value (CONTEXT D-02): wraps the parser
/// outputs (<see cref="ParsedScala"/> + <see cref="ScalaKbm"/>) and EAGERLY
/// precomputes a 128-entry MIDI→Hz lookup table at construction time. Render-time
/// <see cref="PitchConversion.NoteToFrequency"/> reads <see cref="MidiToHz"/>
/// as an O(1) array lookup, preserving Phase 23 Pattern A (single entry point).
///
/// Scale-step semantics (NOT 12-TET semitone semantics): walking N degrees from
/// <c>kbm.MiddleNote</c> walks N steps of the loaded .scl's step structure. The
/// cross-fixture anchor is <c>MidiToHz[kbm.ReferenceNote] == kbm.ReferenceHz</c>
/// EXACTLY by construction. Default KBM (refNote=69, refHz=440.0) thus pins
/// <c>MidiToHz[69] == 440.0</c> for every loaded tuning.
///
/// Identity: reference equality per CONTEXT Claude's Discretion — no override of
/// Equals / GetHashCode. Two <c>(loadScala "x.scl")</c> calls produce distinct
/// values even with identical content (Phase 32 doesn't cache per SPEC out-of-scope).
/// </summary>
public sealed class ResolvedTuning
{
    private readonly double[] _midiToHz;

    /// <summary>Verbatim first non-comment line from the .scl file (D-04 display).</summary>
    public string Description { get; }

    /// <summary>
    /// Intra-period scale steps in cents (length N-1 per D-10). The period itself
    /// lives in <see cref="PeriodCents"/>, NOT here — render code reads from one
    /// field OR the other, never both.
    /// </summary>
    public IReadOnlyList<double> StepCents { get; }

    /// <summary>
    /// Period of the scale in cents (the final entry of the .scl file). 1200.0
    /// for octave-repeating scales (e.g. <c>2/1</c>); 1404.0 for Carlos Alpha;
    /// anything else for an arbitrary non-octave-repeating scale. Per D-10 this
    /// is a dedicated field separate from <see cref="StepCents"/>.
    /// </summary>
    public double PeriodCents { get; }

    /// <summary>
    /// Ratio-input preservation (D-11): for steps the user authored as <c>n/d</c>
    /// the original integer pair lands here keyed by step index 0..N-1 (where N-1
    /// IS the period if it was authored as a ratio). Cents-input steps are absent
    /// from this dictionary. JI-fan-friendly: <c>(str t)</c> + error messages can
    /// surface exact ratio form.
    /// </summary>
    public IReadOnlyDictionary<int, (int Num, int Den)> Ratios { get; }

    /// <summary>
    /// Always-present keyboard mapping per D-05. <see cref="ScalaKbmParser.Default"/>
    /// (Plan 32-02) supplies a synthetic linear mapping when no <c>.kbm</c> is loaded;
    /// every other code path (render, MIDI export, etc.) treats <c>Kbm</c> as non-null.
    /// </summary>
    public ScalaKbm Kbm { get; }

    /// <summary>
    /// 128-entry MIDI→Hz lookup table eagerly populated at construction (D-02).
    /// Length 128; unmapped slots are 0.0 (D-08 — the unmapped-key advisory fires
    /// at LOAD time per Claude's Discretion, in the Plan 32-04 builtin layer; this
    /// class simply records 0.0 here and trusts the loader to inspect post-ctor).
    /// </summary>
    public IReadOnlyList<double> MidiToHz { get; }

    /// <summary>
    /// Constructs the resolved tuning value. Walks scale steps relative to
    /// <c>kbm.MiddleNote</c> and anchors at <c>kbm.ReferenceNote</c> so that
    /// <c>MidiToHz[refNote] == kbm.ReferenceHz</c> by construction. See the
    /// algorithm body for the math; matches the plan's
    /// <c>&lt;algorithm_semantics&gt;</c> block exactly.
    /// </summary>
    public ResolvedTuning(ParsedScala scl, ScalaKbm kbm)
    {
        if (scl is null) throw new ArgumentNullException(nameof(scl));
        if (kbm is null) throw new ArgumentNullException(nameof(kbm));

        Description = scl.Description;
        StepCents = Array.AsReadOnly(scl.StepCents);
        PeriodCents = scl.PeriodCents;
        Ratios = scl.Ratios;
        Kbm = kbm;

        _midiToHz = new double[128];
        PopulateMidiToHz();
        MidiToHz = Array.AsReadOnly(_midiToHz);
    }

    private void PopulateMidiToHz()
    {
        int stepsPerPeriod = StepCents.Count + 1;
        // FIRST compute middleHz so that MidiToHz[refNote] == refHz EXACTLY by
        // construction. The algorithm walks the SCALE STEPS between MiddleNote and
        // ReferenceNote — NOT 12-TET semitones — using StepCents + PeriodCents.
        int refDegree = Kbm.ReferenceNote - Kbm.MiddleNote;
        int refPeriodWraps = (int)Math.Floor((double)refDegree / stepsPerPeriod);
        int refStepInPeriod = refDegree - refPeriodWraps * stepsPerPeriod;
        double refCentsInPeriod = refStepInPeriod == 0 ? 0.0 : StepCents[refStepInPeriod - 1];
        double refCentsFromMiddle = refPeriodWraps * PeriodCents + refCentsInPeriod;
        double middleHz = Kbm.ReferenceHz * Math.Pow(2.0, -refCentsFromMiddle / 1200.0);

        // Linear mapping (size == 0): every MIDI note 0..127 maps to scale degree
        // (midi - middleNote) wrapping by period. Real KBM (size > 0): each MIDI
        // note inside [FirstMidi, LastMidi] reads its degree from Mapping[]; outside
        // range OR null entry → 0.0 (D-08 unmapped advisory firing point).
        for (int midi = 0; midi < 128; midi++)
        {
            bool unmapped = false;
            int degree = midi - Kbm.MiddleNote;

            if (Kbm.Size > 0)
            {
                if (midi < Kbm.FirstMidi || midi > Kbm.LastMidi)
                {
                    unmapped = true;
                }
                else
                {
                    // Wrap the keymap index by Size so MIDI keys below middleNote
                    // wrap to the top of the keymap. C# `%` is signed-trunc; use
                    // Math.Floor to get proper modulo for negative degrees.
                    int rawIdx = midi - Kbm.MiddleNote;
                    int wrappedIdx = ((rawIdx % Kbm.Size) + Kbm.Size) % Kbm.Size;
                    int? mapped = Kbm.Mapping[wrappedIdx];
                    if (mapped is null)
                    {
                        unmapped = true;
                    }
                    else
                    {
                        // The mapped value is the scale degree; the period-wrap math
                        // proceeds from there (still anchored at MiddleNote == degree 0).
                        int periodWraps0 = (int)Math.Floor((double)rawIdx / Kbm.Size);
                        degree = mapped.Value + periodWraps0 * stepsPerPeriod;
                    }
                }
            }

            if (unmapped)
            {
                _midiToHz[midi] = 0.0;
                continue;
            }

            int periodWraps = (int)Math.Floor((double)degree / stepsPerPeriod);
            int stepInPeriod = degree - periodWraps * stepsPerPeriod;
            double centsInPeriod = stepInPeriod == 0 ? 0.0 : StepCents[stepInPeriod - 1];
            double totalCents = periodWraps * PeriodCents + centsInPeriod;
            _midiToHz[midi] = middleHz * Math.Pow(2.0, totalCents / 1200.0);
        }
    }

    /// <summary>
    /// D-04 string form: <c>Tuning("&lt;description&gt;", N steps, period XXX.XX¢)</c>
    /// where N = StepCents.Count + 1 (matches "number of pitches per period"
    /// semantics from D-10). The trailing <c>¢</c> is U+00A2.
    /// </summary>
    public override string ToString()
    {
        int stepsPerPeriod = StepCents.Count + 1;
        return string.Format(
            CultureInfo.InvariantCulture,
            "Tuning(\"{0}\", {1} steps, period {2:F2}¢)",
            Description, stepsPerPeriod, PeriodCents);
    }
}
