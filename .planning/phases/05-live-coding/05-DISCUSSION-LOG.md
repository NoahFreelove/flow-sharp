# Phase 5: Live Coding - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-03
**Phase:** 05-live-coding
**Areas discussed:** Reload trigger & quantization, Playback continuity strategy, Error handling during playback, Scope of hot reload

---

## Reload Trigger & Quantization

| Option | Description | Selected |
|--------|-------------|----------|
| Next bar boundary | Wait for current bar to finish, start new version at next bar | yes |
| Next beat boundary | Lower latency, potentially abrupt | |
| Immediate | Stop and restart like current --watch | |

**User's choice:** Next bar boundary — most musically natural
**Notes:** None

## Playback Continuity Strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Pre-render + swap | Background render new version, swap buffer pointer at bar boundary | yes |
| Crossfade | Short blend between old and new | |
| Hard cut | Stop old, start new at boundary | |

**User's choice:** Pre-render + swap — zero gap, no artifacts
**Notes:** None

## Error Handling During Playback

| Option | Description | Selected |
|--------|-------------|----------|
| Keep playing old, show error | Continue with last valid version, print error to terminal | yes |
| Stop playback, show error | Halt until fixed | |
| Keep playing, ignore silently | No error output | |

**User's choice:** Keep playing old version, show error with line/column
**Notes:** None

## Scope of Hot Reload

| Option | Description | Selected |
|--------|-------------|----------|
| Full re-execution | Re-run entire script from scratch | yes (switched) |
| Section-only swap | Only re-execute changed sections | initially selected |
| Incremental (diff-based) | Detect and process only changed parts | |

**User's choice:** Initially selected section-only swap, then switched to full re-execution
**Notes:** User raised the concern that sections can have cross-dependencies — shared variables, musical context blocks, custom oscillator registrations, and probabilistic functions like vary(). Detecting which sections need re-rendering when external state changes is fragile and complex. Full re-execution is simpler, correct, and the background pre-render hides the latency cost anyway.

### Section Change Detection (discussed then abandoned)

| Option | Description | Selected |
|--------|-------------|----------|
| Re-execute all, diff sections | Full execution, compare SectionRegistry | initially selected |
| AST diff | Compare AST trees for changed sections | |
| Source hash per section | Hash section body source text | |

**User's choice:** Initially selected "re-execute all, diff sections" but then recognized the dependency problem and switched to full re-execution for the whole song.

## Claude's Discretion

- Threading model for background rendering
- Buffer swap mechanism (Interlocked vs lock)
- Bar boundary timing calculation from playback position
- LiveReloadManager internal architecture
- Crossfade duration if needed

## Deferred Ideas

None
