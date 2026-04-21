---
phase: 14
slug: composer-dx-part-1
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-20
completed: 2026-04-20
---

# Phase 14 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Detailed test map lives in `14-RESEARCH.md` §Validation Architecture — this doc pins
> the sampling contract and tracks Wave 0 gap closure.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase14"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | Quick: ~5s · Full: ~30s (81 pre-Phase-14 + ~56 new Phase 14 Facts incl. SliceTests / NoteTypeTests / LexerTests / EnharmonicTests / DynamicsMidiVelocityTests) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase14"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work` in plan 14-04:** Full suite green (100% pass, including pre-Phase-14 regression baseline)
- **Max feedback latency:** ~5 seconds

---

## Per-Task Verification Map

*Populated during planning from `14-RESEARCH.md` §Phase Requirements → Test Map. Status column filled at phase-close (plan 14-04) based on per-plan SUMMARY outcomes.*

| Req ID | Plan | Behavior | Test Type | Automated Command | File Exists | Status |
|--------|------|----------|-----------|-------------------|-------------|--------|
| DX-05 | 14-01-T1 | `slice([1..5], 0, 3)` = `[1,2,3]` | unit | `dotnet test --filter "SliceTests.Array_NormalRange"` | ✅ | ✅ green |
| DX-05 | 14-01-T1 | `slice(seq, -5, 2)` clamps start to 0 | unit | `dotnet test --filter "SliceTests.Array_NegativeStartClamps"` | ✅ | ✅ green |
| DX-05 | 14-01-T1 | `slice(arr, 3, 2)` returns empty (start>=end) | unit | `dotnet test --filter "SliceTests.Array_InvertedRangeEmpty"` | ✅ | ✅ green |
| DX-05 | 14-01-T1 | `slice(Sequence, 1, 3).Bars.Count == 2` | unit | `dotnet test --filter "SliceTests.Sequence_ReturnsCorrectBarCount"` | ✅ | ✅ green |
| DX-05 | 14-01-T1 | End-to-end via `tests/test_slice.flow` | integration | FlowScriptData Theory row | ✅ | ✅ green |
| DX-06 Flat | 14-02-T1 | `Parse("Db4")` → `('D', 4, -1)` | unit | `dotnet test --filter "NoteTypeTests.Parse_FlatLetter_Db"` | ✅ | ✅ green |
| DX-06 Flat | 14-02-T1 | `Parse("Bb-+bbb")` → `('B', 4, -4)` | unit | `dotnet test --filter "NoteTypeTests.Parse_MixedAlteration_BbMinusPlusBBB"` | ✅ | ✅ green |
| DX-06 Flat | 14-02-T1 | Round-trip `Parse(Format(x)) == x` for alt ∈ [-5,+5] | unit | `dotnet test --filter "NoteTypeTests.RoundTrip_AllAlterations"` | ✅ | ✅ green |
| DX-06 Flat | 14-02-T1 | `Parse("Eb0")` throws (post-alt MIDI below E0) | unit | `dotnet test --filter "NoteTypeTests.Parse_Eb0_BelowRange_Throws"` | ✅ | ✅ green |
| DX-06 Flat | 14-02-T1 | `Bb7` lexer-tokenizes as NoteLiteral under extended Parse surface (chord-first dispatch preserves chord-regression gates for `Dm`/`Cmaj7`/`Am7`/`Bdim`/`Csmaj`/`Bfm`) | unit | `dotnet test --filter "LexerTests"` | ✅ | ✅ green |
| DX-06 Flat | 14-02-T1 | `tests/test_flat_literals.flow` renders note stream | integration | FlowScriptData Theory row | ✅ | ✅ green |
| DX-06 Enh | 14-02-T2 | `enharmonic(Db4)` no-key → `C#4` | unit | `dotnet test --filter "EnharmonicTests.NoKey_FlatToSharp"` | ✅ | ✅ green |
| DX-06 Enh | 14-02-T2 | `enharmonic(C4)` → `C4` (natural unchanged) | unit | `dotnet test --filter "EnharmonicTests.NoKey_NaturalUnchanged"` | ✅ | ✅ green |
| DX-06 Enh | 14-02-T2 | In-key Dbmajor: `C#4` → `Db4` | unit | `dotnet test --filter "EnharmonicTests.InKey_Dbmajor_CsharpRespells"` | ✅ | ✅ green |
| DX-06 Enh | 14-02-T2 | `tests/test_enharmonic.flow` with `key Dbmajor { }` | integration | FlowScriptData Theory row | ✅ | ✅ green |
| DX-08 | 14-03-T1 | MIDI note-on velocity sequence matches `[31, 47, 63, 79, 95]` for `crescendo(0.25, 0.75)` over 5 notes | integration | `dotnet test --filter "DynamicsMidiVelocityTests.Crescendo_EmitsExpectedVelocityGradient"` | ✅ | ✅ green |
| DX-08 | 14-03-T1 | `tests/test_dynamics_midi_velocity.flow` Theory row | integration | FlowScriptData Theory row | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

All test files were Wave 0 gaps at plan-start and are now all ✅ green:

