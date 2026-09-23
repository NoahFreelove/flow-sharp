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
        var catalog = TypeCatalog.Default;
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
