using Flow.Studio.Model;
using FlowLang.Music;
using FlowLang.StandardLibrary.Audio.Tuning;

namespace FlowLang.Hosting;

public static class GeneratorTuning
{
    /// <summary>Resolve on the control/preparation side. MIDI has no enharmonic
    /// spelling, so chromatic keys use sharps, matching recorded note spelling.</summary>
    public static Flow.Music.Model.MidiPitchMap ResolveMidi(ProjectTuning tuning)
    {
        var context = new MusicalContext { Key = tuning.Key };
        context.TuningStack.Push(Resolve(tuning));
        var resolved = FlowLang.StandardLibrary.Audio.SongRenderer.ResolveRenderTuning(context);
        const string letters = "CCDDEFFGGAAB";
        return new(Enumerable.Range(0, 128).Select(key =>
            FlowLang.StandardLibrary.Audio.PitchConversion.NoteToFrequency(
                new FlowLang.TypeSystem.SpecialTypes.MusicalNoteData(letters[key % 12], key / 12 - 1,
                    key % 12 is 1 or 3 or 6 or 8 or 10 ? 1 : 0, null, false), resolved)));
    }

    public static void Validate(ProjectTuning tuning) => _ = Resolve(tuning);
    private static RenderTuning Resolve(ProjectTuning tuning)
    {
        ResolvedTuning? custom = null;
        if (tuning.Scala is { } text)
        {
            var scale = ScalaParser.Parse(text, "project-tuning.scl");
            custom = new(scale, tuning.KeyboardMap is { } map ? ScalaKbmParser.Parse(map, "project-tuning.kbm") : ScalaKbmParser.Default(scale));
        }
        return new(Enum.Parse<TuningSystem>(tuning.System), Mode.Major, 'C', 0, custom);
    }
    internal static void Install(FlowLang.Runtime.ExecutionContext context, ProjectTuning tuning)
    {
        context.SetCurrentFrameMusicalContext(new MusicalContext { Key = tuning.Key });
        context.SetFileScopeTuning(Resolve(tuning));
    }
    public static void Set(ProjectDocument document, ProjectTuning tuning)
    {
        document.Edit("Change project tuning", p => WithTuning(p, tuning));
    }
    public static ProjectSnapshot WithTuning(ProjectSnapshot p, ProjectTuning tuning)
    {
        Validate(tuning);
        return p.Context.Tuning == tuning ? p : new(p.Arrangement,
            new(checked(p.Context.Revision + 1), p.Context.Seed, p.Context.Tempo, p.Context.Meter, p.Context.Parameters, tuning),
            p.Sources.Values, p.Routing, p.Assets, p.Automation, p.RenderSettings);
    }
}
