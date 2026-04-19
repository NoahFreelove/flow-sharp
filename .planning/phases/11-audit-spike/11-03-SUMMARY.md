---
phase: 11-audit-spike
plan: 03
subsystem: audio
tags: [audit-spike, envelope, adsr, ar, dsp, dismissal]

requires:
  - phase: planning
    provides: "CODEBASE-AUDIT-2026-04-18.md §1 C3 claim, ARCHITECTURE.md + PITFALLS.md conflicting interpretations"
provides:
  - "Empirical confirmation that EnvelopeProcessor div-by-zero (C3) is a false positive"
  - "Greppable `AUDIT-VERIFIED 2026-04-18: C3` marker at EnvelopeProcessor.cs:105"
  - "tests/spike/c3-envelope-short-segments.flow GREEN regression guard"
affects: [12-stability-fixes, 11-VERIFICATION.md]

tech-stack:
  added: []
  patterns:
    - "AUDIT-VERIFIED marker pattern (D-02): single-line grep-anchored comment at claim site"
    - "Spike-test convention (D-05/D-08): tests/spike/cN-<slug>.flow exercising the exact claim path"

key-files:
  created:
    - tests/spike/c3-envelope-short-segments.flow
  modified:
    - flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs

key-decisions:
  - "C3 dismissed by empirical test (Branch 1, D-05): 6 probes at SR=44100 and SR=100 with sub-frame durations all survive cleanly"
  - "Marker placed above AR-curve attack loop (single marker annotates all 5 claimed sites per plan constraint)"

patterns-established:
  - "Loop-guard dismissal pattern: `for (int i = 0; i < N; i++)` with N==0 skips the body entirely, so divisions inside the body are unreachable when the denominator is zero. Applies symmetrically to the 4 other loops at EnvelopeProcessor.cs:112, 118, 154, and 167."

requirements-completed: [SPIKE-03]

duration: 2min
completed: 2026-04-19
---

# Phase 11 Plan 03: SPIKE-03 — C3 EnvelopeProcessor div-by-zero — Dismissed

**Empirical `.flow` spike confirms C3 is a false positive: the `for (int i = 0; i < attackFrames; i++)` guards at EnvelopeProcessor.cs lines 106, 112, 118 (AR) and 148, 154, 167 (ADSR) prevent the divisions at lines 108, 120, 150, 156, 169 from ever executing with a zero denominator.**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-04-19T03:43:05Z
- **Completed:** 2026-04-19T03:44:51Z
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments

- Branch 1 (empirical) spike authored: `tests/spike/c3-envelope-short-segments.flow` with 6 probes.
- All 5 audit-flagged division sites (lines 108, 120, 150, 156, 169) exercised via `createAR` + `createADSR` + `applyEnvelope` at SR=44100 with sub-frame attack/decay/release values, plus a low-SR (100 Hz) variant that forces frame counts to 0.
- Single greppable marker added: `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs:105` — `// AUDIT-VERIFIED 2026-04-18: C3 — Dismissed: loop body only runs when frames > 0; see tests/spike/c3-envelope-short-segments.flow`.
- Diff is exactly +1 insertion, 0 deletions; `dotnet build` passes (0 errors, 3 pre-existing unrelated nullability warnings).

## Branch Chosen

**Branch 1 — empirical test.** All three APIs (`createAR`, `createADSR`, `applyEnvelope`) are registered as Flow built-ins with `.flow`-callable signatures, and `createBuffer` lets user code supply an arbitrary sample rate. The path is fully reachable from user code, so the D-05 fallback to reasoning-only dismissal was not needed.

## Reachability Census

Grep evidence (`createAR|createADSR|applyEnvelope|CreateAR|CreateADSR|ApplyEnvelope`):

