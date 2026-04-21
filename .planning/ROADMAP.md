# Roadmap: Flow Language

## Milestones

- ~~**v1.0 MVP**~~ — Phases 1-5 (shipped 2026-04-03)
- ✅ **v1.1 Polish & Foundations** — Phases 6-10 (shipped 2026-04-18) — see `milestones/v1.1-ROADMAP.md`
- 🚧 **v1.2 Stability & Composer DX** — Phases 11-17 (started 2026-04-18)

## Phases

<details>
<summary>v1.0 MVP (Phases 1-5) — SHIPPED 2026-04-03</summary>

- [x] **Phase 1: Language Foundations** — Add loops, string interpolation, iteration guards, and sequence visualization (completed 2026-04-01)
- [x] **Phase 2: Audio Pipeline** — Add sample loading, stereo panning, sidechain compression, and polyphonic voice allocation (completed 2026-04-02)
- [x] **Phase 3: Synthesis & MIDI Export** — Add custom oscillator definitions and MIDI file export (completed 2026-04-02)
- [x] **Phase 4: Composition Tools** — Add chord progression DSL, polyrhythm support, and probabilistic pattern variation (completed 2026-04-02)
- [x] **Phase 5: Live Coding** — Add beat-synced live reload with playback state preservation (completed 2026-04-03)

### Phase 1: Language Foundations
**Goal**: Users can write iterative, debuggable Flow scripts with loop constructs, formatted output, and visual feedback on their sequences
**Depends on**: Nothing (first phase)
**Requirements**: LANG-01, LANG-02, LANG-03, LANG-04, VIS-01
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md -- Add for/while loops, break/continue, and iteration guards
- [x] 01-02-PLAN.md -- Add string interpolation with $"...{expr}..." syntax
- [x] 01-03-PLAN.md -- Add ASCII piano-roll sequence visualization

### Phase 2: Audio Pipeline
**Goal**: Users can load audio samples, position sounds in the stereo field, apply sidechain compression, and play polyphonic arrangements without voice clipping
**Depends on**: Phase 1
**Requirements**: AUDIO-01, AUDIO-02, AUDIO-03, AUDIO-04
**Plans**: 3 plans

Plans:
- [x] 02-01-PLAN.md -- WAV file loading (loadWav) and sidechain compression
- [x] 02-02-PLAN.md -- Stereo panning (pan function, Voice.Pan bug fix, pan context block)
- [x] 02-03-PLAN.md -- Polyphonic voice allocation with configurable limits and stealing

### Phase 3: Synthesis & MIDI Export
**Goal**: Users can define their own oscillator waveforms in Flow code and export compositions as standard MIDI files
**Depends on**: Phase 2
**Requirements**: SYNTH-01, SYNTH-02, MIDI-01, MIDI-02
**Plans**: 2 plans

Plans:
- [x] 03-01-PLAN.md -- Custom oscillator definitions (WavetableSynthesizer + oscillator() built-in)
- [x] 03-02-PLAN.md -- MIDI file export (DryWetMidi + writeMidi built-in)

### Phase 4: Composition Tools
**Goal**: Users can write chord progressions with automatic voicing, layer polyrhythmic patterns, and generate probabilistic variations of sequences
**Depends on**: Phase 3
**Requirements**: COMP-01, COMP-02, COMP-03, COMP-04
**Plans**: 2 plans

Plans:
- [x] 04-01-PLAN.md -- Chord progression DSL with voice leading (progression keyword, parser, ProgressionCompiler)
- [x] 04-02-PLAN.md -- Polyrhythm layering and probabilistic pattern variation (polyrhythm, vary built-ins)

### Phase 5: Live Coding
**Goal**: Users can edit Flow scripts during playback and hear changes take effect at musically appropriate moments without interruption
**Depends on**: Phase 4
**Requirements**: LIVE-01, LIVE-02
**Plans**: 2 plans

Plans:
- [x] 05-01-PLAN.md -- Streaming playback infrastructure, capture mode, and LiveReloadManager
- [x] 05-02-PLAN.md -- Wire LiveReloadManager into Program.cs and end-to-end verification

</details>

<details>
<summary>✅ v1.1 Polish & Foundations (Phases 6-10) — SHIPPED 2026-04-18</summary>

