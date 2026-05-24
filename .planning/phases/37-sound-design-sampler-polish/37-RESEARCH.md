# Phase 37: Sound Design + Sampler Polish — Research

**Researched:** 2026-05-22
**Domain:** Audio DSP (phase vocoder, PSOLA, HPS, granular), SFZ sampler polish, sample-based instrument expansion
**Confidence:** HIGH (algorithms are textbook; existing Phase 28/29/33 infrastructure verified by code read; VSCO-CE percussion patch verified via GitHub API; U-Iowa MIS source coverage verified via official site)

## Summary

Phase 37 ships four loosely-coupled surfaces in a single phase: three greenfield DSP builtins (`granular`/`stretch`/`pitchShift`), a stereo-mix retrofit (SFZ path), SFZ sampler polish (round-robin + velocity crossfade + per-articulation envelope multiplier), and three sample-asset expansions (piano warmth, flute D5 gap, sampled drums). CONTEXT.md locks 16 decisions D-37-01..16 covering plan layout, mode hierarchy, API shapes, sample sources, and the MIX-01 audit conclusion (synth-path pan is already shipped — only RMS-regression coverage is new). The remaining research budget is technical depth on the algorithms and a small number of asset-source verifications.

The four big technical risks are: (1) phase vocoder "phasiness" / transient smearing — addressed by Laroche & Dolson 1999 identity phase-locking and a vertical-phase-coherence preservation strategy; (2) PSOLA pitch-period detection accuracy under transient/unvoiced regions — addressed by YIN autocorrelation with voicing gate; (3) HPS transient detector tuning — Fitzgerald 2010 median-filter approach is the textbook reference, default threshold ~0.3 normalized validated against literature; (4) U-Iowa MIS piano ONLY has pp/mf/ff (NO mp) — the "≥4 velocity layers" target (D-37-09) requires the bundle to either ship 3 layers and call it "≥3", synthesize the mp layer by RMS-interpolating pp+mf, or pivot to a different source. This is a real research finding the planner must resolve.

**Primary recommendation:** Build Plan 37-01 as a pure-DSP utilities + granular plan (window/FFT/HPS/PRNG-jitter + granular core). Plan 37-02 builds vocoder + PSOLA on top of 37-01's foundation, exposing `stretch`/`pitchShift` with `#auto` mode dispatch via HPS per-frame decision. Plan 37-03 retrofits SFZ pan + adds round-robin/xfin opcodes + SAMP-03 multiplier (single SFZ-renderer touch). Plans 37-04/05/06 are independent asset plans gated by composer `user_setup` blocks. Plan 37-07 closes. The U-Iowa MIS pp/mf/ff constraint should be resolved in plan-phase by either (a) synthesizing the 4th layer via RMS-interpolation between pp and mf, OR (b) shipping 3 layers and clarifying the success criterion. Recommend (a) because it adds the perceptual mp layer composer expects without needing a new sample source.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `granular`/`stretch`/`pitchShift` builtins | StandardLibrary/Audio/DSP | TypeSystem (Buffer-typed) | Buffer-in, Buffer-out — natural fit alongside existing DSP rack |
| Window/FFT/HPS helpers | StandardLibrary/Audio/DSP/Internal | — | Internal utility namespace; not composer-facing |
| Granular jitter PRNG | Runtime/PrngRegistry (existing) | StandardLibrary/Audio | Phase 36 contract: all stochastic via PrngRegistry keyed by (SourceLocation, name) |
| Mode-decision stderr advisory | Diagnostics/RenderingDiagnostics (existing) | StandardLibrary/Audio | Phase 32 `[tuning]` precedent — sentinel-keyed WarnOnce |
| MIX-02 SFZ pan wire-up | StandardLibrary/Audio/Sfz/SfzRenderer | StandardLibrary/Audio/SongRenderer | SFZ render emits voice-tagged Buffer; SongRenderer applies pan at additive-mix stage (matches synth path) |
| SFZ round-robin (SAMP-01) | StandardLibrary/Audio/Sfz/SfzParser + SfzRenderer | Runtime/PrngRegistry | Parser recognizes opcodes; renderer maintains per-region sequence counter seeded via voice ordinal |
| SFZ velocity crossfade (SAMP-02) | StandardLibrary/Audio/Sfz/SfzParser + SfzRenderer | StandardLibrary/Audio/SynthUtils | Parser stores xfin_*/xfout_* fields; renderer applies equal-power overlap at region-mix time |
| SAMP-03 articulation multiplier | StandardLibrary/Audio/Sfz/SfzRenderer + SampledInstrumentRenderer | StandardLibrary/Audio/SynthUtils | Multiplies on top of Phase 28's GenerateArticulationADSR output (existing helper) |
| PIANO-01 4-layer expansion | flow-lang/Samples/piano/ (assets) + SampleCache + SampledInstrumentRenderer | — | Asset drop + manifest update; crossfade math already in renderer |
| FLUTE-01 sample point | flow-lang/Samples/flute/ (asset) + SampleCache manifest | — | Pure asset + 1-line manifest edit |
| DRUM-01 VSCO-CE drum SFZ | flow-lang/sfz.flow (composer-facing dict) + DSP-02/03 (pitch-shift route) | — | Dict entry for `#drums` → `GM-StylePerc.sfz`; render routes through DSP-02/03 #auto |
| RMS regression baselines | flow-lang.Tests/baselines/Phase37/ + RmsRegressionTests | — | SPEC-8 ±0.5dB/100ms tolerance; existing Phase28 baseline pattern |

## Standard Stack

Phase 37 is **zero new external dependencies**. All DSP is hand-rolled per D-v1.5-03 (RubberBand explicitly rejected). DryWetMidi 8.0.3 / Melanchall.DryWetMidi stays for MIDI export only — not touched in Phase 37. The only "stack" choice is the FFT implementation, and we're rolling our own (~80-line Cooley-Tukey radix-2 — same complexity class as the existing reverb / filter implementations already shipped).

### Core (Existing — Reused)
| Component | Source | Phase 37 Use |
|-----------|--------|--------------|
| `AudioBuffer` (`flow-lang/Audio/`) | Existing | All DSP I/O — mono or stereo, interleaved |
| `PrngRegistry` (`flow-lang/Runtime/`) | Phase 36 Plan 36-01 | Granular jitter, round-robin sequence (when seeded by SourceLocation) |
| `RenderingDiagnostics.WarnOnce` (`flow-lang/Diagnostics/`) | Phase 32 | One-shot stderr `[stretch]`/`[pitchShift]` advisories |
| `SfzParser` / `SfzRenderer` (`flow-lang/Sfz/`) | Phase 33 | Extended with seq_position/seq_length/xfin_*/xfout_* opcodes |
| `SfzRegion` record (`flow-lang/Sfz/`) | Phase 33 | New optional fields: `SeqPosition`, `SeqLength`, `XfinLoVel`, `XfinHiVel`, `XfoutLoVel`, `XfoutHiVel` |
| `SampleCache` (`flow-lang/Audio/`) | Phase 29 | PIANO-01 + FLUTE-01: manifest expansion; new velocity labels |
| `SampledInstrumentRenderer` (`flow-lang/Audio/`) | Phase 29 | PIANO-01: 4-way crossfade replaces 2-way; SAMP-03 multiplier overlay |
| `SynthUtils.GenerateArticulationADSR` (`flow-lang/Audio/`) | Phase 28 | SAMP-03 baseline — multipliers stack on top |
| `RmsRegressionTests.AssertWavMatchesBaseline` (`flow-lang.Tests/Helpers/`) | Phase 28 SPEC-8 | PIANO-01 close-out baseline, MIX-01 audit baseline |
| Named-arg call syntax (`flow-lang/Parsing/Parser.cs:1283-`) | Phase 36 Plan 36-02 | `grain=50ms density=20Hz` etc. — already wired |

### No New Packages

Per CLAUDE.md "Minimal Dependencies" + D-v1.5-03 + Phase 37 CONTEXT canonical_refs, this phase adds **zero NuGet packages**. All algorithms hand-rolled in `flow-lang/StandardLibrary/Audio/DSP/` (existing directory). [VERIFIED: `flow-lang/flow-lang.csproj` reads cleanly today with only Pidgin + DryWetMidi; D-v1.5-03 captures the hand-roll commitment]

### Alternatives Rejected
| Instead of | Could Use | Why Rejected |
|------------|-----------|--------------|
| Hand-rolled phase vocoder | RubberBand library | D-v1.5-03 — GPL hazard mirrors Phase 29 SPEC-2 CC-only license posture |
| Hand-rolled PSOLA | Various NuGet wrappers | None on .NET 10 are LGPL-clean; PSOLA is ~150 lines of textbook code |
| Hand-rolled FFT | MathNet.Numerics / FftSharp | Radix-2 Cooley-Tukey is ~80 lines; matches existing "hand-roll DSP" pattern from PROJECT.md |
| New drum-sample bundle | Curated CC0 kit drop | D-37-13 — route via VSCO-CE SFZ surface for license consistency with Phase 33 |

## Package Legitimacy Audit

Phase 37 installs **zero external packages**. No legitimacy audit required.

| Package | Disposition |
|---------|-------------|
| (none) | N/A — phase ships hand-rolled DSP only |

