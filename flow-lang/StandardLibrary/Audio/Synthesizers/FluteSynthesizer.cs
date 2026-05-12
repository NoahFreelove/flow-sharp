using FlowLang.Core;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Phase 29: flute now delegates to SampledInstrumentRenderer with the bundled
/// CC0 flute samples (G4, G5 — single mf velocity layer).
/// Fallback: silent if CurrentSampleCache is null.
/// </summary>
public class FluteSynthesizer : INoteSynthesizer
{
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        var cache = FlowEngine.CurrentSampleCache;
        if (cache == null || !cache.HasInstrument("flute"))
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        var renderer = new SampledInstrumentRenderer(cache, "flute", hasVelocityLayers: false);
        return renderer.Render(note, sampleRate, durationBeats, bpm, tuning);
    }
}
