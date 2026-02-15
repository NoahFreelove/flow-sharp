using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Harmony;

/// <summary>
/// Built-in functions for chord and harmony operations.
/// </summary>
public static class HarmonyFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        // str(Chord) -> String
        var strChordSignature = new FunctionSignature("str", [ChordType.Instance]);
        registry.Register("str", strChordSignature, args =>
        {
            var chord = args[0].As<ChordData>();
            return Value.String(chord.ToString());
        });

        // chordNotes(Chord) -> Strings
        var chordNotesSignature = new FunctionSignature("chordNotes", [ChordType.Instance]);
        registry.Register("chordNotes", chordNotesSignature, args =>
        {
            var chord = args[0].As<ChordData>();
            var notes = chord.NoteNames.Select(n => Value.String(n)).ToArray();
            return Value.Array(notes, StringType.Instance);
        });

        // chordRoot(Chord) -> String
        var chordRootSignature = new FunctionSignature("chordRoot", [ChordType.Instance]);
        registry.Register("chordRoot", chordRootSignature, args =>
        {
            var chord = args[0].As<ChordData>();
            return Value.String(chord.Root);
        });

        // chordQuality(Chord) -> String
        var chordQualitySignature = new FunctionSignature("chordQuality", [ChordType.Instance]);
        registry.Register("chordQuality", chordQualitySignature, args =>
        {
            var chord = args[0].As<ChordData>();
            return Value.String(chord.Quality);
        });

        // arpeggio(Chord, String) -> Sequence (up, down, updown)
        var arpeggioSignature = new FunctionSignature("arpeggio", [ChordType.Instance, StringType.Instance]);
        registry.Register("arpeggio", arpeggioSignature, args =>
        {
            var chord = args[0].As<ChordData>();
            var direction = args[1].As<string>();

            var noteNames = chord.NoteNames.ToList();

            switch (direction.ToLower())
            {
                case "down":
                    noteNames.Reverse();
                    break;
                case "updown":
                    var down = new List<string>(noteNames);
                    down.Reverse();
                    if (down.Count > 1) down = down.Skip(1).ToList();
                    noteNames.AddRange(down);
                    break;
                // "up" is default order
            }

            // Build a sequence with one bar containing the arpeggiated notes
            var musicalNotes = new List<MusicalNoteData>();
            foreach (var noteName in noteNames)
            {
                var (name, octave, alteration) = NoteType.Parse(noteName);
                musicalNotes.Add(new MusicalNoteData(name, octave, alteration,
                    (int)NoteValueType.Value.EIGHTH, isRest: false));
            }

            var timeSig = new TimeSignatureData(4, 4);
            var bar = new BarData(musicalNotes, timeSig);
            var sequence = new SequenceData();
            sequence.AddBar(bar);

            return Value.Sequence(sequence);
        });

        // scaleNotes(String) -> Strings
        var scaleNotesSignature = new FunctionSignature("scaleNotes", [StringType.Instance]);
        registry.Register("scaleNotes", scaleNotesSignature, args =>
        {
            var keyName = args[0].As<string>();
            var notes = ScaleDatabase.GetScaleNotes(keyName);
            if (notes == null)
                return Value.Array(Array.Empty<Value>(), StringType.Instance);
            return Value.Array(notes.Select(n => Value.String(n)).ToArray(), StringType.Instance);
        });

        // resolveNumeral(String, String) -> Chord
        var resolveNumeralSignature = new FunctionSignature("resolveNumeral",
            [StringType.Instance, StringType.Instance]);
        registry.Register("resolveNumeral", resolveNumeralSignature, args =>
        {
            var numeral = args[0].As<string>();
            var keyName = args[1].As<string>();
            var chordData = ScaleDatabase.ResolveRomanNumeral(numeral, keyName);
            if (chordData == null)
                return Value.Void();
            return Value.Chord(chordData);
        });

        // str(Section) -> String
        var strSectionSignature = new FunctionSignature("str", [SectionType.Instance]);
        registry.Register("str", strSectionSignature, args =>
        {
            var section = args[0].As<SectionData>();
            return Value.String(section.ToString());
        });

        // str(Song) -> String
        var strSongSignature = new FunctionSignature("str", [SongType.Instance]);
        registry.Register("str", strSongSignature, args =>
        {
            var song = args[0].As<SongData>();
            return Value.String(song.ToString());
        });

        // getSections(Song) -> Strings
        var getSectionsSignature = new FunctionSignature("getSections", [SongType.Instance]);
        registry.Register("getSections", getSectionsSignature, args =>
        {
            var song = args[0].As<SongData>();
            var names = song.Sections.Select(s => Value.String(s.Name)).ToArray();
            return Value.Array(names, StringType.Instance);
        });

        // sectionSequences(Section) -> Strings (returns names of sequences in section)
        var sectionSequencesSignature = new FunctionSignature("sectionSequences", [SectionType.Instance]);
        registry.Register("sectionSequences", sectionSequencesSignature, args =>
        {
            var section = args[0].As<SectionData>();
            var names = section.Sequences.Keys.Select(k => Value.String(k)).ToArray();
            return Value.Array(names, StringType.Instance);
        });
    }
}
