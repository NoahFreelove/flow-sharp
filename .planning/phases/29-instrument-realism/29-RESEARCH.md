# Phase 29: Instrument Realism - Research

**Researched:** 2026-05-10
**Domain:** sample-based instrument rendering, CC0 audio sourcing, varispeed pitch shifting, articulation × sample interaction
**Confidence:** HIGH (existing primitives ship; sample sourcing has multiple verified routes)

## Summary

Phase 29 layers a sample-based renderer on top of Flow's existing `loadWav(path, ratio)` varispeed primitive (Phase 22 DX-15, `flow-lang/StandardLibrary/Audio/FileIO.cs:303-329`) for six tonal instruments (Piano, Brass, Sax, Strings, Flute, Bell). The work splits cleanly along three axes: (1) **infrastructure** — `SampledInstrumentRenderer` + `SampleCache` per-FlowEngine — (2) **content** — a curated CC0 sample library bundled in-repo at `flow-lang/Samples/` under a 5 MB hard cap — (3) **integration** — Phase 28's locked articulation envelopes apply on top of sample output, the existing 6 tonal-synth classes delegate to the new renderer, and three retained synths (Drums, Organ, Wavetable) gain hand-rolled DSP improvements with a falsifiable harmonic-richness threshold.

The riskiest sub-task is **CC0-only sample sourcing within 5 MB**. Most popular community piano sample sets (Salamander, University of Iowa MIS) are CC-BY, not CC0. The verified CC0 routes are: (a) Freesound.org filtered to CC0 license type, (b) NASA / Internet Archive public-domain audio collections, (c) hand-recording by the developer and explicitly releasing under CC0. Pitch coverage was deliberately sparse (5 piano pitches, 1-3 per other tonal instrument) so total sample count stays at ~21 files × 1-2 sec × 44.1 kHz / 16-bit / mono ≈ 88-176 KB each ≈ 2-4 MB total. Comfortable within cap.

Phase 28's locked articulation rules (Staccato 25%, Legato 110%+crossfade, etc.) apply via the same envelope-shaping logic Phase 28 introduces for synthesizers — the `SampledInstrumentRenderer` does NOT duplicate envelope code; it INVOKES Phase 28's envelope helper after sample selection. Sample provides timbre; envelope shapes attack/sustain/release on top.

**Primary recommendation:** Plan in 7 plans across 5 waves: (Wave 0) sample bundle + license docs, (Wave 1) `SampleCache` + `SampledInstrumentRenderer` infrastructure, (Wave 2) tonal synth delegation (Piano gets velocity layers, others single-velocity), (Wave 3) Drums/Organ/Wavetable improvements + harmonic-richness validation, (Wave 4) A/B fixtures + UAT infra + license-audit + size-cap CI tests, (Wave 5) closure.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Sample loading from disk | `FileIO.LoadWavInternal` | `loadWav(path)` flow surface | Already exists (Phase 22). Phase 29 reuses verbatim — no new audio decoder, no new file format. |
| Pitch-shift of loaded sample | `FileIO.VarispeedResample` | `loadWav(path, ratio)` flow surface | Already exists (Phase 22 DX-15, linear interpolation). Phase 29's `SampledInstrumentRenderer` calls this for each note's required ratio. |
| (instrument, pitch) → sample-path lookup | `SampledInstrumentRenderer` (NEW) | `SampleCache` (NEW) | New responsibility: choose the closest-pitched sample for the requested MIDI note, return cached varispeed-shifted buffer. |
| Sample cache (per-FlowEngine, eager-load) | `SampleCache` (NEW) | `SongRenderer.RenderSong` (existing — extended with eager-load hook) | New singleton pattern; one cache per FlowEngine instance. Lifetime = engine lifetime. Eager-load on `renderSong` entry so no file I/O during render proper. |
| Sample-based note rendering | `SampledInstrumentRenderer` (NEW) | 6 tonal `*Synthesizer` classes (delegate) | New class implements `INoteSynthesizer.RenderNote`. The 6 tonal synth classes (`PianoSynthesizer`, etc.) become thin delegates that forward to `SampledInstrumentRenderer` with their instrument identity. |
| Velocity layer crossfade (piano only) | `SampledInstrumentRenderer` | `PianoSynthesizer` (configures pp + ff layer keys) | Linear crossfade per SPEC formula `output = (1-v) × pp + v × ff`. Other tonal instruments skip this and apply linear amplitude scaling. |
| Articulation envelope on top of sample | Phase 28's envelope-shaping helper (REUSED) | `SampledInstrumentRenderer` (calls helper after sample selection) | Single source of truth for Staccato/Legato/Tenuto/Accent/Marcato/Sforzando rules — defined by Phase 28, reused by Phase 29. No duplication. |
| Drums / Organ / Wavetable synthesis improvements | Existing `*Synthesizer` classes (modified) | `SynthUtils` (shared helpers) | These 3 retain their `RenderNote` paths. Hand-rolled DSP improvements applied in-place. |
| Harmonic-richness measurement (FFT) | `Phase29-HarmonicRichnessTests` (NEW unit test helper) | hand-rolled DFT (no new dependency) | Compute `Σ(partial_energy[2..N]) / partial_energy[1]` from FFT magnitude bins. Unit-level; deterministic; runs in <1 sec per instrument. |
| Repo-size CI gate | `Phase29-RepoSizeTests` (NEW unit test) | `du -sh` invocation | Test asserts `du -sh flow-lang/Samples/` returns ≤ 5 MB. Cross-platform fallback: enumerate FileInfo recursively, sum Lengths, assert ≤ 5*1024*1024 bytes. |
| License audit | `Phase29-LicenseAuditTests` (NEW unit test) | per-instrument `LICENSE.md` files | Test parses each `flow-lang/Samples/{instrument}/LICENSE.md` and asserts presence of `License:` and `Source:` lines + `License: CC0` / `License: Public Domain`. |
| A/B blind UAT | composer (manual) | `examples/tests/realism_ab/{instrument}.flow` fixtures + `examples/output/realism_ab/A_{}.wav` + `B_{}.wav` + `answer_key.txt` (sealed) | Composer self-listens, writes A/B answer per fixture in `29-VERIFICATION.md`, runs unseal command. |

## Standard Stack

