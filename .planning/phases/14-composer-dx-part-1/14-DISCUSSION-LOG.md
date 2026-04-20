# Phase 14: Composer DX Part 1 — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in 14-CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-20
**Phase:** 14-composer-dx-part-1
**Areas discussed:** slice semantics, enharmonic() direction, H-outside-streams behavior (→ scope change + pragma system), DX-08 scope + plan structure, flat-literal surface, collision grep enforcement, enharmonic location

---

## Gray Area Selection

| Option | Description | Selected |
|--------|-------------|----------|
| slice semantics | Clamping, negative indices, whole-bar behavior, overload split | ✓ |
| enharmonic() direction | Flip, always-sharp, always-flat, key-aware | ✓ |
| H-outside-streams behavior | Where enforcement lives; alias family scope | ✓ |
| DX-08 + plan structure | Verify-only vs plumbing, regression shape, plan count | ✓ |

---

## slice semantics

| Option | Description | Selected |
|--------|-------------|----------|
| Silent clamp both sides | neg→0, end>count→count, start>=end→empty. No errors. | ✓ |
| Clamp low, error high | negs clamp; start>count or end<start raises. | |
| Clamp all, negative = from-end | Pythonic -1-indexing. | |

**User's choice:** Silent clamp both sides (Recommended).

| Option | Description | Selected |
|--------|-------------|----------|
| Single plan, atomic commit | Both Sequence + Array overloads in one plan. | ✓ |
| Two plans — Sequence first, then Array | Split by primary vs follow-on. | |

**User's choice:** Single plan, atomic commit (Recommended).

---

## enharmonic() direction

| Option | Description | Selected |
|--------|-------------|----------|
| Flip sharp↔flat, naturals unchanged | Db4↔C#4, C4 stays C4. | |
| Flip direction, with natural edges | Adds E↔Fb, F↔E#, B↔Cb, C↔B#. | |
| Always toward sharps | Canonical form; breaks flat round-trip. | |
| Key-context aware | Reads active key to pick spelling. | ✓ |

**User's choice:** Key-context aware.
**Notes:** Triggered the fallback-rule clarification below.

### No-key / Cmaj / Amin fallback

| Option | Description | Selected |
|--------|-------------|----------|
| Flip sharp↔flat, naturals unchanged | Fallback: Db4↔C#4; naturals unchanged. | ✓ |
| Always flip (incl. natural edges) | Adds E↔Fb / F↔E# etc. | |
| Always toward sharps | Normalization style. | |

**User's choice:** Flip sharp↔flat, naturals unchanged (Recommended).

### In-key semantics

| Option | Description | Selected |
|--------|-------------|----------|
| Match alteration sign to key signature | Flat keys prefer flats; sharp keys prefer sharps; diatonic naturals unchanged. | |
| Only respell diatonic scale tones | In-scale pitch → scale-diatonic spelling; chromatic pitch → fall back to no-key rule. | ✓ |

**User's choice:** Only respell diatonic scale tones.

---

## H-outside-streams behavior (became scope change + pragma system discussion)

**First attempt asked:** where to enforce the H-only-in-note-stream rule (lexer vs note-stream parser) and whether the alias covers the full `H4`/`H+`/`H++` family.

**User's response:** Clarifying question: "Why is H=B? in what context". After explanation of German musical notation tradition, user reframed:

> "Sure, I like the idea of having a special syntax to enable niche features. it lets us add a bunch of addons without polluting the namespace. Lets no use `use` though, a better keyword should be used"

This turned the question into a scope + language-design decision.

### Scope decision

| Option | Description | Selected |
|--------|-------------|----------|
| Split — new phase for pragma, H defers | Phase 14 DX-06 keeps flats + enharmonic only; H + pragma become a future phase. | ✓ |
| Keep in 14 — pragma + H ship together | Bigger phase, one landing. | |
| Drop H entirely from v1.2 | Flats + enharmonic only; H deferred indefinitely. | |

**User's choice:** Split — new phase for pragma, H defers (Recommended).

