# Phase 11: Audit Spike — Verification

**Date:** 2026-04-18
**Phase:** 11-audit-spike
**Status:** Complete
**Downstream consumer:** Phase 12 Stability (FIX-07 scope determination)

## Verdict Table

| Claim | Verdict | Evidence Path | Next Action |
|-------|---------|---------------|-------------|
| C1 — ExecuteMusicalContext body skip / frame leak | Confirmed (body-skip, NOT frame-leak) | tests/spike/c1-musical-context-body.flow; flow-lang/Interpreter/Interpreter.cs:292 | → Phase 12 FIX-07a |
| C2 — `_returnValue` short-circuit masks errors | Dismissed | tests/spike/c2-return-value-short-circuit.flow; flow-lang/Interpreter/Interpreter.cs:75 | Closed |
| C3 — EnvelopeProcessor div-by-zero on sub-frame segments | Dismissed | tests/spike/c3-envelope-short-segments.flow; flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs:105 | Closed |
| C4 — BufferHelpers FadeIn/FadeOut div-by-zero | Dismissed | tests/spike/c4-fade-short-durations.flow; flow-lang/StandardLibrary/Audio/BufferHelpers.cs:128 | Closed |
| C5 — augment/diminish semantic swap | Dismissed | tests/spike/c5-augment-diminish.flow; flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:239,261 | Closed |

## Summary

- **Total claims investigated:** 5
- **Confirmed (require Phase 12 fix):** 1 (C1 only)
- **Dismissed (closed with inline marker):** 4 (C2, C3, C4, C5)

### C1 — clarified scope

The audit framed C1 as an "ExecuteMusicalContext frame leak." The spike confirms a real bug
exists in `ExecuteMusicalContext`, but the mechanism is different from the audit's stated
hypothesis:

- **Frame leak hypothesis:** The `try/finally { _context.PopFrame(); }` at
  `Interpreter.cs:286-289` correctly balances the stack. No frame leak occurs.
- **Body-skip mechanism (the real bug):** Seven `return` statements inside the `try` (lines
  151, 164, 178, 224, 240, 255, 263 per 11-01-SUMMARY.md evidence) exit the method before
  the `foreach (var stmt in ctx.Body)` loop at 270-284 runs, so any validation error
  silently drops the block body. User-visible symptom: `tempo -5 { | C4 D4 E4 F4 | }`
  reports the tempo error AND discards the note stream with no trace on stdout.

Phase 12 FIX-07a must scope the fix to the body-skip mechanism. The proposed approach
(per 11-01-SUMMARY.md "Next action"): replace each early `return;` with `break;` inside
the `switch`, letting the body loop execute under the partial/default musical context.
Frame balance is already correct and must NOT be altered.

### C2–C5 — dismissal rationale

- **C2:** Source trace showed `_returnValue` is only written by the `ExecuteReturn` handler
  and function-entry/exit resets. No error path touches it. The short-circuit guard at
  `Interpreter.cs:73-74` is standard early-return semantics. Empirical GREEN test confirms
  statements after an error still execute.
- **C3 / C4:** Loop-guard pattern — `for (i = 0; i < N; i++)` with `N == 0` skips the body,
  so the divisions inside (EnvelopeProcessor lines 108/120/150/156/169; BufferHelpers
  lines 130/159) are unreachable when the denominator is zero. Empirically verified with
  sub-frame durations at SR=44100 and SR=100.
