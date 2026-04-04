---
phase: 10
slug: vocalization
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-03
---

# Phase 10 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Flow test scripts (.flow files executed directly) |
| **Config file** | none — tests are standalone .flow scripts |
| **Quick run command** | `dotnet run --project flow-interpreter tests/test_vocalization.flow` |
| **Full suite command** | `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` |
| **Estimated runtime** | ~5 seconds (single test), ~120 seconds (full suite) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet run --project flow-interpreter tests/test_vocalization.flow`
- **After every plan wave:** Run `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 10-01-01 | 01 | 1 | Formant engine | unit | `dotnet run --project flow-interpreter tests/test_vocalization.flow` | ❌ W0 | ⬜ pending |
| 10-01-02 | 01 | 1 | sing() API | integration | `dotnet run --project flow-interpreter tests/test_vocalization.flow` | ❌ W0 | ⬜ pending |
| 10-02-01 | 02 | 2 | TTS hook | integration | `dotnet run --project flow-interpreter tests/test_vocalization.flow` | ❌ W0 | ⬜ pending |
| 10-02-02 | 02 | 2 | Consonants | unit | `dotnet run --project flow-interpreter tests/test_vocalization.flow` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/test_vocalization.flow` — stubs for formant synthesis, sing(), tts(), consonant syllables
- [ ] Build verification — `dotnet build` passes with new Vocalization/ files

*Wave 0 creates test infrastructure; actual tests filled during execution.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Formant audio quality | Vowel recognizability | Subjective — requires listening | Play output of `sing("ah", C4, 2.0)`, verify recognizable "ah" vowel |
| TTS external command | espeak-ng integration | Requires espeak-ng installed | Run `tts("hello world")`, verify WAV output returned as buffer |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 5s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
