# Phase 11: Audit Spike — Context

**Gathered:** 2026-04-18
**Status:** Ready for planning

<domain>
## Phase Boundary

Produce decisive evidence — either a failing `.flow` test reproducing the bug, or an inline `// AUDIT-VERIFIED` dismissal — for each disputed 2026-04-18 audit claim (C1–C5), so Phase 12 knows the exact scope of FIX-07 before any production code is edited.

**In scope:**
- New `.flow` scripts under `tests/spike/` for each of C1–C5
- Inline dismissal comments in the source files named by the audit
- Edits to REQUIREMENTS.md to split FIX-07 into per-claim sub-requirements (after verdicts are in)
- One `11-VERIFICATION.md` summarizing all 5 verdicts

**Out of scope (per ROADMAP success criterion 4):**
- Any modification to production source in `flow-lang/` — this phase is investigation and test authoring only
- Fixes for C6, C7, or confirmed C1–C5 bugs (those belong to Phase 12 FIX-05/FIX-06/FIX-07)
- Any DX, test-unblocking, or Nyquist work

</domain>

<decisions>
## Implementation Decisions

### Dismissal documentation format
- **D-01:** Dismissal verdicts live as **inline source comments only**. No separate annotated audit file or per-claim dismissal doc — the summary goes into `11-VERIFICATION.md` (D-07).
- **D-02:** Dismissal comment format is structured and greppable: `// AUDIT-VERIFIED 2026-04-18: C[N] — <verdict> (<evidence path>)`. Future developers can `grep -r "AUDIT-VERIFIED"` across the codebase to see every audited claim at once.

