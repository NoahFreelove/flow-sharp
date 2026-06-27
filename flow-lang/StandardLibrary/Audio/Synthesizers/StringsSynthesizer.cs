using FlowLang.Core;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Phase 29: strings now delegate to SampledInstrumentRenderer with the bundled
/// CC0 violin / strings samples (D3, D4, D5 — single mf velocity layer).
/// Fallback: silent if CurrentSampleCache is null.
/// </summary>
public class StringsSynthesizer : INoteSynthesizer
{
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        var cache = FlowEngine.CurrentSampleCache;
        if (cache == null || !cache.HasInstrument("strings"))
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        var renderer = new SampledInstrumentRenderer(cache, "strings", hasVelocityLayers: false);
        return renderer.Render(note, sampleRate, durationBeats, bpm, tuning);
    }
}