- [x] `flow-lang.Tests/Unit/Phase14/SliceTests.cs` — DX-05 (plan 14-01)
- [x] `flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs` — DX-06 Flat Parse + Format + RoundTrip + Range (plan 14-02)
- [x] `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs` — DX-06 Enharmonic (plan 14-02)
- [x] `flow-lang.Tests/Unit/Phase14/LexerTests.cs` — chord-vs-note lexer-ordering regression (plan 14-02)
- [x] `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` — DX-08 MIDI byte assertion via DryWetMidi `MidiFile.Read` (plan 14-03)
- [x] `tests/test_slice.flow` — integration regression; Theory row via FlowScriptData (plan 14-01)
- [x] `tests/test_flat_literals.flow` — integration regression for extended Parse surface (plan 14-02)
- [x] `tests/test_enharmonic.flow` — integration regression across no-key and in-key paths (plan 14-02)
- [x] `tests/test_dynamics_midi_velocity.flow` — source for DX-08 MIDI Fact (plan 14-03)

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

The pre-landing collision grep (ROADMAP criterion 5) is a one-shot audit performed during plan 14-02 planning — transcript lives in 14-02-PLAN.md §Pre-landing Collision Grep and is re-surfaced in `14-VERIFICATION.md`. Not a test-time check per CONTEXT D-21.

---

## Observable-Value Pin Discipline (Phase 13 D-11 compliance)

Per Phase 13 D-11: pins must be **error message text** OR **numeric counts / sample values** OR **exact byte sequences**. Buffer byte hashes are forbidden.

| Feature | Pin Type | Pin Value |
|---------|----------|-----------|
| `slice` array | numeric count | `result.Count == expected` |
| `slice` sequence | numeric bar count | `seq.Bars.Count == expected` |
| `NoteType.Parse` | triple equality | `(letter, octave, alteration) == expected` |
| `NoteType.Format` | exact string | run-based `+N`/`-N` emission, e.g. `Format(B,4,-4) == "B4----"` |
| Post-alt range error | error text | `"Note Eb0 is out of valid range (E0 to E10)"` |
| `enharmonic` | triple equality via re-Parse | `Parse(enharmonic("Db4")) == ('C', 4, 1)` |
| MIDI velocity | exact byte sequence | `[31, 47, 63, 79, 95]` for `crescendo(0.25, 0.75)` over 5 notes |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (9 new test files, all ✅ green at phase close)
- [x] No watch-mode flags
- [x] Feedback latency < 10s for quick-run
- [x] `nyquist_compliant: true` set in frontmatter at phase close (2026-04-20, plan 14-04)

**Approval:** Signed off 2026-04-20 — Phase 14 closed.

---

## Final sign-off

Phase 14 (composer DX — part 1) is **validated and closed** as of 2026-04-20.

### What was delivered
- DX-05: `slice(Sequence, Int, Int)` and `slice(Array[T], Int, Int)` with silent two-sided clamping (plan 14-01, commit `4528407`).
- DX-06 (reduced): flat-literal surface with extended alteration encoding (`Db4`, `Bb-+bbb`, etc.), canonical run-based `Format`, post-alteration MIDI range check, SimpleLexer chord-first dispatch reorder (plan 14-02, commit `d2edc90`); `enharmonic(Note) → Note` key-context-aware respelling (plan 14-02, commit `2490c9c`).
- DX-08: MIDI velocity regression via two-pass strict authorship — `DynamicsMidiVelocityTests.Crescendo_EmitsExpectedVelocityGradient` asserts `[31, 47, 63, 79, 95]` (plan 14-03, commit `152e593`, Outcome A GREEN on first run).

### Requirements validated
- `DX-05` (slice) — **validated**
- `DX-06` (flat literals + enharmonic; H-alias clause deferred) — **validated** (reframed per CONTEXT D-19)
- `DX-08` (MIDI velocity end-to-end) — **validated**

### Intentional deferrals
- `DEFER-02` — German `H` alias awaits a notation-locale feature.
- `DEFER-03` — Pragma / feature-flag language construct (candidate `enable`).
- `DEFER-04` — Multi-letter enharmonic-edge respelling.
- `DEFER-05` — Shared MIDI-read helper promotion.
- `DEFER-06` — `slice` negative-from-end indexing.

All deferrals are captured in `deferred-items.md` in this phase directory with acceptance criteria so they can be resumed cleanly in a later phase.

### Closure artefacts
- [`deferred-items.md`](./deferred-items.md) — DEFER-02 through DEFER-06 with target phases and acceptance criteria.
- [`14-VERIFICATION.md`](./14-VERIFICATION.md) — per-requirement evidence matrix, collision grep transcript, commit hash manifest.
- `14-0N-SUMMARY.md` (N = 1, 2, 3) — per-plan outcome records.

### Research log

- 2026-04-18 — Investigated whether `H` should be introduced as a `B` alias in
  the lexer. Concluded: not without a locale switch (see `deferred-items.md`,
  DEFER-02). This closed the only open question that remained from the plan-04
  scoping round.
- 2026-04-18 — Reviewed diagnostic catalogue for locale assumptions. Captured
  the English-only message convention as DEFER-03.
