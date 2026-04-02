---
phase: 03
slug: synthesis-midi-export
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-02
---

# Phase 03 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | .flow test scripts (no unit test framework — tests are .flow scripts executed directly) |
| **Config file** | none — tests are standalone .flow scripts in tests/ |
| **Quick run command** | `dotnet run --project flow-interpreter tests/test_custom_oscillator.flow` |
| **Full suite command** | `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` |
| **Estimated runtime** | ~60 seconds (full suite) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build --nologo --verbosity quiet && dotnet run --project flow-interpreter tests/test_custom_oscillator.flow`
- **After every plan wave:** Run full suite
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 1 | SYNTH-01 | integration | `dotnet run --project flow-interpreter tests/test_custom_oscillator.flow` | ❌ W0 | pending |
| 03-01-02 | 01 | 1 | SYNTH-02 | integration | `dotnet run --project flow-interpreter tests/test_custom_oscillator.flow` | ❌ W0 | pending |
| 03-02-01 | 02 | 1 | MIDI-01 | integration | `dotnet run --project flow-interpreter tests/test_midi_export.flow` | ❌ W0 | pending |
| 03-02-02 | 02 | 1 | MIDI-02 | integration | `dotnet run --project flow-interpreter tests/test_midi_export.flow` | ❌ W0 | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

- [ ] `tests/test_custom_oscillator.flow` — test custom oscillator definition and song rendering
- [ ] `tests/test_midi_export.flow` — test MIDI file export with tempo/timesig/key/velocity

*Created as part of plan execution tasks.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Custom oscillator audio quality | SYNTH-01 | Must listen to verify waveform sounds correct | Define wavetable, render song, play or export WAV, listen |
| MIDI file opens in DAW | MIDI-01 | Requires external DAW/MIDI player | Export .mid, open in MuseScore/LMMS/etc., verify tracks play |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
