# Phase 14: Composer DX Part 1 — Context

**Gathered:** 2026-04-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Ship the three Tier-A composer DX features scoped in ROADMAP Phase 14 — `slice` (bar-level for Sequence, element-level for Array[T]), flat-letter note literals (`Db`/`Eb`/`Gb`/`Ab`/`Bb`/`Cb`/`Fb` + arbitrary alteration composition via any mix of `b`/`#`/`+`/`-`), `enharmonic(Note) → Note`, and end-to-end verification that the existing `dynamics`/`crescendo`/`decrescendo`/`swell` pipeline emits correct MIDI velocity bytes. Each landing is a separate bisectable commit on top of the Phase 12 stability baseline, with a regression test authored before the code change lands.

**In scope:**
- DX-05: `slice(Sequence, Int, Int) → Sequence` and `slice(Array[T], Int, Int) → Array[T]` — both overloads atomic in one plan
- DX-06 (reduced): flat literals (`Db`, `Eb`, `Gb`, `Ab`, `Bb`, `Cb`, `Fb`) accepted by `NoteType.Parse` via an **extended alteration surface** — arbitrary mix of `b`/`#`/`+`/`-` attached to the note letter, net alteration = (sharps − flats) as any integer
- DX-06 (reduced): `enharmonic(Note) → Note` — key-context-aware respelling, reads active `MusicalContext.Key`
- DX-08: purpose-built regression test asserting MIDI note-on velocity bytes for a `.flow` script that uses `dynamics` + `crescendo`/`decrescendo`/`swell`. Two-pass strict authorship with a gap-fix budget inside the same plan if the chain is discovered non-wired on any path
- REQUIREMENTS.md DX-06 reframe in plan 14-04 (original wording preserved as audit-trail, Phase 12 TEST-03 pattern)
- deferred-items.md capturing `H` alias + pragma system for a future phase
- 14-VERIFICATION.md rollup with atomic commit hashes

**Out of scope:**
- `H` as `B` alias inside note streams — DX-06 clause dropped from Phase 14 (see D-08/09) and carried in deferred-items.md. Ships later inside a dedicated pragma / feature-flag phase
- Pragma / `enable "<addon>"` language construct — its own phase; touches lexer + parser + evaluation envelope
- DX-07 `reverbTime` context block — Phase 15
- DX-09 euclidean humanize — Phase 15
- QOL-03 tutorial refresh — Phase 16
- Micro-timing humanize field on `MusicalNoteData` — v1.3 per REQUIREMENTS.md "Future Requirements"
- Any modification to `augment`/`diminish` semantics — dismissed in Phase 11, not revisited here
- New NuGet packages — DryWetMidi read-API is already available via the existing Melanchall.DryWetMidi 8.0.3 dependency

</domain>

<decisions>
## Implementation Decisions

### DX-05 — slice (Sequence + Array[T])

- **D-01:** `slice(start, end)` is **silently clamping on both sides** for both overloads. Negatives clamp to 0. `end > count` clamps to `count`. `start >= end` (after clamping) returns an empty Sequence / empty Array. No errors raised for bounds — matches the spirit of `take`/`drop` at `Collections.cs:117-147`.
- **D-02:** Both overloads (`slice(Sequence, Int, Int)` and `slice(Array[T], Int, Int)`) ship in **one atomic commit** inside plan 14-01. Same semantics, same plan, same regression test.

### DX-06 (reduced) — flat literals + enharmonic()

- **D-03:** `enharmonic(Note) → Note` is **key-context-aware**. The built-in reads the active `MusicalContext.Key` via `ExecutionContext.GetMusicalContext()`, matching the existing precedent at `SongFunctions.AddSequenceToSong` and `StdLib.Rand`. The built-in signature is `(IReadOnlyList<Value> args, ExecutionContext context)`.
- **D-04:** **In-key rule:** if the input pitch is diatonic to the active key, return the scale-diatonic spelling. If the pitch is chromatic (not in the scale), fall back to the no-key rule (D-05). Uses `ScaleDatabase` for scale-tone lookup.
- **D-05:** **No-key / Cmaj / Amin fallback:** flip sharp↔flat (`Db4` ↔ `C#4`, `F#3` ↔ `Gb3`). Natural notes (alteration = 0) are returned **unchanged** — no spurious `E → Fb`, `F → E#`, `B → Cb`, `C → B#` respelling.
- **D-06:** `enharmonic()` lives in `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs`. The file already touches scale context (roman numerals, chord resolution) and already consumes `MusicalContext`. No new file.

