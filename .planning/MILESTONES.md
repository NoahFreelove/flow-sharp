# Flow Language Milestones

Historical record of shipped versions. Full details archived in `.planning/milestones/`.

---

## v1.2 Stability & Composer DX — Shipped 2026-04-26

**Goal:** Verify then fix the critical bugs surfaced by the 2026-04-18 codebase audit, unblock the failing test suite, ship the Tier A + Tier B composer DX bundles, refresh the tutorial so v1.1 + v1.2 capabilities are discoverable, and ship a Flow Language Server with VSCode extension.

**Delivered:** A stable interpreter with cleanly-failing edge cases (init/Thunk/musical-context body), full Tier A + Tier B composer DX (slice, flat literals + enharmonic, MIDI velocity preservation end-to-end, reverbTime context block, euclidean swing/humanize with byte-identical output), a tutorial + showcase pair that exercises every v1.1 + v1.2 feature with audible WAV + MIDI output, a Flow Language Server (LSP) plus VSCode extension with syntax highlighting, live diagnostics, and intelligent completion/hover/signature-help/go-to-def, and retroactive Nyquist validation closing the v1.1 documentation-lag debt.

**Stats:**
- Phases: 7 (Phase 11 – Phase 17)
- Plans: 41 (all complete)
- Requirements: 18 total — all Complete (5 SPIKE verdicts + 13 fix/test/DX/QOL shipments)
- Git range: post-`v1.1` tag → `v1.2` tag (~3 months, 2026-01-24 → 2026-04-26)
- Source files at close: ~83K LOC C# + 312 .flow files

**Key accomplishments:**
1. Audit Spike isolated as Phase 11 — 1 Confirmed (C1 → FIX-07a) + 4 Dismissed (C2/C3/C4/C5) before any production code changed
2. Stability fixes — `init([])` errors cleanly (FIX-05), `Thunk` caches failures via `Lazy<Value>` (FIX-06), `ExecuteMusicalContext` body skip resolved (FIX-07a), `if(Bool, Void, Void)` wildcard overload + auto-mkdir for writeWav (TEST-03)
3. Nyquist Validation Backfill — Phases 6–10 each gained or promoted a `VALIDATION.md` at `nyquist_compliant: true` with observable-value pins (TEST-04)
4. Composer DX Tier A — `slice` (Sequence + Array[T]), extended flat-literal Parse + `enharmonic()` + lexer dispatch reorder, MIDI velocity byte-array regression via DryWetMidi (DX-05/06/08)
5. Composer DX Tier B — `reverbTime <s> { ... }` musical-context block with per-voice RT60 (Schroeder closed-form mapping, dry-render sentinel), `euclidean(hits, steps, note, swing)` + 6-arg humanize/seed overload with byte-identical output across runs (DX-07/09)
6. Tutorial + showcase refresh — `examples/tutorial.flow` and `examples/showcase.flow` exercise every v1.1 + v1.2 feature end-to-end with byte-identical determinism contract holding across two consecutive runs (QOL-03)
7. Flow Language Server + VSCode extension — `flow-lsp` (OmniSharp.Extensions.LanguageServer over stdio, references flow-lang only), per-platform self-contained VSIX (linux-x64, win-x64, osx-x64, osx-arm64) with bundled stdlib, semantic tokens + diagnostics + completion + hover + signature help + go-to-definition + 5 snippet templates + roman-numeral context inside note streams + `BuiltInDocs` lookup table populated to 104 entries

