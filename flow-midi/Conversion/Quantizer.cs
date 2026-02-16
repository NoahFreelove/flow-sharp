using FlowMidi.Midi;

namespace FlowMidi.Conversion;

/// <summary>
/// A note with start/end ticks, pitch, and velocity — derived from pairing note-on/off events.
/// </summary>
record NoteSpan(long StartTick, long EndTick, int Pitch, int Velocity);

/// <summary>
/// A quantized note element ready for code generation.
/// </summary>
record QuantizedNote(string NoteName, string DurationSuffix, bool IsDotted, bool IsTied, int Velocity);

/// <summary>
/// A chord (multiple simultaneous notes) with a shared duration.
/// </summary>
record QuantizedChord(List<string> NoteNames, string DurationSuffix, bool IsDotted, int Velocity);

/// <summary>
/// A rest element.
/// </summary>
record QuantizedRest(string DurationSuffix, bool IsDotted);

/// <summary>
/// Base interface for elements in a bar.
/// </summary>
interface IBarElement
{
    long DurationTicks(int tpqn);
}

record NoteElement(string NoteName, string DurationSuffix, bool IsDotted, bool IsTied, int Velocity) : IBarElement
{
    public long DurationTicks(int tpqn) => Quantizer.SuffixToTicks(DurationSuffix, IsDotted, tpqn);
}

record ChordElement(List<string> NoteNames, string DurationSuffix, bool IsDotted, int Velocity) : IBarElement
{
    public long DurationTicks(int tpqn) => Quantizer.SuffixToTicks(DurationSuffix, IsDotted, tpqn);
}

record RestElement(string DurationSuffix, bool IsDotted) : IBarElement
{
    public long DurationTicks(int tpqn) => Quantizer.SuffixToTicks(DurationSuffix, IsDotted, tpqn);
}

/// <summary>
/// A bar of quantized elements for one track.
/// </summary>
record QuantizedBar(List<IBarElement> Elements, int BarNumber);

/// <summary>
/// A fully quantized track.
/// </summary>
record QuantizedTrack(string Name, List<QuantizedBar> Bars, int Channel, bool IsDrumTrack);

/// <summary>
/// Converts raw MIDI ticks into musical durations, groups notes into bars,
/// detects chords, and inserts rests for gaps.
/// </summary>
static class Quantizer
{
    /// <summary>
    /// Duration grid entries: (multiplier of TPQN, suffix, isDotted)
    /// Ordered from longest to shortest for snapping.
    /// </summary>
    static readonly (double Multiplier, string Suffix, bool IsDotted)[] DurationGrid =
    {
        (4.0,   "w",  false),   // whole
        (3.0,   "h",  true),    // dotted half
        (2.0,   "h",  false),   // half
        (1.5,   "q",  true),    // dotted quarter
        (1.0,   "q",  false),   // quarter
        (0.75,  "e",  true),    // dotted eighth
        (0.5,   "e",  false),   // eighth
        (0.375, "s",  true),    // dotted sixteenth
        (0.25,  "s",  false),   // sixteenth
        (0.125, "t",  false),   // thirty-second
    };

    public static long SuffixToTicks(string suffix, bool isDotted, int tpqn)
    {
        double mult = suffix switch
        {
            "w" => 4.0,
            "h" => 2.0,
            "q" => 1.0,
            "e" => 0.5,
            "s" => 0.25,
            "t" => 0.125,
            _ => 1.0
        };
        if (isDotted) mult *= 1.5;
        return (long)(mult * tpqn);
    }