## Architecture Patterns

### System Architecture Diagram

```
Composer .flow source
    │
    ├─ (granular buf grain=50ms density=20Hz jitter=0.3 windowing=#hann)
    │       │
    │       ├─ Parser → FunctionCallExpression with NamedArgs
    │       ├─ Interpreter → resolves args, dispatches to C# builtin
    │       └─ GranularEngine.Process(buf, grain, density, jitter, window, prng)
    │              │
    │              ├─ WindowFunctions.Build(kind, length)        ┐
    │              ├─ PrngRegistry.NextDouble(site, "granular")  ├─ shared utilities (Plan 37-01)
    │              └─ Grain scheduler (density Hz → grain rate)  ┘
    │
    ├─ (stretch buf 2.0 mode=#auto frameSize=2048 transientThreshold=0.3)
    │       │
    │       └─ StretchEngine.Process(buf, factor, mode, knobs)
    │              │
    │              ├─ #vocoder → STFT → phase-locked propagation → ISTFT (Plan 37-02)
    │              ├─ #psola   → YIN pitch detect → epoch marks → OLA at shifted epochs
    │              └─ #auto    → HPS (median-filter spectrogram) per-frame mode pick
    │                            → emit one-shot stderr advisory at end of pass
    │
    ├─ (pitchShift buf +5st mode=#auto)        # same engine as stretch, inverse remap
    │
    ├─ song [intro ...] / renderSong            # SongRenderer pipeline (existing)
    │       │
    │       ├─ Tonal synthesizers (Piano/Brass/Sax/Strings/Flute/Bell)
    │       │       │
    │       │       └─ SampledInstrumentRenderer.Render
    │       │              │
    │       │              ├─ SampleCache.NearestSamplePitch + GetVarispeed (existing)
    │       │              ├─ PIANO-01: 4-way velocity crossfade (pp/mp/mf/ff) replaces 2-way
    │       │              ├─ FLUTE-01: extra sample point at A4 (closes G4↔G5 gap)
    │       │              ├─ Phase 28 articulation envelope (existing)
    │       │              ├─ SAMP-03 articulation multiplier (NEW — stacks on Phase 28)
    │       │              └─ Tail extension (existing, possibly extended for PIANO-01 release=)
    │       │
    │       ├─ SFZ instruments (via "sampler:NAME" instrument string)
    │       │       │
    │       │       └─ SfzRenderer.Render
    │       │              │
    │       │              ├─ SfzParser regions (NEW: seq_position/seq_length/xfin_*/xfout_*)
    │       │              ├─ Round-robin index (seeded via voice ordinal — deterministic)
    │       │              ├─ Velocity crossfade (equal-power when xfin/xfout opcodes present)
    │       │              ├─ Phase 28 articulation envelope (existing)
    │       │              ├─ SAMP-03 multiplier overlay (NEW)
    │       │              ├─ DRUM-01: per-region pitch shift via DSP-02/03 #auto pipeline
    │       │              ├─ MIX-02: per-voice Pan applied here (NEW — currently per-region only)
    │       │              └─ Equal-power 441-frame loop crossfade (existing)
    │       │
    │       └─ SongRenderer.MixVoicesToStereoBuffer (existing)
    │              │
    │              ├─ MIX-01 synth-path pan (ALREADY SHIPPED, line 308-309)
    │              ├─ MIX-02 SFZ-path pan (NEW — apply before this stage or here)
    │              └─ Constant-power: left = cos((pan+1)·π/4), right = sin((pan+1)·π/4)
    │
    └─ writeWav / play                          # existing output sinks
```

### Project Structure (Phase 37 Touch Points)

```
flow-lang/
├── StandardLibrary/
│   └── Audio/
│       ├── DSP/                                # existing dir; extend
│       │   ├── (existing) Reverb.cs, Filter.cs, Compressor.cs, Delay.cs
│       │   ├── WindowFunctions.cs              # NEW — Hann/Gaussian/Tukey (Plan 37-01)
│       │   ├── Fft.cs                          # NEW — radix-2 Cooley-Tukey (Plan 37-01)
│       │   ├── Hps.cs                          # NEW — median-filter HPS (Plan 37-01)
│       │   ├── GranularEngine.cs               # NEW — grain scheduler (Plan 37-01)
│       │   ├── StretchEngine.cs                # NEW — vocoder + PSOLA (Plan 37-02)
│       │   └── PitchShiftEngine.cs             # NEW — same engine, inverse mapping (Plan 37-02)
│       ├── SampleCache.cs                      # EXTEND — PIANO/FLUTE manifest (Plan 37-04, 37-05)
│       ├── SampledInstrumentRenderer.cs        # EXTEND — 4-way crossfade, release= knob (Plan 37-04)
│       ├── SongRenderer.cs                     # NO CHANGE — MIX-01 confirmed shipped
│       └── Sfz/
│           ├── SfzRegion.cs                    # EXTEND — add 6 fields (Plan 37-03)
│           ├── SfzParser.cs                    # EXTEND — recognize 6 opcodes (Plan 37-03)
│           └── SfzRenderer.cs                  # EXTEND — round-robin + xfin/xfout + SAMP-03 + MIX-02 (Plan 37-03)
├── audio.flow (or new dsp.flow)                # EXTEND — composer-facing builtin signatures
└── sfz.flow                                    # EXTEND — add #drums → "GM-StylePerc.sfz" entry (Plan 37-06)

flow-lang/Samples/
├── piano/                                      # ADD — C2..C6 at mp, mf layers (currently pp/ff only)
├── flute/                                      # ADD — A4 sample point (closes D5 gap)
└── (no drums/ — VSCO-CE routes via SFZ per D-37-13)

flow-lang.Tests/
├── baselines/
│   └── Phase37/                                # NEW — PIANO-01 ragtime baseline, MIX-01 synth-pan
└── Integration/Phase37/                        # NEW — DSP correctness, SAMP-01/02/03 acceptance
```

### Pattern 1: Phase Vocoder (STFT-based) — Plan 37-02

**What:** Classical Flanagan-Golden 1966 / Portnoff 1976 / Dolson 1986 framework with Laroche-Dolson 1999 phase-locking refinement.

**When to use:** `#vocoder` mode for harmonic / tonal material (sustained notes, pads, vocals). Causes "phasiness" + transient smearing on percussive material → that's why `#auto` exists.

