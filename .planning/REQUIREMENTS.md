# Flow Language — v1.5 Requirements

**Milestone:** v1.5 Stage, Studio, Web — live coding revamp, generative algebra, notation interop, real-time MIDI, WASM playground
**Started:** 2026-05-17
**Source:** `.planning/research/SUMMARY.md` + `.planning/research/{STACK,FEATURES,ARCHITECTURE,PITFALLS}.md`

REQ-ID numbering continues from v1.4 close. New categories `LANG-*`, `TEST-*`, `HK-*`, `PAT-*`, `GEN-*`, `SECT-*`, `IMPROV-*`, `DSP-*`, `MIX-*`, `SAMP-*`, `PIANO-*`, `FLUTE-*`, `DRUM-*`, `LIVE-*`, `REPL-*`, `AUDIO-IN-*`, `OSC-*`, `XML-*`, `LILY-*`, `ABC-*`, `MML-*`, `MIDI-RT-*`, `CLOCK-*`, `LINK-*`, `JACK-*`, `WASM-*`, `WASAPI-*`, `COREAUDIO-*`, `BIN-*`, `DOC-*`, `JET-*`, `SHOWCASE-*` introduced this milestone.

**Locked decisions (from `/gsd-new-milestone` discussion + research synthesis):**
- D-v1.5-01: Pre-traction no-deprecation latitude is ACTIVE — breaking syntax/builtin changes ship in single commits; in-repo migrators only; no `flow migrate` CLI subcommand required yet. See external memory `project_pre_public_no_legacy_burden` (rewritten 2026-05-17).
- D-v1.5-02: WASM playground ships on Mono-WASM jiterpreter, NOT NativeAOT-LLVM. Reflection-heavy `InternalFunctionRegistry` would require source-generator pass — deferred to v1.6.
- D-v1.5-03: Phase vocoder hand-rolled (RubberBand GPL hazard rejected, same posture as Phase 29 SPEC-2 license discipline).
- D-v1.5-04: Ableton Link integration license-gated — Phase 40 plan-start requires legal review of GPLv2+/commercial dual-license posture. If conflict, Link deferred to community contribution.
- D-v1.5-05: Pattern matching exhaustiveness — non-exhaustive matches WARN to stderr and fall through to Void (charitable interpretation rule). Composer opts INTO strict via `enable matchExhaustive;` pragma.
- D-v1.5-06: Generative primitive determinism — all PRNG-driven calls (Markov / L-system / cellular / Lorenz / granular jitter / sampler round-robin) route through new `PrngRegistry` keyed by `(SourceLocation, generator-name)`; unseeded calls reseed at `renderSong`/`writeWav` boundary. Lorenz cross-platform FP divergence documented as platform-specific limitation.
- D-v1.5-07: Live block determinism opt-out — `live { ... }` blocks emit a stderr advisory on every entry explicitly noting they opt OUT of the two-run cmp-clean determinism contract. 30s wall-clock evaluation cap + 200ms file-watch debounce + bar-boundary swap.
- D-v1.5-08: MusicXML reference consumer is MuseScore. Articulation decision table locked: Accent→`<accent/>`, Marcato→`<strong-accent/>`, Staccato→`<staccato/>`, Tenuto→`<tenuto/>`, Sforzando→`<dynamics><sfz/></dynamics>`, Legato→slur spans (NOT per-note `<legato/>`).
- D-v1.5-09: Stereo pan audit gap (PROJECT.md says shipped v1.0 Phase 2; v1.4 backlog says open) — resolve at Phase 37 CONTEXT spawn; likely synth-path shipped + SFZ sampler-path mono-only.
- D-v1.5-10: Phase 35 is the dependency root — pattern matching used in Phase 36 destructuring + Phase 40 MIDI dispatch + Phase 39 articulation emit. Span migration runs first within Phase 35.
- D-v1.5-11: Pattern-matching backend lands as naive linear scan in Phase 35; Jacobs/Peterse decision-tree compile deferred to v1.6 per D-v1.5-01 pre-traction no-deprecation latitude. Authorized 2026-05-18 during Phase 35 plan-checker iteration — naive backend is correct (passes the LANG-01 surface contract); decision-tree is a performance optimization not load-bearing at v1.5 match-arm counts. Backend swap when v1.6 lands is internal-only (no composer-visible API change).

---

## v1.5 Active Requirements

### Language Foundation (Phase 35)

- [ ] **LANG-01**: Pattern matching expression `(match expr | pattern => body | pattern => body | _ => body)` with literal / constructor / wildcard / guard pattern forms. Non-exhaustive matches WARN to stderr and fall through to `Void` (D-v1.5-05). `enable matchExhaustive;` pragma promotes warnings to errors. Arms are independent — no C-style fall-through. Pattern AST lives in new `Ast/Patterns/` folder distinct from `Expressions/`/`Statements/`. Backend ships as naive linear scan in v1.5 (D-v1.5-11); Jacobs/Peterse decision-tree compile deferred to v1.6.
- [ ] **LANG-02**: Music-aware pattern extractors — match on chord quality (`Cmaj7`, `Dm`, `F#dim`), scale degree (`I`, `V7`, `vi`), note pitch class (guarded — `| n when (= (pitchClass n) 0) => ...`), articulation token (`#staccato`, `#legato`, `#accent`). Patterns orthogonal to `OverloadResolver` (no participation in function dispatch).
- [ ] **LANG-03**: `-> as name` mid-chain naming — `seq -> (transpose 2) as melody -> (legato 0.5) as legato-melody -> render`. Names the intermediate value WITHOUT breaking the chain; equivalent to `Sequence melody = (transpose seq 2); ...` but inline. Parses as a parser-level transform (no new AST node — annotates `FlowExpression`).
- [ ] **LANG-04**: Rust-style multi-line diagnostics — `Diagnostics/SnippetRenderer` with source-quoted span pointers, secondary notes (`note:` rows), "did you mean?" suggestions for unknown identifiers (closest Levenshtein match in scope). Span field retrofitted across 16 expression + 14 statement AST records via defaulted-parameter (v1.3 Phase 22 pattern). All existing tests must remain green during the Span migration.

### Test Framework (Phase 35)

- [ ] **TEST-01**: Pure-Flow test framework — `(test "name" body)` declares a test block. Assert primitives ship in new `@test` stdlib module: `(assert cond)`, `(assertEq a b)`, `(assertNotesMatch seqA seqB)`, `(assertBytesEqual buf1 buf2)`, `(assertWithinDb buf1 buf2 0.5dB)`. `flow test [path]` CLI subcommand discovers `test_*.flow` files by convention and runs all `(test ...)` blocks they contain.
- [ ] **TEST-02**: Test hermetic isolation — `FlowEngine.SnapshotState()` / `RestoreState()` reset musical context stack, voice pool, PRNG state, ExecutionContext bindings between `(test ...)` blocks. No shared state leakage. Tests run sequentially in a single FlowEngine process (per-test subprocesses rejected as anti-pattern).

### v1.5 Housekeeping (Phase 35)

- [x] **HK-01**: `humanizeGaussian` voice-block bug investigation + fix. Carryover from v1.4 Phase 34 ragtime UAT iteration #2. Bug surfaces when `humanizeGaussian` is applied across a `{voice ...}` polyphony block. **Closed via Plan 35-02 Task 2** — fix recurses into `bar.ParallelVoices` reusing the single seeded `Random` (BarRenderer.cs:62-77 mirror); `tests/test_humanize_voice_block.flow` + `flow-lang.Tests/Phase35/HumanizeGaussianVoiceBlocksTests.cs` pin the regression (WAV grew from 44 bytes → 352,844 bytes).
- [x] **HK-02**: Phase 17 HUMAN-UAT rows 1-3 closure (manual-smoke verification — VS Code extension on non-dev OS, basic editing workflow). Recorded `closed` in `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md`. **Closed via Phase 31 Plan 31-08 UAT** (PyCharm 2025.3 + LSP4IJ — structurally a superset of the planned VSCode dev-host F5 smoke; LSP4IJ's selector + language-id requirements are stricter than VSCode's TextMate-backed pipeline). Plan 35-02 Task 3 records the cross-reference; no further work required.
- [x] **HK-03**: Phase 04 `VERIFICATION.md` `gaps_found` closure. Audit remaining unverified criteria; add regression tests or document as `closed_with_acceptable_gap`. **Closed via Plan 35-02 Task 1 + Task 3** — the `MutateRhythm` switch enum mismatch (the only code-level gap) was found ALREADY CORRECT at audit time (silently fixed at an earlier checkpoint); `flow-lang.Tests/Phase35/MutateRhythmEnumValuesTests.cs` pins the 5-case enum mapping so it cannot regress. COMP-01 / COMP-02 were v1.4 requirements that rolled into v1.4 milestone closure; they are not tracked in this v1.5 REQUIREMENTS.md so no checkbox flip applies here. 04-VERIFICATION.md status flipped from `gaps_found` to `verified`.
- [x] **HK-04**: CLAUDE.md "Public as of v1.4" footnote rewrite — current text states the deprecation rule that is no longer in effect; replace with pre-traction no-deprecation latitude framing matching the rewritten `project_pre_public_no_legacy_burden` external memory. **Closed via Plan 35-02 Task 3** — footnote rewritten to D-v1.5-01 pre-traction framing; cross-references the rewritten 2026-05-17 external memory.

### Pattern Algebra (Phase 36)

- [x] **PAT-01**: 13 Tidal-style combinators on `Sequence` — `every`, `fast`, `slow`, `chunk`, `phase`, `rev`, `iter`, `palindrome`, `jux`, `sometimes`, `degrade`, `sparseSeq`, `superimpose` (D-36-01 hybrid: drops `often`/`rarely` collapsed into `sometimes prob`, drops `cat` redundant with `Transforms.concat`, drops `striate` Phase 37 territory, adds `iter`/`palindrome` from research, adds Flow-native `sparseSeq` for custom drop prob). All compose via direct calls; lambda-required transform-arg style per D-36-03. Live in new `@patterns` stdlib module. (Shipped Phase 36 Plan 36-05 commits `a0f9882`+`4ddbf86`+`c823c83`)
- [x] **PAT-02**: Combinator semantics typed on Flow's `Sequence` (no polymorphic `Pattern a` monad — Flow's type system stays). Failures (zero-length sequence, divide-by-zero rate) charitably interpreted (`(fast seq 0)` → unchanged sequence with stderr advisory). (Shipped Phase 36 Plan 36-05 — `PatternChalkyEdgeCasesTests` 8/8 GREEN)

### Generative Primitives (Phase 36)

- [x] **GEN-01**: Markov chain primitive — `(markov corpus order length seed)` one-shot OR `(markovTrain corpus order) → MarkovModel` + `(markovGenerate model length seed)` train-once-generate-many split (D-36-06). Corpus is `Sequence`; order ∈ [1, 3] charitably clamped; deterministic when `seed` provided. First-class `MarkovModel` reference-identity value type (specificity 148). Feature extraction via named-arg `features=#pitch` or `features=<<#pitch, #duration>>` (D-36-07). (Shipped Phase 36 Plan 36-06 commits `3628c64`+`89bd359`+`2a9067a`)
- [x] **GEN-02**: L-system primitive — `(lsystem axiom rules iterations)` one-shot OR `(lsystemModel + lsystemGenerate)` split. Axiom and rules use `Symbol` alphabet (e.g. `#A #B #+`); rule application via dict of `Symbol → Symbol[]`; terminal symbols map to notes via `(lsystemToSequence symbols mapper)` post-pass. Output is `Sequence`. T-36-17 DoS guard via 20-iteration cap. First-class `LsystemModel` reference-identity value type (specificity 149). (Shipped Phase 36 Plan 36-07 commits `28091f1`+`e4b93ba`+`3bac210`)
- [x] **GEN-03**: Cellular automata — `(cellular rule width steps seed)` for 1D rules (Wolfram canonical: 30/90/110/184 verified via hand-computed boolean rows); `(cellularSeeded rule width steps seed initialPattern)` escape-hatch with explicit `Array[Bool]` seed; `(life width height steps seed)` for 2D Conway with 30%-density seeded fill. 1D output is `Sequence`; 2D output is `Array[Sequence]`. T-36-19 DoS guard via 1024 per-dimension cap. (Shipped Phase 36 Plan 36-08 commits `292585c`+`c1c3a32`+`8478f11`)
- [x] **GEN-04**: Chaos maps — `(lorenz sigma rho beta length seed)` forward-Euler 3-state ODE (returns `Array[Double]` x-axis trajectory; canonical butterfly fallback σ=10/ρ=28/β=8/3 on degenerate params); `(logistic r length seed)` recurrence in [0, 1] with r clamped to [0, 4]. Bridge via `(quantizeToScale series scale)` in two overloads (String scale-name + Array[Note] direct). T-36-21 DoS guard via 100,000-element length cap. Cross-platform FP divergence documented as platform-specific limitation per D-36-09. (Shipped Phase 36 Plan 36-09 commits `f96b5b2`+`061f2ab`+`f77e66a`)
- [x] **GEN-05**: Determinism contract — all GEN-* + stochastic PAT-* + IMPROV-* primitives route through `Runtime/PrngRegistry` keyed by `(SourceLocation, generator-name)`. Unseeded calls reseed at `renderSong`/`writeWav` boundary preserving two-run cmp-clean contract (D-v1.5-06). `PrngRegistryNewRandomGateTests` source-grep gate enforces zero unsanctioned `new Random(` constructions; documented exceptions carry `// PRNG-SANCTIONED:` marker. Lorenz cross-platform FP divergence documented as platform-specific limitation; same-platform two-run cmp-clean preserved across 11 stochastic test/example files. (Shipped Phase 36 Plan 36-01 commits `164483d`+`5a234f1`+`bca3dec`; reinforced across all stochastic plans)

### Parameterized Sections (Phase 36)

- [x] **SECT-01**: Parameterized sections — `section verse(Note root = C4, Int repeats = 2) { ... }`. Section calls take positional + named args (D-36-13 parens syntax `[verse(C4, 2) chorus]`); calling pushes a synthetic frame at CALL time inheriting the CALLSITE's MusicalContext (D-36-10-03 / Pitfall 7 dynamic scope). Closure over outer musical state preserved. Existing zero-arg `section verse { ... }` form unchanged. Full Phase 35 pattern syntax in signatures: typed BindingPattern, tuple destructure, music-aware extractors (chord literal / roman numeral / articulation symbol) per D-36-17. Section overloading via OverloadResolver per D-36-18. Repeat operator `*N` (D-36-14). Defaults work with positional + named-arg forms (D-36-15). Arity / type errors via Phase 35-03 Rust-style multi-line DiagnosticRenderer (D-36-16). (Shipped Phase 36 Plan 36-10 commits `e935991`+`d0ddfb9`+`ac07132`+`c02aa12`)

### Improvisation API (Phase 36)

- [x] **IMPROV-01**: Chord-aware Markov improvisation — `(jam over=chords style=#jazz length=8 key="Cmajor" seed=N order=2)`. Only `over` is required; defaults `style=#jazz`, `length=8`, `key=active MusicalContext`, `seed=PrngRegistry-routed`, `order=2`. Style symbol resolves to composer-editable Flow-file rule packs at `flow-lang/improv/styles/*.flow` (shipped `#jazz` / `#blues` / `#classical` baselines) + `~/.config/flow/styles/*.flow` (user packs, override shipped via Pitfall 8 last-write-wins). Pack Dict shape (scale_weights / interval_transitions / rhythmic_template / articulation_distribution) documented at `flow-lang/improv/styles/README.md`. The `key=` arg pushes a synthetic MusicalContext frame for chromatic pivot bars. Output is `Sequence`. Deterministic when `seed` provided. Charitable interpretation throughout: degenerate inputs emit one-shot advisory + return usable Sequence, never error. (Shipped Phase 36 Plan 36-11 commits `4e8957d`+`1291b87`+`f9dc75f`)

### Sound Design DSP (Phase 37)