#### Flat-literal surface — extended alteration encoding

- **D-07:** `NoteType.Parse` accepts **arbitrary composition** of `b`/`#`/`+`/`-` attached to the note letter, on either side of the octave digits. Net alteration = (count of sharps: `#` + `+`) − (count of flats: `b` + `-`). Examples:
  - `Db4` → `(D, 4, -1)`
  - `Bb` → `(B, 4, -1)` (default octave 4)
  - `C#5` → `(C, 5, +1)`
  - `Bb-+bbb` → `(B, 4, -4)` (user illustration — net of flats and sharps summed across any mix)
  - `F##4` → `(F, 4, +2)`
- **D-08:** Alteration encoding is extended **past the current ±2 range**. `MusicalNoteData.Alteration` is already `int` and does not change. `NoteType.Format(note, octave, alteration)` is extended to emit a canonical string form for any `int` alteration — canonical form uses the existing `+`/`-` run convention (so round-trip via `Format(Parse(x))` normalizes but does not lose pitch information). The `b`/`#` variants are accepted on parse but not emitted on format.
- **D-09:** Range validation uses the **post-alteration MIDI value**, not the letter+octave alone. `Cb4` → MIDI 59 (B3) = in range. `Cb0` → MIDI 11 = below E0 (MIDI 16) → error: `"Note Cb0 is out of valid range (E0 to E10)"`, matching the existing error format at `NoteType.cs:69`.

#### H-alias scope change

- **D-10:** **The `H` = `B` alias clause is dropped from Phase 14 DX-06.** REQUIREMENTS.md DX-06 will be reframed in plan 14-04 to cover only the flat-literal family + `enharmonic()`. Original wording preserved as audit-trail (Phase 12 TEST-03 reframe precedent).
- **D-11:** The `H` alias ships in a **future phase bundled with a new pragma / feature-flag language construct**. Preferred keyword direction (non-binding; the future phase will finalize): `enable "german-notation"` at the top of a file or as a block form. Rationale: adding niche locale-specific grammar as a global alias pollutes the namespace and changes the meaning of `Int H = 5`; a pragma scopes the change to opt-in files/blocks.
- **D-12:** `deferred-items.md` in `.planning/phases/14-composer-dx-part-1/` captures: (a) H alias requirement, (b) pragma system design, (c) German-notation as first pragma user, (d) candidate keyword `enable`. Created in plan 14-04.

### DX-08 — MIDI velocity end-to-end

- **D-13:** The velocity chain appears already wired end-to-end as of Phase 12 close: `MusicalContextType.Dynamics` assigns `MusicalContextData.Velocity` at `Interpreter.cs:184-191`; `NoteStreamCompiler.cs:341` reads `context.Velocity ?? 0.63` and stores per-note; `MidiExport.cs:191-192` maps `note.Velocity * 127` clamped to 1–127. Plan 14-03 uses **two-pass strict authorship** (Phase 13 D-13): Pass 1 drafts the regression test from REQUIREMENTS.md DX-08 wording alone; Pass 2 lands the test against real code. If Pass 2 lands GREEN, no plumbing required. If RED, minimal gap-fix lives in the same plan with a Divergence entry.
- **D-14:** Regression script is **new purpose-built** `tests/test_dynamics_midi_velocity.flow` — a small deterministic `.flow` that exports MIDI via `writeMidi` and hits each dynamic construct (`dynamics f`, `crescendo`, `decrescendo`, `swell`). The xUnit Fact reads the file back via DryWetMidi's `MidiFile.Read(path)`, walks note-on events, and asserts the velocity byte sequence against the known-good gradient.
- **D-15:** Fact lives in `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` (directory convention from Phase 13 D-09). MIDI-read helper is inline inside the Fact; promoted to a shared helper only if a second Fact needs the same read path.

### Plan structure (4 plans, wave-parallel where possible)

