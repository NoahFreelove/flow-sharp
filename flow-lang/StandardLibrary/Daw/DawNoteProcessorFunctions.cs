using Flow.Music.Model;
using Flow.Music.Model.Editing;
using Flow.Studio.Model;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Daw;

/// <summary>Pure note-window operations shared by offline processors and Flow
/// authors. No project ownership, file access or interpreter-backed musical state.</summary>
public static class DawNoteProcessorFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        var sequence = DawNoteSequenceType.Instance; var notes = new ArrayType(DawNoteType.Instance);
        registry.Register("dawNoteSequence", new FunctionSignature("dawNoteSequence", [notes, DoubleType.Instance]), args =>
            Wrap(new(args[1].As<double>(), args[0].As<List<Value>>().Select(n => n.As<NoteEvent>()))));
        registry.Register("dawNotes", new FunctionSignature("dawNotes", [sequence]), args =>
            new Value(args[0].As<PluginNoteInput>().Notes.Select(n => new Value(n, DawNoteType.Instance)).ToList(), notes));
        registry.Register("dawNoteDuration", new FunctionSignature("dawNoteDuration", [sequence]), args => Value.Double(args[0].As<PluginNoteInput>().DurationQuarters));
        registry.Register("dawTransposeNotes", new FunctionSignature("dawTransposeNotes", [sequence, IntType.Instance]), args =>
        {
            var input = args[0].As<PluginNoteInput>();
            var detached = new SequenceSnapshot(Guid.Empty, "processor", input.DurationQuarters, input.Notes);
            var moved = Transposition.Apply(detached, args[1].As<int>(), cancellation: SessionServices.Current?.Cancellation ?? default);
            return Wrap(new(input.DurationQuarters, moved.Notes));
        });
        registry.Register("dawNoteVelocity", new FunctionSignature("dawNoteVelocity", [DawNoteType.Instance]), args => Value.Double(args[0].As<NoteEvent>().Velocity));
        registry.Register("dawNoteVelocity", new FunctionSignature("dawNoteVelocity", [DawNoteType.Instance, DoubleType.Instance]), args =>
        {
            double value = args[1].As<double>();
            if (!double.IsFinite(value) || value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
            return new Value(args[0].As<NoteEvent>() with { Velocity = value }, DawNoteType.Instance);
        });
    }
    private static Value Wrap(PluginNoteInput input) => new(input, DawNoteSequenceType.Instance);
}