- [x] **DSP-01**: Granular synthesis — `(granular buf grain=50ms density=20Hz jitter=0.3 windowing=#hann)`. Windowing options: `#hann` (default), `#gaussian`, `#tukey`. Jitter PRNG routed through `PrngRegistry` (D-v1.5-06). CPU cost proportional to grain density × overlap factor. — closure: Plan 37-01 commits `b724d33` / `818e539` / `0d44e9c`
- [x] **DSP-02**: Independent time-stretch — `(stretch buf factor mode=#auto)`. Modes: `#vocoder` (phase vocoder for harmonic material), `#psola` (PSOLA for percussive material), `#auto` (HPS transient detection picks per-frame). Hand-rolled phase vocoder (D-v1.5-03 — RubberBand rejected). — closure: Plan 37-02 commits `db92da6` / `75d922a` / `3daffe4`
- [x] **DSP-03**: Independent pitch-shift — `(pitchShift buf cents mode=#auto)`. Same vocoder / PSOLA / auto mode hierarchy as DSP-02. Existing `loadWav` varispeed call sites unaffected (varispeed couples pitch + time; DSP-03 decouples). — closure: Plan 37-02 commits `db92da6` / `75d922a` / `3daffe4`

### Stereo Pan (Phase 37)

- [x] **MIX-01**: Per-voice `Pan` attribute on `Voice` (range -1.0 to +1.0). Applied via constant-power law (`left = cos((pan+1)*π/4)`, `right = sin((pan+1)*π/4)`) at SongRenderer additive-mix stage. **Pre-plan audit:** confirm whether existing synth-path pan is shipped (per PROJECT.md "v1.0 Phase 2") and only retrofit work is required (D-v1.5-09). — closure: Plan 37-03 commit `e40cd3e` (audit-only — D-37-15 confirmed pre-shipped; SPEC-8 RMS baseline pinned at `mix_synth_path_pan.wav` SHA-256 `2ea8bc3a...`)
- [x] **MIX-02**: SfzRenderer stereo retrofit — SFZ sampled instruments respect per-voice `Pan` attribute. SFZ samples are mono-only today; renderer applies pan post-render before additive mix. — closure: Plan 37-03 commits `add3e6a` / `b6ceaed` / `e40cd3e` (6-arg Render overload + SectionPan threading + B2 unconditional stereo lock per Pitfall 12 + OQ4 additive-with-clamp composition)

### Sampler Polish (Phase 37)

- [x] **SAMP-01**: SFZ round-robin opcode parser — `seq_position` + `seq_length` opcodes recognized by `SfzParser`. Round-robin index deterministic across runs (seeded from voice ordinal index, not wall-clock). — closure: Plan 37-03 commits `729cb4a` / `e985b83` / `b6ceaed` (KnownOpcodes 14→20 + `_rrCounter` Dict + `ResetAtRenderBoundary` + `seq_length>100` clamp per T-37-03-01)
- [x] **SAMP-02**: SFZ velocity-layer crossfade — `xfin_lovel` / `xfin_hivel` / `xfout_lovel` / `xfout_hivel` opcodes parsed; equal-power crossfade between overlapping velocity regions. Hard-switching remains default when crossfade opcodes absent. — closure: Plan 37-03 commits `729cb4a` / `e985b83` / `b6ceaed` (equal-power sin/cos curve per Pattern 6 + 0.7071 sibling-in-band headroom per Pitfall 7)
- [x] **SAMP-03**: Per-articulation envelope multipliers for sampled path — multiplicative stack on top of Phase 28's locked articulation envelope rules. Sampled-path staccato (currently thinner than synth-path per Phase 29 v1.5 follow-up) gains an articulation-specific envelope multiplier to match the synth-path's perceived sustain. — closure: Plan 37-03 commit `b6ceaed` + Plan 37-04 commit `6560ee6` (A8 Option A scalar ADSR multiplier table; SynthUtils.GenerateArticulationADSR unchanged per Pitfall 10; SamplePathArticulationMultipliers applied at SFZ + SampledInstrumentRenderer caller sites only)

### Sampler Asset Bundle (Phase 37)

- [x] **PIANO-01**: Warmer piano timbre + VSCO velocity-layer expansion — ragtime UAT iteration #2 follow-up. More velocity layers (target ≥4 per pitch point) + tone-shaping pass (envelope tuning, subtle EQ compensation). Composer UAT closes the iteration. — closure: Plan 37-04 commits `af8395f` (composer mf-sample drop) / `6560ee6` / `7f3ad4e` (4 velocity layers pp/mp/mf/ff with synthesized mp via signed-RMS α=0.6 per A5 LOCK; `release=` named arg with default 1.5s per D-37-11 Lehtonen 2007; composer UAT auto-approved per `37-HUMAN-UAT.md` Q1/Q2/Q3)
- [x] **FLUTE-01**: Additional flute samples between G4 and G5 (Phase 29 carryover — close D5 timbre-crossover gap). Adds ≥1 sample point (likely D5 or A4) to the existing 2-sample G4/G5 layout. — closure: Plan 37-05 commits `681908c` (composer drop) / `3686e19` (A4 chosen over D5 per RESEARCH §Pattern 10 + A6 — broader low-register varispeed coverage; Flute.vib.ff.A4 variant-matched to existing G4/G5)
- [x] **DRUM-01**: Sampled drums via `SampledInstrumentRenderer` with transient-preserving pitch shift (PSOLA for transients, vocoder for sustain — same `#auto` mode hierarchy as DSP-02/03). Per SPEC D-02 of Phase 29: drums were locked to synth-only; v1.5 lifts that restriction with the transient-preserving path. — closure: Plan 37-06 commits `75878a0` / `7eaf410` (DRUM-01 ships via Phase 33 SFZ surface against VSCO-CE 1.1.0 per D-37-13, NOT bundled .wav path; W7 LOCK `SfzData.IsPercussion` driven by dict-symbol `#drums` per Plan 37-06; pitch-shift routes through Plan 37-02 PitchShiftEngine `#auto` per D-37-14)

### Live Coding 2.0 (Phase 38)

- [x] **LIVE-01**: `live <quantize> { ... }` block — auto-loops the block; hot-swaps content at the next quantize unit boundary (default `1bar`). Quantize unit accepts `NoteValue` (`q`, `h`, `w`, etc.) or `Bar`. Explicit opt-out from two-run determinism contract with stderr advisory at every entry (D-v1.5-07). **Shipped via Plan 38-02** (commits `fc9edc0` + `155b5aa`).
- [x] **LIVE-02**: Modernized watch mode (rewrite of existing `flow --watch`) — ANSI live status panel, structured stderr (`[live]` prefix on advisory, `[error]` on parse fail), 30s wall-clock evaluation cap with CancellationToken, 200ms file-watch debounce. **Shipped via Plan 38-01** (commits `ccba90f` / `8fbc127` / `d4f14f3`) + **Plan 38-03** (commit `9c02b8d` — timeout-revert wording aligned to UI-SPEC line 330).
- [x] **LIVE-03**: State preservation across live reload — voice-pool state preserved IF voice name still exists post-edit; musical context stack reset to file-scope; PRNG state reseeded at swap boundary. Stale-closure detection: closures referencing now-removed bindings raise a clear advisory rather than silently misbehaving. **Shipped via Plan 38-03** (commits `0c1e30e` / `c9e5f1b` / `9c02b8d`).

### REPL Polish (Phase 38)

- [x] **REPL-01**: LSP-backed tab completion — REPL embeds `flow-lsp` in-process and queries `CompletionHandler` for the current line. Token-heuristic fallback when partial-parse fails (matches identifier prefix against scope). **Shipped via Plan 38-04** (commits `1a99aa9` / `bf5a3b1`); in-process LSP via STATIC `CompletionHandler.BuildItems()` per D-38-12 SIMPLIFICATION.
- [x] **REPL-02**: Inline `:help fn` meta-command — `:help transpose` prints signature + doc-comment + 1-line example from `BuiltInDocs` table (Phase 31 LSP table — 104 entries reused by REPL). Composer asks via `:help <name>` per D-38-09 — consistency with the existing `:quit` / `:help` / `:clear` / `:stop` meta-command family in `flow-interpreter/Repl.cs:210-220`. OVERRIDES earlier `?fn` wording per D-v1.5-01 single-commit migration latitude — see `.planning/phases/38-live-coding-2-0/38-CONTEXT.md` D-38-09 decision + `.planning/phases/38-live-coding-2-0/38-VERIFICATION.md` for rationale. **Shipped via Plan 38-04** (commit `bf5a3b1`).
- [x] **REPL-03**: Multi-line editing + history search — Ctrl+R history search; multi-line input via continuation prompt (paren-balanced detection); persistent history at `~/.config/flow/history`. **Shipped via Plan 38-04** (commits `1a99aa9` / `bf5a3b1`); PrettyPrompt 4.1.1 (MPL-2.0, verified live on NuGet 2026-05-23); `~/.config/flow/history` 10k cap with rotation + 0600 mode on Linux/macOS.
- [x] **REPL-04**: Pretty piano-roll on `(inspect seq)` / `(visualize seq)` alias pair (D-38-10 — both names ship backed by one implementation) — ASCII piano-roll with pitch on Y axis, time on X axis; tick marks at bar boundaries; articulation glyphs (`>` Accent / `.` Staccato / `^` Marcato / `_` Tenuto / `!` Sforzando / `~` Legato gap-fill) at note onsets per UI-SPEC §"Glyph Inventory". `(inspect seq)` is a new alias backed by the existing `flow-lang/StandardLibrary/VisualizationFunctions.cs` renderer — charitable to pre-Phase-38 scripts that called `visualize`. OVERRIDES solo `(inspect seq)` wording per D-v1.5-01 single-commit migration latitude — see `.planning/phases/38-live-coding-2-0/38-CONTEXT.md` D-38-10 decision. **Shipped via Plan 38-04** (commit `644aeb8`).

### Audio Input (Phase 38)

- [x] **AUDIO-IN-01**: Audio input — `(micBuffer duration)` reads from the default input device via PulseAudio capture (`PA_STREAM_RECORD` flag, parallel to existing playback path). Auto-attenuates 20 dB on open to prevent feedback. Returns `Buffer`. **Shipped via Plan 38-05** (commits `a15b1f4` PulseAudioCaptureBackend sibling class + `3a98542` mic_fixture + `34bb251` InputFunctions wiring); sibling-class P/Invoke direction-swap pattern preserves PulseAudioSimpleBackend single-responsibility per RESEARCH §I.
- [x] **AUDIO-IN-02**: Audio input pipeline integration — captured `Buffer` composes with existing `mix`/`play`/`writeWav` builtins. Sample-rate conversion to 44.1 kHz at capture-side (linear interpolation). Granular DSP-01 composes with mic input for real-time texture creation. **Shipped via Plan 38-05** (commit `34bb251` ResampleLinear helper + `2a2146a` `tests/test_audio_in_pipeline.flow` composability smoke).

### OSC (Phase 38)

- [x] **OSC-01**: OSC server — `(oscListen port path handler)`. Accepts OSC 1.0 type-tag conventions (`,f`/`,d`/`,i`/`,s`). Rate-limited to 200 Hz per path (`OSC flood` prevention) — drop-newest sample-and-hold per D-38-14 (5ms window). Handler is a Flow `(Args... => Void)` lambda. Bundle support both directions with timetag honored on receive per D-38-15; nesting depth cap 8 (mirrors Phase 36 T-36-17 / Phase 39 D-39-19 DoS guard). **Shipped via Plan 38-06** (commits `525d1a2` / `465056e`); Rug.Osc 1.2.5 (MIT, .NET Standard 2.0, zero transitive deps).
- [x] **OSC-02**: OSC client — `(oscSend host port path arg1 arg2 ...)`. Args charitable smallest-tag-that-fits inference per D-38-13: Int → `,i` / Long → `,h` / Float → `,f` / Double → `,d` / String|Symbol → `,s` / Bool → `,T`/`,F` / Buffer → `,b` (blob — 4-byte LE IEEE-754 flatten). Composer escape hatch via explicit cast at call site (e.g. `(toLong 1)` for explicit Long, `1.5d` for explicit Double). Uses Rug.Osc 1.2.5. OVERRIDES strict-tag-by-arg wording per D-v1.5-05 charitable interpretation default + D-v1.5-01 single-commit migration latitude — see `.planning/phases/38-live-coding-2-0/38-CONTEXT.md` D-38-13 decision. **Shipped via Plan 38-06** (commit `465056e`).

### Notation Export (Phase 39)

- [x] **XML-01**: MusicXML export — `(writeMusicXML song "piece.xml")`. Partwise 3.1 subset. Articulation decision table per D-v1.5-08. Multi-track `Song` → multi-part `Score`. Microtonal pitches (from Phase 32 Scala tunings) emit as `<alter>` with cent-precision when supported, else as text annotations. **Shipped via Plan 39-01** (commit `4a838b4`); decimal `<alter>` cent-precision adopted unconditionally per D-39-06 (MuseScore 3.6+ supports natively); same-voice slur grouping for Legato per D-39-07; hand-rolled `XmlWriter` with deterministic `NewLineChars` for two-run cmp-clean (Pitfall 6); musicxml-schemas vendoring SKIPPED per Plan 39-01 T1 — `XDocument` structural diff sufficient.
- [x] **XML-02**: MusicXML round-trip CI gate — emit + reload via `mscore --convert-to mxl` validates structure (note count, durations, pitches, articulations). Round-trip is one-way (Flow → XML); XML import deferred to v1.6 per FEATURES.md anti-feature lock. **Shipped via Plan 39-01** (commit `4a838b4`); charitable-skip when `mscore` absent per D-39-08 (`MusicXmlRoundTripTests.StructuralPreservation_NoteCountMatches` auto-skips when binary missing).
- [x] **LILY-01**: LilyPond export — `(writeLilyPond song "piece.ly")`. Text emit; multi-voice notation; tuplet bracket form `\tuplet N/M {...}`; flattened nested tuplets (engraver compatibility); microtonal pitches via cent-offset comments alongside nearest 12-TET notation. **Shipped via Plan 39-02** (commit `dfd719f`); Dutch pitch convention (`cis`/`bes`/etc.) per Pitfall 2; `\layout { }` + `\midi { }` blocks kept per researcher discretion (matches LilyPond user-base expectation).

### Notation Import (Phase 39)

- [x] **ABC-01**: ABC notation import — `(abc "X:1\nT:Reel\nM:4/4\nK:Dmaj\n|: A2 d2 fedB |...")` returns `Section` or `Sequence`. ABC 2.1 subset + abc2midi extension subset. Multi-tune files (`X:1`, `X:2`, ...) return `Array[Section]`. **Shipped via Plan 39-03** (commit `c196023`); ABCSharp vendoring SKIPPED per revised D-39-04 — hand-rolled `AbcLexer.cs` + `AbcImport.cs` (~600 LOC) fits Flow's narrow needs better than third-party dep; ABCSharp's MIT license verified via WebFetch at plan-start for future v1.6 reconsider.
- [x] **ABC-02**: ABC dialect divergence handling — unknown ornaments (`~`/`T`/`S`/etc.) dropped with stderr `[abc]` advisory; unrecognized headers ignored gracefully (charitable interpretation). **Shipped via Plan 39-03** (commit `c196023`); modal keys Edor/Dmix/Aphr/Cmix/Glyd/Bphr/Floc parsed per D-39-15; `Q:` tempo handles bare BPM + 1/4=BPM + "Allegro" 1/4=BPM forms.
- [x] **MML-01**: MML notation import — `(mml "T120 L4 O4 cdefga>c")` returns `Sequence`. PC-98-era common core: notes (a-g, accidentals `+`/`#`/`-`), octave (`O<n>` absolute, `>`/`<` shift), length (`L<n>`), tempo (`T<n>`), loops (`[...]<n>`). Dialect-specific opcodes (FM operator routing, drum maps) ignored with stderr advisory. **Shipped via Plan 39-04** (commit `474595e`); loop depth-cap 16 per D-39-19 (mirrors T-36-17 DoS guard); nested-loop semantics = inner expands each outer iteration per PC-98 PMD/MUCOM convention (researcher discretion).

### Real-Time MIDI (Phase 40)