- [x] **Phase 6: Diagnostics & Bug Fixes** — --verbose flag, Sequence overload fixes, section bare expressions, error masking (completed 2026-04-04)
- [x] **Phase 7: Developer Experience** — // line comments, math stdlib, writeWav, REPL auto-imports (completed 2026-04-04)
- [x] **Phase 8: Audio Production** — mix(), per-section gain, strings/organ/bell synth presets (completed 2026-04-04)
- [x] **Phase 9: Advanced Features** — tempoRamp, interactive tutorial (completed 2026-04-04)
- [x] **Phase 10: Vocalization** — formant sing() + external TTS hook (completed 2026-04-04)

Full details: `milestones/v1.1-ROADMAP.md` · Audit: `milestones/v1.1-MILESTONE-AUDIT.md`

</details>

### v1.2 Stability & Composer DX (Phases 11-17) — in progress

- [x] **Phase 11: Audit Spike** — Reproduce or close C1–C5 audit claims with failing tests or documented dismissals (completed 2026-04-19; 1 Confirmed C1, 4 Dismissed C2–C5)
- [x] **Phase 12: Stability** — Ship confirmed bug fixes (C6 → FIX-05, C7 → FIX-06, C1 → FIX-07a), reframe TEST-03 around real failures (if-overload + auto-mkdir), and unblock the failing test suite (completed 2026-04-19; 4 Shipped + 2 Closed as audit false positives; 68/68 suite green; C5 BREAKING CHANGE bundle NOT TRIGGERED per F-02)
- [x] **Phase 13: Nyquist Validation Backfill** — Retroactive VALIDATION.md for v1.1 phases 6-9 shipped + Phase 10 promoted to nyquist_compliant (completed 2026-04-20)
- [x] **Phase 14: Composer DX Part 1** — `slice`, flat-literal surface + `enharmonic()`, MIDI velocity regression end-to-end (completed 2026-04-20; DX-05/06/08 shipped, H-alias deferred to future pragma phase via deferred-items.md)
- [ ] **Phase 15: Composer DX Part 2** — Euclidean swing/humanize (reuses velocity infra), then `reverbTime` context block (widest blast radius, shipped last)
- [ ] **Phase 16: Tutorial Refresh** — `examples/tutorial.flow` demonstrates v1.1 + v1.2 features end-to-end, produces audible WAV + MIDI
- [x] **Phase 17: Flow Language Server** — Build a language server for Flow (LSP) plus a VSCode extension delivering syntax highlighting, diagnostics, and intelligent completion/hover suggestions for .flow files (completed 2026-04-20; 8/8 plans shipped, 117/117 Phase17 Facts green after code-review fix pass, 3 manual-smoke rows tracked as pending HUMAN-UAT in 17-HUMAN-UAT.md, rows 4-5 deferred to first release tag)

## Phase Details

### Phase 11: Audit Spike
**Goal**: The v1.2 team has decisive evidence — failing tests or written dismissals — for each of the 2026-04-18 audit's disputed critical findings (C1–C5), so no code is edited while researchers disagree about whether the bug exists.
**Depends on**: v1.1 close (Phase 10)
**Requirements**: SPIKE-01, SPIKE-02, SPIKE-03, SPIKE-04, SPIKE-05
**Success Criteria** (what must be TRUE):
  1. For each of C1–C5, a `.flow` script in `tests/spike/` either reproduces the bug with a failing assertion OR a short written dismissal (file + line + reasoning) sits alongside the audit entry.
  2. The verified-2026-04-18 marker appears in the source for every dismissed claim so future audits don't re-raise closed items.
  3. The outcome determines FIX-07's scope: each surviving C1–C5 item becomes a fix task in Phase 12 with its failing test already committed.
  4. No production source under `flow-lang/` is modified during this phase — the spike is pure investigation and test authoring.
**Plans**: 6 plans

Plans:
- [x] 11-01-PLAN.md — C1: musical-context body skip — **CONFIRMED** (→ FIX-07a)
- [x] 11-02-PLAN.md — C2: _returnValue short-circuit — **DISMISSED**
- [x] 11-03-PLAN.md — C3: EnvelopeProcessor div-by-zero — **DISMISSED**
- [x] 11-04-PLAN.md — C4: BufferHelpers fade div-by-zero — **DISMISSED**
- [x] 11-05-PLAN.md — C5: augment/diminish swap — **DISMISSED** (empirical via visualize)
- [x] 11-06-PLAN.md — Aggregate verdicts, write 11-VERIFICATION.md, split FIX-07 in REQUIREMENTS.md

