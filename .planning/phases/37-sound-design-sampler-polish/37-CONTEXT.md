# Phase 37: Sound Design + Sampler Polish - Context

**Gathered:** 2026-05-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 37 ships the v1.5 sound-design pillar across four surfaces:

1. **Greenfield DSP primitives** — `granular`, `stretch`, `pitchShift` as first-class composable builtins (DSP-01..03).
2. **Stereo mix retrofit** — per-voice `Pan` already shipped on synth path (audit-confirmed below); SFZ path needs the wire-up (MIX-01 verification + MIX-02 implementation).
3. **SFZ sampler polish** — round-robin opcodes, equal-power velocity-layer crossfade, per-articulation envelope multipliers (SAMP-01..03).
4. **Sample asset expansion** — warmer piano via ≥4 velocity layers + sustain pedal sim, additional flute sample point to close the D5 crossover gap, sampled drums (lifting Phase 29 SPEC D-02 restriction) using transient-preserving pitch shift (PIANO-01, FLUTE-01, DRUM-01).

In scope: 11 requirements (DSP-01..03, MIX-01..02, SAMP-01..03, PIANO-01, FLUTE-01, DRUM-01).
Out of scope: live coding (Phase 38), notation export (Phase 39), MIDI/Link sync (Phase 40), reach + closer (Phase 41).

</domain>

<decisions>
## Implementation Decisions

### Phase Subdivision (Area 1)

- **D-37-01:** Plans use a **horizontal-layer** layout (4-5 engine plans + 3 asset plans + closer). DSP foundation is built first, then features, then assets, then closer. Vertical (one-plan-per-REQ) and pure-hybrid were rejected.
- **D-37-02:** **Plan 37-01 absorbs all shared DSP foundations.** Window function helpers (Hann/Gaussian/Tukey), FFT helpers, HPS transient detector, PrngRegistry source-location keys, granular core utilities — all land in 37-01. Plan 37-02 (stretch + pitchShift) consumes 37-01's foundation. No dedicated 37-00 utilities plan; no per-plan duplication.
- **D-37-03:** **MIX-01 verification folds into Plan 37-03 (SFZ retrofit + stereo pan).** Single plan owns the whole stereo pan story: adds RMS-windowed regression for the already-shipped synth-path pan AND implements the MIX-02 SFZ retrofit. Standalone verification plan rejected as audit-trail overkill.
- **D-37-04:** **Sample assets ship as three independent plans** (37-04 PIANO-01, 37-05 FLUTE-01, 37-06 DRUM-01). Each carries its own `user_setup` block (Phase 29 precedent) so engine work isn't blocked by sample curation, and each asset can stall independently.
- **D-37-05:** **Wave/plan order:** Plans 37-01 → 37-02 (depends on 37-01) → 37-03 (independent of DSP), 37-04 (PIANO, independent), 37-05 (FLUTE, independent) → 37-06 (DRUM, **depends on 37-02** for transient-preserving pitch shift) → 37-07 closer. Plans 37-03/04/05 can run parallel after 37-01 lands.

### Mode #auto Predictability (Area 2)

- **D-37-06:** **One-shot stderr advisory per call** summarizing the #auto breakdown — e.g. `[stretch] mode=#auto picked: 73% vocoder / 27% psola across 1024 frames`. Mirrors the Phase 32 `[tuning] unmapped MIDI keys` precedent. Per-frame chatty advisory rejected (too noisy for long buffers). Silent operation rejected (composers need predictability when #auto makes the call).
- **D-37-07:** **HPS transient threshold has a locked default + `transientThreshold=` named-arg override.** Claude's discretion picks the default (median-filter-based, ~0.3 normalized, documented). Composers who need different boundary behavior pass `transientThreshold=`. Adaptive per-buffer auto-tune rejected (harder to reason about, adds compute cost).
- **D-37-08:** **All modes accept tuning knobs** — not just `#auto`. `#vocoder` gets `frameSize=`/`hopSize=`/`overlap=`, `#psola` gets `pitchPeriod=`/`windowSize=`, `#auto` gets `transientThreshold=` (and inherits the underlying mode knobs when chosen). Maximum composer control; composer ergonomics first. Sealed-modes design rejected.

