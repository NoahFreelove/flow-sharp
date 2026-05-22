using FlowMidi.Midi;

namespace FlowMidi.Conversion;

/// <summary>
/// A note with start/end ticks, pitch, and velocity — derived from pairing note-on/off events.
/// <see cref="IsContinued"/> is set on fragments produced by cross-bar splitting:
/// when a span crosses a bar boundary, it's split into one fragment per bar, and all
/// fragments except the last are marked IsContinued so the emitter can mark them with
/// a tie (`~`). The BarRenderer's tie-sustain logic then absorbs the fragment's trailing
/// rests (within-bar) and crossfades into the next bar's continuation.
/// </summary>
record NoteSpan(long StartTick, long EndTick, int Pitch, int Velocity, bool IsContinued = false);

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

record ChordElement(List<string> NoteNames, string DurationSuffix, bool IsDotted, int Velocity, bool IsTied = false) : IBarElement
{
    public long DurationTicks(int tpqn) => Quantizer.SuffixToTicks(DurationSuffix, IsDotted, tpqn);
}

record RestElement(string DurationSuffix, bool IsDotted) : IBarElement
{
    public long DurationTicks(int tpqn) => Quantizer.SuffixToTicks(DurationSuffix, IsDotted, tpqn);
}

/// <summary>
/// A bar of quantized elements for one track. When <see cref="Voices"/> is non-null
/// the bar has overlapping polyphony and the FlowGenerator emits per-voice
/// `{voice ...}` blocks; otherwise <see cref="Elements"/> holds a flat single-voice
/// note stream.
/// </summary>
record QuantizedBar(List<IBarElement> Elements, int BarNumber, List<List<IBarElement>>? Voices = null);

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
        (4.0,    "w",  false),   // whole
        (3.0,    "h",  true),    // dotted half
        (2.0,    "h",  false),   // half
        (1.5,    "q",  true),    // dotted quarter
        (1.0,    "q",  false),   // quarter
        (0.75,   "e",  true),    // dotted eighth
        (0.5,    "e",  false),   // eighth
        (0.375,  "s",  true),    // dotted sixteenth
        (0.25,   "s",  false),   // sixteenth
        (0.1875, "t",  true),    // dotted 32nd
        (0.125,  "t",  false),   // thirty-second
        (0.09375,"x",  true),    // dotted 64th
        (0.0625, "x",  false),   // sixty-fourth
        (0.03125,"y",  false),   // 128th
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
            "x" => 0.0625,
            "y" => 0.03125,
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

        // Global time origin: earliest first-note tick across ALL tracks. Sharing
        // this across every emitted sequence keeps multi-voice / multi-track
        // emissions temporally aligned. Without it, each track's per-track
        // leading-trim aligns that track's first note to bar 0 of its sequence —
        // but the SongRenderer mixes sequences in parallel starting at section
        // bar 0, so tracks with later first notes would get pulled forward in
        // time relative to tracks that start earlier.
        long? globalFirstNoteTick = null;
        foreach (var track in midi.Tracks)
            foreach (var evt in track.Events)
                if (evt is NoteOnEvent on && (globalFirstNoteTick == null || on.AbsoluteTick < globalFirstNoteTick))
                    globalFirstNoteTick = on.AbsoluteTick;
        long barTicksGlobal = (long)(tpqn * timeSigNum * (4.0 / timeSigDen));
        int globalFirstBarIdx = globalFirstNoteTick.HasValue
            ? (int)(globalFirstNoteTick.Value / barTicksGlobal)
            : 0;

        if (midi.Format == 0)
        {
            // Format 0: single track, split by channel.
            if (midi.Tracks.Count > 0)
            {
                var byChannel = SplitByChannel(midi.Tracks[0]);
                foreach (var (channel, spans) in byChannel)
                {
                    bool isDrum = channel == 9;
                    string baseName = isDrum ? "drums" : $"track_ch{channel + 1}";
                    EmitTrackAsVoices(result, baseName, spans, tpqn, timeSigNum, timeSigDen, useFlats, channel, isDrum, globalFirstBarIdx);
                }
            }
        }
        else
        {
            // Format 1/2: each track is separate.
            int trackIndex = 0;
            foreach (var track in midi.Tracks)
            {
                var spans = PairNotes(track.Events);
                if (spans.Count == 0)
                {
                    trackIndex++;
                    continue;
                }

                bool isDrum = track.Events.OfType<NoteOnEvent>().Any(e => e.Channel == 9);
                string name = !string.IsNullOrWhiteSpace(track.Name) ? SanitizeName(track.Name) : $"track_{trackIndex + 1}";
                if (isDrum) name = "drums";

                int channel = track.Events.OfType<NoteOnEvent>().FirstOrDefault()?.Channel ?? 0;
                EmitTrackAsVoices(result, name, spans, tpqn, timeSigNum, timeSigDen, useFlats, channel, isDrum, globalFirstBarIdx);

                trackIndex++;
            }
        }

        return new QuantizeResult(result, timeSigNum, timeSigDen);
    }

    /// <summary>
    /// Emits one or more QuantizedTracks for a single MIDI track's spans:
    /// drum tracks pass through as one sequence (drums are intentionally
    /// percussive and don't need polyphonic splitting); melodic tracks get
    /// hand-split (treble/bass at middle C) and track-wide first-fit voice
    /// allocated per hand, producing N parallel sequences with stable musical
    /// voice identity across bars.
    /// </summary>
    static void EmitTrackAsVoices(List<QuantizedTrack> result, string baseName, List<NoteSpan> spans, int tpqn, int timeSigNum, int timeSigDen, bool useFlats, int channel, bool isDrum, int globalFirstBarIdx)
    {
        if (isDrum)
        {
            var bars = QuantizeSpans(spans, tpqn, timeSigNum, timeSigDen, useFlats, globalFirstBarIdx);
            result.Add(new QuantizedTrack(baseName, bars, channel, true));
            return;
        }

        var voices = AllocateVoicesTrackWide(spans, tpqn);
        foreach (var (voiceSuffix, voiceSpans) in voices)
        {
            if (voiceSpans.Count == 0) continue;
            var bars = QuantizeSpans(voiceSpans, tpqn, timeSigNum, timeSigDen, useFlats, globalFirstBarIdx);
            if (bars.Count == 0) continue;
            string trackName = $"{baseName}_{voiceSuffix}";
            result.Add(new QuantizedTrack(trackName, bars, channel, false));
        }
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
    static List<QuantizedBar> QuantizeSpans(List<NoteSpan> spans, int tpqn, int timeSigNum, int timeSigDen, bool useFlats, int? globalFirstBarIdx = null)
    {
        if (spans.Count == 0) return new List<QuantizedBar>();

        // Bar length in ticks
        long barTicks = (long)(tpqn * timeSigNum * (4.0 / timeSigDen));

        // Cross-bar splitting: any span crossing a bar boundary is split into per-bar
        // fragments, all but the last marked IsContinued so the emitter tags them
        // with `~`. Combined with the BarRenderer's tie-sustain semantic, this lets
        // a held note ring through the full duration even though Flow's note stream
        // is bar-bounded.
        spans = SplitSpansAtBars(spans, barTicks);

        long maxTick = spans.Max(s => s.EndTick);
        int totalBars = (int)((maxTick + barTicks - 1) / barTicks);

        // When a globalFirstBarIdx is provided (multi-sequence emission), use it so
        // all parallel sequences share the same time origin and stay aligned when
        // the renderer mixes them additively.
        long firstNoteTick = spans.Min(s => s.StartTick);
        int localFirstBarIdx = (int)(firstNoteTick / barTicks);
        int firstBarIdx = globalFirstBarIdx ?? localFirstBarIdx;

        var bars = new List<QuantizedBar>();

        for (int barIdx = firstBarIdx; barIdx < totalBars; barIdx++)
        {
            long barStart = barIdx * barTicks;
            long barEnd = barStart + barTicks;

            var barSpans = spans
                .Where(s => s.StartTick >= barStart && s.StartTick < barEnd)
                .OrderBy(s => s.StartTick)
                .ThenBy(s => s.Pitch)
                .ToList();

            if (barSpans.Count == 0)
            {
                // Silent bar — emit grid-decomposed full-bar rests so the renderer
                // sees exactly barTicks of silence. AddRests greedily decomposes
                // barTicks into appropriate rest tokens (one `w` for 4/4 etc.).
                var emptyElements = new List<IBarElement>();
                AddRests(emptyElements, barTicks, tpqn);
                bars.Add(new QuantizedBar(emptyElements, barIdx));
                continue;
            }

            // Per-bar chord-grouping then single-voice emission. True polyphony is
            // expressed at the Sequence level (one Sequence per voice in a section)
            // via the track-wide voice allocation in Quantize() — within a single
            // voice's spans, notes never overlap so a flat note stream suffices.
            var groups = GroupSimultaneous(barSpans, tpqn);
            var elements = EmitVoiceElements(groups, barStart, barEnd, tpqn, useFlats);
            bars.Add(new QuantizedBar(elements, barIdx));
        }

        // Trim trailing empty/rest-only bars
        while (bars.Count > 0 && bars[^1].Elements.All(e => e is RestElement))
            bars.RemoveAt(bars.Count - 1);

        return bars;
    }

    /// <summary>
    /// Splits raw MIDI spans into multiple per-voice span lists using hand-split
    /// (treble/bass at <paramref name="splitPitch"/>, default middle C = 60) followed
    /// by track-wide first-fit voice allocation. Chord-groups (simultaneous notes
    /// within <c>tpqn/8</c> tolerance) stay together so the per-bar emitter renders
    /// them as `[A B C]q` chords. Returns one inner list per voice; each voice's
    /// spans are guaranteed non-overlapping by construction, so the single-voice
    /// QuantizeSpans path produces correct timings without bar-fit truncation.
    ///
    /// Each returned voice persists ACROSS bars (musical voice identity preserved),
    /// unlike the abandoned per-bar voice-block approach where a melody line could
    /// migrate between voice indices bar-to-bar and cause re-attacks at every bar
    /// boundary.
    /// </summary>
    static List<(string HandSuffix, List<NoteSpan> Spans)> AllocateVoicesTrackWide(List<NoteSpan> spans, int tpqn, int splitPitch = 60)
    {
        var result = new List<(string, List<NoteSpan>)>();

        var rh = spans.Where(s => s.Pitch >= splitPitch).ToList();
        var lh = spans.Where(s => s.Pitch < splitPitch).ToList();

        foreach (var (handSuffix, handSpans) in new[] { ("rh", rh), ("lh", lh) })
        {
            if (handSpans.Count == 0) continue;

            // Track-wide chord-grouping (cross-bar). Tolerance set to JUST UNDER a
            // 64th note (tpqn/16 - 1) so genuine fast ornaments (trills,
            // grace-note ornaments — typically 24-48 ticks apart at TPQN=384) emit
            // as sequential 64th/32nd notes instead of being collapsed into chords.
            // Humanized chord onsets typically span <23 ticks (~30ms) — still
            // grouped. Trade-off: gaps in 24-95 ticks (the old "grace-note risk"
            // band) now emit as a 64th or 32nd offset, but with finer grid
            // resolution this is more musically accurate than merging.
            var sorted = handSpans.OrderBy(s => s.StartTick).ThenBy(s => s.Pitch).ToList();
            long tolerance = Math.Max(tpqn / 16 - 1, 1);
            var groups = new List<List<NoteSpan>>();
            foreach (var span in sorted)
            {
                // Continuation-fragment sentinels (Pitch < 0) never merge with real
                // notes — they're emitted as standalone rests so the bar's cursor
                // bookkeeping advances correctly without producing audible re-strikes.
                bool sameKind = groups.Count > 0
                    && (groups[^1][0].Pitch < 0) == (span.Pitch < 0);
                if (sameKind && Math.Abs(span.StartTick - groups[^1][0].StartTick) <= tolerance)
                    groups[^1].Add(span);
                else
                    groups.Add(new List<NoteSpan> { span });
            }

            // First-fit allocate groups to voices. Voice identity is stable across
            // the whole track — a held melody note's group anchors voice 1 for the
            // entire hand. Subsequent groups land on voice 1 only if voice 1 has
            // released; otherwise voice 2, etc.
            var voices = new List<List<List<NoteSpan>>>();
            var voiceEnds = new List<long>();
            foreach (var group in groups)
            {
                long groupStart = group[0].StartTick;
                long groupEnd = group.Max(s => s.EndTick);

                int assigned = -1;
                for (int v = 0; v < voices.Count; v++)
                {
                    if (voiceEnds[v] <= groupStart)
                    {
                        assigned = v;
                        break;
                    }
                }
                if (assigned == -1)
                {
                    voices.Add(new List<List<NoteSpan>>());
                    voiceEnds.Add(0);
                    assigned = voices.Count - 1;
                }
                voices[assigned].Add(group);
                voiceEnds[assigned] = Math.Max(voiceEnds[assigned], groupEnd);
            }

            // Flatten each voice's groups back to a span list. Per-bar emission
            // will re-group simultaneous spans via GroupSimultaneous.
            for (int v = 0; v < voices.Count; v++)
            {
                var voiceSpans = voices[v]
                    .SelectMany(g => g)
                    .OrderBy(s => s.StartTick)
                    .ThenBy(s => s.Pitch)
                    .ToList();
                string voiceName = voices.Count == 1 ? handSuffix : $"{handSuffix}_v{v + 1}";
                result.Add((voiceName, voiceSpans));
            }
        }

        return result;
    }

    /// <summary>
    /// Splits any span that crosses a bar boundary into per-bar fragments. Only the
    /// FIRST fragment carries the original pitch — subsequent fragments are flagged
    /// with a sentinel <see cref="NoteSpan.Pitch"/> of -1 so the per-bar emitter
    /// converts them to rests. Reasoning: the renderer's sustain pedal (always-on for
    /// the converter's piano output) keeps the first fragment's audio ringing for
    /// the full original MIDI duration via the buffer extension; emitting an audible
    /// re-strike at every subsequent bar boundary produces a phantom-attack effect
    /// (the composer hears it as a grace note 1-2 beats after the real attack)
    /// because Flow's note-stream syntax carries no per-note velocity, so the
    /// continuation always renders at default 0.63 velocity. Replacing the fragment
    /// with a rest keeps the bar duration math correct without producing the artifact.
    /// </summary>
    static List<NoteSpan> SplitSpansAtBars(List<NoteSpan> spans, long barTicks)
    {
        var result = new List<NoteSpan>();
        foreach (var span in spans)
        {
            long cursor = span.StartTick;
            bool isFirstFragment = true;
            while (cursor < span.EndTick)
            {
                long currentBarEnd = ((cursor / barTicks) + 1) * barTicks;
                long subEnd = Math.Min(span.EndTick, currentBarEnd);
                bool isContinued = subEnd < span.EndTick;

                if (isFirstFragment)
                {
                    result.Add(new NoteSpan(cursor, subEnd, span.Pitch, span.Velocity, isContinued));
                }
                else
                {
                    // Sentinel pitch -1 → emit as a rest of the same duration. Bookkeeping
                    // stays correct (bar sums to barTicks), but no audible re-attack.
                    result.Add(new NoteSpan(cursor, subEnd, -1, 0, isContinued));
                }
                cursor = subEnd;
                isFirstFragment = false;
            }
        }
        return result.OrderBy(s => s.StartTick).ThenBy(s => s.Pitch).ToList();
    }

    /// <summary>
    /// First-fit allocates a list of simultaneous-note groups (already chord-grouped)
    /// to voices. Each group is assigned to the first voice whose last group's end-tick
    /// is &lt;= this group's start-tick. A new voice is created when no existing voice
    /// is free. Returns one inner list per voice, each containing that voice's groups
    /// in onset order.
    /// </summary>
    static List<List<List<NoteSpan>>> AllocateGroupsToVoices(List<List<NoteSpan>> groups)
    {
        var voices = new List<List<List<NoteSpan>>>();
        var voiceEnds = new List<long>();

        foreach (var group in groups)
        {
            long groupStart = group[0].StartTick;
            long groupEnd = group.Max(s => s.EndTick);

            int assigned = -1;
            for (int v = 0; v < voices.Count; v++)
            {
                if (voiceEnds[v] <= groupStart)
                {
                    assigned = v;
                    break;
                }
            }
            if (assigned == -1)
            {
                voices.Add(new List<List<NoteSpan>>());
                voiceEnds.Add(0);
                assigned = voices.Count - 1;
            }
            voices[assigned].Add(group);
            voiceEnds[assigned] = Math.Max(voiceEnds[assigned], groupEnd);
        }

        return voices;
    }

    /// <summary>
    /// Renders a single voice's groups (chord-groups that are sequentially non-overlapping
    /// within this bar) into a list of <see cref="IBarElement"/>. Within a voice, the next
    /// group always starts at or after the previous group ends, so the bar-fit clamp
    /// becomes a no-op and the emitted note duration matches the snapped raw MIDI
    /// duration — preserving sustained-note durations the old single-stream emitter was
    /// forced to truncate.
    /// </summary>
    static List<IBarElement> EmitVoiceElements(List<List<NoteSpan>> groups, long barStart, long barEnd, int tpqn, bool useFlats)
    {
        var elements = new List<IBarElement>();
        long cursor = barStart;

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
                    long emittedRest = AddRests(elements, gap, tpqn);
                    // Advance cursor by what the renderer will actually play, not by
                    // the raw MIDI gap. AddRests may emit slightly less than the gap
                    // when the remainder is sub-32nd (≤ ~3% of a quarter). Using the
                    // emitted value keeps cursor and emitted .flow text in sync.
                    cursor += emittedRest;
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
                    // Single note (or continuation-fragment sentinel rendered as rest).
                    var span = group[0];
                    long rawDuration = span.EndTick - span.StartTick;
                    long capped = Math.Min(rawDuration, availableTicks);
                    var (suffix, isDotted) = SnapDurationCapped(capped, availableTicks, tpqn);
                    long snappedDuration = SuffixToTicks(suffix, isDotted, tpqn);

                    if (span.Pitch < 0)
                    {
                        // Continuation-fragment sentinel — emit as rest so the sustain
                        // pedal carries the original note without an audible re-strike.
                        elements.Add(new RestElement(suffix, isDotted));
                    }
                    else
                    {
                        bool tied = span.IsContinued || span.EndTick > barEnd;
                        string noteName = MidiPitchToFlowNote(span.Pitch, useFlats);
                        elements.Add(new NoteElement(noteName, suffix, isDotted, tied, span.Velocity));
                    }
                    cursor += snappedDuration;
                }
                else
                {
                    long maxDuration = group.Max(s => s.EndTick - s.StartTick);
                    long capped = Math.Min(maxDuration, availableTicks);
                    var (suffix, isDotted) = SnapDurationCapped(capped, availableTicks, tpqn);
                    long snappedDuration = SuffixToTicks(suffix, isDotted, tpqn);

                    // Filter out continuation sentinels (Pitch < 0) from chord notes.
                    var realNotes = group.Where(s => s.Pitch >= 0).ToList();
                    if (realNotes.Count == 0)
                    {
                        elements.Add(new RestElement(suffix, isDotted));
                    }
                    else
                    {
                        bool tied = realNotes.Any(s => s.IsContinued || s.EndTick > barEnd);
                        var noteNames = realNotes.Select(s => MidiPitchToFlowNote(s.Pitch, useFlats)).ToList();
                        int avgVelocity = (int)realNotes.Average(s => s.Velocity);
                        if (noteNames.Count == 1)
                            elements.Add(new NoteElement(noteNames[0], suffix, isDotted, tied, avgVelocity));
                        else
                            elements.Add(new ChordElement(noteNames, suffix, isDotted, avgVelocity, tied));
                    }
                    cursor += snappedDuration;
                }
            }

            // Fill remaining bar with rest. The bar-trailing fill doesn't affect
            // subsequent onset positioning (next bar starts fresh at its own
            // barStart), so emitting slightly less than the residue is harmless —
            // the bar simply ends a hair early; the next bar still begins on the
            // correct downbeat because BarType.ToTimeline resets per bar.
            if (cursor < barEnd)
            {
                long remaining = barEnd - cursor;
                AddRests(elements, remaining, tpqn);
            }

        return elements;
    }

    static List<List<NoteSpan>> GroupSimultaneous(List<NoteSpan> spans, int tpqn)
    {
        if (spans.Count == 0) return new List<List<NoteSpan>>();

        // Per-bar tolerance matches AllocateVoicesTrackWide (tpqn/16 - 1, just
        // under a 64th note) so chord detection stays consistent at both passes.
        long tolerance = Math.Max(tpqn / 16 - 1, 1);

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

    /// <summary>
    /// Per-unit rest grid (suffix, multiplier-of-TPQN, isDotted), ordered LARGEST first.
    /// Includes dotted forms so a 1.5-quarter gap snaps to `q.` rather than degrading
    /// to a misrepresented `q`. midi-voice-block-racing.md root cause: the old grid was
    /// `[w h q e s t]` only, so any dotted-duration gap fell through to a `q` fallback
    /// that shifted subsequent notes by up to a full beat once the FlowGenerator started
    /// emitting explicit-duration rests inside voice blocks.
    /// </summary>
    static readonly (string Suffix, double Mult, bool IsDotted)[] RestGrid =
    {
        ("w",  4.0,     false),
        ("h",  3.0,     true),    // h. = 3 quarters
        ("h",  2.0,     false),
        ("q",  1.5,     true),    // q. = 1.5 quarters
        ("q",  1.0,     false),
        ("e",  0.75,    true),    // e. = 0.75 quarters
        ("e",  0.5,     false),
        ("s",  0.375,   true),    // s. = 0.375 quarters
        ("s",  0.25,    false),
        ("t",  0.1875,  true),    // t. = 0.1875 quarters
        ("t",  0.125,   false),
        ("x",  0.09375, true),    // x. = 0.09375 quarters
        ("x",  0.0625,  false),
        ("y",  0.03125, false),
    };

    /// <summary>
    /// Decomposes <paramref name="ticks"/> into one or more grid-aligned RestElements
    /// and appends them to <paramref name="elements"/>. Returns the TOTAL tick count
    /// actually emitted (the sum of the RestElements' grid durations), which may
    /// differ from the input by up to half a 32nd note when the gap isn't grid-aligned.
    /// Callers must advance their cursor by the RETURNED value, not by the input
    /// <paramref name="ticks"/>, so cursor position stays consistent with what the
    /// renderer will actually play. Failing to do so was the root cause of the
    /// "racing" symptom in midi-voice-block-racing.md — emitted .flow text and
    /// internal cursor disagreed by an accumulating per-rest amount.
    /// </summary>
    static long AddRests(List<IBarElement> elements, long ticks, int tpqn)
    {
        if (ticks <= 0) return 0;

        long emitted = 0;
        long remaining = ticks;
        int safetyMax = 32;

        while (remaining > 0 && safetyMax-- > 0)
        {
            // Pick the largest grid unit that's ≤ remaining. No tolerance band here —
            // any overshoot would push emitted sum past the input gap and accumulate
            // forward-drift across many rests. Undershooting is fine (the loop emits
            // another smaller rest to cover the remainder).
            bool found = false;
            foreach (var (suffix, mult, isDotted) in RestGrid)
            {
                long unitTicks = (long)Math.Round(mult * tpqn);
                if (unitTicks <= 0) continue;
                if (unitTicks <= remaining)
                {
                    elements.Add(new RestElement(suffix, isDotted));
                    emitted += unitTicks;
                    remaining -= unitTicks;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Remaining is smaller than a 32nd note. Drop it — it's sub-grid
                // residue. The caller's cursor advances by `emitted`, not `ticks`,
                // so the next event's onset reflects the actually-emitted timing.
                // Sub-32nd residue is below human perception of timing.
                return emitted;
            }
        }

        return emitted;
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
