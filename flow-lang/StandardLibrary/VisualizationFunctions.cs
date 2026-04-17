using System.Text;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
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

        var sig2 = new FunctionSignature("visualize", [BufferType.Instance]);
        registry.Register("visualize", sig2, VisualizeBuffer);
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
    /// Visualizes an AudioBuffer as an ASCII waveform.
    /// </summary>
    public static Value VisualizeBuffer(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        if (buffer.Frames == 0)
        {
            Console.WriteLine("(empty buffer)");
            return Value.Void();
        }

        // Downmix to mono if stereo
        float[] data;
        if (buffer.Channels == 1)
        {
            data = buffer.Data;
        }
        else
        {
            data = new float[buffer.Frames];
            for (int i = 0; i < buffer.Frames; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < buffer.Channels; ch++)
                    sum += buffer.GetSample(i, ch);
                data[i] = sum / buffer.Channels;
            }
        }

        const int width = 80;
        const int height = 20;
        char[,] grid = new char[height, width];
        for (int r = 0; r < height; r++)
            for (int c = 0; c < width; c++)
                grid[r, c] = ' ';

        // Subsample the data to fit width
        float step = (float)buffer.Frames / width;
        for (int x = 0; x < width; x++)
        {
            int start = (int)(x * step);
            int end = (int)((x + 1) * step);
            if (end > buffer.Frames) end = buffer.Frames;

            // Find min/max in this window
            float min = 1f;
            float max = -1f;
            for (int i = start; i < end; i++)
            {
                if (data[i] < min) min = data[i];
                if (data[i] > max) max = data[i];
            }

            // Map to grid rows
            int rMin = (int)((1f - max) * 0.5f * (height - 1));
            int rMax = (int)((1f - min) * 0.5f * (height - 1));
            rMin = Math.Clamp(rMin, 0, height - 1);
            rMax = Math.Clamp(rMax, 0, height - 1);

            for (int r = rMin; r <= rMax; r++)
                grid[r, x] = '#';
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Waveform Visualization ({buffer.Frames} frames, {buffer.SampleRate}Hz)");
        sb.Append("    +");
        sb.Append(new string('-', width));
        sb.AppendLine("+");

        for (int r = 0; r < height; r++)
        {
            float val = 1.0f - (r * 2.0f / (height - 1));
            sb.Append($"{val,4:F1} |");
            for (int c = 0; c < width; c++)
                sb.Append(grid[r, c]);
            sb.AppendLine("|");
        }

        sb.Append("    +");
        sb.Append(new string('-', width));
        sb.AppendLine("+");
        
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
