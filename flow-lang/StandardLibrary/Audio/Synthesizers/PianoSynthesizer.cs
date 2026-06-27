using System.Threading;
using FlowLang.Core;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Phase 29 REQ-1: piano delegates to <see cref="SampledInstrumentRenderer"/>
/// with the bundled CC0 piano library. Phase 37 PIANO-01 (Plan 37-04 / D-37-09)
/// expands the bundled coverage to 4 velocity layers per pitch point — pp + mf + ff
/// loaded from disk, mp synthesized at eager-load via RmsInterpolate. The Phase 29
/// velocity-layer crossfade upgrades from 2-way (pp/ff) to 4-way (pp/mp/mf/ff)
/// inside <see cref="SampledInstrumentRenderer"/> automatically.
///
/// Phase 37 PIANO-01 (Plan 37-04 / D-37-11) adds the <c>release=</c> named-arg
/// knob threaded via <see cref="CurrentReleaseSec"/>. The
/// <c>renderSong(Song, String, Second)</c> overload in
/// <see cref="SongRenderer"/> sets this AsyncLocal before dispatching the render
/// so per-note <see cref="RenderNote"/> calls see the composer's chosen
/// release-tail length. Test parallelism stays safe (AsyncLocal isolates per
/// async-flow / per-xUnit-test).
///
/// Falls back to silence when <see cref="FlowEngine.CurrentSampleCache"/> is null
/// or the "piano" manifest entry is unavailable (graceful degradation outside an
/// engine). Phase 28 articulation envelope applies on top of the sample inside
/// <c>SampledInstrumentRenderer.Render</c>.
/// </summary>
public class PianoSynthesizer : INoteSynthesizer
{
    /// <summary>
    /// Phase 37 PIANO-01 (Plan 37-04) — per-render release-tail override (seconds).
    /// AsyncLocal so xUnit parallel runs don't bleed knob values across tests
    /// (same convention as <c>VoiceAllocator._lastPoolSizeUsedForTests</c>).
    /// When null, <see cref="SampledInstrumentRenderer.DefaultReleaseSec"/> (1.5s,
    /// D-37-11 lock) applies. Set by <c>SongRenderer.RenderSong</c>'s
    /// release-aware overload before per-note rendering begins; reset to null in
    /// the surrounding finally block to keep the AsyncLocal scope clean.
    /// </summary>
    public static AsyncLocal<double?> CurrentReleaseSec { get; } = new();

    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        var cache = FlowEngine.CurrentSampleCache;
        if (cache is null || !cache.HasInstrument("piano"))
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        var renderer = new SampledInstrumentRenderer(cache, "piano", hasVelocityLayers: true);
        double releaseSec = CurrentReleaseSec.Value ?? SampledInstrumentRenderer.DefaultReleaseSec;
        return renderer.Render(note, sampleRate, durationBeats, bpm, tuning, releaseSec);
    }
}