### Phase 12: Stability
**Goal**: Users who upgrade to v1.2 get an interpreter that errors cleanly on `init([])`, caches failed lazy expressions, runs the `test_custom_oscillator` / `test_while_loop` / `test_full_song` suites green, and behaves correctly wherever the audit spike confirmed a real bug (with user-visible semantic changes communicated via release notes and migration aliases).
**Depends on**: Phase 11 (spike outcome determines FIX-07 scope; C5 confirmation determines whether migration comms are required)
**Requirements**: FIX-05, FIX-06, FIX-07, TEST-01, TEST-02, TEST-03
**Success Criteria** (what must be TRUE):
  1. `init([])` raises an error matching `head([])` / `last([])` semantics, and `Thunk.Force()` on a failed expression re-throws the cached exception instead of silently returning null.
  2. `tests/test_custom_oscillator.flow`, `tests/test_while_loop.flow`, and `tests/test_full_song.flow` execute to completion without errors (either via new `range(Int,Int)` + `break`/`continue` + `bpm`/`createStereoTrack`/`renderBars` implementations, or via documented test rewrites that still exercise the intended paths).
  3. Every C1–C5 item the spike confirmed real ships with a numeric (not behavioral) regression test and its fix in a separate commit, preserving bisectability.
  4. If C5 (`augment`/`diminish` swap) was confirmed real, the correct-semantics fix ships with release-notes BREAKING CHANGE entry, `augmentV1`/`diminishV1` transitional aliases, and updated `examples/*.flow` call sites — all in the same release.
  5. The v1.1 soft-failure contract is preserved: validation errors inside musical-context blocks accumulate in `ErrorReporter` and execution continues, and explicit/implicit `return` from procs still works.
**Plans**: 6 plans

Plans:
- [x] 12-01-PLAN.md — xUnit harness scaffold (flow-lang.Tests) + wrap-as-Theory migration of all 55 .flow scripts (completed 2026-04-19; 54/55 green, spike/c1 RED per D-11)
- [x] 12-02-PLAN.md — FIX-05 init([]) raises InvalidOperationException matching head/last semantics + native unit tests (completed 2026-04-19; commit 6e5a960; 3/3 CollectionsTests green)
- [x] 12-03-PLAN.md — FIX-06 Thunk uses Lazy<Value> with ExecutionAndPublication for failure caching + native unit tests (completed 2026-04-19; commit 557923a; 4/4 ThunkTests green; ExpressionEvaluator.Evaluate promoted to virtual for test-double enablement)
- [x] 12-04-PLAN.md — FIX-07a ExecuteMusicalContext returns→breaks + spike/c1 RED→GREEN flip + soft-failure unit tests (completed 2026-04-19; commits 327aa3c + fd9d801; 6/6 ExecuteMusicalContextTests green; spike/c1 GREEN; AUDIT-VERIFIED C1 Confirmed→Fixed)
- [x] 12-05-PLAN.md — if(Bool, Void, Void) wildcard overload + exportWav/writeWav auto-mkdir in shared ExportWavInternal (completed 2026-04-19; commits 9afbe7a + c09cd82; 68/68 suite green; test_full_song RED→GREEN; test_custom_oscillator Tests 1/2/3 RED→GREEN — Test 4 deferred to plan 12-06 via DEFER-01 for missing `range` stdlib)
- [x] 12-06-PLAN.md — REQUIREMENTS.md closure + 12-VERIFICATION.md rollup with FIX-* commit hashes (completed 2026-04-19; commits c94c379 + b5a8702; FIX-05/06/07a Shipped, TEST-01/02 Closed as audit false positives, TEST-03 Shipped/Reframed; DEFER-01 `range` forward-referenced to future phase)


