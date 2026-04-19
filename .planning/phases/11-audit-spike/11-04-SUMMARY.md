---
phase: 11-audit-spike
plan: 04
subsystem: audio
tags: [audit-spike, buffer, fade, dsp, dismissal]

requires:
  - phase: planning
    provides: "CODEBASE-AUDIT-2026-04-18.md §1 C4 claim, ARCHITECTURE.md + PITFALLS.md conflicting interpretations"
provides:
  - "Empirical confirmation that BufferHelpers.FadeIn/FadeOut div-by-zero (C4) is a false positive"
  - "Greppable `AUDIT-VERIFIED 2026-04-18: C4` marker at BufferHelpers.cs:128"
  - "tests/spike/c4-fade-short-durations.flow GREEN regression guard"
affects: [12-stability-fixes, 11-VERIFICATION.md]

tech-stack:
  added: []
  patterns:
    - "AUDIT-VERIFIED marker pattern (D-02): single-line grep-anchored comment at claim site"
    - "Spike-test convention (D-05/D-08): tests/spike/cN-<slug>.flow exercising the exact claim path"
    - "Loop-guard dismissal pattern: sibling application of 11-03's rationale to a second file/function pair"

key-files:
  created:
    - tests/spike/c4-fade-short-durations.flow
  modified:
    - flow-lang/StandardLibrary/Audio/BufferHelpers.cs

key-decisions:
  - "C4 dismissed by empirical test (Branch 1, D-05): 8 probes across fadeIn/fadeOut at SR=44100 and SR=100 with sub-frame durations all survive cleanly"
  - "Single marker above FadeIn loop (line 128) references both line 130 AND line 159 per plan constraint (one marker covers both divisions sharing the guard pattern)"
  - "Independent verification from 11-03 — loop-guard rationale re-confirmed on FadeIn/FadeOut rather than copy-pasted"

patterns-established:
  - "Loop-guard dismissal pattern (second application): `for (int frame = 0; frame < fadeFrames; frame++)` with fadeFrames==0 skips the body entirely, so the division at line 130 is unreachable when the denominator is zero. Symmetrically, `for (int frame = fadeStart; frame < source.Frames; frame++)` with fadeStart==source.Frames (which happens when fadeFrames==0) skips the body, so the division at line 159 is unreachable."

requirements-completed: [SPIKE-04]

duration: ~2min
completed: 2026-04-19
---

# Phase 11 Plan 04: SPIKE-04 — C4 BufferHelpers FadeIn/FadeOut div-by-zero — Dismissed

**Empirical `.flow` spike confirms C4 is a false positive: the `for (int frame = 0; frame < fadeFrames; frame++)` guard at BufferHelpers.cs:129 (FadeIn) and the equivalent `for (int frame = fadeStart; frame < source.Frames; frame++)` at line 158 (FadeOut, where fadeStart == source.Frames when fadeFrames == 0) both prevent the divisions at lines 130 and 159 from ever executing with a zero denominator.**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-04-19T03:47:21Z
- **Completed:** 2026-04-19T03:48:40Z
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments

- Branch 1 (empirical) spike authored: `tests/spike/c4-fade-short-durations.flow` with 8 probes (2 × zero-duration, 2 × sub-frame at SR=44100, 2 × low-SR 1ms, 2 × control).
- Both audit-flagged division sites (lines 130 and 159) exercised via `fadeIn` and `fadeOut` at SR=44100 with sub-frame durations, plus a low-SR (100 Hz) variant that forces fadeFrames to 0 with ordinary millisecond values.
- Single greppable marker added: `flow-lang/StandardLibrary/Audio/BufferHelpers.cs:128` — `// AUDIT-VERIFIED 2026-04-18: C4 — Dismissed: loop body only runs when fadeFrames > 0; same guard covers FadeOut line 159; see tests/spike/c4-fade-short-durations.flow`.
- Diff is exactly +1 insertion, 0 deletions; `dotnet build flow-lang/flow-lang.csproj` passes (0 errors, 3 pre-existing unrelated nullability warnings — same set seen in 11-03).

## Branch Chosen

