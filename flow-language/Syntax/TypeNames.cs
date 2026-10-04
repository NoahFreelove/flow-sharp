namespace FlowLang.Syntax;

/// <summary>
/// The type names Flow's grammar recognizes. Whether an identifier starts a
/// declaration (<c>Note n = C4</c>) is decided here, independently of which types a
/// host has installed, so the grammar is the same in every profile. What a name
/// means is looked up in <see cref="TypeSystem.TypeCatalog"/>.
/// </summary>
public static class TypeNames
{
    /// <summary>Identifier type names (keyword types such as <c>Int</c> are tokens).</summary>
    public static readonly IReadOnlySet<string> Named = new HashSet<string>(StringComparer.Ordinal)
    {
        "Buffer", "Note", "Bar", "Semitone", "Cent", "Millisecond", "Second", "Decibel", "Hertz",
        "OscillatorState", "Envelope", "Beat", "Voice", "Track", "NoteValue", "TimeSignature",
        "Sequence", "MusicalNote", "Chord", "Symbol", "Section", "Song", "Tuning", "Sfz",
        "MarkovModel", "LsystemModel", "OscHandle", "MidiDevice", "ClockHandle", "JackHandle",
        "Function", "AudioGraph", "DawInstrument", "DawAudio", "DawResult", "DawAutomation", "DawProject", "DawClip", "DawTrack", "DawNote", "DawNoteSequence",
    };

    /// <summary>Generic type constructors written with angle brackets or alone.</summary>
    public static readonly IReadOnlySet<string> Constructors = new HashSet<string>(StringComparer.Ordinal)
    {
        "Lazy", "Tuple", "Dict",
    };

    /// <summary>Singular names whose plural (<c>Notes</c>) is an array type annotation.</summary>
    public static readonly IReadOnlySet<string> Pluralizable = new HashSet<string>(StringComparer.Ordinal)
    {
        "Void", "Int", "Float", "Long", "Double", "String", "Bool", "Number", "Buf", "Buffer",
        "Note", "Bar", "Semitone", "Cent", "Millisecond", "Second", "Decibel", "Hertz",
        "OscillatorState", "Envelope", "Beat", "Voice", "Track", "NoteValue", "TimeSignature",
        "Sequence", "MusicalNote", "Chord", "Symbol", "Section", "Song", "Tuning", "Sfz",
        "MarkovModel", "LsystemModel", "OscHandle", "MidiDevice", "ClockHandle", "JackHandle",
        "Function", "AudioGraph", "DawInstrument", "DawAudio", "DawResult", "DawAutomation", "DawProject", "DawClip", "DawTrack", "DawNote", "DawNoteSequence",
    };

    /// <summary>
    /// Plurals that start a declaration at statement level. Narrower than
    /// <see cref="Pluralizable"/> (the handle and model types are only pluralizable
    /// inside annotations that already are declarations, such as parameters).
    /// </summary>
    public static readonly IReadOnlySet<string> PluralDeclarationStarters = new HashSet<string>(StringComparer.Ordinal)
    {
        "Void", "Int", "Float", "Long", "Double", "String", "Bool", "Number", "Buf", "Buffer",
        "Note", "Bar", "Semitone", "Cent", "Millisecond", "Second", "Decibel", "Hertz",
        "MusicalNote", "Function", "AudioGraph", "DawInstrument", "DawAudio", "DawResult", "DawAutomation", "DawProject", "DawClip", "DawTrack", "DawNote", "DawNoteSequence", "Chord", "Section", "Song", "OscillatorState", "Envelope",
        "Beat", "Voice", "Track", "NoteValue", "TimeSignature", "Sequence", "Symbol",
    };
}