### Phase 13: Nyquist Validation Backfill
**Goal**: v1.1 phases 6–9 each carry a requirements-derived `VALIDATION.md` that would fail if the phase's feature were removed, closing the documentation-lag tech debt carried from v1.1 close.
**Depends on**: Phase 12 (validation targets the post-fix behavior, not the pre-fix behavior)
**Requirements**: TEST-04
**Success Criteria** (what must be TRUE):
  1. `.planning/phases/06-diagnostics-bug-fixes/VALIDATION.md`, `07-developer-experience/VALIDATION.md`, `08-audio-production/VALIDATION.md`, and `09-advanced-features/VALIDATION.md` each satisfy the Nyquist checklist with tests authored against the requirement doc first and the implementation second.
  2. Phase 10 (`10-vocalization`) draft validation is either promoted to `nyquist_compliant: true` or carries an explicit written waiver describing what could not be validated and why.
  3. At least one validation test per phase pins a specific observable value (error message text, buffer byte hash, numeric duration, etc.) rather than asserting "no exception thrown" or "buffer is non-null".
**Plans**: 5 plans

Plans:
- [x] 13-01-PLAN.md — Phase 6 VALIDATION.md (QOL-01, FIX-01, FIX-02 incl. gain-nested, FIX-03) + VerboseFlag + SectionGainBareExpression Facts (completed 2026-04-20; commits ff901fa + 4cf0ccd + 39d53f3; 71/71 suite green; 06-VALIDATION.md at nyquist_compliant: true)
- [x] 13-02-PLAN.md — Phase 7 VALIDATION.md (DX-01..04) + RepLAutoImport Fact + tightened sentinels for test_comments/test_math/test_writewav (completed 2026-04-20; commits fb1a1ae + ed64dec + 9d7575f; 72/72 suite green; 07-VALIDATION.md at nyquist_compliant: true; DX-02 Double format drift documented per Pitfall 5)
- [x] 13-03-PLAN.md — Phase 8 VALIDATION.md (AUDIO-05/06/07) + Mix + SynthesizerFactory Unit Facts + tightened sentinels (completed 2026-04-20; commits ea1d95a + 511085f + b077491; 76/76 suite green; 08-VALIDATION.md at nyquist_compliant: true; AudioCore.Mix IReadOnlyList<Value> signature + SynthesizerFactory outer-namespace + stereo channel-count drift documented under two-pass strict)
- [x] 13-04-PLAN.md — Phase 9 VALIDATION.md (AUDIO-08, QOL-02) + Tutorial Integration Fact + test_tempo_ramp sentinel (completed 2026-04-20; commits ade6fbd + 1a41ada + 1cb508d; 77/77 suite green; 09-VALIDATION.md at nyquist_compliant: true; zero Divergences — AUDIO-08 + QOL-02 both literally testable as drafted; tutorial.flow GREEN under HEAD so no Skip/deferral needed)
- [x] 13-05-PLAN.md — Phase 10 VALIDATION.md promotion (VOC-01 88200 pin + unknown-vowel + VOC-02 round-trip + empty-command Facts) + TEST-04 closure (completed 2026-04-20; commits 331d059 + 81f348c + 21e773d; 81/81 suite green; 10-VALIDATION.md promoted to nyquist_compliant: true; 4 new Facts under flow-lang.Tests/Unit/Phase10/; VOC-02 empty-command assertion shifted from Assert.Equal to Assert.Contains per 2-arg ArgumentException ctor; syllable sample-count Pitfall 8 documented)

### Phase 14: Composer DX Part 1
**Goal**: Composers get three Tier-A building blocks that add no new keyword surface and sit on top of already-shipped infrastructure: bar-level sequence slicing, enharmonic note spellings (`Db`, `Eb`, `H`, …), and a verified end-to-end MIDI-velocity chain driven by `dynamics` / `crescendo` / `decrescendo` / `swell`.
**Depends on**: Phase 12 (stability must land before new surface is added on top of the same files)
**Requirements**: DX-05, DX-06, DX-08
**Success Criteria** (what must be TRUE):
  1. `slice(seq, start, end)` returns a bar-level sub-sequence with start inclusive and end exclusive, clamps out-of-range indices like `take`/`drop`, and the analogous `slice(Array[T], Int, Int)` overload works for arrays.
  2. `Db4`, `Eb4`, `Gb4`, `Ab4`, `Bb4`, `Cb4`, `Fb4` parse as notes inside note-stream context (`| … |`), `H` is accepted as a `B` alias **only inside note streams**, and `Int H = 5;` / `proc H () { … }` / existing identifier uses continue to compile unchanged.
  3. `enharmonic(Note) → Note` returns a pitch-equivalent spelling, round-trippable with existing `NoteType` code.
  4. A `.flow` script that uses a `dynamics` context with `crescendo`/`decrescendo`/`swell` exports a MIDI file whose velocity bytes land in the 1–127 range with the expected gradient; a regression test asserts the velocity byte sequence.
  5. A pre-landing grep of `examples/`, `tests/`, and stdlib `.flow` files for `Db`, `Eb`, `Fb`, `Cb`, `Bb`, `Gb`, `Ab`, `H`, `enharmonic` shows zero ordinary-code identifier collisions (or each collision is renamed before landing).