- **C5:** Architecture agent's reading confirmed. `NoteValueType.Value` enum orders
  `WHOLE=0 … THIRTYSECOND=5`, so `augment`'s `-1` correctly lengthens (QUARTER→HALF) and
  `diminish`'s `+1` correctly shortens (QUARTER→EIGHTH). D-06 empirical test confirmed via
  `visualize` ASCII piano-roll widths (A=####, Q=##, D=#).

## Phase 12 Handoff

FIX-07 has been split per D-04 into the following Phase 12 sub-requirements:

- **FIX-07a** (closes SPIKE-01, C1 Confirmed): fix `ExecuteMusicalContext` body-skip by
  replacing early `return;` statements with `break;` in the validation switch so the body
  loop at `Interpreter.cs:270-284` executes under partial/default context. Regression test
  `tests/spike/c1-musical-context-body.flow` is committed RED and flips GREEN on fix.

No FIX-07b, FIX-07c, FIX-07d, or FIX-07e requirements exist — claims C2, C3, C4, C5 are
dismissed and produce no Phase 12 work.

Dismissed claims close without code change. Their inline `AUDIT-VERIFIED 2026-04-18`
markers in production source serve as "already-reviewed" signals so future audits do not
re-raise closed items.

## Evidence — Inline Markers (cross-check)

Output of `grep -rn "AUDIT-VERIFIED 2026-04-18:" flow-lang/`:

```
flow-lang/Interpreter/Interpreter.cs:75:        // AUDIT-VERIFIED 2026-04-18: C2 — Dismissed: _returnValue only set by ReturnStatement; guard is correct (tests/spike/c2-return-value-short-circuit.flow)
flow-lang/Interpreter/Interpreter.cs:292:    // AUDIT-VERIFIED 2026-04-18: C1 — Confirmed: body skipped after validation error (tests/spike/c1-musical-context-body.flow)
flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:239:    // AUDIT-VERIFIED 2026-04-18: C5 — augment correct (lengthens); observed A=#### vs Q=## columns in visualize (tests/spike/c5-augment-diminish.flow)
flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:261:    // AUDIT-VERIFIED 2026-04-18: C5 — diminish correct (shortens); observed D=# vs Q=## columns in visualize (tests/spike/c5-augment-diminish.flow)
flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs:105:        // AUDIT-VERIFIED 2026-04-18: C3 — Dismissed: loop body only runs when frames > 0; see tests/spike/c3-envelope-short-segments.flow
flow-lang/StandardLibrary/Audio/BufferHelpers.cs:128:        // AUDIT-VERIFIED 2026-04-18: C4 — Dismissed: loop body only runs when fadeFrames > 0; same guard covers FadeOut line 159; see tests/spike/c4-fade-short-durations.flow
```

Six markers total — one per claim, except C5 which has two (one per sibling function,
Augment/Diminish). All six line numbers match the Evidence Path column above.

## BREAKING CHANGE Trigger (C5-specific)

**Not triggered.** C5 was dismissed — `augment` and `diminish` already produce the musically
correct directions (lengthening and shortening respectively). No migration story is needed
for v1.2. ROADMAP Phase 12 success criterion 4 does not apply; no `augmentV1`/`diminishV1`
aliases, no BREAKING-CHANGE release-notes entry, no `examples/*.flow` call-site updates
required.

## Spike Test Inventory

All five spike tests live under `tests/spike/` and were force-added (`git add -f`) because
`tests/` and `*.flow` are gitignored — the cross-plan convention established in Phase 11.

| Test | Verdict | Commit | Lands |
|------|---------|--------|-------|
| tests/spike/c1-musical-context-body.flow | Confirmed | 2b59433 | RED (flips GREEN in Phase 12 FIX-07a) |
| tests/spike/c2-return-value-short-circuit.flow | Dismissed | b01359f | GREEN (regression guard) |
| tests/spike/c3-envelope-short-segments.flow | Dismissed | 0720fb7 | GREEN (regression guard) |
| tests/spike/c4-fade-short-durations.flow | Dismissed | 57293b9 | GREEN (regression guard) |
| tests/spike/c5-augment-diminish.flow | Dismissed | 4c0e826 | GREEN (regression guard; D-06 empirical mandate honored) |

---

*Phase 11 complete. All five claims have a decisive verdict. Phase 12 FIX-07 scope is now
determined: one confirmed bug (C1 body-skip) gets FIX-07a; four dismissed claims close by
marker-only.*
