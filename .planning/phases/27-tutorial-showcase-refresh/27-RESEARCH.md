# Phase 27: Tutorial + Showcase Refresh — Research

**Researched:** 2026-05-10
**Domain:** Documentation / examples refresh (final v1.3 milestone closer)
**Confidence:** HIGH

## Summary

Phase 27 is a documentation-shaped phase that exercises the entire v1.3 surface end-to-end through `examples/tutorial.flow` (currently 684 lines, 19 chapters) and `examples/showcase.flow` (currently 44 lines, v1.2 ambient piece). All the strategic decisions are locked in CONTEXT.md (D-101..D-503); this research document fills in the technical implementation details — concrete syntax for each v1.3 feature, file impact map, byte-identical contract semantics, and integration risks.

**The biggest finding** is that the byte-identical determinism tests (`Phase18ByteIdenticalShowcaseTests` + `Phase25ByteIdenticalShowcaseGaussianTests`) **do not pin specific byte arrays**. They run the script TWICE in fresh `FlowEngineRunner` instances and assert `bytes1.SequenceEqual(bytes2)`. CONTEXT D-204 ("update existing fact files in Phase 27 closure to refresh the byte-pin assertions") is mis-framed: there are no pin bytes to update. The tests are content-agnostic regression gates that automatically follow whatever showcase.flow does, as long as showcase.flow remains deterministic across two consecutive runs. **The Phase 27 closure work for D-204 is therefore "verify they stay GREEN," not "re-pin bytes."** This dramatically simplifies the closure step. Phase 15 `EuclideanByteIdenticalTests` is the only test class that pins specific velocity bytes, and it does NOT consume tutorial.flow / showcase.flow — it has its own inline source.

Pragmas are strictly per-file (verified via `Phase21/PragmaIsolationFacts`), so `enable hAsB;` and `enable justIntonation;` activated in `examples/pragmas/h_alias.flow` and `examples/pragmas/microtonal_ji.flow` will NOT propagate into tutorial.flow even if tutorial.flow `use`s them. This makes the multi-file companion strategy (D-401) clean by construction. Pragmas must appear at the top of the file (before any non-pragma statement); a tutorial chapter cannot demonstrate `enable hAsB;` inline, only via prose + pointer to the companion file.

Tutorial.flow already runs cleanly under v1.3 head (Phase 26.2 closure 86bdd15 left it untouched). All v1.3 feature builtins ship with stdlib forward declarations in `flow-lang/std.flow` + `flow-lang/audio.flow`, so the tutorial just imports `@std` + `@audio` + `@composition` + `@collections` (already does) and every v1.3 builtin is callable.