**Standard parameters** (from Stanford CCRMA / Columbia DSP / Cycling '74 references):
- `frameSize = 2048` (44.1 kHz → ~46 ms analysis window — good for music harmonics)
- `hopSize = 512` (75% overlap, i.e. 4× — minimum for Hann window per CCRMA "Choice of Hop Size")
- `window = Hann` (default; STFT analysis + synthesis both use sqrt(Hann) for COLA reconstruction)
- `overlap factor = frameSize / hopSize` → ≥4 required for Hann; phase vocoder "sounds better at 4×"

**Algorithm sketch:**
```
For each analysis frame at hop boundary:
  1. Window the input frame with Hann
  2. FFT → magnitude + phase
  3. Compute instantaneous frequency per bin from phase delta vs previous frame
  4. Scale time by stretch factor (write synthesis frame at scaled hop position)
  5. Propagate phase forward using inst freq × synthesisHop (NOT analysisHop)
  6. Phase-lock to peaks (Laroche-Dolson identity phase locking): for each magnitude peak,
     compute phase advance; bins in peak's region of influence inherit the peak's phase
     advance (preserves vertical coherence — fixes phasiness)
  7. IFFT → window with Hann → overlap-add into output buffer
```

**Phase-locking refinement (Laroche-Dolson 1999):**
- Peak picking: bins where `|X[k]| > |X[k±1]| ∧ |X[k]| > |X[k±2]|`
- Region of influence: half-way to nearest peak on each side
- Phase update: `phase_out[peak] += inst_freq[peak] × synthHop`; non-peak bins get the same delta
- Eliminates "phasiness" artifact dramatically — this is what RubberBand also does internally

[CITED: https://www.ee.columbia.edu/~dpwe/papers/LaroD99-pvoc.pdf — Laroche & Dolson "New phase-vocoder techniques for pitch-shifting, harmonizing and other exotic effects" 1999]
[CITED: https://ccrma.stanford.edu/~jos/sasp/Choice_Hop_Size.html — Stanford CCRMA on hop size]
[CITED: https://cycling74.com/tutorials/the-phase-vocoder-%E2%80%93-part-i — Cycling '74 phase vocoder tutorial]
[CITED: https://arxiv.org/pdf/2202.07382 — Průša & Holighaus "Phase Vocoder Done Right" 2022 (modern reference)]

### Pattern 2: PSOLA (Time-Domain) — Plan 37-02

**What:** Time-Domain Pitch-Synchronous Overlap-Add — extracts pitch-period-sized grains, repositions them at scaled epoch positions.

**When to use:** `#psola` mode for percussive / transient material (drums, plucked strings, attack-heavy content). Vocoder smears transients; PSOLA preserves them because grains are time-aligned to onsets.

**Pitch period detection: YIN algorithm** (de Cheveigné & Kawahara 2002):
- Difference function `d(τ) = Σ (x[n] − x[n+τ])²`
- Cumulative mean normalized difference `d'(τ) = d(τ) / [(1/τ) Σ d(j)]` — penalizes octave errors
- Pick first τ below threshold (default 0.1) for fundamental
- More accurate than plain autocorrelation (0.5% error vs 17% on standard speech corpus per YIN paper)

**Voiced/unvoiced gate:** YIN's threshold metric doubles as a voicing indicator — if no τ falls below threshold within the audio range, treat the frame as unvoiced and use a default grain length (≈ 10 ms ≈ 441 samples at 44.1 kHz). Unvoiced regions use OLA-without-pitch-sync — quality is comparable for noise-like content.

**OLA at shifted epochs (the actual stretch):**
```
For each authored output epoch position (= input epoch × stretch factor):
  1. Find nearest input epoch
  2. Extract grain of 2 × pitchPeriod centered on input epoch (Hann-windowed)
  3. Overlap-add at output epoch position
  4. (Pitch shift: input/output use different pitchPeriods — shrink/expand grain pre-OLA)
```

[CITED: https://github.com/sannawag/TD-PSOLA — reference TD-PSOLA Python implementation]
[CITED: YIN paper (de Cheveigné & Kawahara, JASA 2002) — pitch detector]

### Pattern 3: HPS for #auto Mode Decision — Plan 37-01

**What:** Harmonic-Percussive Source Separation via median filtering (Fitzgerald 2010). Decides per-frame which mode (`#vocoder` for harmonic, `#psola` for percussive) handles each STFT frame.

**When to use:** `#auto` mode. Per CONTEXT D-37-06, `#auto` emits one-shot stderr summary `[stretch] mode=#auto picked: X% vocoder / Y% psola across N frames` at end of pass.

**Algorithm (Fitzgerald 2010 — ~12 lines of pseudocode):**
```
Given STFT magnitude spectrogram S (frame × bin):
  H = median_filter(S, kernel=horizontal, length=17 frames)  # smooths in time → harmonic enhanced
  P = median_filter(S, kernel=vertical,   length=17 bins)    # smooths in freq → percussive enhanced
  ratio[frame] = mean(P[frame]) / (mean(H[frame]) + ε)
  if ratio[frame] > transientThreshold:  pick #psola for this frame
  else:                                  pick #vocoder for this frame
```

**Default `transientThreshold = 0.3`** [ASSUMED — based on Fitzgerald's normalized-spectrogram examples + librosa default `margin=1.0` and a ~30% percussive-vs-harmonic energy split typical of mixed music; locked default per D-37-07, composer overrides via `transientThreshold=` named arg].

**Kernel sizes** (Fitzgerald original): 17 frames horizontal, 17 bins vertical at 44.1 kHz / 2048 frame / 512 hop. Roughly 200 ms time / 366 Hz freq smoothing — empirically validated by Fitzgerald and reproduced in librosa.

[CITED: https://dafx10.iem.at/papers/DerryFitzGerald_DAFx10_P15.pdf — Fitzgerald 2010 DAFx paper, original HPS via median filter]
[CITED: https://audiolabs-erlangen.de/resources/MIR/FMP/C8/C8S1_HPS.html — FMP / AudioLabs Erlangen HPS tutorial]
[CITED: https://librosa.org/doc/main/auto_examples/plot_hprss.html — librosa HPSS reference]

### Pattern 4: Granular Synthesis — Plan 37-01

**What:** Grain scheduler that pulls grain-sized chunks from input buffer at random offsets (within `jitter` envelope), applies window, overlap-adds at `density` rate.

**When to use:** Texture creation, cloud synthesis, time-stretch-like effects. DSP-01 surface.

**Parameter mapping** (per REQ DSP-01 + Curtis Roads "Microsound"):
- `grain=50ms` → grain length in samples = 50e-3 × sampleRate = 2205 samples @ 44.1 kHz
- `density=20Hz` → grains per second; period between grain onsets = 1/20 = 50 ms
- `jitter=0.3` → random offset into source buffer up to ±0.3 × grain length per grain; AND random ±0.3 × density-period jitter on emit timing
- `windowing=#hann` (default) | `#gaussian` | `#tukey`

**Density × grain → overlap density:**
- 20 Hz × 50 ms = 1.0 → grains are back-to-back, no overlap (sparse texture)
- 40 Hz × 50 ms = 2.0 → 2× overlap (smooth pad-like)
- 100 Hz × 50 ms = 5.0 → dense cloud (lots of overlap, smearing)

[ASSUMED — composer chooses default. Trust composer per language philosophy (per D-37 deferred-ideas section). Document the overlap formula in CLAUDE.md so composers know what they're paying for.]

**Window functions (closed-form, all 3 windowing options):**
```
Hann:     w[n] = 0.5 × (1 − cos(2π n / (N−1)))
Gaussian: w[n] = exp(−0.5 × ((n − (N−1)/2) / (σ × (N−1)/2))²)
          (σ = 0.4 typical; σ > 0.5 has audible discontinuity at endpoints)
Tukey:    w[n] = Hann(n) for n in transition band; 1.0 for flat top
          (α = 0.5 — flat 50% center + Hann roll-off 25% each side)
```

[CITED: https://en.wikipedia.org/wiki/Window_function — closed-form window definitions]
[CITED: https://michaelkrzyzaniak.com/AudioSynthesis/2_Audio_Synthesis/11_Granular_Synthesis/ — granular window function tradeoffs]
[CITED: Curtis Roads "Microsound" (MIT Press 2001) — granular synthesis canonical reference]

**Jitter PRNG (D-v1.5-06 / Phase 36 Plan 36-01 contract):**
```csharp
var rng = ctx.PrngRegistry;  // singleton-per-FlowEngine
double offsetJitter = rng.NextDouble(callSite, "granular_offset") * 2.0 - 1.0;  // [-1, +1]
double timeJitter   = rng.NextDouble(callSite, "granular_timing") * 2.0 - 1.0;
```
Same call site → same Random reference → same sequence across two runs. Reseeded at `renderSong`/`writeWav` boundary. Preserves two-run cmp-clean determinism contract.

### Pattern 5: SFZ Round-Robin (seq_position/seq_length) — Plan 37-03

**What:** `seq_length=N` defines a round-robin group of N regions sharing the same key+vel range; `seq_position=K` (1..N) marks which alternate this region represents. The renderer maintains a per-(key, vel-range) counter that cycles 1..N → 1..N → ... on consecutive triggers.

**Spec semantics** [VERIFIED: https://sfzformat.com/opcodes/seq_position/ and /seq_length/]:
- Counter starts at 1
- On each note-on matching the key range, counter advances modulo seqLength
- Maximum seq_position value is 100
- Counter is per-region-group, NOT global

**Deterministic seeding (per D-37-13 — preserves two-run cmp-clean):**
- Seed the counter from voice ordinal index (the position of the voice in `SongData.Voices`, NOT wall-clock or `GetHashCode`)
- For songs with N voices triggering a region group, voice 0 → starts at counter=1, voice 1 → counter=2, ..., voice N → counter=(N mod seqLength)+1
- This is deterministic across runs at the same git SHA AND across architectures (avoids the C# `GetHashCode` per-process randomization Pitfall 4 from Phase 36)

**VSCO-CE GM-StylePerc.sfz uses this pattern extensively** [VERIFIED: github.com/sgossner/VSCO-2-CE/blob/SFZ/GM-StylePerc.sfz] — kick (MIDI 36) has 7 velocity layers × 2 round-robin alternates (rr1/rr2). DRUM-01 will exercise this code path heavily.

### Pattern 6: SFZ Velocity Crossfade (xfin_*/xfout_*) — Plan 37-03

**What:** Crossfade between adjacent velocity layers instead of hard-switching at velocity boundaries.

**Spec semantics** [VERIFIED: https://sfzformat.com/opcodes/xfin_hivel/ and xfout_hivel]:
- `xfin_lovel=A xfin_hivel=B` → region gain = 0 for vel ≤ A, 1 for vel ≥ B, interpolated in between (fade IN as velocity rises)
- `xfout_lovel=A xfout_hivel=B` → region gain = 1 for vel ≤ A, 0 for vel ≥ B (fade OUT as velocity rises)
- Two adjacent layers: layer1 has xfout, layer2 has xfin — the two interpolations overlap, summing to constant total power IF using equal-power curve

**Equal-power curve** (per `xf_velcurve=power` default, equivalent to Phase 33's existing constant-power formula):
```
theta = (norm_v) × (π / 2)        where norm_v ∈ [0, 1] within fade band
gain_fadein  = sin(theta)
gain_fadeout = cos(theta)
power = sin²(θ) + cos²(θ) = 1     # constant power across band
```

**Fallback when opcodes absent (default):** Hard switch at `lovel`/`hivel` boundaries — current Phase 33 behavior. This is the existing Phase 33 default and stays the default per CONTEXT D-37-08 (sealed-modes rejected — all knobs exposed but defaults preserved).

[CITED: https://sfzformat.com/opcodes/xf_velcurve/ — xf_velcurve power vs gain (linear) choice; we use power]
[CITED: https://www.soundonsound.com/sound-advice/q-should-use-linear-or-constant-power-crossfades — when to use equal-power vs linear]

### Pattern 7: SAMP-03 Articulation Envelope Multiplier — Plan 37-03 (or 37-04 if PIANO-coupled)

**What:** A multiplier curve that stacks **multiplicatively** on top of Phase 28's `SynthUtils.GenerateArticulationADSR` output. Closes the Phase 29 v1.5 follow-up "staccato sampled path sounds thinner than synth path."

**Why the gap exists** (per CLAUDE.md "Known sampled-instrument quirks"): under Phase 28's locked staccato envelope (25% duration + sustain=0 + release×0.5), the sample envelope cuts before the sample body develops. The synth path's hand-rolled oscillator has full energy from sample 0; the sample path has natural attack ramp built into the recording, so the truncated envelope cuts that ramp off.

**Two candidate multiplier shapes** (resolve in plan-phase per D-37-09 / "Claude's Discretion"):

**Option A — Scalar ADSR multiplier:** Each articulation gets a per-stage scalar:
```csharp
// per articulation:
SamplePathArticulationMultipliers[Staccato] = (attack: 0.5, decay: 1.0, sustain: 1.0, release: 0.8);
// multiplies Phase 28's stage durations:
finalAttack  = phase28.Attack  * mult.attack;
// etc.
```
Simple, composable, low-risk. May not fully close the perceptual gap.

**Option B — Full curve overlay:** A 100-sample curve mapped over the entire envelope; multiplied sample-by-sample.
```
finalEnv[i] = phase28Env[i] * sampleMultCurve[i × sampleMultCurve.Length / finalEnv.Length]
```
Higher fidelity; needs per-articulation curve tuning by ear.

**Recommendation:** Start with Option A in plan-phase (lower risk + composable); escalate to Option B if ragtime UAT iteration #2 (D-37-12) still flags the staccato gap.

[CITED: CLAUDE.md "Known sampled-instrument quirks (v1.5 backlog)" — the exact gap SAMP-03 closes]
[CITED: Phase 28 SynthUtils.GenerateArticulationADSR — the locked baseline this stacks on]

### Pattern 8: Piano Sustain Release Tail (PIANO-01) — Plan 37-04

**What:** The `release=` named-arg knob (D-37-11) controls how long after authored note-end the sample tail continues to ring. Current Phase 29 default is 500 ms.

**Physical reference** (from Lehtonen et al. JASA 2007, "Analysis and modeling of piano sustain-pedal effects"):
- Sustain pedal increases decay time of middle-range partials but NOT bass/treble
- Two-stage decay: fast initial (~0.1–0.3s) → slow tail (1.5–6s depending on register + pedal)
- 6s practical full-decay under sustain pedal per Yamaha CP4 community docs

**Phase 29 current implementation** [VERIFIED: `SampledInstrumentRenderer.cs:79-152`]:
- `tailSeconds = 0.5` constant
- Tail fade: `exp(-frame / (sampleRate × 0.15))` → ~6dB/100ms exponential decay
- 500 ms total tail (0.15s time constant means inaudible by ~500 ms)

**PIANO-01 proposal** (per D-37-11):
- Replace `tailSeconds = 0.5` constant with a `release=` named arg (default 1.5s — matches ragtime sustain expectations; default validated against ragtime composer feedback in UAT iteration #2)
- `release=2.5s` → tailSeconds = 2.5, time constant = 2.5 × 0.3 → audible for full 2.5s
- `release=0.2s` → short tail, useful for staccato textures
- Smart default: `1.5s` (locked in plan-phase pending UAT) [ASSUMED — composer-validated in UAT iteration]

**Interaction with Phase 28 articulation envelope (PIANO + Staccato + release=2.0):**
- Articulation envelope applies only to authored frames (existing behavior)
- Tail begins at authored end, decays per `release=` parameter
- Composer chooses release length independent of articulation — composer ergonomics first

[CITED: https://pubmed.ncbi.nlm.nih.gov/17927438/ — Lehtonen, Penttinen et al. "Analysis and modeling of piano sustain-pedal effects" JASA 2007]
[CITED: SampledInstrumentRenderer.cs:79-152 — existing 500 ms tail implementation]

### Pattern 9: U-Iowa MIS Piano Velocity Coverage (PIANO-01 — IMPORTANT FINDING)

**Verified availability** [VERIFIED: https://theremin.music.uiowa.edu/MISpiano.html]:
| Dynamic | Range | Status |
|---------|-------|--------|
| pp | Bb0 – C8 | available |
| mf | B0 – C8 | available |
| ff | A0 – C8 | available |
| **mp** | **— NOT RECORDED** | **NOT AVAILABLE** |

**Implication:** PIANO-01's "≥4 velocity layers per pitch point" (D-37-09, locked) cannot be served by a clean re-extract from U-Iowa MIS — the source only offers pp/mf/ff. Three resolution paths, in order of preference:

**Path 1 (RECOMMENDED): Synthesize mp by RMS-interpolation between pp and mf.**
- Per-sample: `mp[n] = sqrt(pp[n]² × (1 − α) + mf[n]² × α)` where α = 0.6 — gives mp a clear identity between pp and mf, perceptually mp-ish, but no NEW recordings needed
- Cost: per-pitch synthesis at eager-load time; cached
- Composer sees pp/mp/mf/ff at every pitch point → ≥4 layers satisfied
- Risk: synthesized mp may sound "in-between" rather than truly distinct; ragtime UAT decides if it's enough

**Path 2: Ship pp/mf/ff at MORE pitch points (currently 5: C2/C3/C4/C5/C6).**
- Add C#2..B6 chromatic coverage (or some subset) — more sample points → less varispeed shift per note → warmer timbre regardless of velocity layer count
- Reframe "≥4 velocity layers" success criterion as "≥4 timbral degrees of freedom per note" (combining velocity layer count + pitch resolution)
- May be what D-37-09 actually means in spirit; defer to planner to clarify in plan-phase

**Path 3: Pivot to Salamander Grand or VSCO-CE for piano (per D-37-10 alternative).**
- Salamander Grand: 4 velocity layers (pp/mp/mf/ff), CC-BY 3.0 — fits SPEC-2 Phase 29 relaxed license posture
- Requires re-curating a new sample bundle (slower); inconsistent with D-37-10 lock
- D-37-10 LOCKED U-Iowa MIS — pivoting requires re-opening that decision

**Recommendation:** Plan 37-04 ships Path 1 (synthesized mp) with a documented rationale. If ragtime UAT iteration #2 (D-37-12) finds the synthesized mp inadequate, escalate to Path 2 (more pitch points) without re-opening D-37-10.

[VERIFIED: https://theremin.music.uiowa.edu/MISpiano.html — official U-Iowa MIS piano page lists only pp/mf/ff]
[VERIFIED: existing Phase 29 SampleCache.cs manifest line 51: piano has C2/C3/C4/C5/C6 at pp+ff (10 samples total)]

### Pattern 10: FLUTE-01 D5 Gap Closure

**Current Phase 29 flute manifest** [VERIFIED: `SampleCache.cs:55`]: G4 and G5 at mf only (2 samples).
**U-Iowa MIS flute availability** [VERIFIED: https://theremin.music.uiowa.edu/MISflute.html]: B3 – Db7 chromatic at pp/mf/ff, vibrato + non-vibrato.

**Gap to close per D-v1.5-08 hint:** D5 timbre crossover — melodies spanning the G4↔G5 boundary cross the varispeed shift point. Adding a single sample at D5 (or A4) halves the varispeed shift magnitude on either side.

**Recommendation (resolve in plan-phase per CONTEXT "Claude's Discretion"):** Add **A4 (MIDI 69)** rather than D5 (MIDI 74).
- A4 is in the most expressive register of the flute (where embouchure timbre changes are most audible)
- D5 (74) is closer to G5 (79) — only 5 semitones; the G4→D5 stretch is 7 semitones vs G4→A4 is 2 semitones (better varispeed coverage of the LOWER register where most flute melodies live)
- Composer perceives "fuller low register" more readily than "fuller upper register" in ragtime/classical melodies

Alternative: add BOTH D5 + A4 if file-size budget allows. Each sample is ~150 KB (mono 16-bit 44.1 kHz at ~3.5s) — well under the 5 MB cap (CLAUDE.md SPEC-D-02). [ASSUMED — concrete A4 file size to verify in plan-phase with actual U-Iowa download].

[CITED: U-Iowa MIS flute page; existing `flow-lang/Samples/flute/G4.wav` + `G5.wav` manifest]

### Pattern 11: DRUM-01 via VSCO-CE GM-StylePerc.sfz

**Source verification** [VERIFIED: https://github.com/sgossner/VSCO-2-CE/blob/SFZ/GM-StylePerc.sfz]:
- File exists in VSCO-CE SFZ branch; license CC0 (per repo Readme)
- Uses GM percussion key mapping (kick=36, snare=37/38/40, gong=42/46, cymbals=49/51, congas=60-65, etc.)
- Already uses `seq_position`/`seq_length` opcodes extensively → DRUM-01 exercises SAMP-01 SAMP-01 from day one
- Uses velocity layering with cascading lovel/hivel splits (no xfin/xfout — hard switch) → could be enhanced with SAMP-02 crossfade if desired

**Integration steps (Plan 37-06):**
1. Add `#drums  "GM-StylePerc.sfz"` entry to `flow-lang/sfz.flow` GM dict (line ~60)
2. Update VSCO-PATH-AUDIT.md (Phase 33 doc) to mark the 20th entry verified
3. Composer surface: `Sfz drums = (loadSfz #drums)` followed by `renderSong song "sampler:drums"`
4. Per D-37-14: pitch shift routes through DSP-02/03 `#auto` pipeline — VSCO drum samples are recorded at fixed pitches, but composer's note-on at non-recorded MIDI numbers needs varispeed OR proper pitch shift. PSOLA for transients (hits) + vocoder for sustains (cymbal rolls) = right call

**Risk:** Most percussion notes from a composer arrive AT GM percussion MIDI numbers (36, 38, etc.) which ARE recorded — no pitch shift needed. The pitch-shift dependency activates only for non-GM-mapped percussion lookups. This means DRUM-01 may not actually exercise DSP-02/03 heavily in the common case → reconfirm the dependency in plan-phase (D-37-14 may be over-cautious).

**Sub-recommendation:** In plan-phase, decide whether `renderSong song "sampler:drums"` snaps composer note pitches to GM standard (clean 1:1, no pitch shift) OR honors composer's authored pitch with pitch shift (matches the "DSP-02/03 dependency" framing of D-37-14). Suggest: honor composer pitch with `#auto` pitch shift, but emit a one-shot stderr advisory when shifting >12 semitones (drum samples don't pitch-shift well over large intervals — varispeed artifacts dominate).

[VERIFIED: VSCO GM-StylePerc.sfz exists with full GM kit mapping]
[VERIFIED: SFZ branch at github.com/sgossner/VSCO-2-CE/tree/SFZ — 75 SFZ files total]

### Anti-Patterns to Avoid

- **Don't write a generic FFT for one use:** Plan 37-01's FFT is ONLY for vocoder analysis. Don't try to be a general FFT library. Radix-2, power-of-2 only, Cooley-Tukey, in-place — ~80 lines.
- **Don't use C# `Random` directly:** Per D-v1.5-06, MUST route through PrngRegistry. Direct `new Random()` will silently break the two-run cmp-clean contract.
- **Don't try to make `#auto` continuously variable:** Per CONTEXT D-37-06, the per-frame decision is binary (vocoder XOR psola). A continuous blend sounds worse than either pure mode + creates more parameters to tune. The "advisory percentage" tells composers what `#auto` chose; that's enough predictability.
- **Don't redefine Phase 28 articulation envelope:** SAMP-03 multipliers STACK on it (multiplicatively). Per CONTEXT canonical_refs: "Do NOT redefine the baseline." The locked rules are user-visible contracts.
- **Don't wire MIX-02 by mutating SongRenderer:** SongRenderer.MixVoicesToStereoBuffer already handles per-voice pan (line 308-309). The SFZ retrofit should make SfzRenderer EMIT a `Voice` with the right `Pan` field set, NOT mutate the mix stage. Wire it at the SFZ→Voice handoff.
- **Don't trust composer to know overlap rules:** Document in CLAUDE.md that `density × grain > 1.0` means overlap. Trust composer per language philosophy, but make the cost model visible.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| MIDI file I/O | New SMF encoder/decoder | Existing DryWetMidi 8.0.3 (already in csproj) | Not touched in Phase 37 — kept here for orientation |
| Constant-power pan math | New panning helper | `SfzRenderer.ToStereoBufferWithPan` (Phase 33) or `SongRenderer:308-309` formula | Both exist; just call them |
| One-shot stderr advisory | New WarnOnce | `RenderingDiagnostics.WarnOnce(sentinelKey, message)` | Phase 32 pattern; sentinel keyed; same call shape as `[tuning]` advisory |
| PRNG seeding for round-robin/jitter | New `Random` | `ctx.PrngRegistry.NextInt(callSite, "name")` | D-v1.5-06 — required for two-run cmp-clean |
| Articulation envelope generation | New ADSR | `SynthUtils.GenerateArticulationADSR(...)` (Phase 28 helper) | SAMP-03 multiplies on top; Phase 28 baseline is the contract |
| Linear varispeed pitch shift | New resampler | `FileIO.VarispeedResample(buffer, ratio)` (Phase 29) | Still useful for non-`#auto` pitch shifts; existing reference |
| Equal-power crossfade math | New `cos/sin` blend | Existing Phase 33 SFZ loop-crossfade math (441-frame sin/cos blend) | Same formula; SAMP-02 xfin/xfout reuses it |
| RMS-windowed regression test | New baseline harness | `RmsRegressionTests.AssertWavMatchesBaseline(actual, baselinePath)` (Phase 28 SPEC-8) | Validates "perceptually identical" within ±0.5 dB / 100 ms |
| Sample loading from disk | New WAV reader | `FileIO.LoadWavInternal(path)` (Phase 29) | Plan 37-04/05 reuse for new piano/flute samples |
| Trim leading silence on samples | New onset detector | `SampleCache.TrimLeadingSilence(buf)` (Phase 29) | New piano mp layer + flute A4 sample go through same trimmer |
| SFZ parser | New parser | `SfzParser.Parse(...)` (Phase 33) — EXTEND, don't replace | Adds 6 opcodes (seq_position, seq_length, xfin_lovel, xfin_hivel, xfout_lovel, xfout_hivel) to existing 14-opcode whitelist |

**Key insight:** Phase 37's surface is large (11 REQs across DSP, mix, SFZ, samples) but the **reused-vs-new ratio is high** — every plan extends existing infrastructure rather than building parallel paths. The greenfield additions are exclusively the three DSP engines (granular/vocoder/PSOLA) and the supporting Fft/Hps/WindowFunctions utility files. Everything else is "add fields to record + extend switch statement + add manifest entries."

## Common Pitfalls

### Pitfall 1: Phase Vocoder "Phasiness" / Loss of Presence
**What goes wrong:** Naive phase vocoder time-stretch produces a metallic, reverberant artifact called "phasiness" — described in literature as "loss of presence." The output sounds underwater or chorus-like.
**Why it happens:** When phase is propagated independently per FFT bin, vertical phase coherence within a single sinusoid (across bins in its main lobe) is destroyed. The bin-independent phase makes each sinusoid sound like a noisy chord rather than a clean tone.
**How to avoid:** Implement Laroche-Dolson 1999 identity phase locking (Pattern 1). Pick magnitude peaks; lock surrounding bins' phase advance to the peak's phase advance. Reduces phasiness "dramatically."
**Warning signs:** Output sounds metallic/reverby/underwater; pure sine tone in → multi-bin smearing out. Test with a single sustained sine wave at 440 Hz time-stretched 2×.

### Pitfall 2: Transient Smearing in `#vocoder` Mode
**What goes wrong:** Drum hits, plucked-string attacks, vocal consonants get smeared into a "muddy" transient when vocoded.
**Why it happens:** Phase propagation assumes locally stationary harmonics. Transients are by definition non-stationary; the phase math doesn't model their energy spike.
**How to avoid:** Detect transients (HPS or onset-detection), switch to `#psola` for transient frames in `#auto` mode. Per CONTEXT D-37-06, the `#auto` advisory tells composers when this happens.
**Warning signs:** Time-stretched drum loop sounds "blurry"; sharp attacks become soft-onset waves.

### Pitfall 3: PSOLA Octave Errors on Voiced Material
**What goes wrong:** PSOLA detects pitch period as 2× or ½× the true period, causing audible chirps and pitch jumps.
**Why it happens:** Plain autocorrelation has 17% octave-error rate. YIN's cumulative mean normalized difference reduces this to 0.5% by penalizing the τ=0 peak.
**How to avoid:** Use YIN (not raw autocorrelation). Set threshold to 0.1 (YIN default). Validate on a sustained sung note + a sustained piano note before shipping.
**Warning signs:** Single-frequency tone in → output briefly jumps octave then recovers; instability at note transitions.

### Pitfall 4: HPS Median-Filter Kernel Size Mismatch
**What goes wrong:** HPS picks `#psola` for everything (or `#vocoder` for everything) — `#auto` collapses to a single mode.
**Why it happens:** Median-filter kernel sizes (17 frames / 17 bins per Fitzgerald 2010) are tuned for ~46 ms STFT frames at 44.1 kHz. If we use different frame sizes (e.g., composer overrides `frameSize=512` for faster low-latency), the kernels need to scale.
**How to avoid:** Scale kernels with frame size: `horizontalKernel = round(17 × (frameSize / 2048))`. Or document that `frameSize` and HPS kernels are coupled and recommend frameSize ≥ 1024 for `#auto` mode.
**Warning signs:** Advisory percentage is always 0%/100% or 100%/0% regardless of input material.

### Pitfall 5: Granular Density Cost Explosion
**What goes wrong:** Composer writes `(granular buf grain=100ms density=1000Hz jitter=0.5)` → 1000 grains/sec × 100 ms each = 100× overlap; CPU usage explodes; render takes 10× real-time.
**Why it happens:** No hard cap per D-37 deferred ideas (trust composer per language philosophy). Composer doesn't realize density × grain > 1 means overlap.
**How to avoid:** Document the cost model in CLAUDE.md: "CPU cost ≈ density × grain × sampleRate × output-duration." Plan 37-01 publishes this in `audio.flow` doc-comment.
**Warning signs:** Render time >> playback time; CPU at 100% on `granular` call.

### Pitfall 6: Round-Robin Counter Not Reset Across Renders
**What goes wrong:** Two consecutive `renderSong` calls produce different output because the round-robin counter carries state across renders.
**Why it happens:** Counter lives in SfzRenderer instance; without explicit reset at render boundary, two consecutive calls hit different rr1/rr2 cycles.
**How to avoid:** Reset round-robin counter at `renderSong`/`writeWav` entry, same boundary as `PrngRegistry.ResetAtRenderBoundary()` (Phase 36 contract).
**Warning signs:** Two-run cmp-clean determinism test fails on songs using SFZ with round-robin opcodes.

### Pitfall 7: Equal-Power Crossfade Sums to >1.0 Power When Both Layers Present
**What goes wrong:** Adjacent SFZ velocity layers (layer1 with xfout, layer2 with xfin) sum to >1.0 power in the crossfade region → clipping at output.
**Why it happens:** Equal-power preserves total POWER but if BOTH layers play simultaneously their SIGNAL amplitudes can sum to >1.0 even when each individually is below 1.0. This is the classic equal-power-mix problem.
**How to avoid:** Apply a safety headroom factor of 0.707 (= 1/√2) when both layers are in their crossfade band simultaneously. Test on a velocity sweep through the crossfade band.
**Warning signs:** Clipping advisory fires on samples that don't clip when played individually.

### Pitfall 8: PRNG Site Collision Between `granular` and SFZ Round-Robin
**What goes wrong:** Two different stochastic features at the same SourceLocation get the same Random instance → their draws interfere.
**Why it happens:** PrngRegistry keys by `(SourceLocation, generator-name)` — if both granular and round-robin use generator name "granular" (or unnamed), they collide.
**How to avoid:** Use distinct generator names: `"granular_offset"`, `"granular_timing"`, `"sfz_rr_voice0"`, `"sfz_rr_voice1"`. Phase 36 36-01 SUMMARY captures this contract.
**Warning signs:** Adding a granular call near an SFZ instrument changes the SFZ output bytes.

### Pitfall 9: U-Iowa MIS Piano mp Layer Does Not Exist (NEW finding)
**What goes wrong:** Plan 37-04 sets out to add mp + mf layers per D-37-09 "≥4 velocity layers" — discovers the source only has pp/mf/ff.
**Why it happens:** D-37-09 was locked without verifying source availability. CONTEXT.md "Claude's Discretion" line 60 already flagged FLUTE-01 sample picking as "defer to RESEARCH" — the SAME deferral applies to PIANO mp availability and wasn't explicitly called out.
**How to avoid:** Synthesize the mp layer by RMS-interpolation between pp and mf (Pattern 9 Path 1). Or pivot to Path 2 (more pitch points, reframed criterion). Or pivot to Path 3 (different source — re-open D-37-10). Recommend Path 1.
**Warning signs:** Plan 37-04 task "extract mp samples from U-Iowa MIS" — would fail at download time.

### Pitfall 10: Sample-Path Articulation Multiplier Stacks on Synth-Path Too (SAMP-03 scoping)
**What goes wrong:** SAMP-03 multiplier intended only for sample path is also applied to synth path → synth path gets a DIFFERENT articulation envelope than Phase 28 locked rules → breaks all existing tests.
**Why it happens:** If the multiplier is added inside `SynthUtils.GenerateArticulationADSR`, it applies to every caller (both synth synthesizers and `SampledInstrumentRenderer` / `SfzRenderer`).
**How to avoid:** Apply the multiplier OUTSIDE the helper, at the sample-path caller site only. The helper stays Phase 28 verbatim; the caller multiplies its output by the per-articulation curve before applying.
**Warning signs:** Phase 28 RMS regression tests start failing after SAMP-03 lands.

### Pitfall 11: Stretch Factor of Exactly 1.0 Not a No-Op
**What goes wrong:** `(stretch buf 1.0)` runs the full vocoder pipeline → output bytes differ from input by FFT roundoff → breaks identity expectations.
**Why it happens:** No fast-path for factor=1.0.
**How to avoid:** Add explicit `if (factor == 1.0) return buf;` short-circuit. Same as Phase 32's pragma identity short-circuit (DEFER-06 byte-identical regression pattern).
**Warning signs:** Two-run cmp-clean test fails on a script that calls `(stretch buf 1.0)` somewhere.

### Pitfall 12: Voice.Pan = 0 Treated as "Unset" Instead of "Centered"
**What goes wrong:** MIX-02 wire-up reads `voice.Pan` and only applies pan when ≠ 0 (mirroring existing SFZ region.Pan check at line 204). Composer explicitly setting `pan(voice, 0.0)` gets the same path as unset → semantically same outcome but creates a bug-prone branch.
**Why it happens:** Phase 33's `SfzRenderer:204` uses `if (region.Pan != 0.0)` to avoid promoting mono→stereo unnecessarily. MIX-02 should NOT replicate this — synth path always promotes to stereo (line 302).
**How to avoid:** Always apply constant-power split for sample-rendered voices in mix stage. Don't replicate the SFZ region's mono-preservation optimization at the per-voice level.
**Warning signs:** Composer-set `voice.Pan = 0` produces a mono buffer; composer expects stereo center.

## Runtime State Inventory

> Phase 37 is greenfield + extensions — no rename/refactor/migration. **Section skipped.**

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All builds | ✓ | 10.x (per csproj `<TargetFramework>net10.0</TargetFramework>`) | — |
| dotnet CLI | All builds + tests | ✓ | bundled with SDK | — |
| Existing `flow-lang/Samples/{piano,flute}/` | PIANO-01, FLUTE-01 | ✓ | Phase 29 bundle | — |
| U-Iowa MIS download (composer setup) | PIANO-01, FLUTE-01 (composer-side asset drops) | ✗ (composer's machine) | — | `user_setup` block in plan documents the curl URL + checksum |
| VSCO-CE SFZ bundle (composer setup) | DRUM-01 | ✗ (composer's machine) | — | composer points `sfz_root` in `~/.config/flow/config.toml` per Phase 33; existing UX |
| `examples/ragtime/ragtime.flow` | PIANO-01 UAT | ✓ | shipped in v1.4 Phase 34 | — |
| Existing FFT-capable code | Plan 37-01 vocoder | ✗ | — | Hand-roll radix-2 Cooley-Tukey (~80 lines) — standard reference, no external lib |

**Missing dependencies with no fallback:** None — all Phase 37 work is buildable from current repo state. Composer-side asset drops (piano mp synthesis or extraction, flute A4, VSCO drums SFZ root) are documented in `user_setup` blocks per Phase 29 precedent.

**Missing dependencies with fallback:** FFT — hand-rolled per Phase 37 hand-roll-everything stance.

## Validation Architecture

> **Section required** — `workflow.nyquist_validation` defaults to enabled.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit + custom RmsRegressionTests helper (existing Phase 28) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run command | `dotnet test flow-lang.Tests/ --filter "FullyQualifiedName~Phase37"` |
| Full suite command | `dotnet test` (entire test solution) |
| Baseline directory | `flow-lang.Tests/baselines/Phase37/` (new — mirrors `baselines/Phase28/` from Phase 28) |
| RMS tolerance | ±0.5 dB / 100 ms windows per SPEC-8 |

### Phase Requirements → Test Map

| REQ ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| DSP-01 | `(granular buf grain=50ms density=20Hz jitter=0.3 windowing=#hann)` returns a Buffer composable with reverb/gain/pan/filter | integration | `dotnet test --filter "GranularSynthesisTests"` | ❌ Wave 0 |
| DSP-01 | Hann/Gaussian/Tukey windowing options produce DIFFERENT output (no two identical) | unit | `dotnet test --filter "WindowFunctionTests"` | ❌ Wave 0 |
| DSP-01 | Granular jitter PRNG seeded via PrngRegistry — two-run cmp-clean | integration | `dotnet test --filter "GranularDeterminismTests"` | ❌ Wave 0 |
| DSP-02 | `(stretch buf 2.0 mode=#vocoder)` doubles audio length within ±1 sample | integration | `dotnet test --filter "StretchVocoderTests"` | ❌ Wave 0 |
| DSP-02 | `(stretch buf 2.0 mode=#psola)` preserves transients (drum hit onset position drift ≤ 5 ms) | integration | `dotnet test --filter "StretchPsolaTransientTests"` | ❌ Wave 0 |
| DSP-02 | `(stretch buf 2.0 mode=#auto)` emits stderr `[stretch] mode=#auto picked: X% vocoder / Y% psola` exactly once per call | integration | `dotnet test --filter "StretchAutoAdvisoryTests"` | ❌ Wave 0 |
| DSP-02 | `(stretch buf 1.0)` returns input byte-for-byte (fast-path identity) | unit | `dotnet test --filter "StretchIdentityTests"` | ❌ Wave 0 |
| DSP-03 | `(pitchShift buf +5st)` shifts pitch by 5 semitones, preserves duration within ±1 sample | integration | `dotnet test --filter "PitchShiftTests"` | ❌ Wave 0 |
| DSP-03 | `loadWav` varispeed path unaffected — Phase 27 byte-identical baseline holds | regression | `dotnet test --filter "LoadWavVarispeedRegression"` | ✓ exists (Phase 27) |
| MIX-01 | Existing synth-path pan baseline pinned via RMS regression | regression | `dotnet test --filter "Phase37MixSynthPathRegression"` | ❌ Wave 0 |
| MIX-02 | SFZ-rendered voice with `voice.Pan = 0.7` produces stereo with right-louder-than-left | integration | `dotnet test --filter "SfzPanRetrofitTests"` | ❌ Wave 0 |
| MIX-02 | SFZ + per-region pan (Phase 33) + per-voice pan (Phase 37) compose correctly (multiplicative or additive — locked in plan-phase) | integration | `dotnet test --filter "SfzPanCompositionTests"` | ❌ Wave 0 |
| SAMP-01 | `seq_position`/`seq_length` opcodes parsed; multiple triggers on same key produce DIFFERENT samples (round-robin) | integration | `dotnet test --filter "SfzRoundRobinTests"` | ❌ Wave 0 |
| SAMP-01 | Round-robin sequence deterministic across two consecutive renders (voice ordinal seed) | integration | `dotnet test --filter "SfzRoundRobinDeterminismTests"` | ❌ Wave 0 |
| SAMP-02 | `xfin_lovel`/`xfin_hivel` opcodes parsed; velocity within crossfade band produces NON-zero output from BOTH layers | integration | `dotnet test --filter "SfzVelocityCrossfadeTests"` | ❌ Wave 0 |
| SAMP-02 | Hard-switch fallback when xfin/xfout absent matches Phase 33 byte-identical baseline | regression | `dotnet test --filter "SfzHardSwitchRegression"` | ❌ Wave 0 |
| SAMP-03 | Per-articulation envelope multiplier active on sample path only — synth path Phase 28 regression unaffected | regression | `dotnet test --filter "Phase28ArticulationRegression"` | ✓ exists (Phase 28) |
| SAMP-03 | Sample-path staccato has measurably more harmonic energy than pre-multiplier baseline (closes Phase 29 v1.5 gap) | integration | `dotnet test --filter "SampledStaccatoEnergyTests"` | ❌ Wave 0 |
| PIANO-01 | Piano `SampleCache` has ≥4 velocity layers per pitch point (pp/mp/mf/ff) after eager-load | unit | `dotnet test --filter "PianoSampleCacheLayersTest"` | ❌ Wave 0 |
| PIANO-01 | `release=` named arg overrides default; release=2.0 produces audible tail at t=1.5s past authored end | integration | `dotnet test --filter "PianoReleaseKnobTests"` | ❌ Wave 0 |
| PIANO-01 | Ragtime UAT iteration #2 — composer subjective approval — RMS regression baseline locked after approval | HUMAN-UAT (not in CI) | manual: composer listens, signs off, lock baseline | `37-HUMAN-UAT.md` per D-37-12 |
| FLUTE-01 | Flute `SampleCache` has ≥3 sample points after Plan 37-05 (G4, [A4 OR D5], G5) | unit | `dotnet test --filter "FluteSampleCacheTests"` | ❌ Wave 0 |
| FLUTE-01 | D5 crossover gap closed — note at D5 timbre RMS-matches the nearer sample point within ±0.5 dB | integration | `dotnet test --filter "FluteD5CrossoverTests"` | ❌ Wave 0 |
| DRUM-01 | `(loadSfz #drums)` resolves to `GM-StylePerc.sfz` and parses without error | integration | `dotnet test --filter "SfzDrumsLoadTest"` | ❌ Wave 0 |
| DRUM-01 | Drum note pitch-shift uses `#auto` PSOLA path for transient kits (kick=36, snare=38) | integration | `dotnet test --filter "DrumPitchShiftAutoTests"` | ❌ Wave 0 |
| GLOBAL | Two-run cmp-clean determinism preserved on full v1.4 example suite + new Phase 37 examples | regression | `dotnet test --filter "TwoRunDeterminismTests"` | ✓ exists (Phase 18/25/27/28/29/33/36 pattern) |
| GLOBAL | SPEC-8 RMS regression baselines (±0.5 dB / 100 ms) committed for any behavior-changing tests | regression | `dotnet test --filter "Phase37RmsRegression"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Phase37&Category!=Slow"` (≤30 s — unit + lightweight integration)
- **Per wave merge:** `dotnet test --filter "FullyQualifiedName~Phase37"` (≤2 min — full Phase 37 suite)
- **Phase gate:** Full `dotnet test` green before `/gsd:verify-work`

### Wave 0 Gaps

The following files need to exist before Wave 1+ implementation (planner allocates to Wave 0 tasks):

- [ ] `flow-lang.Tests/Integration/Phase37/GranularSynthesisTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/WindowFunctionTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/GranularDeterminismTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/StretchVocoderTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/StretchPsolaTransientTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/StretchAutoAdvisoryTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/StretchIdentityTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/PitchShiftTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/SfzRoundRobinTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/SfzRoundRobinDeterminismTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/SfzVelocityCrossfadeTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/SfzHardSwitchRegression.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/SfzPanRetrofitTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/SfzPanCompositionTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/SampledStaccatoEnergyTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/PianoSampleCacheLayersTest.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/PianoReleaseKnobTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/FluteSampleCacheTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/FluteD5CrossoverTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/SfzDrumsLoadTest.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/DrumPitchShiftAutoTests.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/Phase37MixSynthPathRegression.cs`
- [ ] `flow-lang.Tests/Integration/Phase37/Phase37RmsRegression.cs`
- [ ] `flow-lang.Tests/baselines/Phase37/` directory
- [ ] `flow-lang.Tests/fixtures/Phase37/` directory (test WAV fixtures: sustained sine, drum hit, mixed material)

## Security Domain

Phase 37 is pure audio DSP + sample loading — no network, no auth, no user-input parsing beyond existing SFZ parser surface. Applies ASVS V5 (Input Validation) primarily.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | yes | SfzParser charitable-fallback already in place (Phase 33); extend to new opcodes with same WarnOnce pattern |
| V6 Cryptography | no | — |
| V12 File and Resources | yes | Sample file paths from SFZ + composer `release=`/`grain=` knob ranges |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed SFZ opcode value (e.g., `seq_length=999999999`) | DoS | SfzParser charitable-fallback pattern (existing) — log + use spec default; clamp `seq_length` to spec max 100 |
| Pathological granular density (`density=1e9Hz`) | DoS | Document cost model; no hard cap per language philosophy; composer self-rescues |
| Negative stretch factor or zero | Tampering | Validate `factor > 0.0` at builtin entry; throw with clear message |
| Sample path traversal in user SFZ files | Tampering | Existing Phase 33 path resolution uses `sfz_root` as anchor + `Path.Combine` — no `..` escape per Phase 33 SPEC-3 |
| FFT buffer overflow on non-power-of-2 frame size | DoS | Validate `frameSize` is power of 2 at builtin entry; auto-pad and warn if not |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | HPS transient threshold default 0.3 normalized is composer-ergonomic | Pattern 3, D-37-07 | Low — composer overrides via `transientThreshold=` named arg; default chosen via mid-range of Fitzgerald's normalized examples |
| A2 | Tukey α=0.5 default flat-top is composer-ergonomic for granular | Pattern 4 | Low — composer can build window themselves if defaults inadequate; alternative α=0.25 if too smooth |
| A3 | Gaussian σ=0.4 default avoids audible endpoint discontinuity | Pattern 4 | Low — Krzyzaniak granular synthesis reference uses σ=0.3-0.4 as a working range; values above 0.5 cause pops |
| A4 | PIANO-01 default `release=1.5s` matches ragtime sustain | Pattern 8 | MEDIUM — locked only after ragtime UAT iteration #2; planner sets initial value with low confidence, UAT confirms or shifts |
| A5 | Synthesized mp piano layer (RMS-interpolation pp+mf at α=0.6) is perceptually distinct enough from pp and mf | Pattern 9 Path 1, Pitfall 9 | HIGH — this is the recommended resolution for the U-Iowa "no mp" finding; UAT decides if it's enough or escalation needed |
| A6 | FLUTE-01 should add A4 not D5 (varispeed coverage of low register) | Pattern 10 | LOW — composer-validated in UAT or A/B; both files small enough to ship both |
| A7 | DRUM-01 honors composer pitch and stderr-advises on shifts >12 semitones | Pattern 11 | LOW — Phase 32 advisory precedent makes this composable; alternative (snap to GM grid) is a one-line config flag |
| A8 | SAMP-03 Option A scalar ADSR multiplier closes the staccato perceptual gap | Pattern 7 | MEDIUM — UAT decides; Option B full curve overlay is the escalation if Option A insufficient |
| A9 | YIN voicing threshold 0.1 works on music (not just speech) | Pattern 2 | LOW — YIN paper validates on music; community implementations use same default |
| A10 | Granular per-FlowEngine cache uses same lifetime as Phase 29 SampleCache | Pattern 4 | LOW — Phase 29 precedent is well-tested; no new cache invalidation logic needed |

**Items needing user confirmation before plan-phase locks them:** A4 (release default), A5 (synthesized mp acceptability), A8 (SAMP-03 multiplier shape). All three are composer-perceptual and should be discussed at `/gsd:discuss-phase` if planner wants to escalate.

## Open Questions

1. **PIANO-01 mp resolution path** (Pitfall 9 / Pattern 9)
   - What we know: U-Iowa MIS only ships pp/mf/ff; D-37-09 says "≥4 layers"; D-37-10 locks the source
   - What's unclear: Whether RMS-interpolated mp is perceptually distinct enough OR whether to escalate to Path 2 (more pitch points) or Path 3 (re-open D-37-10)
   - Recommendation: Plan 37-04 ships Path 1; UAT iteration #2 decides escalation. Document in plan SUMMARY which path closed.

2. **SAMP-03 multiplier shape — scalar or curve?**
   - What we know: Phase 29 v1.5 follow-up flagged "staccato sampled path sounds thinner than synth path"; SAMP-03 is the closure
   - What's unclear: Whether scalar ADSR multipliers (Option A — low risk) are enough OR full curve overlay (Option B — higher fidelity) needed
   - Recommendation: Plan-phase picks Option A; SUMMARY documents escalation criterion

3. **DRUM-01 dependency on DSP-02/03 — strict or weak?**
   - What we know: D-37-14 locks "strict dependency on DSP-02/03 for transient-preserving pitch shift"
   - What's unclear: Whether composer-authored drum notes typically land at GM percussion MIDI numbers (which are recorded — no pitch shift needed) — if so, DSP-02/03 dependency may be rarely exercised
   - Recommendation: Plan 37-06 honors composer pitch; emits stderr advisory on >12 semitone shifts. Re-evaluate D-37-14 if it turns out drums rarely pitch-shift in practice

4. **MIX-02 + Phase 33 per-region pan composition**
   - What we know: SFZ regions can have `pan=` opcode (per-region pan, Phase 33); voices can have `voice.Pan` (per-voice pan, Phase 37)
   - What's unclear: Multiplicative (compose pan angles) or additive (sum then clamp to [-1, 1]) — both have legitimate semantics
   - Recommendation: Plan-phase decides; suggest additive-with-clamp because composer's per-voice pan is "where the source instrument sits in the stereo field" and per-region pan is "intrinsic to the patch's stereo image"; they should add

5. **`#auto` advisory granularity — when is it emitted?**
   - What we know: D-37-06 — one-shot per call, summarizes the breakdown
   - What's unclear: Per `(stretch ...)` call site or per render? If composer calls `(stretch buf 2.0)` inside a loop processing 100 buffers, does each iteration emit?
   - Recommendation: Per call (since RenderingDiagnostics.WarnOnce is sentinel-keyed by `{call_site}:{summary}`, the sentinel changes per call — composer sees one advisory per *summary*; same summary inside a loop dedups naturally)

## Sources

### Primary (HIGH confidence)
- Existing codebase audit:
  - `flow-lang/StandardLibrary/Audio/SongRenderer.cs:280-338` — MIX-01 synth-path pan shipped (D-37-15 confirmed)
  - `flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs:47-70` — per-region SFZ pan exists; per-voice missing (D-37-16 confirmed)
  - `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs:202-206,395-423` — `ToStereoBufferWithPan` helper for MIX-02 reuse
  - `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs:79-152` — current 500 ms tail; PIANO-01 release= replaces
  - `flow-lang/StandardLibrary/Audio/SampleCache.cs:47-58` — InstrumentManifest piano = 5 pitches × pp/ff; PIANO-01 expands
  - `flow-lang/StandardLibrary/Audio/SynthUtils.GenerateArticulationADSR` — Phase 28 baseline SAMP-03 stacks on
  - `flow-lang/Runtime/PrngRegistry.cs:78-126` — D-v1.5-06 PRNG contract for granular jitter + round-robin
  - `flow-lang/Diagnostics/RenderingDiagnostics.cs:19-29` — WarnOnce contract for `[stretch]` advisory
  - `flow-lang/Parsing/Parser.cs:1283-1320` — named-arg call syntax (Phase 36 Plan 36-02)
  - `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs:440-499` — SFZ parse + charitable-fallback pattern SAMP-01/02 extends
  - `flow-lang/sfz.flow` (full content) — 19-entry GM dict DRUM-01 adds to
  - `flow-lang/Samples/piano/` (5 .wav files: C2-C6 × pp/ff) — current Phase 29 bundle
  - `flow-lang/Samples/flute/` (G4.wav, G5.wav) — current Phase 29 bundle
- VSCO-CE source verification:
  - github.com/sgossner/VSCO-2-CE branch `SFZ` tree (via `gh api`) — 75 .sfz files including `GM-StylePerc.sfz`
  - `GM-StylePerc.sfz` content (via web fetch) — uses `seq_position=1/2 seq_length=2` plus cascading velocity layers
- U-Iowa MIS source verification:
  - https://theremin.music.uiowa.edu/MISpiano.html — piano: pp/mf/ff (NO mp); range Bb0-C8
  - https://theremin.music.uiowa.edu/MISflute.html — flute: B3-Db7 chromatic at pp/mf/ff, vibrato/non-vibrato
- CONTEXT.md decisions D-37-01..16 — verbatim authority for plan layout and locked choices

### Secondary (MEDIUM confidence — academic + community DSP references)
- Laroche & Dolson 1999 "New phase-vocoder techniques" — https://www.ee.columbia.edu/~dpwe/papers/LaroD99-pvoc.pdf — identity phase-locking
- Fitzgerald 2010 "Harmonic/Percussive Separation Using Median Filtering" — https://dafx10.iem.at/papers/DerryFitzGerald_DAFx10_P15.pdf — HPS algorithm
- de Cheveigné & Kawahara 2002 "YIN, a fundamental frequency estimator for speech and music" — pitch detector for PSOLA
- Stanford CCRMA "Choice of Hop Size" — https://ccrma.stanford.edu/~jos/sasp/Choice_Hop_Size.html — hop size = frameSize/4 for Hann window
- Cycling '74 "The Phase Vocoder — Part I" — https://cycling74.com/tutorials/the-phase-vocoder-%E2%80%93-part-i — practical implementation walkthrough
- Průša & Holighaus 2022 "Phase Vocoder Done Right" — arxiv:2202.07382 — modern phase vocoder consistency analysis
- Lehtonen et al. 2007 "Analysis and modeling of piano sustain-pedal effects" JASA — https://pubmed.ncbi.nlm.nih.gov/17927438/ — release tail timing
- Curtis Roads 2001 "Microsound" (MIT Press) — granular synthesis canonical reference (citation only; not fetched)
- Krzyzaniak "Audio Synthesis: Window Functions" — https://michaelkrzyzaniak.com/AudioSynthesis/2_Audio_Synthesis/11_Granular_Synthesis/1_Window_Functions/ — Hann/Gaussian/Tukey in granular context

### Tertiary (verified against authoritative source above; documented for traceability)
- SFZ Format spec — https://sfzformat.com/opcodes/seq_position/ + /seq_length/ + /xfin_hivel/ + /xfout_hivel/ + /xf_velcurve/ — opcode semantics
- SoundOnSound "Linear or constant-power crossfades?" — https://www.soundonsound.com/sound-advice/q-should-use-linear-or-constant-power-crossfades — when to use each curve
- librosa HPSS docs — https://librosa.org/doc/main/auto_examples/plot_hprss.html — reference HPS kernel defaults
- VSCO-2-CE Readme — github.com/sgossner/VSCO-2-CE — license confirmation (CC0)
- TD-PSOLA reference impl — github.com/sannawag/TD-PSOLA — voiced/unvoiced gate pattern

## Metadata

**Confidence breakdown:**
- Phase vocoder algorithm: HIGH — Laroche-Dolson is textbook; Stanford CCRMA / multiple papers cross-verify parameters
- PSOLA algorithm: HIGH — YIN pitch detection is well-established; TD-PSOLA reference implementations available
- HPS transient detector: HIGH for algorithm + MEDIUM for default threshold (literature gives range, exact value is ASSUMED — A1)
- Granular synthesis: HIGH for algorithm + MEDIUM for defaults (ASSUMED — A2, A3)
- SFZ opcode semantics (SAMP-01/02): HIGH — official format spec verified for all 6 new opcodes
- MIX-01 audit conclusion: HIGH — code read confirms D-37-15 (line 308-309 already applies pan)
- MIX-02 audit conclusion: HIGH — code read confirms D-37-16 (region.Pan exists, voice.Pan not wired)
- PIANO-01 source coverage: HIGH for finding + RECOMMENDATION confidence MEDIUM (A5 — synthesized mp untested)
- FLUTE-01 sample point: MEDIUM — recommendation justified, alternative is ship both
- DRUM-01 VSCO route: HIGH — VSCO-CE GM-StylePerc.sfz verified; seq_position/seq_length already exercised
- SAMP-03 multiplier shape: MEDIUM — Option A vs Option B unresolved (A8); UAT decides
- Validation architecture: HIGH — Wave 0 gaps clearly enumerated; baselines pattern is Phase 28 precedent

**Research date:** 2026-05-22
**Valid until:** 2026-06-22 (30 days — DSP algorithms are stable; SFZ format stable; only risk window is if VSCO-CE GM-StylePerc.sfz path changes in a new upstream release)
