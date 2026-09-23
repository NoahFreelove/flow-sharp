using FlowLang.Ast;
using FlowLang.Ast.Elements;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Interpreter;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Music;

/// <summary>
/// Binds Flow's music grammar to the music library: what note streams, chords, songs,
/// sections, context blocks, unit literals, music members and music patterns mean.
/// A host that wants music installs these on its execution context.
/// </summary>
public static class MusicBindings
{
    /// <summary>Adds the music bindings to <paramref name="bindings"/> and returns it.</summary>
    public static DomainBindings Install(DomainBindings bindings) => bindings
        .LiteralParser(MusicExpressions.ParseLiteral)
        .Constant(ResolveConstant)
        .Expression<ChordLiteralExpression>((e, ev) => new MusicExpressions(ev).EvaluateChordLiteral(e))
        .Expression<BeatLiteralExpression>((e, ev) => new MusicExpressions(ev).EvaluateBeatLiteral(e))
        .Expression<NoteStreamExpression>((e, ev) => new MusicExpressions(ev).EvaluateNoteStream(e))
        .Expression<ProgressionExpression>((e, ev) => new MusicExpressions(ev).EvaluateProgression(e))
        .Expression<SongExpression>((e, ev) => new MusicExpressions(ev).EvaluateSong(e))
        .Statement<MusicalContextStatement>((s, i) => new MusicStatements(i).ExecuteMusicalContext(s))
        .Statement<TuningContextStatement>((s, i) => new MusicStatements(i).ExecuteTuningContext(s))
        .Statement<LiveBlockStatement>((s, i) => new MusicStatements(i).ExecuteLiveBlock(s))
        .Statement<SectionDeclaration>((s, i) => new MusicStatements(i).ExecuteSectionDeclaration(s))
        .Members(ResolveMember)
        .DefaultValue(CreateDefault)
        .DeclarationObserver(OnDeclared)
        .LiteralPattern(MatchLiteralPattern)
        .ConstructorPattern(MatchConstructorPattern);

    /// <summary>A new binding set with only the music bindings.</summary>
    public static DomainBindings Create() => Install(new DomainBindings());

    // 0615 bare-notevalues — a bare e/q/h/w/s (+ t/x/y) in expression position is a
    // NoteValue constant. The language consults this only after variable and function
    // lookup, so `Int e = 5` (or a proc param named `q`) shadows the constant.
    private static Value? ResolveConstant(string name) =>
        NoteValueType.TryGetPredefinedConstant(name, out var noteValue) ? MusicValue.NoteValue((int)noteValue) : null;

    private static bool ResolveMember(Value obj, string memberName, out Value? value)
    {
        switch (obj.Data)
        {
            case StandardLibrary.Audio.Voice voice:
                value = memberName switch
                {
                    "OffsetBeats" => Value.Double(voice.OffsetBeats),
                    "Gain" => Value.Double(voice.Gain),
                    "Pan" => Value.Double(voice.Pan),
                    _ => null,
                };
                return true;
            case StandardLibrary.Audio.Track track:
                value = memberName switch
                {
                    "SampleRate" => Value.Int(track.SampleRate),
                    "Channels" => Value.Int(track.Channels),
                    "OffsetBeats" => Value.Double(track.OffsetBeats),
                    "Gain" => Value.Double(track.Gain),
                    "Pan" => Value.Double(track.Pan),
                    _ => null,
                };
                return true;
            case ChordData chordData:
                value = memberName switch
                {
                    "Root" => Value.String(chordData.Root),
                    "Quality" => Value.String(chordData.Quality),
                    "Octave" => Value.Int(chordData.Octave),
                    "NoteNames" => Value.Array(
                        chordData.NoteNames.Select(n => Value.String(n)).ToArray(),
                        StringType.Instance),
                    _ => null,
                };
                return true;
            case BarData barData:
                value = memberName switch
                {
                    "TimeSignature" => MusicValue.TimeSignature(barData.TimeSignature),
                    "Count" => Value.Int(barData.Notes.Count),
                    _ => null,
                };
                return true;
            case SectionData sectionData:
                value = memberName switch
                {
                    "Name" => Value.String(sectionData.Name),
                    "SequenceCount" => Value.Int(sectionData.Sequences.Count),
                    _ => null,
                };
                return true;
            case SongData songData:
                value = memberName switch
                {
                    "SectionCount" => Value.Int(songData.Sections.Count),
                    _ => null,
                };
                return true;
            default:
                value = null;
                return false;
        }
    }

    private static Value? CreateDefault(FlowType type) => type switch
    {
        BufferType => MusicValue.EmptyBuffer(),
        NoteType => MusicValue.Note("C4"),
        SemitoneType => MusicValue.Semitone(0),
        CentType => MusicValue.Cent(0.0),
        MillisecondType => MusicValue.Millisecond(0.0),
        SecondType => MusicValue.Second(0.0),
        DecibelType => MusicValue.Decibel(0.0),
        BeatType => MusicValue.Beat(0.0),
        NoteValueType => MusicValue.NoteValue(0),
        TimeSignatureType => MusicValue.TimeSignature(new TimeSignatureData(4, 4)),
        SequenceType => MusicValue.Sequence(new SequenceData()),
        BarType => MusicValue.Bar(new BarData(new List<MusicalNoteData>(), new TimeSignatureData(4, 4))),
        _ => null,
    };

    private static void OnDeclared(Runtime.ExecutionContext context, string name, Value value, FlowType declaredType)
    {
#if !FLOW_WEB
        // Phase 33 D-12: a typed Sfz binding registers its patch so
        // `renderSong song "sampler:violin"` finds it by name (last binding wins).
        // Stripped on Web with the SFZ surface (D-47-08).
        if (declaredType is SfzType && value.Data is StandardLibrary.Audio.Sfz.SfzData sfzData)
            context.SfzPatchRegistry[name] = sfzData;
#endif
    }

    // sweep-0614: a note-literal pattern (`| C4 => ...`) carries the note text; compare
    // it as a Note so the verbatim note-text equality applies.
    private static bool? MatchLiteralPattern(object payload, Value scrutinee) =>
        scrutinee.Type is NoteType && payload is string noteText
            ? Utils.LooseEquals(scrutinee, MusicValue.Note(noteText))
            : null;

    private static bool? MatchConstructorPattern(
        Ast.Patterns.ConstructorPattern ctor, Value scrutinee, Runtime.ExecutionContext context)
    {
        if (ctor.IsChordLiteral)
            return MusicPatterns.MatchChordQuality(ctor.Name, scrutinee);
        if (ctor.IsRomanNumeral)
            return MusicPatterns.MatchRomanNumeral(ctor.Name, scrutinee, context);
        if (ctor.IsArticulationSymbol)
            return MusicPatterns.MatchArticulation(ctor.Name, scrutinee);
        return null;
    }
}