**Patterns established:**
- Two-pass strict authorship — Pass 1 from REQUIREMENTS, Pass 2 reality-check; format/signature drift surfaced before commit
- Charitable interpretation as load-bearing — 4 criterion-moot/reframe events (TEST-03 P12, DX-06 P14, criterion #3 P15, criterion #4 P16)
- HUMAN-UAT for non-blocking checkpoints — Phase 17 `17-HUMAN-UAT.md` (status: partial) instead of fake-passing manual verification
- "Closed (audit false positive)" first-class traceability marker — TEST-01/TEST-02 retained as audit-trail entries
- Determinism contract — synth white-noise + TPDF dither RNGs reseeded at renderSong/writeWav boundaries; byte-identical WAV + MIDI for `tutorial.flow` and `showcase.flow` two consecutive runs

**Known deferred items at close:** 4 (see STATE.md Deferred Items)
- Debug session: `function-overload-resolution-failures` (awaiting human verify)
- Quick task: pure-Flow test library (`260420-0c0...`)
- Phase 17: 3 pending HUMAN-UAT scenarios (manual-smoke rows 1-3)
- Phase 04: VERIFICATION.md gaps_found

**Forward-deferred items (DEFER-01..06):**
- DEFER-01: `range(Int, Int) → Array[Int]` stdlib registration
- DEFER-02/03: `H` note-stream-only `B` alias via pragma system + candidate `enable` keyword
- DEFER-04: Multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#)
- DEFER-05: Slice negative-from-end indexing
- DEFER-06: Gaussian humanize distribution

**Archives:**
- `.planning/milestones/v1.2-ROADMAP.md`
- `.planning/milestones/v1.2-REQUIREMENTS.md`

---

## v1.1 Polish & Foundations — Shipped 2026-04-18

**Goal:** Fix critical bugs that break user scripts, improve developer experience with missing language features, then expand music production capabilities.

**Delivered:** A production-grade interpreter with honest error reporting, a usable math stdlib, comment syntax, better audio composition primitives (mix, per-section gain, three new synth timbres, tempo ramps), and a formant-based vocal synthesis pipeline with external TTS integration.

**Stats:**
- Phases: 5 (Phase 6 – Phase 10)
- Plans: 10 (all complete)
- Requirements: 16 total — 15 Complete, 1 Invalid (FIX-04 premise did not hold)
- Git range: `9f4d1cb` (v1.1 execution commit) → `v1.1` tag
- Files changed during milestone: 123
- LOC delta: +12,768 / −1,625

**Key accomplishments:**
1. `--verbose` flag with TextWriter-threaded diagnostics (module loads, failed overload resolutions) on stderr (QOL-01)
2. Overload resolution fixed for music-type widening (`transpose(seq, 2)`, `vary(seq, 0.5)` now work) (FIX-01)
3. Bare expressions captured inside sections — including deeply nested musical-context blocks after audit-driven fix (FIX-02 × AUDIO-06)
4. Error reporter no longer masks function-not-found failures; exit code 1 on errors (FIX-03)
5. `//` line comments + 17-function math stdlib (sin/cos/tan/abs/sqrt/min/max/floor/ceil/round/pow/log + pi/tau) (DX-01, DX-02)
6. `writeWav(path, buf)` path-first convention with `exportWav` alias + REPL auto-imports of @std, @audio, @collections (DX-03, DX-04)
7. `mix(Buffer, Buffer)` with mono-to-stereo promotion + per-section `gain` musical context block (AUDIO-05, AUDIO-06)
8. Three new synthesizer presets — strings (detuned saws), organ (Hammond additive), bell (Risset inharmonic partials) (AUDIO-07)
9. `tempoRamp(sequence, startBPM, endBPM) → Buffer` with bar-midpoint interpolation (AUDIO-08)
10. Formant-based vocal synthesis (`sing(phoneme, note, dur)`) + external TTS hook (`tts(text)`, `setTtsCommand(cmd)`) producing AudioBuffers that compose with mix/writeWav/play (VOC-01, VOC-02)

**Known gaps at close:**
- FIX-04 reclassified as Invalid (architectural precondition does not exist)
- `examples/tutorial.flow` does not yet demonstrate v1.1 features (deferred, tracked in next milestone planning)
- Phases 6–9 lacked individual `VERIFICATION.md` files; verified retroactively via 3-source audit (SUMMARY frontmatter + code inspection + live E2E execution)
- Nyquist validation incomplete: 4/5 phases missing `VALIDATION.md`, phase 10 has draft with `nyquist_compliant: false`

**Archives:**
- `.planning/milestones/v1.1-ROADMAP.md`
- `.planning/milestones/v1.1-REQUIREMENTS.md`
- `.planning/milestones/v1.1-MILESTONE-AUDIT.md`

---

## v1.0 MVP — Shipped 2026-04-03

The initial MVP. Delivered: lexer/parser/interpreter pipeline, static type system with music-specific types, flow operator, musical context blocks, note stream syntax, chord literals + roman numerals, section/song structure, pattern transforms, piano/brass/sax/drums synthesis, DSP effects, WAV export, PulseAudio playback, MIDI import, REPL with watch mode, module imports, standard library.

**Phases:** 1–5 (Language Foundations, Audio Pipeline, Synthesis & MIDI Export, Composition Tools, Live Coding)

*(v1.0 was shipped before milestone-completion tooling existed; details are preserved in `.planning/ROADMAP.md` under the v1.0 MVP collapsed section.)*