    public static List<QuantizedTrack> Quantize(MidiFile midi)
    {
        var result = new List<QuantizedTrack>();

        // Collect global events from all tracks (common in Format 1)
        var globalTempoEvents = new List<TempoEvent>();
        var globalTimeSigEvents = new List<TimeSignatureEvent>();
        var globalKeySigEvents = new List<KeySignatureEvent>();

        foreach (var track in midi.Tracks)
        {
            foreach (var evt in track.Events)
            {
                if (evt is TempoEvent te) globalTempoEvents.Add(te);
                if (evt is TimeSignatureEvent tse) globalTimeSigEvents.Add(tse);
                if (evt is KeySignatureEvent kse) globalKeySigEvents.Add(kse);
            }
        }

        // Use first tempo/time-sig; warn about changes
        if (globalTempoEvents.Count > 1)
            Console.Error.WriteLine($"Warning: {globalTempoEvents.Count} tempo changes found; using the first (BPM={globalTempoEvents[0].Bpm:F1}).");

        if (globalTimeSigEvents.Count > 1)
            Console.Error.WriteLine($"Warning: {globalTimeSigEvents.Count} time signature changes found; using the first.");

        int tpqn = midi.TicksPerQuarterNote;
        int timeSigNum = globalTimeSigEvents.Count > 0 ? globalTimeSigEvents[0].Numerator : 4;
        int timeSigDen = globalTimeSigEvents.Count > 0 ? globalTimeSigEvents[0].Denominator : 4;

        // Use flats when key signature has flats
        bool useFlats = globalKeySigEvents.Count > 0 && globalKeySigEvents[0].SharpsFlats < 0;

        if (midi.Format == 0)
        {
            // Format 0: single track, split by channel
            if (midi.Tracks.Count > 0)
            {
                var byChannel = SplitByChannel(midi.Tracks[0]);
                foreach (var (channel, spans) in byChannel)
                {
                    bool isDrum = channel == 9;
                    string name = isDrum ? "drums" : $"track_ch{channel + 1}";
                    var bars = QuantizeSpans(spans, tpqn, timeSigNum, timeSigDen, useFlats);
                    result.Add(new QuantizedTrack(name, bars, channel, isDrum));
                }
            }
        }
        else
        {
            // Format 1/2: each track is separate
            int trackIndex = 0;
            foreach (var track in midi.Tracks)
            {
                var spans = PairNotes(track.Events);
                if (spans.Count == 0)
                {
                    trackIndex++;
                    continue;
                }

                // Detect drum track (channel 9)
                bool isDrum = track.Events.OfType<NoteOnEvent>().Any(e => e.Channel == 9);
                string name = track.Name != null ? SanitizeName(track.Name) : $"track_{trackIndex + 1}";
                if (isDrum) name = "drums";

                int channel = track.Events.OfType<NoteOnEvent>().FirstOrDefault()?.Channel ?? 0;
                var bars = QuantizeSpans(spans, tpqn, timeSigNum, timeSigDen, useFlats);
                result.Add(new QuantizedTrack(name, bars, channel, isDrum));
                trackIndex++;
            }
        }

        return result;
    }

    static Dictionary<int, List<NoteSpan>> SplitByChannel(MidiTrack track)
    {
        var byChannel = new Dictionary<int, List<NoteSpan>>();
        // Pair notes per channel
        var activeNotes = new Dictionary<(int Channel, int Pitch), (long Tick, int Velocity)>();

        foreach (var evt in track.Events)
        {
            switch (evt)
            {
                case NoteOnEvent on:
                    activeNotes[(on.Channel, on.Pitch)] = (on.AbsoluteTick, on.Velocity);
                    break;
                case NoteOffEvent off:
                    var key = (off.Channel, off.Pitch);
                    if (activeNotes.TryGetValue(key, out var start))
                    {
                        if (!byChannel.ContainsKey(off.Channel))
                            byChannel[off.Channel] = new List<NoteSpan>();
                        byChannel[off.Channel].Add(new NoteSpan(start.Tick, off.AbsoluteTick, off.Pitch, start.Velocity));
                        activeNotes.Remove(key);
                    }
                    break;
            }
        }

        return byChannel;
    }

    static List<NoteSpan> PairNotes(List<MidiEvent> events)
    {
        var spans = new List<NoteSpan>();
        var activeNotes = new Dictionary<(int Channel, int Pitch), (long Tick, int Velocity)>();

        foreach (var evt in events)
        {
            switch (evt)
            {
                case NoteOnEvent on:
                    activeNotes[(on.Channel, on.Pitch)] = (on.AbsoluteTick, on.Velocity);
                    break;
                case NoteOffEvent off:
                    var key = (off.Channel, off.Pitch);
                    if (activeNotes.TryGetValue(key, out var start))
                    {
                        spans.Add(new NoteSpan(start.Tick, off.AbsoluteTick, off.Pitch, start.Velocity));
                        activeNotes.Remove(key);
                    }
                    break;
            }
        }

        return spans.OrderBy(s => s.StartTick).ThenBy(s => s.Pitch).ToList();
    }

