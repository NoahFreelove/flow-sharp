using System.Runtime.CompilerServices;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Registers the music, audio and device types with <see cref="TypeCatalog.Default"/>
/// when this code is loaded. After the assembly split this becomes the music
/// assembly's own initializer, so a language-only host simply lacks these bindings.
/// </summary>
internal static class MusicTypeCatalog
{
    [ModuleInitializer]
    internal static void Register()
    {
        Runtime.ValueConversions.Register(MusicValueConversions.Convert);
        Runtime.ValueComparisons.RegisterNumericView(MusicValueConversions.NumericView);
        Runtime.ValueComparisons.RegisterCommonScale(MusicValueConversions.CommonScale);

        Runtime.ValueComparisons.RegisterOrdering((a, b) =>
        {
            if (a.Type is not NoteType || b.Type is not NoteType) return null;
            var (na, oa, aa) = NoteType.Parse(a.As<string>());
            var (nb, ob, ab) = NoteType.Parse(b.As<string>());
            return NoteType.ToMidiNote(na, oa, aa).CompareTo(NoteType.ToMidiNote(nb, ob, ab));
        });
        var catalog = TypeCatalog.Default;
        catalog.Register("DawResult", DawResultType.Instance);
        catalog.Register("DawAudio", DawAudioType.Instance);
        catalog.Register("DawNote", DawNoteType.Instance);
        catalog.Register("DawNoteSequence", DawNoteSequenceType.Instance);
        catalog.Register("DawTrack", DawTrackType.Instance);
        catalog.Register("DawClip", DawClipType.Instance);
        catalog.Register("DawProject", DawProjectType.Instance);
        catalog.Register("DawAutomation", DawAutomationType.Instance);
        catalog.Register("DawInstrument", DawInstrumentType.Instance);
        catalog.Register("AudioGraph", AudioGraphType.Instance);
        catalog.Register("Buffer", BufferType.Instance);
        catalog.Register("Note", NoteType.Instance);
        catalog.Register("Bar", BarType.Instance);
        catalog.Register("Semitone", SemitoneType.Instance);
        catalog.Register("Cent", CentType.Instance);
        catalog.Register("Millisecond", MillisecondType.Instance);
        catalog.Register("Second", SecondType.Instance);
        catalog.Register("Decibel", DecibelType.Instance);
        catalog.Register("Hertz", HertzType.Instance);
        catalog.Register("OscillatorState", OscillatorStateType.Instance);
        catalog.Register("Envelope", EnvelopeType.Instance);
        catalog.Register("Beat", BeatType.Instance);
        catalog.Register("Voice", VoiceType.Instance);
        catalog.Register("Track", TrackType.Instance);
        catalog.Register("NoteValue", NoteValueType.Instance);
        catalog.Register("TimeSignature", TimeSignatureType.Instance);
        catalog.Register("Sequence", SequenceType.Instance);
        catalog.Register("MusicalNote", MusicalNoteType.Instance);
        catalog.Register("Chord", ChordType.Instance);
        catalog.Register("Section", SectionType.Instance);
        catalog.Register("Song", SongType.Instance);
        catalog.Register("Tuning", TuningType.Instance);
        catalog.Register("Sfz", SfzType.Instance);
        catalog.Register("MarkovModel", MarkovModelType.Instance);
        catalog.Register("LsystemModel", LsystemModelType.Instance);
        catalog.Register("OscHandle", OscHandleType.Instance);
#if !FLOW_WEB
        catalog.Register("MidiDevice", MidiDeviceType.Instance);
        catalog.Register("ClockHandle", ClockHandleType.Instance);
        catalog.Register("JackHandle", JackHandleType.Instance);
#endif
    }
}