### PIANO-01 "Warmer Timbre" (Area 3)

- **D-37-09:** **Warmth = velocity layers + release tails.** Two locked levers from the four candidates: (1) expand to **≥4 velocity layers per pitch point** (currently 2: pp + ff — add mp + mf), (2) **longer release tails / sustain pedal sim**. EQ shaping and sympathetic-string resonance deferred to v1.6 (in case ≥4 layers + release tuning alone closes the ragtime UAT gap).
- **D-37-10:** **Sample source: University-of-Iowa MIS** (current Phase 29 bundle). Re-extract pp/mp/mf/ff from the same source — keeps the bundled .wav path, no SFZ runtime needed. VSCO-CE piano path remains available via the Phase 33 SFZ surface for composers who want VSCO piano explicitly, but PIANO-01 ships through the bundled path.
- **D-37-11:** **Release control via `release=` named arg with smart default.** Locked default matches ragtime sustain expectations; composer overrides per call. Locked-only and per-articulation-multiplier (SAMP-03 coupling) both rejected — release is a primary composer-facing knob and shouldn't require diving into articulation envelopes for a global tweak.
- **D-37-12:** **PIANO-01 UAT closure: composer (user) listens to `examples/ragtime/ragtime.flow` rerendered with the warmth levers active and gives subjective approval.** Same shape as Phase 33 SFZ UAT and ragtime UAT iteration #1. Locked into `37-HUMAN-UAT.md` (composer-curated, NOT in CI). RMS-windowed regression test added at close-out to pin the warm baseline against future regressions.

### DRUM-01 Sample Source (Area 4)

- **D-37-13:** **VSCO Community Edition drum kit** (CC-BY 4.0, consistent with Phase 33 SFZ source). Routed via the SFZ surface (`renderSong song "sampler:drums"` / explicit `Sfz` binding), NOT the bundled .wav path. No drum-sample directory under `flow-lang/Samples/drums/` — the SFZ patch lives wherever the composer points `sfz_root` in `~/.config/flow/config.toml`. Curated CC0 bundle and hybrid synth+sample paths both rejected (license posture + bundle-size + consistency with Phase 33).
- **D-37-14:** **DRUM-01 strictly depends on DSP-02/DSP-03** (Plan 37-02). Plan 37-06 (DRUM) ships after Plan 37-02 (stretch + pitchShift) lands. Drums route pitch shift through the `#auto` pipeline (PSOLA for transients, vocoder for sustain). Varispeed fallback and "no pitch shift at all" both rejected — clean dependency on the new DSP infrastructure is the point of bundling DRUM-01 into Phase 37 alongside DSP-02/03.

### Pre-Plan Audit Findings (D-v1.5-09 mandatory)

- **D-37-15:** **MIX-01 (per-voice synth-path pan) is already shipped.** Audit confirmed `flow-lang/StandardLibrary/Audio/SongRenderer.cs:308-309` applies the locked constant-power formula `panAngle = (voice.Pan + 1.0) * 0.25 * π` per voice, with references to "D-05, D-08 bug fix" tying it to earlier-phase work. MIX-01 scope reduces to verification + RMS regression coverage; no new synth-path code. Plan 37-03 folds this in (per D-37-03).
- **D-37-16:** **MIX-02 (SFZ retrofit) is real work.** `SfzRegion.Pan` field exists and `SfzRenderer.cs:202-206` applies constant-power split — BUT this is per-REGION pan from the SFZ file's opcodes, not per-VOICE pan from Flow composer code. The retrofit must wire Flow's `voice.Pan` attribute into the SFZ render path before mixdown.

### Claude's Discretion