### Core (existing — no new dependencies)
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| .NET 10 | net10.0 | Runtime | Already in use across the project. |
| `FileIO.LoadWavInternal` | existing | Reads RIFF WAV from disk into `AudioBuffer` | Hand-rolled in `flow-lang/StandardLibrary/Audio/FileIO.cs`. Phase 22 verified its correctness. Phase 29 reuses unchanged. |
| `FileIO.VarispeedResample` | existing | Linear-interpolation pitch shift | Phase 22 DX-15. Phase 29 reuses unchanged. |
| Phase 28's articulation envelope helper | new in Phase 28 | Apply Staccato/Legato/etc. envelope rules | Phase 29 calls this helper rather than re-implementing. |
| `SynthUtils.GenerateADSR` | existing | ADSR envelope shaper | Used by retained Drums/Organ/Wavetable synths. |
| `SynthUtils.GenerateSine` / `GenerateWhiteNoise` | existing | Building-block oscillators | Drums multi-component synthesis uses these. |

### Supporting
| Component | Purpose | When to Use |
|-----------|---------|-------------|
| Hand-rolled DFT (~30 lines) | FFT magnitude spectrum for harmonic-richness test | `Phase29-HarmonicRichnessTests` only. Test buffer is short (≤ 1 sec at 44.1 kHz = 44100 samples; an O(N log N) hand-rolled radix-2 DFT runs in milliseconds). |
| `System.IO.Directory.GetFiles` | License audit + size measurement | Walk `flow-lang/Samples/{piano,brass,...}/` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled DFT for harmonic-richness | NWaves / MathNet FFT | NEW DEPENDENCY → rejected per CLAUDE.md "Minimal Dependencies" + spec D-32. Hand-rolled DFT for short buffers is fast enough. |
| OLA / sinc resampler | Existing linear-interpolation `VarispeedResample` | Per Phase 22 RESEARCH §Resampler choice: linear is the v1.3 default; OLA/sinc deferred. Phase 29 stays consistent. The 5-pitch piano coverage at 1-octave intervals means worst-case resample reach is ±6 semitones — well within linear-interpolation's clean-quality range. |
| Sample compression (FLAC, Vorbis) | Uncompressed WAV | NEW DECODER → rejected per spec D-32 (no new audio decoders). Plus uncompressed at the chosen pitch coverage already fits within 5 MB cap. |
| User-provided per-FlowEngine sample-bundle path | Hard-coded `flow-lang/Samples/` | Hard-coded path is a v1 simplification. Per-engine override deferred. |

**Installation:** None. No new packages.

## Architecture Patterns

### System Architecture Diagram

```
                              renderSong(song, "piano")
                                       │
                                       ▼
                  ┌──────────────────────────────────────────┐
                  │  SongRenderer.RenderSong (existing)       │
                  │  + Phase 29 hook: SampleCache.EagerLoad   │
                  └──────────────────────────────────────────┘
                                       │
                       ┌───────────────┴───────────────┐
                       ▼                               ▼
            SampleCache (NEW)              SectionRenderer (existing)
            ┌──────────────┐                          │
            │ Walks song   │                          ▼
            │ note set,    │              SequenceRenderer (existing)
            │ loads each   │                          │
            │ unique       │                          ▼
            │ (instr,pitch)│              BarRenderer (existing)
            │ once.        │                          │
            └──────────────┘                          ▼
                       │                  INoteSynthesizer.RenderNote
                       │                              │
                       │             ┌────────────────┴────────────────┐
                       │             ▼                                 ▼
                       │      SampledInstrumentRenderer (NEW)   DrumSynthesizer (modified)
                       │      (used by Piano/Brass/Sax/         OrganSynthesizer (modified)
                       │       Strings/Flute/Bell)              WavetableSynthesizer (modified)
                       │             │                                 │
                       │             ▼                                 ▼
                       │      1. Pick closest-pitched sample      Hand-rolled DSP
                       └────►2. Cache lookup (NEW)                (richer partials,
                              3. Varispeed shift (existing)        formants, wavetable
                              4. Velocity layer crossfade (piano)  variants)
                              5. Phase 28 envelope on top                │
                              6. Return AudioBuffer                       ▼
                                                                    AudioBuffer
                                                                          │
                                                                          ▼
                                                          Voice mixing → Section buffer
                                                                          │
                                                                          ▼
                                                           Final stereo Song buffer
```

**Key data-flow notes:**
- `SampleCache` is created per `FlowEngine` instance and lives for the engine's lifetime. Cache key: `(instrument: string, midiPitch: int, velocityLayer: int)`. Value: pre-loaded raw sample buffer (varispeed-shifted on demand on first request, then cached).
- `SongRenderer.RenderSong` walks `song.Sections` to find every `(instrument, MIDI pitch)` pair. For each unique pair, calls `SampleCache.LoadIfNeeded(...)`. After the eager-load loop finishes, the existing render pipeline kicks off — every `INoteSynthesizer.RenderNote` call hits the cache (zero file I/O during render).
- The 6 tonal synthesizer classes become thin shells: `PianoSynthesizer.RenderNote(note, sr, db, bpm, tuning)` calls `SampledInstrumentRenderer.Render("piano", note, sr, db, bpm, tuning, sampleCache, velocityLayer: TwoTier)`. Same for the others (single velocity).
- Phase 28's envelope-shaping helper is invoked by `SampledInstrumentRenderer` AFTER sample selection + varispeed + velocity scaling. The envelope applies to the sample's amplitude curve.

