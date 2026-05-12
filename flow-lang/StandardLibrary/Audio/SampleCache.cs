using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Phase 29 — per-FlowEngine cache for bundled instrument samples.
/// Lifetime = engine lifetime (SPEC D-15). Eager-loads samples on renderSong entry.
/// Idempotent: repeated EagerLoad calls for the same (song, instrument) are no-ops.
///
/// Storage model:
///   * <see cref="_rawCache"/> holds the unmodified WAV buffers loaded from disk,
///     keyed by (instrument, sample-pitch MIDI number, velocity layer label).
///   * <see cref="_shiftedCache"/> memoizes the result of pitch-shifting each raw
///     sample by a specific semitone offset, so the per-render varispeed cost is
///     paid once per unique (sample, shift) pair instead of per note.
///
/// Determinism (Pitfall 5 / 29-PATTERNS.md): every iteration over the manifest's
/// pitches + velocities is performed on sorted copies (pitches ascending,
/// velocity labels ordinal-ascending) so the file-load order is identical on
/// every run — required to preserve the Phase 18 / 25 / 27 two-run byte-identical
/// determinism contract.
/// </summary>
public class SampleCache
{
    // Raw samples loaded from disk (no varispeed shift)
    private readonly Dictionary<(string instrument, int sampleMidi, string velocity), AudioBuffer> _rawCache = new();
    // Varispeed-shifted samples (cached on first access per (sample, shift) pair)
    private readonly Dictionary<(string instrument, int sampleMidi, string velocity, int shift), AudioBuffer> _shiftedCache = new();
    // Per-instrument list of which sample pitches are available (sorted ascending)
    private readonly Dictionary<string, List<int>> _availablePitches = new();

    private readonly string _samplesRoot;
    // Keys are "instrument:Song.GetHashCode().ToString()" — guards EagerLoad
    // against redundant rescans of the same (song, instrument) pair within an
    // engine's lifetime.
    private readonly HashSet<string> _eagerLoadedKeys = new();

    /// <summary>
    /// Per-instrument manifest of (sample pitches, velocity labels) shipping in the
    /// Phase 29 bundle. Pitches are MIDI numbers; velocity labels match the
    /// filename suffix convention used by Plan 29-01 (<c>C4_ff.wav</c>,
    /// <c>A4.wav</c>). Derived from SPEC D-09 pitch-coverage strategy.
    /// </summary>
    private static readonly Dictionary<string, (int[] pitches, string[] velocities)> InstrumentManifest = new()
    {
        // Piano: 5 pitches × pp/ff = 10 samples
        ["piano"] = (new[] { 36, 48, 60, 72, 84 }, new[] { "pp", "ff" }),  // C2, C3, C4, C5, C6
        ["brass"] = (new[] { 57, 69, 81 }, new[] { "mf" }),                 // A3, A4, A5 (single velocity)
        ["sax"] = (new[] { 65, 72 }, new[] { "mf" }),                       // F4, C5
        ["strings"] = (new[] { 50, 62, 74 }, new[] { "mf" }),               // D3, D4, D5
        ["flute"] = (new[] { 67, 79 }, new[] { "mf" }),                     // G4, G5
        ["bell"] = (new[] { 72 }, new[] { "mf" }),                          // C5
    };

    public SampleCache(string samplesRoot = "flow-lang/Samples")
    {
        _samplesRoot = samplesRoot;
    }