**Primary recommendation:** Plan a Wave 1 (language-feature weaves) + Wave 2 (music-batch chapter) + Wave 3 (graduation song refactor) + Wave 4 (showcase rewrite + companion files) + Wave 5 (closure docs + REQUIREMENTS QOL-04 rewrite + Phase27ByteIdenticalPragmaTests). Mirror Phase 16's 5-plan structure verbatim.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Tutorial chapter prose (`Note:` / `(print "...")`) | examples/ source files | — | Tutorial is a `.flow` script; the interpreter executes it like any other script. No new infrastructure tier needed. |
| v1.3 feature demonstration | flow-lang stdlib (already shipped) | examples/tutorial.flow (caller) | All Phase 18-26.2 features ship as stdlib `internal proc` decls + C# registrations; Phase 27 only exercises the existing surface. |
| Byte-identical determinism gate | flow-lang.Tests/Integration/Phase18 + Phase25 | examples/showcase.flow (input source) | Tests are content-agnostic — they re-read showcase.flow from disk on each run. Phase 27 must keep showcase.flow deterministic; tests automatically follow. |
| Companion-file byte-identical gate (NEW) | flow-lang.Tests/Integration/Phase27 (NEW) | examples/pragmas/*.flow (input sources) | New `Phase27ByteIdenticalPragmaTests` class mirrors Phase18 verbatim — same two-runner content-agnostic pattern, different script paths. |
| Graduation song WAV+MIDI export | examples/output/ (gitignored) | examples/tutorial.flow (writeWav + writeMidi callers) | `examples/output/.gitignore` already exists from Phase 16; reused for tutorial + showcase + companion outputs. |
| REQUIREMENTS QOL-04 rewrite at closure | .planning/REQUIREMENTS.md | examples/tutorial.flow (the demonstration) | D-101 follows the Phase 26.1 DICT-01/02/03 + Phase 26.2 ERG-01..05 author-at-closure pattern. Closure plan rewrites QOL-04 to include the actual landed scope. |
| CLAUDE.md Music Types Quick Reference table | CLAUDE.md (project root) | — | D-104 docs surface; lives next to existing Music-Specific section. Pure documentation; no code touch. |

## Standard Stack

### Core (already in place — no changes)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 | net10.0 | Runtime | Already in csproj; Phase 27 adds zero new dependencies. |
| flow-lang stdlib (`@std`, `@audio`, `@composition`, `@collections`) | v1.3 head | Tutorial-callable surface | All v1.3 feature builtins (`humanizeGaussian`, `dict`, `volume`, `lowpass(buf, Hz)`, etc.) already have `internal proc` forward decls. Tutorial just calls them. [VERIFIED: grep flow-lang/std.flow + audio.flow] |
| examples/output/ + .gitignore | Phase 16 | Generated artifact location | Already exists (`*.wav` + `*.mid` ignored). Companion files D-404 reuse the same directory. [VERIFIED: ls examples/output/.gitignore] |

### New Test Infrastructure (Phase 27 ships)

| Library | Purpose | Why Standard |
|---------|---------|--------------|
| `flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs` | Pin two-run byte-identity for `examples/pragmas/h_alias.flow` + `microtonal_ji.flow` (4 facts) | D-403; mirrors `Phase18/ByteIdenticalShowcaseTests.cs:1-90` verbatim — only changes are namespace, class name, script paths. [VERIFIED: read of existing Phase18 test class] |

### NOT recommended

| Library | Why Not |
|---------|---------|
| Pinning specific byte arrays in any new tests | Phase 18 + 25 + 23 byte-identical tests use **two-run SequenceEqual**, not pinned bytes. Pin-byte arrays exist only in `Phase15/EuclideanByteIdenticalTests.cs` (inline source, not tutorial.flow / showcase.flow). Phase 27 follows the two-run pattern. [VERIFIED: read of all four byte-identical test classes] |
| Any new external NuGet package | Pre-public, minimal-deps philosophy. Phase 27 is documentation; no new code surface. |

**Installation:** No new packages.

## Architecture Patterns

### System Architecture Diagram

```
                     ┌──────────────────────────────────────┐
                     │ examples/tutorial.flow (~900 lines)  │
                     │   (educational; weaves v1.3 inline)  │
                     └───────────────┬──────────────────────┘
                                     │
                                     │ run via dotnet run --project flow-interpreter
                                     ▼
                     ┌──────────────────────────────────────┐
                     │ FlowEngine (lex → parse → interpret) │
                     └───────────────┬──────────────────────┘
                                     │
                  ┌──────────────────┼─────────────────────┐
                  ▼                  ▼                     ▼
        ┌─────────────────┐  ┌──────────────────┐  ┌────────────────┐
        │ stdout (chapter │  │ examples/output/ │  │ examples/output│
        │   prose +       │  │ flow_tutorial.wav│  │ flow_tutorial. │
        │   sequence      │  │ (graduation song │  │ mid (graduation│
        │   prints)       │  │  audio render)   │  │ song notation) │
        └─────────────────┘  └──────────────────┘  └────────────────┘

                     ┌──────────────────────────────────────┐
                     │ examples/showcase.flow (~50-80 lines)│
                     │   (decoration; "wow listen to this") │
                     └───────────────┬──────────────────────┘
                                     │
                                     │ same FlowEngine pipeline
                                     ▼
                     ┌──────────────────────────────────────┐
                     │ examples/output/flow_showcase.{wav,  │
                     │ mid}                                  │
                     └──────────────────────────────────────┘
                                     │
                                     │ regression-gated by
                                     ▼
                     ┌──────────────────────────────────────┐
                     │ Phase18/ByteIdenticalShowcaseTests   │
                     │ Phase25/ByteIdenticalShowcaseGaussian│
                     │   (run twice in fresh runners,       │
                     │    SequenceEqual the bytes)          │
                     └──────────────────────────────────────┘

                     ┌──────────────────────────────────────┐
                     │ examples/pragmas/h_alias.flow        │
                     │ examples/pragmas/microtonal_ji.flow  │
                     │   (NEW — D-401; standalone runners)  │
                     └───────────────┬──────────────────────┘
                                     │
                                     ▼
                     ┌──────────────────────────────────────┐
                     │ examples/output/{h_alias,microtonal_ │
                     │   ji}.{wav,mid}                       │
                     └───────────────┬──────────────────────┘
                                     │
                                     ▼
                     ┌──────────────────────────────────────┐
                     │ Phase27ByteIdenticalPragmaTests (NEW)│
                     │   4 facts; same two-run pattern      │
                     └──────────────────────────────────────┘
```

### Component Responsibilities

| Component | File(s) | Phase 27 action |
|-----------|---------|------------------|
| Tutorial source | `examples/tutorial.flow` | Modify: add Symbols/Tuples/Dict/prefix-arithmetic chapters (D-302); add v1.3 Music Capabilities batch chapter (D-303); refactor graduation song (D-304); update Congratulations bullet list. Expected delta ~+250-350 lines (684 → ~950). |
| Showcase source | `examples/showcase.flow` | **Replace entirely** (D-201). New polyrhythmic-minimal piece with tuplet groove + Dict-driven drums + Hertz filter sweep + Ms-typed delay + Second-decay reverb (D-202). Length cap none (D-203). |
| Companion: H-alias | `examples/pragmas/h_alias.flow` (NEW) | Create: ~30 lines. `enable hAsB; ... | H4q B4q | ... Int H = 5;` per D-402. |
| Companion: JI | `examples/pragmas/microtonal_ji.flow` (NEW) | Create: ~40 lines. `enable justIntonation; ... | C4 E4 G4 |` Cmaj triad demo per D-402. |
| Companion-file gitignore | `examples/output/.gitignore` | No change — existing `*.wav` + `*.mid` rule covers companion outputs. [VERIFIED: cat of file] |
| Phase 18 byte-identical test | `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` | **No code change required.** Test re-reads showcase.flow on each run; new content auto-flows through the SequenceEqual contract. Just confirm 2/2 GREEN at closure. [VERIFIED: read of test source] |
| Phase 25 byte-identical test | `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` | Same as Phase 18 — no code change. Just confirm 2/2 GREEN at closure (assuming showcase.flow still calls `humanizeGaussian` somewhere — D-202 implies it should via melody humanization). |
| Phase 18 byte-identical TUTORIAL test | `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` | **No code change required.** Same two-run content-agnostic pattern. Tutorial.flow stays deterministic (use fixed seeds for euclidean + humanizeGaussian); test follows automatically. [VERIFIED: read of test source] |
| New Phase 27 pragma test | `flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs` (NEW) | Create: 4 facts (h_alias WAV + h_alias MIDI + microtonal_ji WAV + microtonal_ji MIDI). Copy Phase18 ShowcaseTests.cs verbatim, change script paths and basenames. |
| REQUIREMENTS QOL-04 | `.planning/REQUIREMENTS.md` line ~127 | Rewrite at closure (D-101): expand to include Phase 26.2 surface (volume, Hertz, Ms-FX, Second-decay, createXxxTone-Hertz, gain-vs-volume split). Mirror DICT-01/02/03 closure rewrite from Phase 26.1 plan 06. |
| ROADMAP Phase 27 | `.planning/ROADMAP.md` line ~266-275 | Mark Complete at closure; success criteria #1 expanded to include Phase 26.2 surface per D-101. |
| STATE.md | `.planning/STATE.md` | Advance progress.completed_phases 11→12; current focus → "v1.3 milestone shipped, ready for /gsd-complete-milestone v1.3". |
| CLAUDE.md | `CLAUDE.md` Music-Specific section | Append Music Types Quick Reference table per D-104 (~20 lines, columns: literal | type | IsCompatibleWith | accepted at). |
| Phase 27 VERIFICATION | `.planning/phases/27-tutorial-showcase-refresh/27-VERIFICATION.md` (NEW) | Mirror Phase 16 + 26.2 VERIFICATION.md shape. |
| Phase 27 SUMMARY | `.planning/phases/27-tutorial-showcase-refresh/27-SUMMARY.md` (NEW) | Standard closure summary. |

### Recommended Project Structure

```
examples/
├── tutorial.flow              # MODIFY: ~+250-350 lines for v1.3 chapters
├── showcase.flow              # REPLACE: new polyrhythmic-minimal piece
├── pragmas/                   # NEW directory
│   ├── h_alias.flow           # NEW: ~30 lines, enable hAsB; demo
│   └── microtonal_ji.flow     # NEW: ~40 lines, enable justIntonation; demo
└── output/                    # No change (already exists from Phase 16)
    ├── .gitignore             # No change (*.wav + *.mid already covered)
    └── (generated artifacts)  # tutorial.{wav,mid}, showcase.{wav,mid}, h_alias.{wav,mid}, microtonal_ji.{wav,mid}

flow-lang.Tests/Integration/
└── Phase27/                   # NEW directory
    └── Phase27ByteIdenticalPragmaTests.cs  # NEW: 4 facts
```

### Pattern 1: Two-Run Byte-Identical Test (canonical, content-agnostic)

**What:** Run the .flow script twice in fresh `FlowEngineRunner` instances, redirect output to per-run paths, then `SequenceEqual` the bytes.

**When to use:** Every byte-identical regression gate for a tutorial/showcase/companion script. Single source of truth — no inline pinned bytes.

**Example (verbatim from Phase 18 — copy this for Phase 27):**

```csharp
// Source: flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs:1-90
[Collection("FlowScripts")]
public class Phase27ByteIdenticalPragmaTests
{
    [Fact] public void HAlias_TwoRunsProduceIdenticalWav() => RunTwiceAndCompare("h_alias", isMidi: false);
    [Fact] public void HAlias_TwoRunsProduceIdenticalMidi() => RunTwiceAndCompare("h_alias", isMidi: true);
    [Fact] public void MicrotonalJi_TwoRunsProduceIdenticalWav() => RunTwiceAndCompare("microtonal_ji", isMidi: false);
    [Fact] public void MicrotonalJi_TwoRunsProduceIdenticalMidi() => RunTwiceAndCompare("microtonal_ji", isMidi: true);

    private static void RunTwiceAndCompare(string baseName, bool isMidi)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string scriptPath = Path.Combine(repoRoot, "examples", "pragmas", $"{baseName}.flow");
        // ... (rest verbatim from ByteIdenticalShowcaseTests:32-89, with paths swapped)
    }
}
```

### Pattern 2: Tutorial Chapter Skeleton (Phase 16 precedent)

**Format (from existing tutorial.flow):**

```flow
Note: -----------------------------------------------------------
Note: N. Chapter Title
Note: -----------------------------------------------------------
(print "")
(print "--- N. Chapter Title ---")
(print "")
Note: One or two prose lines explaining what this chapter teaches.

<demo body — use Note: for multi-line prose, // for inline annotations>

Sequence demoSeq = | C4q D4q E4q |
(print $"demo: {(str demoSeq)}")
```

**Anti-patterns to avoid (from Phase 16 REVIEW-FIX):**
- Don't add new render/export calls inside non-graduation chapters — risks breaking byte-identical contract for the graduation song. Keep new demos as standalone print-and-show; only the graduation chapter renders to file.
- Don't claim a feature works in a comment without an executable demo (Phase 16 IN-02: tutorial claimed `decrescendo` in summary but never demoed it; fix was to drop the claim).
- Don't introduce dynamics-block scoping that traps a `Sequence` reference (Phase 16 IN-05: `dynamics ff { Sequence x = ... }` scopes `x` to the dynamics block; downstream sections can't see it. Use inline marker `| ff C4 D4 |` instead).

### Pattern 3: Graduation-Song Style (Phase 16 D-04 + D-07)

**Required structure (carries over to Phase 27):**

```flow
tempo BPM {
    timesig 4/4 {
        key Kmajor {
            // section declarations with named Sequence variables
            section secName { Sequence mel = | ... | ; Sequence bass = | ... | }
            ...

            Song mySong = [section1 section2*2 section3 ...]
            (print $"Song: {(str mySong)}")

            Buffer rawMix = (renderSong mySong "piano")
            // Polished effects chain via flow operator
            Buffer finalMix = rawMix -> (reverb 0.25) -> (lowpass 4000.0) -> (gain -2.0)

            // Optional: tempoRamp tail for ritardando — mix into WAV but NOT MIDI
            Sequence tail = | C4h G3h C3w |
            Buffer ritBuf = (tempoRamp tail X.0 Y.0)
            Buffer finalWithTail = (mix finalMix ritBuf)

            // BOTH writeWav (Buffer) and writeMidi (Song) from same Song value
            (writeWav "examples/output/flow_tutorial.wav" finalWithTail)
            (writeMidi "examples/output/flow_tutorial.mid" mySong)
        }
    }
}
```

### Pattern 4: Phase 16 Plan Skeleton (5 plans, mirror exactly)

| Plan | Phase 16 role | Phase 27 mapping |
|------|---------------|-------------------|
| 27-01 | Wave 1 — language-feature weaves (Symbols / Tuples+~> / Dict / prefix-arithmetic chapter prose update) | D-302 weave targets |
| 27-02 | Wave 2 — music-feature batch chapter (tuplets+fractional / microtonal+scale-lint / DX-10..15 / misc small wins) | D-303 batch sub-sections |
| 27-03 | Wave 3 — graduation song refactor + showcase replace + companion files (run in parallel if planner picks) | D-103 audible features + D-201 + D-401/402 |
| 27-04 | Wave 3 alt — companion-file Phase27ByteIdenticalPragmaTests + tests/output/ smoke | D-403 |
| 27-05 | Wave 4 — closure: REQUIREMENTS QOL-04 rewrite (D-101) + CLAUDE.md Music Types Quick Reference (D-104) + ROADMAP/STATE/VERIFICATION/SUMMARY | Phase 16 + 26.1 + 26.2 closure precedent |

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Byte-pin assertion for showcase regression | Inline `byte[]` literal pinning a multi-MB WAV byte sequence | Two-run `SequenceEqual` pattern from Phase18/ByteIdenticalShowcaseTests | Phase 15 D-13 doctrine (RESEARCH §Critical Note on Phase 18 Showcase Test): tutorial/showcase WAVs are too large to pin in source; two-run identity is the regression contract. Pinning is reserved for compact MIDI velocity sequences (Phase 15 EuclideanByteIdenticalTests with 3 bytes). |
| Custom test runner / test framework for `examples/pragmas/*.flow` | New xUnit Theory machinery | Existing `FlowScriptData.GetFlowScripts()` auto-discovers any .flow file under `tests/` | If pragma files need to be auto-discovered as smoke tests, they could move under `tests/`. But D-401 says they live under `examples/pragmas/` (composer-discoverable). The dedicated `Phase27ByteIdenticalPragmaTests` class handles them via explicit path; no auto-discovery hook needed. |
| Tutorial-chapter index / ToC machinery | Top-of-file index of chapters | Phase 16 D-12 prose-only inline traceability | "No central index at top or bottom of the file. The reader sees feature references right next to the snippet that demonstrates them." Don't create an index in Phase 27. |
| Comment-style overhaul of existing 19 chapters | Global Note: ↔ // refactor | CONTEXT D-102 + Phase 16 D-09 carryover: only refresh comments where v1.3 requires explanation | Phase 16 took a pass on this; Phase 27 only touches comments needed for new v1.3 surface. |
| Pinning of expected file sizes | `Assert.Equal(5503724, bytes.Length)` | `Assert.True(bytes.Length > 0)` smoke + SequenceEqual identity | File sizes drift across .NET patches (Phase 15 RESEARCH Pitfall 7: System.Random algorithm drift). Length-pin would break under .NET 10 minor updates. Smoke + identity is the durable contract. |

**Key insight:** Phase 27's regression contract is structural (run twice, get the same bytes), not absolute (these specific bytes). This makes Phase 27 closure dramatically lighter than CONTEXT D-204 implies — there's no "capture bytes via dotnet run × 2 + cmp, encode hex literals" step.

## Runtime State Inventory

> Phase 27 is greenfield additive content (new tutorial chapters + new showcase + new companion files + new test class). The only "rename" risk is the showcase.flow replacement, which the existing two-run tests handle structurally.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — Phase 27 reads/writes only the `examples/output/` directory which is gitignored. No databases, no datastores. | None |
| Live service config | None — no n8n, no external services. The `dotnet run` invocation is fully self-contained. | None |
| OS-registered state | None — no Task Scheduler, no pm2, no systemd. | None |
| Secrets/env vars | None — no .env, no SOPS keys involved. | None |
| Build artifacts / installed packages | `examples/output/flow_tutorial.{wav,mid}` + `flow_showcase.{wav,mid}` from Phase 16. **D-201 replaces showcase.flow content; the artifact files in examples/output/ are stale** but gitignored — they regenerate on first run. No action needed. New artifacts (`h_alias.{wav,mid}`, `microtonal_ji.{wav,mid}`) auto-create when companion files run. | None — gitignored artifacts; they regenerate cleanly. |

**The canonical question — verified for Phase 27:** After every file edit ships, what runtime systems still have stale state? **Answer: only the `examples/output/*.{wav,mid}` artifacts**, which are gitignored and regenerate on first run. There is no runtime state to migrate.

## Common Pitfalls

### Pitfall 1: Misreading the byte-identical contract as "pin specific bytes"

**What goes wrong:** Plan tries to capture bytes via `cmp` + dotnet run × 2 and encode them as hex literals in the test (mirrors Phase 15 EuclideanByteIdenticalTests), then has to maintain that pin every time showcase.flow content drifts.

**Why it happens:** CONTEXT D-204 wording ("update the byte-pin assertions") + Phase 15 EuclideanByteIdenticalTests precedent (which does pin 3 velocity bytes inline).

**How to avoid:** Read `Phase18/ByteIdenticalShowcaseTests.cs:78-83` and `Phase25/ByteIdenticalShowcaseGaussianTests.cs:86-91` directly. They run the script twice and assert `bytes1.SequenceEqual(bytes2)`. There is NO pinned byte array. The tests pass automatically as long as showcase.flow is deterministic across two consecutive runs (fixed seeds for euclidean + humanizeGaussian, no `Random` without seed, no `DateTime.Now`, etc.). [VERIFIED: read of all 4 byte-identical test classes]

**Warning signs:** A plan task that says "capture bytes from `dotnet run`" or "encode hex literals into the test" — that's the misreading.

### Pitfall 2: Pragma activation in tutorial.flow blowing the file's pragma budget

**What goes wrong:** Tutorial.flow declares `enable justIntonation;` at top to demonstrate microtonal — but pragmas are FILE-SCOPED (Phase 21 D-02), so this changes the entire file's pitch rendering, breaking the byte-identical determinism contract for the existing 12-TET v1.1+v1.2 chapters AND the graduation song.

**Why it happens:** Composer instinct says "just enable it inline next to the demo." Pragmas don't work that way — they're declared at the top of the file or not at all.

**How to avoid:** Per CONTEXT D-401, pragmas are demonstrated via **companion files only** (`examples/pragmas/h_alias.flow`, `microtonal_ji.flow`). Tutorial.flow contains print-only explanation + paste-ready snippets + a pointer ("run `examples/pragmas/microtonal_ji.flow` to hear this"). CONTEXT D-304 closure note explicitly leaves planner discretion to decide whether tutorial.flow activates ONE microtonal pragma if the graduation song benefits — **default is NO pragma in tutorial.flow.** [VERIFIED: PragmaScannerFacts:42-49 + PragmaIsolationFacts:24-69 — pragmas strictly per-file, NOT propagated via use]

**Warning signs:** A plan that adds `enable justIntonation;` to line 1 of tutorial.flow without a downstream "rerender all 19 existing chapters under JI to confirm byte-identity" gate.

### Pitfall 3: Tuplet + tied-note edge case in showcase

**What goes wrong:** Showcase uses `{3:2 C4 D4 E4~}q` (tied note across tuplet boundary) — interaction was not extensively tested in Phase 19 (no dedicated tied-tuplet fact found). [VERIFIED: grep "tied" in Phase19/*.cs returns no tuplet-specific tied tests]

**Why it happens:** Tied-note semantics are a per-note flag (`IsTied` on `MusicalNoteData`); tuplet compilation preserves the flag through `NoteStreamCompiler`. But the **render-time** behavior — `BarRenderer.cs:81-86` extends render duration by 100ms overlap when `note.IsTied` — was authored before tuplets shipped. Inside a tuplet whose total duration is < 100ms (e.g., `{3:2 C4q D4 E4~}q` where each note ≈ 167ms), the overlap is meaningful; inside a `{15:4 ...}s` 60-tuplet, the overlap could exceed the next note's onset, creating subtle voice-stacking.

**How to avoid:** Showcase tuplet groove uses **un-tied** notes inside the tuplet bracket. If a tied note is desired across a tuplet→non-tuplet boundary, place the tie on the LAST tuplet note BEFORE crossing back into straight time: `| {3:2 C4 D4 E4}q E4q |` (no tie) → `| {3:2 C4 D4 E4~}q E4q |` is the safe form. Avoid placing tied notes on the FIRST element of a tuplet (`| C4q~ {3:2 ... }q |`) — semantics underspecified.

**Warning signs:** Showcase plan that uses tied notes inside `{N:M ...}` brackets — request planner to flag for verification or restructure.

### Pitfall 4: Dict-driven drum dispatch ordering surprise

**What goes wrong:** Showcase uses `(each drumPattern (fn Symbol s, Beat at => (renderHit (get drums s) at)))` with `drums = (dict #kick C2 #snare D2 #hihat F#3)`. Composer expects iteration in a specific order (e.g., kick first, hihat last); dict iteration follows **insertion order**, not alphabetical or hash order.

**Why it happens:** REQUIREMENTS DICT-03 explicitly contracts insertion-order preservation. [VERIFIED: flow-lang/Runtime/Value.cs:106 — `OrderedDictionary<TKey,TValue>` backs `DictData`]

**How to avoid:** Construct the dict in the order you want iteration to happen. `(dict #kick C2 #snare D2 #hihat F#3)` iterates kick → snare → hihat. Document this in the showcase comment so a reader doesn't think it's hash-order coincidence.

**Warning signs:** Showcase plan that asserts iteration order without a comment explaining "insertion order" — easy to miss for a future maintainer who refactors the dict construction.

### Pitfall 5: gain/volume footgun in graduation song refactor

**What goes wrong:** Graduation song wraps section in `(volume sectionBuf 0.6)` thinking it's the same as the v1.2 `gain 0.6 { section ... }` — but the v1.2 form is a **musical context block** that scales per-section gain (a CompositionContext setting, applied during `renderSong`), while the new `volume(Buffer, Double)` is a **post-render buffer multiplier** (applied AFTER `renderSong` returns the Buffer). Different timing → different audible effect.

**Why it happens:** The two are unit-different (linear-multiplier vs. linear-scalar-during-render) AND tier-different (musical-context vs. post-buffer). [VERIFIED: tutorial.flow:306-313 + 585-589 — current `gain 0.6 { section ... }` is musical-context block; volume(Buffer, Double) is in audio.flow:415-417 as a post-render buffer op]

**How to avoid:** Gain-as-musical-context-block (`gain 0.6 { section x { ... } }`) STAYS for per-section dynamic shaping (existing tutorial.flow:585-589 idiom). The new `volume(Buffer, Double)` is for post-render buffer-level adjustments — use it on `finalMix` or on a section's rendered Buffer, NOT as a musical-context block. Phase 26.2 ERG-03 documents this split; CONTEXT D-104 Music Types Quick Reference table will pin it in CLAUDE.md.

**Warning signs:** A plan that replaces `gain 0.6 { section ... }` with `volume sectionBuf 0.6` — that's the footgun. Keep the gain musical-context block as-is; ADD volume(buf, linear) at a different position in the chain.

### Pitfall 6: Tutorial graduation song pragma scoping

**What goes wrong:** Tutorial.flow's graduation song activates `enable justIntonation;` to demonstrate JI — but the pragma applies to the ENTIRE file, retuning every chapter's note rendering. Chapter 11's `(renderSong presetSong "strings")` now produces JI-tuned strings instead of 12-TET; chapter 18's euclidean groove tuned in JI; etc. The byte-identical contract for all the buffers rendered upstream changes.

**Why it happens:** Same as Pitfall 2 — pragmas are file-scoped.

**How to avoid:** Default per CONTEXT D-304 closure note: NO pragma in tutorial.flow. Microtonal demo lives in `examples/pragmas/microtonal_ji.flow`. If planner finds the graduation song genuinely benefits from JI audibly (likely no — most short progressions don't show 5/4 vs 1.2599 audibly), the WHOLE tutorial.flow goes JI; weigh the regression cost.

**Warning signs:** A plan that activates a microtonal pragma in tutorial.flow without explicitly addressing "this changes every prior chapter's rendered audio."

### Pitfall 7: Companion file pragma + write-path collision

**What goes wrong:** Both `examples/pragmas/h_alias.flow` and `examples/pragmas/microtonal_ji.flow` write to `examples/output/h_alias.{wav,mid}` etc., and the `Phase27ByteIdenticalPragmaTests` runs each file twice. If the test rewrites only the `examples/output/` path to `tests/output/phase27_X_run1.{wav,mid}` (mirroring Phase 18 pattern), but the companion file uses a different write idiom (e.g., `(writeWav "examples/output/microtonal_ji.wav" buf)` with literal string), the path-rewrite `string.Replace()` must match exactly.

**Why it happens:** Phase 18 test uses `source.Replace($"examples/output/{baseName}.{ext}", $"tests/output/...")` — a string match. If the companion file uses `(writeWav "/tmp/microtonal_ji.wav" buf)` or any other path style, the replacement fails silently and `Assert.NotEqual(source, sourceRun1)` catches it as a deliberate halt.

**How to avoid:** Companion files MUST use the exact write idiom `(writeWav "examples/output/h_alias.wav" buf)` and `(writeMidi "examples/output/h_alias.mid" song)` (and similarly for microtonal_ji). Document this in the companion file plan as a contract: "Path-string MUST be `examples/output/<basename>.{wav,mid}` for the test rewrite to engage."

**Warning signs:** Companion file plan that uses `String wavPath = ...; (writeWav wavPath buf)` form — Phase 16 Plan 02 hit this exact issue (REVIEW IN-02). Keep paths inline in the writeWav/writeMidi call.

## Code Examples

Verified concrete syntax for each v1.3 feature the tutorial demonstrates. All snippets confirmed runnable against current head via stdlib forward decl inspection.

### Prefix arithmetic (chapter 2 prose update — D-302)

```flow
// Source: existing flow-lang/std.flow forward decls + Phase 26 STD-01..03 already migrated
Int sum = (add 10 25)
Double product = (mul 3.0 4.5)
Int difference = (sub 100 37)
Double quotient = (div 10.0 4.0)        // 2.5 — Double / Double via fast path
Int idivResult = (idiv 10 3)             // 3 — Int integer division
Double negated = (neg 3.14)              // -3.14 via unary builtin
String greeting = (concat "Hello, " "musician")  // explicit string concat
```

The chapter 2 update is **prose-only** — STD-03 already migrated all .flow files. Add a "no infix" rule paragraph.

### Symbols (new chapter — D-302 after chapter 1)

```flow
// Source: tests/test_symbol_literal.flow + REQUIREMENTS SYM-01
Symbol kick = #kick
Symbol same = #kick
Bool eq1 = (equals kick same)            // true — interned, pointer-equal
Bool eq2 = (equals #foo "foo")           // false — strict separation from String
Bool eq3 = (equals #foo #bar)            // false
(print $"#kick == #kick: {(str eq1)}")
(print $"#foo == \"foo\": {(str eq2)}")  // STRICT: distinct types
(print $"#foo == #bar: {(str eq3)}")
```

### Tuples + ~> + (unpack) (new chapter — D-302 after chapter 4)

```flow
// Source: tests/test_tuple_literal.flow + tests/test_tuple_destructure.flow + REQUIREMENTS TUP-09/10/11
Tuple<<Int, Int>> pair = <<10, 20>>
Int first = pair@0                       // 10
Int second = pair@1                      // 20
Tuple<<>> empty = <<>>                   // empty tuple legal
Tuple<<Note>> singleton = <<C4>>         // singleton tuple legal

// Destructuring assignment
<<Int a, Int b>> = pair
(print $"a={(str a)}, b={(str b)}")

// ~> unpacks tuple into multi-arg call (parse-time)
proc add3(Int: a, Int: b, Int: c)
    (add (add a b) c)
end proc
Int total = <<1, 2, 3>> ~> add3          // becomes (add3 1 2 3) at parse time
(print $"sum: {(str total)}")

// ~> falls through to -> on non-tuple LHS
proc doubleIt(Int: n) (mul n 2) end proc
Int doubled = 5 ~> doubleIt              // becomes (doubleIt 5) — non-tuple → -> semantics
(print $"5 ~> doubleIt: {(str doubled)}")

// (unpack) is the runtime equivalent of ~> for first-class function dispatch
Int viaUnpack = (unpack <<3, 4, 5>> add3)  // = 12
(print $"unpack: {(str viaUnpack)}")
```

### Generic Dict (new chapter — D-302 after Tuples)

```flow
// Source: tests/test_dict_construct.flow + tests/test_dict_ops.flow + REQUIREMENTS DICT-01/02/03
// Flat constructor: (dict K V K V ...)
Dict<Symbol, Int> velocities = (dict #kick 90 #snare 70 #hihat 50)
(print $"size: {(str (size velocities))}")
(print $"#kick: {(str (get velocities #kick))}")

// Tuple-pair constructor: (dictTuple <<K, V>> ...)
Dict<Symbol, Int> sameDict = (dictTuple <<#kick, 90>> <<#snare, 70>> <<#hihat, 50>>)

// 14-op surface
Bool hasKick = (has velocities #kick)              // true
Int defaulted = (getOr velocities #cowbell 0)      // 0 — missing key → default
Dict<Symbol, Int> updated = (set velocities #ride 60)
Dict<Symbol, Int> shrunk = (remove velocities #hihat)
Symbol[] ks = (keys velocities)                    // insertion order: [#kick, #snare, #hihat]
Int[] vs = (values velocities)                     // insertion order: [90, 70, 50]

// (each) over a Dict: 2-arg lambda receives unpacked key/value (DICT-03)
(each velocities (fn Symbol k, Int v => (print $"{(str k)} = {(str v)}")))

// (map) transforms values
Dict<Symbol, Int> doubled = (map velocities (fn Symbol k, Int v => (mul v 2)))

// (merge) — last-write-wins
Dict<Symbol, Int> overlap = (dict #kick 99)
Dict<Symbol, Int> merged = (merge velocities overlap)  // (#kick → 99)

// Empty dict
Dict<Symbol, Int> empty = (dict)
(print $"empty size: {(str (size empty))}")
```

### Tuplets + fractional durations (music-batch chapter — D-303)

```flow
// Source: REQUIREMENTS TUP-01..08 + flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs
timesig 4/4 {
    // Bracket form {N:M ...}q — three notes in the time of two quarters
    Sequence triplets = | {3:2 C4 D4 E4}q |             // each note = 1/12 whole = 1/3 quarter
    (print $"triplets: {(str triplets)}")

    // Shorthand {N ...}q — N=3 → 3:2 implicit (music21 convention)
    Sequence shorthand = | {3 C4 D4 E4}q |              // ≡ {3:2 C4 D4 E4}q

    // Per-note fractional duration C4/N — N is whole-note divisor
    Sequence frac = | C4/12 D4/12 E4/12 |               // each = 1/12 whole — same as triplet

    // Per-note tuplet shorthand C4/X:Y[suffix]
    Sequence pernote = | C4/3:2 D4/3:2 E4/3:2 |         // ≡ {3:2 C4 D4 E4}q
    Sequence quintuplet = | C4/5:4h |                   // single note, 1/10 whole

    // Nested tuplets
    Sequence nested = | {3:2 C4 {3:2 D4 E4 F4}q G4}h |  // inner durations multiply through

    // Bar-fit accepts rational sums
    Sequence balanced = | {3:2 C4 D4 E4}q {3:2 F4 G4 A4}q B4q C5q |  // sums to 4/4
}
```

### Microtonal pragmas (music-batch chapter — D-303 + companion file D-402)

```flow
// COMPANION FILE: examples/pragmas/microtonal_ji.flow (D-402)
enable justIntonation;
use "@std"
use "@audio"

// Cmaj triad in JI: C4 → ratio 1, E4 → 5/4 = 1.25, G4 → 3/2 = 1.5
// vs. 12-TET: C4 → 1, E4 → ~1.2599 (Math.Pow(2, 4/12)), G4 → ~1.4983
tempo 120 {
    timesig 4/4 {
        section ji_triad {
            | C4q E4q G4q C4w |
        }
    }
}
Song song = [ji_triad]
Buffer audio = (renderSong song "piano")
(writeWav "examples/output/microtonal_ji.wav" audio)
(writeMidi "examples/output/microtonal_ji.mid" song)
(print "JI Cmaj triad rendered — major third at 5:4 ratio")
```

In tutorial.flow's music-batch chapter, **prose-only** explanation:

```flow
Note: -----------------------------------------------------------
Note: v1.3 Music: Microtonal Tunings
Note: -----------------------------------------------------------
(print "")
(print "--- Microtonal Tunings ---")
(print "")
Note: Flow ships three named tunings, activated via top-of-file pragma:
Note:   enable justIntonation;    // 5-limit JI — Cmaj third = 5:4 = 1.25
Note:   enable pythagorean;       // 3-limit — Cmaj third = 81:64 ≈ 1.2656
Note:   enable equalTemperament;  // 12-TET — Cmaj third = 2^(4/12) ≈ 1.2599 (default)
Note:
Note: Pragmas are file-scoped — they apply to the WHOLE file. Tutorial.flow
Note: stays in 12-TET so all chapters use a consistent tuning.
Note:
Note: To hear JI in action:
Note:   dotnet run --project flow-interpreter examples/pragmas/microtonal_ji.flow
Note:
Note: This produces examples/output/microtonal_ji.{wav,mid} — a Cmaj triad
Note: rendered with JI's pure 5:4 major third.
```

### Hertz literals + filter sweep (chapter 9 inline weave — D-102 + D-103)

```flow
// Source: flow-lang/audio.flow:446-448 lowpass(Buffer, Hertz) + REQUIREMENTS ERG-04
Buffer dry = (renderSong effectSong "piano")

// Hertz literal forms
Hertz cutoffA = 440Hz                    // 440.0 Hz canonical
Hertz cutoffB = 1.5kHz                   // 1500.0 Hz (kHz × 1000 at lex time)

// Filter overloads accept Hertz directly (or bare Double — both work)
Buffer filtered = dry -> (lowpass 1.2kHz)
Buffer hpFiltered = dry -> (highpass 200Hz)
Buffer bpFiltered = dry -> (bandpass 200Hz 4kHz)

// createXxxTone overloads accept Hertz
Buffer toneHz = (createSineTone 0.5 880Hz 0.4)
```

### Ms-typed delay + Second-decay reverb (chapter 9 + chapter 16 inline weave — D-102 + D-103)

```flow
// Source: flow-lang/audio.flow:434 + 443 — Phase 26.2 ERG-02 overloads
Buffer melody = (renderSong melodySong "piano")

// Ms-typed delay (replaces existing bare-Double form in chapter 9 examples)
Buffer delayed = (delay melody 250ms 0.5 0.4)    // 250ms delay, 0.5 feedback, 0.4 mix

// Second-decay reverb (chapter 16 — append at end after existing reverbTime block)
Buffer reverbed = (reverb melody 0.5 1.8s)       // mix=0.5, decaySec=1.8

// Decibel gain (Phase 26.2 ERG-05) — keeps existing dB semantics
Buffer attenuated = (gain melody -12dB)          // -12 decibels
```

### gain vs volume (own chapter — D-102)

```flow
// Source: flow-lang/audio.flow:409 + 413 + 417 — Phase 26.2 ERG-03
Buffer signal = (renderSong demoSong "piano")

// gain — second arg is DECIBELS
Buffer attenuated = (gain signal -6.0)           // -6 dB → ≈ 0.501× linear
Buffer dbLiteral  = (gain signal -6dB)            // identical, explicit dB type

// volume — second arg is LINEAR multiplier
Buffer halfVol = (volume signal 0.5)             // 0.5× amplitude (≈ -6.02 dB)
Buffer doubled = (volume signal 2.0)             // 2× amplitude → CLIPS, prints stderr warning

// volume rejects negatives (no phase-invert via this function)
// (volume signal -0.5)  // → InvalidOperationException

// FOOTGUN AVOIDANCE: (gain buf 0.5) is 0.5 dB attenuation, NOT 50% volume!
// Use (volume buf 0.5) for "halve the amplitude" semantic.
```

### humanizeGaussian (existing chapter 18.5 stays; melody humanize in showcase D-202)

```flow
// Source: existing tutorial.flow:540-547 + showcase.flow:20 + REQUIREMENTS DEFER-06
Sequence rawMel = | mp _ _ E5q G5q | A5h E5h |
Sequence humanized = (humanizeGaussian rawMel 0.08 314)  // amount=0.08, seed=314

// Byte-identical: same seed → same output across two consecutive runs
```

### range / negative slice / multi-letter enharmonics (music-batch misc small wins — D-303)

```flow
// Source: REQUIREMENTS DEFER-01 + DEFER-04 + DEFER-05 + tests/test_range.flow + test_slice_negative.flow
Int[] forwards = (range 0 5)                     // [0, 1, 2, 3, 4]
Int[] stepped = (range 0 10 2)                   // [0, 2, 4, 6, 8]
Int[] reversed = (range 5 0 -1)                  // [5, 4, 3, 2, 1]

// Negative slice — Python-style from-end indexing
Int[] xs = [1, 2, 3, 4, 5]
Int[] tail3 = (slice xs -3 5)                    // [3, 4, 5]
Int[] dropLast = (slice xs 0 -1)                 // [1, 2, 3, 4]
Int last = xs@-1                                  // 5

// Multi-letter enharmonic edges — DEFER-04
Note efb = (enharmonic E4)                       // Fb4 (same MIDI pitch as E4)
Note fes = (enharmonic F4)                       // E#4
Note bcb = (enharmonic B4)                       // Cb5
Note cbs = (enharmonic C4)                       // B#3
```

### Showcase: polyrhythmic minimal piece (D-202 sketch)

```flow
// File: examples/showcase.flow (REPLACE per D-201)
// Genre: polyrhythmic minimal — 120 BPM Cmajor, tuplet groove + Dict-driven drums
//   + ambient pad bed + soft melody. Phase 26.2 features woven audibly per D-103.
use "@std"
use "@audio"
use "@composition"

(print "Flow Showcase — v1.3 Polyrhythmic Minimal")
(print "Generating examples/output/flow_showcase.{wav,mid} ...")

tempo 120 {
    timesig 4/4 {
        key Cmajor {
            // Tuplet groove leading the genre
            Sequence drumTriplets = | {3:2 _ C2 _}q C2 {3:2 _ C2 D2}q C2 |

            // Dict-driven drum dispatch — Symbol keys + insertion-order iteration (DICT-03)
            Dict<Symbol, Note> kit = (dict #kick C2 #snare D2 #hihat F#3)
            // (insertion order kick → snare → hihat preserved)

            // Euclidean drum with seed for byte-identical
            Sequence drums = (euclidean 5 16 (get kit #kick) 0.18 0.12 7)

            // Soft melody humanized via Gaussian (existing showcase pattern)
            Sequence melody = (humanizeGaussian | mp _ _ E5q G5q | A5h E5h | 0.08 314)

            // Ambient pad bed
            Sequence pad = | C4w | F4w | G4w | C4w |

            section showcase {
                Sequence groove = drums
                Sequence lead = melody
                Sequence bed = pad
            }

            Song piece = [showcase]
            Buffer rawMix = (renderSong piece "strings")

            // Phase 26.2 audible features per D-103:
            //   Hertz filter sweep at section boundary
            //   Ms-typed delay on lead
            //   Second-decay reverb on tail
            //   volume(buf, linear) for section dynamics
            Buffer filtered = rawMix -> (lowpass 1.2kHz)
            Buffer delayed = filtered -> (delay 250ms 0.5 0.4)
            Buffer withReverb = delayed -> (reverb 0.5 1.8s)
            Buffer finalMix = (volume withReverb 0.7)

            (writeWav "examples/output/flow_showcase.wav" finalMix)
            (writeMidi "examples/output/flow_showcase.mid" piece)
        }
    }
}

(print "WAV:  examples/output/flow_showcase.wav")
(print "MIDI: examples/output/flow_showcase.mid")
```

> Planner may rework musical content; the structural pattern (single section, fixed seeds, Phase 26.2 audible features in the chain) is what matters for the byte-identical contract.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Bare-Double FX params (`(delay buf 100.0 0.5 0.4)`) | Music-typed FX overloads (`(delay buf 100ms 0.5 0.4)`) | Phase 26.2 (2026-05-10) | Tutorial chapter 9 examples migrate to music-typed forms inline (D-102). Bare-Double overloads PRESERVED — coexistence by exact-match scoring. |
| `gain(Buffer, Double)` interpreted as either dB or linear (composer-ambiguous) | `gain` strictly dB; new `volume(Buffer, Double)` for linear | Phase 26.2 (2026-05-10) | New tutorial chapter (D-102) explains the split. Existing graduation song `gain 0.6 { section ... }` is musical-context block (different tier) — NOT affected. |
| Hard-coded Hz numerics (`(lowpass buf 800.0)`) | Hertz literal (`(lowpass buf 800Hz)`) | Phase 26.2 (2026-05-10) | Inline weave into tutorial chapter 9 (D-102). 1.5kHz lex form available. |
| Bare-Double reverb decay (`(reverb buf 0.5 1.5)` second Double = damping) | `(reverb buf 0.5 1.8s)` Second-typed decay | Phase 26.2 (2026-05-10) | Inline at end of tutorial chapter 16 (D-102). NOTE: existing `(reverb Buffer, Double, Double)` was damping+mix — different semantics from the new `(reverb Buffer, Double, Second)` decay form. Plan must verify no overload ambiguity (CONTEXT 26.2 D-08 verified clean — ERG-02 shipped). |
| Infix `+`/`-`/`*`/`/` in .flow source | Prefix-only via `(add)`/`(sub)`/`(mul)`/`(div)` | Phase 26 (2026-05-09) | Tutorial chapter 2 prose update only; STD-03 already migrated all .flow files. |
| Phase 16 graduation song using v1.2 features | Phase 27 graduation song using v1.3 features (D-304) | Phase 27 (this phase) | Replace the entire graduation song; no compatibility window. |

**Deprecated/outdated:**
- v1.2 ambient showcase (Aminor/72 BPM): replaced by v1.3 polyrhythmic-minimal piece per D-201. Pre-public, no legacy users.
- ROADMAP Phase 27 success criterion #1 wording: omits Phase 26.2 surface. D-101 expands at closure.

## Project Constraints (from CLAUDE.md)

- **Goals: Ergonomics first.** "Music production is historically slow... Flow prioritizes composer ergonomics over everything else." → Tutorial prose stays composer-friendly; jargon explained on first use; `(print)` outputs match the chapter's teaching intent.
- **Genre-agnostic.** Tutorial graduation song genre is planner's choice (D-304 closure note). Showcase is polyrhythmic-minimal per D-202; this is one genre, not "the" genre.
- **Non-Goals: Type strictness for its own sake.** `(volume buf 0.5)` accepts any non-negative `Double`; tutorial demonstrates without arguing the type system into the example.
- **C# Conventions: .NET 10, nullable enabled, file-scoped namespaces, record AST nodes.** Phase 27 ships zero new C# code beyond `Phase27ByteIdenticalPragmaTests.cs` — single test class, mirrors Phase 18 verbatim.
- **Functional S-expression style only — no infix operators.** Phase 27 chapter prose teaches the prefix-only rule (chapter 2 update D-302).
- **Charitable interpretation** (memory `feedback_charitable_interpretation`): silent-and-documented over magic. The gain/volume split chapter (D-102) is the canonical example — function name documents the unit; chapter explains why.
- **Pre-public, no legacy code.** D-201 (replace showcase) and D-304 (replace graduation song) follow this lean.
- **`(sub 0.0 N)` idiom for negative doubles** (carryover from Phase 12 D-19, Phase 14 D-19, Phase 16 D-17): tutorial uses this where a negative double literal is needed at expression-start. **However**, Phase 26 STD-02 + Phase 26.2 ERG-05 shipped single-token negative literals at expression-start (`-3.14`, `-12dB`), so the `(sub 0.0 N)` idiom is now MOSTLY obsolete in expression position. Existing tutorial chapter 18 prose explaining the idiom (lines 514-520) STAYS for backward-compatibility teaching; new chapters can use `-3.14` direct.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Phase 18 + Phase 25 byte-identical tests are content-agnostic two-run SequenceEqual gates, NOT pinned bytes. | Pitfall 1; Architectural Responsibility Map; Don't Hand-Roll | Risk LOW — verified directly by reading the test source files (Phase18/ByteIdenticalShowcaseTests.cs:78-83 + Phase25/ByteIdenticalShowcaseGaussianTests.cs:86-91). [VERIFIED] |
| A2 | Pragmas (`enable hAsB;` + `enable justIntonation;`) are file-scoped and do NOT propagate via `use`. | Pitfall 2 + Pitfall 6 | Risk LOW — verified by Phase 21 PragmaIsolationFacts:24-69 + Phase 21 RESEARCH D-02 + lexer pre-scan algorithm. [VERIFIED] |
| A3 | Dict iteration order is insertion-order across all 14 ops (each / map / filter / keys / values / merge). | Pitfall 4; Code Examples Dict | Risk LOW — verified by REQUIREMENTS DICT-03 + flow-lang/Runtime/Value.cs:106 references `OrderedDictionary<TKey, TValue>`. [VERIFIED] |
| A4 | Tied notes inside tuplets compile correctly but the BarRenderer 100ms tied-overlap may over-extend short tuplet notes. | Pitfall 3 | Risk MEDIUM — no Phase 19 fact directly tests `{N:M ... C4q~}q` tuplet+tied combinations. The IsTied flag flows through compilation; render-time overlap was authored pre-tuplets. Showcase plan should avoid the edge by NOT placing ties inside tuplets. [ASSUMED — recommend planner avoid tuplet-internal ties; if showcase needs them, add a smoke `.flow` script + fact before relying on the combination] |
| A5 | `(reverb Buffer, Double, Second)` overload (Phase 26.2 ERG-02) is NOT ambiguous with the existing `(reverb Buffer, Double, Double)` — different convertible scoring. | State of the Art table | Risk LOW — Phase 26.2 CONTEXT D-08 explicitly verifies cleanly via OverloadResolver exact-match; ERG-02 shipped at dfbfa1f. Tutorial chapter 16 inline weave (D-102) hits the music-typed form via exact match. [VERIFIED via Phase 26.2 closure SUMMARY] |
| A6 | Examples/output/.gitignore covers companion file outputs (`h_alias.{wav,mid}`, `microtonal_ji.{wav,mid}`) without modification. | Component Responsibilities | Risk LOW — verified by `cat examples/output/.gitignore` shows `*.wav` + `*.mid` patterns covering all suffixes. [VERIFIED] |
| A7 | The `Phase 18 ByteIdenticalTutorialTests` (which exists separately from ShowcaseTests) will continue to pass with the v1.3-expanded tutorial.flow as long as the file is deterministic. | Component Responsibilities | Risk LOW — same two-run pattern; tutorial.flow already uses `seed=42` for euclidean and `seed=42` for humanizeGaussian. New chapters (Symbols, Tuples, Dict) are deterministic by construction (no PRNG). [VERIFIED via test source read] |
| A8 | The `Phase18ByteIdenticalTutorialTests` writeWav/writeMidi path-rewrite (`source.Replace("examples/output/flow_tutorial.{ext}", "tests/output/...")`) will still match after Phase 27 rewrites tutorial.flow — i.e., the planner keeps the writeWav/writeMidi path strings inline (not via variable). | Pitfall 7 | Risk MEDIUM — Phase 16 hit this in IN-02; Phase 27 plan must instruct executor to keep path strings inline in writeWav/writeMidi calls. [ASSUMED — planner-enforceable] |
| A9 | The Music Types Quick Reference table for CLAUDE.md (D-104) lives under "Language Features → Music-Specific" — NOT under Special Types list. | Component Responsibilities | Risk LOW — CONTEXT D-104 says "appended to 'Language Features → Music-Specific'". CLAUDE.md already has Hertz in Special Types list (Phase 26.2 closure 86bdd15). The Music Types Quick Reference is a different, composer-facing table. [VERIFIED via CLAUDE.md Phase 26.2 closure SUMMARY description] |

## Open Questions (RESOLVED)

1. **Should tuplet ties be demonstrated in the music-batch chapter (D-303 sub-section "tuplets+fractional+tied-note interaction")?**
   - What we know: CONTEXT D-303 explicitly lists "tied-note interaction" as a sub-section.
   - What's unclear: Pitfall 3 surfaces a render-time edge (BarRenderer 100ms overlap on tied notes inside short tuplet members). No existing Phase 19 fact pins the combination.
   - Recommendation: Demo `| {3:2 C4 D4 E4~}q E4q |` (tie on LAST tuplet note crossing back to straight time) — safest form. Avoid `| C4q~ {3:2 ... } |` (tie INTO a tuplet) and `| {3:2 C4~ D4 E4}q |` (tie INSIDE a tuplet). Add a smoke `.flow` test in `tests/test_tuplet_tied.flow` if the demo lands in tutorial.
   - **RESOLVED:** Yes, only in safe form — last tuplet member crossing back to straight time. Demoed as `{3:2 C4 D4 E4~}q E4q` in tutorial 19.5.A. Ties INSIDE the bracket avoided per RESEARCH Pitfall 3.

2. **Does the v1.3 graduation song activate ANY microtonal pragma?**
   - What we know: CONTEXT D-304 closure note explicitly leaves to planner discretion; default NO pragma in tutorial.flow.
   - What's unclear: Whether a JI-tuned graduation song produces audibly meaningfully different output worth the file-scope pragma blast radius.
   - Recommendation: NO pragma in tutorial.flow. Microtonal demo is fully covered by `examples/pragmas/microtonal_ji.flow`. Tutorial graduation stays 12-TET; consistent with Phase 16 graduation precedent.
   - **RESOLVED:** NO. tutorial.flow stays 12-TET. Microtonal demo lives exclusively in examples/pragmas/microtonal_ji.flow per D-401/D-402.

3. **Does the Phase 27 plan ship a CHAPTER FOR `enable scaleLint;` even though flow-interpreter doesn't surface diagnostics?**
   - What we know: CONTEXT D-303 + D-501 — tutorial documents scale-lint print-only ("flow-lsp surfaces diagnostics; flow-interpreter does not").
   - What's unclear: Whether reading the chapter without flow-lsp open is meaningful for a beginner.
   - Recommendation: One short prose paragraph inside the music-batch microtonal+scale-lint sub-section. No demo code (it would be a no-op in flow-interpreter). Reader reads the prose, knows the feature exists, and finds it when they install flow-lsp.
   - **RESOLVED:** Yes, prose-only inside 19.5.B. flow-interpreter does not surface lint diagnostics — flow-lsp owns the surface. Tutorial documents this distinction explicitly.

4. **Does showcase use a tuplet-tied-note edge in the drum groove (`{3:2 _ C2 D2~}q C2`)?**
   - What we know: CONTEXT §specifics suggests a sketch `Sequence drumTriplets = | {3:2 _ kick _ }q kick {3:2 _ kick snare}q kick |` — no ties.
   - What's unclear: Whether the showcase needs a tied note for groove feel.
   - Recommendation: Avoid ties inside the drum tuplet. Drums are short; the 100ms tied-overlap edge would cause subtle voice-stacking that shifts mix levels. Use clean note articulation in the groove.
   - **RESOLVED:** NO. Showcase.flow tuplet groove uses no ties INSIDE the {3:2 ...}q brackets per RESEARCH Pitfall 3.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | flow-interpreter (`dotnet run --project flow-interpreter`) | ✓ (assumed — Phase 26.2 just shipped) | net10.0 | — |
| `dotnet test` runner | Phase27ByteIdenticalPragmaTests | ✓ | bundled with .NET 10 | — |
| flow-lang stdlib (`@std`, `@audio`, `@composition`, `@collections`) | tutorial.flow + showcase.flow + pragma companions | ✓ (Phase 26.2 head) | v1.3-rc | — |
| espeak-ng (for `tts` builtin) | tutorial chapter 19 prose mention only | not required to run | — | Tutorial chapter 19 mentions tts in prose without invoking it (existing pattern from Phase 16); skip-if-absent semantics. No fallback needed. |
| PulseAudio | runtime audio playback (`play`, `loop`, `preview`) | tutorial doesn't call playback builtins | — | tutorial uses writeWav/writeMidi (no PulseAudio dependency); audio is generated to file |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None.

## Validation Architecture

> Phase 27 is documentation-shaped. The "tests" are: (1) tutorial.flow + showcase.flow + companion files exit cleanly with non-empty WAV+MIDI; (2) two consecutive runs produce byte-identical output; (3) full unit suite stays GREEN. No new test framework, no new Wave 0 scaffolding beyond the new `Phase27ByteIdenticalPragmaTests` class.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (existing — flow-lang.Tests/flow-lang.Tests.csproj) |
| Config file | flow-lang.Tests/flow-lang.Tests.csproj (existing) |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase27"` |
| Full suite command | `dotnet test flow-lang.Tests --nologo` |
| Smoke run (tutorial) | `dotnet run --project flow-interpreter examples/tutorial.flow` |
| Smoke run (showcase) | `dotnet run --project flow-interpreter examples/showcase.flow` |
| Smoke run (companion) | `dotnet run --project flow-interpreter examples/pragmas/h_alias.flow` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| QOL-04 (a) | tutorial.flow demonstrates every v1.3 feature | grep + smoke | `grep -c "humanizeGaussian\|(dict\|<<.*>>\|#\\w\\+\|{3:2\\|/12\\|enable .*\|range\\|@-1\\|kHz\\|Hz\\|ms\\|dB\\|s)" examples/tutorial.flow` (expect ≥ N matches) + tutorial smoke run | ❌ Wave 0 needed (grep audit script) |
| QOL-04 (a) | showcase.flow demonstrates v1.3 feature audibly | smoke | `dotnet run --project flow-interpreter examples/showcase.flow` exit 0 + non-empty wav + non-empty mid | ❌ Wave 0 needed (smoke script) |
| QOL-04 (b) | tutorial.flow exits 0 + non-empty WAV + non-empty MIDI | smoke | `dotnet run --project flow-interpreter examples/tutorial.flow && [ -s examples/output/flow_tutorial.wav ] && [ -s examples/output/flow_tutorial.mid ]` | Existing (Phase 16 pattern); needs re-run after Phase 27 changes |
| QOL-04 (c) | tutorial.flow byte-identical across two runs | xUnit (existing) | `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalTutorial"` | ✅ Phase18/ByteIdenticalTutorialTests.cs (auto-follows new content) |
| QOL-04 (c) | showcase.flow byte-identical across two runs | xUnit (existing) | `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalShowcase\|FullyQualifiedName~Phase25.ByteIdenticalShowcaseGaussian"` | ✅ Phase18 + Phase25 (auto-follows new content) |
| QOL-04 (d) | h_alias.flow + microtonal_ji.flow byte-identical | xUnit (NEW) | `dotnet test --filter "FullyQualifiedName~Phase27.ByteIdenticalPragma"` | ❌ Phase27/Phase27ByteIdenticalPragmaTests.cs (NEW — Wave 3/4 task) |
| QOL-04 (e) | Existing v1.1+v1.2 chapters preserved | grep audit | `grep -cE "^Note: [0-9]+\\." examples/tutorial.flow` (expect chapter count ≥ 19, plus new chapters) | Existing |
| QOL-04 (f) | REQUIREMENTS QOL-04 rewrite includes Phase 26.2 surface (D-101) | grep | `grep -E "QOL-04.*volume\|QOL-04.*Hertz\|QOL-04.*Ms.*FX\|QOL-04.*Second.*reverb" .planning/REQUIREMENTS.md` (expect match) | ❌ Wave 5 closure task |
| Full suite GREEN | Zero regressions | xUnit | `dotnet test flow-lang.Tests --nologo` (expect 879+ passed / 0 failed / 0 skipped) | Existing |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Phase27"` (Phase 27 facts only, ~4 facts)
- **Per wave merge:** Phase 27 + Phase 18 + Phase 25 byte-identical sentinels (`dotnet test --filter "FullyQualifiedName~Phase18.ByteIdentical|FullyQualifiedName~Phase25.ByteIdenticalShowcase|FullyQualifiedName~Phase27"`)
- **Phase gate:** Full unit suite GREEN before `/gsd-verify-work` + tutorial.flow smoke + showcase.flow smoke + h_alias.flow smoke + microtonal_ji.flow smoke

### Wave 0 Gaps

- [ ] `flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs` — covers QOL-04 (d) byte-identical contract for companion files (D-403). Mirrors Phase18/ByteIdenticalShowcaseTests.cs structure verbatim.
- [ ] `examples/pragmas/h_alias.flow` — companion file (D-402); ~30 lines.
- [ ] `examples/pragmas/microtonal_ji.flow` — companion file (D-402); ~40 lines.
- [ ] (Optional) Plan-level shell snippet for tutorial-feature grep audit — single bash one-liner that confirms each v1.3 feature appears at least once in tutorial.flow. Lives in the closure plan's verification section, not as a tracked artifact.

*If the planner chooses to roll the new pragma tests into Phase 27's first plan (Wave 0), this becomes Phase 16's Plan-01 pattern (`examples/output/.gitignore + 5 new chapters`). Otherwise the test class lands in Wave 3 alongside the companion files.*

### Note on Nyquist Validation

`workflow.nyquist_validation` is `true` in `.planning/config.json`. This phase **is** subject to Nyquist validation, but the validation surface is small: the closure plan's VERIFICATION.md mirrors Phase 16-VERIFICATION.md's grep-table + smoke-transcript + commit-hash-manifest shape. The 4 new pragma facts auto-discoverable via `dotnet test --filter` count toward the validation set; the 4 existing showcase/tutorial byte-identical facts are regression sentinels (already in the pre-Phase-27 baseline).

## Sources

### Primary (HIGH confidence)

- `examples/tutorial.flow` (684 lines, current head) — direct read of every chapter to confirm structure + idioms.
- `examples/showcase.flow` (44 lines, current head) — direct read confirms v1.2 pattern to be replaced.
- `flow-lang/audio.flow` (520 lines) — direct read confirms every v1.3 audio builtin's forward decl (volume, gain Decibel, lowpass Hertz, delay Ms, compress Decibel+Ms, reverb Second, createXxxTone Hertz, etc.).
- `flow-lang/std.flow` lines 101-120 — direct read confirms unpack, dict, dictTuple, get/getOr/set/remove/has/keys/values/size/merge/each Dict / map Dict / filter Dict forward decls; humanizeGaussian at line 187.
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` (90 lines) — direct read confirms two-run SequenceEqual pattern, NO pinned bytes.
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` (112 lines) — direct read confirms same pattern for tutorial.
- `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` (99 lines) — direct read confirms verbatim mirror of Phase18 ShowcaseTests.
- `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` (191 lines) — direct read confirms this is the ONLY byte-identical test class with inline pinned bytes (3-byte velocity sequence at line 107), and it does NOT consume tutorial.flow / showcase.flow.
- `flow-lang.Tests/Integration/Phase21/PragmaIsolationFacts.cs` (70 lines) — direct read confirms PRAG-02 file-scope isolation: pragmas declared in modules do NOT propagate to importers.
- `flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs` lines 22-49 — direct read confirms top-of-file requirement, comments-and-blanks-allowed prefix region, fast-path zero-allocation.
- `tests/test_dict_construct.flow` + `test_tuple_literal.flow` + `test_tuple_destructure.flow` + `test_h_alias.flow` + `test_tuning_ji.flow` — direct read confirms canonical syntax for each feature.
- `.planning/phases/27-tutorial-showcase-refresh/27-CONTEXT.md` — strategic decisions D-101..D-503.
- `.planning/phases/16-tutorial-refresh/16-CONTEXT.md` + `16-SUMMARY.md` + `16-VERIFICATION.md` + `16-REVIEW-FIX.md` — Phase 16 precedent (D-01..D-09 patterns + 5-plan structure + REVIEW-FIX gotchas).
- `.planning/phases/26.1-symbols-tuples-dicts/26.1-06-SUMMARY.md` — closure pattern for REQUIREMENTS author-at-closure (DICT-01/02/03 rewrite).
- `.planning/phases/26.2-music-type-ergonomics-fx-overloads-inserted/26.2-06-SUMMARY.md` — most recent closure pattern (D-101 + D-104 + D-204 follow this).
- `.planning/phases/26.2-music-type-ergonomics-fx-overloads-inserted/26.2-CONTEXT.md` — Phase 26.2 D-04..D-13 (gain/volume + Hertz + Ms-FX overload semantics).
- `.planning/REQUIREMENTS.md` — direct read of QOL-04 entry + ERG-01..ERG-05 entries + DICT-01/02/03 entries + DEFER-04..06 + TUP-01..08 + STD-01..03.
- `.planning/ROADMAP.md` lines 51-310 — Phase 27 scope + dependencies + success criteria + progress table + Phase 26.2 closure annotation (single source of truth on Phase 26.2 surface).
- `.planning/STATE.md` lines 1-40 — current focus = Phase 27, milestone progress 11/12.
- `flow-lang.Tests/FlowScriptData.cs:1-65` — auto-discovery of `tests/*.flow` test scripts; explains why companion files at `examples/pragmas/` need explicit test class (not auto-discovered).
- `flow-lang/StandardLibrary/Audio/MidiExport.cs:25-50` + `flow-lang/TypeSystem/Fraction.cs:15` — verified TPQN cap = 9600 (TUP-06 D-USER-E).
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs:79-86` — verified tied-note 100ms render-overlap behavior (Pitfall 3 source).
- `flow-lang/Runtime/Value.cs:106` — verified `OrderedDictionary<TKey, TValue>` backs DictData (Pitfall 4 source).
- `examples/output/.gitignore` — direct read confirms `*.wav` + `*.mid` rule covers all suffixes (Architectural Responsibility Map).

### Secondary (MEDIUM confidence)

- CLAUDE.md content quoted at top of this research session — lists Phase 26.1 + 26.2 features in Core Language Features bullets. Confidence MEDIUM because the quote is a snapshot; live CLAUDE.md is the authoritative version.

### Tertiary (LOW confidence)

- None. Every claim is verified against direct file read or pinned to a Phase X-SUMMARY closure record.

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — zero new dependencies; everything ships from existing flow-lang stdlib.
- Architecture: HIGH — Phase 16 precedent is direct (5-plan structure, examples/output/ + gitignore, single graduation Song dual-export, two-run byte-identical contract).
- Pitfalls: HIGH for Pitfalls 1, 2, 4, 5, 6, 7 (each verified against test source or implementation file). MEDIUM for Pitfall 3 (tuplet+tied interaction lacks dedicated facts; recommendation is to AVOID rather than verify).
- Code Examples: HIGH — every snippet verified against existing `tests/test_*.flow` exemplars.
- Validation Architecture: HIGH — mirrors Phase 16 + Phase 26.2 verbatim with a single new test class.

**Research date:** 2026-05-10
**Valid until:** 2026-06-10 (30 days — stable surface; Phase 26.2 just closed and no upstream changes are pending in v1.3 scope)

## RESEARCH COMPLETE
