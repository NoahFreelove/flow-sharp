using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Sfz;

/// <summary>
/// Phase 33 — per-FlowEngine cache for SFZ patches.
/// Mirrors Phase 29's <see cref="SampleCache"/> shape exactly: raw + shifted
/// per-region buffer dictionaries, idempotent eager-load keyed by
/// <c>(patch, song)</c>, and a sorted-iteration discipline at eager-load time
/// to preserve the Phase 18 / 25 / 27 two-run byte-identical contract
/// (Pitfall 5).
///
/// Lifetime: same as the owning <c>FlowEngine</c>. Plan 33-07 wires
/// <c>FlowEngine.CurrentSfzSampleCache</c> and registers the eager-load
/// callback alongside the existing <c>FlowEngine.CurrentSampleCache</c>
/// surface; this plan ships the cache in isolation.
///
/// Storage model:
///   * <see cref="_rawCache"/> holds the unmodified WAV buffers loaded from
///     disk, keyed by <c>(SfzData patch, string samplePath)</c>. The patch
///     reference acts as a per-patch namespace so two patches that happen
///     to reference the same on-disk WAV path still get distinct cache
///     entries (correct under D-10's last-bound-wins reassign semantics).
///   * <see cref="_shiftedCache"/> memoizes the result of varispeed-shifting
///     each raw sample by a specific semitone offset so the per-note shift
///     cost is paid once per unique <c>(patch, samplePath, shift)</c> triple
///     instead of per note.
///
/// Determinism (Pitfall 5 / 33-PATTERNS.md): the eager-load region walk
/// collects distinct regions into a <c>HashSet&lt;SfzRegion&gt;</c> (whose
/// iteration order is implementation-defined in .NET), then wraps the set in
/// <c>.OrderBy(r => r.SamplePath, StringComparer.Ordinal)
/// .ThenBy(r => r.PitchKeycenter)</c> before iterating the WAV-load loop.
/// This is the locked Phase 29 precedent — same pattern as
/// <see cref="SampleCache"/>'s manifest iteration at lines 87-92 of
/// <c>SampleCache.cs</c>.
/// </summary>
public class SfzSampleCache
{
    // Raw samples loaded from disk (no varispeed shift).
    private readonly Dictionary<(SfzData patch, string samplePath), AudioBuffer> _rawCache = new();

    // Varispeed-shifted samples (cached on first access per (patch, samplePath, shift) triple).
    private readonly Dictionary<(SfzData patch, string samplePath, int semitonesShift), AudioBuffer> _shiftedCache = new();

    // Keys are "sfz:{patch.GetHashCode()}:{song.GetHashCode()}" — guards
    // EagerLoad against redundant rescans of the same (song, patch) pair
    // within an engine's lifetime (mirrors SampleCache.cs:77 idempotency pattern).
    private readonly HashSet<string> _eagerLoadedKeys = new();

    /// <summary>
    /// Returns the raw (unshifted) buffer cached for the given
    /// <paramref name="patch"/> + <paramref name="samplePath"/>, or null if
    /// the sample hasn't been eager-loaded yet (caller's responsibility to
    /// handle the missing-sample case — typically via a silence fallback +
    /// <c>RenderingDiagnostics.WarnOnce</c> advisory).
    /// </summary>
    public AudioBuffer? GetSample(SfzData patch, string samplePath)
    {
        return _rawCache.TryGetValue((patch, samplePath), out var buf) ? buf : null;
    }

    /// <summary>
    /// Returns the buffer for <paramref name="samplePath"/> in
    /// <paramref name="patch"/>, varispeed-shifted by
    /// <paramref name="semitonesShift"/> semitones. Memoizes the shifted
    /// result so repeated notes at the same pitch reuse the cached buffer.
    /// Returns null when no raw sample is loaded for the requested key.
    ///
    /// <paramref name="semitonesShift"/> == 0 short-circuits to the raw
    /// buffer (no resample work; identical reference).
    /// </summary>
    public AudioBuffer? GetVarispeed(SfzData patch, string samplePath, int semitonesShift)
    {
        var shiftedKey = (patch, samplePath, semitonesShift);
        if (_shiftedCache.TryGetValue(shiftedKey, out var cached)) return cached;

        var rawKey = (patch, samplePath);
        if (!_rawCache.TryGetValue(rawKey, out var raw)) return null;

        var shifted = semitonesShift == 0
            ? raw
            : FileIO.VarispeedResample(raw, Math.Pow(2.0, semitonesShift / 12.0));
        _shiftedCache[shiftedKey] = shifted;
        return shifted;
    }