### Investigation pacing & order
- **D-03:** All 5 claims investigate in **parallel as independent plans** within Phase 11. No inter-claim dependencies; no priority ordering. The plans run concurrently (respecting the planner's wave parallelization).
- **D-04:** After verdicts land, REQUIREMENTS.md gets edited in a dedicated commit to **split FIX-07** into per-surviving-claim sub-requirements (`FIX-07a`, `FIX-07b`, …). Claims dismissed in Phase 11 do NOT produce a sub-requirement; they close with the inline comment + VERIFICATION row.

### Proof bar for dismissal
- **D-05:** **Positive `.flow` test when feasible; reasoning-in-comment when the claimed bug path is not reachable from user-facing code.** For each dismissed claim, default to writing a `tests/spike/cN-<slug>.flow` that exercises the exact code path the audit named and runs green. If the path isn't reachable from Flow source (e.g., internal invariants), the inline comment carries file+line+reasoning without a new test.
- **D-06:** **C5 (augment/diminish) requires an empirical `.flow` test regardless of verdict.** The architecture and pitfalls researchers disagreed hardest on C5, so the spike must settle it with observed behavior. Test runs `augment(quarter)` / `diminish(quarter)`, prints resulting `NoteValue`, asserts the expected direction.

### Phase 12 handoff
- **D-07:** Terminal artifact is a single **`11-VERIFICATION.md`** in the phase directory with one row per claim: claim / verdict (Confirmed|Dismissed) / evidence path (`tests/spike/...` or inline comment file:line) / next action (→ Phase 12 FIX-07a, or Closed). This is Phase 12's primary input alongside the REQUIREMENTS.md diff.
- **D-08:** **Failing tests for confirmed bugs are committed during Phase 11 (red).** ROADMAP success criterion 3 requires this: "each surviving C1–C5 item becomes a fix task in Phase 12 *with its failing test already committed*." Phase 12's job is to flip them green. Dismissal tests land green.

### Claude's Discretion
- Exact filename slugs under `tests/spike/` (e.g., `c1-context-stack-leak.flow` vs `c1-musical-context.flow`) — planner can pick readable names
- Whether to use `.flow` `print` assertions vs process exit codes — match prevailing convention in `tests/`
- Content of VERIFICATION.md beyond the mandated 4-column table — Claude can add brief prose context if helpful

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Audit (source of truth for claims)
- `.planning/CODEBASE-AUDIT-2026-04-18.md` §1 Critical Bugs — the C1–C7 table this phase investigates
- `.planning/CODEBASE-AUDIT-2026-04-18.md` §5 — relevant only insofar as it frames ecosystem context; the spike ignores §2–5

### Research (conflicting interpretations to reconcile)
- `.planning/research/ARCHITECTURE.md` §"Audit Re-Verification" — architecture agent's position: C1, C3, C4, C5 likely false positives
- `.planning/research/PITFALLS.md` §2 — pitfalls agent's position: C1 description wrong but real bug exists, C5 confirmed swapped at TransformFunctions.cs:247,268, C3/C4 `Math.Max(1,frames)` is wrong fix
- `.planning/research/SUMMARY.md` §"Researcher Disagreement — Mandatory Audit Spike Before Phase B" — consolidated conflict table

### Milestone planning
- `.planning/REQUIREMENTS.md` §Audit Spike — SPIKE-01..05 acceptance criteria
- `.planning/REQUIREMENTS.md` §Stability Contingent — FIX-07 umbrella that this phase splits
- `.planning/ROADMAP.md` §"Phase 11: Audit Spike" — 4 success criteria (esp. #4 no-code-change rule)

### Code targets (for spike authoring; NOT for modification)
- `flow-lang/Interpreter/Interpreter.cs` — C1 (ExecuteMusicalContext ~130-290) and C2 (`ExecuteStatement` 71-128 `_returnValue` guard)
- `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs` — C3 (lines 108, 120, 150, 156, 169)
- `flow-lang/StandardLibrary/Audio/BufferHelpers.cs` — C4 (lines 130, 159)
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — C5 (augment line 247, diminish line 268)

### Test authoring conventions
- `tests/test_musical_context_errors.flow`, `tests/test_error_masking.flow` — existing prior-art for interpreter-level spike tests
- `tests/test_comprehensive.flow` — general convention pattern

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `tests/` directory convention: each test is a standalone `.flow` script run by `dotnet run --project flow-interpreter tests/...`; pass = no errors in stderr, exit 0
- `print (str value)` pattern is the de-facto assertion mechanism across existing tests
- `tests/test_musical_context_errors.flow` already exercises `ExecuteMusicalContext` validation paths — good template for C1/C2 spikes

### Established Patterns
- **Soft-failure error model (v1.0):** programs continue after errors accumulate in `ErrorReporter`. Spike tests should verify error accumulation is preserved, not broken.
- **No unit-test framework:** everything is `.flow`-script-based. For C3/C4 (internal invariants), if the path isn't reachable from `.flow` source, D-05 permits reasoning-only dismissal.
- **Musical context is a scoped stack:** tempo → key → timesig → gain → pan (+ new reverbTime in v1.2). ExecuteMusicalContext's `try/finally` is the hot path here.

### Integration Points
- `tests/spike/` is a NEW subdirectory — no existing `tests/spike/*.flow` yet. Phase 11 creates it.
- REQUIREMENTS.md edit in D-04 touches the Stability-Contingent section and the Traceability table.
- `11-VERIFICATION.md` fits the standard GSD phase-artifact slot (same as `VERIFICATION.md` in earlier phase directories).

</code_context>

<specifics>
## Specific Ideas

- **C5 empirical test (D-06) is non-negotiable** — even if code-reading would settle it, runtime confirmation is the tiebreaker. Test file: `tests/spike/c5-augment-diminish.flow`.
- **Parallel plans (D-03)** — likely 5 parallel plans in Phase 11, one per claim. Planner should structure as independent waves.
- **Structured comment marker (D-02)** is greppable across the entire repo: `grep -rn "AUDIT-VERIFIED"` yields the audit trail. This pattern generalizes — future audits can reuse it with new dates.

</specifics>

<deferred>
## Deferred Ideas

- **Automating the `AUDIT-VERIFIED` grep into CI** — nice-to-have; could land in a later polish phase. Not in scope for v1.2.
- **Retroactively tagging unrelated v1.1 verified code paths with AUDIT-VERIFIED markers** — scope creep; keep Phase 11 focused on C1–C5 only.
- **Major audit §2 bugs (overload ambiguity, bandpass Q unbounded, etc.)** — deferred to v1.3 per REQUIREMENTS.md Future section. Not in scope here.

</deferred>

---

*Phase: 11-audit-spike*
*Context gathered: 2026-04-18*
