# Flow Language Milestones

Historical record of shipped versions. Full details archived in `.planning/milestones/`.

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
