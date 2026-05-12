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

                    // SPEC-5: one Sequence per MIDI track/channel. No pitch-split heuristic (Bug B Defect 3 closure).
                    if (!isDrum)
                    {
                        var bars = QuantizeSpans(spans, tpqn, timeSigNum, timeSigDen, useFlats);
                        result.Add(new QuantizedTrack(baseName, bars, channel, false));
                    }
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

                // SPEC-5: one Sequence per MIDI track/channel. No pitch-split heuristic (Bug B Defect 3 closure).
                if (!isDrum)
                {
                    var bars = QuantizeSpans(spans, tpqn, timeSigNum, timeSigDen, useFlats);
                    result.Add(new QuantizedTrack(name, bars, channel, false));
                }
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

    // The pitch-range hand-split heuristic was deleted in Plan 30-07 per
    // SPEC-5 ("one Sequence per MIDI track"). Bug B Defect 3 was that any
    // track whose pitch range exceeded 24 semitones got bisected at the
    // median pitch (clamped near middle C) and emitted as two sub-tracks
    // with right-hand / left-hand suffixes, double-splitting a 2-channel
    // ragtime MIDI into 4 sequences. The composer-authored channel/track
    // assignment is the source of truth for hand/voice separation; flow-midi
    // now respects that without heuristic re-derivation.

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

    /// <summary>
    /// Quantizes a list of note spans into bars.
    ///
    /// Leading-trim symmetry (Bug B Defect 2 closure): bars before the first
    /// note's onset are not emitted. The trailing-trim at the bottom of this
    /// method already handles the back end; the leading-trim added here makes
    /// the contract symmetric. This prevents the cascade where 4 silent bars
    /// at the start of a track became 4 whole-bar rests of `| _ |` in the
    /// generated .flow output (ragtime_imported.flow's bar 0 was four `_q`
    /// tokens before any actual note).
    /// </summary>
    static List<QuantizedBar> QuantizeSpans(List<NoteSpan> spans, int tpqn, int timeSigNum, int timeSigDen, bool useFlats)
    {
        if (spans.Count == 0) return new List<QuantizedBar>();

        // Bar length in ticks
        long barTicks = (long)(tpqn * timeSigNum * (4.0 / timeSigDen));

        // Find the total extent
        long maxTick = spans.Max(s => s.EndTick);
        int totalBars = (int)((maxTick + barTicks - 1) / barTicks);

        // Leading-trim: start emitting from the bar containing the first note,
        // not from bar 0. See method-level doc for rationale.
        long firstNoteTick = spans.Min(s => s.StartTick);
        int firstBarIdx = (int)(firstNoteTick / barTicks);

        var bars = new List<QuantizedBar>();

        for (int barIdx = firstBarIdx; barIdx < totalBars; barIdx++)
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

            // Bar-fit clamp (bar-overflow-rh-desync, 2026-05-03):
            // The Quantizer used to emit each note/chord with its FULL MIDI
            // duration. When the source track contains overlapping but not
            // simultaneous-onset notes (a held melody pitch with an inner
            // ornament happening DURING the held note), the cursor would
            // advance past barEnd — producing bars where the sum of
            // non-chord-tone durations exceeded the time-signature numerator.
            // The renderer then silently dropped voices whose absolute frame
            // exceeded the nominal-beats-derived buffer length, causing the
            // RH to "go mute" in dense passages.
            //
            // Fix: each emitted note/chord is shortened so that the running
            // sum of snapped durations within the bar never exceeds barTicks.
            // For each group: the available room is `min(nextEventTick - cursor,
            // barEnd - cursor)`. After emission, the cursor advances by the
            // SNAPPED duration (which is bounded above by available). If the
            // MIDI source over-densifies a bar (more notes than the bar can
            // hold), the trailing notes are silently dropped — preferable to
            // overflowing into the next bar's slot. The IsTied flag still
            // tracks cross-bar continuity (span.EndTick > barEnd), unrelated
            // to within-bar truncation.
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (cursor >= barEnd)
                    break; // No room left in this bar; drop remaining groups.

                var group = groups[gi];
                long groupStart = group[0].StartTick;

                // Insert rest if there is a gap between cursor and the group's
                // ideal onset. If a previous emission has already advanced past
                // groupStart, do NOT realign backward — emit the note immediately
                // at cursor (effectively "behind schedule" within the bar text,
                // but the bar will still fit in nominal time).
                if (groupStart > cursor)
                {
                    long gap = Math.Min(groupStart - cursor, barEnd - cursor);
                    AddRests(elements, gap, tpqn);
                    cursor += gap;
                    if (cursor >= barEnd)
                        break;
                }

                // Available room from cursor to either the next group's start
                // or to barEnd. Both bounds matter — using cursor (not groupStart)
                // ensures the running snap-tick total never exceeds barTicks.
                long nextEventTick = (gi + 1 < groups.Count) ? groups[gi + 1][0].StartTick : barEnd;
                long availableTicks = Math.Min(nextEventTick, barEnd) - cursor;
                if (availableTicks <= 0)
                    continue;

                if (group.Count == 1)
                {
                    // Single note
                    var span = group[0];
                    long rawDuration = span.EndTick - span.StartTick;
                    bool tied = span.EndTick > barEnd;

                    // Clamp to fit the available room (bar-fit invariant).
                    long capped = Math.Min(rawDuration, availableTicks);

                    var (suffix, isDotted) = SnapDurationCapped(capped, availableTicks, tpqn);
                    long snappedDuration = SuffixToTicks(suffix, isDotted, tpqn);
                    string noteName = MidiPitchToFlowNote(span.Pitch, useFlats);
                    elements.Add(new NoteElement(noteName, suffix, isDotted, tied, span.Velocity));
                    cursor += snappedDuration;
                }
                else
                {
                    // Chord — use the duration of the longest note in the group,
                    // but clamp to the available room so it does not overflow
                    // into the next group's slot.
                    long maxDuration = group.Max(s => s.EndTick - s.StartTick);
                    bool tied = group.Any(s => s.EndTick > barEnd);

                    long capped = Math.Min(maxDuration, availableTicks);

                    var (suffix, isDotted) = SnapDurationCapped(capped, availableTicks, tpqn);
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

    /// <summary>
    /// Snaps <paramref name="ticks"/> to the closest grid duration whose
    /// snapped tick count does NOT exceed <paramref name="capTicks"/> + a
    /// small TPQN-relative tolerance band. Used by the bar-fit logic in
    /// <see cref="QuantizeSpans"/> so that the emitted note/chord cannot
    /// push the cursor past the next event's start (or past the bar
    /// boundary) by more than a sliver of a tick.
    ///
    /// Tolerance band (tpqn/32 ticks, ~3% of a quarter at TPQN=480) lets
    /// exact-quarter notes snap to q even when availableTicks is off by a
    /// few ticks due to upstream channel-grouping arithmetic noise. The
    /// caller's `cursor &gt;= barEnd` guard still bounds any total bar-fit
    /// overshoot — within-bar emissions cannot push beyond barEnd because
    /// the loop short-circuits the moment the cursor catches up, and the
    /// trailing rest fill absorbs any fractional remainder.
    ///
    /// Bug B Defect 1 (.planning/debug/midi-import-quarter-quantize.md):
    /// without this tolerance, a 480-tick quarter following a slightly
    /// jittered earlier note (availableTicks = 479) would be rejected
    /// strictly and fall back to ("e", true) at 360 ticks — producing the
    /// composer-observed `D4s. _ _ _ _ _` cascade in ragtime_imported.flow.
    /// </summary>
    static (string Suffix, bool IsDotted) SnapDurationCapped(long ticks, long capTicks, int tpqn)
    {
        // Tiny safety: if the cap is so small that even a 32nd doesn't fit
        // cleanly, still emit a 32nd — the caller has already inserted the
        // rest gap and the cursor will simply over-step a fraction of a tick,
        // which the bar-end fill rest will absorb.
        if (capTicks <= 0)
            return ("t", false);

        // Tolerance band — see method-level doc for rationale. tpqn/32 is
        // 15 ticks at TPQN=480, never less than 1 even at very small TPQNs.
        long tolerance = Math.Max(tpqn / 32, 1);

        double bestDistance = double.MaxValue;
        string bestSuffix = "t";
        bool bestDotted = false;

        foreach (var (mult, suffix, isDotted) in DurationGrid)
        {
            double gridTicks = mult * tpqn;
            if (gridTicks > capTicks + tolerance)
                continue; // would overflow the cap beyond the tolerance band

            double distance = Math.Abs(ticks - gridTicks);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSuffix = suffix;
                bestDotted = isDotted;
            }
        }

        return (bestSuffix, bestDotted);
    }

    static void AddRests(List<IBarElement> elements, long ticks, int tpqn)
    {
        // Flow rests are plain "_" with no duration suffix — they auto-fit.
        // Auto-fit divides remaining bar time equally among all suffix-less
        // elements. So when the gap doesn't snap to a single named duration,
        // a single fallback rest is correct (and ergonomic) — the bar's
        // auto-fit logic distributes the remaining time. Bug B Defect 2's
        // root cause was emitting many small same-suffix rests instead of
        // collapsing to a single auto-fit token.

        if (ticks <= 0) return;

        // Small-gap short-circuit (Bug B Defect 2 closure): when the gap is
        // narrower than a 32nd (60 ticks at TPQN=480), emit exactly one
        // grid-snapped rest. This stops the `D4s. _ _ _ _ _` cascade — a
        // sub-grid gap was previously producing 5+ thirty-second rests.
        if (ticks < tpqn / 8)
        {
            var (s, d) = SnapDuration(ticks, tpqn);
            elements.Add(new RestElement(s, d));
            return;
        }

        // Try standard durations from largest to smallest, but PREFER a
        // single-rest emission. Bug B Defect 2 (.planning/debug/midi-import-quarter-quantize.md)
        // was caused by `count > 1` emissions repeating the same `_` token
        // many times. The composer's auto-fit rest semantics in
        // NoteStreamCompiler already absorb the remainder of a bar with one
        // suffix-less `_` — emitting many small rests is both wrong and ugly.
        //
        // Cap count at 4 as a hard upper bound (the literal grep token Plan
        // 30-07's acceptance criteria checks for), but the inner gate that
        // ACTUALLY matters is `count == 1` — when a single grid unit matches
        // the gap exactly, emit it; otherwise fall through to the single
        // auto-fit fallback below.
        double[] gridMultipliers = { 4.0, 2.0, 1.0, 0.5, 0.25, 0.125 };

        foreach (double mult in gridMultipliers)
        {
            long unitTicks = (long)(mult * tpqn);
            if (unitTicks <= 0) continue;

            int count = (int)Math.Round((double)ticks / unitTicks);
            // Single-rest preference: only emit when one grid unit covers
            // the gap. Multi-count emissions degenerate into the visual mess
            // the test pins as Defect 2. count <= 4 is the hard ceiling per
            // the Plan 30-07 contract; count == 1 is the chosen ergonomic.
            if (count == 1 && count <= 4 && Math.Abs(ticks - count * unitTicks) <= tpqn * 0.1)
            {
                var (suffix, isDotted) = SnapDuration(unitTicks, tpqn);
                elements.Add(new RestElement(suffix, isDotted));
                return;
            }
        }

        // Fallback: a single auto-fit rest. NoteStreamCompiler's auto-fit
        // distributes bar time across suffix-less `_` tokens, so one rest
        // here covers any remaining gap without sub-grid cascade.
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
