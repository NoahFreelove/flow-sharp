# Phase 11: Audit Spike — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in `11-CONTEXT.md` — this log preserves the alternatives considered.

**Date:** 2026-04-18
**Phase:** 11-audit-spike
**Areas discussed:** Dismissal documentation format, Investigation pacing & order, What proves a dismissal, Phase 12 handoff format

---

## Dismissal documentation format

### Q: Where should the "dismissal verdict" for each dismissed C1–C5 claim live?

| Option | Description | Selected |
|--------|-------------|----------|
| Inline source comment only | Minimal overhead; lives where it's needed | ✓ |
| Source comment + annotated audit file | Also edit CODEBASE-AUDIT-2026-04-18.md with verdict; duplicates info | |
| Source comment + standalone SPIKE-REPORT.md | Also a single phase-dir summary report | |

**User's choice:** Inline source comment only
**Notes:** No separate audit-file edit; phase-level summary happens in VERIFICATION.md instead (see Area 4).

### Q: What should the dismissal comment marker look like?

| Option | Description | Selected |
|--------|-------------|----------|
| `// AUDIT-VERIFIED 2026-04-18: C[N] — <verdict> (<evidence path>)` | Structured, greppable | ✓ |
| `// Re-verified 2026-04-18 — see tests/spike/cN-*.flow` | Human-readable prose, still greppable | |
| Whatever reads naturally — no template | Claude's discretion per file | |

**User's choice:** Structured greppable marker
**Notes:** `grep -rn "AUDIT-VERIFIED"` must surface every verdict trail across the repo.

---

## Investigation pacing & order

### Q: How should the 5 claims be investigated?

| Option | Description | Selected |
|--------|-------------|----------|
| Parallel — all 5 as independent plans | Fastest; 5 concurrent plans | ✓ |
| Priority-first — C1, C5, then the rest | Highest-disagreement first; some sequencing overhead | |
| Fully serial — C1 → C2 → C3 → C4 → C5 | One at a time; slowest; lowest cognitive load | |

**User's choice:** Parallel — all 5 as independent plans
**Notes:** No inter-claim dependencies were found by the architecture/pitfalls research. Planner should structure as 5 independent plans, likely a single wave.

### Q: Should we audit the FIX-07 requirement WORDING based on spike outcomes?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — edit REQUIREMENTS.md to split FIX-07 | Clean traceability; explicit Phase 12 scope | ✓ |
| No — keep FIX-07 as umbrella | Requirements file doesn't churn | |

**User's choice:** Edit REQUIREMENTS.md to split FIX-07 into sub-requirements (FIX-07a, FIX-07b, …)
**Notes:** Split happens at end of Phase 11 as a dedicated commit. Dismissed claims do not produce sub-requirements.

---

## What proves a dismissal

### Q: What evidence is required to close a claim as 'dismissed / not a bug'?

| Option | Description | Selected |
|--------|-------------|----------|
| Positive .flow test showing correct behavior | Strongest signal; most work | |
| Code reading + reasoning in the comment | Fastest; relies on reviewer's eyes | |
| Positive test when feasible, reasoning when not | Default to test; fall back when path unreachable | ✓ |

**User's choice:** Positive test when feasible, reasoning when not
**Notes:** C3/C4 internal invariants may not be reachable from user-facing `.flow` code — reasoning-only dismissal acceptable in those cases. All other dismissals get a positive `tests/spike/cN-*.flow` that would turn red if the claimed bug were introduced.

### Q: For C5 specifically, given the agent disagreement, what's the bar?

| Option | Description | Selected |
|--------|-------------|----------|
| Empirical .flow test regardless of verdict | Source of truth for C5 regardless of direction | ✓ |
| Same rules as others — no special treatment | Per-claim rule is sufficient | |

**User's choice:** Empirical .flow test regardless of verdict
**Notes:** Architecture agent said augment/diminish are correctly implemented; pitfalls agent confirmed swap at TransformFunctions.cs:247,268. Disagreement warrants runtime confirmation. Test: `tests/spike/c5-augment-diminish.flow` runs `augment(quarter)` / `diminish(quarter)`, prints `NoteValue`, asserts direction.

---

## Phase 12 handoff format

### Q: Beyond the FIX-07 split in REQUIREMENTS.md, what's Phase 11's terminal artifact?

| Option | Description | Selected |
|--------|-------------|----------|
| One Phase 11 VERIFICATION.md summarizing all 5 verdicts | Standard GSD phase-artifact; 4-column table | ✓ |
| Separate SPIKE-REPORT.md per claim (5 files) | More granular; harder to review at a glance | |
| Nothing extra — tests + inline comments are the record | Minimum overhead; relies on Phase 12 planner piecing it together | |

**User's choice:** One `11-VERIFICATION.md` summarizing all 5 verdicts
**Notes:** Table columns: claim / verdict (Confirmed|Dismissed) / evidence path / next action (→ Phase 12 FIX-07a or Closed). Fits standard GSD phase-artifact conventions.

### Q: If a claim confirms a real bug during Phase 11, should the failing test be committed in Phase 11 or Phase 12?

| Option | Description | Selected |
|--------|-------------|----------|
| Commit failing tests in Phase 11 | ROADMAP criterion 3 already requires this; Phase 12 flips red→green | ✓ |
| Hold tests until Phase 12 | No red tests in master; contradicts ROADMAP | |

**User's choice:** Commit failing tests in Phase 11 (red); Phase 12 flips them green
**Notes:** Aligns with ROADMAP.md success criterion 3. Dismissal tests land green. Phase 12 sees a concrete TDD-style starting point per surviving claim.

---

## Claude's Discretion

- Exact filename slugs under `tests/spike/` — planner picks readable names
- `.flow` `print` assertions vs process exit codes — match prevailing `tests/` convention
- Content of VERIFICATION.md beyond the mandated 4-column table — brief prose context allowed if helpful

## Deferred Ideas

- Automating the `AUDIT-VERIFIED` grep into CI — nice-to-have, later phase
- Retroactively tagging unrelated v1.1 verified code paths — scope creep, skip
- Major audit §2 bugs (overload ambiguity, bandpass Q, etc.) — deferred to v1.3 per REQUIREMENTS.md