- **D-16:** **14-01** — DX-05 `slice(Sequence, Int, Int)` + `slice(Array[T], Int, Int)` atomic, plus one `.flow` regression test covering both overloads + clamp edges. One commit.
- **D-17:** **14-02** — DX-06 reduced scope: flat-literal surface (`NoteType.Parse` extension + `Format` extension + post-alteration range check) + `enharmonic()` built-in in `HarmonyFunctions.cs`. Two commits: (a) NoteType changes + unit Facts, (b) `enharmonic()` registration + unit Facts + key-context integration Fact. Pre-landing collision grep (ROADMAP criterion 5) performed once in this plan, transcript pasted into 14-02-PLAN.md (D-19).
- **D-18:** **14-03** — DX-08 two-pass strict. `tests/test_dynamics_midi_velocity.flow` + `DynamicsMidiVelocityTests` Fact. One commit on GREEN path; two commits (Divergence + gap-fix) if Pass 2 finds a gap.
- **D-19:** **14-04** — REQUIREMENTS.md reframe (DX-06 H-alias clause moved to audit-trail, flat+enharmonic clauses kept) + deferred-items.md for H/pragma + 14-VERIFICATION.md rollup with FIX-* / DX-* commit hashes + REQUIREMENTS.md traceability table rows marked. One commit.
- **D-20:** **Wave 1 parallel:** 14-01, 14-02, 14-03 touch independent files (`Collections.cs` / `BuiltInFunctions.cs` slice registration · `NoteType.cs` + `HarmonyFunctions.cs` · `tests/` + new Fact file). Zero file overlap. Phase 12 Wave 2 pattern reused. **14-04 is strictly last** — depends on all prior commits existing.

### Collision grep enforcement (ROADMAP criterion 5)

- **D-21:** Pre-landing collision grep is **one-shot at plan time**, not an ongoing test. Recipe:
  ```
  grep -rn '\b(Db|Eb|Fb|Gb|Ab|Bb|Cb|enharmonic)\b' flow-lang/*.flow examples/ tests/ --include='*.flow'
  ```
  plus equivalent for `\bH\b` noted for historical reasons even though H is deferred. Transcript (expected empty) pasted into 14-02-PLAN.md §Pre-landing Collision Grep and re-surfaced in 14-VERIFICATION.md. Matches Phase 11 `AUDIT-VERIFIED` one-shot convention and Phase 12 D-18 bisectability discipline. No xUnit Fact — the feature-once audit is sufficient.

### Empirical findings carried in from prior phases

- **F-01:** DX-08 velocity chain is **assumed wired per STATE.md blocker** but not yet verified by a regression test. Phase 14 is the first time the chain is exercised end-to-end by an automated assertion. Pass 2 in plan 14-03 will confirm or log a Divergence. No gap-fix work is pre-committed.
- **F-02:** The ROADMAP DX-06 wording (flats normalize to existing `±2` alteration triples) is **under-scoped** relative to the user's vision (arbitrary `b`/`#`/`+`/`-` composition with any integer net alteration). Plan 14-04 documents this as a scope expansion Divergence — not a bug fix, not a scope cut, but an intentional vision clarification captured at discuss time. The encoding already supports it (`Alteration` is `int`); only `Parse` and `Format` need extending.
- **F-03:** DX-06 **H-alias defers**, not drops. Scope creep redirect — user chose to redesign around a pragma system rather than smuggle in a global alias.

### Claude's Discretion