    /// <summary>
    /// Eager-load all samples needed for this song under the given top-level instrument.
    /// Idempotent for the same (song, instrument) pair within an engine lifetime.
    /// Walks the InstrumentManifest entry for the requested instrument and loads every
    /// shipped (pitch, velocity) WAV into the raw cache. Sorted lexicographically before
    /// load (Pitfall 5 — preserves two-run determinism).
    /// </summary>
    public void EagerLoad(SongData song, string instrument)
    {
        if (song is null) return;
        instrument = (instrument ?? string.Empty).ToLowerInvariant();
        // Idempotency key combines instrument with the song's identity (object hash) so
        // a second renderSong call on the same SongData skips re-walking the manifest.
        string key = $"{instrument}:{song.GetHashCode()}";
        if (_eagerLoadedKeys.Contains(key)) return;

        if (!InstrumentManifest.TryGetValue(instrument, out var manifest))
        {
            // Not a tonal instrument we sampled — skip (drums/organ/wavetable stay synthesized)
            _eagerLoadedKeys.Add(key);
            return;
        }

        // Sorted-ascending pitch list for nearest-neighbour lookup at render time.
        _availablePitches[instrument] = manifest.pitches.OrderBy(p => p).ToList();
        // Iterate sorted to keep file-load order deterministic across runs (Pitfall 5).
        foreach (var pitch in manifest.pitches.OrderBy(p => p))
        {
            foreach (var velocity in manifest.velocities.OrderBy(v => v, StringComparer.Ordinal))
            {
                var cacheKey = (instrument, pitch, velocity);
                if (_rawCache.ContainsKey(cacheKey)) continue;

                string filename = manifest.velocities.Length > 1
                    ? $"{MidiToPitchName(pitch)}_{velocity}.wav"
                    : $"{MidiToPitchName(pitch)}.wav";
                string path = Path.Combine(_samplesRoot, instrument, filename);
                if (File.Exists(path))
                {
                    var raw = FileIO.LoadWavInternal(path);
                    // Onset-align: skip leading silence before storing. Multi-velocity
                    // samples (piano pp/ff) can have different pre-strike silence
                    // durations in the source recording; without onset-trimming, the
                    // velocity-layer crossfade in SampledInstrumentRenderer maps the
                    // pp content against the ff *pre-strike silence* and the resulting
                    // mix collapses to near-silence whenever the velocity selects the
                    // ff side of the transition band (Plan 29-03 deviation Rule 2 —
                    // required for REQ-3 cosSim acceptance gate).
                    _rawCache[cacheKey] = TrimLeadingSilence(raw);
                }
                // If file missing, silently skip — render-time fallback to nearest available
                // (Plan 03 / 04 will surface a diagnostic if NO sample is ever found).
            }
        }

        _eagerLoadedKeys.Add(key);
    }

    /// <summary>
    /// Returns the available sample pitch closest to <paramref name="targetMidi"/> for
    /// <paramref name="instrument"/>. When the instrument has no loaded samples (e.g. the
    /// bundle isn't shipped or the manifest is empty), returns <paramref name="targetMidi"/>
    /// unchanged so callers can fall back to silence / synthesis.
    /// </summary>
    public int NearestSamplePitch(string instrument, int targetMidi)
    {
        instrument = (instrument ?? string.Empty).ToLowerInvariant();
        if (!_availablePitches.TryGetValue(instrument, out var pitches) || pitches.Count == 0)
            return targetMidi;  // fallback — caller handles missing-sample case
        int nearest = pitches[0];
        int bestDist = Math.Abs(targetMidi - nearest);
        foreach (var p in pitches)
        {
            int d = Math.Abs(targetMidi - p);
            if (d < bestDist) { nearest = p; bestDist = d; }
        }
        return nearest;
    }

    /// <summary>
    /// Returns the raw sample for (instrument, sampleMidi, velocity) varispeed-shifted
    /// by <paramref name="semitonesShift"/> semitones. Memoizes the shifted result so
    /// repeated notes at the same pitch reuse the cached buffer. Returns null when no
    /// raw sample is loaded for the requested key (caller falls back to silence).
    /// </summary>
    public AudioBuffer? GetVarispeed(string instrument, int sampleMidi, string velocity, int semitonesShift)
    {
        instrument = (instrument ?? string.Empty).ToLowerInvariant();
        var shiftedKey = (instrument, sampleMidi, velocity, semitonesShift);
        if (_shiftedCache.TryGetValue(shiftedKey, out var cached)) return cached;

        var rawKey = (instrument, sampleMidi, velocity);
        if (!_rawCache.TryGetValue(rawKey, out var raw)) return null;

        var shifted = semitonesShift == 0
            ? raw
            : FileIO.VarispeedResample(raw, Math.Pow(2.0, semitonesShift / 12.0));
        _shiftedCache[shiftedKey] = shifted;
        return shifted;
    }