    static List<QuantizedBar> QuantizeSpans(List<NoteSpan> spans, int tpqn, int timeSigNum, int timeSigDen, bool useFlats)
    {
        if (spans.Count == 0) return new List<QuantizedBar>();

        // Bar length in ticks
        long barTicks = (long)(tpqn * timeSigNum * (4.0 / timeSigDen));

        // Find the total extent
        long maxTick = spans.Max(s => s.EndTick);
        int totalBars = (int)((maxTick + barTicks - 1) / barTicks);

        var bars = new List<QuantizedBar>();

        for (int barIdx = 0; barIdx < totalBars; barIdx++)
        {
            long barStart = barIdx * barTicks;
            long barEnd = barStart + barTicks;

            // Get notes that start in this bar
            var barSpans = spans
                .Where(s => s.StartTick >= barStart && s.StartTick < barEnd)
                .OrderBy(s => s.StartTick)
                .ThenBy(s => s.Pitch)
                .ToList();

            var elements = new List<IBarElement>();
            long cursor = barStart;

            // Group simultaneous notes (chords)
            var groups = GroupSimultaneous(barSpans, tpqn);

            foreach (var group in groups)
            {
                long groupStart = group[0].StartTick;

                // Insert rest if there's a gap
                if (groupStart > cursor)
                {
                    long gap = groupStart - cursor;
                    AddRests(elements, gap, tpqn);
                    cursor = groupStart;
                }

                if (group.Count == 1)
                {
                    // Single note
                    var span = group[0];
                    long duration = span.EndTick - span.StartTick;

                    // Clamp notes that extend past bar boundary
                    bool tied = span.EndTick > barEnd;
                    if (tied) duration = barEnd - span.StartTick;

                    var (suffix, isDotted) = SnapDuration(duration, tpqn);
                    long snappedDuration = SuffixToTicks(suffix, isDotted, tpqn);
                    string noteName = MidiPitchToFlowNote(span.Pitch, useFlats);
                    elements.Add(new NoteElement(noteName, suffix, isDotted, tied, span.Velocity));
                    cursor += snappedDuration;
                }
                else
                {
                    // Chord — use the duration of the longest note in the group
                    long maxDuration = group.Max(s => s.EndTick - s.StartTick);
                    bool tied = group.Any(s => s.EndTick > barEnd);
                    if (tied) maxDuration = barEnd - groupStart;

                    var (suffix, isDotted) = SnapDuration(maxDuration, tpqn);
                    long snappedDuration = SuffixToTicks(suffix, isDotted, tpqn);
                    var noteNames = group.Select(s => MidiPitchToFlowNote(s.Pitch, useFlats)).ToList();
                    int avgVelocity = (int)group.Average(s => s.Velocity);
                    elements.Add(new ChordElement(noteNames, suffix, isDotted, avgVelocity));
                    cursor += snappedDuration;
                }
            }

            // Fill remaining bar with rest
            if (cursor < barEnd)
            {
                long remaining = barEnd - cursor;
                AddRests(elements, remaining, tpqn);
            }

            if (elements.Count > 0)
                bars.Add(new QuantizedBar(elements, barIdx));
        }

        // Trim trailing empty/rest-only bars
        while (bars.Count > 0 && bars[^1].Elements.All(e => e is RestElement))
            bars.RemoveAt(bars.Count - 1);

        return bars;
    }

    static List<List<NoteSpan>> GroupSimultaneous(List<NoteSpan> spans, int tpqn)
    {
        if (spans.Count == 0) return new List<List<NoteSpan>>();

        // Notes within this tolerance of each other's start time are simultaneous
        long tolerance = Math.Max(tpqn / 48, 1); // ~10 ticks at TPQN=480

        var groups = new List<List<NoteSpan>>();
        var currentGroup = new List<NoteSpan> { spans[0] };

        for (int i = 1; i < spans.Count; i++)
        {
            if (Math.Abs(spans[i].StartTick - currentGroup[0].StartTick) <= tolerance)
            {
                currentGroup.Add(spans[i]);
            }
            else
            {
                groups.Add(currentGroup);
                currentGroup = new List<NoteSpan> { spans[i] };
            }
        }
        groups.Add(currentGroup);

        return groups;
    }

