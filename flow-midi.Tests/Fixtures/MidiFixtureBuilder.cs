using FlowMidi.Midi;

namespace FlowMidi.Tests.Fixtures;

/// <summary>
/// Fluent in-memory builder for synthetic <see cref="MidiFile"/> instances
/// used by the Phase 30 / Bug B test suite.
///
/// All flow-midi MIDI record types are internal — this assembly reaches them
/// via the <c>InternalsVisibleTo("flow-midi.Tests")</c> attribute added to
/// flow-midi.csproj in Plan 30-06 Task 1.
///
/// Defaults: format 0, TPQN 480, single track. Calling Add*Event before
/// any explicit StartNewTrack() targets a default first track that is
/// created lazily on the first event.
/// </summary>
sealed class MidiFixtureBuilder
{
    int _format = 0;
    int _tpqn = 480;
    readonly List<TrackBuffer> _tracks = new();
    TrackBuffer? _current;

    sealed class TrackBuffer
    {
        public string? Name;
        public readonly List<MidiEvent> Events = new();
    }

    TrackBuffer CurrentTrack()
    {
        if (_current is null)
        {
            _current = new TrackBuffer();
            _tracks.Add(_current);
        }
        return _current;
    }

    public MidiFixtureBuilder WithFormat(int format)
    {
        _format = format;
        return this;
    }

    public MidiFixtureBuilder WithTpqn(int tpqn)
    {
        if (tpqn <= 0) throw new ArgumentOutOfRangeException(nameof(tpqn), "TPQN must be positive.");
        _tpqn = tpqn;
        return this;
    }

    public MidiFixtureBuilder WithTrackName(string name)
    {
        CurrentTrack().Name = name;
        return this;
    }

    public MidiFixtureBuilder StartNewTrack()
    {
        _current = new TrackBuffer();
        _tracks.Add(_current);
        return this;
    }

    public MidiFixtureBuilder AddTempoEvent(double bpm, long tick = 0)
    {
        if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm), "BPM must be positive.");
        int microsecondsPerBeat = (int)Math.Round(60_000_000.0 / bpm);
        CurrentTrack().Events.Add(new TempoEvent(tick, microsecondsPerBeat));
        return this;
    }

    public MidiFixtureBuilder AddTimeSignatureEvent(int numerator, int denominator, long tick = 0)
    {
        if (numerator <= 0) throw new ArgumentOutOfRangeException(nameof(numerator));
        if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
        CurrentTrack().Events.Add(new TimeSignatureEvent(tick, numerator, denominator));
        return this;
    }

    public MidiFixtureBuilder AddKeySignatureEvent(int sharpsFlats, bool isMinor, long tick = 0)
    {
        CurrentTrack().Events.Add(new KeySignatureEvent(tick, sharpsFlats, isMinor));
        return this;
    }

    /// <summary>
    /// Adds a matched NoteOn / NoteOff pair to the current track.
    /// </summary>
    public MidiFixtureBuilder AddNote(int channel, int pitch, long startTick, long endTick, int velocity = 100)
    {
        if (endTick <= startTick) throw new ArgumentException("endTick must be greater than startTick.", nameof(endTick));
        var t = CurrentTrack();
        t.Events.Add(new NoteOnEvent(startTick, channel, pitch, velocity));
        t.Events.Add(new NoteOffEvent(endTick, channel, pitch));
        return this;
    }

    /// <summary>
    /// Convenience: four contiguous quarter notes starting at tick 0 in 4/4 at the current TPQN.
    /// Pitches: <paramref name="pitch"/>, +2, +4, +5 — diatonic ascending (C-D-E-F shape).
    /// Each note occupies exactly one quarter at TPQN.
    /// </summary>
    public MidiFixtureBuilder AddFourQuarterNotes(int channel, int pitch)
    {
        long q = _tpqn;
        AddNote(channel, pitch,     0,     q);
        AddNote(channel, pitch + 2, q,     2 * q);
        AddNote(channel, pitch + 4, 2 * q, 3 * q);
        AddNote(channel, pitch + 5, 3 * q, 4 * q);
        return this;
    }

    /// <summary>
    /// Sorts each track's events by absolute tick (stable) and produces an immutable MidiFile.
    /// </summary>
    public MidiFile Build()
    {
        var midiTracks = new List<MidiTrack>(_tracks.Count);
        foreach (var t in _tracks)
        {
            // Stable sort: preserve insertion order within identical-tick events.
            var sorted = t.Events.OrderBy(e => e.AbsoluteTick).ToList();
            midiTracks.Add(new MidiTrack(t.Name, sorted));
        }
        return new MidiFile(_format, _tpqn, midiTracks);
    }
}
