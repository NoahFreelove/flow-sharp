# Project Research Summary -- v1.2 Stability & Composer DX

**Project:** Flow Language
**Domain:** Brownfield milestone -- interpreter bug fixes + composer DX features on an existing music-production DSL
**Researched:** 2026-04-18
**Confidence:** MEDIUM-HIGH overall (HIGH on stack/features/integration points; MEDIUM on bug reproducibility due to researcher disagreement on C1-C5)

---

## Executive Summary

v1.2 is a **brownfield stability + DX milestone**, not a new-subsystem milestone. Research across stack, features, architecture, and pitfalls converges on a single recommendation: **no new dependencies, no new tooling, no new subsystems -- every v1.2 target is an extension of existing machinery.** The seven critical bug fixes (C1-C7), the three test-unblocking additions (`range(Int,Int)`, `break`/`continue`, `bpm`/`createStereoTrack`/`renderBars`), and the five Tier A composer DX features (`slice`/`loopEdit`, enharmonic helpers, `reverbTime` context, MIDI velocity from dynamics, euclidean swing/humanize) are all implementable by editing existing files.

The **significant risk** emerging from research is **disagreement between the architecture and pitfalls researchers about the reality of audit findings C1, C2, C3, C4, and C5**. The architecture agent, after re-reading source, believes four of these may be false positives -- the frame leak, statement short-circuit, and div-by-zero claims appear to describe code behavior that differs from what the source actually does. The pitfalls agent, reading the same files, agrees the audit *description* of C1 is incomplete but insists there IS a real bug with a different mechanism (validation errors cause the block body to be silently skipped, not frame leakage), and separately confirms C5 (augment/diminish semantic swap) as a real, shipped breaking bug. **Both agree C6 and C7 are real.** This conflict requires an explicit "reproduce or close" spike at the top of Phase A before anyone edits allegedly-buggy code, because changing working code while believing it is broken will introduce regressions that are hard to bisect later.

The recommended approach: **stability-first ordering** (audit spike -> confirmed bugs -> test unblocking -> validation debt), then **smallest-surface DX before largest-surface DX** (MIDI velocity -> slice -> euclidean -> enharmonics -> reverbTime), with **C5's semantic-swap communication artifacts (release notes, migration aliases, example updates) bundled into the same commit as the code fix** because a silent duration-semantics flip will break user compositions if shipped without a migration path.

---

## Key Findings

### Recommended Stack

The stack is **unchanged from v1.1** and deliberately minimal. No new NuGet package is required for any v1.2 requirement; all work is internal C# edits against existing `flow-lang` / `flow-interpreter` projects.

**Core technologies (existing, no changes):**
- **.NET 10 (`net10.0`)** -- per actual csproj inspection (CLAUDE.md says net9 but repo moved to net10; SDK 10.0.106 confirmed installed)
- **C# 13/14** -- records, pattern matching, file-scoped namespaces already pervasive
- **Melanchall.DryWetMidi 8.0.3** -- already referenced; used by `StandardLibrary/Audio/MidiExport.cs` (velocity byte construction at line 192 already maps `note.Velocity * 127`); no upgrade needed for MIDI-velocity Tier A work
- **PulseAudio via P/Invoke** -- already implemented in `Audio/PulseAudioSimpleBackend.cs`; not touched by any v1.2 feature
- **Pidgin 3.5.1** -- referenced but unused (actual parser is hand-written recursive descent); cleanup candidate for later milestone, leave as-is for v1.2

**Explicitly rejected:** NAudio, CSCore, NWaves, MathNet.Numerics, FFT libraries, managed-midi, xUnit/NUnit, DryWetMidi 9.0-prerelease. Reasons per STACK.md: Windows-centric, abandonware signal, duplication of hand-rolled DSP, or wrong risk profile for a stability milestone.

See: `.planning/research/STACK.md`

### Expected Features