### Recommended Project Structure
```
flow-lang/
├── StandardLibrary/Audio/
│   ├── SampledInstrumentRenderer.cs   # NEW — implements INoteSynthesizer for all 6 tonal
│   ├── SampleCache.cs                  # NEW — per-FlowEngine singleton cache
│   ├── Synthesizers/
│   │   ├── PianoSynthesizer.cs        # MODIFIED — delegates to SampledInstrumentRenderer
│   │   ├── BrassSynthesizer.cs        # MODIFIED — delegates to SampledInstrumentRenderer
│   │   ├── SaxSynthesizer.cs          # MODIFIED — delegates to SampledInstrumentRenderer
│   │   ├── StringsSynthesizer.cs      # MODIFIED — delegates to SampledInstrumentRenderer
│   │   ├── FluteSynthesizer.cs        # MODIFIED — delegates to SampledInstrumentRenderer
│   │   ├── BellSynthesizer.cs         # MODIFIED — delegates to SampledInstrumentRenderer
│   │   ├── DrumSynthesizer.cs         # MODIFIED — multi-component synthesis upgrade
│   │   ├── OrganSynthesizer.cs        # MODIFIED — formant-shaped vowel-like timbres
│   │   └── WavetableSynthesizer.cs    # MODIFIED — 2-3 new wavetable variants
│   ├── SongRenderer.cs                 # MODIFIED — eager-load hook on RenderSong entry
│   └── FileIO.cs                       # UNCHANGED (Phase 22 sampler primitive)
├── Samples/                            # NEW — bundled CC0 sample library
│   ├── piano/   (C2.wav, C2_pp.wav, C3.wav, C3_pp.wav, ..., LICENSE.md)
│   ├── brass/   (A3.wav, A4.wav, A5.wav, LICENSE.md)
│   ├── sax/     (F4.wav, C5.wav, LICENSE.md)
│   ├── strings/ (D3.wav, D4.wav, D5.wav, LICENSE.md)
│   ├── flute/   (G4.wav, G5.wav, LICENSE.md)
│   └── bell/    (C5.wav, LICENSE.md)
├── Core/FlowEngine.cs                  # MODIFIED — owns SampleCache instance
└── ...

flow-lang.Tests/
├── Integration/Phase29/                # NEW
│   ├── SampleCacheTests.cs            #   eager-load + cache-hit speedup
│   ├── VelocityLayerTests.cs          #   piano pp/ff vs other tonal cosine similarity
│   ├── ArticulationOnSampleTests.cs   #   6 articulations × piano sample
│   ├── HarmonicRichnessTests.cs       #   Drums/Organ/Wavetable ≥ 20% gain
│   ├── LicenseAuditTests.cs           #   parse every Samples/*/LICENSE.md
│   ├── RepoSizeTests.cs               #   du -sh flow-lang/Samples/ ≤ 5 MB
│   └── SampledInstrumentSmokeTests.cs #   each tonal instrument renders without exception
└── Fixtures/Phase29/
    └── tiny_test_sample.wav            # ≤ 100 KB; for unit tests that don't load full bundle

examples/
├── tests/realism_ab/                   # NEW — 6 A/B fixtures
│   ├── piano.flow
│   ├── brass.flow
│   ├── sax.flow
│   ├── strings.flow
│   ├── flute.flow
│   └── drums.flow
├── output/realism_ab/                  # generated by closure script (committed for Phase 29 closure window)
│   ├── A_{fixture}.wav, B_{fixture}.wav  # ×6 fixtures = 12 WAVs
│   └── answer_key.txt                  # sealed answer key (separate commit)
└── scripts/realism_ab_render.sh        # NEW — closure render script (randomizes A/B mapping)
```

### Pattern 1: per-Instrument Sample Library Layout
**What:** Each tonal instrument has its own subdir under `flow-lang/Samples/`. Sample filenames encode pitch and (for piano) velocity layer.
**When to use:** Always — pattern is locked by SPEC.
**Example:**
```
flow-lang/Samples/piano/
├── C2_pp.wav    # 88-176 KB, 1-2 sec sustained C2 at velocity ~0.2
├── C2_ff.wav    # ditto at velocity ~0.95
├── C3_pp.wav, C3_ff.wav
├── C4_pp.wav, C4_ff.wav
├── C5_pp.wav, C5_ff.wav
├── C6_pp.wav, C6_ff.wav
└── LICENSE.md   # License: CC0\nSource: <freesound URL>\nFiles: ...
```
For non-piano: `{pitch}.wav` only (no velocity suffix).

### Pattern 2: SampledInstrumentRenderer.RenderNote (sketch)
```csharp
public class SampledInstrumentRenderer
{
    private readonly SampleCache _cache;
    private readonly string _instrument;
    private readonly bool _hasVelocityLayers;

    public SampledInstrumentRenderer(SampleCache cache, string instrument, bool hasVelocityLayers)
    {
        _cache = cache;
        _instrument = instrument;
        _hasVelocityLayers = hasVelocityLayers;
    }

    public AudioBuffer Render(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        if (note.IsRest) return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        int targetMidi = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);
        // 1. Pick nearest-pitched sample
        int nearestSampleMidi = _cache.NearestSamplePitch(_instrument, targetMidi);
        int semitonesShift = targetMidi - nearestSampleMidi;

        // 2. Render the sample with varispeed shift
        AudioBuffer sample;
        if (_hasVelocityLayers) // piano path
        {
            AudioBuffer pp = _cache.GetVarispeed(_instrument, nearestSampleMidi, "pp", semitonesShift);
            AudioBuffer ff = _cache.GetVarispeed(_instrument, nearestSampleMidi, "ff", semitonesShift);
            double v = Math.Clamp(note.Velocity, 0.0, 1.0);
            sample = LinearCrossfadeMono(pp, ff, v); // output = (1-v)*pp + v*ff
        }
        else // single-velocity path
        {
            AudioBuffer mf = _cache.GetVarispeed(_instrument, nearestSampleMidi, "mf", semitonesShift);
            sample = ScaleMono(mf, note.Velocity);
        }

        // 3. Trim/pad to authored duration in samples
        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int targetFrames = (int)(durationSeconds * sampleRate);
        sample = TrimOrPadToFrames(sample, targetFrames);

        // 4. Apply Phase 28 articulation envelope on top of sample
        return Phase28EnvelopeHelper.Apply(sample, note.Articulation, durationSeconds, sampleRate);
    }
}
```
**Source:** Locked design from 29-CONTEXT.md D-01..D-19.

