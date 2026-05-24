# Phase 42 — Type System & Stdlib Audit (Skeleton)

**Generated:** 2026-05-24
**Source:** `scripts/StdlibAuditor` reflective harness over 37 FlowType subclasses and 413 registered signatures.
**Status:** SKELETON — Plan 03 authors the prioritized body using `type-signature-graph.json` (this file's machine-readable sibling).

---

## Orphaned Types

_Coercible types with zero consumer signatures. Reference-identity types (Tuning/Sfz/MarkovModel/LsystemModel/OscHandle) excluded per RESEARCH Pitfall 2._

Count: 1

## Missing Conversions

_To be authored from `overload_gap_candidates` + manual cross-check._

## Asymmetric Pairs

_Pairs where `A.IsCompatibleWith(B) != B.IsCompatibleWith(A)` (or the CanConvertTo equivalent). False positives expected for music-type → numeric widening (by design, Pitfall 5)._

Count: 122

## Dead-End Builtins

_Builtins whose return value flows nowhere — requires manual cross-check against `.flow` stdlib (REQ-AUDIT-05)._

## Overload Gaps

_Functions accepting Double/Float but missing music-type companions (REQ-AUDIT-06)._

Count: 85

## Clamp & Advisory Inventory

_To be authored from Plan 02's `grep -rn 'Math.Clamp'` + `'RenderingDiagnostics.WarnOnce'` sweep._

## Prioritization & Phase Routing

_To be authored: each finding routed to Phase 43, Phase 44, or v1.6 backlog with composer-impact rationale._

## Limitations

- `FunctionSignature` has no `ReturnType` field (Open Question 1) — producer half of the type graph is inferred manually, NOT enumerated.
- Reference-identity types (TuningType, SfzType, MarkovModelType, LsystemModelType, OscHandleType) are excluded from the orphan list by design.
- Asymmetric-pair detection produces a candidate list; many entries are correct-by-design widening edges (e.g. Beat → Double).
