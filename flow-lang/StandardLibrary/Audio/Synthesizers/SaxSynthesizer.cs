using FlowLang.Core;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Phase 29: sax now delegates to SampledInstrumentRenderer with the bundled
/// CC0 saxophone samples (F4, C5 — single mezzo-forte velocity layer).
/// Fallback: silent if CurrentSampleCache is null.
/// </summary>
public class SaxSynthesizer : INoteSynthesizer
{
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        var cache = FlowEngine.CurrentSampleCache;
        if (cache == null || !cache.HasInstrument("sax"))
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        var renderer = new SampledInstrumentRenderer(cache, "sax", hasVelocityLayers: false);
        return renderer.Render(note, sampleRate, durationBeats, bpm, tuning);
    }
}
