---
phase: 16-tutorial-refresh
reviewed: 2026-04-25T00:00:00Z
depth: standard
files_reviewed: 3
files_reviewed_list:
  - examples/output/.gitignore
  - examples/tutorial.flow
  - examples/showcase.flow
findings:
  critical: 0
  warning: 2
  info: 5
  total: 7
status: issues_found
---

# Phase 16: Code Review Report

**Reviewed:** 2026-04-25
**Depth:** standard
**Files Reviewed:** 3
**Status:** issues_found

## Summary

All three files in scope build, type-check, and run cleanly under
`dotnet run --project flow-interpreter`. Both `.flow` scripts produce
non-empty WAV + MIDI artifacts at `examples/output/` and the
byte-identical determinism contract from Phase 15 holds end-to-end
(verified by independent two-run `cmp` smoke during this review:
`flow_tutorial.{wav,mid}` and `flow_showcase.{wav,mid}` all `cmp`-clean
across consecutive runs). The QOL-03 14-feature checklist is observable
in the tutorial via the grep map already pinned in 16-VERIFICATION.md.

The findings below are pedagogical/clarity issues, not correctness bugs.
The most important is **WR-01**: Chapter 15 (Enharmonic) prose comments
promise output that the printed string form does not actually produce
(`enharmonic Db4` prints `C4+`, not `C#4` as the comment implies). This
will confuse a first-time reader running the tutorial, since the lesson
is specifically about enharmonic respelling and the visible output
contradicts the chapter's claim. **WR-02** flags that Chapter 18 leans
on the `(sub 0.0 N)` idiom for negative swing without first introducing
that idiom in-chapter, even though it is the central syntactic
workaround for negative double literals (D-17 carryover).

The remaining items are info-level polish (chapter-head naming,
under-demonstration of features named in the closing summary, minor
duplication in the final "you've learned" list).

No security issues. No injection vectors. No null/index hazards. No
secrets. No infinite-loop or unreachable-code patterns. Tutorial follows
the project's S-expression idiom inside teaching prose; the only infix
arithmetic appears in the dedicated "operator style" demonstration
(line 56-58, intentional per chapter goal) and inside `proc`/lambda
bodies (lines 69, 73, 108, 113, 119), where it reads more naturally for
beginners and matches existing tutorial precedent rather than
introducing an inconsistency.

## Warnings

### WR-01: Chapter 15 enharmonic prose contradicts actual `(str Note)` output

**File:** `examples/tutorial.flow:412-418`
**Issue:** The chapter teaches enharmonic respelling and the inline
`//` comments promise specific outputs:

```
(print $"enharmonic Db4 = {(str (enharmonic Db4))}")    // C#4
(print $"enharmonic F#3 = {(str (enharmonic F#3))}")    // Gb3
(print $"enharmonic C4  = {(str (enharmonic C4))}")     // C4 (natural)
key Dbmajor {
    (print $"in Dbmajor, enharmonic C#4 = {(str (enharmonic C#4))}")    // Db4
}
```

But the actual stdout (verified by running the tutorial) is:

```
enharmonic Db4 = C4+
enharmonic F#3 = G3-
enharmonic C4  = C4
in Dbmajor, enharmonic C#4 = D4-
```

`NoteType.Format` (flow-lang/TypeSystem/SpecialTypes/NoteType.cs:175)
emits canonical `{letter}{octave}{'+' * n | '-' * |n|}` form regardless
of which spelling produced the underlying value. So enharmonic does
correctly respell internally — the printed form just doesn't reveal it.
For the chapter that explicitly teaches "the `enharmonic` built-in
flips between them," this is misleading: a reader sees `C4+` and
reasonably concludes either (a) the function is broken, or (b) `C4+`
*is* the sharp/flat form, neither of which is the lesson.

This is the kind of place where the charitable-interpretation rule
(silent assumptions over errors) does not apply — the expected
behaviour was specifically called out in prose, so silent divergence
between prose and stdout is a teaching defect rather than a tolerated
edge.

**Fix:** Pick one of:

1. **Update comments to match actual output** — least-intrusive, but
   teaches a confusing idea ("enharmonic produces the same letter with
   a `+`/`-` cents marker").
2. **Show the underlying respelling another way** — call out that the
   returned Note has the same MIDI but a different spelling internally;
   prove it via a roundtrip:
   ```
   Note flipped = (enharmonic Db4)
   (print $"enharmonic Db4 internal letter = same pitch, sharp spelling")
   (print $"  print form: {(str flipped)}")
   (print $"  (the printed +/- shows the alteration relative to the")
   (print $"   nearest natural; both Db4 and C#4 print as C4+)")
   ```
3. **Add a small helper** — e.g., a tutorial-local `proc displayNote(Note: n)`
   that emits sharp/flat conventional spelling for visual feedback, and
   use it for the chapter 15 prints. Heavier change but lands the lesson.

Option 2 is the smallest charitable fix; option 3 is the most
pedagogically honest. Either resolves the prose↔stdout mismatch.

---

### WR-02: Chapter 18 uses `(sub 0.0 N)` idiom without explanation

**File:** `examples/tutorial.flow:481-498`
**Issue:** Chapter 18's prose states:

```
Note: positive swing accents on-beats,
Note: negative swing accents off-beats (use the (sub 0.0 N) idiom for negatives).
```

then code at lines 496-498:

```
Double negSwing = (sub 0.0 0.2)
Sequence backbeat = (euclidean 5 16 C4 negSwing)
```

This is the first appearance of `(sub 0.0 N)` in the tutorial — it is
introduced in passing as "the idiom" without any explanation of *why*
the idiom exists. A new reader has no way to know that
(a) bare `-0.2` collides with binary subtraction in the parser,
(b) this is a documented language constraint (D-17 carryover from
Phase 14 D-19 / Phase 12 D-19), or
(c) the same idiom recurs later in the graduation piece (lines 249-250,
580-581).

Compare the careful introduction of every other concept in the
tutorial (variables, lambdas, the flow operator, etc.) — each has at
least a single-sentence reason. The `(sub 0.0 N)` idiom is a real
papercut that the tutorial is the natural place to defuse.

**Fix:** Add one prose `Note:` line before the negative-swing example,
e.g.:

```
Note: Bare `-0.2` would parse as binary subtraction, so for negative
Note: double literals Flow uses the (sub 0.0 N) idiom: subtract from zero.
Double negSwing = (sub 0.0 0.2)
```

This is a single-line fix that turns an undocumented incantation into
a documented language quirk, and makes the recurrence in the
graduation piece (line 249, 580) self-explanatory rather than a
mysterious pattern.

## Info

### IN-01: Chapter 5 header trailing colon reads ambiguously

**File:** `examples/tutorial.flow:123`
**Issue:** The chapter divider reads `Note: 5. Comments -- // and Note:`.
The trailing `Note:` inside the comment text is harmless to the lexer
(the leading `Note:` already comments the whole line) but visually it
parses as "...comments and Note: <something missing>". A reader
glancing at the header momentarily wonders whether something got cut
off.
**Fix:** Rephrase as `Note: 5. Comments -- // and 'Note:'` (quote the
keyword) or `Note: 5. Comments (// and Note: styles)`.

---

### IN-02: Closing summary lists `decrescendo` but tutorial only demonstrates `crescendo`

**File:** `examples/tutorial.flow:625` (and `:452` for prose mention)
**Issue:** Line 625 promises:

```
(print "  - MIDI velocity from `dynamics`, `crescendo`, `decrescendo`")
```

but the only velocity-shaping primitive actually exercised in code is
`crescendo` (line 461). `decrescendo` and `swell` are mentioned in the
chapter 17 prose intro (line 452) but never demonstrated. A diligent
reader who finishes the tutorial and tries to recall the example for
`decrescendo` will not find one.
**Fix:** Either add a one-line `decrescendo` demonstration in chapter
17 (mirror the `crescendo` example, e.g.,
`Sequence decr = baseLine -> decrescendo 0.75 0.25`) or trim the
closing-summary line to match what was actually demonstrated:
`MIDI velocity from \`dynamics\` and \`crescendo\``.

---

### IN-03: `cresc` sequence built but never rendered or otherwise used

**File:** `examples/tutorial.flow:461-462`
**Issue:** The crescendo demo creates `cresc` but only `(print $"crescendo
seq: {(str cresc)}")`s its structural form. For a chapter titled "MIDI
Velocity with Dynamics," a reader naturally expects to *hear* (or at
least see exported) the velocity gradient. The graduation piece does
not include this `cresc` sequence either, so the promised feature
("rides in the byte stream") is asserted but not demonstrated
end-to-end.
**Fix:** Either render `cresc` to a small Buffer with a quick
`renderSong` of a single-section Song, or note in prose that the
gradient surfaces only on `writeMidi` (not on the print form) and
arrange for `cresc` to feed into the graduation Song's MIDI export.

---

### IN-04: Closing-summary bullet phrasing inconsistent (slightly)

**File:** `examples/tutorial.flow:618`
**Issue:** Line 618 reads `Per-section gain (introduced now; integrated
in graduation piece)`. The "introduced now" phrasing is a leftover from
an earlier draft (when the bullet was added) — at read-time the user
has just finished the tutorial, so "now" is jarring.
**Fix:** Reword to `Per-section gain (with integration in the
graduation piece)` or simply `Per-section gain for dynamic shaping`.

---

### IN-05: showcase.flow uses inline `mp` velocity marker without acknowledging the deviation

**File:** `examples/showcase.flow:20`
**Issue:** Line 20 uses inline-marker form
`Sequence melody = | mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w |`
which is correct and parses (per Parser.NoteStream.cs:341 mapping
`mp → 0.5`). The 16-04-SUMMARY documents this as a Rule 1 deviation
(block form `dynamics mp { Sequence melody = ... }` would scope `melody`
to the block, breaking the section reference). The showcase intentionally
omits per-feature comments per CONTEXT D-03 ("wow listen to this" tone),
so this is *not* asking for explanatory prose in the showcase itself.
However, the `mp` marker is otherwise undocumented in the tutorial too
— the only place a reader could discover inline dynamic markers is by
reading the Parser source. Consider a one-line mention in tutorial
chapter 17 (alongside the existing `dynamics ff` block-form demo) so
the showcase's `| mp ...` form is reachable from tutorial knowledge.
**Fix (optional):** Add to chapter 17 after line 468:
```
Note: Inline form -- `mp` (or pp/p/mp/mf/f/ff) at the start of a stream
Note: applies to all notes that follow until the next marker or barline.
Sequence soft = | mp C4q D4q E4q F4q |
(print $"inline mp: {(str soft)}")
```
This gives the reader a documented path from tutorial chapter 17 to
the showcase's `| mp _ _ ...` line without requiring source-diving.

---

_Reviewed: 2026-04-25_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