**Branch 1 — empirical test.** Both `fadeIn` and `fadeOut` are registered as Flow built-ins (`BuiltInFunctions.cs:520` and `:525`) with `(Buffer, Double)` signatures and exposed to user code via `audio.flow:246` and `audio.flow:249`. `createBuffer(Int frames, Int channels, Int sampleRate)` lets user code supply arbitrary frame counts and sample rates, which is sufficient to drive `fadeFrames == 0`. The path is fully reachable from user code, so the D-05 fallback to reasoning-only dismissal was not needed.

## Reachability Census

Grep evidence (`fadeIn|fadeOut|FadeIn|FadeOut` across `flow-lang/` and `tests/`):

| Location | Role |
|----------|------|
| `flow-lang/StandardLibrary/Audio/BufferHelpers.cs:117` | `FadeIn(IReadOnlyList<Value>)` — the C# implementation |
| `flow-lang/StandardLibrary/Audio/BufferHelpers.cs:145` | `FadeOut(IReadOnlyList<Value>)` — the C# implementation |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs:517-520` | `registry.Register("fadeIn", (Buffer, Double) → Buffer, FadeIn)` — user-callable |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs:522-525` | `registry.Register("fadeOut", (Buffer, Double) → Buffer, FadeOut)` — user-callable |
| `flow-lang/audio.flow:246` | `internal proc fadeIn(Buffer: buffer, Double: duration)` stdlib declaration |
| `flow-lang/audio.flow:249` | `internal proc fadeOut(Buffer: buffer, Double: duration)` stdlib declaration |
| `tests/test_fade.flow` | Existing prior-art: `fadeBuf -> fadeIn 0.5`, `fadeBuf -> fadeOut 0.5` |
| `flow-lang/StandardLibrary/Audio/Vocalization/ConsonantSynthesizer.cs:49-148` | Internal `fadeIn`/`fadeOut` local variables — unrelated to public API |
| `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs:36,67` | `ApplyFadeOut` — internal polyphony helper, unrelated to public fade API |

`tests/test_fade.flow` relevant lines confirm Flow-level call conventions:

```flow
Buffer faded = fadeBuf -> fadeIn 0.5
Buffer fadedOut = fadeBuf -> fadeOut 0.5
```

(The `->` flow operator rewrites to `(fadeIn fadeBuf 0.5)` at parse time.)

Signatures (Flow-callable):

- `fadeIn(Buffer source, Double durationSeconds) -> Buffer`
- `fadeOut(Buffer source, Double durationSeconds) -> Buffer`

Conclusion: reachable, testable, user-controlled frame count AND sample rate → Branch 1.

## Verdict

**Dismissed.** Architecture researcher was correct. Matches the parallel 11-03 verdict for C3 and uses the same loop-guard rationale, independently re-verified on FadeIn/FadeOut.

### Evidence

Spike command:

```
dotnet run --project flow-interpreter tests/spike/c4-fade-short-durations.flow
```

stdout (trimmed of build banner/warnings):

```
Flow Language Interpreter v0.1

c4-probe1-fadeIn-zero-survived
c4-probe2-fadeOut-zero-survived
c4-probe3-fadeIn-subframe-survived
c4-probe4-fadeOut-subframe-survived
c4-probe5-fadeIn-lowSR-survived
c4-probe6-fadeOut-lowSR-survived
c4-probe7-fadeIn-normal-survived
c4-probe8-fadeOut-normal-survived
c4-all-probes-complete
```

Exit code: `0`. stderr: empty (no `DivideByZeroException`, no stack trace, no NaN warnings).

### Probe coverage matrix

| Probe | Target line | Function | SR | Duration → truncated fadeFrames | Outcome |
|-------|-------------|----------|----|--------------------------------|---------|
| 1 | 130 | fadeIn | 44100 | 0.0 s → 0 | survived |
| 2 | 159 | fadeOut | 44100 | 0.0 s → 0 | survived |
| 3 | 130 | fadeIn | 44100 | 0.00001 s → 0.441 → 0 | survived |
| 4 | 159 | fadeOut | 44100 | 0.00001 s → 0.441 → 0 | survived |
| 5 | 130 | fadeIn | 100 | 0.001 s → 0.1 → 0 | survived |
| 6 | 159 | fadeOut | 100 | 0.001 s → 0.1 → 0 | survived |
| 7 | 130 (control) | fadeIn | 44100 | 0.05 s → 2205 frames | survived |
| 8 | 159 (control) | fadeOut | 44100 | 0.05 s → 2205 frames | survived |