Composer DX research benchmarked Flow's Tier A bundle against SuperCollider, TidalCycles, Sonic Pi, Strudel, LilyPond, and mainstream DAW / notation software. Every Tier A feature has a clear precedent and an expected user mental model.

**Must have (table stakes -- composers expect in any modern music-coding environment):**
- `slice(seq, start, end)` + `loopEdit(...)` -- TidalCycles/Strudel `slice` + DAW region-paste intuition; 0-based, exclusive end
- `H` alias for `B` (German notation) + `enharmonic(note)` -- LilyPond / notation-software precedent; **additive only -- do NOT redefine `B` as `Bb`**
- Per-voice `reverbTime { ... }` context block -- DAW aux-send / Strudel `.room()` / SuperCollider orbit-per-pattern precedent; value in seconds (RT60 convention)
- MIDI velocity from dynamics envelope -- MuseScore single-note-dynamics / Dorico CC11+velocity precedent; sample at note onset, map 0.0-1.0 -> MIDI 8-127 (floor of 8 prevents whisper-silence)
- Euclidean swing/humanize -- TidalCycles `swingBy` / Ableton RRG / MPC precedent; reuse `MusicalContext.Swing` field (0.5 = straight)

**Differentiators (Flow's idiomatic twists):**
- `loopEdit(seq, start, end, replacement)` -- first-class phrase editing, derived from `slice`; no surveyed environment has a single-call equivalent
- `reverbTime` as a **context block** (not a plugin send) -- pure declarative model
- Automatic MIDI velocity from Flow dynamics -- closes export-parity loop that MuseScore/Dorico struggle with

**Explicitly anti-features (defer or exclude):**
- Slice by sub-beat time (mid-note split complexity)
- Full German-mode remap of `B` -> `Bb` (silent-breaking every existing script)
- `reverbTime` as live-modulatable UGen (requires streaming DSP; offline-batch model stays)
- Humanize that changes pitch (overlaps with `C4+50c` cent syntax)
- `enharmonic(seq)` whole-sequence re-spelling (hard music-theory problem; v2+)
- MIDI CC11 expression export alongside velocity (v1.2 = velocity only)

See: `.planning/research/FEATURES.md`

### Architecture Approach

The integration architecture is a direct superset of existing mechanisms. No new subsystem boundaries. Feature-by-feature file-touch analysis:

**Major components reused:**
1. **`MusicalContext` scoped stack** (`Runtime/MusicalContext.cs`) -- add one `double? ReverbTime` field mirroring existing `Gain`/`Pan`/`Swing`; extend `Clone()`, `ToString()`, and `ExecutionContext.GetMusicalContext` resolution loop (including the early-break predicate at line 201-205 which must be updated to acknowledge the 8th property)
2. **Built-in registration** (`StandardLibrary/BuiltInFunctions.cs` + `InternalFunctionRegistry`) -- `slice`, `enharmonic`, euclidean overloads slot into the existing `FunctionSignature` -> lambda pattern (~100 existing registrations)
3. **AST + interpreter switch-dispatch** -- `MusicalContextType.ReverbTime` enum case, new `case` in `ExecuteMusicalContext`, mirror of existing validation path
4. **Lexer keyword table + note-detection** (`Lexing/SimpleLexer.cs:540-608, 670-701`) -- one-line additions for `reverbTime`, carefully scoped additions for `H` / `Db` / `Cb` alterations
5. **Dynamics transforms** (`StandardLibrary/Transforms/TransformFunctions.cs:395-474`) -- `Crescendo`/`Decrescendo`/`Swell` **already write per-note `Velocity`** via `ApplyVelocityGradient`; MIDI export **already reads** `note.Velocity` at `MidiExport.cs:192`. The "MIDI velocity from dynamics" feature may be mostly-complete already -- verification task, not new-code task
6. **DSP primitives** (`Audio/DSP/Reverb.cs`) -- Schroeder reverb already parameterized (input, roomSize, damping, mix); `reverbTime` is a closed-form RT60 -> feedback-coefficient transform (`feedback = 10^(-3 * delayTime / RT60)`)
7. **Bjorklund algorithm** (`BuiltInFunctions.cs:1028-1071`) -- already implemented; swing/humanize extensions add parameters, not algorithm

**Critical data-flow uncertainty (highlighted by architecture agent, flagged for requirements phase):**
- Does `NoteStreamCompiler` propagate the active `MusicalContext.Velocity` (from a `dynamics` block) into `MusicalNoteData.Velocity` at compile time? **Unknown without inspection of the 647-line file.** If not, the MIDI-velocity feature needs an explicit propagation fix; if so, it may be entirely verification-only work.

See: `.planning/research/ARCHITECTURE.md`

### Critical Pitfalls

The pitfalls research surfaces ten distinct pitfalls across bug-fix and feature work. The highest-impact ones for roadmap planning:

1. **C1 "frame leak" -- the audit description is wrong, but there IS a bug.** Early `return`s inside the `try` block of `ExecuteMusicalContext` correctly pop the frame via `finally`, but they **skip the block body** -- so `tempo -1 { | C4 D4 | }` emits an error AND silently drops the 12 notes. The naive fixes (`return` -> `throw`; `return` -> `break`) both violate project invariants. Correct fix: set `musicalCtx` to valid defaults on validation error, emit error, **fall through to body execution**. Must NOT be committed with C2.

2. **C5 `augment`/`diminish` semantic swap is a shipped breaking change.** The code fix is one line per function, but the **communication artifacts are the whole work**: BREAKING CHANGE section in release notes, transitional aliases (`augmentV1`/`diminishV1`), audit of every `examples/*.flow` call site, tutorial update, numeric-duration regression test. Must ship as a single atomic release; PR review comment "this is just a one-line swap, ship it" is a rejection.

3. **C7 Thunk exception caching -- real bug, different mechanism than the audit stated.** `_isEvaluated` is NOT set on exception (contra audit); failed thunks re-evaluate on retry, duplicating side effects and re-reporting errors. Fix matches `Lazy<T>` semantics: cache the exception, re-throw on subsequent `Force()`.

4. **New keywords (`reverbTime`) and new note aliases (`H`, `Db`) risk identifier collision.** Must grep stdlib/examples/tests before committing; prefer **note-stream-scoped** aliasing for `H`/`Db` (apply only inside `| ... |`) to avoid breaking `Int H = 5` user variables. `reverbTime` must reject negative/zero values to match existing `tempo`/`pan`/`gain` validation.

5. **Euclidean humanize determinism + PRNG stability.** `System.Random` is explicitly not stable across .NET patch versions; Flow's "code is the score" reproducibility contract requires a pinned PRNG (xorshift64*, splitmix64) and **a required or documented-defaulted seed parameter**. Without a seed, rendering the same .flow twice produces different MIDI/WAV bytes -- violates DSL philosophy.

Also: C3/C4 naive `Math.Max(1, frames)` fix masks rather than fixes zero-length-segment behavior (skip the segment instead); MIDI velocity sampling policy must be pinned (note-onset sample; document it); retroactive Nyquist validation must be written against requirements, not code, to avoid confirmation bias.

See: `.planning/research/PITFALLS.md`

---

## Researcher Disagreement -- Mandatory Audit Spike Before Phase B

**This is the single most important planning-phase input.** The architecture and pitfalls researchers independently re-read the source files cited by `CODEBASE-AUDIT-2026-04-18.md` and disagree on whether multiple critical findings are real bugs.

| # | Audit Claim | Architecture agent | Pitfalls agent | Status |
|---|-------------|--------------------|-----------------|--------|
| C1 | `ExecuteMusicalContext` leaks frames on early-return | Likely false (try/finally does pop) | Real bug with **different mechanism**: early returns skip block body, dropping statements | **Audit wording wrong; real bug exists with different semantics** |
| C2 | `_returnValue` statement short-circuit masks errors | Partial; guard exists, behavior unclear | Real, coupled with C1 | **Needs repro; coupled with C1** |
| C3 | `EnvelopeProcessor` div-by-zero | Likely false (loop body gated on N>=1) | Real but `Math.Max(1,n)` is wrong fix (skip segment instead) | **Needs repro; fix direction matters** |
| C4 | `BufferHelpers` div-by-zero | Likely false (same pattern) | Same as C3 | **Needs repro** |
| C5 | `augment`/`diminish` swapped | Likely false (semantics appear correct) | **Confirmed swapped at `TransformFunctions.cs:247,268`** -- one agent verified file:line | **Needs human verification** -- researchers disagree on a file:line claim |
| C6 | `init([])` silent empty | **CONFIRMED** | **CONFIRMED** | Real, ship fix |
| C7 | `Thunk` cache corruption | **CONFIRMED** | **CONFIRMED** (with different mechanism than audit stated) | Real, ship fix |

**Required action during REQUIREMENTS phase:** include an explicit "reproduce or close" audit spike as the first deliverable of any stability phase. Each of C1, C2, C3, C4, C5 needs a failing test (or a documented inability to produce one, closing the claim) **before any code is edited**. Changing working code while believing it's broken is the fastest path to regressions this milestone cannot afford.

C6 and C7 are confirmed real by both agents and can proceed to fix without a spike.

---

## Implications for Roadmap

Based on converged research, the following phase structure is suggested. **All researchers agree on ordering: stability before DX, smallest-surface DX before largest-surface DX.**

### Phase 1: Audit Spike -- Reproduce or Close (C1-C5)
**Rationale:** Researchers disagree on whether C1-C5 are real bugs. Fixing-without-reproducing risks regressions in working code. Each claim gets a <1-hour repro attempt producing either a failing test or a documented dismissal.
**Delivers:** Failing tests for confirmed bugs + closed audit items for dismissed claims + rewritten bug descriptions for bugs that exist with different mechanisms than audit stated (C1 especially).
**Addresses:** C1, C2, C3, C4, C5 status resolution.
**Avoids:** PITFALLS P1-P5 (Pitfalls §1-5 all assume the bug is real; spike validates the assumption first).

### Phase 2: Stability -- Confirmed Critical Bugs + Test Unblocking
**Rationale:** C6, C7 are confirmed; any C1-C5 survivors from spike belong here; test-unblocking trio is pure interpreter/stdlib work; Nyquist validation debt from v1.1 phases 6-9.
**Delivers:**
- C6 (`init([])` error) and C7 (Thunk exception caching) fixes -- both isolated single-file edits
- Any confirmed C1-C5 fixes with behavior-pinning numeric regression tests (not "no exception thrown" smoke tests)
- `range(Int, Int)` overload, `break`/`continue` interpreter signals (tokens already exist per `SimpleLexer.cs:593-594`), `bpm()` / `createStereoTrack` / `renderBars` -- or documented removal from `test_full_song.flow`
- Nyquist validation tests for v1.1 phases 6-9, written **against requirement docs not code** to avoid confirmation bias
**Addresses:** All audit Critical items + test suite unblocking + v1.1 validation debt.
**Avoids:** PITFALLS P1, P2, P4, P5, P10. One commit per Critical fix (no bundling) to enable bisection.
**Uses:** Existing `ExecutionContext`, `Thunk`, `Collections`, interpreter control-flow infrastructure.

### Phase 3: C5 Semantic Swap with Communication Artifacts
**Rationale:** C5 is a user-visible semantic breaking change. The code fix is trivial; the comms work is the milestone. Must ship atomically.
**Delivers:**
- `augment` / `diminish` swap (correct musical semantics: augment lengthens, diminish shortens)
- `augmentV1` / `diminishV1` transitional aliases (deprecated, removal targeted v1.3)
- BREAKING CHANGE release-notes section with before/after examples
- Tutorial updates (every mention of augment/diminish audited)
- `examples/*.flow` audit -- each call site labeled with original intent
- Optional: migration script that rewrites `.flow` files to `augmentV1`/`diminishV1`
- Numeric duration regression tests (not behavioral)
**Addresses:** C5.
**Avoids:** PITFALLS P3 specifically. This is the only Critical bug that is a semantic change rather than an objectively-wrong-behavior fix -- communication is the whole work.
**Standalone phase** because the comms work is substantial and confounds bisection if bundled with other fixes.

### Phase 4: Composer DX -- Smallest Surface First
**Rationale:** Each DX feature is independent (with one internal ordering: `slice` before `loopEdit`). Ship smallest blast-radius first so earlier work stabilizes before widest-surface `reverbTime`.
**Delivers (ordered by blast radius):**
1. **MIDI velocity from dynamics** -- start with **verification pass** of existing `NoteStreamCompiler` + `TransformFunctions` + `MidiExport` chain; the feature may be mostly-complete already. Only write new code for the confirmed-missing link(s). Pin sampling policy (velocity-at-note-onset) in docs.
2. **`slice(seq, start, end)` + `loopEdit(seq, start, end, replacement)`** -- pure `BuiltInFunctions.cs` additions; unit is bars, 0-based exclusive end.
3. **Euclidean swing/humanize** -- extend `euclidean` registration with swing (consults `MusicalContext.Swing`) + humanize (pinned xorshift64* PRNG + required seed). Defer micro-timing jitter if `MusicalNoteData` lacks timing-offset field; land swing-as-velocity-accent + velocity-humanize for v1.2.
4. **Enharmonic helpers (`H` + `enharmonic()` + flat spellings)** -- NoteType.Parse + SimpleLexer edits + new `enharmonic()` registration. **Note-stream-scoped aliasing only** (do not lex `H` as note globally). Land same patch as any audit-flagged lexer-lookahead fix.
5. **`reverbTime { ... }` context block** -- nine-file touch (lexer, parser, AST, runtime, stdlib DSP, render site, resolution loop). Ship last so earlier stability work is bedded in before the widest-surface DX change. Pre-requirement: grep stdlib/examples/tests for `reverbTime` identifier; validate positive values; update `ExecutionContext.GetMusicalContext` early-break predicate for 8th property.

**Addresses:** All Tier A features from FEATURES.md must-have list.
**Avoids:** PITFALLS P6, P7, P8, P9.
**Uses:** Existing `MusicalContext`, `BuiltInFunctions` registration pattern, `Reverb.cs` parameterization, `MidiExport.cs` velocity byte, Bjorklund algorithm.

### Phase 5: Tutorial Refresh + Documentation
**Rationale:** v1.2 adds real composer-DX surface that deserves tutorial coverage; also closes the documentation lag from v1.1 (math stdlib, mix, presets).
**Delivers:** `examples/tutorial.flow` updated to include one runnable snippet per v1.1 Validated requirement + every v1.2 Tier A feature; release notes finalized.
**Addresses:** UX pitfall ("tutorial that doesn't exercise v1.1 features").
**Avoids:** Feature atrophy.
**Pure `.flow` content work** -- no changes to `flow-lang/` source.

### Phase Ordering Rationale

- **Audit spike first** because the researcher disagreement on C1-C5 is the highest-impact planning risk; resolving it before any code edit avoids regressions in working code.
- **Stability before DX** because DX features extend modules that the bug fixes may touch (e.g., `reverbTime` adds to `ExecuteMusicalContext`, where C1/C2 may or may not need to be fixed first).
- **C5 as its own phase** because the comms work is substantial and confounds bisection if bundled.
- **Within DX: smallest surface first** because integration risk compounds; MIDI-velocity may be verification-only; `reverbTime` touches nine files.
- **Tutorial last** because it documents the shipped reality; writing it early means rewriting it when features shift.

### Research Flags

Phases likely needing deeper research during planning (via `/gsd-research-phase`):

- **Phase 1 (Audit Spike):** **YES -- required.** Needs source-walk research for each of C1-C5: write the failing test (or document inability to produce one). Output is the decisive input for Phase 2.
- **Phase 4.1 (MIDI velocity verification):** **YES -- recommended.** Specifically for `NoteStreamCompiler` (647 lines) -- does it propagate `MusicalContext.Velocity` to `MusicalNoteData.Velocity` at compile time? This determines whether the feature is verification-only or requires new code. Architecture agent flagged this as the single highest-leverage unknown.
- **Phase 4.3 (Euclidean swing/humanize):** **YES -- required.** PRNG choice + seed semantics + units of humanize (fraction of beat vs. fraction of sixteenth vs. milliseconds) must be specified in requirements before coding; pitfalls agent lists five concrete ambiguities.

Phases with standard patterns (skip deeper research):

- **Phase 2 (confirmed C6, C7, test trio):** All files already inspected; precedent pattern established in existing code.
- **Phase 3 (C5 swap):** Implementation is trivial; comms artifacts are straightforward to draft from release-note conventions.
- **Phase 4.2 (slice/loopEdit):** `Take`/`Drop` precedent in `Collections.cs` is direct template.
- **Phase 4.4 (enharmonic helpers):** `PitchConversion.GetMidiNote` exists; `NoteType.Parse` pattern is established.
- **Phase 4.5 (reverbTime):** Pattern mirrors shipped `gain`/`pan`/`swing` context blocks -- known recipe.
- **Phase 5 (tutorial):** Pure `.flow` content; no new research needed.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | **HIGH** | Direct csproj/file inspection verified all dependencies; no new deps required; official DryWetMidi NuGet metadata confirmed current stable |
| Features | **HIGH** on mental models / MEDIUM on exact behavioral defaults | Composer-DX patterns cross-referenced across 6+ environments (SuperCollider, TidalCycles, Sonic Pi, Strudel, LilyPond, DAWs); ambiguities on sampling policy / slice-unit / seed-default remain for requirements phase |
| Architecture | **HIGH** on integration points (file:line precision) / **MEDIUM** on one data-flow hop (NoteStreamCompiler velocity propagation) | Integration points verified against HEAD; single unknown flagged explicitly |
| Pitfalls | **HIGH** (audit-verified via file:line inspection) | Ten pitfalls mapped to specific mechanisms; each has prevention + warning-sign + recovery plan |
| **C1-C5 reality** | **LOW -- requires human spike** | Architecture and pitfalls researchers independently re-read source and disagree; this is the single highest-impact gap |

**Overall confidence:** **MEDIUM-HIGH.** The stack, features, architecture integration points, and pitfall mechanisms are all well-understood. The one soft spot is the audit-claim reality check, which has a clear mitigation (the Phase 1 spike).

### Gaps to Address

1. **C1/C2/C3/C4/C5 audit claims need human repro** -- handled by Phase 1 Audit Spike; each gets a failing test or a documented dismissal before any fix is written.
2. **`NoteStreamCompiler` velocity propagation path** -- handled by Phase 4.1 verification pass; determines MIDI-velocity scope.
3. **Slice unit (bars vs. note-indices)** -- requirements-phase decision; recommendation: bars.
4. **Humanize units (fraction of beat / sixteenth / ms)** -- requirements-phase decision; recommendation: fraction of beat duration, matching DAW "Humanize %" convention.
5. **Humanize seed semantics (required vs. optional vs. default-0)** -- requirements-phase decision; recommendation: optional with default 0 meaning "no randomness" for full reproducibility.
6. **MIDI velocity floor (1 vs. 8 vs. 0-as-rest-threshold)** -- requirements-phase decision; recommendation: 8 per pitfalls §8 to avoid whisper-silent triggers; audit follow-up M-1 overlaps this file and may be bundled.
7. **`reverbTime` RT60 semantics vs. Reverb.cs roomSize/damping parameterization** -- requirements-phase decision; both options are viable and a single line-of-math translation exists.

All seven gaps have clear mitigations at the requirements or spike phase -- none blocks roadmap generation.

---

## Sources

### Primary (HIGH confidence -- directly verified in this session)

**Codebase file:line inspections:**
- `flow-lang/flow-lang.csproj` -- verified net10.0, DryWetMidi 8.0.3, Pidgin 3.5.1
- `flow-lang/Interpreter/Interpreter.cs:73-128, 130-290` -- C1, C2 analysis
- `flow-lang/Runtime/MusicalContext.cs:17-106` -- scoped property pattern + `ValidKeys` enharmonic coverage
- `flow-lang/Runtime/Thunk.cs:27-46` -- C7 exception-caching analysis
- `flow-lang/Runtime/ExecutionContext.cs:186-212` -- musical-context resolution + early-break predicate
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:239-279, 395-474` -- C5 swap + `ApplyVelocityGradient` dynamics
- `flow-lang/StandardLibrary/Audio/MidiExport.cs:180-210` -- velocity byte construction
- `flow-lang/StandardLibrary/Audio/EnvelopeProcessor.cs:95-172` -- C3 re-audit
- `flow-lang/StandardLibrary/Audio/BufferHelpers.cs:115-168` -- C4 re-audit
- `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs:26` -- Schroeder reverb parameterization
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:1011-1075` -- euclidean registration + Bjorklund
- `flow-lang/StandardLibrary/Collections.cs:84-92` -- C6 init([]) confirmation
- `flow-lang/Lexing/SimpleLexer.cs:540-610, 670-701` -- keyword switch + note lookahead
- `flow-lang/Lexing/TokenType.cs:593-594` -- Break/Continue tokens already defined
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:21-73, 174-211` -- Parse + Velocity field
- `.planning/CODEBASE-AUDIT-2026-04-18.md` -- audit source (re-verified, partially challenged)
- `PROJECT.md` -- Key Decisions (soft-failure error model, scoped musical context)
- `CLAUDE.md` -- minimal-deps philosophy, conventions

**Official external references:**
- [Melanchall.DryWetMidi NuGet](https://www.nuget.org/packages/Melanchall.DryWetMidi) -- v8.0.3 current stable as of 2025-12-15
- [LilyPond -- note names in other languages](https://lilypond.org/doc/v2.25/Documentation/notation/note-names-in-other-languages) -- German H/B convention
- [Rational Acoustics -- RT60 reverberation time](https://support.rationalacoustics.com/support/solutions/articles/150000190451-reverberation-time-spilling-the-t-on-rt60) -- RT60 standard unit
- [Ableton CV Tools -- Rotating Rhythm Generator](https://www.ableton.com/en/blog/geometric-sequencing/) -- Euclid + swing mainstream precedent

### Secondary (MEDIUM confidence -- community consensus / multiple sources)

- TidalCycles docs (slice, swingBy, euclid) -- userbase wiki + official reference pages
- Strudel docs (slice, room, chunk) -- official Strudel documentation
- SuperCollider class docs (Pseq, Pdef, PbindFx) -- doc.sccode.org
- MuseScore forum threads on MIDI dynamics export -- canonical source for the pain point Flow is solving
- Dorico forum on CC11 vs velocity -- industry-standard discussion

### Tertiary (LOW confidence -- single source or inference, flagged)

- **The audit itself (`CODEBASE-AUDIT-2026-04-18.md`) on C1-C5** -- this is a LOW-confidence input, not HIGH, given researcher disagreement; Phase 1 spike is the mitigation.

### Full source lists

See individual research files: `.planning/research/STACK.md` §Sources, `.planning/research/FEATURES.md` §Sources, `.planning/research/ARCHITECTURE.md` §Confidence & Gaps, `.planning/research/PITFALLS.md` §Sources.

---

*Research completed: 2026-04-18*
*Ready for roadmap: yes -- with Phase 1 Audit Spike as a non-negotiable first deliverable before any code edit*
