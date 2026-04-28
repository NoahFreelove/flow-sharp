# Phase 14 — Deferred Items

**Created:** 2026-04-20 (Phase 14 closure — plan 14-04)
**Scope:** Items scoped-out of Phase 14 that need a home before the phase ships.

Phase 14 (composer DX — part 1) delivered DX-05 (slice), DX-06 reduced scope
(flat-literal surface + `enharmonic()`), and DX-08 (MIDI velocity regression).
The items below surfaced during planning or execution and were intentionally
left for a future phase. Each carries acceptance criteria so it can be picked
up cleanly later.

---

## DEFER-02: `H` = `B` note-stream alias

**Origin:** Original DX-06 clause per REQUIREMENTS.md (preserved as audit-trail
in the reframed DX-06 row). CONTEXT D-10/D-11 — scope-reduce decision during
Phase 14 planning.

**Full requirement** (verbatim from the original audit-trail):

> `H` accepted as `B` alias **only within note-stream context** (`| … |`).
> Must NOT break existing identifier `H` as a variable name in ordinary code.

**Intended family** (beyond the single-letter clause, inferred from the
extended alteration surface shipped in Phase 14 Plan 02):

- `H` → `B`
- `H4`, `H5`, etc. → `B4`, `B5` (with octave)
- `H+`, `H++` → `B+`, `B++` (with alteration)
- `H4+`, `Hb`, `H-`, `H#` etc. — consistent with the D-07/D-08 alteration
  encoding

**Why deferred** (CONTEXT D-10/D-11):

A global alias pollutes the namespace. `Int H = 5;` today compiles cleanly
and would silently change meaning if `H` were aliased, because the lexer
cannot distinguish "note letter" from "identifier" without additional
context. The user's decision is to redesign around a **pragma /
feature-flag language construct** (DEFER-03) that makes the alias opt-in
per file or per block.

**Dependencies blocking implementation:**

- DEFER-03 (pragma system) must ship first — the H alias is the first user
  of that system.

**Acceptance criterion (for the future phase that picks this up):**

- A locale/config pragma that, when active in a file or block, makes the
  lexer treat `H` as the English `B` and `B` as the English `Bb` (full
  German-notation swap), or the simpler H→B-only variant if the user
  decides the full swap is too aggressive.
- Round-trip tests that confirm English and German spellings of the same
  piece render identical audio.
- Docs entry under the note-name ergonomics guide explaining which alias
  set is active and how to switch.
- Clear error (via the diagnostic catalogue) when `H` is used outside a
  German-locale context, pointing the user at the pragma.

---

## DEFER-03: Pragma / feature-flag language construct

**Origin:** CONTEXT D-11 — future-phase redirect to support DEFER-02 (H
alias) and other locale-specific grammar variants without polluting the
global namespace.

**Candidate keyword** (non-binding — the future phase finalizes):

```
enable "german-notation"              // file-scoped
enable "german-notation" { … }        // block-scoped
```

**First user:** German-notation addon, enabling the H/B alias family from
DEFER-02 within the enabled scope.

**Design open questions for the future phase** (CONTEXT §Deferred Ideas):

- File-scoped vs block-scoped semantics — or both, layered?
- Do enable blocks stack (multiple enables active simultaneously)?
- Interaction with `use` imports — are pragmas imported?
- Do pragma-aware tokens also propagate into chord literals / key blocks?
- Pragma registry: hard-coded list vs discoverable/extensible?
- Does the pragma affect lexer dispatch at token production, or at the
  parser's token-consumption stage?
- Error message when a disabled pragma is referenced outside its scope?

**Why this is the right shape** (CONTEXT §Specifics):

Niche syntactic variants should be **opt-in**, not global. Pragmas scope
the change to users who request it, preserving the default grammar
stability that solo-composer users rely on.

**Candidate future phase location:** Between Phase 17 (Language Server)
and any new milestone, OR as part of a v1.3 language-extension milestone.

---

## DEFER-04: Multi-letter enharmonic-edge respelling

> ~~**Origin:** CONTEXT D-05 — intentional exclusion from Phase 14 scope.~~
>
> ~~**Full requirement** (if later demanded):~~
>
> > ~~`E ↔ Fb`, `F ↔ E#`, `B ↔ Cb`, `C ↔ B#` enharmonic-edge respelling.~~
> > ~~Under Phase 14's no-key fallback (D-05), naturals return unchanged.~~
> > ~~A strict-mode `enharmonic(note, strict: true)` variant or a separate~~
> > ~~`enharmonicWithEdges()` built-in could carry this without disturbing the~~
> > ~~baseline.~~
>
> ~~**Why deferred:**~~
>
> ~~No current user need. Adding edge-respelling by default risks silent~~
> ~~respelling where composers expect identity (`enharmonic("E4")` returning~~
> ~~`"Fb4"` is rarely what a composer means). Deferred until requested~~
> ~~explicitly.~~