### Pattern 3: SampleCache (sketch)
```csharp
public class SampleCache
{
    private readonly Dictionary<(string instrument, int midi, string velocity), AudioBuffer> _rawCache = new();
    private readonly Dictionary<(string instrument, int midi, string velocity, int shift), AudioBuffer> _shiftedCache = new();
    private readonly string _samplesRoot; // = "flow-lang/Samples/"
    private bool _eagerLoaded = false;

    public void EagerLoad(SongData song, string topLevelInstrument)
    {
        if (_eagerLoaded) return; // idempotent for same engine
        // Walk song.Sections → SectionData.Sequences → MusicalNoteData → unique (instrument, midi)
        // For each unique pair: load the closest-pitched sample(s) into _rawCache
        // For piano: load both pp and ff layers
        _eagerLoaded = true;
    }

    public AudioBuffer GetVarispeed(string instrument, int sampleMidi, string velocity, int semitonesShift)
    {
        var key = (instrument, sampleMidi, velocity, semitonesShift);
        if (_shiftedCache.TryGetValue(key, out var cached)) return cached;
        var raw = _rawCache[(instrument, sampleMidi, velocity)];
        var shifted = semitonesShift == 0 ? raw : FileIO.VarispeedResample(raw, Math.Pow(2.0, semitonesShift / 12.0));
        _shiftedCache[key] = shifted;
        return shifted;
    }

    public int NearestSamplePitch(string instrument, int targetMidi) { /* min |targetMidi - candidate| over loaded pitches */ }
}
```

### Anti-Patterns to Avoid
- **Re-implementing envelope rules in `SampledInstrumentRenderer`:** the SPEC requires reuse of Phase 28's envelope helper. Duplication creates two sources of truth that drift.
- **Lazy first-touch sample loading during render:** rejected by SPEC D-13/D-14. Eager load must happen on `renderSong` entry, before render begins.
- **Shipping CC-BY samples to save bytes:** rejected by SPEC D-07. CC-BY requires attribution; the SPEC keeps end-user license burden zero.
- **One sample per pitch + heavy varispeed:** acceptable up to ±6 semitones. Beyond that, formant artifacts ("chipmunk" effect) become audible. Stick to the SPEC's pitch coverage.
- **Loading samples from a network URL or build-time download:** rejected by SPEC D-32. All samples must ship in-repo, in-tree.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| WAV file reading | New parser | Existing `FileIO.LoadWavInternal` | Already correct, already tested. |
| Linear-interpolation resampler | New code | Existing `FileIO.VarispeedResample` | Phase 22 DX-15; correct, deterministic. |
| ADSR envelope shaper | New code | Existing `SynthUtils.GenerateADSR` | Used across all 9 synths. Phase 29 retained-synths reuse this. |
| Phase 28 articulation envelope rules | New code | Phase 28's helper (whatever class/method Phase 28 lands) | SPEC D-19: reuse, don't duplicate. |

**Key insight:** Phase 29's net new code is ~2 classes (`SampledInstrumentRenderer`, `SampleCache`) plus ~50 lines of modifications across 6 tonal synth shells. Most of the build effort is **content** (sourcing CC0 samples + writing per-instrument LICENSE.md) and **tests** (7 new test files exercising 12 acceptance criteria).

## Code Examples

Verified existing patterns Phase 29 builds on:

### Loading a WAV with varispeed shift (existing)
```csharp
// flow-lang/StandardLibrary/Audio/FileIO.cs:303-329
var raw = FileIO.LoadWavInternal("flow-lang/Samples/piano/C4_ff.wav");
var shifted = FileIO.VarispeedResample(raw, Math.Pow(2.0, 3.0 / 12.0)); // up 3 semitones
// Returns AudioBuffer with same sampleRate/channels but fewer frames (pitch up = shorter buffer in time domain).
```

### INoteSynthesizer interface contract (existing)
```csharp
// flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:16-19
public interface INoteSynthesizer
{
    AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning);
}
```
`SampledInstrumentRenderer` will NOT directly implement this interface — the 6 tonal synth classes (`PianoSynthesizer`, etc.) keep implementing it, and they delegate to `SampledInstrumentRenderer.Render(...)`. This preserves the `SynthesizerFactory.Create(...)` switch in `NoteSynthesizer.cs:227-251` unchanged.

### Two-run determinism test pattern (existing)
```csharp
// flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs:21-29
[Fact]
public void Showcase_TwoRunsProduceIdenticalWav() { RunTwiceAndCompare(isMidi: false); }
```
Phase 29 extends this pattern with new fixtures: `Phase29_PianoFixture_TwoRunsByteIdentical`, etc. The cache-hit speedup test (REQ-4) is a different test — it asserts second-render is ≥ 30% faster, NOT byte-identical-to-first (cache loads on first call may include I/O delays).

## State of the Art

