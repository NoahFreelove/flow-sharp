using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Factories for music, audio and device values. These lived on <see cref="Value"/>;
/// they belong to the music layer so the language's value type names no music type.
/// </summary>
public static class MusicValue
{
    public static Value Buffer(object? value = null) => new Value(value, BufferType.Instance);
    /// <summary>An empty stereo 44.1 kHz buffer: the default value of the Buffer type.</summary>
    public static Value EmptyBuffer() => new Value(new StandardLibrary.Audio.AudioBuffer(0, 2, 44100), BufferType.Instance);
    public static Value Note(string value) => new Value(value, NoteType.Instance);
    public static Value Bar(BarData value) => new Value(value, BarType.Instance);
    public static Value Semitone(int value) => new Value(value, SemitoneType.Instance);
    public static Value Cent(double value) => new Value(value, CentType.Instance);
    public static Value Millisecond(double value) => new Value(value, MillisecondType.Instance);
    public static Value Second(double value) => new Value(value, SecondType.Instance);
    public static Value Decibel(double value) => new Value(value, DecibelType.Instance);
    public static Value Hertz(double value) => new Value(value, HertzType.Instance);
    public static Value OscillatorState(StandardLibrary.Audio.OscillatorState value) => new Value(value, OscillatorStateType.Instance);
    public static Value Envelope(StandardLibrary.Audio.Envelope value) => new Value(value, EnvelopeType.Instance);
    public static Value Beat(double value) => new Value(value, BeatType.Instance);
    public static Value Voice(StandardLibrary.Audio.Voice value) => new Value(value, VoiceType.Instance);
    public static Value Track(StandardLibrary.Audio.Track value) => new Value(value, TrackType.Instance);
    public static Value NoteValue(int enumValue) => new Value(enumValue, NoteValueType.Instance);
    public static Value TimeSignature(TimeSignatureData timeSig) => new Value(timeSig, TimeSignatureType.Instance);
    public static Value MusicalNote(MusicalNoteData note) => new Value(note, MusicalNoteType.Instance);
    public static Value Sequence(SequenceData sequence) => new Value(sequence, SequenceType.Instance);
    public static Value Chord(ChordData chord) => new Value(chord, ChordType.Instance);
    public static Value Section(SectionData section) => new Value(section, SectionType.Instance);
    public static Value Song(SongData song) => new Value(song, SongType.Instance);

    /// <summary>
    /// Phase 32 Plan 32-04 — wraps a <see cref="StandardLibrary.Audio.Tuning.ResolvedTuning"/>
    /// reference in a Flow <see cref="Value"/> typed as <see cref="TuningType.Instance"/>.
    /// Identity follows reference equality per CONTEXT D-* / Claude's Discretion: two
    /// <c>(loadScala "x.scl")</c> calls produce distinct Values even with identical
    /// content (Phase 32 doesn't cache per SPEC out-of-scope).
    /// </summary>
    public static Value Tuning(StandardLibrary.Audio.Tuning.ResolvedTuning resolved)
        => new Value(resolved, TuningType.Instance);

    /// <summary>
    /// Phase 33 Plan 33-02 — wraps a <see cref="StandardLibrary.Audio.Sfz.SfzData"/>
    /// reference in a Flow <see cref="Value"/> typed as <see cref="SfzType.Instance"/>.
    /// Identity follows reference equality per CONTEXT § "Claude's Discretion": two
    /// <c>(loadSfz #violin)</c> calls produce distinct Values even with identical
    /// resolved paths (Phase 33 doesn't cache at the value layer; mirrors Phase 32's
    /// <see cref="MusicValue.Tuning"/> contract).
    ///
    /// Phase 47 D-47-08: SfzData type stripped from Web build — this factory
    /// is unavailable there. Callers (SfzBuiltins.LoadSfzSymbol / LoadSfzString)
    /// are also stripped, so the missing factory is unreachable on Web.
    /// </summary>
#if !FLOW_WEB
    public static Value Sfz(StandardLibrary.Audio.Sfz.SfzData data)
        => new Value(data, SfzType.Instance);
#endif

    /// <summary>
    /// Phase 36 Plan 36-06 (GEN-01, D-36-06) — wraps a
    /// <see cref="MarkovModelData"/> reference in a Flow <see cref="Value"/>
    /// typed as <see cref="MarkovModelType.Instance"/>. Reference identity per
    /// CLAUDE.md Music Types Quick Reference (Pitfall 6 in
    /// <c>36-PATTERNS.md</c>): two <c>(markovTrain corpus order)</c> calls
    /// produce distinct Values even with identical training input. Composers
    /// who need structural compare use the dedicated <c>(markovEqual a b)</c>
    /// builtin. Mirrors the Phase 32 <see cref="MusicValue.Tuning"/> + Phase 33
    /// <see cref="MusicValue.Sfz"/> precedent.
    /// </summary>
    public static Value MarkovModel(MarkovModelData model)
        => new Value(model, MarkovModelType.Instance);