- HPS transient threshold default value (~0.3 normalized — locked in 37-RESEARCH.md after literature review).
- Granular density and grain-length defaults beyond the spec'd `density=20Hz`/`grain=50ms` floor — choose reasonable composer-ergonomic values, document.
- Exact mode-specific knob names (`frameSize=` vs `windowLength=`, etc.) per audio-DSP convention.
- FLUTE-01 sample-point pick — D5 vs A4 vs both — defer to RESEARCH (D-v1.5-08 hints D5, but A4 may also close the crossover gap; pick during plan-phase based on the actual sample availability in U-Iowa MIS).
- Granular density/grain caps — trust composer per the language philosophy (no hard cap); document the cost model in CLAUDE.md so composers know what they're paying for.
- SAMP-03 per-articulation envelope multiplier shape — single ADSR scalar vs full envelope curve — pick during plan-phase based on what closes the "staccato sampled path sounds thinner than synth path" gap from Phase 29 v1.5 follow-ups.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 37 ROADMAP + Requirements
- `.planning/ROADMAP.md` §"Phase 37: Sound Design + Sampler Polish" — phase goal, success criteria, REQ list, dependencies, pre-plan audit citation (D-v1.5-09)
- `.planning/REQUIREMENTS.md` §"DSP Primitives", §"Stereo Pan", §"Sampler Polish", §"Sampler Asset Bundle" — DSP-01..03, MIX-01..02, SAMP-01..03, PIANO-01, FLUTE-01, DRUM-01 detailed surfaces
- `.planning/PROJECT.md` §"Constraints" — .NET 10, Linux primary, minimal dependencies, real-time audio constraints
- `.planning/MILESTONES.md` v1.5 entry — milestone context and post-v1.4 carryover list

### Locked v1.5 Milestone Decisions (apply to Phase 37)
- **D-v1.5-03:** RubberBand rejected — hand-rolled phase vocoder for DSP-02/03 (`.planning/MILESTONES.md` or `.planning/milestones/v1.5/`)
- **D-v1.5-06:** All PRNG routed through `flow-lang/Runtime/PrngRegistry.cs` (Phase 36 Plan 36-01 SUMMARY captures the existing keying contract). Granular jitter PRNG MUST follow this.
- **D-v1.5-08:** MuseScore reference consumer (not directly Phase 37, but the ragtime UAT precedent is shared).
- **D-v1.5-09:** Pre-plan audit required at CONTEXT spawn — captured above (D-37-15/D-37-16).
- **SPEC-8:** RMS-windowed regression tolerance ±0.5 dB / 100ms — applies to PIANO-01 UAT close-out baseline and any behavior-changing tests for MIX-01/02.