**Plans**: 4 plans

Plans:
- [x] 14-01-PLAN.md — DX-05 slice(Sequence + Array[T]) atomic with silent two-sided clamp (D-01/D-02)
- [x] 14-02-PLAN.md — DX-06 reduced scope: flat-literal Parse/Format + SimpleLexer dispatch reorder + enharmonic() (H-alias deferred)
- [x] 14-03-PLAN.md — DX-08 MIDI velocity regression via two-pass strict, DryWetMidi byte-array pin
- [x] 14-04-PLAN.md — Phase 14 closure: REQUIREMENTS.md reframe + deferred-items.md + 14-VERIFICATION.md + nyquist promotion (completed 2026-04-20)

### Phase 15: Composer DX Part 2
**Goal**: Composers get humanized euclidean grooves with deterministic output and per-voice reverb-tail control via a new musical-context block — the two widest-surface DX features of the milestone, shipped after smaller-surface work has bedded in.
**Depends on**: Phase 14 (DX-09 reuses the MIDI-velocity infrastructure verified in DX-08; DX-07 is the widest blast radius and ships last)
**Requirements**: DX-07, DX-09
**Success Criteria** (what must be TRUE):
  1. `euclidean(hits, steps, note, swing)` applies swing as a velocity accent on on-beats (no timing-offset field change); `euclidean(hits, steps, note, swing, humanize, seed)` perturbs velocity within `±humanize` using a pinned PRNG seeded by the required `seed` parameter.
  2. Rendering the same `euclidean(…, humanize, seed)` call twice produces byte-identical MIDI and WAV output — the "code is the score" contract holds across runs and across .NET patch versions.
  3. A `reverbTime <seconds> { … }` musical-context block sets per-voice RT60 that propagates through `Audio/DSP/Reverb.cs` via the RT60→feedback mapping, mirrors the `gain` / `pan` / `swing` context pattern, and rejects negative or zero values with a clear error.
  4. Nested `reverbTime` blocks (inside `tempo` / `key` / other contexts) resolve correctly through `ExecutionContext.GetMusicalContext`, with the early-break predicate updated to account for the 8th scoped property.
  5. A pre-landing grep of `examples/`, `tests/`, and stdlib `.flow` files for `reverbTime` shows zero identifier collisions (or each collision is renamed before landing).
**Plans**: 7 plans across 4 waves (planned 2026-04-20)
  - [ ] 15-01-PLAN.md — Wave 0 scaffolding: Phase15 test subtree + MidiReadHelpers promotion (closes DEFER-05) + tests/output/.gitignore + 3 placeholder .flow scripts wired to FlowScriptData
  - [ ] 15-02-PLAN.md — DX-07 grammar + runtime: MusicalContextType.ReverbTime, lexer keyword, Parser case (parse-time negative reject), Interpreter case (silent clamp at 30s, 0.0 dry sentinel), GetMusicalContext 8-clause early-break update, ReverbTimeContextTests (F-01, F-03, F-04, F-05, F-22, F-23 + Parse_Zero_ProducesDry)
  - [ ] 15-03-PLAN.md — DX-07 audio path: ProcessChannel refactor + new Reverb.Apply(rt60) Schroeder overload (feedback cap 0.99) + SongRenderer per-voice reverb with exact-0 short-circuit + test_reverb_time.flow body + ReverbApplyRt60Tests + ReverbTimeRenderTests (F-02, F-06, F-07, F-08)
  - [ ] 15-04-PLAN.md — DX-09 euclidean overloads: 4-arg swing-only + 6-arg swing/humanize/seed via RegisterContextDependentFunctions (base velocity = MusicalContext.Velocity ?? 0.63); std.flow declarations; steps>1024 guard; EuclideanSwingTests + EuclideanHumanizeTests (F-09..F-18, F-21 + SameSeed_ProducesIdenticalVelocities)
  - [ ] 15-05-PLAN.md — DX-09 byte-identical MIDI + WAV regression via two-pass strict empirical byte capture (F-19, F-20)
  - [ ] 15-06-PLAN.md — DX-09 end-to-end .flow scripts replace Plan 01 placeholders (test_euclidean_swing.flow + test_euclidean_humanize.flow)
  - [ ] 15-07-PLAN.md — Phase closure: ROADMAP criterion #3 reframe per D-02 + REQUIREMENTS Shipped markers + F-24 collision grep transcript + 15-VERIFICATION.md + 15-VALIDATION.md promotion to nyquist_compliant: true + 15-SUMMARY.md + STATE advance