    static (string Suffix, bool IsDotted) SnapDuration(long ticks, int tpqn)
    {
        // Clamp very short notes to thirty-second
        double minTicks = tpqn * 0.125;
        if (ticks < minTicks * 0.5)
            return ("t", false);

        // Find closest grid value within 15% tolerance
        double bestDistance = double.MaxValue;
        string bestSuffix = "q";
        bool bestDotted = false;

        foreach (var (mult, suffix, isDotted) in DurationGrid)
        {
            double gridTicks = mult * tpqn;
            double distance = Math.Abs(ticks - gridTicks);
            double tolerance = gridTicks * 0.15;

            if (distance <= tolerance && distance < bestDistance)
            {
                bestDistance = distance;
                bestSuffix = suffix;
                bestDotted = isDotted;
            }
        }

        // If no grid match found within tolerance, use closest
        if (bestDistance == double.MaxValue)
        {
            foreach (var (mult, suffix, isDotted) in DurationGrid)
            {
                double gridTicks = mult * tpqn;
                double distance = Math.Abs(ticks - gridTicks);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestSuffix = suffix;
                    bestDotted = isDotted;
                }
            }
        }

        return (bestSuffix, bestDotted);
    }

    static void AddRests(List<IBarElement> elements, long ticks, int tpqn)
    {
        // Decompose a gap into the fewest rest elements
        long remaining = ticks;
        int safety = 20; // prevent infinite loop

        while (remaining > 0 && safety-- > 0)
        {
            // Find largest grid value that fits
            string bestSuffix = "t";
            bool bestDotted = false;
            long bestTicks = (long)(tpqn * 0.125);

            foreach (var (mult, suffix, isDotted) in DurationGrid)
            {
                long gridTicks = (long)(mult * tpqn);
                if (gridTicks <= remaining + (long)(gridTicks * 0.05)) // small tolerance
                {
                    bestSuffix = suffix;
                    bestDotted = isDotted;
                    bestTicks = gridTicks;
                    break; // Grid is sorted longest-first, take the biggest that fits
                }
            }

            elements.Add(new RestElement(bestSuffix, bestDotted));
            remaining -= bestTicks;
            if (remaining < 0) remaining = 0;
        }
    }

    // Sharp names: C C# D D# E F F# G G# A A# B
    static readonly string[] SharpNames = { "C", "C", "D", "D", "E", "F", "F", "G", "G", "A", "A", "B" };
    static readonly bool[] IsSharp =      { false, true, false, true, false, false, true, false, true, false, true, false };

    // Flat names: C Db D Eb E F Gb G Ab A Bb B
    static readonly string[] FlatNames = { "C", "D", "D", "E", "E", "F", "G", "G", "A", "A", "B", "B" };
    static readonly bool[] IsFlat =      { false, true, false, true, false, false, true, false, true, false, true, false };

    static string MidiPitchToFlowNote(int midiPitch, bool useFlats)
    {
        int octave = (midiPitch / 12) - 1;
        int semitone = midiPitch % 12;

        if (useFlats)
        {
            string name = FlatNames[semitone];
            string alteration = IsFlat[semitone] ? "-" : "";
            return $"{name}{octave}{alteration}";
        }
        else
        {
            string name = SharpNames[semitone];
            string alteration = IsSharp[semitone] ? "+" : "";
            return $"{name}{octave}{alteration}";
        }
    }

    static string SanitizeName(string name)
    {
        // Replace non-alphanumeric chars with underscore, collapse multiples
        var sb = new System.Text.StringBuilder();
        bool lastWasUnderscore = false;

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasUnderscore = false;
            }
            else if (!lastWasUnderscore)
            {
                sb.Append('_');
                lastWasUnderscore = true;
            }
        }

        string result = sb.ToString().Trim('_');

        // Ensure it starts with a letter
        if (result.Length == 0 || char.IsDigit(result[0]))
            result = "track_" + result;

        return result.ToLowerInvariant();
    }
}