- Exact xUnit Fact naming for new regression tests (e.g., `DynamicsMidiVelocityTests.Forte_Emits127` vs `VelocityBytes_MatchGradient`)
- Whether the inline MIDI-read helper in plan 14-03 gets promoted to `flow-lang.Tests/Shared/MidiReadHelpers.cs` (defer decision to Pass 2 based on duplication)
- Internal representation of the extended-range alteration on `Format` — canonical output style (e.g., `B+++` vs `B^3`) so long as `Parse(Format(x)) == x` round-trips
- Exact error message text for Cb0-style range-overshoot — must match existing Head/Last format
- Whether `slice` uses LINQ `Skip(start).Take(end - start)` or an explicit pre-sized list allocation
- Whether to add a Phase 14 `14-VALIDATION.md` at `nyquist_compliant: true` during 14-04 (Phase 13 D-24 precedent: minimal VALIDATION.md for pure-docs phases; Phase 14 ships code but follows the same observable-value pin rule — likely yes, but owner's call at plan time)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone planning
- `.planning/ROADMAP.md` §"Phase 14: Composer DX Part 1" — 5 success criteria (esp. #1 slice semantics, #2 enharmonic flats with H-clause deferred, #3 enharmonic() signature, #4 MIDI velocity byte regression, #5 pre-landing collision grep)
- `.planning/REQUIREMENTS.md` §"Composer DX — Tier A" — DX-05 / DX-06 / DX-08 wording; DX-06 H clause is reframed in plan 14-04
- `.planning/PROJECT.md` §Key Decisions — `IsCompatibleWith widening`, `Path-first arg convention`, `Bar-midpoint BPM interpolation` decisions relevant to overload design

### Prior CONTEXT decisions that carry forward
- `.planning/phases/11-audit-spike/11-CONTEXT.md` D-02 — `// AUDIT-VERIFIED YYYY-MM-DD:` marker convention (Phase 14 does NOT add markers; no AUDIT fixes in this phase)
- `.planning/phases/12-stability/12-CONTEXT.md` D-18 — atomic commits per fix for bisectability
- `.planning/phases/12-stability/12-CONTEXT.md` D-01 — REQUIREMENTS reframe audit-trail preservation pattern (Phase 14 reuses in plan 14-04 for DX-06 H-clause)
- `.planning/phases/13-nyquist-validation-backfill/13-CONTEXT.md` D-06 — Wave-1 parallel plan structure when plans touch independent files
- `.planning/phases/13-nyquist-validation-backfill/13-CONTEXT.md` D-09, D-11, D-12 — xUnit Fact directory convention (`Integration/Phase{NN}/`), observable-value pins (error text or numeric counts; forbid buffer byte hashes), no new NuGet packages
- `.planning/phases/13-nyquist-validation-backfill/13-CONTEXT.md` D-13, D-14, D-15 — two-pass strict authorship + Divergence logging (reused directly by plan 14-03 for DX-08)

### Code targets
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` — `Parse` (lines 21-73), `Format` (lines 142-155), `IsValidNoteRange` (lines 78-88), `GetNoteValue` (lines 93-108). Extension for arbitrary `b`/`#`/`+`/`-` composition + post-alteration range check.
- `flow-lang/Lexing/SimpleLexer.cs` — `TryParseNote` (lines 669-701), `ScanIdentifierOrKeyword` (lines 526-609). Confirm `Db4`, `Bb`, `F##4` tokenize as single identifier candidates and survive the `TryParseNote` round-trip.
- `flow-lang/Runtime/NoteStreamCompiler.cs` — 13 call sites calling `NoteType.Parse`; extended surface must flow through unchanged (pure pitch+alteration ints, no string-level handling of `b`/`#`).
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` — `enharmonic()` lives here; existing `resolveNumeral` uses the same key-context-aware pattern at lines 99-130. `ScaleDatabase` is the in-scale lookup.
- `flow-lang/StandardLibrary/Collections.cs:117-147` — `Take` / `Drop` implementations; `slice` is the same shape (LINQ-based, clamping on `count`).
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:369-373` — `take`/`drop` registration site; `slice` registrations for both overloads land nearby.
- `flow-lang/StandardLibrary/Audio/MidiExport.cs:191-199` — velocity byte emission; DX-08 regression Fact reads the output back via `MidiFile.Read` from Melanchall.DryWetMidi 8.0.3.
- `flow-lang/Runtime/MusicalContext.cs` — `MusicalContextData` + `GetMusicalContext` (referenced via `ExecutionContext`); `Velocity` and `Key` fields consumed by DX-06 and DX-08.

### Test infrastructure (Phase 12 + 13)
- `flow-lang.Tests/flow-lang.Tests.csproj` — target for new Facts; no csproj edits expected (DryWetMidi + xunit already referenced)
- `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` — stdout/stderr capture fixture; regression `.flow` scripts use this
- `flow-lang.Tests/FlowScriptData.cs` — Theory row catalog; `tests/test_dynamics_midi_velocity.flow` gets a row here as part of plan 14-03
- `flow-lang.Tests/Unit/InterpreterTests.cs` — example pattern for Unit Facts that need `use "@std"` prelude (Phase 12 Plan 04 deviation)

### Existing regression tests to leave untouched
- `tests/test_dynamics.flow` — stdout-assertion coverage for dynamics; Phase 14 **does not extend this file** (Phase 13 D-21: MAY add tests but MAY NOT modify existing). New MIDI-byte Fact lives in a new `.flow` file per D-14.
- `tests/test_crescendo.flow` — existing sentinel coverage; untouched.

### Template references
- `~/.claude/get-shit-done/templates/VALIDATION.md` — canonical VALIDATION.md schema if plan 14-04 decides to ship `14-VALIDATION.md` (Claude's Discretion per last bullet of §Claude's Discretion)
- `.planning/phases/13-nyquist-validation-backfill/13-VERIFICATION.md` — format reference for `## Divergences` logging in 14-03 if DX-08 goes Pass 2 RED

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`IReadOnlyList<Value> args, ExecutionContext context` signature** (`StdLib.Rand`, `SongFunctions.AddSequenceToSong`, `Collections.Each`/`Map`/`Filter`/`Reduce`) — direct pattern for `enharmonic()` to access `MusicalContext.Key`
- **LINQ `Skip`/`Take`** (`Collections.cs:127-146`) — direct template for `slice` (`Skip(start).Take(end - start)`); clamping of `count` to `0` for negatives is identical
- **DryWetMidi 8.0.3 read-API** (`MidiFile.Read(path)` + `GetNotes()` / `GetTrackChunks()`) — already referenced in `flow-lang.csproj` for write; read path available at zero cost
- **`MusicalContextData.Velocity`** propagation through `NoteStreamCompiler.cs:341` — DX-08's chain is wired; DX-08 plan verifies rather than builds
- **`ScaleDatabase`** — lookup-by-key scale tones; input to D-04 in-key respelling rule
- **`NoteType.GetNoteValue` / `ToMidiNote` / `FromMidiNote`** (lines 93-137) — already compute MIDI values with arbitrary alteration; D-09 post-alteration range check reuses these without modification

### Established Patterns
- **Atomic commits per fix/feature** (ROADMAP criterion 3 across phases) — every DX-* lands bisectable
- **Two-pass strict authorship** (Phase 13 D-13) — applied to DX-08 velocity verification; Pass 1 drafts from REQUIREMENTS alone, Pass 2 reconciles with real code
- **Wave-1 parallel where file-overlap is zero** (Phase 12 / Phase 13 D-06) — 14-01/02/03 run in parallel, 14-04 lands strictly last
- **REQUIREMENTS.md reframe with audit-trail preservation** (Phase 12 TEST-03) — DX-06 H-clause reframe in 14-04 follows the same shape
- **One-shot audit markers, not ongoing test Facts** (Phase 11 D-02) — applied to the pre-landing collision grep in plan 14-02
- **No new NuGet packages** (CLAUDE.md + Phase 13 D-12) — DryWetMidi read via existing dep; no FFT or additional libraries

### Integration Points
- **`BuiltInFunctions.cs` ` Register*` call sites:** slice (near line 369), enharmonic (near HarmonyFunctions registration, line ~1000). No csproj changes.
- **`flow-lang.Tests.csproj`:** new Fact files auto-included via net10.0 compile glob; no edits
- **`flow-lang.sln`:** no new projects
- **`FlowScriptData.cs`:** add a Theory row for `tests/test_dynamics_midi_velocity.flow` in plan 14-03 (additive, non-breaking)
- **`NoteType.cs` blast radius:** 13 call sites of `NoteType.Parse` across the codebase (see `grep -n NoteType.Parse flow-lang/`). D-07/D-08/D-09 extensions must preserve existing behavior on all existing spellings — no test Theory row should flip RED
- **`NoteStreamCompiler.cs`:** 647-line file, touched heavily. D-07 extension is pure upstream (at `Parse`); compiler consumes the triple unchanged. No compiler edits expected for DX-06.
- **`HarmonyFunctions.cs`:** new `enharmonic` registration + impl. `ScaleDatabase` access is read-only; no new state.

### Known risk surface
- **`NoteType.Format` round-trip:** D-08 extends `Format` to emit canonical runs for any `int` alteration. If an existing consumer of `Format(...)` assumes output is one of `""` / `+` / `++` / `-` / `--`, it may misparse extended output. Planner should grep `NoteType.Format` call sites before landing extension. Current usage: `NoteType.cs:236` in `MusicalNoteData.ToString()` — benign (ToString, for display).
- **Lexer ambiguity for `Bb4`:** Current identifier scanner at `SimpleLexer.cs:526-565` consumes letters/digits continuously; `Bb4` becomes one `Bb4` identifier. `TryParseNote` will be updated to recognize flats. Verify no conflict with `b4` as an existing variable name in any test — the pre-landing collision grep (D-21) catches this.
- **Range check regression:** Existing `IsValidNoteRange` uses letter+octave only. D-09 shifts to post-alteration MIDI. Existing in-range spellings that stay in-range under both rules are unaffected; tests around E0 and E10 boundaries need review.

</code_context>

<specifics>
## Specific Ideas

- **The user's vision for DX-06 is intentionally larger than the ROADMAP wording.** `Bb-+bbb` composes flats and sharps as a running tally — this is a creative feature, not a bug fix, and the alteration encoding extension serves it. The planner should treat this as a first-class design choice and not trim it back to "normalize to existing ±2 triples".
- **`enable` keyword is the preferred pragma name direction**, but it's not binding — the future phase that ships the pragma system will finalize. The direction matters because it shapes how Phase 14's deferred-items.md is written: "pragma system with opt-in keyword, candidate `enable`" rather than an unnamed concept.
- **DX-08 is almost certainly verification-only.** Phase 13 D-04 landed a zero-Divergence outcome when v1.1 audit + Phase 12 stability had already reconciled requirements vs reality. The same conditions hold for DX-08: the velocity chain is plumbed, just uncovered by an automated assertion. Expect plan 14-03 to land GREEN on Pass 2 with no gap-fix work.
- **The collision grep is cheaper to run than to design around.** Phase 12 / 13 used one-shot audit markers precisely because the cost of running a grep during plan-time is ~10 seconds and the future cost of a new xUnit Fact gating every test run is non-trivial. Phase 14 follows the same reasoning.
- **`slice` is a 20-line patch in total.** LINQ does the work; the only novelty is two registrations in `BuiltInFunctions.cs` and one .flow regression test. Atomic single-plan delivery is proportional to the work.
- **Phase 13's "existing coverage wins"** (D-19) does NOT apply to DX-08 — the existing `tests/test_dynamics.flow` pins stdout, not MIDI bytes. A new regression Fact is necessary for observable-value pinning of the velocity output.
- **The H-alias deferral is not a punt.** It's a deliberate redesign to avoid locking in a namespace-polluting alias before the pragma/feature-flag mechanism exists. The future phase gets to design the pragma system without the pressure of a composer-DX milestone around it.

</specifics>

<deferred>
## Deferred Ideas

- **`H` = `B` note-stream alias (from original DX-06 clause).** Dropped from Phase 14 scope per D-10. Ships in a future phase bundled with the pragma system below. Full family (`H`, `H4`, `H+`, `H++`, `H4+`) intended. Rationale: user's vision is that niche syntactic variants should be opt-in via a pragma keyword, not a global alias.
- **Pragma / feature-flag language construct.** Candidate keyword direction: `enable "<addon-name>"` at top-of-file or `enable "<addon-name>" { ... }` block form. First user: German notation addon (`H`-alias). Design questions for the future phase: file-scoped vs block-scoped, layered/stackable, interaction with `use` imports, whether the pragma-aware tokens also propagate into chord literals / key blocks.
- **Multi-letter enharmonic-edge respelling** (`E ↔ Fb`, `F ↔ E#`, `B ↔ Cb`, `C ↔ B#`). Intentionally excluded per D-05 — naturals round-trip unchanged under the fallback rule. If a future user need surfaces, a separate `enharmonicWithEdges()` built-in or an `enharmonic(note, strict: true)` variant can carry it without disturbing the baseline.
- **`NoteType.Format` canonical output redesign.** D-08 expands format to emit runs for any `int` alteration, but the exact emission style (`B+++` vs `B^3` vs `B#3` vs a new `Note.toString`) is Claude's discretion at plan time. A future readability-pass could revisit.
- **Promoting DX-08 MIDI byte read helper to a shared fixture** (`Shared/MidiReadHelpers.cs`). Deferred to Pass 2 based on whether another Fact duplicates the same call shape. Phase 15 DX-09 (euclidean humanize) will likely need the same helper for reproducibility assertions — natural promotion candidate.
- **Adding `14-VALIDATION.md` at `nyquist_compliant: true`** as part of plan 14-04. Not required by ROADMAP; Phase 13 D-24 set the precedent for minimal VALIDATION.md on pure-docs phases, and a code phase can benefit similarly. Claude's discretion at 14-04 plan time.
- **`slice` negative-from-end indexing** (Pythonic `slice(seq, -2, -1)`). Not shipped — D-01 chose simple two-sided clamping. If user surfaces a need, add a separate overload or a `reverseSlice` rather than overload existing semantics.

</deferred>

---

*Phase: 14-composer-dx-part-1*
*Context gathered: 2026-04-20*