    /// <summary>
    /// Phase 36 Plan 36-07 (GEN-02, D-36-06 + D-36-08) — wraps an
    /// <see cref="LsystemModelData"/> reference in a Flow <see cref="Value"/>
    /// typed as <see cref="LsystemModelType.Instance"/>. Reference identity per
    /// CLAUDE.md Music Types Quick Reference (Pitfall 6 in
    /// <c>36-PATTERNS.md</c>): two <c>(lsystemModel axiom rules)</c> calls
    /// produce distinct Values even with identical axiom + rules input.
    /// Composers who need structural compare use the dedicated
    /// <c>(lsystemEqual a b)</c> builtin. Mirrors the Phase 32
    /// <see cref="MusicValue.Tuning"/> + Phase 33 <see cref="MusicValue.Sfz"/> + Plan
    /// 36-06 <see cref="MusicValue.MarkovModel"/> precedent.
    /// </summary>
    public static Value LsystemModel(LsystemModelData model)
        => new Value(model, LsystemModelType.Instance);

    /// <summary>
    /// Phase 38 Plan 38-06 (OSC-01, D-38-16) — wraps a
    /// <see cref="StandardLibrary.Network.OscHandleData"/> reference in a Flow
    /// <see cref="Value"/> typed as <see cref="OscHandleType.Instance"/>.
    /// Reference identity per CONTEXT D-38-16 + the Phase 32/33/36
    /// precedent: two <c>(oscListen 7777 "/x" h)</c> calls produce distinct
    /// Values even with identical port + path (each call spawns its own
    /// background receive loop and CancellationTokenSource — no caching at
    /// the value layer). Mirrors <see cref="MusicValue.Tuning"/> /
    /// <see cref="MusicValue.Sfz"/> / <see cref="MusicValue.MarkovModel"/> /
    /// <see cref="MusicValue.LsystemModel"/> contract.
    /// </summary>
#if !FLOW_WEB
    // Phase 47 D-47-08: OscHandleData type stripped from Web build (Plan 47-01).
    public static Value OscHandle(StandardLibrary.Network.OscHandleData handle)
        => new Value(handle, OscHandleType.Instance);

    /// <summary>
    /// Phase 40 MIDI-RT-01 (D-40-03) — wraps a
    /// <see cref="StandardLibrary.Midi.MidiDeviceData"/> as a reference-identity
    /// Flow <see cref="Value"/> typed <see cref="MidiDeviceType.Instance"/>.
    /// Two <c>(openMidiOutput "x")</c> calls produce distinct Values (no caching
    /// at the value layer). Mirrors <see cref="OscHandle"/>. <c>#if !FLOW_WEB</c>
    /// — MidiDeviceData + MidiDeviceType are stripped on Web (T-40-03).
    /// </summary>
    public static Value MidiDevice(StandardLibrary.Midi.MidiDeviceData handle)
        => new Value(handle, MidiDeviceType.Instance);

    /// <summary>
    /// Phase 40 CLOCK-01/02 (D-40-03) — wraps a
    /// <see cref="StandardLibrary.Midi.ClockHandleData"/> as a reference-identity
    /// Flow <see cref="Value"/> typed <see cref="ClockHandleType.Instance"/>.
    /// Returned by <c>(clockMaster MidiDevice)</c> + <c>(clockSlave String)</c>;
    /// consumed by <c>(clockStop ClockHandle)</c>. Two calls produce distinct
    /// Values (each spawns its own timing thread / listener loop — no caching at
    /// the value layer). Mirrors <see cref="OscHandle"/> / <see cref="MidiDevice"/>.
    /// <c>#if !FLOW_WEB</c> — ClockHandleData + ClockHandleType are stripped on
    /// Web (T-40-03).
    /// </summary>
    public static Value ClockHandle(StandardLibrary.Midi.ClockHandleData handle)
        => new Value(handle, ClockHandleType.Instance);

    /// <summary>
    /// Phase 40 JACK-01 (D-40-03 / D-40-05 best-effort) — wraps a
    /// <see cref="StandardLibrary.Midi.JackHandleData"/> as a reference-identity
    /// Flow <see cref="Value"/> typed <see cref="JackHandleType.Instance"/>.
    /// Returned by <c>(jackSync)</c> (opt-in <c>use "@jack"</c>). A handle is
    /// returned even when no JACK server is present (a charitable dead handle) so
    /// non-JACK workflows are never affected and <c>jackSync</c> never throws.
    /// <c>#if !FLOW_WEB</c> — JackHandleData + JackHandleType are stripped on Web
    /// (T-40-03).
    /// </summary>
    public static Value JackHandle(StandardLibrary.Midi.JackHandleData handle)
        => new Value(handle, JackHandleType.Instance);
#endif

}
