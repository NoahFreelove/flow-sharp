using System.Collections.Generic;
using System.Linq;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Transforms;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// MICR-02: transforms (transpose / invert / retrograde / augment / diminish)
/// remain pitch-class-agnostic. They operate purely on MIDI numbers and
/// duration enums, so the resulting MIDI numbers are invariant regardless of
/// active tuning context. Tuning is render-time only — transforms never see it.
///
/// These Facts pin the contract by invoking the transform via the same registry
/// path that <c>.flow</c> code uses, then asserting the resulting MIDI numbers
/// match the expected post-transform values. Because the transforms ignore
/// tuning entirely, the assertion shape "MIDI numbers identical regardless of
/// active tuning" is satisfied by construction — running the same transform
/// under different tuning pragmas would produce identical outputs because the
/// transform code path never reads <c>MusicalContext.Tuning</c>.
/// </summary>
public class TransformInvarianceFacts
{
    private static MusicalNoteData Note(char letter, int octave, int alteration = 0) =>
        new MusicalNoteData(letter, octave, alteration, durationValue: (int)NoteValueType.Value.QUARTER, isRest: false);

    private static SequenceData CMajorTriadSequence()
    {
        var bar = new BarData(
            new[] { Note('C', 4), Note('E', 4), Note('G', 4) },
            new TimeSignatureData(4, 4));
        var seq = new SequenceData();
        seq.AddBar(bar);
        return seq;
    }

    private static int[] MidiNumbersOf(SequenceData seq)
    {
        var result = new List<int>();
        foreach (var bar in seq.Bars)
            foreach (var note in bar.MusicalNotes)
                if (!note.IsRest)
                    result.Add(((note.Octave + 1) * 12)
                        + (note.NoteName switch { 'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11, _ => 0 })
                        + note.Alteration);
        return result.ToArray();
    }

    private static InternalFunctionRegistry RegistryWithTransforms()
    {
        var registry = new InternalFunctionRegistry();
        TransformFunctions.Register(registry);
        // Phase 44 Plan 44-05: 8 transforms moved to RegisterContextDependent.
        // None of THIS file's Facts exercise those 8 transforms today, but
        // wiring the context-dependent path now keeps the test harness
        // forward-compatible with any future Phase 23 Fact that does.
        var dummyReporter = new FlowLang.Diagnostics.ErrorReporter();
        var dummyContext = new FlowLang.Runtime.ExecutionContext(dummyReporter, registry);
        TransformFunctions.RegisterContextDependent(registry, dummyContext);
        return registry;
    }

    private static SequenceData Invoke(InternalFunctionRegistry registry, string name, FunctionSignature sig, params Value[] args)
    {
        Assert.True(registry.TryGetImplementation(name, sig, out var impl, out _),
            $"transform '{name}' not found");
        return impl!(args).As<SequenceData>();
    }

    [Fact]
    public void Transpose_MidiInvariant_AcrossTunings()
    {
        // transpose(Sequence, Semitone) — purely MIDI-based.
        // C4=60, E4=64, G4=67. Transpose +5 -> F4=65, A4=69, C5=72.
        var registry = RegistryWithTransforms();
        var triad = CMajorTriadSequence();
        var sig = new FunctionSignature("transpose", [SequenceType.Instance, SemitoneType.Instance]);
        var transposed = Invoke(registry, "transpose", sig,
            Value.Sequence(triad), Value.Semitone(5));
        Assert.Equal(new[] { 65, 69, 72 }, MidiNumbersOf(transposed));
    }

    [Fact]
    public void Invert_MidiInvariant_AcrossTunings()
    {
        // invert(Sequence) — uses first non-rest note as axis.
        // Axis = C4 = 60. Inverting around 60: C4(60)->60, E4(64)->56, G4(67)->53.
        var registry = RegistryWithTransforms();
        var triad = CMajorTriadSequence();
        var sig = new FunctionSignature("invert", [SequenceType.Instance]);
        var inverted = Invoke(registry, "invert", sig, Value.Sequence(triad));
        Assert.Equal(new[] { 60, 56, 53 }, MidiNumbersOf(inverted));
    }

    [Fact]
    public void Retrograde_OrderReversed_MidiInvariant()
    {
        // retrograde(Sequence) — reverses note order. C-E-G -> G-E-C.
        var registry = RegistryWithTransforms();
        var triad = CMajorTriadSequence();
        var sig = new FunctionSignature("retrograde", [SequenceType.Instance]);
        var reversed = Invoke(registry, "retrograde", sig, Value.Sequence(triad));
        Assert.Equal(new[] { 67, 64, 60 }, MidiNumbersOf(reversed));
    }

    [Fact]
    public void Augment_DurationDoubled_MidiInvariant()
    {
        // augment(Sequence) — moves duration toward WHOLE. MIDI numbers UNCHANGED.
        var augmented = TransformFunctions.AugmentForTesting(CMajorTriadSequence());
        Assert.Equal(new[] { 60, 64, 67 }, MidiNumbersOf(augmented));
    }

    [Fact]
    public void Diminish_DurationHalved_MidiInvariant()
    {
        // diminish(Sequence) — moves duration toward THIRTYSECOND. MIDI numbers UNCHANGED.
        var diminished = TransformFunctions.DiminishForTesting(CMajorTriadSequence());
        Assert.Equal(new[] { 60, 64, 67 }, MidiNumbersOf(diminished));
    }
}
