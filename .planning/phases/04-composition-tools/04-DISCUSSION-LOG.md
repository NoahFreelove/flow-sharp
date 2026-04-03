# Phase 4: Composition Tools - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-02
**Phase:** 04-composition-tools
**Areas discussed:** Chord progression DSL syntax, Voice leading algorithm, Polyrhythm approach, Pattern variation engine

---

## Chord Progression DSL Syntax

| Option | Description | Selected |
|--------|-------------|----------|
| New syntax block | `progression \| I IV vi V \|` parsed into ProgressionExpression AST node | yes |
| Built-in function | `progression("I IV vi V")` string argument parsed at runtime | |
| Array of chords | `[I, IV, vi, V]` using existing roman numeral resolution | |

**User's choice:** New syntax block — matches note stream `| ... |` style, feels native
**Notes:** None

## Chord Duration

| Option | Description | Selected |
|--------|-------------|----------|
| One chord per bar | Each chord fills one bar, optional `:N` suffix for multi-bar | yes |
| One chord per beat | Each chord fills one beat | |
| Explicit duration required | Every chord must specify duration | |

**User's choice:** One chord per bar with optional `:N` override
**Notes:** None

## Voice Count

| Option | Description | Selected |
|--------|-------------|----------|
| 4-voice SATB | Fixed 4-part harmony | |
| Match chord note count | Triads=3, 7ths=4 | default |
| Configurable | User specifies voice count | yes |

**User's choice:** Configurable, defaulting to chord note count
**Notes:** User wanted flexibility but practical defaults

## Polyrhythm Approach

| Option | Description | Selected |
|--------|-------------|----------|
| Parallel sections | Sections with different timesig, combined via polyrhythm() | yes |
| Inline overlay syntax | New `overlay { ... } { ... }` syntax | |
| Built-in function only | polyrhythm(seq1, seq2, beats) | |

**User's choice:** Parallel sections — leverages existing section + timesig infrastructure
**Notes:** None

## Polyrhythm Alignment

| Option | Description | Selected |
|--------|-------------|----------|
| Auto LCM + optional override | Auto-calculate LCM, optional beats param | yes |
| Always user-specified | Require total beats every time | |

**User's choice:** Auto LCM with optional override
**Notes:** User asked about how LCM alignment works — confirmed 3/4+4/4=12 beats (4x3 and 3x4)

## Pattern Variation Mutations

| Option | Description | Selected |
|--------|-------------|----------|
| Pitch shift | Move notes by scale degrees | yes |
| Rhythm variation | Split/merge durations | yes |
| Rest insertion | Replace notes with rests | yes |
| Velocity variation | Alter dynamics | yes |

**User's choice:** All four mutation types
**Notes:** None

## Variation API

| Option | Description | Selected |
|--------|-------------|----------|
| Single probability param | `vary(seq, 0.3)` — simple, one probability | |
| Per-mutation-type control | Named parameters per mutation type | |
| Both (overloads) | Simple + specific overloads | yes |

**User's choice:** Both overloads — simple `vary(seq, 0.3)` and specific `vary(seq, 0.3, "pitch")`
**Notes:** None

## Claude's Discretion

- Voice leading algorithm implementation details
- Voice count syntax (parameter vs function)
- polyrhythm function signature details
- How vary interacts with existing (? ...) syntax

## Deferred Ideas

- **Vocaloid support** — user mentioned during area selection. Noted as new capability for future phase/milestone.
