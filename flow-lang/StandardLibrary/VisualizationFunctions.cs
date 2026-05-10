using System.Text;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Provides ASCII piano-roll visualization for sequences.
/// Renders pitch on Y axis (note names), time on X axis (beats),
/// bar lines as | separators, notes as horizontal bars, rests as empty space.
/// </summary>
public static class VisualizationFunctions
{
    /// <summary>
    /// Registers the visualize built-in function.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        var sig = new FunctionSignature("visualize", [SequenceType.Instance]);
        registry.Register("visualize", sig, Visualize);
    }

    /// <summary>
    /// Visualizes a Sequence as an ASCII piano-roll grid printed to stdout.
    /// </summary>
    public static Value Visualize(IReadOnlyList<Value> args)
    {
        var sequence = args[0].Data as SequenceData;
        if (sequence == null || sequence.Bars.Count == 0)
        {
            Console.WriteLine("(empty sequence)");
            return Value.Void();
        }

        var timeline = sequence.ToTimeline();

        // Collect all notes with their absolute beat positions and durations
        var noteEvents = new List<(int midiPitch, string label, double startBeat, double durationBeats)>();
        double totalBeats = 0;
        var barBoundaries = new List<double>();

        foreach (var (bar, offsetBeats) in timeline)
        {
            barBoundaries.Add(offsetBeats);

            if (bar.Mode != BarMode.Musical || bar.TimeSignature == null)
                continue;

            int timeSigDenom = bar.TimeSignature.Denominator;
            double beatCursor = 0;

            foreach (var note in bar.MusicalNotes)
            {
                double noteDuration = note.GetBeats(timeSigDenom);

                if (!note.IsRest)
                {
                    int midi = ToMidi(note.NoteName, note.Octave, note.Alteration);
                    string label = FormatNoteLabel(note.NoteName, note.Octave, note.Alteration);
                    noteEvents.Add((midi, label, offsetBeats + beatCursor, noteDuration));
                }

                beatCursor += noteDuration;
            }

            double barBeats = bar.IsPickup ? bar.GetActualBeats() : bar.TimeSignature.Numerator;
            totalBeats = Math.Max(totalBeats, offsetBeats + barBeats);
        }

        // Add final bar boundary
        barBoundaries.Add(totalBeats);

        if (noteEvents.Count == 0)
        {
            Console.WriteLine("(no notes in sequence)");
            return Value.Void();
        }

        // Determine pitch range
        int minMidi = noteEvents.Min(n => n.midiPitch);
        int maxMidi = noteEvents.Max(n => n.midiPitch);

        // Use 2 columns per beat for eighth-note resolution
        int columnsPerBeat = 2;
        int gridWidth = (int)Math.Ceiling(totalBeats * columnsPerBeat);
        int gridHeight = maxMidi - minMidi + 1;

        if (gridWidth <= 0 || gridHeight <= 0)
        {
            Console.WriteLine("(sequence too short to visualize)");
            return Value.Void();
        }

        // Build the grid
        char[,] grid = new char[gridHeight, gridWidth];
        for (int r = 0; r < gridHeight; r++)
            for (int c = 0; c < gridWidth; c++)
                grid[r, c] = ' ';

        // Place bar line markers
        var barLineColumns = new HashSet<int>();
        foreach (double boundary in barBoundaries)
        {
            int col = (int)Math.Round(boundary * columnsPerBeat);
            if (col >= 0 && col < gridWidth)
                barLineColumns.Add(col);
        }

        // Fill in notes
        foreach (var (midi, label, startBeat, duration) in noteEvents)
        {
            int row = maxMidi - midi; // top = highest pitch
            int startCol = (int)Math.Round(startBeat * columnsPerBeat);
            int endCol = (int)Math.Round((startBeat + duration) * columnsPerBeat);
            endCol = Math.Min(endCol, gridWidth);

            for (int c = startCol; c < endCol; c++)
            {
                if (c >= 0 && c < gridWidth)
                    grid[row, c] = '#';
            }
        }

        // Build pitch labels (collect unique labels per MIDI pitch)
        var pitchLabels = new Dictionary<int, string>();
        foreach (var (midi, label, _, _) in noteEvents)
        {
            pitchLabels.TryAdd(midi, label);
        }

        // Determine label width
        int labelWidth = pitchLabels.Values.Any() ? pitchLabels.Values.Max(l => l.Length) : 2;
        labelWidth = Math.Max(labelWidth, 3);

        // Render output
        var sb = new StringBuilder();

        for (int r = 0; r < gridHeight; r++)
        {
            int midi = maxMidi - r;
            string label = pitchLabels.TryGetValue(midi, out var l) ? l : MidiToLabel(midi);
            sb.Append(label.PadLeft(labelWidth));
            sb.Append(" |");

            for (int c = 0; c < gridWidth; c++)
            {
                if (grid[r, c] == '#')
                    sb.Append('#');
                else if (barLineColumns.Contains(c) && c > 0)
                    sb.Append('|');
                else
                    sb.Append(' ');
            }

            sb.AppendLine("|");
        }

        // Bottom separator line
        sb.Append(new string(' ', labelWidth));
        sb.Append(" +");
        for (int c = 0; c < gridWidth; c++)
        {
            if (barLineColumns.Contains(c) && c > 0)
                sb.Append('+');
            else
                sb.Append('-');
        }
        sb.AppendLine("+");

        // Beat number axis
        sb.Append(new string(' ', labelWidth + 2));
        for (int beat = 0; beat < (int)Math.Ceiling(totalBeats); beat++)
        {
            string beatLabel = (beat + 1).ToString();
            int col = beat * columnsPerBeat;
            if (col < gridWidth)
            {
                sb.Append(beatLabel);
                // Pad to next beat position
                int padding = columnsPerBeat - beatLabel.Length;
                if (padding > 0)
                    sb.Append(new string(' ', padding));
            }
        }
        sb.AppendLine();

        Console.Write(sb.ToString());
        return Value.Void();
    }

    /// <summary>
    /// Converts note name, octave, and alteration to MIDI number.
    /// </summary>
    private static int ToMidi(char noteName, int octave, int alteration)
    {
        int noteOffset = noteName switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
            'G' => 7, 'A' => 9, 'B' => 11,
            _ => 0
        };
        return (octave + 1) * 12 + noteOffset + alteration;
    }

    /// <summary>
    /// Converts a MIDI number back to a note label string.
    /// </summary>
    private static string MidiToLabel(int midi)
    {
        int octave = (midi / 12) - 1;
        int pitchClass = midi % 12;
        string name = pitchClass switch
        {
            0 => "C", 1 => "C#", 2 => "D", 3 => "D#",
            4 => "E", 5 => "F", 6 => "F#", 7 => "G",
            8 => "G#", 9 => "A", 10 => "A#", 11 => "B",
            _ => "?"
        };
        return $"{name}{octave}";
    }

    /// <summary>
    /// Formats a note label from its components.
    /// </summary>
    private static string FormatNoteLabel(char noteName, int octave, int alteration)
    {
        string alt = alteration switch
        {
            1 => "#",
            -1 => "b",
            2 => "##",
            -2 => "bb",
            _ => ""
        };
        return $"{noteName}{alt}{octave}";
    }
}