- [ ] **MIDI-RT-01**: `IMidiBackend` abstraction parallel to `IAudioBackend` — methods: `ListPorts() → Array[String]`, `OpenOutput(port) → IMidiOutputDevice`, `device.SendNoteOn(channel, pitch, velocity)`, `device.SendNoteOff(channel, pitch)`, `device.SendControlChange(channel, controller, value)`, `device.SendSysex(data)` (best-effort queue), `device.Close()`. Hot-plug events surface via `device.PortChanged` callback.
- [ ] **MIDI-RT-02**: Linux ALSA-seq MIDI backend via RtMidi.Core 1.0.53 — primary platform (Flow's Linux-primary constraint). DryWetMidi 8.0.3 explicitly has NO Linux device I/O (verified via library's Supported OS docs); RtMidi.Core is the load-bearing replacement.
- [ ] **MIDI-RT-03**: macOS CoreMIDI + Windows WinMM backends via RtMidi.Core — secondary platforms enabled in Phase 41 cross-platform binary work.
- [ ] **MIDI-RT-04**: Audio-MIDI latency alignment — MIDI events emit at `audioBuffer.PlaybackStartTime + bufferOffset` (NOT at queue time). Sysex on separate best-effort queue. Hot-plug failures: log + retry + quiet-drop (NEVER throw — would break long `live` sessions).

### MIDI Clock (Phase 40)

- [ ] **CLOCK-01**: MIDI clock master — emit 24 PPQN clock + start/stop/continue messages tied to active `MusicalContext.Tempo`. Tempo changes apply at next bar boundary (no mid-bar tempo jumps to slaved devices).
- [ ] **CLOCK-02**: MIDI clock slave — receive 24 PPQN clock from external master and drive `MusicalContext.Tempo`. 8-pulse settle on master tempo change (avoids jitter). Mode (master XOR slave) switchable only at bar boundary.

### Ableton Link (Phase 40, license-gated)

- [ ] **LINK-01**: Ableton Link integration — peer-equal tempo sync via libabl_link P/Invoke. **License-gated** (D-v1.5-04): GPLv2+/commercial dual-license requires legal review at Phase 40 plan-start; if conflict, this REQ is deferred to community contribution (PR welcome, not shipped from upstream in v1.5).
- [ ] **LINK-02**: Link tempo is render-time input for playback ONLY — NEVER applied to `writeWav` / `writeMidi` (offline render preserves deterministic output). Peer-disappear: latch last-seen tempo (no mid-piece fallback). CI test: byte-identical `writeWav` output with Link peer connected vs without.

### JACK Transport (Phase 40, Linux opt-in)

- [ ] **JACK-01**: JACK transport sync (Linux opt-in) — JackSharp 0.4.0 wrapper. Transport position drives `MusicalContext.Tempo` + bar/beat. Optional dependency; absence does not affect non-JACK workflows. macOS / Windows: JACK is theoretically available but not shipped/tested in v1.5.

### WASM Playground (Phase 41)

- [ ] **WASM-01**: WASM playground host — new `flow-wasm/` Blazor WebAssembly project, Mono-WASM jiterpreter (D-v1.5-02). References `flow-lang` directly. Audio out via Web Audio API through KristofferStrube.Blazor.WebAudio wrapper.
- [ ] **WASM-02**: Browser live-coding UX — editor pane + run/play/stop controls + share-via-URL (URL-encoded source, limit 8 KB). Pairs with Phase 38 `live` block: the browser experience IS watch-mode-in-browser.
- [ ] **WASM-03**: Bundle size ≤15 MB compressed — measured at Phase 41 Plan 01 dry-run. If exceeded, prune stdlib subset (lazy-load `@sfz`, `@notation-emit`, `@osc`) or lazy-load Phase 29 sample bundle on first sampled-instrument use.

### Cross-Platform Audio Backends (Phase 41)

- [ ] **WASAPI-01**: Windows audio backend via NAudio.Wasapi 2.3.0 — implements `IAudioBackend` for `play` / `loop` / `preview`. Single `WasapiBackend.cs` file scoped to playback. Shared-mode (default) + exclusive-mode (opt-in via config flag) supported.
- [ ] **COREAUDIO-01**: macOS audio backend — OwnAudioSharp 1.0.68 (miniaudio binding) preferred path. Phase 41 Plan 01 smoke-test on real hardware required; fall back to hand-rolled CoreAudio P/Invoke `AudioUnit` if latency unacceptable for live coding (>20ms round-trip).

### Cross-Platform Binaries (Phase 41)

- [ ] **BIN-01**: Cross-platform self-contained binaries — `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, `win-x64` published via `dotnet publish -p:PublishSingleFile=true`. Released as v1.5.0 tarballs (Linux/macOS) + zip (Windows) alongside existing flow-linux-x64.tar.gz.

### Documentation Generator (Phase 41)

- [ ] **DOC-01**: `flow doc` documentation generator — extracts `///` doc-comments (new lexer grammar additive to `//`) + proc signatures + builtin metadata from `BuiltInDocs`. Output: browsable HTML reference site (`docs/reference/index.html` by default). Content-hash incremental cache for re-gen.
- [ ] **DOC-02**: `flow doc` example execution — code examples in `///` doc-comments execute via the test framework (TEST-01 hermetic isolation). Failures surface in `flow doc` output as `[example failed]` annotations. Runnable examples double as regression tests.

### JetBrains Marketplace Publish (Phase 41)

- [ ] **JET-01**: JetBrains plugin Marketplace publish — plugin.xml metadata + build.gradle.kts signing config (JetBrains marketplace cert) + CHANGELOG.md. Plugin verifier CI checks compatibility against IntelliJ Platform 2024.3+. Direct-download fallback page (`docs/jetbrains/install.html`) if marketplace review delays.

### v1.5 Closer Showcase (Phase 41)

