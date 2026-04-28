---
status: passed
phase: 19
phase_name: tuplets-arbitrary-fractional-durations
closed: 2026-04-26
verification_source: plan-19-05-closure
must_haves_verified: 8
must_haves_total: 8
deferred: []
---

# Phase 19: Tuplets & Arbitrary Fractional Durations — Verification

**Phase:** 19
**Milestone:** v1.3 Composer DX Tier B/C
**Status:** Complete
**Closed:** 2026-04-26 (plan 19-05 closure commit)
**Plans:** 5/5 complete (Wave 1: 19-01 → Wave 2: 19-02 → Wave 3: 19-03 ‖ 19-04 → Wave 4: 19-05)
**Cumulative Phase 19 Facts:** 34 (8 + 9 + 6 + 6 + 5)
**Full suite at close:** 340/340 (306 pre-Phase-19 baseline + 34 Phase19)

---

## Success Criteria Verification (from ROADMAP.md)

| # | Criterion | Pinning Artifact | Commit |
|---|-----------|------------------|--------|
| 1 | Composer can write `\| {3:2 C4 D4 E4}q \|` and three notes render summing to one quarter note (TUP-01) | `flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs::TripletQuarterGroup_ProducesThreeOneTwelfthNotes` (Fraction(1,3) quarter × 3, GetBeats sum 1.0) | `a7f94ef` |
| 2 | `{3 C4 D4 E4}q` shorthand is equivalent to `{3:2 C4 D4 E4}q` per music21 (TUP-02) | `TupletBracketTests::ShorthandThree_EquivalentToThreeTwo` + `ShorthandTwelve_RaisesParseError` + `ShorthandFive_LookupTableLocked` + `ShorthandSeven_LookupTableLocked` | `a7f94ef` |
| 3 | Nested tuplets `\| {3:2 C4 {3:2 D4 E4 F4}q G4}h \|` resolve correctly (TUP-03) | `TupletBracketTests::NestedTriplet_OuterAndInnerComposeViaScaleAccumulation` (5 notes: [2/3, 2/9, 2/9, 2/9, 2/3] quarter) | `a7f94ef` |
| 4 | `\| C4/12 D4/12 E4/12 \|` arbitrary-denominator + bar-fit validator accepts rational sums (TUP-04, TUP-05) | `flow-lang.Tests/Unit/Phase19/FractionalDurationTests.cs::SlashTwelve_ProducesThreeOneTwelfthNotes` + `SlashOne_ProducesWholeNote` + `SlashZero_RaisesParseError`; bar-fit Facts in `BarFitOverflowTests.cs::ExactFitFourFourBar_FourTupletsPlusTwoQuarters_NoOverflow` + `OverflowFiveFourths_TruncatesAtBoundary_EmitsInfo` + `TupletBracketWithoutSuffix_RaisesParseError_ValidatorNeverReached` + `SixEightBarWithOneTriplet_UnderfillAccepted` | `9aae23c` (TUP-04) + `3679ab4` (TUP-05) |
| 5 | MIDI export auto-elevates TPQN to LCM(480, tuplet_denoms) capped at 9600; `{7:8}` → 3360, `{11:13}` → cap error (TUP-06) | `flow-lang.Tests/Unit/Phase19/MidiTpqnElevationTests.cs::Triplet_StaysAt480` + `Quintuplet_StaysAt480` + `Septuplet_ElevatesTo3360` + `LargeRatioCombination_RaisesCapError` + `ZeroTuplets_StaysAt480` + `PerNoteSeptuplet_ElevatesTo3360` | `dbc6f30` |
| 6 | `augment(tupletSeq)` doubles + `diminish(tupletSeq)` halves rational durations; AUDIT-VERIFIED C5 marker re-validated (TUP-07) | `flow-lang.Tests/Unit/Phase19/TupletAugmentDiminishTests.cs::Augment_RationalDouble` + `Diminish_RationalHalve` + `Augment_NonTupletPath_StaysOnEnumPath` + `Diminish_NonTupletPath_StaysOnEnumPath` + `AuditVerifiedComment_Phase19TUP07_PresentAtBothSites` | `e2cdbe5` |