Probes 1-6 force `fadeFrames = Math.Min((int)(durationSeconds * source.SampleRate), source.Frames) == 0`. In every case:

- **FadeIn (line 128-136):** `for (int frame = 0; frame < 0; frame++)` — loop condition false on entry, body never evaluated, `(float)frame / fadeFrames` never runs.
- **FadeOut (line 157-165):** `fadeStart = source.Frames - 0 = source.Frames`, so `for (int frame = source.Frames; frame < source.Frames; frame++)` — loop condition false on entry, body never evaluated, `1.0f - ((float)(frame - fadeStart) / fadeFrames)` never runs.

No division ever executes with a zero denominator in either function. This matches the `ARCHITECTURE.md §"Audit Re-Verification"` row C4 conclusion and extends the 11-03 loop-guard pattern to a second file. The `PITFALLS.md §5` concern that `Math.Max(1, frames)` would be the wrong fix remains valid as general guidance (no change needed here), but is moot for C4 because no fix is needed in the first place.

### Source-level reasoning (supporting the empirical result)

```csharp
// BufferHelpers.cs:122-130 (FadeIn)
int fadeSamples = (int)(durationSeconds * source.SampleRate);
int fadeFrames = Math.Min(fadeSamples, source.Frames);       // fadeFrames >= 0
...
for (int frame = 0; frame < fadeFrames; frame++)              // guard: frame < N
{
    float t = (float)frame / fadeFrames;                      // claimed div-by-zero site (line 130)
    ...
}
```

```csharp
// BufferHelpers.cs:150-159 (FadeOut)
int fadeSamples = (int)(durationSeconds * source.SampleRate);
int fadeFrames = Math.Min(fadeSamples, source.Frames);
int fadeStart = source.Frames - fadeFrames;                   // == source.Frames when fadeFrames==0
...
for (int frame = fadeStart; frame < source.Frames; frame++)   // empty range when fadeStart==source.Frames
{
    float t = 1.0f - ((float)(frame - fadeStart) / fadeFrames); // claimed div-by-zero site (line 159)
    ...
}
```

`(int)(0.00001 * 44100) == 0`, `Math.Min(0, 4410) == 0`. For FadeIn: the condition `frame < 0` is false on entry, so the line-130 division is never evaluated. For FadeOut: `fadeStart = 4410 - 0 = 4410`, so the condition `frame < 4410` starting at `frame = 4410` is false on entry, and the line-159 division is never evaluated. Probes 1 and 2 (duration exactly 0.0) produce the degenerate case directly without any floating-point truncation reasoning; the empirical survival confirms both C# bounds semantics and the audit-claim refutation.

## Task Commits

1. **Task 1: Probe reachability + author spike test + execute** — `57293b9` (test)
2. **Task 2: Record AUDIT-VERIFIED marker in BufferHelpers.cs** — `976c6d6` (docs)

_Final metadata commit (this SUMMARY) will follow this doc._

## Files Created/Modified

- `tests/spike/c4-fade-short-durations.flow` — created. 8-probe GREEN spike exercising both audit-flagged division sites at two sample rates across both fade directions (4901 bytes).
- `flow-lang/StandardLibrary/Audio/BufferHelpers.cs` — modified. `+1 insertion, 0 deletions` at line 128: single-line AUDIT-VERIFIED comment above the FadeIn loop.

## Inline Marker

- **File:** `flow-lang/StandardLibrary/Audio/BufferHelpers.cs`
- **Line:** 128
- **Text:** `// AUDIT-VERIFIED 2026-04-18: C4 — Dismissed: loop body only runs when fadeFrames > 0; same guard covers FadeOut line 159; see tests/spike/c4-fade-short-durations.flow`
- **Grep check:** `grep -c "AUDIT-VERIFIED 2026-04-18: C4" flow-lang/StandardLibrary/Audio/BufferHelpers.cs` → `1`
- **Diff check:** `git diff --stat 976c6d6^..976c6d6 -- flow-lang/StandardLibrary/Audio/BufferHelpers.cs` → `1 insertion(+), 0 deletion(-)`
- **Line range check:** line 128 is within the required [120, 135] window, immediately above the FadeIn loop at line 129 whose body contains line 130.
- **Dual-line coverage:** verdict text explicitly names both line 130 (FadeIn) AND line 159 (FadeOut) per plan constraint — ROADMAP criterion 4 forbids a second marker at line 159, and the guard rationale is symmetric.

