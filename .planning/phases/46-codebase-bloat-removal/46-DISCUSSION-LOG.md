# Phase 46: Codebase Bloat Removal - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-30
**Phase:** 46-codebase-bloat-removal
**Areas discussed:** TimelineMap fate, Progression DSL fate, cleanup breadth, low-level synth API, superseded composer-callable APIs, keep treatment

---

## TimelineMap (editor live-highlighting render path)

| Option | Description | Selected |
|--------|-------------|----------|
| Remove | Delete TimelineMap.cs + parallel render overloads (~250 LOC); zero callers, re-add cheaply for v1.6 LSP | ✓ |
| Preserve | Keep parallel paths for a concrete v1.6 flow-lsp highlighting plan | |

**User's choice:** Remove
**Notes:** Not composer-reachable — internal render plumbing. Re-add if v1.6 LSP actually wires highlighting.

---

## Progression DSL (`progression | I IV V |`)

| Option | Description | Selected |
|--------|-------------|----------|
| Remove | Delete ProgressionExpression + ProgressionCompiler (~340 LOC); superseded by in-key numerals | |
| Keep + invest | Keep the syntax, write unit tests, add to showcase | ✓ |

**User's choice:** Keep + invest
**Notes:** Consistent with the keep-usable-features principle (D-01) — it's a composer-callable syntax surface.

---

## Cleanup breadth / appetite

| Option | Description | Selected |
|--------|-------------|----------|
| §1 + §2 medium | High-priority + medium items (~1,100 LOC upper-bound, one test gate) | ✓ |
| §1 high-priority only | Locked high-priority only (~700 LOC) | |
| Maximal | §1 + §2 + resolve §3 direction calls in this phase | |

**User's choice:** §1 + §2 medium
**Notes:** Later filtered by D-01 — composer-facing items within §1/§2 are kept, narrowing actual removal to ~550–650 LOC.

---

## Low-level synth API (OscillatorState/Envelope + audio.flow convenience layer §2.3)

| Option | Description | Selected |
|--------|-------------|----------|
| Keep core, trim speculative | OscillatorState stays; remove §2.3 convenience wrappers | |
| Treat as superseded | Remove convenience wrappers + deprioritize OscillatorState | |
| Keep everything | Both surfaces stay; only dead createSineTone quad-decls cleaned | (effectively ✓) |

**User's choice (free-text):** "Don't remove things just because there aren't examples using it. If it's a feature which users can use then keep it. We can't have all flow code ever written locally to see what's used or will be used."
**Notes:** This became the GOVERNING PRINCIPLE (D-01) for the whole phase, not just this question. Keep OscillatorState/Envelope AND the audio.flow convenience layer AND `preview`. Recorded as external memory `feedback_usage_not_removal_signal.md`.

---

## Superseded composer-callable APIs (which to still remove)

| Option | Description | Selected |
|--------|-------------|----------|
| Track/Timeline DAW layer | ~380 LOC, one test consumer, roadmap-locked removal | (KEPT) |
| bars.flow legacy bar API | ~120 LOC, zero example usage, superseded by note-streams | (KEPT) |
| exportWav alias | Reversed-arg redundancy of writeWav | ✓ remove |
| test.flow legacy assertions | Pre-Phase-35 assertion lib, superseded by @test module | ✓ remove |

**User's choice:** Remove exportWav + test.flow legacy assertions; keep Track/Timeline + bars.flow.
**Notes:** User asked two clarifying questions, answered from code: (1) bars.flow is Beat-orthogonal (measure construction, zero Beat refs) — so the "useful for Beats?" rationale doesn't apply, but it stays as a usable bar API anyway; (2) Track/Timeline shares the `Voice` type with Song/Section but is a parallel manual-mixing abstraction, not integrated into the Song render path — a distinct capability, so kept. exportWav and the legacy assertion half are pure redundancy with strictly-better equivalents (writeWav, @test), so they qualify for removal under D-01(b).

---

## Keep treatment (for superseded-but-kept surfaces)

| Option | Description | Selected |
|--------|-------------|----------|
| Document as legacy | Short "superseded by X" note, fully functional, no warnings | ✓ |
| Leave untouched | No doc changes | |
| Soft-deprecate | Functional + one-shot stderr advisory | |

**User's choice:** Document as legacy
**Notes:** No deprecation warnings / advisories — premature pre-traction.

---

## Claude's Discretion

- Target ordering within the phase (suggested: Fixtures merge first as latent-bug risk reducer).
- Whether the §1.6 confirm-grep is its own task.
- Wording/placement of legacy doc notes.
- Verification mechanics beyond the locked test-green gate.

## Deferred Ideas

- flow-lsp editor live-highlighting (the feature TimelineMap was scaffolding for) → v1.6.
- §3.2 conversion-proc unification (frames/beats/seconds) → future product-direction call.
- §2.6 FlowFunctionSynthesizer inlining / §2.7 IFunctionInvoker → awareness-only, not actionable.