### Phase 16: Tutorial Refresh
**Goal**: A new user running `examples/tutorial.flow` against v1.2 can experience every v1.1 + v1.2 composer-visible feature end-to-end, producing audible WAV and MIDI output, so features added since v1.0 stop atrophying unused.
**Depends on**: Phase 15 (tutorial documents shipped reality; writing it before features land means rewriting it when features shift)
**Requirements**: QOL-03
**Success Criteria** (what must be TRUE):
  1. `examples/tutorial.flow` demonstrates `//` line comments, `writeWav`, `mix`, per-section `gain`, the `strings` / `organ` / `bell` synth presets, `tempoRamp`, `sing` / `tts`, `slice`, enharmonic helpers, `reverbTime`, MIDI velocity export via dynamics, and `euclidean` swing/humanize — at least one runnable snippet per feature.
  2. Running `dotnet run --project flow-interpreter examples/tutorial.flow` produces a non-empty WAV file, a non-empty MIDI file, and exits with status 0.
  3. Each tutorial snippet is traceable to a requirement — every v1.1 Validated requirement and every v1.2 Tier A feature is referenced in at least one tutorial comment.
  4. If C5 shipped as a breaking change in Phase 12, the tutorial's `augment`/`diminish` usages reflect the new (correct) semantics and link back to the migration notes.
**Plans**: TBD

### Phase 17: Flow Language Server
**Goal**: Flow users editing `.flow` files in VSCode get syntax highlighting, live diagnostics from the interpreter's parser/type-checker, and intelligent completions/hover suggestions for built-in functions, musical types, chord symbols, and imported stdlib modules — delivered as an LSP server (reusing flow-lang) and a VSCode extension that ships the server binary.
**Depends on**: Phase 12 (stable interpreter required; parser/evaluator surface must not churn while the LSP consumes it)
**Requirements**: D-01..D-15 (locked decisions in 17-CONTEXT.md substitute for REQ-IDs per RESEARCH §"Phase Requirements")
**Success Criteria** (what must be TRUE):
  1. `flow-lsp/` project builds under net10.0, references only `flow-lang` (no audio deps), boots OmniSharp over stdio and accepts initialize+shutdown (Wave 0 gate — D-01, D-02).
  2. Every `ErrorReporter` error surfaces as an LSP Diagnostic with correct severity + 0-based range; empty diagnostic arrays still publish to clear stale markers (D-06).
  3. Semantic tokens emit valid 5-tuple delta-encoded LSP output mapping every SimpleLexer TokenType that `FlowSyntaxHighlighter` colored; standard VSCode scopes only, no invented `*.flow` sub-scopes (D-04, D-05).
  4. Completion delivers built-ins + stdlib procs + user symbols + keywords + 5 snippet templates in default context; `use "@"` context returns only the 6 stdlib module paths; `| ... |` note-stream context returns roman numerals (in key block) or note letters/durations/rests (otherwise); never proc names inside streams (D-07, D-11).
  5. Hover shows signature + `BuiltInDocs` summary for built-ins; user symbol kind for locals; stdlib-proc signature for imports (D-08, D-12).
  6. Go-to-definition jumps to declaration for user procs/vars and to stdlib `.flow` file for imports; built-ins return null (D-09).
  7. Signature help reports the correct active parameter by comma-count for built-ins and user procs (D-10).
  8. Per-platform self-contained VSIXs (linux-x64, win32-x64, darwin-x64, darwin-arm64) build via CI matrix; each VSIX contains the platform-native `flow-lsp` binary AND the 6 stdlib `.flow` files (Pitfall 6 gate) (D-14).
  9. Dual-marketplace publish (VSCode Marketplace + OpenVSX) via tag push; OpenVSX namespace claimed before first publish (Pitfall 8) (D-15).
  10. Non-VSCode editor users have `docs/editor-setup/nvim-lspconfig.lua` + `helix-languages.toml` starter snippets + README with build-from-source instructions (D-13 second clause).
