using FlowLang.Core;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Phase 29: brass now delegates to SampledInstrumentRenderer with the bundled
/// CC0 brass samples (A3, A4, A5 — single mezzo-forte velocity layer). The hand-rolled
/// sawtooth-plus-octave-up synthesis is replaced by sample-based playback with
/// linear amplitude scaling by note.Velocity. The Phase 28 articulation envelope
/// applies on top of the sample.
///
/// Fallback: silent if CurrentSampleCache is null (test-isolation path).
/// </summary>
public class BrassSynthesizer : INoteSynthesizer
{
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        var cache = FlowEngine.CurrentSampleCache;
        if (cache == null || !cache.HasInstrument("brass"))
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        var renderer = new SampledInstrumentRenderer(cache, "brass", hasVelocityLayers: false);
        return renderer.Render(note, sampleRate, durationBeats, bpm, tuning);
    }
}