**Score:** 6/6 ROADMAP success criteria verified.

---

## Criteria → Artifact Mapping (per-Requirement detail)

| REQ | SPEC acceptance | Artifact | Plan | Commit |
|-----|----------------|----------|------|--------|
| TUP-01 | `\| {3:2 C4 D4 E4}q \|` → 3 notes DurationFraction = 1/3 quarter (= 1/12 whole), sum = 1 quarter | `TupletBracketTests::TripletQuarterGroup_ProducesThreeOneTwelfthNotes` | 19-01 | `a7f94ef` |
| TUP-02 | `{3 ...}q ≡ {3:2 ...}q` per music21; `{12 ...}` parse error | `TupletBracketTests::ShorthandThree_EquivalentToThreeTwo` + `ShorthandTwelve_RaisesParseError` + `ShorthandFive_LookupTableLocked` + `ShorthandSeven_LookupTableLocked` | 19-01 | `a7f94ef` |
| TUP-03 | Nested `{3:2 C4 {3:2 D4 E4 F4}q G4}h` → 5 notes [2/3, 2/9, 2/9, 2/9, 2/3] quarter (= [1/6, 1/18, 1/18, 1/18, 1/6] whole) | `TupletBracketTests::NestedTriplet_OuterAndInnerComposeViaScaleAccumulation` | 19-01 | `a7f94ef` |
| TUP-04 | `\| C4/12 D4/12 E4/12 \|` → 3 notes 1/3 quarter each + `/0` parse error + `/1` whole note | `FractionalDurationTests::SlashTwelve_ProducesThreeOneTwelfthNotes` + `SlashOne_ProducesWholeNote` + `SlashZero_RaisesParseError` | 19-02 | `9aae23c` |
| TUP-05 | 4/4 exact-fit clean + 5/4 overflow truncates + Info diagnostic + no-suffix parse error + underfill accepted | `BarFitOverflowTests::ExactFitFourFourBar_FourTupletsPlusTwoQuarters_NoOverflow` + `OverflowFiveFourths_TruncatesAtBoundary_EmitsInfo` + `OverflowMidElement_NonZeroRemaining_TruncatesBoundary` + `NonTupletBar_DoesNotInvokeValidator` + `TupletBracketWithoutSuffix_RaisesParseError_ValidatorNeverReached` + `SixEightBarWithOneTriplet_UnderfillAccepted` | 19-03 | `3679ab4` |
| TUP-06 | `{3:2}` → 480 + `{5:4}` → 480 + `{7:8}` → 3360 + `{11:13}` → cap error + zero-tuplet → 480 + per-note parity | `MidiTpqnElevationTests::Triplet_StaysAt480` + `Quintuplet_StaysAt480` + `Septuplet_ElevatesTo3360` + `LargeRatioCombination_RaisesCapError` + `ZeroTuplets_StaysAt480` + `PerNoteSeptuplet_ElevatesTo3360` | 19-04 | `dbc6f30` |
| TUP-07 | `augment([1/3 q × 3])` → `[2/3 q × 3]` + `diminish([1/3 q × 3])` → `[1/6 q × 3]` + AUDIT-VERIFIED comment refresh + non-tuplet enum path preserved | `TupletAugmentDiminishTests::Augment_RationalDouble` + `Diminish_RationalHalve` + `Augment_NonTupletPath_StaysOnEnumPath` + `Diminish_NonTupletPath_StaysOnEnumPath` + `AuditVerifiedComment_Phase19TUP07_PresentAtBothSites` | 19-05 | `e2cdbe5` |
| TUP-08 | `\| C4/3:2 D4/3:2 E4/3:2 \|` ≡ `\| {3:2 C4 D4 E4}q \|` + `C4/5:4h` = 1/10 whole + mixed legal + `/0:2` parse error + per-note triggers TPQN-elevation | `FractionalDurationTests::PerNoteThreeAgainstTwo_EquivalentToBracket` + `PerNoteWithHalfSuffix_OneTenthWhole` + `MixedRatios_AdjacentNotesLegal` + `PerNoteZeroNumerator_RaisesParseError` + `RandomChoiceWeights_AndPerNoteTuplet_DoNotCollide` + `MidiTpqnElevationTests::PerNoteSeptuplet_ElevatesTo3360` | 19-02 + 19-04 | `9aae23c` + `dbc6f30` |