    /// <summary>
    /// Walk every note of <paramref name="song"/>, dereference
    /// <c>patch.Grid[midi, vel]</c> for each <c>(pitch, velocity)</c> cell,
    /// collect the set of distinct regions actually needed, and load every
    /// such region's <c>.wav</c> file into the raw cache. Idempotent for the
    /// same <c>(song, patch)</c> pair within an engine lifetime — mirrors
    /// <see cref="SampleCache.EagerLoad"/>'s D-13/14/15 contract.
    ///
    /// Iteration order is sorted lexicographically by
    /// <c>(SamplePath ordinal, PitchKeycenter ascending)</c> per Pitfall 5
    /// — required to preserve the two-run byte-identical determinism
    /// contract across Phase 18 / 25 / 27.
    /// </summary>
    public void EagerLoad(SongData song, SfzData patch)
    {
        if (song is null || patch is null) return;

        // Idempotency key — both refs combined so a script that calls
        // renderSong on the same (song, patch) pair twice does NOT re-walk
        // the song or re-open the WAVs.
        string key = $"sfz:{patch.GetHashCode()}:{song.GetHashCode()}";
        if (_eagerLoadedKeys.Contains(key)) return;

        // Walk the song → sections → sequences → bars → notes, collecting
        // distinct regions actually referenced by the song's note set. The
        // 128×128 grid is dereferenced per note's clamped (midi, vel) cell.
        var needed = new HashSet<SfzRegion>();
        foreach (var sectionRef in song.Sections)
        {
            if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var section))
                continue;
            foreach (var sequenceEntry in section.Sequences)
            {
                var sequence = sequenceEntry.Value;
                foreach (var bar in sequence.Bars)
                {
                    CollectRegionsFromBar(bar, patch, needed);
                }
            }
        }

        // Pitfall 5: HashSet<T> iteration order is implementation-defined in
        // .NET — wrap in a sorted enumeration BEFORE the load loop so the
        // file-load order is identical across runs.
        var sorted = needed
            .OrderBy(r => r.SamplePath, StringComparer.Ordinal)
            .ThenBy(r => r.PitchKeycenter);

        foreach (var region in sorted)
        {
            var rawKey = (patch, region.SamplePath);
            if (_rawCache.ContainsKey(rawKey)) continue;

            string fullPath = Path.Combine(patch.BasePath, region.SamplePath);
            if (File.Exists(fullPath))
            {
                _rawCache[rawKey] = FileIO.LoadWavInternal(fullPath);
            }
            // If file missing, silently skip — render-time will surface the
            // missing-region case via the charitable WarnOnce + silence path
            // (Pattern 4 step (d) in 33-RESEARCH.md).
        }

        _eagerLoadedKeys.Add(key);
    }

    /// <summary>
    /// Walk a bar's notes (and any parallel voice sub-bars) and add the
    /// dereferenced <c>patch.Grid[midi, vel]</c> region for each note to
    /// <paramref name="needed"/>. Non-rest notes only — rests have no
    /// region. Velocity is clamped to <c>[1, 127]</c> per Pitfall 9
    /// (matches <c>SfzRenderer.Render</c>'s clamp so the eager-load and
    /// render-time lookup hit the same cell).
    /// </summary>
    private static void CollectRegionsFromBar(BarData bar, SfzData patch, HashSet<SfzRegion> needed)
    {
        foreach (var note in bar.MusicalNotes)
        {
            if (note.IsRest) continue;
            int midi = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);
            if (midi < 0 || midi > 127) continue;
            int vel = Math.Clamp((int)Math.Round(note.Velocity * 127.0), 1, 127);
            var region = patch.Grid[midi, vel];
            if (region is not null) needed.Add(region);
            // null cells are fine — render-time nearest-pitch fallback will
            // load the fallback region's WAV on first render.
        }

        // Phase 28 voice blocks: recurse into parallel sub-bars so polyphonic
        // voices also contribute their regions to the eager-load set.
        if (bar.ParallelVoices is not null)
        {
            foreach (var voice in bar.ParallelVoices)
            {
                CollectRegionsFromBar(voice, patch, needed);
            }
        }
    }

    /// <summary>
    /// Diagnostic: number of raw WAV buffers loaded into the cache. Used by
    /// tests to verify eager-load completed.
    /// </summary>
    public int RawSampleCount => _rawCache.Count;

    /// <summary>
    /// Diagnostic: number of (patch, samplePath, shift) entries in the
    /// shifted cache. Used by tests to verify varispeed memoization works.
    /// </summary>
    public int ShiftedSampleCount => _shiftedCache.Count;
}