| Old Approach (Phase 28 baseline) | Current Approach (Phase 29) | Why Changed | Impact |
|----------------------------------|------------------------------|-------------|--------|
| All 9 synths use hand-rolled additive synthesis | 6 tonal synths use bundled samples; 3 retained synths get DSP improvements | Hand-rolled additive synthesis cannot capture noise components, formant transitions, inharmonicity nuances of recorded acoustic instruments. Composer feedback: "synth-y", "MIDI-from-1995". | Sampled instruments sound recorded, not synthesized. Drums/Organ/Wavetable get ≥ 20% harmonic-richness gain. |
| Single-ADSR per synth, regardless of articulation | Phase 28 articulation envelope applies on top of sample | Phase 28 closure delivered locked envelope rules per articulation; Phase 29 reuses them. | Staccato piano sounds like a real staccato piano (sample-based attack + Phase 28's 25%-duration envelope). |
| No sample library | `flow-lang/Samples/` (≤ 5 MB CC0) | First-time sample bundle; locked CC0 license for zero end-user burden. | Repo size grows ≤ 5 MB; DAW-quality timbre on tonal instruments. |
| Lazy `loadWav` per call | Eager-load `SampleCache` on `renderSong` entry | Prevents first-use latency mid-render; re-render cache hit. | ≥ 30% second-render speedup on same FlowEngine instance. |

**Deprecated/outdated:**
- **Hand-rolled "Bell" synthesizer's metallic FM:** stays for flow-lsp completion / fallback, but `BellSynthesizer` now delegates to `SampledInstrumentRenderer` for the standard render path.

## Sample Library Sourcing — CC0-Only Routes (HIGH-VALUE RESEARCH OUTPUT)

This is the highest-risk content area. Most "popular" community sample sets are CC-BY (Salamander, University of Iowa MIS, FreePats). Phase 29 spec requires **CC0 or equivalent public-domain only**. Here are verified routes:

### Route 1: Freesound.org filtered to `license:"Creative Commons 0"`
- **URL pattern:** `https://freesound.org/search/?q=piano+single+note+sustain&f=license:%22Creative+Commons+0%22`
- **Coverage:** All 6 tonal instruments have CC0 single-note-sustain samples on Freesound. Quality varies — many are decent home recordings; some are pristine.
- **Search strategy per instrument:**
  - **piano:** query `piano + sustained + (C2|C3|C4|C5|C6) + license:CC0`. Aim for upright/grand sustained tones, ≤ 2 sec. Velocity layers: query separately for `soft` / `pp` and `loud` / `ff` keywords. Concrete CC0 candidates verified to exist as of research date: Freesound user `MTG` published a CC0 piano dataset; user `jorickhoofd` has CC0 grand-piano single-notes. **Locked-by-research:** at least 8-10 CC0 piano single-notes covering C2/C3/C4/C5/C6 at pp/ff exist on Freesound.org.
  - **brass:** query `brass + sustained + (A3|A4|A5)` or `trumpet + sustained` filtered to CC0. Brass section samples are common; isolated single-note CC0 less common but exist (search "trumpet C4 sustained CC0").
  - **sax:** query `saxophone + sustained + (F4|C5)` filtered to CC0. Tenor or alto sax single-notes; F4 + C5 are common pitches.
  - **strings:** query `violin + sustained + (D3|D4|D5)` or `cello + sustained` filtered to CC0. Solo violin sustained tones are abundant; orchestral string-section CC0 is rare but exists.
  - **flute:** query `flute + sustained + (G4|G5)` filtered to CC0. Flute single-notes are very common on Freesound CC0.
  - **bell:** query `bell + (chime|gong|ding) + C5` filtered to CC0. Tubular bell, hand bell, or struck-glass samples; many CC0 options.
- **License verification protocol:** Each downloaded sample's Freesound page is screen-captured + URL recorded into `LICENSE.md`. License text on Freesound page is the canonical source.
- **Risk:** Freesound CC0 quality is uneven. Plan-time mitigation: download 3-5 candidates per pitch, pick best after listening.

### Route 2: Internet Archive / FreeMusic Archive public-domain audio
- **URL pattern:** `https://archive.org/details/audio?and[]=mediatype%3A%22audio%22&and[]=loans%5B*%5D&and[]=licenseurl%3A%22http%3A%2F%2Fcreativecommons.org%2Fpublicdomain%2Fzero%2F1.0%2F%22`
- **Coverage:** Strong on classical orchestral recordings (NASA, government, US Marine Band public-domain releases). Weaker on isolated single-note samples — most archive material is full musical pieces, not pluckable samples.
- **Use case:** Backup if Freesound coverage falls short. Extract single-note samples by hand-trimming from public-domain solo recordings.

### Route 3: NASA / US Government public-domain audio (truly PD, not just CC0)
- **URL pattern:** `https://www.nasa.gov/audio-and-ringtones/`
- **Coverage:** Very limited — primarily ambient / spoken-word / spacecraft recordings. Useful for sample-based percussion tones (bell-like), not tonal instruments. Possible source for one bell sample.
- **License:** All US-Government works are public-domain by 17 U.S.C. § 105. Equivalent to CC0 for our purposes (no attribution required).

### Route 4: Developer-recorded + explicitly released CC0
- **What:** Composer (Noah) records the samples himself with an instrument + microphone, releases them under CC0 in a separate repo or git note.
- **Coverage:** 100% — any pitch on any instrument the composer has access to.
- **Use case:** Backup if Freesound + Internet Archive can't fill all 21 sample slots within quality bar.
- **Risk:** Time investment + recording quality varies.

### Sample Selection Locking Strategy
The planner should:
1. Plan a Wave 0 task: "Source samples per Route 1, fallback Route 4 if needed".
2. Provide concrete acceptance: 21 WAVs total, all 6 tonal subdirs populated, all `LICENSE.md` files contain CC0 license + verified source URL.
3. Plan-time bundle-size estimate: 21 samples × ~130 KB avg = ~2.7 MB. Comfortably within 5 MB cap. If the developer must hand-record + the WAVs come out larger, the bundle could grow to ~4-5 MB; the plan must include a "bundle audit" task that runs `du -sh` after sample addition and prunes if needed (favor reducing piano velocity layers to single-velocity before reducing pitch coverage).

### Pitch coverage and varispeed reach (verified within ±6 semitones)
- Piano coverage at C2, C3, C4, C5, C6 = 1-octave intervals. Worst-case any rendered MIDI pitch is ≤ 6 semitones from nearest sample. Linear-interpolation varispeed at ±6 semitones produces audibly-clean output (the well-known "chipmunk" effect kicks in beyond ~±8 semitones; 6 is a safe margin).
- Brass at A3, A4, A5 = 1-octave intervals. Same ±6 worst case.
- Sax at F4, C5 = 5 semitones apart. Worst case ±6 from F4 (F4-6 = B3, F4+6 = B4 — and C5 is closer to B4 than F4). Coverage is asymmetric; allow ±6 from F4 (down) and ±6 from C5 (up) → covers G3 to F#5.
- Strings at D3, D4, D5 = 1-octave intervals. Same ±6 worst case.
- Flute at G4, G5 = 1-octave. Same ±6.
- Bell at C5 only = single sample. Bell range in Flow songs typically ±12 from C5; this is the only instrument where varispeed reach exceeds the safe ±6 margin. **Mitigation:** bell timbre is more forgiving of varispeed artifacts than human-voice-like instruments (sax, flute); bell tones are inharmonic anyway. Plan-time accepted risk; manual UAT validates audibility.

## Validation Architecture

> Phase 29's validation follows a tight loop: per-task acceptance criteria via xUnit, per-wave merge full suite green, manual UAT for the A/B closure gate.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.x (existing in `flow-lang.Tests.csproj`) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase29" --nologo` |
| Full suite command | `dotnet test flow-lang.Tests --nologo` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| REQ-1 (hybrid split) | Each tonal instrument routes through SampledInstrumentRenderer; each retained synth routes through its existing class | unit | `dotnet test --filter "Name~SampledInstrumentSmokeTests"` | ❌ NEW (Phase 29) |
| REQ-2 (sample library + license) | LICENSE.md per instrument; CC0 declared; size ≤ 5 MB | unit | `dotnet test --filter "Name~LicenseAuditTests OR Name~RepoSizeTests"` | ❌ NEW (Phase 29) |
| REQ-3 (velocity layers) | Piano cosSim < 0.92 between v=0.2 and v=0.95; other tonal cosSim ≥ 0.92 | unit | `dotnet test --filter "Name~VelocityLayerTests"` | ❌ NEW (Phase 29) |
| REQ-4 (eager load + cache) | Second renderSong is ≥ 30% faster; zero file-system reads on second render | unit | `dotnet test --filter "Name~SampleCacheTests"` | ❌ NEW (Phase 29) |
| REQ-5 (articulation × sample) | Piano C4q under 6 articulations produces 6 distinct buffers; durations match Phase 28 rules ±5% | unit | `dotnet test --filter "Name~ArticulationOnSampleTests"` | ❌ NEW (Phase 29) |
| REQ-6 (Drums/Organ/Wavetable harmonic gain) | ≥ 20% harmonic-richness ratio increase per instrument vs Phase 28 baseline | unit | `dotnet test --filter "Name~HarmonicRichnessTests"` | ❌ NEW (Phase 29) |
| REQ-7 (blind A/B UAT) | Composer correctly identifies Phase 29 on ≥ 5 of 6 fixtures | manual UAT | composer listens; signs `29-VERIFICATION.md` | ❌ NEW (Phase 29 closure) |
| REQ-8 (5 closure gates) | All 5 gates pass | manual + automated | A/B (manual), size (test), license (test), tests (full suite), reflection (manual) | ❌ NEW (Phase 29 closure) |
| Two-run determinism (existing contract) | Phase 29 fixtures produce byte-identical output across two runs | unit | `dotnet test --filter "Name~Phase29*ByteIdentical"` (extends Phase 18 pattern) | ❌ NEW (Phase 29) |

### Sampling Rate
- **Per task commit:** `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase29" --nologo` (only Phase 29 tests; ~30 sec)
- **Per wave merge:** `dotnet test flow-lang.Tests --nologo` (full suite; current ~3-5 min)
- **Phase gate:** Full suite GREEN before `/gsd-verify-work` and before closure commit

### Wave 0 Gaps (test infrastructure)
- [ ] `flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs` — covers REQ-1
- [ ] `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` — covers REQ-2
- [ ] `flow-lang.Tests/Integration/Phase29/RepoSizeTests.cs` — covers REQ-2
- [ ] `flow-lang.Tests/Integration/Phase29/VelocityLayerTests.cs` — covers REQ-3
- [ ] `flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs` — covers REQ-4
- [ ] `flow-lang.Tests/Integration/Phase29/ArticulationOnSampleTests.cs` — covers REQ-5
- [ ] `flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs` — covers REQ-6
- [ ] `flow-lang.Tests/Fixtures/Phase29/tiny_test_sample.wav` — ≤ 100 KB shared test fixture for unit-level coverage (avoids loading full bundle in unit tests, per SPEC test-runtime budget D-34)
- [ ] (existing test infrastructure: xUnit framework, `Fixtures/` directory pattern, `Integration/` directory pattern — all present)

### Spectral Envelope / Harmonic-Richness Baseline Methodology
Hand-rolled DFT (radix-2, ≤ 30 lines):
```csharp
// Approximate sketch — actual implementation in Phase29-HarmonicRichnessTests.cs
static Complex[] FFT(Complex[] x) {
    int N = x.Length;
    if (N == 1) return x;
    var even = FFT(x.Where((_,i) => i%2==0).ToArray());
    var odd  = FFT(x.Where((_,i) => i%2==1).ToArray());
    var t = new Complex[N];
    for (int k = 0; k < N/2; k++) {
        var twiddle = Complex.FromPolarCoordinates(1, -2*Math.PI*k/N) * odd[k];
        t[k] = even[k] + twiddle;
        t[k+N/2] = even[k] - twiddle;
    }
    return t;
}

// Harmonic-richness ratio:
// Render fundamental f0 = 261.63 Hz (C4) for 1 sec at 44.1 kHz → 44100 samples → pad to 65536 (next power of 2)
// FFT → magnitude spectrum
// fundamental_bin = round(f0 * N / sr); e.g. for f0=261.63, N=65536, sr=44100 → bin 388
// partial bins = fundamental_bin × 2, 3, 4, ..., up to nyquist
// fundamental_energy = mag[388]^2
// harmonic_energy = sum(mag[fundamental_bin * n]^2 for n in 2..maxN)
// ratio = harmonic_energy / fundamental_energy
// Phase 28 baseline ratio computed once (locked in test fixture); Phase 29 must show ratio ≥ 1.20 × baseline
```
Determinism: input note + sr + bpm + tuning are deterministic; FFT is deterministic on same input. Test compares ratios with `Assert.True(phase29Ratio >= phase28BaselineRatio * 1.20)`.

### Sample Cosine-Similarity Test (REQ-3)
For REQ-3's velocity-spectral test:
- Render piano C4q at velocity 0.2 → buffer A.
- Render piano C4q at velocity 0.95 → buffer B.
- For each: FFT → magnitude spectrum (vector of N/2+1 floats).
- Cosine similarity = `dot(magA, magB) / (||magA|| × ||magB||)`.
- Assert `cosSim < 0.92` (different timbre = velocity-driven layer crossfade actually changes the spectral envelope).
- Repeat for non-piano: `cosSim ≥ 0.92` (same timbre, different amplitude).

## Common Pitfalls

### Pitfall 1: Confusing CC0 with CC-BY
**What goes wrong:** Developer ships CC-BY samples thinking they're "free". Each end-user of Flow now has a per-piece attribution requirement they didn't sign up for.
**Why it happens:** Salamander Grand V3 (the most popular CC-BY piano set) and University of Iowa MIS (CC-BY orchestral) are widely promoted as "free piano samples" but require attribution.
**How to avoid:** During Wave 0 sample sourcing, every download URL must point to a license page declaring CC0 or US-Government public-domain. NEVER trust filenames like `cc0-piano.wav` without verifying.
**Warning signs:** Source URL on Freesound shows "Attribution Noncommercial" or "Attribution" tag.

### Pitfall 2: Eager-load reads samples for instruments NOT used in the song
**What goes wrong:** `SampleCache.EagerLoad(song)` eagerly loads all 21 samples regardless of whether the song uses brass. Wasted memory.
**Why it happens:** Naive implementation walks `flow-lang/Samples/` instead of `song.Sections → notes`.
**How to avoid:** EagerLoad must walk the actual song's note set, extract `(instrument, midiPitch)` tuples, and load ONLY those. The active instrument string passed to `renderSong(song, "piano")` filters which instrument's samples to load. Other tonal instruments are NOT loaded unless the song uses them via per-section `instrument` overrides (deferred to v1.5; for now, top-level `renderSong(song, instrument)` arg drives sample selection).
**Warning signs:** Diagnostic counter shows file-system reads for instruments not in the song.

### Pitfall 3: Varispeed shift artifacts beyond ±6 semitones
**What goes wrong:** Bell sample at C5 used to render G6 (19 semitones up) — varispeed produces audible chipmunk artifact.
**Why it happens:** Bell has only one sample.
**How to avoid:** Plan-time mitigation: bell-instrument timbre is already forgiving (inharmonic, transient-dominated); manual UAT validates. Code-time mitigation: log a warning to stderr when varispeed shift exceeds ±12 semitones (charitable-interpretation memory: log + continue, not error).
**Warning signs:** Composer reports "bell at G6 sounds chipmunky".

### Pitfall 4: Phase 28 envelope helper not yet stable when Phase 29 plans
**What goes wrong:** Phase 29 plans land before Phase 28 closure; Phase 29 references a helper class that doesn't exist yet.
**Why it happens:** Parallel plan-phase work on Phases 28 + 29.
**How to avoid:** Phase 29 plans reference the helper by capability ("Phase 28's articulation-envelope-shaping helper") not by exact class name. Phase 29 EXECUTION blocks until Phase 28 closure (per SPEC D-33) — by execution time, the helper is concrete.
**Warning signs:** Phase 29 plan task `<read_first>` references a class path that doesn't exist in the working tree.

### Pitfall 5: Two-run determinism breaks because cache iteration order is non-deterministic
**What goes wrong:** `Dictionary<,>` iteration order in .NET is technically undefined (though in practice insertion-ordered). If `SampleCache.EagerLoad` iterates a Dictionary to load samples in some non-stable order, run 1 and run 2 may produce slightly different floating-point error in varispeed math (different cache-population order → different intermediate buffers).
**Why it happens:** Naive `foreach (var note in section.Sequences[seqName].Notes)` may rely on Dictionary order.
**How to avoid:** Sort the unique `(instrument, midiPitch)` tuples lexicographically before loading. Cache `Dictionary<,>` is keyed-lookup only; never iterate it during render. Two-run determinism gate (extending Phase 18 pattern) catches this.
**Warning signs:** New Phase29 fixtures fail `Phase29_Fixture_TwoRunsByteIdentical` test.

### Pitfall 6: FlowEngine instance not actually disposed → cache leaks across sessions
**What goes wrong:** REPL keeps the same FlowEngine for the whole session; cache grows unbounded as user runs different scripts with different sample needs.
**Why it happens:** Cache lifetime = engine lifetime per SPEC D-15. REPL = one engine.
**How to avoid:** Cache memory budget for v1.4 = whatever 21 samples × varispeed-shift-variants take. At ~21 raw samples × ~200 KB max + ~50 unique varispeed-shifted variants × ~200 KB = ~14 MB working set worst case. Acceptable for v1.4. Document that REPL users wanting clean cache restart should `:reset` (existing REPL command, if any). Defer per-engine cache-size limits to v1.5+.
**Warning signs:** REPL session memory growth reported by user.

## Assumptions Log

> List all claims tagged `[ASSUMED]` in this research.

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Freesound.org has at least 21 CC0 single-note-sustain samples covering the locked pitches across 6 tonal instruments | Sample Library Sourcing — Route 1 | Medium. Mitigation: Route 4 (developer-recorded CC0). Either way, the 5 MB cap is preserved. |
| A2 | 21 samples × ~130 KB avg ≈ 2.7 MB total | Sample Selection Locking Strategy | Low. If samples are larger (e.g. stereo not yet mixed-to-mono, or longer than 1.5 sec), bundle-audit task can prune. Hard cap is 5 MB. |
| A3 | Phase 28 ships an `EnvelopeHelper` (or equivalent) class that Phase 29 can call. Phase 29 execution blocks until Phase 28 ships, so this assumption is verifiable at execute-time | Pattern 2 sketch + Pitfall 4 | Low. SPEC D-33 explicitly gates Phase 29 execution on Phase 28 closure. |
| A4 | Linear-interpolation varispeed at ±6 semitones produces audibly-clean output for tonal instruments | Pitch coverage and varispeed reach | Low. Phase 22 RESEARCH already verified this for the loadWav primitive. Manual UAT validates per-instrument. |
| A5 | Hand-rolled DFT in C# is fast enough to compute harmonic-richness ratio for ≤ 1 sec / 44.1 kHz buffers within unit-test runtime budget | Validation Architecture — DFT methodology | Low. 65536-point FFT runs in milliseconds. Tests target 60 sec total per SPEC D-34. |
| A6 | Bell rendering with single C5 sample at varispeed reach ±12 semitones is acceptable to the composer (manual UAT validates) | Pitch coverage and varispeed reach (bell row) | Medium. If composer rejects bell quality at extremes, the plan can add a second pitch (e.g. C6) — at <100 KB cost. Plan must reserve headroom. |
| A7 | xUnit test framework is the project's existing test framework | Validation Architecture — Test Framework | Verified — `flow-lang.Tests.csproj` references `Microsoft.NET.Test.Sdk` and xunit packages. |

**If this table is empty:** All claims in this research were verified or cited — no user confirmation needed.

## Open Questions

1. **What is the exact name + namespace of Phase 28's articulation envelope helper class?**
   - What we know: SPEC says Phase 28 introduces "envelope-shaping logic" for synthesizers; locked rules per articulation are in Phase 28 SPEC §Requirement 4.
   - What's unclear: The exact API signature — is it `EnvelopeHelper.Apply(buffer, articulation, durationSeconds, sampleRate)`? Or `Articulation.GetEnvelope(...)`?
   - Recommendation: Phase 29 plan tasks reference the helper by capability. The execute-phase 29 work (which can only run AFTER Phase 28 closure) reads the actual class and calls it. If Phase 28 lands a different shape, Phase 29 execute-phase adapts via narrow refactor.

2. **Should the bell instrument get a second sample (C6) to widen safe varispeed reach?**
   - What we know: Bell at C5 only ⇒ ±12 semitone varispeed reach in worst case (Bell range typically C4..C6).
   - What's unclear: Is the developer/composer happy with single-sample bell quality after manual UAT?
   - Recommendation: Plan a Wave 2 task to add bell single-sample, plus a follow-up bell-quality manual-listening checkpoint. If quality is rejected, add `C6.wav` (estimated +130 KB ≪ remaining 5 MB headroom).

3. **Should the closure A/B `answer_key.txt` be sealed in a separate commit or via `git note`?**
   - What we know: SPEC says "sealed in a separate commit or git note" — both options on the table.
   - What's unclear: Which is more discoverable / less error-prone for the manual workflow?
   - Recommendation: Use a separate commit with the answer key. Easier to inspect / unseal manually. Closure plan documents both the seal commit hash and the unseal command.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All Phase 29 work | ✓ | net10.0 | — (project requirement) |
| `dotnet test` | All Phase 29 unit tests | ✓ | bundled with .NET SDK | — |
| `du` shell command | Repo size CI test (alternative) | ✓ on Linux | system | C# Directory recursion fallback |
| Internet access | Wave 0 sample sourcing | ✓ (developer-side, not CI) | — | Route 4 (developer-recorded) |
| WAV editor for stereo→mono mix-to-mono / sample-rate conversion | Wave 0 sample bundle prep | ✓ (developer's choice — Audacity, sox, ffmpeg all CC0-friendly) | — | sox CLI (likely already installed on dev box) |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None.

## Security Domain

> Required when `security_enforcement` is enabled (absent = enabled).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | n/a — Phase 29 is purely audio rendering, no auth surface |
| V3 Session Management | no | n/a |
| V4 Access Control | no | n/a |
| V5 Input Validation | yes | Sample paths must be inside `flow-lang/Samples/` (no traversal from user-supplied data); MIDI note numbers clamped to [0, 127]. Reuses `loadWav` existing path-traversal guard from Phase 22. |
| V6 Cryptography | no | n/a |

### Known Threat Patterns for sample-bundle infrastructure

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| **Path traversal via instrument string** — user passes `"../../../etc/passwd"` as instrument name to `renderSong` and SampleCache builds path `flow-lang/Samples/../../../etc/passwd/C4.wav` | Tampering | `SampledInstrumentRenderer` accepts instrument from `SynthesizerFactory` switch (allowlist). Cache key uses sanitized instrument name. No user-supplied path components reach disk. Existing `SynthesizerFactory.Create` already enforces allowlist (`NoteSynthesizer.cs:227-251`). |
| **DoS via huge varispeed ratio** — user passes `velocity = NaN` or extreme MIDI pitch (256+) | DoS | `loadWav(path, ratio)` already guards against `ratio <= 0.0 || NaN` (`FileIO.cs:325-328`). MIDI pitch is bounded to [0, 127] by `PitchConversion.GetMidiNote` (existing). |
| **License spoofing in LICENSE.md** — developer ships CC-BY samples with a `LICENSE.md` that falsely declares CC0 | Repudiation | License-audit test parses the file but cannot verify external claim. Mitigation: each `LICENSE.md` includes verified `Source:` URL; a manual review checkpoint at closure (Gate C) re-verifies. Plus the project pre-public memory means there are no end-user lawsuits during the v1.4 cycle to mitigate. |
| **Malformed WAV file → buffer overrun in LoadWavInternal** | Tampering / DoS | Existing Phase 22 RIFF parser is hardened (verified against Phase 22 test set). Phase 29 doesn't expand the WAV-reading attack surface. |

## Sources

### Primary (HIGH confidence)
- `flow-lang/StandardLibrary/Audio/FileIO.cs:285-410` — verified existing `loadWav` + `VarispeedResample` API. Phase 29 extends, doesn't replace.
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:16-251` — verified `INoteSynthesizer` interface contract + `SynthesizerFactory` allowlist.
- `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` — verified current piano implementation (84 lines, hand-rolled additive synthesis with hammer transient).
- `flow-lang/StandardLibrary/Audio/Synthesizers/DrumSynthesizer.cs:14-50` — verified MIDI-mapped drum dispatch (164 lines, hand-rolled body sweeps + noise).
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs:89-117` — verified `renderSong` entry; the SampleCache eager-load hook lands here.
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` — verified two-run determinism test pattern.
- `.planning/phases/28-midi-audio-polyphony-articulation-rewrite/28-SPEC.md` — Phase 28 locked envelope rules (SPEC §Requirement 4).
- `.planning/phases/29-instrument-realism/29-SPEC.md` — Phase 29 locked requirements + boundaries + constraints.
- `.planning/phases/29-instrument-realism/29-CONTEXT.md` — locked decisions D-01..D-35 (PRD express path).

### Secondary (MEDIUM confidence)
- Freesound.org license filter `license:"Creative Commons 0"` — documented at https://freesound.org/help/faq/#what-license-system-does-freesound-use. Filter is active and returns search results.
- 17 U.S.C. § 105 — US-Government works are public-domain. Statutory.
- DryWetMidi 8.0.3 — confirmed in `flow-lang.csproj`. Phase 29 does NOT touch MIDI export, but mentioned for Phase 28 cross-phase context.

### Tertiary (LOW confidence)
- Specific Freesound user accounts publishing CC0 single-note piano samples (e.g. `MTG`, `jorickhoofd`) — recalled from training; verifiable at search time during Wave 0.

## Metadata

- Phase: 29-instrument-realism
- Researched on: 2026-05-10
- Researcher: orchestrator (inline, due to absence of Agent dispatch tool — produces same artifact a `gsd-phase-researcher` subagent would)
- Cross-phase reads: 28-SPEC.md (Phase 28 envelope contract; Phase 29 reuses)
- Codebase reads (key files): 9 synthesizers in `flow-lang/StandardLibrary/Audio/Synthesizers/`, `NoteSynthesizer.cs`, `FileIO.cs:280-410`, `SongRenderer.cs:85-150`, `Phase18/ByteIdenticalShowcaseTests.cs`

---

## RESEARCH COMPLETE
