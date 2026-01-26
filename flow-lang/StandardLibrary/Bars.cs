using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Bar (musical measure) operations.
/// </summary>
public static class Bars
{
    /// <summary>
    /// Creates an empty bar.
    /// </summary>
    public static Value CreateBar(IReadOnlyList<Value> args)
    {
        var bar = new BarData();
        return Value.Bar(bar);
    }

    /// <summary>
    /// Creates a bar with a single note.
    /// </summary>
    public static Value CreateBarWithNote(IReadOnlyList<Value> args)
    {
        var note = args[0].As<string>();
        var bar = new BarData(new[] { note });
        return Value.Bar(bar);
    }

    /// <summary>
    /// Creates a bar from an array of notes.
    /// </summary>
    public static Value CreateBarFromNotes(IReadOnlyList<Value> args)
    {
        var notesArray = args[0].As<IReadOnlyList<Value>>();
        var notes = notesArray.Select(v => v.As<string>()).ToList();
        var bar = new BarData(notes);
        return Value.Bar(bar);
    }

    /// <summary>
    /// Adds a note to an existing bar (mutates the bar).
    /// </summary>
    public static Value AddNoteToBar(IReadOnlyList<Value> args)
    {
        var bar = args[0].As<BarData>();
        var note = args[1].As<string>();
        bar.AddNote(note);
        return Value.Void();
    }

    /// <summary>
    /// Gets a note from a bar at the specified index.
    /// </summary>
    public static Value GetNoteFromBar(IReadOnlyList<Value> args)
    {
        var bar = args[0].As<BarData>();
        int index = args[1].As<int>();
        string note = bar.GetNote(index);
        return Value.Note(note);
    }

    /// <summary>
    /// Gets the number of notes in a bar.
    /// </summary>
    public static Value BarLength(IReadOnlyList<Value> args)
    {
        var bar = args[0].As<BarData>();
        return Value.Int(bar.Count);
    }

    /// <summary>
    /// Sets the time signature for a bar.
    /// </summary>
    public static Value SetTimeSignature(IReadOnlyList<Value> args)
    {
        var bar = args[0].As<BarData>();
        int numerator = args[1].As<int>();
        int denominator = args[2].As<int>();

        bar.TimeSignatureNumerator = numerator;
        bar.TimeSignatureDenominator = denominator;

        return Value.Void();
    }

    /// <summary>
    /// Gets the time signature of a bar as a string (e.g., "4/4").
    /// </summary>
    public static Value GetTimeSignature(IReadOnlyList<Value> args)
    {
        var bar = args[0].As<BarData>();

        if (bar.TimeSignatureNumerator.HasValue && bar.TimeSignatureDenominator.HasValue)
        {
            string signature = $"{bar.TimeSignatureNumerator}/{bar.TimeSignatureDenominator}";
            return Value.String(signature);
        }

        return Value.String("(no time signature)");
    }
}
