using FlowLang.Core;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Phase 29: bell now delegates to SampledInstrumentRenderer with the bundled
/// CC0 bell sample (C5 only — single mf velocity layer). Varispeed reach for
/// bell notes ranges up to ±12 semitones in worst case (see RESEARCH Pitfall 3 +
/// Open Question #2). Bell's inharmonic timbre is forgiving of varispeed shift.
/// Fallback: silent if CurrentSampleCache is null.
/// </summary>
public class BellSynthesizer : INoteSynthesizer
{
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        var cache = FlowEngine.CurrentSampleCache;
        if (cache == null || !cache.HasInstrument("bell"))
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        var renderer = new SampledInstrumentRenderer(cache, "bell", hasVelocityLayers: false);
        return renderer.Render(note, sampleRate, durationBeats, bpm, tuning);
    }
}