**Plans**: 8 plans

Plans:
- [x] 17-01-PLAN.md — flow-lsp scaffold + ParseSession + BuiltInDocs + OmniSharp boot smoke (Wave 1) — completed 2026-04-20 (commits 8aeba9e, fadd371)
- [ ] 17-02-PLAN.md — VSCode extension scaffold + TextMate grammar + snippets + grammar fixtures (Wave 1)
- [x] 17-03-PLAN.md — DocumentManager + TextDocumentSyncHandler + DiagnosticsPublisher + LspMappings (Wave 2) — completed 2026-04-20 (commits 86a4364, 04e8cda)
- [x] 17-04-PLAN.md — SemanticTokensHandler: SimpleLexer to LSP semantic tokens (Wave 3) — completed 2026-04-20 (commit 5d010d7)
- [x] 17-05-PLAN.md — Symbol indices + BuiltInDocs population + CompletionHandler (Wave 4) — completed 2026-04-20 (commits 8bc29a8, 34147cf)
- [x] 17-06-PLAN.md — HoverHandler + SignatureHelpHandler + DefinitionHandler + NoteStreamContext (Wave 5) — completed 2026-04-20 (commits d6dcc89, c8a4678)
- [x] 17-07-PLAN.md — LSP smoke script + per-platform CI matrix + TM grammar snapshots (Wave 6) — completed 2026-04-21 (commits 53cea82, 2f90408, a831035)
- [x] 17-08-PLAN.md — Non-VSCode editor docs + Marketplace/OpenVSX setup + manual smoke + phase closure (Wave 7) — completed 2026-04-20 (commits ec8e18f, 888f432, 9d33c90, 7026982; Task 3 deferred to HUMAN-UAT per user direction — 3 pending tests in 17-HUMAN-UAT.md, rows 4-5 deferred to first release tag)

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Language Foundations | v1.0 | 3/3 | Complete | 2026-04-01 |
| 2. Audio Pipeline | v1.0 | 3/3 | Complete | 2026-04-02 |
| 3. Synthesis & MIDI Export | v1.0 | 2/2 | Complete | 2026-04-02 |
| 4. Composition Tools | v1.0 | 2/2 | Complete | 2026-04-02 |
| 5. Live Coding | v1.0 | 2/2 | Complete | 2026-04-03 |
| 6. Diagnostics & Bug Fixes | v1.1 | 2/2 | Complete | 2026-04-04 |
| 7. Developer Experience | v1.1 | 2/2 | Complete | 2026-04-04 |
| 8. Audio Production | v1.1 | 2/2 | Complete | 2026-04-04 |
| 9. Advanced Features | v1.1 | 2/2 | Complete | 2026-04-04 |
| 10. Vocalization | v1.1 | 2/2 | Complete | 2026-04-04 |
| 11. Audit Spike | v1.2 | 6/6 | Complete | 2026-04-19 |
| 12. Stability | v1.2 | 6/6 | Complete    | 2026-04-19 |
| 13. Nyquist Validation Backfill | v1.2 | 5/5 | Complete    | 2026-04-20 |
| 14. Composer DX Part 1 | v1.2 | 4/4 | Complete    | 2026-04-20 |
| 15. Composer DX Part 2 | v1.2 | 0/? | Not started | - |
| 16. Tutorial Refresh | v1.2 | 0/? | Not started | - |
| 17. Flow Language Server | v1.2 | 8/8 | Complete (HUMAN-UAT deferred) | 2026-04-20 |