---

## Pre-landing Collision Grep (CONTEXT D-21 — re-surfaced from 19-01-PLAN.md / 19-01-SUMMARY.md)

Recipe (per 19-01-PLAN.md §Pre-landing Collision Grep + 19-01-SUMMARY.md §Pre-landing Collision Grep Transcript):

```bash
grep -rn '\\| .*\\{|\\{[0-9]' tests/ examples/ flow-lang/ --include='*.flow'
```

**Result: EMPTY** (zero hits — `{` `}` were unused inside note streams pre-Phase-19).

Conclusion (per Phase 14 D-21 / Phase 15 closure precedent): tuplet-bracket syntax landed safely, zero identifier or syntactic collisions across the existing `.flow` corpus.

---

## Commit Hash Manifest

| Plan | Commit | Subject |
|------|--------|---------|
| 19-01 | `a7f94ef` | `feat(19-01): TUP-01/02/03 tuplet bracket {N:M ...}q + AST + compiler` |
| 19-02 | `9aae23c` | `feat(19-02): TUP-04/08 per-note fractional + tuplet-ratio shorthand` |
| 19-03 | `3679ab4` | `feat(19-03): TUP-05 bar-fit validator + charitable overflow + Info` |
| 19-04 | `dbc6f30` | `feat(19-04): TUP-06 MIDI TPQN auto-elevation + 9600 cap error` |
| 19-05 | `e2cdbe5` | `feat(19-05): TUP-07 augment/diminish tuplet-aware + AUDIT-VERIFIED refresh` |
| 19-05 docs | (this commit — closure) | `docs(19-05): close Phase 19 — verification rollup + traceability` |

---

## Full-Suite Fact Count

| Stage | `dotnet test` Fact count |
|-------|-------------------------|
| Pre-Phase-19 baseline (post-Phase-18 close) | 306 |
| Post-19-01 (TupletBracketTests, +8) | 314 |
| Post-19-02 (FractionalDurationTests, +9) | 323 |
| Post-19-03 (BarFitOverflowTests, +6) | 329 |
| Post-19-04 (MidiTpqnElevationTests, +6) | 335 |
| Post-19-05 (TupletAugmentDiminishTests, +5) | 340 |
| Phase 19 close | **340/340 GREEN** (full suite, 23 s duration) |

---

## Phase 18 Regression Gate Held

Phase 18 byte-identical Facts (`flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` + `ByteIdenticalShowcaseTests.cs`, 4 Facts; plus 15 unit Facts in `FractionTests.cs` + `MusicalNoteDataTests.cs`) — **19/19 GREEN end-to-end** through all 5 Phase 19 commits.

`examples/tutorial.flow` + `examples/showcase.flow` contain zero tuplet syntax (verified at execute time via grep), so they route through:

- **Plan 19-01:** `NoteStreamCompiler.CompileBar`'s TupletElement switch arm — unreached (no `{...}` in source)
- **Plan 19-02:** `ParseNoteStream`'s NoteLiteral arm with no Slash — `TupletRatio` stays null
- **Plan 19-03:** `ValidateBarFit`'s `Any(n => n.DurationFraction.HasValue)` guard — false → validator skipped
- **Plan 19-04:** `MidiExport.ComputeRequiredTpqn`'s `denominators.Count == 0 → return 480` — TPQN unchanged
- **Plan 19-05:** `Augment`/`Diminish`'s `if (note.DurationFraction.HasValue)` branch — false → enum path runs verbatim