    /// <summary>
    /// True if Phase 29 ships sample coverage for <paramref name="instrument"/>.
    /// </summary>
    public bool HasInstrument(string instrument) =>
        InstrumentManifest.ContainsKey((instrument ?? string.Empty).ToLowerInvariant());

    /// <summary>
    /// Diagnostic: number of raw samples loaded into the cache. Used by tests to
    /// verify eager-load completed.
    /// </summary>
    public int RawSampleCount => _rawCache.Count;

    /// <summary>
    /// Trim leading silence from a sample so the onset (first audible content)
    /// lands at frame 0. Threshold is set relative to the sample's own peak so
    /// quiet recordings (pp) don't have their entire body trimmed. The fixed
    /// floor (1e-4 absolute amplitude) is the bit-depth noise floor of a 16-bit
    /// WAV — values below that are quantization noise.
    ///
    /// Why onset-align? Multi-velocity sample sets recorded at different
    /// dynamics can have different pre-strike pad durations (the engineer
    /// trimmed each take to its own onset window). For velocity-layer crossfade
    /// to map velocity → mix coefficient consistently, both layers must start
    /// with their audible content at the same frame index.
    /// </summary>
    internal static AudioBuffer TrimLeadingSilence(AudioBuffer raw)
    {
        if (raw is null || raw.Frames == 0) return raw!;

        float peak = 0f;
        for (int i = 0; i < raw.Data.Length; i++)
        {
            float a = Math.Abs(raw.Data[i]);
            if (a > peak) peak = a;
        }
        if (peak <= 1e-9f) return raw; // entire sample silent — nothing to trim

        // Onset threshold: 5% of the sample's own peak, with an absolute floor
        // at 1e-4 (16-bit quantization noise). The 5% relative threshold catches
        // the first audible buildup without false-triggering on noise; the
        // absolute floor prevents quiet samples from triggering on noise tails.
        float thresh = Math.Max(peak * 0.05f, 1e-4f);
        int channels = raw.Channels;

        int onsetFrame = 0;
        for (int f = 0; f < raw.Frames; f++)
        {
            bool above = false;
            for (int c = 0; c < channels; c++)
            {
                if (Math.Abs(raw.Data[f * channels + c]) >= thresh)
                {
                    above = true;
                    break;
                }
            }
            if (above) { onsetFrame = f; break; }
        }
        if (onsetFrame == 0) return raw; // already onset-aligned

        int newFrames = raw.Frames - onsetFrame;
        var trimmed = new AudioBuffer(newFrames, channels, raw.SampleRate);
        Array.Copy(raw.Data, onsetFrame * channels, trimmed.Data, 0, newFrames * channels);
        return trimmed;
    }

    /// <summary>
    /// Convert a MIDI note number to the pitch-name + octave form used by the
    /// Phase 29 sample filenames (e.g. 60 → "C4", 36 → "C2", 81 → "A5", 67 → "G4").
    /// Phase 29 bundle only ships natural notes (C, A, D, F, G) so accidental
    /// labelling never fires — but the table is general.
    /// </summary>
    private static string MidiToPitchName(int midi)
    {
        string[] names = { "C", "Csharp", "D", "Dsharp", "E", "F", "Fsharp", "G", "Gsharp", "A", "Asharp", "B" };
        int noteIndex = ((midi % 12) + 12) % 12;
        int octave = (midi / 12) - 1;
        return $"{names[noteIndex]}{octave}";
    }
}