| Location | Role |
|----------|------|
| `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs:14` | `CreateAR(IReadOnlyList<Value>)` — the C# implementation |
| `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs:29` | `CreateADSR(IReadOnlyList<Value>)` — the C# implementation |
| `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs:46` | `ApplyEnvelope(IReadOnlyList<Value>)` — the C# implementation |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs:529-542` | `registry.Register("createAR", ...)`, `registry.Register("createADSR", ...)`, `registry.Register("applyEnvelope", ...)` — exposure to `.flow` user code |
| `flow-lang/audio.flow:254-260` | `internal proc createAR(...)`, `createADSR(...)`, `applyEnvelope(...)` stdlib declarations |
| `flow-lang/audio.flow:298-305` | `proc ar(Double, Double)` / `proc adsr(Double, Double, Double, Double)` convenience wrappers |
| `tests/*.flow` | Zero invocations — audit §4 is correct that ADSR/AR envelopes lacked tests before this spike |

Signatures (Flow-callable):

- `createAR(Double attackSec, Double releaseSec, Int sampleRate) -> Envelope`
- `createADSR(Double attack, Double decay, Double sustain, Double release, Int sampleRate) -> Envelope`
- `applyEnvelope(Buffer buffer, Envelope envelope) -> Void` (in-place)

Conclusion: reachable, testable, user-controlled sample rate → Branch 1.

## Verdict

**Dismissed.** Architecture researcher was correct.

### Evidence

Spike command:

```
dotnet run --project flow-interpreter tests/spike/c3-envelope-short-segments.flow
```

stdout (trimmed of build banner/warnings):

```
Flow Language Interpreter v0.1

c3-probe1-survived
c3-probe2-survived
c3-probe3-survived
c3-probe4-survived
c3-probe5-survived
c3-probe6-survived
c3-all-probes-complete
```

Exit code: `0`. stderr: empty (no `DivideByZeroException`, no stack trace, no NaN warnings).

### Probe coverage matrix

| Probe | Target lines | SR | Attack/Decay/Release → truncated frames | Outcome |
|-------|--------------|----|-----------------------------------------|---------|
| 1 | 108 | 44100 | 0.00001s → 0 / (n/a) / 0.05s → 2205 | survived |
| 2 | 108 + 120 | 44100 | 0.00001s → 0 / (n/a) / 0.00001s → 0 | survived |
| 3 | 150 + 156 + 169 | 44100 | 0.00001s → 0 / 0.00001s → 0 (sustain=0.8) / 0.00001s → 0 | survived |
| 4 | 108 + 120 | 100 | 0.001s → 0 / (n/a) / 0.001s → 0 | survived |
| 5 | 150 + 156 + 169 | 100 | 0.001s → 0 / 0.001s → 0 (sustain=0.5) / 0.001s → 0 | survived |
| 6 | all (control, non-zero frames) | 44100 | 0.01s → 441 / (n/a) / 0.02s → 882 | survived |

Probes 1-5 force at least one, and often all, envelope segments to truncate to `(int)(seconds * sampleRate) == 0`. In every case the `for (int i = 0; i < 0; i++)` loop body is skipped, the `curve` array keeps its default `float` (0.0f) fill, and `applyEnvelope` proceeds to multiply buffer samples by zero. No division ever executes with a zero denominator.

This matches the `ARCHITECTURE.md §"Audit Re-Verification"` row C3 conclusion. The `PITFALLS.md §5` concern about `Math.Max(1, frames)` being the wrong fix remains valid as general guidance (no change needed here), but is moot for C3 because no fix is needed in the first place.

### Source-level reasoning (supporting the empirical result)

```csharp
// EnvelopeProcessor.cs:98-108 (AR)
attackFrames = Math.Min(attackFrames, totalFrames);           // attackFrames >= 0
...
for (int i = 0; i < attackFrames; i++, frame++)               // guard: i < N
{
    curve[frame] = (float)i / attackFrames;                   // claimed div-by-zero site
}
```

`(int)(0.00001 * 44100) == 0`, then `Math.Min(0, totalFrames) == 0`. The loop condition `i < 0` is false on entry, so `(float)i / attackFrames` is never evaluated. The same pattern applies to the 4 other loops (lines 112, 118, 154, 167).

