---
phase: 16-tutorial-refresh
fixed_at: 2026-04-25T00:00:00Z
review_path: .planning/phases/16-tutorial-refresh/16-REVIEW.md
iteration: 2
findings_in_scope: 7
fixed: 7
skipped: 0
status: all_fixed
---

# Phase 16: Code Review Fix Report

**Fixed at:** 2026-04-25
**Source review:** .planning/phases/16-tutorial-refresh/16-REVIEW.md
**Iteration:** 2

**Summary:**
- Findings in scope: 7 (2 warnings closed in iteration 1 + 5 info items in iteration 2)
- Fixed: 7 (WR-01, WR-02 in iteration 1; IN-01..IN-05 in iteration 2)
- Skipped: 0

## Previously fixed (iteration 1)

The two warnings were closed in the first `critical_warning`-scope fix pass and remain in their original state — iteration 2 did NOT re-touch them.

### WR-01: Chapter 15 enharmonic prose contradicts actual `(str Note)` output

**Files modified:** `examples/tutorial.flow`
**Commit:** `37df7e6`
**Applied fix:** Adopted Option 2 from the review (charitable explainer). Replaced the misleading inline `// C#4` / `// Gb3` / `// Db4` comments with the canonical cents-marker form Flow's `NoteType.Format` actually produces (`C4+`, `G3-`, `C4`, `D4-`). Added a short prose block explaining that `enharmonic` does flip the Note's internal letter and preserves MIDI, but `(str)` always prints the canonical letter+cents-marker form, so the visible respelling surfaces in MIDI export rather than in stdout.

### WR-02: Chapter 18 uses `(sub 0.0 N)` idiom without explanation

**Files modified:** `examples/tutorial.flow`
**Commit:** `aaca7bc`
**Applied fix:** Added a four-line `Note:` paragraph before the negative-swing example explaining why the `(sub 0.0 N)` idiom exists: bare `-0.2` would parse as binary subtraction, so Flow uses subtract-from-zero for negative double literals. Cross-referenced the same idiom's recurrences in chapter 9 (gain) and the graduation piece.

## Iteration 2 fixes

All five Info findings applied as one atomic commit each. Every change was either a string-literal edit or a `Note:`/comment-only insertion plus a single standalone print demo (IN-05); none touched the executable code paths feeding into the graduation `Song sunrise`, so the Phase 15 F-19/F-20 byte-identical determinism contract for `flow_tutorial.{wav,mid}` is preserved end-to-end.

### IN-01: Chapter 5 header trailing colon reads ambiguously

**Files modified:** `examples/tutorial.flow`
**Commit:** `21c03df`
**Applied fix:** Quoted the keyword in the chapter divider — changed `Note: 5. Comments -- // and Note:` to `Note: 5. Comments -- // and 'Note:'`. Single-line text fix; no executable code touched.

### IN-02: Closing summary lists `decrescendo` but tutorial only demonstrates `crescendo`

**Files modified:** `examples/tutorial.flow`
**Commit:** `9622ae8`
**Applied fix:** Trimmed the you've-learned bullet from `MIDI velocity from \`dynamics\`, \`crescendo\`, \`decrescendo\`` to `MIDI velocity from \`dynamics\` and \`crescendo\``. Chose this over adding a `decrescendo` demo because adding executable sequence/print calls in chapter 17 would risk shifting the graduation piece's exported byte stream. Left the chapter 17 prose intro at line 462 alone — it mentions `decrescendo` and `swell` only as the family of dynamic transforms, which is informational rather than a demo claim.

### IN-03: `cresc` sequence built but never rendered or otherwise used

**Files modified:** `examples/tutorial.flow`
**Commit:** `4425a41`
**Applied fix:** Added a five-line `Note:` paragraph after the chapter 17 crescendo demo clarifying that `(str cresc)` shows only the structural sequence — the per-note velocity gradient (0.25 -> 0.75) surfaces in the WAV+MIDI bytes when a Song containing this kind of sequence is exported via `writeWav` / `writeMidi`, and pointing the reader at the graduation piece below as the end-to-end MIDI export demo. Chose the prose-explainer option over rendering `cresc` to a Buffer because adding render/export calls would risk breaking the Phase 15 byte-identical contract.

### IN-04: Closing-summary bullet phrasing inconsistent (slightly)

**Files modified:** `examples/tutorial.flow`
**Commit:** `d2c78a9`
**Applied fix:** Replaced `Per-section gain (introduced now; integrated in graduation piece)` with `Per-section gain for dynamic shaping`. Single string literal change; no executable behaviour shift.

### IN-05: showcase.flow uses inline `mp` velocity marker without acknowledging the deviation

**Files modified:** `examples/tutorial.flow`
**Commit:** `0a2b35f`
**Applied fix:** Added the review's optional snippet right after the existing `dynamics ff` block-form demo:

```
Note: Inline form -- `mp` (or pp/p/mp/mf/f/ff) at the start of a stream
Note: applies to all notes that follow until the next marker or barline.
Sequence soft = | mp C4q D4q E4q F4q |
(print $"inline mp: {(str soft)}")
```

Previously the inline-marker form was reachable only by reading the parser source. This gives chapter 17 a one-line on-ramp into the showcase's `| mp _ _ E5q G5q | ...` idiom. **Critically:** the new sequence is a top-level chapter demo — intentionally NOT wrapped in a `section` and NOT fed into `renderSong`, so the graduation piece's exported byte stream is unchanged.

## Skipped Issues

_None — all 5 in-scope info findings applied cleanly._

## Verification

All fixes are documentation/string-literal changes plus one standalone print demo (IN-05) that does not feed into any `section` or `Song`. After applying iteration 2, the following end-to-end verification passed:

| Check | Result |
|---|---|
| `dotnet run --project flow-interpreter examples/tutorial.flow` (run 1) | exit 0 |
| `dotnet run --project flow-interpreter examples/tutorial.flow` (run 2) | exit 0 |
| `cmp examples/output/flow_tutorial.wav` (run 1 vs run 2) | clean (byte-identical) |
| `cmp examples/output/flow_tutorial.mid` (run 1 vs run 2) | clean (byte-identical) |
| `dotnet run --project flow-interpreter examples/showcase.flow` | exit 0 |
| `examples/output/flow_tutorial.wav` size (matches iteration 1) | 5,503,724 bytes |
| `examples/output/flow_tutorial.mid` size (matches iteration 1) | 814 bytes |
| `examples/output/flow_showcase.wav` size (matches iteration 1) | 2,352,044 bytes |
| `examples/output/flow_showcase.mid` size (matches iteration 1) | 200 bytes |
| `dotnet build` | 0 errors (13 pre-existing warnings, unrelated) |
| New `inline mp:` print appears in chapter 17 stdout | confirmed |

The Phase 15 F-19/F-20 byte-identical determinism contract is preserved end-to-end across both `flow_tutorial.{wav,mid}` and `flow_showcase.{wav,mid}`. The seeded `(euclidean ... 42)` and `tempoRamp` values in the graduation piece were not touched. Output file sizes match iteration 1 exactly, confirming no shift in the rendered byte stream.

All 7 findings from `16-REVIEW.md` are now resolved (2 warnings in iteration 1 + 5 info items in iteration 2).

---

_Fixed: 2026-04-25_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 2_