## Decisions Made

- **Branch 1 over Branch 2.** Reachability census showed full Flow-level access with user-controlled frame count AND sample rate; empirical testing was feasible and preferred under D-05.
- **Single marker, not two.** Plan constraint (ROADMAP criterion 4, D-02) requires exactly one `AUDIT-VERIFIED 2026-04-18: C4` comment. It annotates the FadeIn loop (first claimed site, line 130 by claim) and the verdict text explicitly names both line 130 and line 159 and cites the spike, which exercises both division sites.
- **Sample rate of 100 Hz for Probes 5-6.** At SR=100, even a 1 ms (0.001 s) fade duration truncates to 0 frames without needing exotic microsecond values. This defends against a reviewer objecting that 0.00001 s durations are unrealistic — at low sample rates the bug (if real) would be reachable with ordinary millisecond values. Mirrors the same methodological choice made in 11-03.
- **Parallel-but-independent verification of the 11-03 loop-guard rationale.** Although C3 used the same argument structure, the source of BufferHelpers.cs was read end-to-end and the FadeOut branch's `fadeStart = source.Frames - fadeFrames` construction was analyzed separately — the argument does not copy-paste from 11-03 because FadeOut starts its loop at a non-zero index, which required inspecting the arithmetic for the fadeFrames==0 case.
- **Test grew to 8 probes (vs. C3's 6).** Because C4 has two separate functions (FadeIn and FadeOut), every sub-frame scenario is run twice — once per direction — to prove the argument symmetrically. The two added probes (5 and 6) are the low-SR fadeOut variants that C3 didn't need.

## Deviations from Plan

None - plan executed exactly as written. Branch 1 was the plan's primary branch, and reachability confirmed it; no Rule 1-3 auto-fixes needed and no Rule 4 architectural questions arose.

## Issues Encountered

- `tests/` is gitignored (`.gitignore:7: tests/`, `.gitignore:13: *.flow`). Resolved using `git add -f`, matching the established Phase 11 convention (used by 11-01 C1 spike `2b59433`, 11-02 C2 spike `b01359f`, 11-03 C3 spike `0720fb7`). Not a deviation — this is the cross-plan convention.

## Self-Check

- `tests/spike/c4-fade-short-durations.flow` present (`ls -la` confirms, 4901 bytes).
- Commit `57293b9` reachable (`git log --oneline` confirms).
- Commit `976c6d6` reachable (`git log --oneline` confirms).
- `grep -c "AUDIT-VERIFIED 2026-04-18: C4" flow-lang/StandardLibrary/Audio/BufferHelpers.cs` == `1`.
- Marker at line 128 — within the required [120, 135] window, immediately above the FadeIn loop.
- `git diff --stat 976c6d6^..976c6d6 -- flow-lang/StandardLibrary/Audio/BufferHelpers.cs` == `1 insertion(+), 0 deletion(-)`.
- `dotnet build flow-lang/flow-lang.csproj` passes with 0 errors, 3 pre-existing unrelated nullability warnings (same set as 11-03).
- Spike stdout: all 8 `probeN-...-survived` lines plus `c4-all-probes-complete` sentinel; exit=0; stderr clean.
- Verdict text in the marker references both line 130 AND line 159 as required by the plan.

## Self-Check: PASSED

## Next Action

→ **Closed.** No Phase 12 fix task needed for C4. The 11-VERIFICATION.md row for C4 will read:

| Claim | Verdict | Evidence | Next Action |
|-------|---------|----------|-------------|
| C4 | Dismissed | `tests/spike/c4-fade-short-durations.flow` + `BufferHelpers.cs:128` | Closed |

Per D-04, this claim does NOT produce a `FIX-07d` sub-requirement; the REQUIREMENTS.md split at the end of Phase 11 should drop C4 from the stability-contingent queue.

---
*Phase: 11-audit-spike*
*Completed: 2026-04-19*
