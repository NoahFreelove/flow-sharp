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
/// Result of quantization, including chosen metadata and tracks.
/// </summary>
record QuantizeResult(List<QuantizedTrack> Tracks, int TimeSigNumerator, int TimeSigDenominator);

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

    public static QuantizeResult Quantize(MidiFile midi)
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

        // Use first tempo; warn about changes
        if (globalTempoEvents.Count > 1)
            Console.Error.WriteLine($"Warning: {globalTempoEvents.Count} tempo changes found; using the first (BPM={globalTempoEvents[0].Bpm:F1}).");

        int tpqn = midi.TicksPerQuarterNote;

        // Pick the most prevalent time signature (by tick duration), not just the first.
        // Many MIDIs start with a short pickup bar in a different time sig.
        var timeSig = PickPrimaryTimeSig(globalTimeSigEvents, midi);
        int timeSigNum = timeSig.Numerator;
        int timeSigDen = timeSig.Denominator;

        if (globalTimeSigEvents.Count > 1)
            Console.Error.WriteLine($"Warning: {globalTimeSigEvents.Count} time signature changes found; using {timeSigNum}/{timeSigDen}.");

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
                    string baseName = isDrum ? "drums" : $"track_ch{channel + 1}";

                    if (!isDrum)
                        AddSplitTracks(result, baseName, spans, channel, tpqn, timeSigNum, timeSigDen, useFlats);
                    else
                    {
                        var bars = QuantizeSpans(spans, tpqn, timeSigNum, timeSigDen, useFlats);
                        result.Add(new QuantizedTrack(baseName, bars, channel, true));
                    }
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
                string name = !string.IsNullOrWhiteSpace(track.Name) ? SanitizeName(track.Name) : $"track_{trackIndex + 1}";
                if (isDrum) name = "drums";

                int channel = track.Events.OfType<NoteOnEvent>().FirstOrDefault()?.Channel ?? 0;

                if (!isDrum)
                    AddSplitTracks(result, name, spans, channel, tpqn, timeSigNum, timeSigDen, useFlats);
                else
                {
                    var bars = QuantizeSpans(spans, tpqn, timeSigNum, timeSigDen, useFlats);
                    result.Add(new QuantizedTrack(name, bars, channel, true));
                }

                trackIndex++;
            }
        }

        return new QuantizeResult(result, timeSigNum, timeSigDen);
    }

    /// <summary>
    /// Splits a set of note spans into upper/lower voices if the pitch range spans
    /// more than 2 octaves (24 semitones). This handles piano MIDIs where both hands
    /// are on the same track.
    /// </summary>
    static void AddSplitTracks(
        List<QuantizedTrack> result, string baseName, List<NoteSpan> spans,
        int channel, int tpqn, int timeSigNum, int timeSigDen, bool useFlats)
    {
        if (spans.Count == 0) return;

        int minPitch = spans.Min(s => s.Pitch);
        int maxPitch = spans.Max(s => s.Pitch);
        int range = maxPitch - minPitch;

        // Only split if range exceeds 2 octaves (24 semitones)
        if (range <= 24)
        {
            var bars = QuantizeSpans(spans, tpqn, timeSigNum, timeSigDen, useFlats);
            result.Add(new QuantizedTrack(baseName, bars, channel, false));
            return;
        }

        // Find the split point: use the median pitch, but clamp near middle C (MIDI 60)
        var pitches = spans.Select(s => s.Pitch).OrderBy(p => p).ToList();
        int medianPitch = pitches[pitches.Count / 2];

        // Bias the split toward middle C if the median is close
        int splitPoint = medianPitch;
        if (Math.Abs(medianPitch - 60) < 12)
            splitPoint = 60; // Split at middle C

        var upperSpans = spans.Where(s => s.Pitch >= splitPoint).ToList();
        var lowerSpans = spans.Where(s => s.Pitch < splitPoint).ToList();

        // Handle notes right at the split that might belong to either hand
        // by checking simultaneous grouping — if a note at the split is
        // simultaneous with notes above, keep it in upper; otherwise lower.
        // (The simple pitch split above handles most cases well enough.)

        if (upperSpans.Count > 0)
        {
            var upperBars = QuantizeSpans(upperSpans, tpqn, timeSigNum, timeSigDen, useFlats);
            result.Add(new QuantizedTrack(baseName + "_rh", upperBars, channel, false));
        }

        if (lowerSpans.Count > 0)
        {
            var lowerBars = QuantizeSpans(lowerSpans, tpqn, timeSigNum, timeSigDen, useFlats);
            result.Add(new QuantizedTrack(baseName + "_lh", lowerBars, channel, false));
        }
    }

    /// <summary>
    /// Picks the time signature that spans the most ticks in the file.
    /// Many MIDIs start with a short pickup bar (e.g. 1/8) before the "real" time sig.
    /// </summary>
    static (int Numerator, int Denominator) PickPrimaryTimeSig(List<TimeSignatureEvent> events, MidiFile midi)
    {
        if (events.Count == 0)
            return (4, 4);

        if (events.Count == 1)
            return (events[0].Numerator, events[0].Denominator);

        // Find total extent of all notes
        long maxTick = 0;
        foreach (var track in midi.Tracks)
            foreach (var evt in track.Events)
                if (evt.AbsoluteTick > maxTick)
                    maxTick = evt.AbsoluteTick;

        if (maxTick == 0) maxTick = 1;

        // Sort by tick
        var sorted = events.OrderBy(e => e.AbsoluteTick).ToList();

        // Calculate how many ticks each time sig is active for
        var durations = new Dictionary<(int Num, int Den), long>();
        for (int i = 0; i < sorted.Count; i++)
        {
            long start = sorted[i].AbsoluteTick;
            long end = (i + 1 < sorted.Count) ? sorted[i + 1].AbsoluteTick : maxTick;
            var key = (sorted[i].Numerator, sorted[i].Denominator);

            if (!durations.ContainsKey(key))
                durations[key] = 0;
            durations[key] += end - start;
        }

        // Pick the one with the longest total duration
        var best = durations.OrderByDescending(kv => kv.Value).First().Key;
        return best;
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
        // Flow rests are plain "_" with no duration suffix — they auto-fit.
        // Auto-fit divides remaining bar time equally among all suffix-less elements.
        // So we emit the right COUNT of "_" elements to fill the gap.
        //
        // Strategy: find the largest standard duration that evenly divides the gap,
        // then emit that many rests.

        if (ticks <= 0) return;

        // Try standard durations from largest to smallest
        double[] gridMultipliers = { 4.0, 2.0, 1.0, 0.5, 0.25, 0.125 };

        foreach (double mult in gridMultipliers)
        {
            long unitTicks = (long)(mult * tpqn);
            if (unitTicks <= 0) continue;

            // Check if this duration evenly divides the gap (with small tolerance)
            int count = (int)Math.Round((double)ticks / unitTicks);
            if (count > 0 && count <= 16 && Math.Abs(ticks - count * unitTicks) <= tpqn * 0.1)
            {
                // Find the suffix for this multiplier
                var (suffix, isDotted) = SnapDuration(unitTicks, tpqn);
                for (int i = 0; i < count; i++)
                    elements.Add(new RestElement(suffix, isDotted));
                return;
            }
        }

        // Fallback: use single rest (auto-fit will handle it)
        elements.Add(new RestElement("q", false));
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
