# Phase 23: Microtonal Tuning (Wedge) — Context

**Gathered:** 2026-05-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Three named tunings (`enable justIntonation;`, `enable pythagorean;`, `enable equalTemperament;`) ship as render-time pragmas that override `PitchConversion.NoteToFrequency`. Score-level transforms remain pitch-class agnostic per MICR-02. Pragma is file-scope per Phase 21 D-02; activation infrastructure is reused from Phase 21.

**In scope:**
- Register `justIntonation`, `pythagorean`, `equalTemperament` in `flow-lang/Lexing/PragmaRegistry.cs` (Phase 21 D-17 reserved their addition for this phase).
- Add a `Tuning` property on `flow-lang/Runtime/MusicalContext.cs` (file-set, NOT stacked).
- New `flow-lang/StandardLibrary/Audio/Tuning/` (or sibling) module containing the chromatic-ratio tables + frequency math for the three tuning systems.
- Bridge pragma → `MusicalContext.Tuning` in `flow-lang/Core/FlowEngine.cs` at entry-point `Run()`.
- Modify `flow-lang/StandardLibrary/Audio/PitchConversion.cs` so `NoteToFrequency` consults active tuning.
- Extend `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs` `ParseKeyName` to recognize church mode suffixes (`dorian`, `phrygian`, `lydian`, `mixolydian`, `locrian`) so the active mode can pick the right ratio table.
- Add `enharmonic()` one-time-per-session warning when called inside non-12-TET (Pitfall 5 #3 + AUDIT-VERIFIED marker).
- Add `writeMidi` one-time-per-session warning when called under non-12-TET tuning.
- Acceptance: MICR-01 + MICR-02 + MICR-03 from REQUIREMENTS.md.

**Out of scope (deferred or other phases):**
- Full Scala (`.scl`) loader — REQUIREMENTS.md D-03 defers to v1.4.
- Faithful microtonal MIDI export (per-channel pitch-bend events per Pitfall 5 #2) — deferred to v1.4. Phase 23 emits a one-time stderr warning when `writeMidi` is called under non-12-TET.
- Spelling-preserving transforms (`transposePreserveSpelling` etc.) — Phase 23 keeps transforms MIDI-based per MICR-02; the silent enharmonic-respelling caveat is documented.
- Block-scope `tuning { }` syntax — Phase 21 D-02 defers all block-scope pragmas.
- Configurable A4 reference frequency — A4 = 440 Hz is reused from existing `PitchConversion`.

</domain>

<decisions>
## Implementation Decisions

### JI / Pythagorean Tonic Resolution
- **D-01:** When `enable justIntonation;` or `enable pythagorean;` is active, the 1/1 reference pitch is read from the innermost active `MusicalContext.Key` (set by `key Cmajor { ... }` etc.). Symmetric with how `transpose`/`HarmonyFunctions` already consult `MusicalContext.Current.Key`. Innermost-key-wins matches the documented Phase 24 LINT-03 nested-key semantics.
- **D-02:** If a non-12-TET pragma is active but no `key` block is in scope, the renderer **silently** roots at C major (tonic = C, mode = major). Documented in `enable justIntonation;` / `enable pythagorean;` reference + the function doc comment on `PitchConversion.NoteToFrequency`. Aligns with `feedback_charitable_interpretation` memory and mirrors the Phase 22 D-07 voicing-fallback pattern.
- **D-03:** Mode SHIFTS the chromatic ratio table — natural minor uses 6/5 minor third, dorian uses 9/8 second + 6/5 third + 9/8 sixth, etc. Phase 23 ships seven mode-specific JI tables AND seven mode-specific Pythagorean tables: major + natural minor + dorian + phrygian + lydian + mixolydian + locrian.
- **D-04:** Phase 23 extends `ScaleDatabase.ParseKeyName` to recognize the five church-mode suffixes (`dorian`, `phrygian`, `lydian`, `mixolydian`, `locrian`) alongside the existing `major`/`minor`. Each parses to a `(root, mode)` tuple consumed both by tuning-table lookup AND by future scale-aware tooling (Phase 24 `scaleLint`). Future modes (e.g., harmonic minor, melodic minor) are out of scope for this phase.

### Pragma → Renderer Plumbing
- **D-05:** Active tuning lives on `MusicalContext.Tuning` as a top-level (NOT Push/Pop) property. Synthesizers read `MusicalContext.Current.Tuning` symmetrically with how they already consult `Key`/`Tempo`. Aligns with Pitfall 5 #4. The pragma is file-scope so a stack-scoped property would be over-engineered for what Phase 21 actually delivers.
- **D-06:** `FlowEngine.Run()` reads the entry-point `Program.Pragmas`, resolves to a tuning value, and sets `MusicalContext.Tuning` once before interpretation begins. `ModuleLoader.cs` does NOT touch tuning state. Imported modules render in the caller's tuning. Matches Phase 21 D-06 (parse-time isolation) AND CLAUDE.md "imports execute in caller's context" (runtime-time inheritance). Outer file decides tuning.
- **D-07:** REPL: pragma extraction stays per-line per Phase 21 D-07, but the **resolved** `MusicalContext.Tuning` PERSISTS across REPL lines until another tuning pragma replaces it or the REPL session ends. This is a documented departure from the strict pragma scope semantics — necessary so interactive composition under JI is usable. The departure must appear in the pragma reference doc + the REPL `--help`.
- **D-08:** Default tuning when no `enable` pragma is declared is `equalTemperament` (12-TET). When a file declares `enable equalTemperament;` explicitly, the pragma is functionally a no-op (same numeric output as no declaration) but it IS registered + visible to tooling per MICR-01. Used downstream by `enable scaleLint;` (Phase 24) to know the user's tuning intent.

### Cent Offset & Spelling Under Non-12-TET
- **D-09:** Spelling-aware tuning tables: `Eb4` and `D#4` produce **different** rendered frequencies under JI/Pythagorean (Eb4 → 6/5 ratio in 5-limit JI; D#4 → 75/64 ratio). The chromatic ratio table keys on `(note name, alteration)`, not on semitone offset from tonic. Honors Pitfall 5 #3 ("in JI, F♭ and E are different pitches") and the whole point of declaring JI.
- **D-10:** Cent offsets compose **additively in cent-space**: `freq = tonic_hz × ratio × 2^(cents/1200)`. Composer can write `E4+5c` to fine-tune the JI third; cents always do the same thing they did in 12-TET. Charitable: cents never silently disappear.
- **D-11:** `enharmonic()` emits a **one-time-per-session stderr warning** when called inside non-12-TET tuning: `[enharmonic] called inside tuning != equalTemperament; conversion is destructive (≈ 21 cent shift)`. Conversion still happens (existing behavior preserved). Matches Pitfall 5 #3 + AUDIT-VERIFIED marker. Documented exception to the charitable-interpretation memory because the regression is silent and audible.
- **D-12:** Transforms (`transpose`, `invert`, `retrograde`, `augment`, `diminish`) stay MIDI-based per MICR-02. The MIDI-pitch-number invariant is preserved across tunings. When `FromMidi` produces a different spelling than the input, the renderer uses key-aware spelling via `HarmonyFunctions.GetInKeyEnharmonic` (already plumbed in Phase 14) so spellings stay diatonic when a key is active. The silent-respelling case (~21 cent shift at enharmonic junctions under non-12-TET) is **documented in the Phase 23 doc** as a known caveat with a future `transposePreserveSpelling` strict-mode escape hatch noted as a v1.4 candidate.

### MIDI Export Tuning Awareness
- **D-13:** Phase 23 scope = synthesizer + audio render path only. MIDI export stays 12-TET (existing behavior unchanged). When `writeMidi` is called and `MusicalContext.Tuning != EqualTemperament`, emit a one-time stderr warning per session: `[midi] tuning != equalTemperament; MIDI export emits 12-TET pitches without pitch-bend (faithful microtonal MIDI deferred to v1.4)`. Smallest blast radius; unblocks Phase 23 ship without `flow-midi/Midi/` per-channel allocation work.

### Unknown Tuning Names (MICR-03)
- **D-14:** Unknown tuning names trip the existing Phase 21 D-12 unknown-pragma error path (Levenshtein did-you-mean + alphabetized known list) — `PragmaRegistry` is closed-set per Phase 21 D-17. Phase 23 extends the error message in the tuning-pragma case to add a final line: `Full Scala (.scl) loader is documented as deferred to v1.4 — see ADR/REQUIREMENTS.md D-03.` Satisfies MICR-03 acceptance ("clear error pointing at the documented v1.4 Scala-loader expansion") without forking the unknown-pragma path.

### Claude's Discretion
- Type shape of `MusicalContext.Tuning` (closed enum vs `ITuning` interface vs sealed-record) — planner decides. Closed-enum matches Phase 21 D-17 house style + CLAUDE.md "closed-enum / closed-set design"; `ITuning` interface future-proofs for v1.4 Scala-loader extensibility. Recommendation: closed enum + `static class TuningTables` keyed by `(TuningSystem, Mode)` — defer interface refactor to v1.4 when there's a real second extensibility point.
- File layout under `flow-lang/StandardLibrary/Audio/Tuning/` — single file vs split (`TuningSystem.cs` + `JustIntonationTable.cs` + `PythagoreanTable.cs` + `RatioMath.cs`) — planner decides based on table size.
- Exact ratio values in the chromatic tables (5-limit JI commonly cites a few competing tables — e.g., `9/8` vs `10/9` for the major second; `45/32` vs `64/45` for the tritone). Planner picks ONE canonical 5-limit table and ONE canonical 3-limit Pythagorean table and pins them with citations. Recommendation: use the standard 5-limit table from Helmholtz/Ellis (canonical reference) for JI; standard chain-of-fifths Pythagorean from C tonic.
- Warning channel for D-11 / D-13 — `Console.Error.WriteLine` (existing pattern in `TransformFunctions.TransposeSemitone`) vs `ErrorReporter` vs new `DiagnosticChannel`. Recommendation: `Console.Error.WriteLine` to match the existing `transpose` warning style; one-shot guard via a per-session HashSet on `MusicalContext` or a static `RenderingDiagnostics` helper.
- Test placement: `tests/test_tuning_ji.flow` + `tests/test_tuning_pythagorean.flow` + xUnit `TuningFacts` for ratio math; OR a single combined `test_tuning.flow` + `TuningFacts`. Planner decides.
- Determinism gate: whether `tutorial.flow` / `showcase.flow` need any tuning-pragma additions to extend the byte-identical regression contract to JI/Pythagorean paths. Recommendation: NO — keep tutorial/showcase 12-TET to preserve the v1.2 byte-identical pin; add a separate `tests/test_tuning_determinism.flow` that pins the JI/Pythagorean paths independently.

### Folded Todos
None — no pending todos matched Phase 23 scope at the time of discussion.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 23 Locked Requirements
- `.planning/REQUIREMENTS.md` lines 73–75 — MICR-01, MICR-02, MICR-03 acceptance criteria (canonical contract).
- `.planning/REQUIREMENTS.md` line 14 — D-03 locks the named-tunings wedge scope; full Scala loader deferred to v1.4.
- `.planning/REQUIREMENTS.md` line 103 — Scala loader explicit-defer note used by D-14 unknown-tuning error message.
- `.planning/ROADMAP.md` § "Phase 23: Microtonal Tuning (Wedge)" — 4 success criteria + dependency on Phase 21 pragma system.

### Pitfalls and Constraints
- `.planning/research/PITFALLS.md` § "Pitfall 5: Microtonal tuning state vs. transpose/invert/retrograde transforms" (lines 135–162) — the foundational constraint document for this phase. D-05/D-09/D-11/D-12/D-13 all map to specific Pitfall 5 mitigation points.
- `.planning/research/PITFALLS.md` line 162 — AUDIT-VERIFIED marker text for the `enharmonic()` warning (D-11).

### Prior-Phase Decisions This Phase Builds On
- `.planning/phases/21-pragma-system-h-alias/21-CONTEXT.md` — Phase 21 D-02 (file-scope), D-06 (per-module PragmaSet), D-07 (REPL fresh-per-line, **D-07 modified for tuning by Phase 23 D-07**), D-12 (unknown-pragma error path, extended by D-14), D-17 (closed-set PragmaRegistry, extended by D-08).
- `.planning/phases/20-cheap-defer-closures-multi-letter-enharmonic-edges/20-VERIFICATION.md` — DEFER-04 multi-letter enharmonic edges (`B# ↔ C`, `Cb ↔ B`) shipped; spelling-aware JI inherits this when chromatic alterations resolve through `HarmonyFunctions.Enharmonic`.
- `.planning/phases/14-composer-dx-part-1/` — Phase 14 plumbed `HarmonyFunctions.GetInKeyEnharmonic` for diatonic-spelling preservation. D-12 reuses this so transposed notes stay diatonic when a key is active.

### Existing Code This Phase Touches
- `flow-lang/StandardLibrary/Audio/PitchConversion.cs:13,22,34` — `NoteToFrequency` (modified to consult active tuning) and `GetMidiNote` (unchanged, still the MIDI-number authority for transforms).
- `flow-lang/Runtime/MusicalContext.cs` — gains `Tuning` top-level property per D-05.
- `flow-lang/Core/FlowEngine.cs:51` — gains pragma → `MusicalContext.Tuning` bridge per D-06.
- `flow-lang/Lexing/PragmaRegistry.cs` — registers `justIntonation`, `pythagorean`, `equalTemperament` per D-08.
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:25,65,106,147` and all `flow-lang/StandardLibrary/Audio/Synthesizers/*.cs` (Piano, Brass, Sax, Strings, Flute, Organ, Drums) — all currently call `PitchConversion.NoteToFrequency(note)`; no change to call sites since `NoteToFrequency` itself becomes tuning-aware via `MusicalContext.Current.Tuning`.
- `flow-lang/StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs:59` — same as above.
- `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:152,164,169,196` — `ParseKeyName` extended for 5 church mode suffixes per D-04.
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:88,95` — `GetInKeyEnharmonic` reused by D-12 for diatonic-spelling preservation in transformed notes.
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:262,289` — `TransposeSemitone` / `TransposeCent` unchanged per MICR-02 / D-12; doc comment updated to call out the spelling-respelling caveat.
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — already routes notes through synthesizers; tuning flows through transparently.
- `flow-midi/Midi/` (existing MIDI export pipeline) — gains the one-time `writeMidi` non-12-TET warning per D-13. No pitch-bend infrastructure in this phase.

### Project Memory (CLAUDE.md auto-memory)
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_charitable_interpretation.md` — informs D-02 (silent C-default for no-key code), D-10 (cent additivity always defined), and the structure of the documented exceptions D-11/D-13.
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_language_philosophy.md` — informs the no-arg pragma syntax shape (no infix, S-expr-aligned).

### Test Patterns to Follow
- `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` — recent xUnit patterns (uses `FlowScriptData.FindTestsRoot()` for cwd portability).
- `flow-lang.Tests/Unit/Phase18/` — Fraction Facts (template for `TuningFacts` ratio math).
- `tests/test_*.flow` — `.flow` script integration loop (each tuning ships at least one `tests/test_tuning_*.flow` smoke script).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`MusicalContext` stack** (`flow-lang/Runtime/MusicalContext.cs`) — already carries tempo/timesig/key/swing. Adding `Tuning` as a non-stacked property fits cleanly per D-05; renderer-side reads use the existing `MusicalContext.Current` accessor.
- **`PitchConversion.NoteToFrequency`** (`flow-lang/StandardLibrary/Audio/PitchConversion.cs:22`) — single chokepoint for note→Hz translation across every synthesizer and the vocalization path. Modifying it once routes all renderers through the new tuning-aware path with zero call-site churn.
- **`HarmonyFunctions.GetInKeyEnharmonic`** (`flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:88`) — already preserves diatonic spelling against active key. Reused by D-12 to keep `transpose` round-trip spelling diatonic when a key is active.
- **`HarmonyFunctions.Enharmonic`** (Phase 14/20) — multi-letter enharmonic edge resolver. JI's `B#` / `Cb` cases route through this canonically.
- **`ScaleDatabase.ParseKeyName`** (`flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:152`) — clean extension point for D-04's church-mode suffix recognition; the existing `EndsWith("major")` / `EndsWith("minor")` pattern generalizes.
- **`Console.Error.WriteLine` warning style** — `TransformFunctions.TransposeSemitone:276` is the existing template for one-shot composer-facing diagnostic warnings. D-11 / D-13 follow this style.
- **`PragmaRegistry`** (Phase 21) — closed set; adding three new entries is mechanical per Phase 21 D-17's reservation.
- **`MusicalNoteData.CentOffset`** (`flow-lang/TypeSystem/SpecialTypes/NoteType.cs:218`) — already plumbed through `NoteStreamCompiler` and the `Sequence` data path. D-10's additive-cents math just multiplies after the ratio lookup.

### Established Patterns
- **No-runtime-state for parse-time concepts** (Phase 21 D-05/D-06; CLAUDE.md "Module imports execute in the caller's context — no separate scope/namespace isolation"). Tuning is RENDER-TIME state, so it legitimately lives on `MusicalContext` (which is runtime/render state). The pragma → MusicalContext bridge happens once at FlowEngine entry per D-06.
- **Closed-enum / closed-set design** (CLAUDE.md house style; Phase 21 D-17 `PragmaRegistry`; `TokenType`, `DurationValue`). Tuning system identifier is a closed enum (`TuningSystem.JustIntonation` / `Pythagorean` / `EqualTemperament`); chromatic ratio tables keyed by `(TuningSystem, Mode)` form a closed `Dictionary<...>`.
- **One-shot stderr warnings for audible silent regressions** — `TransposeSemitone:276` (out-of-range clamp), `TransposeCent:297` (non-multiple-of-100 round). D-11 / D-13 / the writeMidi warning all follow this pattern; per-session dedup via a small static HashSet.
- **xUnit Facts written BEFORE production code** — Phase 18-22 all RED → GREEN. Phase 23 plans should follow.
- **Phase 18 byte-identical regression gate** — every Phase 19/20/21/22 closure verified `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` 19/19 GREEN. Phase 23 too — D-08 default-tuning behavior MUST be byte-identical to current 12-TET output for any file that doesn't declare a tuning pragma.
- **Per-feature `tests/test_*.flow` smoke scripts** — each tuning ships at least one `.flow` script demonstrating it; integration loop runs all such scripts and any non-zero exit is a regression.
- **Atomic commits per task** — `feat(23-NN): ...` / `test(23-NN): ...` conventional-commit prefix per recent phases.

### Integration Points
- **Single chokepoint at `PitchConversion.NoteToFrequency`** — every synthesizer + vocalization call site routes through this one function; modifying it once propagates tuning awareness across the entire audio pipeline with zero call-site churn.
- **`FlowEngine.Run()` entry-point** — single bridge site for D-06 pragma → `MusicalContext.Tuning` activation. Tests can mock this by setting `MusicalContext.Tuning` directly.
- **`ScaleDatabase.ParseKeyName`** — single point of mode-suffix recognition; D-04's extension here unblocks both this phase's tuning tables AND Phase 24's `scaleLint`.
- **`MusicalContext.Current` accessor** — already used by every synthesizer for tempo/key reads. New `Tuning` property is read symmetrically.
- **`flow-midi/Midi/` writeMidi entry** — single guarded site for D-13's one-time non-12-TET warning. No pitch-bend infrastructure in this phase.

</code_context>

<specifics>
## Specific Ideas

- The MICR-01 acceptance pin is exact: `enable justIntonation;` followed by `play(C4 E4)` produces frequency ratio 5:4 (= 1.25), not 12-TET ~1.2599. This becomes the canary test in `tests/test_tuning_ji.flow`. The xUnit equivalent should assert on the 5:4 ratio explicitly (not just on the absolute Hz value, so test stays resilient to A4 reference frequency choices).
- 5-limit JI canonical reference: Helmholtz/Ellis chromatic table from C tonic. 3-limit Pythagorean: standard chain-of-fifths from C tonic, ±6 fifths to cover the chromatic. Citations belong in the doc comment of the tuning-table file.
- `enable equalTemperament;` is functionally a no-op vs no-pragma per D-08, but it MUST register so Phase 24 `enable scaleLint;` can later read tuning intent. Test pin: a file with explicit `enable equalTemperament;` produces byte-identical output to the same file without the pragma.
- The acceptance "transforms remain pitch-class agnostic" (MICR-02) is verified by a test: `transpose(seq, 5)` under JI vs Pythagorean vs no-pragma produces the same MIDI numbers at every position, even though the rendered Hz differ. The MIDI numbers are the invariant.
- Naming: stay close to the user's pragma names everywhere. `TuningSystem.JustIntonation` (not `FiveLimitJI`); `TuningSystem.Pythagorean` (not `ThreeLimitPythagorean`); `TuningSystem.EqualTemperament` (not `TwelveToneEqual`). Aligns user-facing pragma vocabulary with C# identifiers.
- Document the v1.4 Scala loader deferral text once, in the `MICR-03` error message AND in the pragma reference doc. Single source-of-truth string so users searching for "Scala" find the same canonical pointer everywhere.

</specifics>

<deferred>
## Deferred Ideas

### Out of Phase 23 Scope
- **Full Scala (`.scl`) loader** — `tuning loadScala("path.scl") { ... }`-style block; deferred to v1.4 per REQUIREMENTS.md D-03. MICR-03 unknown-tuning error message points users at this future expansion.
- **Faithful microtonal MIDI export** — per-channel pitch-bend events per Pitfall 5 #2; deferred to v1.4. Phase 23 emits a one-time stderr warning per D-13.
- **Spelling-preserving transforms** (`transposePreserveSpelling`, etc.) — opt-in strict-mode escape hatch for transforms under non-12-TET. v1.4 candidate; mentioned in the D-12 doc-comment caveat so users can find it.
- **Block-scope `tuning { ... }` syntax** — explicitly deferred per Phase 21 D-02 (REQUIREMENTS.md "Future Requirements"). v1.3 ships file-scope only.
- **Configurable A4 reference frequency** (e.g., A4 = 432 Hz, 442 Hz) — Phase 23 hard-codes A4 = 440 Hz inherited from existing `PitchConversion`. v1.4+ candidate if composer feedback requests it.
- **Mode-aware tuning tables for harmonic minor / melodic minor / blues / etc.** — Phase 23 ships major + natural minor + 5 standard church modes only. Other modes future work alongside Scala loader.
- **Pre-resolution warning when `enharmonic()` would change pitch under non-12-TET BEFORE the call** — Phase 23 ships post-call warning per D-11. A pre-call warning (e.g., LSP squiggle) would belong in flow-lsp work post-v1.3.
- **Block-scope `tuning JustIntonation { ... }`-style syntax** — would let composers swap tunings mid-piece. Out of scope per Phase 21 D-02.
- **REPL meta-command `:tuning ji`** — discussed in Pragma Plumbing area; rejected in favor of D-07 (resolved tuning persists across REPL lines). If users find the persisted-tuning behavior confusing, revisit with a meta-command.

### Reviewed Todos (not folded)
None — no todos surfaced for Phase 23.

</deferred>

---

*Phase: 23-microtonal-tuning-wedge*
*Context gathered: 2026-05-03*
