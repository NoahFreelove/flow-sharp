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
    ///
    /// Phase 38 Plan 38-04 D-38-10: <c>(inspect seq)</c> ships as a builtin-level
    /// alias backed by the same <see cref="Visualize"/> dispatch (overrides
    /// REQUIREMENTS.md REPL-04 wording per D-v1.5-01 single-commit migration —
    /// composer can call either name; identical output).
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        var sig = new FunctionSignature("visualize", [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("visualize", sig, Visualize);

        var sig2 = new FunctionSignature("visualize", [BufferType.Instance],
            ParameterNames: ["buf"]);
        registry.Register("visualize", sig2, VisualizeBuffer);

        // Phase 38 Plan 38-04 D-38-10 — (inspect seq) alias (same dispatch).
        var sig3 = new FunctionSignature("inspect", [SequenceType.Instance],
            ParameterNames: ["seq"]);
        registry.Register("inspect", sig3, Visualize);

        // (inspect buf) alias mirrors (visualize buf) so the documented alias
        // pair holds for both overloads.
        var sig4 = new FunctionSignature("inspect", [BufferType.Instance],
            ParameterNames: ["buf"]);
        registry.Register("inspect", sig4, VisualizeBuffer);
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

        // Collect all notes with their absolute beat positions, durations, and
        // articulation (Phase 38 Plan 38-04 D-38-10 — articulation drives the
        // onset glyph per UI-SPEC §"Glyph Inventory" lines 187-201).
        var noteEvents = new List<(int midiPitch, string label, double startBeat, double durationBeats, Articulation articulation)>();
        double totalBeats = 0;
        var barBoundaries = new List<double>();

        foreach (var (bar, offsetBeats) in timeline)
        {
            barBoundaries.Add(offsetBeats);

            if (bar.Mode != BarMode.Musical || bar.TimeSignature == null)
                continue;

            // Voice-block bars carry their audible content exclusively in
            // ParallelVoices; the parent bar's MusicalNotes holds a single
            // whole-bar rest placeholder (NoteStreamCompiler). Mirror the audio
            // / MIDI / MusicXML / LilyPond paths (BarRenderer.cs:62) and stack
            // every voice on the shared bar onset so overlapping voices show on
            // their own pitch rows. When ParallelVoices is present the parent
            // MusicalNotes is just the placeholder rest, so the two passes do
            // not double-count.
            if (bar.ParallelVoices != null && bar.ParallelVoices.Count > 0)
            {
                foreach (var voiceBar in bar.ParallelVoices)
                {
                    var voiceSig = voiceBar.TimeSignature ?? bar.TimeSignature;
                    CollectNoteEvents(voiceBar.MusicalNotes, voiceSig.Denominator, offsetBeats, noteEvents);
                }
            }
            else
            {
                CollectNoteEvents(bar.MusicalNotes, bar.TimeSignature.Denominator, offsetBeats, noteEvents);
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

        // Fill in notes per Phase 38 Plan 38-04 D-38-10:
        //   - First cell of a sustained note is the articulation glyph (UI-SPEC line 210):
        //       Accent → '>', Staccato → '.', Marcato → '^', Tenuto → '_',
        //       Sforzando → '!', Normal/Legato → '#' (Legato handled by the gap-fill pass
        //       below per UI-SPEC line 212).
        //   - Subsequent cells stay '#' (the sustain glyph; pre-Phase-38 behavior).
        //   - Single-cell notes collapse to the onset glyph alone (UI-SPEC line 211 —
        //     naturally true because the loop stops when endCol == startCol).
        foreach (var (midi, label, startBeat, duration, articulation) in noteEvents)
        {
            int row = maxMidi - midi; // top = highest pitch
            int startCol = (int)Math.Round(startBeat * columnsPerBeat);
            int endCol = (int)Math.Round((startBeat + duration) * columnsPerBeat);
            endCol = Math.Min(endCol, gridWidth);

            // Ensure at least one cell renders for very short notes (so the onset glyph is
            // visible per UI-SPEC line 211 — single-cell collapse).
            if (endCol <= startCol) endCol = startCol + 1;
            endCol = Math.Min(endCol, gridWidth);

            char onsetGlyph = articulation switch
            {
                Articulation.Accent => '>',
                Articulation.Staccato => '.',
                Articulation.Marcato => '^',
                Articulation.Tenuto => '_',
                Articulation.Sforzando => '!',
                _ => '#'  // Normal — pre-Phase-38 baseline. Legato handled separately below.
            };

            for (int c = startCol; c < endCol; c++)
            {
                if (c >= 0 && c < gridWidth)
                    grid[row, c] = (c == startCol) ? onsetGlyph : '#';
            }
        }

        // Phase 38 Plan 38-04 D-38-10 + UI-SPEC line 212 — Legato gap-fill pass.
        // For each Legato note, look back to the previous note on the same row that ends
        // immediately before this note's startCol; fill the gap cell with `~`. Charitable
        // skip when no adjacent prior-row note exists (D-v1.5-05).
        var rowNoteEnds = new Dictionary<int, List<(int startCol, int endCol)>>();
        foreach (var (midi, _, startBeat, duration, _) in noteEvents)
        {
            int row = maxMidi - midi;
            int startCol = (int)Math.Round(startBeat * columnsPerBeat);
            int endCol = (int)Math.Round((startBeat + duration) * columnsPerBeat);
            if (!rowNoteEnds.ContainsKey(row)) rowNoteEnds[row] = new List<(int, int)>();
            rowNoteEnds[row].Add((startCol, endCol));
        }
        foreach (var (midi, _, startBeat, _, articulation) in noteEvents)
        {
            if (articulation != Articulation.Legato) continue;
            int row = maxMidi - midi;
            int startCol = (int)Math.Round(startBeat * columnsPerBeat);
            // Look for a prior note on the same row ending at startCol or startCol-1.
            if (!rowNoteEnds.TryGetValue(row, out var spans)) continue;
            foreach (var (prevStart, prevEnd) in spans)
            {
                if (prevEnd >= startCol) continue;
                int gapCol = prevEnd; // first empty cell after the previous note
                if (gapCol >= 0 && gapCol < gridWidth && gapCol < startCol && grid[row, gapCol] == ' ')
                {
                    grid[row, gapCol] = '~';
                    break;
                }
            }
        }

        // Build pitch labels (collect unique labels per MIDI pitch)
        var pitchLabels = new Dictionary<int, string>();
        foreach (var (midi, label, _, _, _) in noteEvents)
        {
            pitchLabels.TryAdd(midi, label);
        }

        // Determine label width
        int labelWidth = pitchLabels.Values.Any() ? pitchLabels.Values.Max(l => l.Length) : 2;
        labelWidth = Math.Max(labelWidth, 3);

        // Render output
        var sb = new StringBuilder();

        // Phase 38 Plan 38-04 D-38-10 + UI-SPEC lines 217-228 — tick-mark row
        // rendered ABOVE the first pitch row. Format mirrors the existing bottom
        // separator (`+` at bar-line cols, `-` elsewhere) and is followed by
        // beat-number annotations below the rule.
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

        for (int r = 0; r < gridHeight; r++)
        {
            int midi = maxMidi - r;
            string label = pitchLabels.TryGetValue(midi, out var l) ? l : MidiToLabel(midi);
            sb.Append(label.PadLeft(labelWidth));
            sb.Append(" |");

            for (int c = 0; c < gridWidth; c++)
            {
                char cell = grid[r, c];
                // Articulation glyphs + sustain + legato gap-fill take precedence
                // over the bar-line stamp at the same cell EXCEPT for `|` itself —
                // per UI-SPEC line 214, bar line wins over sustain `#`.
                if (cell != ' ' && cell != '#')
                {
                    // Onset glyphs (>./^_!~) win over sustain; per UI-SPEC line 213.
                    sb.Append(cell);
                }
                else if (cell == '#')
                {
                    // Sustain — bar line wins per UI-SPEC line 214.
                    if (barLineColumns.Contains(c) && c > 0)
                        sb.Append('|');
                    else
                        sb.Append('#');
                }
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
    /// Collects note events from a flat list of musical notes, advancing a fresh
    /// per-list beat cursor that starts at <paramref name="offsetBeats"/>. Shared by
    /// the single-voice and parallel-voice (voice-block) passes — each parallel
    /// voice starts at the same bar onset so overlapping voices stack correctly.
    /// Rests advance the cursor but emit no event.
    /// </summary>
    private static void CollectNoteEvents(
        IReadOnlyList<MusicalNoteData> notes,
        int timeSigDenom,
        double offsetBeats,
        List<(int midiPitch, string label, double startBeat, double durationBeats, Articulation articulation)> noteEvents)
    {
        double beatCursor = 0;
        foreach (var note in notes)
        {
            double noteDuration = note.GetBeats(timeSigDenom);

            if (!note.IsRest)
            {
                int midi = ToMidi(note.NoteName, note.Octave, note.Alteration);
                string label = FormatNoteLabel(note.NoteName, note.Octave, note.Alteration);
                noteEvents.Add((midi, label, offsetBeats + beatCursor, noteDuration, note.Articulation));
            }

            beatCursor += noteDuration;
        }
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