## Task Commits

1. **Task 1: Probe reachability + author spike test + execute** — `0720fb7` (test)
2. **Task 2: Record AUDIT-VERIFIED marker in EnvelopeProcessor.cs** — `f19aeae` (docs)

_Final metadata commit (this SUMMARY) will follow this doc._

## Files Created/Modified

- `tests/spike/c3-envelope-short-segments.flow` — created. 6-probe GREEN spike exercising all 5 audit-flagged division sites at two sample rates.
- `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` — modified. `+1 insertion, 0 deletions` at line 105: single-line AUDIT-VERIFIED comment above the AR-curve attack loop.

## Inline Marker

- **File:** `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs`
- **Line:** 105
- **Text:** `// AUDIT-VERIFIED 2026-04-18: C3 — Dismissed: loop body only runs when frames > 0; see tests/spike/c3-envelope-short-segments.flow`
- **Grep check:** `grep -c "AUDIT-VERIFIED 2026-04-18: C3" flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` → `1`
- **Diff check:** `git diff --stat <prior>..HEAD -- flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` → `+1 insertion, 0 deletions`.

## Decisions Made

- **Branch 1 over Branch 2.** Reachability census showed full Flow-level access with user-controlled sample rate; empirical testing was feasible and preferred under D-05.
- **Single marker, not five.** Plan constraint (ROADMAP criterion 4, D-02) requires exactly one `AUDIT-VERIFIED 2026-04-18: C3` comment. It annotates the first claimed site (AR attack loop, line 106) and references the spike, which exercises all 5 division sites. The comment text itself explains why the 4 sibling loops are covered by the same argument.
- **Sample rate of 100 Hz for Probes 4-5.** At SR=100, even a 1 ms (0.001 s) envelope segment truncates to 0 frames without needing exotic microsecond values. This defends against a reviewer objecting that 0.00001 s durations are unrealistic — at low sample rates the bug (if real) would be reachable with ordinary millisecond values.

## Deviations from Plan

None - plan executed exactly as written. Branch 1 was the plan's primary branch, and reachability confirmed it; no rules 1-3 auto-fixes needed and no Rule 4 architectural questions arose.

## Issues Encountered

- `tests/` is gitignored (`.gitignore:7: tests/`, `.gitignore:13: *.flow`). Resolved using `git add -f`, matching the pattern already used by commits `2b59433` (C1 spike) and `b01359f` (C2 spike). Not a deviation — this is the established Phase 11 convention.

## Self-Check

- `tests/spike/c3-envelope-short-segments.flow` present (`ls -la` confirms, 3869 bytes).
- Commit `0720fb7` reachable (`git log --oneline` confirms).
- Commit `f19aeae` reachable (`git log --oneline` confirms).
- `grep -c "AUDIT-VERIFIED 2026-04-18: C3" flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` == `1`.
- `git diff --stat f19aeae^..f19aeae -- flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` == `1 insertion(+), 0 deletion(-)`.
- `dotnet build flow-lang/flow-lang.csproj` passes with 0 errors.
- Spike stdout: all 6 `probeN-survived` lines plus `all-probes-complete` sentinel; exit=0; stderr clean.

## Self-Check: PASSED

## Next Action

→ **Closed.** No Phase 12 fix task needed for C3. The 11-VERIFICATION.md row for C3 will read:

| Claim | Verdict | Evidence | Next Action |
|-------|---------|----------|-------------|
| C3 | Dismissed | `tests/spike/c3-envelope-short-segments.flow` + `EnvelopeProcessor.cs:105` | Closed |

Per D-04, this claim does NOT produce a `FIX-07c` sub-requirement; the REQUIREMENTS.md split at the end of Phase 11 should drop C3 from the stability-contingent queue.

---
*Phase: 11-audit-spike*
*Completed: 2026-04-19*