### Existing Code (Audit Citations)
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs:228, 259, 308-309` — per-voice synth-path pan already shipped (audit anchor for D-37-15).
- `flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs:47, 70` and `SfzRenderer.cs:202-206, 409` — per-region SFZ pan in place; per-voice wire-up missing (D-37-16).
- `flow-lang/StandardLibrary/Audio/PanningFunctions.cs` — `pan(Buffer, Double)` builtin (mono→stereo promotion, constant-power). Reference pattern for buffer-level pan operations.
- `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` — Phase 29 sample-based instrument render path. DRUM-01 extends this surface (or routes around it via SFZ — see D-37-13).
- `flow-lang/Samples/{piano,brass,sax,strings,flute,bell}/` — current Phase 29 bundled samples (U-Iowa MIS). PIANO-01 expands the piano layout; FLUTE-01 expands the flute layout; DRUM-01 does NOT add a `drums/` subdirectory (D-37-13 routes via SFZ).
- `flow-lang/Samples/CREDITS.md` + per-instrument `LICENSE.md` — attribution pattern PIANO-01 + FLUTE-01 must extend for any added sample points.

### Phase 28 Articulation Baseline (SAMP-03 coupling)
- `flow-lang/StandardLibrary/Audio/SynthUtils.GenerateArticulationADSR` — Phase 28's locked articulation envelope. SAMP-03 multipliers stack multiplicatively ON TOP of this baseline for the sample path. Do NOT redefine the baseline.
- `CLAUDE.md` §"Locked articulation rules" — staccato 25% + sustain=0 + release×0.5, marcato 25% + accent velocity, tenuto 100% + release×1.2, legato 110% + crossfade, accent +0.30, sforzando 100% + 1.5×→1.0× envelope spike over first 15% of frames.

### Phase 29/33 Sampler Surfaces (DRUM-01 + asset infrastructure)
- `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` — common-subset SFZ opcode parser. SAMP-01/02 extends with `seq_position`/`seq_length` + `xfin_lovel`/`xfin_hivel`/`xfout_*`.
- `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` — SFZ render path. SAMP-03 adds per-articulation envelope multiplier; MIX-02 adds per-voice pan wire-up.
- `flow-lang/StandardLibrary/Audio/Sampler/SampleCache.cs` (or equivalent in 29-SUMMARYs) — per-FlowEngine sample cache. PIANO-01 + FLUTE-01 layer expansion must cache new layers; DRUM-01 follows SFZ-cache pattern instead.

### Phase 36 Patterns (downstream pattern reuse)
- `.planning/phases/36-sequence-algebra-generative/36-01-SUMMARY.md` — PrngRegistry contract for granular jitter PRNG (D-v1.5-06).
- `.planning/phases/36-sequence-algebra-generative/36-02-SUMMARY.md` — Named-argument call syntax for the `windowing=`/`mode=`/`grain=`/`density=`/`jitter=`/`transientThreshold=`/`release=` knobs.
- `.planning/phases/36-sequence-algebra-generative/36-06-SUMMARY.md` — `MarkovModel` reference-identity value type pattern (relevant if granular/stretch grow first-class model types; likely not needed for Phase 37 but the precedent is documented).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **Constant-power panning math**: `SongRenderer.cs:308-309` already applies the locked formula. MIX-02 SFZ retrofit can reuse the same math (extract into a helper if not already in `Panner.cs`).
- **`pan(Buffer, Double)` builtin**: `PanningFunctions.cs` is a reference for buffer-level pan operations — useful if DSP-01..03 ever need pan-aware buffer manipulation.
- **`Panner.Apply()`**: Helper used by the `pan` builtin. Likely the right anchor for MIX-02 retrofit's per-voice mono→stereo→pan path.
- **U-Iowa MIS source**: Already curated and licensed under `flow-lang/Samples/{piano,brass,sax,strings,flute,bell}/`. PIANO-01 + FLUTE-01 re-extract from this source — no new license review needed.
- **SFZ common-subset parser**: SAMP-01/02 extends an existing parser, doesn't write a new one.
- **Phase 28 articulation envelope**: SAMP-03 multiplies on top — no re-implementation needed.
- **`SampleCache`**: Per-FlowEngine sample cache from Phase 29; PIANO-01 + FLUTE-01 layer expansion plugs into the existing cache contract.

### Established Patterns
- **Named-arg surface**: All Phase 37 builtins use Phase 36-02's named-arg call form (`grain=50ms density=20Hz` etc.) — composer ergonomics; positional args remain as the back-compat path.
- **PrngRegistry seeding**: All stochastic DSP (granular jitter, eventually round-robin via SAMP-01) routes through `PrngRegistry` keyed by `(SourceLocation, generator-name)` — preserves two-run cmp-clean determinism per D-v1.5-06.
- **`#symbol` mode enum**: `#hann`/`#gaussian`/`#tukey` for windowing, `#vocoder`/`#psola`/`#auto` for stretch/pitch-shift modes. Phase 26.1 Symbol type — strict reference equality, NOT String.
- **Stderr advisory shape**: One-shot per call, prefixed `[stretch]`/`[pitchShift]` (D-37-06 follows the Phase 32 `[tuning]` precedent).
- **Composer-curated asset gates**: Phase 29 introduced the `user_setup` block pattern in PLANs (composer drops .wav files into `flow-lang/Samples/{instrument}/`). PIANO-01 + FLUTE-01 follow this pattern. DRUM-01 does NOT (routes via SFZ — D-37-13).
- **HUMAN-UAT pattern**: `37-HUMAN-UAT.md` (composer-curated, NOT in CI). PIANO-01 closure (D-37-12) lands here.

### Integration Points
- **`FlowEngine.cs`**: New DSP builtins register here. `granular`, `stretch`, `pitchShift` join the existing audio builtin surface.
- **`audio.flow`** (or new `dsp.flow`): Composer-facing module that exposes the new builtins. Decide during plan-phase whether to extend `audio.flow` or split into a `@dsp` stdlib module (likely extend — keeps the import surface flat).
- **`Voice.Pan`**: Already exists per audit (D-37-15). MIX-02 plugs into this from the SFZ render path.
- **`SongRenderer.AdditiveMix`**: Reads `voice.Pan` to apply per-voice constant-power split for synth path. MIX-02 mirrors this for SFZ-rendered voices before they hit the same additive-mix stage.
- **`AudioBuffer` (stereo, interleaved LRLRLR)**: Existing buffer format. Granular/stretch/pitchShift output stereo buffers when input is stereo, mono when input is mono. Document the channel-preservation contract.