End-to-end byte-identical contract is structurally guaranteed across all 5 plans, not merely empirically observed.

---

## Charitable Interpretation Memory Honoured

Per CLAUDE.md memory (`music > rigid correctness`) + CONTEXT D-03 lock: bar overflow is silent-truncate + Info diagnostic, NOT hard error. Composer feedback flows via `ErrorReporter.ReportInfo`. Same input always produces same truncation (deterministic). Plan 19-03's `BarFitOverflowTests::OverflowFiveFourths_TruncatesAtBoundary_EmitsInfo` Fact pins the deterministic charitable behaviour.

Plan 19-03 D-USER-D refinement: zero-remaining boundary case drops the offending element instead of truncate-to-zero — strictly more charitable than the original SPEC (a zero-duration note is musically meaningless; dropping it is the kind interpretation).

---

## Two-Pass Strict Authorship Outcomes (CONTEXT D-15)

| Plan | Pass 1 → Pass 2 | Outcome |
|------|-----------------|---------|
| 19-01 | TUP-01/02/03 Facts drafted from REQUIREMENTS+SPEC; production code landed Plan-side | Outcome A — REQUIREMENTS-as-drafted matched reality |
| 19-02 | TUP-04/08 Facts drafted from REQUIREMENTS prose | Outcome A |
| 19-03 | TUP-05 Facts drafted from charitable-overflow SPEC wording | Outcome A |
| 19-04 | TUP-06 Facts drafted from TPQN cap formula in SPEC | Outcome A |
| 19-05 | TUP-07 Facts drafted from REQUIREMENTS quarter-unit translation | **Outcome A — GREEN on first run** |

5 consecutive zero-divergence plans in the two-pass strict series across Phase 19, after the prior 13/14/18 series streak. Pattern reinforced: when SPEC + RESEARCH have reduced ambiguity below ~0.20, Pass 1 and Pass 2 match verbatim.

---

## Deferred / Out of Scope

Per CONTEXT.md §Deferred Ideas (already routed to other v1.3 phases or v1.4):

- LSP semantic-tokens for `{N:M ...}` syntax — flow-lsp graceful-degradation path (Phase 17 pattern); follow-up phase if needed
- Tuplet-aware `humanize` / `humanizeGaussian` interaction — **Phase 25** (DEFER-06)
- Tutorial demonstration of tuplets — **Phase 26** (QOL-04 tutorial refresh)
- Auto-fit duration inside tuplet brackets — **locked NO** (D-06; explicit duration suffix required)
- Hard-error bar overflow — **locked NO** (D-07; silent-truncate + Info diagnostic per charitable-interpretation memory)
- ABC `(p:q:r` counter-form tuplet syntax — **anti-feature** (bracket parens make `r` redundant)
- Microtonal interaction with tuplet rendering — **Phase 23** (MICR-01..03; tuplets use existing 12-TET path)

---

## Status

**Phase 19 closed.** All 8 TUP-XX requirements Shipped. AUDIT-VERIFIED C5 re-validated against tuplet sequences. Phase 18 byte-identical contract preserved structurally across all 5 plans. v1.3 milestone advances 2/9 phases complete (Phase 18 + Phase 19).

---

## Sign-off

- [x] All 6 ROADMAP success criteria verified (8 requirements TUP-01..08)
- [x] Pre-landing collision grep transcript re-surfaced verbatim (19-01-SUMMARY.md)
- [x] All 5 atomic commit hashes recorded (a7f94ef, 9aae23c, 3679ab4, dbc6f30, e2cdbe5)
- [x] Full suite green at phase close: 340/340
- [x] Phase 18 byte-identical regression gate green: 19/19 across all 5 Phase19 commits
- [x] Charitable-interpretation memory honoured per CLAUDE.md
- [x] Two-pass strict authorship discipline preserved across all 5 plans (Outcome A throughout)

---

*Phase: 19-tuplets-arbitrary-fractional-durations*
*Closed: 2026-04-26*
*Verifier: Claude (gsd-executor) via plan 19-05 closure*