**CLOSED 2026-04-26 by Phase 20 plan 20-02 (DEFER-04).** Implementation: 5-line
natural-edge switch in `HarmonyFunctions.Enharmonic` — E↔Fb / F↔E# (same octave) /
B↔Cb (octave +1) / C↔B# (octave -1); D/G/A naturals remain unchanged (no enharmonic
edge per CONTEXT D-USER-C). In-key diatonic preservation rule (D-USER-B) preserves
Phase 14 D-04 — edge respelling fires only on no-key fallback. Existing
`ComputeFlippedSpelling` already produces correct inverses for the non-natural
edge cases (Fb→E, E#→F, Cb→B, B#→C). 4 Phase14/EnharmonicTests Facts migrated to
`NoKey_NaturalEdgeRespells_*` per Pitfall 1 shape (a). Commit hash: `d835336`. See
`.planning/phases/20-cheap-defer-closures-multi-letter-enharmonic-edges/20-02-SUMMARY.md`
for full divergences.

---

## DEFER-05: Shared MIDI-read helper promotion (`Shared/MidiReadHelpers.cs`)

> ~~**Origin:** CONTEXT §Claude's Discretion — Phase 14 plan 14-03 keeps the
> MIDI-read helper INLINE inside `DynamicsMidiVelocityTests`. Phase 15 DX-09
> (euclidean humanize) is likely to need the same `MidiFile.Read` +
> `GetNotes` + velocity projection pattern for reproducibility assertions.~~
>
> ~~**Trigger for promotion:** Second Fact duplicates the inline call shape.
> Extract to `flow-lang.Tests/Shared/MidiReadHelpers.cs` at that point.~~
>
> ~~**Signature sketch:**~~
>
> ~~```csharp
> internal static class MidiReadHelpers
> {
>     public static byte[] GetVelocityBytes(string midiPath);
>     public static int[] GetNoteNumbers(string midiPath);
> }
> ```~~

**CLOSED 2026-04-21 by Phase 15 Plan 01.** Helper promoted to
`flow-lang.Tests/Shared/MidiReadHelpers.cs` with signatures:
`GetVelocityBytes(string) -> byte[]`, `GetNoteNumbers(string) -> int[]`,
`ReadAllBytes(string) -> byte[]` (the third method added beyond the
sketch above to support byte-identical Fact comparisons without each
caller re-typing `File.ReadAllBytes`). Consumers: Phase 14
`DynamicsMidiVelocityTests` (refactored to call helper) + Phase 15
`EuclideanByteIdenticalTests` (F-19). `grep -rn "MidiFile.Read"
flow-lang.Tests/` returns exactly 2 lines, both inside
`Shared/MidiReadHelpers.cs` itself — zero duplicate call sites leaked.
See `.planning/phases/15-composer-dx-part-2/15-01-SUMMARY.md` and
`.planning/phases/15-composer-dx-part-2/15-VERIFICATION.md §Deferred
Items Summary`.

---

## DEFER-06: `slice` negative-from-end indexing (Pythonic)

> ~~**Origin:** CONTEXT D-01 — Phase 14 ships simple two-sided clamping.~~
> ~~Negative indices (e.g., `slice(seq, -2, -1)`) are not interpreted as~~
> ~~from-end selection; they clamp to 0 instead.~~
>
> ~~**Resolution path:** If a user surfaces the need, add a separate overload~~
> ~~or a `reverseSlice` / `sliceFromEnd` rather than overload existing~~
> ~~semantics. Overloading the existing clamping behavior would silently~~
> ~~change the meaning of any script that currently relies on the clamp.~~

**CLOSED 2026-04-26 by Phase 20 plan 20-03 (DEFER-05 supersedes).** Implementation:
pre-clamp Pythonic normalization (`idx < 0 ? idx + count : idx`) in BOTH
`Collections.SliceArray` and `Collections.SliceSequence`, preserving Phase 14 D-01
silent-clamp tradition for post-normalization out-of-range values per CONTEXT
D-USER-D. The original DEFER-06 recommendation ("add a separate overload or
`reverseSlice` / `sliceFromEnd` rather than overload existing semantics") was
superseded by REQUIREMENTS.md DEFER-05 which explicitly framed this as a
behavioral change. Existing 9 Phase14/SliceTests Facts UNCHANGED (verification
matrix shows cases coincide between old silent-clamp and new Python normalization).
Repository grep `slice.*,.*,.*-` over `tests/` empty — zero existing user scripts
relied on the old clamp. Commit hash: `edd20b1`. See
`.planning/phases/20-cheap-defer-closures-multi-letter-enharmonic-edges/20-03-SUMMARY.md`.

---

## Cross-reference

- Project-level deferred-items roll-up: `.planning/deferred-items.md`
  (created when the first project-level entry is captured).
- Phase 12 deferred-items: `.planning/phases/12-stability/deferred-items.md`
  (DEFER-01 — `range` stdlib function).
- Phase 14's contribution to the deferred log is DEFER-02 through DEFER-06
  above.

## Handling protocol

When a future phase picks up one of these items:

1. Copy the acceptance criterion into that phase's plan as a success
   criterion.
2. Reference this file in the plan's `<context>` so reviewers can see the
   history and constraints.
3. Once delivered, strike through the entry here (don't delete it) and
   link to the SUMMARY that closed it out, so the audit trail stays
   intact.