</code_context>

<specifics>
## Specific Ideas

- **Granular API shape (locked in REQ DSP-01):** `(granular buf grain=50ms density=20Hz jitter=0.3 windowing=#hann)`. Returns a Buffer composable with `reverb`/`gain`/`pan`/`filter`. Jitter routes through PrngRegistry.
- **Stretch API shape (locked in REQ DSP-02):** `(stretch buf factor mode=#auto)`. Modes: `#vocoder` / `#psola` / `#auto`. Factor 1.0 = identity, 2.0 = double length, 0.5 = half length, no pitch change.
- **PitchShift API shape (locked in REQ DSP-03):** `(pitchShift buf cents mode=#auto)`. Cents-precision (positive = up, negative = down), no time change. `Cent` first-class type already exists.
- **`#auto` stderr breakdown shape:** `[stretch] mode=#auto picked: 73% vocoder / 27% psola across 1024 frames` (D-37-06).
- **`mode=#auto` HPS threshold default value:** Locked default + `transientThreshold=` named-arg override (D-37-07). Default to be set in 37-RESEARCH.md.
- **Mode-specific knobs:** All modes accept their natural parameters (vocoder: `frameSize=`/`hopSize=`/`overlap=`, psola: `pitchPeriod=`/`windowSize=`, auto: `transientThreshold=`).
- **Piano warmth target:** ≥4 velocity layers per pitch point (pp + mp + mf + ff) + sustain-pedal-sim release; `release=` named-arg knob with smart default.
- **Ragtime UAT criterion:** subjective composer approval of `examples/ragtime/ragtime.flow` rerender; RMS regression baseline locked at close-out.
- **Drums via VSCO-CE SFZ surface:** No `flow-lang/Samples/drums/` directory; SFZ patch lives at composer's `sfz_root`. Routes via `renderSong song "sampler:drums"` or explicit `Sfz` binding.
- **Phase 37 closer plan (37-07):** Examples + verification + STATE/ROADMAP/REQUIREMENTS sweep + CLAUDE.md updates. Mirrors Phase 36's Plan 36-12 shape.

</specifics>

<deferred>
## Deferred Ideas

- **PIANO-01 v1.6 stretch goals** (per D-37-09 narrow-scope): EQ-shaping curve (boost low-mids, gentle 5kHz roll-off) and sympathetic-string resonance modeling. Defer to a future "Sound Design 2.0" phase if ≥4 layers + release tuning alone don't close the ragtime UAT gap.
- **Granular density/grain hard caps**: Trust composer per language philosophy; document cost model. Revisit if a composer trips a real performance gotcha during v1.5.
- **Sampled drums with pitch shift fallback to varispeed**: Rejected in favor of clean DSP-02/03 dependency (D-37-14). Re-evaluate only if DSP-02/03 ship late and DRUM-01 needs to ship independently.
- **Per-articulation release-tail multipliers (SAMP-03 coupling for PIANO-01)**: Rejected in favor of standalone `release=` knob (D-37-11). The two paths can converge later (v1.6) once SAMP-03 multiplier shape is concrete.
- **Sealed mode design for DSP-02/03**: Rejected in favor of full per-mode knob exposure (D-37-08). Re-evaluate if the knob surface grows beyond ~6 named args per builtin (ergonomics threshold).
- **Adaptive per-buffer HPS threshold**: Rejected in favor of locked default + override (D-37-07). Re-evaluate if composer feedback shows the static default misfires on real material.

### Reviewed Todos (not folded)

None — no .planning/seeds/ or backlog todos surfaced during cross-reference that matched Phase 37 scope.

</deferred>

---

*Phase: 37-sound-design-sampler-polish*
*Context gathered: 2026-05-22*