### Pragma keyword direction (non-binding; future phase finalizes)

| Option | Description | Selected |
|--------|-------------|----------|
| `dialect "german"` | Grammar-variant framing. | |
| `enable "german-notation"` | Verb-first. | ✓ |
| `feature "german-notation"` | Feature-flag framing. | |
| `extend "german-notation"` | Grammar-extension framing. | |

**User's choice:** `enable "german-notation"`.

---

## DX-08 scope + plan structure

| Option | Description | Selected |
|--------|-------------|----------|
| Two-pass strict, fix gaps if any | Pass 1 drafts; Pass 2 lands + fixes if RED. | ✓ |
| Verify-only; no gap-fix budget | Assume GREEN; follow-on plan if RED. | |
| Full plumbing plan | Assume work-in-progress; land compile-time propagation. | |

**User's choice:** Two-pass strict, fix gaps if any (Recommended).

| Option | Description | Selected |
|--------|-------------|----------|
| Purpose-built small script | New `tests/test_dynamics_midi_velocity.flow` + MIDI byte assertion Fact. | ✓ |
| Extend existing `tests/test_dynamics.flow` | Mix stdout + MIDI assertions. | |

**User's choice:** Purpose-built small script with known velocity gradient (Recommended).

| Option | Description | Selected |
|--------|-------------|----------|
| 3 parallel plans + closure | 14-01 slice · 14-02 flats+enharmonic · 14-03 DX-08 · 14-04 closure. | ✓ |
| Bundled 2 plans | 14-01 all three DX items · 14-02 closure. | |
| Research plan first | 14-00 RESEARCH.md + 14-01..04. | |

**User's choice:** 3 parallel plans + closure (Recommended).

---

## Flat-literal surface

| Option | Description | Selected |
|--------|-------------|----------|
| Bare + default-octave only | `Db`, `Eb` etc.; no stacking with `+`/`-`. | |
| Full composition incl. postfix | `Db`, `Bb`, `Bb+`, `Bb-` as double-flat. | |
| Bare + strict range policy | Bare + octave, plus explicit range-below-E0 error. | |

**User's choice:** Full flexibility (free-text note).
**User's free-text notes:** "Full flexibility - as long as its connected to the note, you can do Bb-+bbb if youd like"

Interpreted as: arbitrary mix of `b`/`#`/`+`/`-` attached to the note letter, on either side of the octave digits; net alteration = (count of sharps) − (count of flats) as any integer. Alteration encoding extended past current ±2 range. Range validation uses post-alteration MIDI value.

---

## Collision grep enforcement (ROADMAP criterion 5)

| Option | Description | Selected |
|--------|-------------|----------|
| One-shot grep at plan time + 14-VERIFICATION.md | Phase 11/12 audit-marker precedent. | ✓ |
| xUnit Fact that greps tree on every test run | Ongoing regression guard. | |
| Both | Belt and suspenders. | |

**User's choice:** One-shot grep at plan time + checked into 14-VERIFICATION.md (Recommended).

---

## enharmonic() location

| Option | Description | Selected |
|--------|-------------|----------|
| `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` | Existing harmony file; already touches `MusicalContext`. | ✓ |
| New `flow-lang/StandardLibrary/Notes/NoteFunctions.cs` | New file for note-level ops. | |

**User's choice:** `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (Recommended).

---

## Claude's Discretion

- Exact xUnit Fact naming for new regression tests
- Whether the MIDI-read helper gets promoted to a shared fixture
- Internal emission style for extended-range alteration in `NoteType.Format`
- Error message text for range-overshoot (must mirror Head/Last format)
- LINQ vs explicit allocation inside `slice`
- Whether plan 14-04 ships a `14-VALIDATION.md`

## Deferred Ideas

- H alias (moved into future pragma phase)
- Pragma / `enable` keyword system (future phase)
- Multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#)
- Shared MIDI read helper promotion (revisit in Phase 15)
- Pythonic negative-from-end slicing
- `14-VALIDATION.md` creation (Claude's discretion at 14-04)
