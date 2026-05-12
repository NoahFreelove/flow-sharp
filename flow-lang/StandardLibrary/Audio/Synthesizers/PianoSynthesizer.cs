using FlowLang.Core;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Phase 29 REQ-1: piano delegates to <see cref="SampledInstrumentRenderer"/>
/// with the bundled CC0 piano library (5 pitches × pp/ff). REQ-3 velocity-layer
/// crossfade between pp and ff drives timbre change with velocity (v=0 → pp,
/// v=1 → ff, linear mix in between). Phase 28 articulation envelope applies on
/// top of the sample inside <c>SampledInstrumentRenderer.Render</c>. Falls back
/// to silence when <see cref="FlowEngine.CurrentSampleCache"/> is null or the
/// "piano" manifest entry is unavailable (graceful degradation outside an engine).
/// </summary>
public class PianoSynthesizer : INoteSynthesizer
{
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        var cache = FlowEngine.CurrentSampleCache;
        if (cache is null || !cache.HasInstrument("piano"))
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        var renderer = new SampledInstrumentRenderer(cache, "piano", hasVelocityLayers: true);
        return renderer.Render(note, sampleRate, durationBeats, bpm, tuning);
    }
}