- [ ] **SHOWCASE-01**: Third-genre showcase piece — jazz / EDM / death metal (composer's choice). ~60s curated piece in `examples/<genre>/<piece>.flow` consuming features from Phases 35-40 (at minimum: pattern matching, one generative primitive, granular DSP or time-stretch, live block, real-time MIDI playback via new IMidiBackend). README.md `## Showcase` v1.5 section embeds inline-audio. v1.5.0 GitHub Release ships the audio + cross-platform binaries. Genre choice validates Flow's genre-agnostic claim alongside v1.4's symphony + ragtime.

### Compile-Target Flavors (Phase 47)

**Phase 47 IN PROGRESS** — `FlowTarget=Desktop|Web` MSBuild conditioning so flow-lang compiles cleanly under WASM by stripping browser-incompatible features (P/Invoke audio backends, FileSystemWatcher, raw UDP sockets, large sample assets). Foundation for Phase 48. Plans 47-01 + 47-02 shipped 2026-05-26.

- [x] **REQ-WEB-TARGET-01**: `<FlowTarget>Desktop</FlowTarget>` MSBuild property in `flow-lang/flow-lang.csproj`; defaults to Desktop so no-flag `dotnet build` preserves byte-identical behavior; single source of truth (D-47-01). Shipped Plan 47-01 commit `635cbda`.
- [x] **REQ-WEB-TARGET-02**: `FLOW_WEB` preprocessor symbol activates via conditional `<DefineConstants>$(DefineConstants);FLOW_WEB</DefineConstants>` when `'$(FlowTarget)' == 'Web'`; asymmetric (no `FLOW_DESKTOP` — D-47-02). Shipped Plan 47-01 commit `635cbda`.
- [x] **REQ-WEB-TARGET-03**: Web-conditional `<ItemGroup>` with 7 `<Compile Remove>` entries (PulseAudio*Backend.cs, CoreAudioBackend.cs, StandardLibrary/Audio/Sfz/**, OscFunctions.cs, OscHandleData.cs, InputFunctions.cs) + `<None Remove="Samples/**" />` belt-and-suspenders + `<None Remove="sfz.flow" />` + `<None Remove="osc.flow" />` + Rug.Osc PackageReference gated to `'$(FlowTarget)' != 'Web'`. Shipped Plan 47-01 commit `635cbda`.
- [x] **REQ-WEB-TARGET-04**: New `flow-lang/Audio/WebAudioBackend.cs` sealed class implements `IAudioBackend` as Phase-48-targeted STUB (D-47-05). All 7 surface methods throw `PlatformNotSupportedException(StubMessage)` with pinned `"WebAudioBackend stub — Phase 48 will implement via [JSImport]"`; `Dispose()` is no-op (using-block safe). Method signatures PINNED — Phase 48 grep-replaces by signature. Shipped Plan 47-02 commit `7021d8a`.
- [x] **REQ-WEB-TARGET-09**: `AudioPlaybackManager.DetectBackend` rewired to probe `WebAudioBackend.IsAvailable()` (returns `OperatingSystem.IsBrowser()` JIT intrinsic per D-47-07) as FIRST branch (D-47-06); existing CoreAudio + PulseAudio probes wrapped in `#if !FLOW_WEB` (Plan 47-01 strip-list compatibility). Throw-on-no-backend at end preserved (PATTERNS.md §Discrepancy 2 Option (a) — no NullAudioBackend introduced). Pinned by 7-Fact `WebAudioBackendStubTests` xUnit fixture (all GREEN on Desktop). Shipped Plan 47-02 commits `156dbd4` + `ba4d3fb`.
- [x] **REQ-WEB-TARGET-05**: FlowEngine + ExecutionContext + Value + SongRenderer + TestSnapshot `#if !FLOW_WEB` guards close the remaining 13 Web-build compile errors (Sfz / Network references). Shipped Plan 47-03 commits `dfa359f` + `9600ddb` — Web build flips RED→GREEN (13 errors → 0). Rule 3 deviation expanded guards to AudioPlaybackManager.IsAudioAvailable + Interpreter Sfz-type variable handler (covered files: 7 total = FlowEngine + BuiltInFunctions + 5 consumer sites).
- [x] **REQ-WEB-TARGET-06**: ModuleLoader stripped-stdlib gate (`@sfz` / `@osc`) emits charitable `[target] module 'X' unavailable on Web target — line N. Build with FlowTarget=Desktop to enable.` advisory via `WarnOnce`. Parser parse-time gate for `live { ... }` blocks throws Rust-style ParseException when `!FlowEngine.SupportsLiveBlocks`. Pinned by `WebTargetGuardTests` (4 Facts, Desktop-side; Web-side deferred to Plan 47-04 FlowTargetFact). Shipped Plan 47-03 commits `905b819` (ModuleLoader) + `d0b8b11` (Parser) + `8f6b814` (test fixture).
- [x] **REQ-WEB-TARGET-07**: DryWetMidi 8.0.3 WASM-compat smoke shipped via `DryWetMidiWasmCompatTests` (2 cross-target `[FlowTargetFact("Desktop", "Web")]` Facts — `MidiFile_WriteAndRead_RoundTripsMinimalSmf` + `DryWetMidiAssembly_IsLoadable`). Desktop 2/2 GREEN confirms DryWetMidi 8.0.3 APIs are reachable. Web execution path conditioned on Plan 47-06 closer tag-sweep (test project currently cascade-fails on 18 Sfz/Osc-referencing files); if Web execution fails after the closer fixes the cascade, Plan 47-06 strips `writeMidi` from Web with parse-error advisory per D-48-04. Shipped Plan 47-04 commit `f51e58d`.
- [x] **REQ-WEB-TARGET-08**: `FlowTargetFactAttribute` xUnit subclass shipped at `flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs` — `params string[] targets` + `#if FLOW_WEB` ternary + descriptive Skip property + `public const string CurrentTarget`. 3 Phase47 test files (DryWetMidiWasmCompatTests / WebTargetParserTests / WebTargetModuleLoaderTests) use the attribute to discriminate target-conditioned execution; Desktop-side Facts pin Plan 47-03 Web-side behavior with documented Skip reason. Desktop-only tag sweep across the 18 Sfz/Osc-referencing Phase 33/37/38 test files deferred to Plan 47-06 closer (documented in 47-04-SUMMARY.md). Shipped Plan 47-04 commit `8adc89c`.
- [ ] **REQ-WEB-TARGET-10**: `AssemblyReferenceScanTests` via Mono.Cecil 0.11.5 (MIT) — reflective scan of Web-compiled `flow-lang.dll` asserts zero references to `Rug.Osc`, `System.IO.FileSystemWatcher`, `libpulse-simple` P/Invoke, `AudioToolbox` P/Invoke, `RtMidi.Core`. Target: Plan 47-05.

### Module Names & Qualified Imports (Phase 43)

**Phase 43 SHIPPED 2026-05-24** — file-level `module <name>` declarations + `(mod.fn args)` qualified-call surface + Beat-backfill builtins (`beatToSec` / `secToBeat` + Beat-companion `delay` / `renderBarAtBeat` overloads) + 12-file stdlib migration + Phase 42 audit polarity flip (D-10 atomic). Closes Phase 42 AUDIT.md §1 BeatType-orphan anchor finding via context-aware tempo-reading conversions. The module surface is unqualified-by-default (ergonomics-first per `feedback_ergonomics_priority`); composers reach for `(mod.fn args)` only when disambiguating collisions surfaced by the D-04 last-import-wins shadow advisory. Pre-traction no-deprecation latitude (D-11) means the 12-file stdlib migration shipped in one commit; D-12 explicitly rejects a composer-facing `flow migrate` CLI subcommand (in-repo migrator sufficient until a third-party fork appears). See `.planning/phases/43-module-names-qualified-imports/43-VERIFICATION.md` for per-REQ closure evidence + D-NN decision trace + two-run cmp-clean confirmation.

- [x] **REQ-MOD-01**: `module` lexer keyword + `ModuleDeclarationStatement` AST record + first-non-comment position constraint enforced via `Parser._seenNonModuleNonCommentStatement` flag-flip in the `Parse()` driver. Mid-file `module` declarations REPORTED via ErrorReporter (not thrown — soft-failure error model per Pitfall 1). Shipped Plan 43-01 commits `e156dcc` (test) + `13c6b9e` (feat).
- [x] **REQ-MOD-02**: `ModuleRegistry` runtime data structure (per-`ExecutionContext`, NOT static singleton — D-03 hermetic isolation per Phase 35 TEST-02 precedent). API: `Register(name, exportedProcs)` / `Contains(name)` / `TryGetProc(name, procName)`. Shipped Plan 43-02 commits `2bc2905` (test) + `f8f338f` (feat).
- [x] **REQ-MOD-03**: `ModuleLoader` registration hook walks `program.Statements` post-Execute for leading `ModuleDeclarationStatement` + remaining `ProcDeclaration` nodes (RESEARCH A2 walk-statements over snapshot-and-diff per D-05). Pitfall 7 short-circuit at `_loadedModules.Contains` ensures the hook runs ONCE per resolvedPath — second `use` of the same file does not re-register, does not fire the dup advisory. Shipped Plan 43-03 commits `c5b1120` (test) + `1e97902` (feat).
- [x] **REQ-MOD-04**: `ExpressionEvaluator.EvaluateMemberAccess` registry-first branch (D-02) — peek if LHS is bare `VariableExpression` and `TryGetProc` against `ModuleRegistry`; hit returns Function Value, miss falls through to existing instance-member dispatch (`chord.Root`, `voice.Pan`, `song.SectionCount` all preserved per Pitfall 2). `EvaluateFunctionCall` qualified-call routing detects dot in `call.Name` and routes through `ModuleRegistry.TryGetProc`. Parser 4-token-lookahead disambiguator added to make `(mod.fn args)` syntax reachable from .flow source (Plan 43-03 Rule 3 blocking-issue deviation). Shipped Plan 43-03 commits `1e97902` + `8ee4d39`.
- [x] **REQ-MOD-05**: D-06 one-shot duplicate-module advisory `[module] duplicate module name '<name>' — last load wins` via `WarnOnce(sentinel="module-dup:<name>")` — per-name dedup (NOT per-name-and-path) is hot-reload safe. D-04 last-import-wins cross-module shadow advisory `[module] '<fn>' from '<B>' shadows '<fn>' from '<A>' — qualify with '<A>.<fn>' or '<B>.<fn>' to disambiguate` via `WarnOnce(sentinel="module-shadow:<prior>:<new>:<proc>")` (per-triple dedup). `ExecutionContext.ProcOwnership` Dict tracks last-write-wins ownership. Shipped Plan 43-03 commit `8ee4d39`.
- [x] **REQ-MOD-06**: 12 stdlib `.flow` files migrated to declare `module <name>` per D-07 in ONE commit (D-11 pre-traction no-deprecation latitude): `audio` / `bars` / `collections` / `composition` / `generative` / `improv` / `osc` / `patterns` / `sfz` / `test`, plus `notation-io.flow → module notation` (canonical name claim per Pitfall 6) + `notation.flow → module notes` (rename-not-merge per Pitfall 6 — file path unchanged). `std.flow` remains declaration-less per D-07 (always-on prelude — keeps unqualified-only behavior). D-12 explicitly NO `flow migrate` CLI subcommand. Shipped Plan 43-05 commit `578b9ab`.
- [x] **REQ-MOD-07**: `beatToSec(Beat) → Second` context-aware conversion builtin via `RegisterContextDependent` (Phase 22 DX-12 closure-captures-ExecutionContext pattern reused). Reads `MusicalContext.Tempo` fresh per call; defaults to 120 BPM with one-shot stderr advisory `[beatToSec] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)` via `WarnOnce(sentinel="beatToSec-no-tempo")` per D-08. Shipped Plan 43-04 commit `f9f4618`.
- [x] **REQ-MOD-08**: `secToBeat(Second) → Beat` symmetric inverse builtin — same `RegisterContextDependent` pattern + default-120 advisory via `WarnOnce(sentinel="secToBeat-no-tempo")`. Closes Phase 42 AUDIT.md §1 BeatType-orphan finding (Beat now appears in 4 builtin signatures, no longer a coercible orphan). Shipped Plan 43-04 commit `f9f4618`.
- [x] **REQ-MOD-09**: `delay(Buffer, Beat, Double, Double)` Beat-companion overload + Pitfall 6 `notation.flow` / `notation-io.flow` rename-not-merge resolution. The `delay` overload routes through `EffectsFunctions.RegisterContextDependent` lambda + dispatches to `Delay.Apply` after Beat→ms conversion. The notation file pair stays at the original `notation.flow` / `notation-io.flow` paths; only declared module names change (`notes` + `notation`). Shipped Plan 43-04 commit `b0b9c6f` (delay overload) + Plan 43-05 commit `578b9ab` (notation rename).
- [x] **REQ-MOD-10**: `renderBarAtBeat(Bar, Beat, String, Int, Double)` Beat-companion overload (same impl as `Double` overload — Beat is Double-backed) + D-10 atomic polarity flip — `Phase42.AuditHarnessTests.OrphanList_ContainsBeatType` renamed `OrphanList_DoesNotContainBeatType` in the SAME commit as the overload landing (Pitfall 5 prevents a RED window between commits). Shipped Plan 43-04 commit `b0b9c6f`.
- [x] **REQ-MOD-11**: Composer-facing scripts continue running without spurious advisories after the 12-file stdlib migration. Three composer-facing smoke scripts verified — `examples/showcase.flow` + `examples/tutorial.flow` + `examples/dsp/granular.flow` — exit 0 with zero `[module]` advisories in stderr. Three duplicate `internal proc` forward declarations removed from `notation.flow` (addNoteToBar/renderSequenceToVoices/noteToFrequency — also declared in bars.flow/audio.flow) as Rule 1 auto-fix to honor the must-have truth. Note: plan-referenced `examples/symphony/symphony.flow` + `examples/ragtime/ragtime.flow` were deleted from this worktree earlier (commits `cd9f053` + `9990782`); substitutes preserve the REQ-MOD-11 intent. Tracking documented in `43-VERIFICATION.md §2 Known Caveats`. Shipped Plan 43-03 (advisory semantics, commit `8ee4d39`) + Plan 43-05 (notation duplicate-decl cleanup, commit `578b9ab`).
- [x] **REQ-MOD-12**: Final regression bar — Phase 43 fixture suite 34/34 GREEN (5 ModuleDeclarationParserTests + 5 ModuleRegistryTests + 7 ModuleCollisionAdvisoryTests + 4 QualifiedAccessDispatchTests + 6 BeatConversionTests + 5 BeatCompanionOverloadTests + the polarity-flipped Phase 42 fact) + Phase 42 `AuditHarnessTests` 9/9 GREEN (incl. `OrphanList_DoesNotContainBeatType`) + 123 happy-path `tests/test_*.flow` scripts PASS + 4 expected non-zero-exit scripts unchanged. **Full xUnit suite: 1779 passed / 36 failed / 1 skipped / 1816 total — all 36 failures from the Phase 42 deferred-items.md baseline, ZERO new failures introduced by Phase 43.** Tracking-file sweep (STATE.md + ROADMAP.md + REQUIREMENTS.md + 43-VERIFICATION.md) landed in Plan 43-05 closer (this section's commit).

### Strict Mode (Phase 44)

Phase 44 ships the `enable strict;` file pragma — opt-in "JS-mode-off" reliability knob for composers writing test fixtures, shared snippets, and large pieces. File-scoped (no propagation via `use`); stdlib stays charitable by default so strict files can still call it. Three axes covered by the single pragma: Axis A (no type coercion — OverloadResolver's +100 convertible tier disabled), Axis B (input-perimeter clamps + advisories become errors — 13 §6a clamps + ~113 §6b advisories per Phase 42 AUDIT), Axis C (truthy/stringy/equality strictness — Bool required for `if`/`(and)`/`(or)`/`(not)`, String required for `(print)`, cross-type comparison errors, `(equals 1 1.0)` returns false). Pre-strict bug fix bundled in scope (D-12 non-strict path): `(print 42)` auto-strs via `(str x)`; `if Int x` truthy-coerces; `(not)` ships as a new builtin (was missing per RESEARCH A6). Six new forward-conversion builtins (`db`/`hz`/`ms`/`sec`/`cents`/`semitones`) + 24 reverse extractor overloads make strict-mode refactoring incremental. REPL gains `:strict on/off` sticky meta-commands; live-block strict applies on initial parse + every reload via Phase 38 LiveReloadManager.RenderScript's fresh-engine path (RESEARCH Pattern 7 — zero new plumbing). Two-run cmp-clean determinism preserved (no PRNG sites added). Composer-facing positive `.flow` test suite under `tests/strict/` + xUnit Theory tests pinning ~126 verbatim `[strict] ` error strings.

- [x] **REQ-STRICT-01**: `enable strict;` pragma registered in `PragmaRegistry.KnownPragmas["strict"]` with D-04 description verbatim (`Opt-in strict mode: no type coercion + input-perimeter clamps become errors + Bool-required for if/and/or/not + same-type required for equals/comparisons. File-scoped, no propagation via use imports.`). Typo recovery via existing `LevenshteinHelper.SuggestNearest` D-12 path — `enable stric;` produces `did you mean strict?`. Plan 44-01.
- [x] **REQ-STRICT-02**: `ExecutionContext.StrictMode` boolean field (default false) set per declaring file at FlowEngine.Execute via new `ApplyStrictPragma(program)` helper (mirrors Phase 32 `ApplyTuningPragma` pattern). `ModuleLoader.LoadModule` saves + sets + restores StrictMode around each imported file's Execute (D-03 file-scope; stdlib stays charitable when called from strict files). Plan 44-01 + Plan 44-02 thread the bit through `ProcDeclaration.IsStrict` (parse-time capture) + `Interpreter.ExecuteUserFunctionWithCaptures` (per-proc-entry push/pop adjacent to PushFrame/PopFrame).
- [x] **REQ-STRICT-03**: `ExecutionContext.CallerStrictMode` snapshot field (D-05 two-field design — distinct from StrictMode) set at every call dispatch boundary in `ExpressionEvaluator.EvaluateFunctionCall` adjacent to the existing `prevCallSite` save/restore. Covers unqualified call branch, Phase 43 qualified `(mod.fn args)` branch, and `_invoker.ExecuteUserFunctionWithCaptures` user-proc invocation paths. Stdlib leaf sites read THIS field (not StrictMode) so internal stdlib-to-stdlib calls stay charitable per D-03. Plan 44-02.
- [x] **REQ-STRICT-04**: Axis A — `OverloadResolver.Resolve` threads `bool strictMode = false` to `FunctionSignature.Matches`; strict drops BOTH implicit-conversion clauses per RESEARCH Pitfall 1: `argTypes[i].CanConvertTo(InputTypes[i])` (numeric widening — Int → Double) AND `InputTypes[i].IsCompatibleWith(argTypes[i])` (inverse-direction music-type widening — Decibel.IsCompatibleWith(Double)=true). `(gain buf -12.0)` strict-fails; `(gain buf -12dB)` both modes succeed. `ExecutionContext.ResolveFunction` reads ctx.StrictMode at entry and forwards (Pitfall 4: explicit-parameter route — no ThreadLocal). Plan 44-03.
- [x] **REQ-STRICT-05**: Six forward-direction explicit-conversion builtins (`(db x)`, `(hz x)`, `(ms x)`, `(sec x)`, `(cents x)`, `(semitones x)`) shipped in new `flow-lang/StandardLibrary/ConversionFunctions.cs`. First five accept Int/Long/Float/Double + idempotent on target tagged type (5 overloads each = 25 registrations). `(semitones x)` accepts ONLY Int per D-08 (whole-numbers-by-design per `CentType.cs:24-27` / `SemitoneType` pattern). All 6 available in BOTH modes per D-09. Plan 44-04.
- [x] **REQ-STRICT-06**: Four reverse-direction extractor overloads — `(double x)` / `(float x)` / `(int x)` / `(long x)` accepting all 6 tagged music types (Decibel, Hertz, Cent, Millisecond, Second, Semitone). 24 reverse registrations. `(int 100ms)` floors lossy per existing doubleToInt convention. All available in BOTH modes per D-10. Plan 44-04.
- [x] **REQ-STRICT-07**: Axis B input-perimeter clamps — all 13 §6a sites in `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (lines 106 / 107 / 649 / 650 / 657 / 658 / 666 / 667 / 785 / 821 / 904 / 960 / 1106) flip to `[strict] <tag> <issue>` errors via `ErrorReporter.ReportError` when `ctx.CallerStrictMode == true`. Error strings match AUDIT §6a Column 5 verbatim with `[strict] ` prefix per D-07. Non-strict path byte-identical. `Phase44ClampGrepConsistencyTests` pins exactly 13 input-perimeter clamps remain. Plan 44-05.
- [x] **REQ-STRICT-08**: Axis B advisory sites — ~113 in-scope `WarnOnce` sites from §6b across 19 stdlib modules flip to `[strict] ` errors under CallerStrictMode. HIGH priority (Plan 44-06): Audio/Sfz (22) + Patterns (17) + SongRenderer (2) + SampledInstrumentRenderer (3) + match-non-exhaustive (1) + Audio/DSP (5). MEDIUM + LOW (Plan 44-07): Generative (24) + JamFunctions (16) + Notation (15) + Network/OSC (3) + Audio/Tuning (2) + Harmony (1) + Audio/InputFunctions (3) + Audio/MidiExport (1). **5 carve-out sites STAY charitable** per D-06 + Pitfall 2: `Interpreter.cs:476` (`[live] entering` — D-v1.5-07 design-lock) + `StyleRegistry.cs:156/244/258/265` (4 `[improv]` style-pack discovery). Authoritative manifest at `.planning/phases/44-strict-mode/strict-error-manifest.csv` (Plan 44-00 Wave 0 deliverable).
- [x] **REQ-STRICT-09**: Axis C — Bool required in strict for `(and)` / `(or)` / `(not)` / `if`. Strict `(and Int Int)` errors `[strict] (and) requires Bool — got Int`. Non-strict charitable: `(and)`/`(or)` keep current Bool-only return (NOT switching to Lisp-last-truthy per RESEARCH Open Question 2); `if Int x` truthy-coerces; `(not Int 0)` charitable returns true. Plans 44-08 (non-strict charitable + `(not)` builtin registration per RESEARCH A6) + 44-09 (strict tightening for `(and)`/`(or)`).
- [x] **REQ-STRICT-10**: Pre-strict bug fix per ROADMAP line 404. Non-strict `(print 42)` charitable auto-strs via `StdLib.AutoStr(Value)` helper (mirrors existing `str` dispatch); non-strict `if Int x` truthy-coerces. Void-wildcard overloads added for `print`, `if`, `not`; explicit String/Bool overloads continue to win at +1000 specificity per RESEARCH Pitfall 3 (`(print "hello")` byte-identical). `(not)` registered as new builtin (was absent per RESEARCH A6). Plan 44-08.
- [x] **REQ-STRICT-11**: Strict cross-type comparison `(gt)` / `(lt)` / `(gte)` / `(lte)` errors with `[strict] cross-type comparison <T1> vs <T2> — use explicit (double x) / (int x)`. Strict `(equals 1 1.0)` returns FALSE (set-theoretic per D-11); non-strict `(equals 1 1.0)` retains current Utils.LooseEquals behavior (true via numeric coercion) per RESEARCH Open Question 1 Option (b). `Utils.LooseEqualsStrict(a, b, ctx)` helper short-circuits on cross-type under strict; `StdLib.GreaterThanCharitable`/etc. branch on CallerStrictMode. D-13 Dict type-strict lookup preserved in both modes (regression-pin via `DictTypeStrictRegressionTests`). Plan 44-09.
- [x] **REQ-STRICT-12**: Live-block strict — `enable strict;` file with `live <quantize> { body }` applies strict to body on initial parse + every live-reload re-eval. Mechanism per RESEARCH Pattern 7: `LiveReloadManager.RenderScript` already constructs a fresh `FlowEngine` and calls `Execute` → `PragmaScanner.Scan` → `ApplyStrictPragma` from Plan 44-01 — strict re-applies automatically with zero new plumbing. The `[live] entering live block` advisory STAYS charitable per D-15 carve-out + D-v1.5-07 design-lock. Plan 44-10.
- [x] **REQ-STRICT-13**: REPL strict — `:strict on` / `:strict off` meta-commands (Repl.cs HandleCommand switch arms) flip a sticky `_sessionStrict` field and mutate `_engine.Context.StrictMode` immediately. Typing `enable strict;` at the REPL is also observed by PragmaScanner; per-line execution syncs `_sessionStrict` bidirectionally. Mirrors Phase 38 `:help`/`:quit`/`:clear`/`:stop`/`:help fn` meta-command family. Plan 44-10.
- [x] **REQ-STRICT-14**: Positive `.flow` integration smoke suite at `tests/strict/` — 6 narrow fixtures (`test_strict_axis_a_overload.flow`, `test_strict_axis_b_clamps.flow`, `test_strict_explicit_conversions.flow`, `test_strict_equality.flow`, `test_strict_with_justintonation.flow`, `test_strict_dict_typecheck.flow`) + 1 composer-facing showcase (`showcase_strict.flow`, ~16-bar single-instrument piece naturally using `(db x)`/`(hz x)`/`(cents x)`). Each begins with `enable strict;`; each ends with `(print "PASS")`. `StrictFlowScriptSuiteTests` Theory iterates all files via Process.Start dotnet run + asserts exit-0 + PASS-in-stdout. Plan 44-11.
- [x] **REQ-STRICT-15**: Two-run cmp-clean determinism preserved across strict-mode introduction (CLAUDE.md "Conventions" contract). No PRNG sites added by Phase 44 — every site rewrite is shape `if (ctx.CallerStrictMode) er.ReportError(LITERAL_STRING_FROM_ARGS); else { existing-charitable-body }`. Error strings are deterministic concat of `args[i].As<T>()` + sentinel verbatim (no DateTime, no Random, no Guid per RESEARCH Pitfall 5). `Phase44TwoRunDeterminismTests` runs `showcase_strict.flow` + a representative narrow fixture twice via Process.Start and SHA-256-equates the captured stdout (and `writeWav` bytes if applicable). Same-platform-only deterministic for chaos-primitive sites per D-36-09 + AUDIT §8 Limitation 6 (strict error path short-circuits before chaotic compute → strict errors deterministic). Plan 44-11.

---


</content>
</invoke>
### Type System & Stdlib Audit (Phase 42)

Phases 42-44 were added 2026-05-24 as the v1.5 closeout trio addressing stdlib growth pressure (collisions, dead-end types, charitable-default escape hatch). **Phase 42 SHIPPED 2026-05-24** — read-only audit producing `42-AUDIT.md` (277 lines, 9 sections, 53 routing tags) that feeds Phase 43 (module/naming + new builtins) and Phase 44 (strict mode + explicit-conversion builtins). The audit harness lives at `scripts/StdlibAuditor/` (re-runnable) + `scripts/audit/clamp-grep.sh` + `scripts/audit/flow-callers.sh`; raw data at `.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data/`. Zero production code touched (invariant gate-enforced via `git diff --stat -- flow-lang/StandardLibrary/ flow-lang/TypeSystem/ "flow-lang/*.flow"` empty at every commit boundary).

- [x] **REQ-AUDIT-01**: Audit harness enumerates every `FlowType` subclass + `FunctionSignature` reflectively via `Assembly.GetTypes()` + `FlowEngine.Context.InternalRegistry.EnumerateSignatures()` without throwing. Snapshot: **37 types (10 coercible + 5 reference-identity + 22 strict-equality), 413 registered signatures.** D-42-01-A (locked 2026-05-24) — FlowEngine-as-registry-source pattern (NOT `RegisterSignaturesOnly`) captures all 14 context-bound stdlib surfaces (SfzBuiltins / NotationIoBuiltins / OscFunctions / MarkovFunctions / LsystemFunctions / CellularFunctions / ChaosFunctions / StretchFunctions / PitchShiftFunctions / GranularFunctions / PatternFunctions / JamFunctions / StyleRegistry / Scala). Shipped Plan 42-01 commits `3c74e70` + `e47f7b4`.
- [x] **REQ-AUDIT-02**: `42-AUDIT.md` emitted with 5 gap-class sections (§1 Orphaned Types + §2 Missing Conversions + §3 Asymmetric Pairs + §4 Dead-End Builtins + §5 Overload Gaps) + §6 Clamp & Advisory Inventory + §7 Prioritization & Phase Routing + §8 Limitations + Composer Sign-Off. AuditReportShapeTests xUnit fixture pins the schema (7 InlineData section-presence rows + 4 standalone content-invariant facts = 11 facts; all GREEN). Shipped Plan 42-01 + 42-03 commits `3c74e70` + `76972b4`.
- [x] **REQ-AUDIT-03**: Existing `flow-lang.Tests` suite + every `tests/test_*.flow` script remain green — zero production regressions (Phase 42 invariant). Phase 42 fixture filter 26/26 PASS. Full-suite caveat: 37 pre-existing Phase 28/29/35/38 failures from spawn commit `c4cd738` documented in `.planning/phases/42-type-system-stdlib-audit/deferred-items.md` + `42-VERIFICATION.md §Known Caveats`; verified pre-existing via `git diff c4cd738..HEAD --name-only` showing zero production-code touch across all four plans. Shipped Plan 42-04 (this closer).
- [x] **REQ-AUDIT-04**: Asymmetric-pair findings surfaced via Pitfall 5 false-positive guard (12-row table in §3). 6 genuine asymmetric pairs identified (3 → v1.6-backlog: `readMidi`/`readMusicXML`/`writeABC`+`writeMML`; 3 → not a gap: `loadWav`/`writeWav` closed by Phase 22, LilyPond intentional one-way, Scala/SFZ vendor formats). 6 closed pairs documented with false-positive guard explicitly noted (Markov/Lsystem train+generate, OSC listen+stop, oscSend+oscReceive callback-style design). Shipped Plan 42-02 + 42-03 commits `a0858f4` + `763a9fc` + `76972b4`.
- [x] **REQ-AUDIT-05**: `42-AUDIT.md §4` dead-end candidates cross-referenced against 327 `.flow` proc declarations + 4114 unique call-site tokens in `flow-lang/*.flow` + `examples/**/*.flow` + `tests/test_*.flow`. **Zero genuine dead-ends identified** — all 5 candidates (`?`, `??`, `??reset`, `??set`, `inspect`) are parser-syntactic or REPL-only sites. Pitfall 1 sanity check passed (>20-entry lists are false-positive floods; our 5-entry list resolved 100% via cross-reference). Shipped Plan 42-02 + 42-03 commits `a0858f4` + `76972b4`.
- [x] **REQ-AUDIT-06**: `42-AUDIT.md §5` overload gaps surfaced from JSON `overload_gap_candidates` (85 raw candidates) with CLAUDE.md ergonomics test applied. §5a: 1 HIGH (`pitchShift(Buffer, Hertz)` design-decision-required — semantically distinct from cents-relative). §5b: 70+ candidates CULLED to v1.6-backlog because music-typed call works today via `IsCompatibleWith` widening per CLAUDE.md Music Types Quick Reference (e.g. `(reverb buf 2.5s)` already resolves via Second → Double). §5c: verified-OK pairs (`transpose(Sequence, Semitone)` + `transpose(Sequence, Cent)`). D-42-03-D documented the cull rationale. Shipped Plan 42-01 + 42-03 commits `3c74e70` + `76972b4`.
- [x] **REQ-AUDIT-07**: Clamp/advisory inventory complete (load-bearing for Phase 44 Axis B per ROADMAP line 380). `42-AUDIT-data/all-clamps.txt` (72 sites total) + `input-clamps.txt` (13 Phase 44 Axis B candidates per Pitfall 4 input-perimeter heuristic) + `advisory-sites.txt` (117 `WarnOnce` sites) + `charitable-sites.txt` (110 charitable-fallback markers — pointer-only sweep for bespoke `if (x < 0) x = 0` patterns per §6c). `42-AUDIT.md §6a` enumerates 13 input-perimeter clamps with proposed strict-mode error messages; `§6b` groups 117 advisories across 19 stdlib modules with HIGH/MEDIUM/LOW Phase 44 priorities. `ClampGrepConsistencyTests` 6/6 PASS pins baseline counts with intentionally wide tolerance bands (allows forward drift across Phase 43+/44+ stdlib additions). Shipped Plan 42-02 + 42-03 commits `a0858f4` + `763a9fc` + `76972b4`.
- [x] **REQ-AUDIT-08**: Composer-approved prioritization — `42-AUDIT.md §7` has 53 routing tags across `→ Phase 43`, `→ Phase 44`, `→ v1.6-backlog`, `→ not a gap`. **Auto-approved 2026-05-24** via `/gsd:execute-phase --auto` chain mode per D-42-03-F. Checkpoint type was `human-verify` with `gate="blocking"` (NOT `blocking-human` / NOT package legitimacy), so auto-mode protocol auto-approved and continued. Per-row stable-identifier rule (`builtin_name + signature`, NOT `file:line`, per Pitfall 7) survives Phase 43 rename work. A future composer who disagrees with any specific row can issue a follow-up Quick task to re-classify; Phase 43/44 plan-phase consumption remains valid across such revisions. Shipped Plan 42-03 commit `d512158`.
- [x] **REQ-AUDIT-09**: `42-AUDIT.md` committed (Plan 42-03 commit `76972b4`); ROADMAP.md Phase 42 row marked 4/4 Complete with the deliverable filename cited; STATE.md frontmatter `stopped_at` updated + `last_activity` cites Phase 42 closure + v1.5 Phase Map gained Phase 42 row; this `REQUIREMENTS.md` cross-insert added (REQ-AUDIT-01..09 traceability table). Tracking-file sweep landed in Plan 42-04 (this closer).

---


## v1.5 Traceability

Populated by `gsd-roadmapper` on 2026-05-18 — 66 v1.5 requirements mapped 1:1 to Phases 35-41 (zero orphans, zero duplicates).

| Requirement | Phase | Status |
|-------------|-------|--------|
| LANG-01 | Phase 35 | Pending |
| LANG-02 | Phase 35 | Pending |
| LANG-03 | Phase 35 | Pending |
| LANG-04 | Phase 35 | Pending |
| TEST-01 | Phase 35 | Pending |
| TEST-02 | Phase 35 | Pending |
| HK-01 | Phase 35 | Pending |
| HK-02 | Phase 35 | Pending |
| HK-03 | Phase 35 | Pending |
| HK-04 | Phase 35 | Pending |
| PAT-01 | Phase 36 | Shipped (Plan 36-05 — `a0f9882` / `4ddbf86` / `c823c83`) |
| PAT-02 | Phase 36 | Shipped (Plan 36-05 — `a0f9882` / `c823c83`) |
| GEN-01 | Phase 36 | Shipped (Plan 36-06 — `3628c64` / `89bd359` / `2a9067a`) |
| GEN-02 | Phase 36 | Shipped (Plan 36-07 — `28091f1` / `e4b93ba` / `3bac210`) |
| GEN-03 | Phase 36 | Shipped (Plan 36-08 — `292585c` / `c1c3a32` / `8478f11`) |
| GEN-04 | Phase 36 | Shipped (Plan 36-09 — `f96b5b2` / `061f2ab` / `f77e66a`) |
| GEN-05 | Phase 36 | Shipped (Plan 36-01 foundation — `164483d` / `5a234f1` / `bca3dec`; reinforced 36-05/06/07/08/09/11/12) |
| SECT-01 | Phase 36 | Shipped (Plan 36-10 — `e935991` / `d0ddfb9` / `ac07132` / `c02aa12`) |
| IMPROV-01 | Phase 36 | Shipped (Plan 36-11 — `4e8957d` / `1291b87` / `f9dc75f`) |
| DSP-01 | Phase 37 | Shipped (Plan 37-01 — `b724d33` / `818e539` / `0d44e9c`) |
| DSP-02 | Phase 37 | Shipped (Plan 37-02 — `db92da6` / `75d922a` / `3daffe4`) |
| DSP-03 | Phase 37 | Shipped (Plan 37-02 — `db92da6` / `75d922a` / `3daffe4`) |
| MIX-01 | Phase 37 | Shipped (Plan 37-03 — `e40cd3e`; audit-only baseline pin per D-37-15) |
| MIX-02 | Phase 37 | Shipped (Plan 37-03 — `add3e6a` / `b6ceaed` / `e40cd3e`) |
| SAMP-01 | Phase 37 | Shipped (Plan 37-03 — `729cb4a` / `e985b83` / `b6ceaed`) |
| SAMP-02 | Phase 37 | Shipped (Plan 37-03 — `729cb4a` / `e985b83` / `b6ceaed`) |
| SAMP-03 | Phase 37 | Shipped (Plan 37-03 — `b6ceaed`; Plan 37-04 — `6560ee6` overlay extension) |
| PIANO-01 | Phase 37 | Shipped (Plan 37-04 — `af8395f` / `6560ee6` / `7f3ad4e`) |
| FLUTE-01 | Phase 37 | Shipped (Plan 37-05 — `681908c` / `3686e19`) |
| DRUM-01 | Phase 37 | Shipped (Plan 37-06 — `75878a0` / `7eaf410`) |
| LIVE-01 | Phase 38 | Shipped (Plan 38-02 — `fc9edc0` / `155b5aa`) |
| LIVE-02 | Phase 38 | Shipped (Plan 38-01 — `ccba90f` / `8fbc127` / `d4f14f3`; Plan 38-03 — `9c02b8d` timeout-revert wording finalization) |
| LIVE-03 | Phase 38 | Shipped (Plan 38-03 — `0c1e30e` / `c9e5f1b` / `9c02b8d`) |
| REPL-01 | Phase 38 | Shipped (Plan 38-04 — `1a99aa9` / `bf5a3b1`) |
| REPL-02 | Phase 38 | Shipped (Plan 38-04 — `bf5a3b1`; D-38-09 `:help fn` overrides bare `?fn` wording per D-v1.5-01) |
| REPL-03 | Phase 38 | Shipped (Plan 38-04 — `1a99aa9` / `bf5a3b1`) |
| REPL-04 | Phase 38 | Shipped (Plan 38-04 — `644aeb8`; D-38-10 `(inspect seq)` / `(visualize seq)` alias pair per D-v1.5-01) |
| AUDIO-IN-01 | Phase 38 | Shipped (Plan 38-05 — `a15b1f4` / `3a98542` / `34bb251`) |
| AUDIO-IN-02 | Phase 38 | Shipped (Plan 38-05 — `34bb251` / `2a2146a`) |
| OSC-01 | Phase 38 | Shipped (Plan 38-06 — `525d1a2` / `465056e`) |
| OSC-02 | Phase 38 | Shipped (Plan 38-06 — `465056e`; D-38-13 charitable smallest-tag-that-fits per D-v1.5-05 + D-v1.5-01) |
| XML-01 | Phase 39 | Pending |
| XML-02 | Phase 39 | Pending |
| LILY-01 | Phase 39 | Pending |
| ABC-01 | Phase 39 | Pending |
| ABC-02 | Phase 39 | Pending |
| MML-01 | Phase 39 | Pending |
| MIDI-RT-01 | Phase 40 | Pending |
| MIDI-RT-02 | Phase 40 | Pending |
| MIDI-RT-03 | Phase 40 | Pending |
| MIDI-RT-04 | Phase 40 | Pending |
| CLOCK-01 | Phase 40 | Pending |
| CLOCK-02 | Phase 40 | Pending |
| LINK-01 | Phase 40 | Pending |
| LINK-02 | Phase 40 | Pending |
| JACK-01 | Phase 40 | Pending |
| WASM-01 | Phase 41 | Pending |
| WASM-02 | Phase 41 | Pending |
| WASM-03 | Phase 41 | Pending |
| WASAPI-01 | Phase 41 | Pending |
| COREAUDIO-01 | Phase 41 | Pending |
| BIN-01 | Phase 41 | Pending |
| DOC-01 | Phase 41 | Pending |
| DOC-02 | Phase 41 | Pending |
| JET-01 | Phase 41 | Pending |
| SHOWCASE-01 | Phase 41 | Pending |
| REQ-MOD-01 | Phase 43 | Shipped (Plan 43-01 — `e156dcc` / `13c6b9e`) |
| REQ-MOD-02 | Phase 43 | Shipped (Plan 43-02 — `2bc2905` / `f8f338f`) |
| REQ-MOD-03 | Phase 43 | Shipped (Plan 43-03 — `c5b1120` / `1e97902`) |
| REQ-MOD-04 | Phase 43 | Shipped (Plan 43-03 — `1e97902` / `8ee4d39`) |
| REQ-MOD-05 | Phase 43 | Shipped (Plan 43-03 — `8ee4d39`) |
| REQ-MOD-06 | Phase 43 | Shipped (Plan 43-05 — `578b9ab`; D-11 single-commit migration) |
| REQ-MOD-07 | Phase 43 | Shipped (Plan 43-04 — `f9f4618`) |
| REQ-MOD-08 | Phase 43 | Shipped (Plan 43-04 — `f9f4618`) |
| REQ-MOD-09 | Phase 43 | Shipped (Plan 43-04 — `b0b9c6f`; Plan 43-05 — `578b9ab` Pitfall 6 rename-not-merge) |
| REQ-MOD-10 | Phase 43 | Shipped (Plan 43-04 — `b0b9c6f`; D-10 atomic polarity flip) |
| REQ-MOD-11 | Phase 43 | Shipped (Plan 43-03 — `8ee4d39`; Plan 43-05 — `578b9ab` notation duplicate-decl cleanup) |
| REQ-MOD-12 | Phase 43 | Shipped (Plan 43-05 — Plan 43-05 closer this commit; 34/34 Phase 43 fixtures GREEN + 9/9 Phase 42 AuditHarnessTests GREEN + 123 happy-path scripts pass + pre-existing-36 baseline preserved) |

**Coverage:**
- v1.5 requirements: 87 total (10 in Phase 35, 9 in Phase 36, 11 in Phase 37, 11 in Phase 38, 6 in Phase 39, 9 in Phase 40, 10 in Phase 41, 9 in Phase 42, 12 in Phase 43)
- Mapped to phases: 87/87 ✓
- Unmapped: 0 ✓

---

---

# Flow Language — v1.3 Requirements (historical)

**Milestone:** v1.3 Composer DX Tier B/C — Tuplets, DEFER closures, Tier B/C bundle
**Started:** 2026-04-26
**Source:** `.planning/research/SUMMARY.md` + `.planning/research/{STACK,FEATURES,ARCHITECTURE,PITFALLS}.md`

**Goal:** Close every DEFER-01..06 item carried from v1.2 and ship the Tier B/C composer DX bundle, with tuplet + arbitrary-duration note syntax as the lead capability.

REQ-ID numbering continues from v1.2 (last used: SPIKE-05, FIX-07a, TEST-04, DX-09, QOL-03). New categories `TUP-*`, `FRAC-*`, `PRAG-*`, `MICR-*`, `LINT-*`, `DICT-*` introduced this milestone.

**Locked decisions (from /gsd-new-milestone discussion):**
- D-01: Tuplet bracket syntax is `{N:M ...}` (braces)
- D-02: Pragmas are **file-scope only**, top-of-file only, NOT propagated via `use`
- D-03: Microtonal scope is **named-tunings wedge** (`enable justIntonation;` / `enable pythagorean;`); full Scala loader deferred to v1.4
- D-04: Gaussian humanize ships as a **separate `humanizeGaussian()` function** (preserves byte-identical determinism for existing uniform calls)
- D-05: MIDI TPQN cap when tuplets force auto-elevation is **9600**

---

## Active Requirements

### Foundation — Rational Duration Arithmetic

- [x] **FRAC-01
**: A new `Fraction(int Num, int Denom)` value type lives in `flow-lang/TypeSystem/`, normalizes via GCD on construction, supports addition / multiplication / equality / comparison, and never uses `double` arithmetic for tuplet duration math (Pitfall 1 mitigation). Ships with unit Facts pinning canonical examples (`1/3 + 1/3 + 1/3 == 1`, `2/4 == 1/2`, `3/12 == 1/4`).
- [x] **FRAC-02
**: `MusicalNoteData` gains optional `Fraction? DurationFraction` field that overrides the existing `DurationValue` enum when set. Existing power-of-2 path stays unchanged when the field is null. All ~70 existing `.flow` test scripts must remain byte-identical (regression gate via `cmp` on tutorial.flow + showcase.flow output).

### Tuplets & Arbitrary Fractional Durations

- [x] **TUP-01
**: A `{N:M element element element}q` tuplet bracket compiles to a `TupletElement` AST node (recursive — children are heterogeneous `NoteStreamElement`s including nested tuplets). Per D-01, brackets use `{ }`. Compiles to `MusicalNoteData` instances whose `DurationFraction` reflects the N:M ratio applied to the parent duration. Acceptance: `| {3:2 C4 D4 E4}q |` renders three notes that sum to one quarter note (i.e. each note is a duration of 1/3 quarter = 1/12 whole).
- [x] **TUP-02
**: `{N elem elem elem}` shorthand (no `:M`) defaults to the music21 convention (3-tuplet → 3:2, 5-tuplet → 5:4, 7-tuplet → 7:4 etc.). Acceptance: `{3 C4 D4 E4}q` is equivalent to `{3:2 C4 D4 E4}q`.
- [x] **TUP-03
**: Nested tuplets resolve correctly via accumulating `Fraction outerScale` propagation through the compiler. Acceptance: `| {3:2 C4 {3:2 D4 E4 F4}q G4}h |` renders 5 notes whose durations multiply through both tuplet ratios.
- [x] **TUP-04
**: `C4/N` arbitrary fractional duration syntax is accepted in note-stream context. `C4/12` is a 1/12 note (equivalent to triplet sixteenth at the appropriate tuplet bracket). Lexer disambiguates from arithmetic `/` by being inside `| ... |` note-stream context. Acceptance: `| C4/12 D4/12 E4/12 |` parses and renders three 1/12 notes.
- [x] **TUP-05
**: NoteStreamCompiler bar-fit validator accepts tuplet/fractional bars whose sum equals the time-signature value as a rational fraction (Pitfall 2 mitigation). Acceptance: `tempo 120 timesig 4/4 { | {3:2 C4 D4 E4}q {3:2 F4 G4 A4}q B4q C5q | }` validates clean (each tuplet sums to 1/4, plus 2 quarter notes = 4/4).
- [x] **TUP-06
**: MIDI export auto-elevates TPQN to `LCM(480, 2 × tuplet_denominators)`, capped at 9600 per D-05 (Pitfall 3 mitigation). Tuplets requiring TPQN > 9600 raise a clear error citing the cap. Acceptance: `{3:2 ...}` exports at TPQN=480 (480/3=160 ticks each, exact); `{5:4 ...}` at TPQN=480 (480/5=96 each, exact); `{7:8 ...}` auto-elevates to TPQN=3360 (480 × 7); `{11:13 ...}` raises a TPQN-cap error.
- [x] **TUP-07**: AUDIT-VERIFIED C5 (augment/diminish in `TransformFunctions.cs:239,261`) re-validated against tuplet-aware sequences (Pitfall 9 mitigation). New regression Fact: `augment(tupletSeq)` doubles the rational durations (each 1/12 becomes 1/6); `diminish(tupletSeq)` halves them (each 1/12 becomes 1/24).
- [x] **TUP-08
**: Per-note tuplet shorthand `C4/X:Y[suffix]` inside note streams. `C4/3:2` is one tuplet member at the 3:2 ratio (default level: quarter); `DurationFraction = suffix_fraction / X` of a whole. Optional level suffix (`w/h/q/e/s/t`, default `q`). Per-note instances are independent — mixed ratios in adjacent notes are legal. Y is preserved as the tuplet-ratio label and feeds the same TPQN auto-elevation path as bracket-form. Acceptance: `| C4/3:2 D4/3:2 E4/3:2 |` ≡ `| {3:2 C4 D4 E4}q |`; `| C4/5:4h |` = duration 1/10 whole; `| C4/0:2 |` raises parse error.

### DEFER Closures from v1.2

- [x] **DEFER-01
**: `range(Int, Int) → Array[Int]` and `range(Int, Int, Int) → Array[Int]` (with step) registered in stdlib. Standard semantics: start inclusive, end exclusive, default step=1, negative step iterates backward. Empty array when range is unsatisfiable. Acceptance: `(range 0 5)` → `[0, 1, 2, 3, 4]`; `(range 0 10 2)` → `[0, 2, 4, 6, 8]`; `(range 5 0 -1)` → `[5, 4, 3, 2, 1]`.
- [x] **DEFER-04
**: Multi-letter enharmonic edges resolved in `HarmonyFunctions.Enharmonic`: E↔Fb, F↔E#, B↔Cb, C↔B# round-trip correctly (Pitfall 10 mitigation; must precede DEFER-02/03). Acceptance: `enharmonic(E4)` → `Fb4`; `enharmonic(Fb4)` → `E4`; `enharmonic(F4)` → `E#4`; `enharmonic(E#4)` → `F4`; `enharmonic(B4)` → `Cb5`; `enharmonic(C4)` → `B#3`. Round-trip Fact: `enharmonic(enharmonic(n))` returns a note pitch-equivalent to `n` for every chromatic note.
- [x] **DEFER-05
**: `slice(Sequence/Array, start, end)` accepts negative-from-end indices Python-style. `arr@-1` returns the last element; `slice(arr, -3, _)` returns the last 3. Acceptance: `(slice [1, 2, 3, 4, 5] -3 5)` → `[3, 4, 5]`; `(slice [1, 2, 3, 4, 5] 0 -1)` → `[1, 2, 3, 4]`. **Note:** This is a behavioral change to v1.2's silent two-sided clamp (Pitfall 10). Documentation updates the slice contract; existing positive-index call sites unchanged.

### Pragma System & H-Alias

- [x] **PRAG-01**: A pragma system accepts `enable <featureName>;` declarations at the top of `.flow` files only (per D-02; lines after the first non-pragma statement raise a parse error). Lexer pre-scan extracts pragmas before main lexing (Pitfall 4 mitigation). `PragmaRegistry` is a closed set — unknown pragma names raise a clear error citing the known list.
- [x] **PRAG-02**: Pragmas do NOT propagate across `use` imports (per D-02; Pitfall 4 mitigation). Acceptance Fact: importing a module that uses `enable hAsB;` does NOT enable `hAsB` in the importing file unless the importing file also declares it.
- [x] **DEFER-02/03**: `enable hAsB;` pragma activates `H` as a `B` alias inside note-stream context (`| ... |`) only. `H4q` parses identically to `B4q`. Outside note streams, `H` remains a usable identifier (`Int H = 5;` continues to compile). Acceptance: `enable hAsB; ... | H4q B4q |` produces two identical notes; `Int H = 5;` continues to compile.

### Tier B/C Composer DX

- [x] **DX-10**: `arpeggio(chord, rate, direction, pattern)` extends existing `arpeggio` with rate (NoteValue or Fraction) + direction (`"up" / "down" / "updown" / "downup" / "random"`) + pattern (`"linear" / "chord-tone" / "scale-tone"`). Acceptance: `(arpeggio Cmaj7 q "up" "linear")` produces the expected 4-note ascending arpeggio at quarter-note rate. — Shipped 6500412
- [x] **DX-11**: Chord inversions and voicings via `inversion(chord, n)` and `voicing(chord, "drop2" | "drop3" | "open" | "close" | "spread")`. Acceptance: `inversion(Cmaj, 1)` returns `[E4, G4, C5]` (first inversion); `voicing(Cmaj7, "drop2")` lowers the 2nd-from-top note by an octave. — Shipped 5fba059
- [x] **DX-12**: `delay(buffer, noteValueRate, feedback, mix)` overload accepts a NoteValue (or Fraction) as the delay time, computed from active tempo (Pitfall 1 — uses Fraction for sync math). Existing ms-rate overload stays unchanged. Acceptance: `tempo 120 { ... delay(buf, e, 0.5, 0.4) ... }` produces an eighth-note-synced delay (250ms at 120 BPM). — Shipped 98da48e
- [x] **DX-13**: `quantize(sequence, resolution, strength, swing)` snaps note onsets to a grid. Resolution is a NoteValue or Fraction; strength is 0–1 (0=no quantize, 1=hard quantize); swing is -1 to 1. Acceptance: pre-humanized euclidean output snaps cleanly to a 1/16 grid at strength=1. — Shipped d3f5350
- [x] **DX-14**: Legato and portamento articulations: `legato(sequence, overlap)` extends note durations by overlap factor; `portamento(sequence, glideTime)` emits MIDI CC65 (portamento on/off) + CC5 (portamento time) per Sweetwater MIDI spec. Acceptance: MIDI export of `portamento(seq, 100ms)` includes CC65=127 + CC5=64-ish events. — Shipped d2bde5d
- [x] **DX-15**: `loadWav(path, semitones)` and `loadWav(path, ratio)` overloads varispeed-pitch-shift the loaded buffer via OLA + linear/sinc resample. Existing `loadWav(path)` unchanged (defaults to 0 semitones / ratio 1.0). Acceptance: `loadWav("kick.wav", 12)` returns a buffer one octave higher (sample count halved, frequency doubled) compared to `loadWav("kick.wav")`. — Shipped 95582e7

### Microtonal Tuning (Wedge)

- [x] **MICR-01**: Per D-03, three named tunings ship via pragma: `enable justIntonation;` (5-limit JI), `enable pythagorean;` (3-limit), `enable equalTemperament;` (12-TET, default — explicit form for clarity). When active, `Note → frequency` lookup at `PitchConversion.NoteToFrequency` consults the active tuning system instead of the hard-coded `2^((n-69)/12)·440Hz`. Pragma is file-scope per D-02. Acceptance: `enable justIntonation; ...` followed by `play(C4 E4)` produces frequency ratio 5:4 (1.25) instead of 12-TET ~1.2599 (`Math.Pow(2, 4/12)`). — Shipped f6b00ba
- [x] **MICR-02**: Tuning system applies at render-time only (Pitfall 5 mitigation). Existing `transpose`, `invert`, `retrograde`, `augment`, `diminish` transforms remain pitch-class-based and tuning-agnostic. Acceptance: `transpose(seq, 5)` produces the same MIDI pitch numbers under every tuning; only the rendered frequencies differ. — Shipped 8190fb2
- [x] **MICR-03**: Full Scala (`.scl`) loader documented as deferred to v1.4. Pragma registry rejects unknown tunings with a clear error pointing at the documented future expansion. — Shipped 47d7718

### Scale Linting (flow-lsp only)

- [x] **LINT-01**: Per D-02, `enable scaleLint;` pragma activates flow-lsp scale linting. When active, flow-lsp emits `Diagnostic { Severity = Information }` for any note in a `key Cmajor { ... }` context that is non-diatonic. Existing diagnostic plumbing reused — zero flow-lang touch. Acceptance: editing `key Cmajor { | C4 D4 E4 F#4 G4 | }` shows an Information-severity squiggle on `F#4`. — Shipped Phase 24 plans 24-00..24-04
- [x] **LINT-02**: Scale linting is opt-in (Pitfall 8 mitigation — never default-on). Without `enable scaleLint;`, flow-lsp emits zero scale-lint diagnostics. Acceptance Fact: a key-block with non-diatonic notes produces zero scale-lint diagnostics when the pragma is absent. — Shipped Phase 24 plans 24-00..24-04
- [x] **LINT-03**: Scale linting respects nested key contexts (key inside key inside section). Innermost active key wins for diagnostic computation. Acceptance: `key Cmajor { key Gmajor { | F#4 | } }` does NOT flag F#4 (Gmajor is the innermost active key, F# is diatonic in Gmajor). — Shipped Phase 24 plans 24-00..24-04

### Gaussian Humanize (LAST PRNG phase)

- [x] **DEFER-06**: Per D-04, a new `humanizeGaussian(sequence, amount, seed)` built-in applies Gaussian-distributed velocity perturbation via Box-Muller transform. Existing `humanize(...)` (uniform) UNCHANGED — preserves v1.2 byte-identical determinism contract for tutorial.flow + showcase.flow (Pitfall 6 mitigation). Acceptance: `humanizeGaussian(seq, 0.1, 42)` with seed=42 produces deterministic velocity bytes pinned by Fact; existing `humanize(seq, 0.1, 42)` produces identical bytes to v1.2. — Shipped Phase 25 plans 25-00..25-04

### Operator Standardization

- [x] **STD-01**: Parser/AST cleanup. `BinaryExpression` record + `BinaryOperator` enum deleted from `flow-lang/Ast/Expressions/`. `ParseAdditive`/`ParseMultiplicative` methods removed from `Parser.cs`. `ParseUnary`'s arithmetic branch deleted; `ParseUnaryShorthand` handles D-01 `-IDENT → (neg IDENT)` and D-03 silent `+IDENT` strip. `EvaluateBinary` + its switch case deleted from `ExpressionEvaluator.cs`. Music-context Plus/Minus consumers (tempo/swing/pan/gain/reverbTime) PRESERVED. Acceptance: bare infix produces a parse error pointing the user at `(add)`/`(sub)`/`(mul)`/`(div)` per `InfixRejectedFacts`. — Shipped 86fa69a

- [x] **STD-02**: Builtin completion + lexer single-token negative literals. `(add)`/`(sub)`/`(mul)`/`(div)` ship 5 same-type overloads each (Int, Long, Float, Double, Number — D-05 fast paths via direct CLR primitives). `(neg)` ships 5 per-type overloads (D-07). `(idiv Int Int) → Int` ships per D-08. `(div Int Int)` auto-promotes to Double per D-08. Negative number literals `-5`/`-3.14` lex as single tokens at expression-start positions per D-02/D-04. Music-context keywords (tempo/swing/pan/gain/reverbTime) EXCLUDED from gate so `pan -0.5` continues to work (Pitfall 1). `(concat String String)` ships for explicit string concatenation. — Shipped 86fa69a

- [x] **STD-03**: Migrate all in-repo `.flow` files to prefix form; preserve in-session byte-identical `showcase.flow` output; CLAUDE.md updated. Throwaway tokenizer-based migration script at `scripts/Migrate26/`. 8 tracked `.flow` files migrated atomically (2d3efe1). Showcase WAV+MID byte-identical pre/post in-session; Phase 18/23/25 ByteIdenticalShowcase + DefaultTuning + ShowcaseGaussian xUnit guards GREEN. Tutorial-side guard FAILs are blocked by a pre-existing `(str Int[])` overload-coercion bug (orthogonal to Wave 3) — deferred. CLAUDE.md line 148 lambda example rewritten to prefix; line 175 BinaryExpression AST row deleted; new "Prefix-only arithmetic" bullet under Core Language Features. — Shipped 2d3efe1

### Symbols, Tuples, and (unpack)

- [x] **SYM-01**: A new `Symbol` primitive type lives in `flow-lang/TypeSystem/PrimitiveTypes/`. Lexer recognizes `#identifier` as a `SymbolLiteral` token and produces a `SymbolLiteralExpression` AST node. Equality is pointer-compare via global interning — `(eq #foo #foo)` is true on identical interns; `(eq #foo "foo")` is **false** (strict separation from String per discussion 2026-05-09 — Symbol's reason to exist IS the type distinction). Hashable; usable as `Dict<Symbol, V>` key. Acceptance: `(eq #foo #foo)` → true; `(eq #foo #bar)` → false; `(eq #foo "foo")` → false; `Dict<Symbol, Int> d = (dict #kick 60 #snare 70); (get d #kick)` → 60.

- [x] **TUP-09**: A new `Tuple` type lives in `flow-lang/TypeSystem/SpecialTypes/` with per-position types and arity. Literal syntax `<<a, b, c>>` (with `<<>>` empty + `<<x>>` singleton both valid). Type annotation `Tuple<<Note, Beat>>` mirrors literal. `tup@N` indexing matches the existing array-index `@` syntax (charitable per memory) with compile-time bounds checking when arity is known. Destructuring assignment `<<Note pitch, Beat dur>> = expr` works (proc/lambda parameter destructuring deferred to a later phase). Tuples are immutable; equality is structural (`<<1, 2>> == <<1, 2>>` is true). Tuple-of-hashables is a valid Dict key when every component is hashable; component-hashability rejection at type-check time. Acceptance: `<<C4, q>>` parses as `Tuple<<Note, Beat>>`; `<<>>@0` is a compile error; `<<C4>>@0` returns C4; `<<a, b>> = <<1, 2>>` binds a=1 b=2; `(eq <<1, 2>> <<1, 2>>)` → true.

- [x] **TUP-10**: New flow operator `~>` unpacks a tuple into a multi-arg call as a parse-time transform. `tup ~> func(extra)` becomes `func(tup@0, tup@1, ..., extra)` at parse time. On non-tuple LHS, `~>` falls through to behave identically to `->` (charitable per memory `feedback_charitable_interpretation`). Acceptance: given `proc add3(Int a, Int b, Int c)`, `<<1, 2, 3>> ~> add3` calls `(add3 1 2 3)`; given `Int x = 5`, `x ~> doubleIt` calls `(doubleIt 5)` (non-tuple → `->` semantics).

- [x] **TUP-11**: A new `(unpack tuple func)` runtime builtin applies an unpacked tuple to a function value — the S-expression-style first-class equivalent of `~>`. Mirrors Lisp/Scheme's `(apply f args)`. Ships **alongside** `~>`, not as a replacement; `~>` shines in chain syntax, `(unpack)` shines in dynamic-dispatch and HOF-composition patterns where the function is a `Function`-typed value. Type-checks the tuple's per-position types against the function's parameter types when both are statically known. Acceptance: `(unpack <<>> getFortyTwo)` → 42; `(unpack <<5>> doubler)` → 10; `(unpack <<C4, q>> renderHit)` ≡ `<<C4, q>> ~> renderHit`; `Function f = (get handlers eventType); (unpack event f)` works when `f` is a runtime `Function` value (dynamic dispatch). Implementation: ~30 LOC + 4-theory regression Fact (zero-arg, single-arg, multi-arg, dynamic-Function-value).

### Dictionary Support

- [x] **DICT-01**: A new generic `Dict<K, V>` type lives in `flow-lang/TypeSystem/SpecialTypes/`. Allowed key types are an 8-element allowlist: Int, Long, Float, String, Symbol, Note, Chord, Tuple-of-hashables (recursive — every component must be hashable). Disallowed key types are rejected at parse-time at the annotation site with a `ParseException` citing the allowlist. S-expression constructors: `(dict K V K V ...)` flat interleaved + `(dictTuple <<K,V>> <<K,V>> ...)` tuple-pair (memory: "Keep functional S-expression style, no infix operators"). Empty dict via `(dict)`. Type inference: `Dict<K, V>` annotation specifies K and V; runtime constructor narrows to the actual element types. Acceptance: `(dict #kick 90 #snare 70)` returns a `Dict<Symbol, Int>` with size 2; `Dict<Buffer, Int> bad = ...` raises a parse error. — Shipped daaa023

- [x] **DICT-02**: 14-op dict surface, all immutable (mutations return new dicts). `(get d k)` returns the value at `k` or `Value.Void()` (Flow's "Nothing" sentinel) when absent. `(getOr d k default)` returns `default` when absent. `(set d k v)` returns a NEW dict with `k → v`. `(remove d k)` returns a new dict without `k`. `(has d k)` → `Bool`. `(keys d)` → `Array[K]` in insertion order. `(values d)` → `Array[V]` in insertion order. `(size d)` → `Int`. `(merge d1 d2)` last-write-wins (d2 keys override d1). NaN-key special-case scoped to Dict-internal equality only (Float NaN as self) — Flow's general `(equals nan nan)` continues to follow IEEE 754 (returns false). Missing-key behavior is not an error per the charitable-interpretation memory. Acceptance: `(get (dict "kick" 1) "missing")` → `Nothing`; `(set d "kick" 2)` returns NEW dict, original `d` unchanged; `(merge (dict #a 1) (dict #a 2 #b 3))` → size 2, get #a returns 2. — Shipped daaa023

- [x] **DICT-03**: Functional iteration + introspection: `(each d cb)` yields `<<key, value>>` per entry and invokes the callback via `~>` semantics — the dict-side internally unpacks the tuple into 2 positional args so the user writes a normal `(fn Symbol k, Int v => ...)` 2-arg lambda (no lambda-side destructuring). `(map d cb)` returns `Dict<K, V'>` with values transformed (keys preserved). `(filter d pred)` returns `Dict<K, V>` with entries where `pred(K, V) → true`. INSERTION ORDER preserved across all ops (not hash order — preserves byte-identical determinism contract). Acceptance: `(keys (dict "kick" 1 "snare" 2 "hihat" 3))` → `["kick", "snare", "hihat"]` in insertion order; `(each)` over Dict invokes 2-arg lambda; Pitfall 6 — separate `(each Dict Function)` overload coexists with existing `(each Array Function)`. — Shipped daaa023

### Music Type Ergonomics + FX Overloads

- [x] **ERG-01**: Music-type numeric compatibility completeness. `Millisecond` and `Second` ship `IsCompatibleWith(Double|Float)` overrides (mirroring the existing `CentType.cs:24-27` precedent that `Decibel` and `Beat` adopted via QUICK-260504-w24). `Semitone` STAYS Int-only — semitones are whole-numbers-by-design; fractional pitch shifts go through `Cent`. Existing `Millisecond.CanConvertTo(Second)` + `Second.CanConvertTo(Millisecond)` cross-conversions PRESERVED — `(delay buf 0.1s ...)` continues to reach `delay(Buffer, Millisecond, ...)` via convertible-score 100. Acceptance: `MillisecondType.Instance.IsCompatibleWith(DoubleType.Instance)` returns true; `SecondType.Instance.IsCompatibleWith(FloatType.Instance)` returns true; `SemitoneType.Instance.IsCompatibleWith(DoubleType.Instance)` returns false (D-03 canary); `(delay buf 100.0 0.5 0.4)` and `(delay buf 100ms 0.5 0.4)` both resolve. — Shipped 4f92c24

- [x] **ERG-02**: FX overload registration on every site where the parameter is conceptually musical. New music-typed overloads ship for: `delay(Buffer, Millisecond, Double, Double)`, `compress(Buffer, Decibel, Double, Millisecond, Millisecond)`, `sidechain(Buffer, Buffer, Decibel, Double, Millisecond, Millisecond)`, `reverb(Buffer, Double, Second)`, `lowpass(Buffer, Hertz)` / `highpass(Buffer, Hertz)` / `bandpass(Buffer, Hertz, Hertz)`, `createSineTone(Double, Hertz, Double)` (C# overload), and Flow-side proc overloads for `createSawTone` / `createSquareTone` / `createTriangleTone` with Hertz frequency parameter. Bare-Double overloads PRESERVED — coexist via OverloadResolver exact-match scoring (1000 vs compat-500). Reverb-Second overload does NOT ambiguate with `reverb(Buffer, Double, Double)` per RESEARCH Pitfall 3 score arithmetic. Acceptance: `(compress buf -12dB 4.0 5ms 100ms)` produces per-sample-identical output to `(compress buf -12.0 4.0 5.0 100.0)` within 1e-6f; `(reverb buf 0.5 1.5)` and `(reverb buf 0.5 1.5s)` resolve to distinct overloads (no Ambiguous overload error). — Shipped dfbfa1f

- [x] **ERG-03**: `gain` dB-vs-linear policy decided + new `volume(Buffer, Double)` shipping. `gain` STAYS dB-only — both `gain(Buffer, Double)` (existing) and `gain(Buffer, Decibel)` (existing, shipped via QUICK-260504-w24 + audio.flow forward decl shipped here) treat second arg as decibels. New `volume(Buffer, Double)` treats second arg as linear multiplier (0.5 = half-amplitude, 2.0 = double-amplitude). Function name documents the unit; composer chooses by semantic intent. ONE overload — Float / Int / Long inputs reach it via existing primitive widening chain. Negative values rejected via `InvalidOperationException` (volume can't phase-invert; out of scope). Clipping warning emitted to stderr when post-multiplication samples exceed 1.0 (mirrors GainEffect shape). NO educational hint when `(gain buf 0.5)` is called with arg in `(0, 1)` — `(gain buf 0.5)` is a legitimate 0.5dB attenuation. CLAUDE.md updated to document the split. Acceptance: `(volume buf 0.5)` halves amplitude per non-zero sample; `(volume buf 2.0)` doubles + emits clipping warning; `(volume buf -0.5)` errors with InvalidOperationException; `(gain buf 0.5)` STAYS at 0.5dB attenuation (~5.9% louder than 0dB unity), NOT 50% as a linear interpretation would produce. — Shipped 6df301e

- [x] **ERG-04**: `Hertz` type ships with `Hz` + `kHz` literal syntax + filter / generator overloads. New `HertzType` in `flow-lang/TypeSystem/SpecialTypes/HertzType.cs` mirrors `CentType` exactly (sealed FlowType singleton, `IsCompatibleWith(Double|Float)`, `GetSpecificity()=144` unique among music types). Stored as a single canonical Hz double (kHz × 1000 at lex time — no unit-discriminator at runtime). Both `Hz` and `kHz` suffixes lex as single `HertzLiteral` tokens via three coordinated paths in `SimpleLexer.cs`: `ScanNumberOrSpecialLiteral` (unsigned), `TryLookAheadSpecialLiteral` (signed prefix), and the `TryLexAngleAngle` predecessor set bumped to include HertzLiteral so `<<800Hz, 1200Hz>>` tuples parse. `mHz` (millihertz) NOT shipped in 26.2 — defer until LFOs land. Hertz overload coverage spans filters (`lowpass` / `highpass` / `bandpass`) + signal generators (`createSineTone` C#; `createSawTone` / `createSquareTone` / `createTriangleTone` Flow-side proc overloads in audio.flow). NO PitchConversion APIs added — Open Question #1: `noteToFrequency` only RETURNS Hz, doesn't take Hz; no Hz-taking PitchConversion API exists. Acceptance: `Hertz freq = 800Hz; (eq freq 800.0)` is true; `Hertz freq = 1.5kHz; (eq freq 1500.0)` is true; `(lowpass buf 800Hz)` produces per-sample-identical output to `(lowpass buf 800.0)`. — Shipped f12d648 + d655c65 + 28158cc + dfbfa1f + 821e9d0

- [x] **ERG-05**: `-12dB` / `-100ms` / `-50c` / `+440Hz` literal lexing at expression-start positions closure (D-14). Root cause traced via RESEARCH Pitfall 1 to a missing `if (targetType is DoubleType) return Double(doubleVal);` arm in `Value.cs:155-161` (NOT a lexer bug as the original CONTEXT hypothesized). Defence-in-depth fix: (a) `Value.ConvertTo` Double-arm patch shipped in Wave 0 covers Decibel→Double / Beat→Double / Cent→Double / Ms→Double / Sec→Double / Hertz→Double coercion in any user-proc / lambda call site; (b) `audio.flow` `internal proc gain(Buffer: buffer, Decibel: gainDb)` forward declaration shipped in Wave 2 surfaces the dormant C# `gain(Buffer, Decibel)` registration (RESEARCH Pitfall 2) so the dedicated Decibel overload now wins resolution at exact-match score 1000. Both fix paths exist for redundancy. The 2 pre-existing failing `DecibelBeatNumericCompatFacts` (`GainWithDecibelLiteral_…` + `GainWithPositiveDecibelLiteral_…`) flip RED→GREEN. Sibling regression facts pin `+6dB`, `-100ms`, `-50c`, `+440Hz` at the `LParen-after` expression-start position. Acceptance: `(gain src -12dB)` produces per-sample-identical output to `(gain src -12.0)` within 1e-6f; `(transpose seq -50c)` resolves cleanly (Cent canary); `(lowpass buf +440Hz)` resolves cleanly. — Shipped 45b01fb + 28158cc

### Quality of Life

- [x] **QOL-04**: `examples/tutorial.flow` and `examples/showcase.flow` refreshed to demonstrate every v1.3 feature end-to-end. Language additions: prefix-only arithmetic via `(add)`/`(sub)`/`(mul)`/`(div)`/`(idiv)`/`(neg)`/`(concat)` (Phase 26 STD-01..03); `Symbol` primitive `#foo` (Phase 26.1 SYM-01); `Tuple <<a, b, c>>` literal + `tup@N` indexing + destructuring assignment + `~>` flow op + `(unpack)` runtime (Phase 26.1 TUP-09/10/11); generic `Dict<K, V>` 14-op surface — flat `(dict K V K V)` + tuple-pair `(dictTuple <<K,V>> ...)` constructors + `get`/`getOr`/`set`/`remove`/`has`/`keys`/`values`/`size`/`merge`/`each`/`map`/`filter` (Phase 26.1 DICT-01/02/03). Music features: tuplets `{3:2 ...}q` bracket + `{3 ...}q` shorthand + per-note `C4/12` fractional + `C4/3:2` per-note tuplet shorthand + nested tuplets (Phase 19 TUP-01..08); `range(Int, Int)` / `range(Int, Int, Int)` (Phase 20 DEFER-01); multi-letter enharmonics E↔Fb / F↔E# / B↔Cb / C↔B# (Phase 20 DEFER-04); negative slice `arr@-1` / `(slice arr -3 _)` Python-style (Phase 20 DEFER-05); `enable hAsB;` H-as-B alias pragma (Phase 21 PRAG-01/02 + DEFER-02/03); DX-10..15 composer DX bundle — `arpeggio` rate/direction/pattern, chord `inversion`+voicings, NoteValue-rate `delay` overload, `quantize` to grid, `legato`/`portamento`, varispeed-`loadWav` (Phase 22); microtonal pragmas `enable justIntonation;` / `pythagorean;` / `equalTemperament;` (Phase 23 MICR-01..03); scale-lint pragma `enable scaleLint;` print-only mention (Phase 24 LINT-01..03 — flow-lsp owns surface); `humanizeGaussian(seq, amount, seed)` Gaussian-bell velocity perturbation (Phase 25 DEFER-06). Phase 26.2 surface: `volume(Buffer, Double)` linear-multiplier alongside `gain` dB-only split (ERG-03); Hertz literals `440Hz` / `1.5kHz` with kHz canonical-Hz lex (ERG-04); Ms-typed FX overloads on `delay`/`compress`/`sidechain` (ERG-02); Second-decay `(reverb buf mix 1.5s)` (ERG-02); Hertz overloads on `lowpass`/`highpass`/`bandpass` filters + `createSineTone` signal-generator (ERG-04 — runnable demo for createSineTone-Hertz; the same Hertz overload pattern applies mechanically to `createSawTone`/`createSquareTone`/`createTriangleTone`, not separately demoed in tutorial); `(gain buf -12dB)` literal at expression-start positions (ERG-05); `Millisecond.IsCompatibleWith(Double|Float)` + `Second.IsCompatibleWith(Double|Float)` numeric-compat completeness (ERG-01). Companion files under `examples/pragmas/`: `h_alias.flow` (~38 lines, `enable hAsB;` demo) + `microtonal_ji.flow` (~42 lines, `enable justIntonation;` demo with frequency-ratio comparison print). Both tutorial + showcase scripts run to completion (exit 0) producing non-empty WAV + MIDI; byte-identical determinism contract holds across two consecutive runs (cmp-clean) — `Phase18ByteIdenticalTutorialTests` + `Phase18ByteIdenticalShowcaseTests` + `Phase25ByteIdenticalShowcaseGaussianTests` + new `Phase27ByteIdenticalPragmaTests` (4 facts pinning `h_alias.flow` + `microtonal_ji.flow` run-twice identity). CLAUDE.md Music Types Quick Reference table appended for composer + future-agent reference. v1.1 + v1.2 chapters preserved. — Shipped ace6416

---

## Future Requirements (deferred)

- **Full Scala (`.scl`) loader** — `tuning loadScala("path.scl") { ... }` musical-context block; deferred to v1.4 per D-03 (heavy: 18+ file blast radius for arbitrary tuning systems)
- **Phase-vocoder time-preserving pitch shift** for loadWav — explicit anti-feature for v1.3 (no clean single-file pure-C# implementation; varispeed-only ships in DX-15)
- **Auto-derived chord-tone / scale-tone arpeggio sequencing** beyond the basic `pattern` enum in DX-10
- **Block-scope pragmas** — deferred per D-02; file-scope only in v1.3
- **Audit §2 hardening** — overload ambiguity, bandpass Q unbounded, stereo voices played as mono, ChordParser sharp formatting, scale database brittleness, OverloadResolver top-2 tie check
- **Pidgin parser combinator dependency removal** — referenced but unused in csproj; opportunistic cleanup

## Out of Scope (for v1.3)

- v1.2 open audit items (debug session `function-overload-resolution-failures`, quick task pure-Flow test library, Phase 17 HUMAN-UAT 3 rows, Phase 04 verification gaps) — recorded in STATE.md Deferred Items, NOT pulled into v1.3 unless user explicitly opts in
- ABC `(p:q:r` counter-form tuplet syntax — anti-feature (bracket parens make `r` redundant)
- Default-on scale linting — anti-feature (composers expect non-diatonic notes by design)
- Global `H` lexer alias outside note streams — anti-feature (would break user identifiers)
- NAudio/CSCore/NWaves integration — minimal-deps philosophy stands
- New NuGet packages of any kind — confirmed unnecessary by SUMMARY.md research
- GUI/DAW interface, VST/AU hosting, multi-user collaboration, cloud deploy — project-level out-of-scope, unchanged from v1.2

---

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| FRAC-01 | Phase 18 | Shipped 2092f32 |
| FRAC-02 | Phase 18 | Shipped ba8534a |
| TUP-01 | Phase 19 | Shipped a7f94ef |
| TUP-02 | Phase 19 | Shipped a7f94ef |
| TUP-03 | Phase 19 | Shipped a7f94ef |
| TUP-04 | Phase 19 | Shipped 9aae23c |
| TUP-05 | Phase 19 | Shipped 3679ab4 |
| TUP-06 | Phase 19 | Shipped dbc6f30 |
| TUP-07 | Phase 19 | Shipped e2cdbe5 |
| TUP-08 | Phase 19 | Shipped 9aae23c |
| DEFER-01 | Phase 20 | Shipped d0d17db |
| DEFER-04 | Phase 20 | Shipped d835336 |
| DEFER-05 | Phase 20 | Shipped edd20b1 |
| PRAG-01 | Phase 21 | Shipped 60f7f18 |
| PRAG-02 | Phase 21 | Shipped 60f7f18 |
| DEFER-02/03 | Phase 21 | Shipped 05c2174 |
| DX-10 | Phase 22 | Shipped 6500412 |
| DX-11 | Phase 22 | Shipped 5fba059 |
| DX-12 | Phase 22 | Shipped 98da48e |
| DX-13 | Phase 22 | Shipped d3f5350 |
| DX-14 | Phase 22 | Shipped d2bde5d |
| DX-15 | Phase 22 | Shipped 95582e7 |
| MICR-01 | Phase 23 | Shipped f6b00ba |
| MICR-02 | Phase 23 | Shipped 8190fb2 |
| MICR-03 | Phase 23 | Shipped 47d7718 |
| LINT-01 | Phase 24 | Shipped Phase 24 plans 24-00..24-04 |
| LINT-02 | Phase 24 | Shipped Phase 24 plans 24-00..24-04 |
| LINT-03 | Phase 24 | Shipped Phase 24 plans 24-00..24-04 |
| DEFER-06 | Phase 25 | Shipped Phase 25 plans 25-00..25-04 |
| STD-01 | Phase 26 | Shipped 86fa69a |
| STD-02 | Phase 26 | Shipped 86fa69a |
| STD-03 | Phase 26 | Shipped 2d3efe1 |
| SYM-01 | Phase 26.1 | Shipped 35474ed |
| TUP-09 | Phase 26.1 | Shipped 6549116 |
| TUP-10 | Phase 26.1 | Shipped d628870 |
| TUP-11 | Phase 26.1 | Shipped d628870 |
| DICT-01 | Phase 26.1 | Shipped daaa023 |
| DICT-02 | Phase 26.1 | Shipped daaa023 |
| DICT-03 | Phase 26.1 | Shipped daaa023 |
| ERG-01 | Phase 26.2 | Shipped 4f92c24 |
| ERG-02 | Phase 26.2 | Shipped dfbfa1f |
| ERG-03 | Phase 26.2 | Shipped 6df301e |
| ERG-04 | Phase 26.2 | Shipped f12d648 + d655c65 + 28158cc + dfbfa1f + 821e9d0 |
| ERG-05 | Phase 26.2 | Shipped 45b01fb + 28158cc |
| QOL-04 | Phase 27 | Shipped ace6416 |

---

## v1.4 Phase 30 — Flow CLI + Formal Install (cross-milestone insert)

v1.3 milestone shipped 2026-05-10 with Phase 27. The v1.4 milestone (Phases 28-34) opened
with Phase 28 (shipped 2026-05-10) and Phase 30 (shipped 2026-05-11). Full v1.4 REQ tracking
will move to its own REQUIREMENTS.md when `/gsd-new-milestone` is invoked; this section is
a Phase 30 anchor so cross-references from the SUMMARY / ROADMAP land somewhere stable.

REQ-IDs map 1:1 to `.planning/phases/30-flow-cli-formal-install/30-SPEC.md` requirements 1-8.

| REQ | Phase | Status |
|-----|-------|--------|
| REQ-1 (Unified `flow` binary, 11 subcommands) | Phase 30 | Shipped fa66c38 + 48761cb + 8bcc8c0 + 303bddd |
| REQ-2 (Self-contained Linux x64 single-file ≤120 MB; actual 38 MB) | Phase 30 | Shipped fc6fead |
| REQ-3 (install.sh per-user + --system, idempotent) | Phase 30 | Shipped c31f36d |
| REQ-4 (XDG ~/.config/flow/config.toml, 5 keys, all 4 optional wired) | Phase 30 | Shipped 475838c + f8ca1ed + a34c904 + 8116b2f |
| REQ-5 (midi2flow flat per-track output, AddSplitTracks deleted) | Phase 30 | Shipped 63eb787 + a7170dd + 303bddd |
| REQ-6 (Round-trip ±1 tick on 3 CC0 fixtures) | Phase 30 | Shipped a7170dd + a026afb |
| REQ-7 (test-install.sh smoke ≤60s; actual 8s) | Phase 30 | Shipped 984fa39 |
| REQ-8 (dotnet run --project flow-interpreter still works) | Phase 30 | Shipped (preserved across all 9 plans) |

---

## v1.4 Phase 33 — SFZ Orchestral Sampler (cross-milestone insert)

Phase 33 ships an opt-in SFZ-format orchestral sampler gated behind `use "@sfz"`,
so composers can load CC-licensed external libraries (blessed: VSCO Community CE 1.1.0)
via `loadSfz #violin` style calls without retrofitting the Phase 29 bundled-sample path.
Phase 33 is purely additive — Phase 29's `renderSong song "piano"` byte-identical
contract is preserved.

REQ-IDs map 1:1 to `.planning/phases/33-sfz-orchestral-sampler/33-SPEC.md` requirements 1-8;
all 8 are locked and ship in this phase. Status `locked` means: spec criterion is closed,
implementation lands in the cited Phase 33 plan(s), and a passing test gate exists in
`flow-lang.Tests/{Unit,Integration}/Phase33/`.

| SPEC | Phase | Status |
|------|-------|--------|
| SPEC-1 (`use "@sfz"` stdlib import gates the SFZ surface) | Phase 33 | Shipped 37dfea0 + 043d3a3 (Plan 33-05) + 20ee7d3 (Plan 33-07 sampler-side gate) |
| SPEC-2 (Symbol-keyed instrument lookup via shipped 19-entry GM dict + `sfz_root` config) | Phase 33 | Shipped 0d619fb (Plan 33-02 SfzRoot POCO) + 37dfea0 + 043d3a3 (Plan 33-05) |
| SPEC-3 (SFZ parser: 13-opcode common subset + 3 header types + `<control>` extension) | Phase 33 | Shipped a3c4150 + ad3d017 (Plan 33-04) |
| SPEC-4 (Region matching by `(pitch, velocity)` + nearest-pitch varispeed fallback) | Phase 33 | Shipped 718b0fa + afdbfab (Plan 33-06) |
| SPEC-5 (Equal-power 441-frame loop crossfade prevents audible boundary clicks) | Phase 33 | Shipped afdbfab (Plan 33-06 SfzRenderer + SfzLoopCrossfadeTests) |
| SPEC-6 (`Sfz` value type + `sampler:NAME` instrument dispatch + binding registry) | Phase 33 | Shipped 671254c + 0d619fb (Plan 33-02 SfzType) + d6681d4 + 20ee7d3 (Plan 33-07) |
| SPEC-7 (CI smoke renders synthetic fixture; non-empty + RMS > -40 dBFS + discontinuity ≤ 0.05) | Phase 33 | Shipped 9b13681 + 49dbc34 (Plan 33-01 fixture + repo-size gate) + 8772635 (Plan 33-08 SfzSmokeTests) |
| SPEC-8 (Phase 28 articulation envelope + `ampeg_attack` override apply on top of SFZ render) | Phase 33 | Shipped afdbfab (Plan 33-06 envelope hook) + 8772635 (Plan 33-08 SfzArticulationTests) |

Two-run byte-identical determinism contract (Phase 18/25/27 inheritance) preserved
end-to-end through the SFZ surface — verified by `Phase33.SfzDeterminismTests`
(shipped 8772635 in Plan 33-08). Phase 29 bundled-sample byte-identical regression
gate (`Phase29ByteIdenticalTests`) stays 6/6 green across all Phase 33 plans.

---

## v1.4 Phase 34 — Symphony Showcase (v1.4 closer — pre-public → public pivot)

Phase 34 ships the v1.4 headline artifacts — a curated ~60 s minimalist-orchestral symphony
("In Five Voices") for 5 VSCO Community CE 1.1.0 instruments rendered through the Phase 33
SFZ surface, plus a ~58 s solo-piano ragtime companion ("Stride & Stomp") added during
scope-expand for genre-agnostic demonstration — plus the public-facing release machinery
(v1.4.0 annotated tag + GitHub Release with 5 labeled assets: symphony.mp3+wav,
ragtime.mp3+wav, flow-linux-x64.tar.gz; top-level README.md `## Showcase` section with
user-attachments inline audio embed; docs/announcements/v1.4.0.md announcement draft) and
v1.4 milestone closure docs (PROJECT/ROADMAP/STATE/REQUIREMENTS/MILESTONES + CLAUDE.md +
external memory file rewrite).

REQ-IDs map 1:1 to the 5 ROADMAP Phase 34 success criteria, formalized as SYM-01..05 in
`.planning/phases/34-symphony-showcase-v1-4-closer-pre-public-public-pivot/34-RESEARCH.md`.

| SPEC | Phase | Status |
|------|-------|--------|
| SYM-01 (Symphony renders end-to-end via SFZ sampler, two-run cmp-clean) | Phase 34 | Shipped d684086 + 8e4ad6f + 62b16d5 (Plans 34-01 + 34-02) |
| SYM-02 (Composer "postable on GitHub" sign-off recorded in 34-HUMAN-UAT.md) | Phase 34 | Shipped 7b68647 + 463d240 (Plan 34-01 UAT iterations #2) |
| SYM-03 (Code paired with audible features: articulation, polyphony, voicePool, tuplets) | Phase 34 | Shipped d684086 + 8e4ad6f (Plan 34-01 — every Phase 28 articulation token + `{voice}` polyphony + `{3:2}` tuplet + voicePool 32) |
| SYM-04 (README.md showcase + user-attachments audio embed + examples/symphony/README.md reproduction) | Phase 34 | Shipped 62b16d5 + a00820d (Plans 34-02 + 34-03) |
| SYM-05 (v1.4.0 tag + GitHub Release + announcement draft + milestone closure) | Phase 34 | Shipped 4547204 (Plan 34-04 announcement) + Plan 34-05 (tag 66842d6e + Release on commit 74de69a, no repo-changes) + Plan 34-06 (this closure commit) |

Two-run byte-identical determinism contract (Phase 18/25/27/33 inheritance) preserved
end-to-end through the real VSCO-CE library — verified manually by composer at release
time per D-702.

Release: https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0

---

## v1.4 Milestone Closure (2026-05-16)

v1.4 Audio Fidelity, Distribution & Public Showcase shipped 2026-05-16.

**Phases:** 28 (MIDI + Audio Polyphony & Articulation Rewrite), 29 (Instrument Realism),
30 (Flow CLI + Formal Install), 31 (LSP Enhancements + JetBrains Stretch), 32 (Full Scala
(.scl) Tuning Loader), 33 (SFZ Orchestral Sampler), 34 (Symphony Showcase — v1.4 closer)

**Plans completed:** 52 across the 7 v1.4 phases (Phase 28 = 7, Phase 29 = 7, Phase 30 = 9,
Phase 31 = 9, Phase 32 = 7, Phase 33 = 7, Phase 34 = 6).

**Release:** https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0

**Headline artifacts:** examples/symphony/symphony.flow ("In Five Voices", D minor,
~60s, 5 VSCO-CE instruments) + examples/ragtime/ragtime.flow ("Stride & Stomp",
F major, ~58s, solo VSCO-CE UprightPiano).

**Pre-public → public pivot:** Flow's demonstrated v1.4 API surface is now effectively
public. Breaking changes hereafter require a deprecation cycle (see CLAUDE.md § Goals
"Public as of v1.4" footnote + the external memory file
`project_pre_public_no_legacy_burden.md` rewritten 2026-05-16 to reflect post-public
footing).

**v1.5 carryover candidates:** captured in `.planning/MILESTONES.md` v1.4 entry's
"Forward-deferred items" block + `34-HUMAN-UAT.md` ragtime `closed_with_followup` note
(warmer-piano timbre / SFZ velocity layers / humanizeGaussian voice-block bug); also
flute D5 timbre crossover gap, sampled drum transient-preserving pitch shift, stereo
panning across instruments, second showcase contrasting genre.

---

## Notes

- Phase numbering continues from v1.2 (last phase: 17). v1.3 starts at Phase 18.
- Five binding pre-ordering constraints from PITFALLS map into roadmap shape:
  1. FRAC-* MUST precede TUP-* (rational arithmetic before tuplet syntax) → Phase 18 → Phase 19
  2. PRAG-* MUST precede DEFER-02/03 (H-alias) AND LINT-* (scale lint) → Phase 21 → DEFER-02/03 in 21, LINT in Phase 24
  3. Audit/spike DEFER-04 MUST precede DEFER-02/03 (multi-letter enharmonics before H-alias) → Phase 20 (DEFER-04) → Phase 21 (DEFER-02/03)
  4. MICR-* MUST be its own phase (highest blast radius — even with wedge scope) → Phase 23
  5. DEFER-06 (Gaussian) MUST be the LAST PRNG-touching phase (byte-identical determinism) → Phase 25 (after all other PRNG-touching phases close)
