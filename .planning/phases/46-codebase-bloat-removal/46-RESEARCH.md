# Phase 46: Codebase Bloat Removal - Research

**Researched:** 2026-05-30
**Domain:** Mechanical C# / Flow-stdlib cleanup (pure removal/redirect, no behavior change)
**Confidence:** HIGH (every line range, caller list, and redirect target verified by direct grep/read against the current `dev` tree, not the audit's numbers)

## Summary

This is a **mechanical implementation map**, not a scoping exercise. CONTEXT.md decisions D-01..D-19 are LOCKED; my job was to verify the audit's claims against the CURRENT tree and hand the planner exact `file:line` ranges, complete in-repo caller lists, and per-target verification mechanics.

Verifying against the live tree surfaced **three material corrections to the audit** that the planner MUST act on:

1. **D-04 (Fixtures case-collision) is ALREADY RESOLVED in `flow-lang.Tests`.** Commit `e0d7274` (squashed into `5f61a1e`, 2026-05-26 — two days AFTER the 2026-05-24 audit) already merged `Fixtures/` → lowercase `fixtures/` during Phase 44. The current tree has only `flow-lang.Tests/fixtures/` (lowercase) in git AND working tree; every C# path string already uses `"fixtures"`. The only residual capital `/Fixtures/` git path is `flow-midi.Tests/Fixtures/MidiFixtureBuilder.cs` — a *different project*, single code file, **no lowercase counterpart → no collision**. D-04 in `flow-lang.Tests` is a **NO-OP / already-done**; the planner should reduce D-04 to a one-task *verification* (confirm no capital-F path strings remain) plus an optional decision on whether to touch `flow-midi.Tests`.

2. **D-09 (Phase35 diagnostics baselines) FAILS its confirm-gate — the `.txt` files are LIVE-READ.** CONTEXT D-09 explicitly conditioned removal on "confirming `DiagnosticRendererGoldenTests.cs` uses inline golden assertions and does NOT read the `.txt` files." That confirmation **fails**: `DiagnosticRendererGoldenTests.cs:39` calls `File.ReadAllText(path)`, and `:77`/`:116` call `ReadBaseline("unknown_identifier.txt")` / `ReadBaseline("type_mismatch.txt")`. The `.csproj` (lines 95-102) has a live `CopyToOutputDirectory` rule for them. **D-09 must become a KEEP** — removing the files breaks two `[Fact]`s.

3. **D-03 (NoteSynthesizer → SynthUtils redirect) carries a real byte-divergence risk the audit understated.** The inline oscillator loops compute phase as `(frequency * t) % 1.0` (sine: `Math.Sin(2π·f·t)`) recomputed fresh from integer sample index `i` each sample, whereas `SynthUtils.Generate*` use **incremental phase accumulation** (`phase += phaseInc; if (phase >= 1.0) phase -= 1.0`). These are equal in exact arithmetic but **NOT bit-identical in IEEE-754 double** — accumulated rounding diverges. Because the generators are also composer-callable builtins (`generateSine`/etc.), and because NO existing test does an exact byte-comparison of `SineSynthesizer.RenderNote` output (only RMS-tolerance baselines + same-code two-run determinism), a naive redirect could silently change rendered bytes and pass the locked gate. **D-03 needs an explicit before/after byte-comparison guard** (see Validation Architecture + Risk Callouts).

Everything else (D-02, D-05, D-06, D-07, D-08, D-12, D-16) is verified clean and mapped below with exact line ranges and caller lists.

**Primary recommendation:** Order = D-04 verify (cheap, de-risks the "highest priority" item that's already done) → D-09 KEEP confirmation → D-02/D-05/D-06/D-07/D-08 removals (each atomic) → D-03 redirect LAST behind a new exact-byte guard → D-12 invest → D-16 doc-notes. Gate every commit on full `flow-lang.Tests` + all `tests/test_*.flow` + Phase 28 RMS baselines + two-run cmp-clean.

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01 (LOAD-BEARING):** Removal justified ONLY for (a) genuinely unreachable internal plumbing OR (b) pure redundancy where a strictly-better equivalent exists. **Composer-callable features STAY even at zero local usage.** Overrides any audit/roadmap "locked removal" justified only by low usage.
- **D-02:** REMOVE TimelineMap editor-highlighting stack (`TimelineMap.cs` + `RenderSongWithTimeline`/`RenderSectionWithTimeline` + parallel TimelineMap-flavored overloads in BarRenderer/SequenceRenderer). Zero callers; not composer-reachable.
- **D-03:** REMOVE/redirect NoteSynthesizer private duplicate helpers (4× `BeatsToSeconds`+`CreateSilence` + inline oscillator loops) → route through `SynthUtils.*`. **Oscillator math MUST stay byte-identical.**
- **D-04:** MERGE `Fixtures/` + `fixtures/` case-collision to lowercase `fixtures/`. *(See Correction #1 — already done in flow-lang.Tests.)*
- **D-05:** REMOVE the dead `internal proc` createSineTone forward-decls in `audio.flow:224,227` ONLY. The stereo Flow proc wrappers (`audio.flow:352-411`) are UNTOUCHED.
- **D-06:** REMOVE `exportWav` legacy alias. Migrate ~5 test callers to `writeWav`; drop `ExportWav`/`ExportWavWithBitDepth` shims in `FileIO.cs`.
- **D-07:** REMOVE `test.flow` legacy assertion half (lines 30-136). Port `tests/test_test_library.flow` to the Phase 35 `@test` surface.
- **D-08:** INLINE `ClampSamples` thin-wrapper shims → direct `AudioUtils.ClampSamples()` calls.
- **D-09:** REMOVE Phase35/diagnostics/*.txt orphaned baselines — *only after* confirming the golden test does not read them. *(See Correction #2 — confirmation FAILS, so KEEP.)*
- **D-10..D-15:** KEEP Track/Timeline, bars.flow, Progression DSL (+invest D-12), OscillatorState/Envelope, audio.flow buffer convenience, `preview`.
- **D-16:** Add short "legacy / superseded by X — kept as a usable surface" notes to Track/Timeline + bars.flow source/docs. **No deprecation warnings, no stderr advisories.**
- **D-17:** Scope = audit §1 + §2, filtered by D-01. §3 low-confidence calls are OUT.
- **D-18:** Atomic commit per target. One test-green gate. Zero behavior change.
- **D-19:** Pre-traction single-commit removals, callers ported in-place, no migrators.

### Claude's Discretion
- Ordering of cleanup targets (CONTEXT suggests D-04 first as risk reducer).
- Whether D-09's confirm-grep is its own task or folded in.
- Exact wording/placement of D-16 legacy doc notes.
- Verification mechanics beyond the locked test-green gate.

### Deferred Ideas (OUT OF SCOPE)
- flow-lsp editor live-highlighting (the feature TimelineMap was scaffolding for).
- §3.2 conversion-proc unification (frames/beats/seconds).
- §2.6 FlowFunctionSynthesizer inlining / §2.7 IFunctionInvoker.

## Phase Requirements

> No formal REQ IDs were assigned at roadmap time (REQUIREMENTS.md:535 only lists an unrelated Pidgin-removal opportunistic note). Per the objective, concrete coverage is derived from the confirmed removals/keeps. The planner should mint REQ IDs of the form `CLEAN-NN` mapped 1:1 to the decisions below.

| Derived REQ | Decision | Research Support |
|----|----|----|
| CLEAN-02 | D-02 TimelineMap removal | §D-02 map below — exact ranges, zero external callers verified |
| CLEAN-03 | D-03 NoteSynthesizer redirect | §D-03 map — SynthUtils signatures + byte-divergence guard |
| CLEAN-04 | D-04 Fixtures verify | §D-04 — already resolved; reduce to verification task |
| CLEAN-05 | D-05 audio.flow internal decls | §D-05 — exactly 2 decls at 224, 227 |
| CLEAN-06 | D-06 exportWav removal | §D-06 — 7 caller sites + FileIO shims + arg-order |
| CLEAN-07 | D-07 test.flow legacy half | §D-07 — port semantics (FAIL-case inversion) |
| CLEAN-08 | D-08 ClampSamples inline | §D-08 — 2 shims, 3 callsites |
| CLEAN-09 | D-09 diagnostics KEEP | §D-09 — confirm-gate fails; no-op |
| CLEAN-12 | D-12 Progression invest | §D-12 — test idiom + showcase target |
| CLEAN-16 | D-16 legacy doc-notes | §D-16 — exact source locations |

## Standard Stack

No new packages. This phase removes/redirects existing code only. Build/test stack unchanged:
- .NET 10 / C# 13, `dotnet build`, `dotnet test`
- Master Flow-script suite: `flow-lang.Tests/FlowScriptData.cs` auto-discovers `tests/**/*.flow` via `Directory.EnumerateFiles(..., "*.flow", SearchOption.AllDirectories)` and runs each through `FlowEngineRunner.RunFile` asserting success.
- Shell loop: `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done`

**Package Legitimacy Audit:** N/A — no external packages installed in this phase.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Note→buffer synthesis (D-03) | Library (`StandardLibrary/Audio`) | — | Synth math lives in flow-lang; redirect is intra-tier |
| Render-path timeline plumbing (D-02) | Library renderers | (intended: flow-lsp, never wired) | Removing dead editor-highlighting plumbing |
| WAV export surface (D-06) | Library (`FileIO`) | — | `writeWav`/`exportWav` both delegate to one private impl |
| Test assertion stdlib (D-07) | Flow stdlib (`test.flow`) + C# `TestFramework` | flow-cli `test` runner | Legacy Flow half removed; C# `@test` surface kept |
| Playback sample clamping (D-08) | Library (`Audio`, `PlaybackFunctions`) | — | Inline a same-namespace helper |

## Per-Target Implementation Map

### D-02 — TimelineMap editor-highlighting stack (REMOVE)

**Verified:** Zero callers outside the removable surface itself. `TimelineMap`/`TimelineEntry` are referenced ONLY by the parallel `*WithTimeline` overloads and the timeline-flavored renderer overloads — no `flow-lsp`, `flow-interpreter`, `flow-cli`, or `flow-lang.Tests` caller. `RenderSongWithTimeline` is `public` but unreferenced repo-wide.

**Files + exact ranges (delete):**
| File | Range | Content |
|------|-------|---------|
| `flow-lang/Audio/TimelineMap.cs` | whole file (1-67) | `TimelineEntry` record + `TimelineMap` class. Delete the file. |
| `flow-lang/StandardLibrary/Audio/SongRenderer.cs` | 450-554 | `RenderSongWithTimeline` (453-505) + `RenderSectionWithTimeline` (510-554, `private`). Delete both incl. the `/// <summary>` at 450-452 and 507-509. |
| `flow-lang/StandardLibrary/Audio/BarRenderer.cs` | 305-389 | Timeline-aware overloads: `RenderBarToVoices(...,TimelineMap,...)` (309-329), `RenderBarAtBeat(...,TimelineMap,...)` string+synth pair (331-389). Delete the four timeline-threading overloads; the non-timeline `RenderBarToVoices`/`RenderBarAtBeat` (above line 305) STAY. |
| `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` | 123-165 | Timeline-aware `RenderSequenceToVoices(...,TimelineMap,...)` string+synth pair. The non-timeline overloads (above 123) STAY. |

**KEEP (do NOT touch):** `BarType.ToTimeline()` (`flow-lang/TypeSystem/SpecialTypes/BarType.cs:182`) and `SequenceType.ToTimeline()` (`SequenceType.cs:46`) — these are the *primary* render path's beat-offset projection and are used at `BarRenderer.cs:81,362`, `VisualizationFunctions.cs:53`, etc. The name "Timeline" overlaps but `ToTimeline()` is unrelated to `TimelineMap`.

**Verification:** After removal, `grep -rn "TimelineMap\|TimelineEntry" flow-lang flow-interpreter flow-cli flow-lsp flow-lang.Tests` must return zero. Build + full suite green (the primary render path is unchanged → byte-identical).

---

### D-03 — NoteSynthesizer private duplicate helpers (REDIRECT to SynthUtils) ⚠️ BYTE-RISK

**File:** `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs`

**Current structure (verified):** 4 synth classes, each with an inline oscillator loop + a `private BeatsToSeconds` + a `private CreateSilence`:
| Class | RenderNote body | inline osc loop | `BeatsToSeconds` | `CreateSilence` |
|-------|----------------|-----------------|------------------|-----------------|
| `SineSynthesizer` | 26-46 | 38-43 | 48-51 | 53-58 |
| `SawSynthesizer` | 66-87 | 78-84 | 89-92 | 94-99 |
| `SquareSynthesizer` | 107-128 | 119-125 | 130-133 | 135-140 |
| `TriangleSynthesizer` | 148-169 | 160-166 | 171-174 | 176-181 |

> Audit said "lines 24-182" — verified accurate. (`FlowFunctionSynthesizer` at 188-208 is the D-deferred §2.6 item — DO NOT touch.)

**Redirect targets (verified signatures in `flow-lang/StandardLibrary/Audio/SynthUtils.cs`):**
```csharp
SynthUtils.BeatsToSeconds(double beats, double bpm)                                    // :32  — IDENTICAL body to the private ones
SynthUtils.CreateSilence(int sampleRate, double durationBeats, double bpm)             // :40  — IDENTICAL body to the private ones
SynthUtils.GenerateSine(float[] buffer, double freq, double amp, int sr, double startPhase = 0.0)      // :50  — additive (+=), incremental phase
SynthUtils.GenerateSaw(float[] buffer, double freq, double amp, int sr, double startPhase = 0.0)       // :67
SynthUtils.GenerateSquare(float[] buffer, double freq, double amp, int sr, double startPhase = 0.0)    // :85
SynthUtils.GenerateTriangle(float[] buffer, double freq, double amp, int sr, double startPhase = 0.0)  // :103
SynthUtils.ToMonoBuffer(float[] samples, int sampleRate)                               // :248 — wraps a fresh float[] into a 1-channel AudioBuffer
```

**`BeatsToSeconds`/`CreateSilence` redirect is SAFE — byte-identical by inspection.** The private bodies are character-for-character identical to `SynthUtils.BeatsToSeconds` (`(beats/bpm)*60.0`) and `SynthUtils.CreateSilence` (same int-truncation). Replace all 4 private pairs with `SynthUtils.BeatsToSeconds`/`SynthUtils.CreateSilence` calls — zero risk.

**⚠️ The oscillator-loop redirect is the ONE byte-divergence risk in the phase.** The inline loops compute, per sample `i`:
- Sine: `Math.Sin(2.0 * Math.PI * frequency * t)` where `t = i / (double)sampleRate`
- Saw/Square/Triangle: `phase = (frequency * t) % 1.0` recomputed fresh from `t` each sample

`SynthUtils.Generate*` instead **accumulate** phase: `phase += phaseInc; if (phase >= 1.0) phase -= 1.0` (sine: `phase += 2π·f/sr`). In exact arithmetic these are equal, but in IEEE-754 `double` the accumulated `+=` rounds differently than the fresh `(f·t)%1.0` / `2π·f·t` — samples can differ by ±1 ULP, producing **different WAV bytes**. SynthUtils is also **additive** (`+=`), so the redirect must write into a `new float[numSamples]` (zero-initialized) exactly as the audit's fix-shape says:
```csharp
var samples = new float[numSamples];
SynthUtils.GenerateSine(samples, frequency, amplitude, sampleRate);   // startPhase defaults 0.0
return SynthUtils.ToMonoBuffer(samples, sampleRate);
```

**How to discharge the risk (planner MUST include one of these as a task):**
- **Preferred:** Add a NEW xUnit Fact that renders each of the 4 synths via `SynthesizerFactory.Create("sine"|"saw"|"square"|"triangle").RenderNote(...)` and `Assert.Equal` (exact `float[]`/byte compare) against a baseline captured from the PRE-redirect `dev` build. Capture the baseline BEFORE editing NoteSynthesizer.cs. If the redirect diverges, the Fact fails and the planner knows to keep the inline math (or accept an RMS-only regression with an `overrideReason`, per the Phase 28 RMS contract).
- **Fallback if divergence is confirmed:** keep the per-sample inline loops (they ARE the byte contract) and only redirect `BeatsToSeconds`+`CreateSilence` (the safe half). This still removes ~32 LOC (8 private helpers) with zero risk and satisfies D-03's "redirect duplicate helpers" intent; document that the oscillator loops stay inline because SynthUtils' accumulation diverges.

**Existing coverage is INSUFFICIENT to catch this automatically:** `SynthesizerFactoryTests.cs` only asserts dispatch *type*; Phase 28 RMS baselines pass at ±0.5 dB tolerance (would not flag a ±1-ULP shift); two-run cmp-clean compares same-code-to-same-code (cannot catch before-vs-after). Hence the explicit guard above.

---

### D-04 — Fixtures/ + fixtures/ case-collision (VERIFY — already resolved)

**MAJOR CORRECTION: this was already fixed.** Commit `e0d7274` ("refactor(tests): merge Fixtures/ + fixtures/ into single lowercase fixtures/ directory", squashed into `5f61a1e`, dated 2026-05-26) performed the merge during Phase 44 — two days after the 2026-05-24 audit. STATE.md:809 (written 2026-05-24) and the audit both predate the fix and are stale on this point.

**Current verified state:**
- `git ls-files flow-lang.Tests` → only `flow-lang.Tests/fixtures/` (lowercase, 29 tracked files). No capital `Fixtures/` data dir in git or working tree.
- All C# path strings already use lowercase `"fixtures"` (verified: `Phase29BaselineRecorder.cs:145`, `HarmonicRichnessTests.cs:58`, `ScalaParserFacts.cs:41`, `Phase37Fixtures.cs:45`, `SfzParserTests.cs:53`, `Midi2FlowRoundTripTests.cs:14`, etc.). `grep '"Fixtures"\|/Fixtures/' --include=*.cs flow-lang.Tests` returns **nothing**.
- `flow-lang.Tests.csproj` `<None Update>` entries use lowercase `fixtures\midi\...`.
- The `FlowLang.Tests.Fixtures` *C# namespace* (capital, in `using FlowLang.Tests.Fixtures;`) is UNRELATED — its file lives at `flow-lang.Tests/fixtures/FlowEngineRunner.cs` (lowercase dir, capital namespace). Namespaces are case-insensitive in C#; this is not a filesystem collision.
- The capital `bin/Debug/net10.0/Fixtures` + `bin/.../fixtures` are **gitignored stale build artifacts** (`git check-ignore` confirms). They regenerate; not a source concern.

**Residual:** The only capital `/Fixtures/` git path is `flow-midi.Tests/Fixtures/MidiFixtureBuilder.cs` (a separate test project). `flow-midi.Tests/` has NO lowercase `fixtures/` → no collision there either.

**Planner action (reduce D-04 to a verification task):**
1. Assert no capital-F path strings remain in `flow-lang.Tests`: `grep -rn '"Fixtures"\|"Fixtures/\|/Fixtures/' --include="*.cs" flow-lang.Tests` returns empty.
2. Assert no capital `flow-lang.Tests/Fixtures/` git path: `git ls-files | grep 'flow-lang.Tests/Fixtures/'` returns empty.
3. (Optional, planner discretion) Decide whether `flow-midi.Tests/Fixtures/` warrants lowercasing for project-wide consistency. It is NOT a collision (no sibling lowercase dir), so it does not satisfy D-01's "latent FS bug" justification on its own. Recommend documenting as out-of-scope-but-noted rather than touching it.

This converts the "highest priority latent bug" into a green checkmark — a genuine risk reduction.

---

### D-05 — audio.flow dead internal createSineTone forward-decls (REMOVE)

**File:** `flow-lang/audio.flow`

**Verified — exactly 2 internal decls (audit's "16 declarations" claim was wrong):**
```
224: internal proc createSineTone(Double: duration, Double: freq, Double: amp)
227: internal proc createSineTone(Double: duration, Hertz: freqHz, Double: amplitude)
```
Saw/Square/Triangle have **NO** internal decls — only the stereo proc wrappers. Delete lines 224 and 227 (and their lead-in `Note:` comments at 222-226 as appropriate; line 222 `internal proc noteToFrequency` is a DIFFERENT decl — keep it).

**Why safe (mechanism verified):** An `internal proc` decl is what registers the C# builtin into the Flow function table (`Interpreter.cs:845-859` → `InternalRegistry.TryGetImplementation` → `DeclareFunction`). The C# `createSineTone` builtins (registered at `BuiltInFunctions.cs:767-777` → `SignalGeneration.CreateSineTone`, which produces **mono**) are only *declared into Flow scope* via these two `internal proc` lines. The composer-facing stereo proc wrappers at `audio.flow:352` (`Double,Double,Double`) and `:365` (`Double,Hertz,Double`) have the SAME signatures and are declared LATER in the file, so they shadow the internal decls regardless. Removing lines 224/227 makes the mono C# builtin unreachable from Flow (intended dead-weight removal); the proc wrappers (which call `createBufferStereo` + `generateSine` + `createOsc`, NOT the C# builtin) are unaffected.

**All `createSineTone` callers (verified) get the stereo wrapper, unaffected:** 20+ sites across `tests/*.flow` (test_mix, test_panning, test_writewav, test_wav_loading, test_sidechain, test_gain_context, test_dx_*, etc.) and `examples/dsp/*.flow`, plus C# Facts (`HertzFXOverloadFacts`, `VolumeFunctionFacts`, `DelaySyncFacts`, `LoadWavVarispeedFacts`, etc.). None call the mono path directly.

**Planner choice (note, not blocking):** The audit's option (a) "drop internal decls" is locked by D-05. Whether to ALSO unregister the now-dead C# `SignalGeneration.CreateSineTone` builtin (`BuiltInFunctions.cs:767-777`) is optional — leaving it registered-but-undeclared is harmless dead code; removing it is a cleaner finish but widens the diff. Recommend leaving the C# registration in place this phase (D-05 says "internal proc decls ONLY") to keep the commit minimal.

**Verification:** Full `.flow` suite still prints all `createSineTone` PASS sentinels (proves the stereo wrapper still resolves); two-run cmp-clean on any sine-using script unchanged.

---

### D-06 — exportWav legacy alias (REMOVE, migrate callers to writeWav)

**Registration to delete:** `flow-lang/StandardLibrary/BuiltInFunctions.cs:709-721`
```
709-714: exportWav(Buffer, String)        → Audio.FileIO.ExportWav
716-721: exportWav(Buffer, String, Int)   → Audio.FileIO.ExportWavWithBitDepth
```
**Internal proc decls to delete:** `flow-lang/audio.flow:38` and `:41`.
**Shims to delete in `flow-lang/StandardLibrary/Audio/FileIO.cs`:**
- `ExportWav` (30-37)
- `ExportWavWithBitDepth` (42-50)

**KEEP:** `WriteWav` (272-278), `WriteWavWithBitDepth` (283-290), and the shared private `ExportWavInternal` (55+). Note: `WriteWav`/`WriteWavWithBitDepth` already delegate to `ExportWavInternal` — do NOT rename `ExportWavInternal` (it's the shared core, not the alias).

**Arg-order difference (verified):**
- `exportWav(buffer, filepath[, bitDepth])` — buffer-first (`FileIO.cs:32-33`)
- `writeWav(filepath, buffer[, bitDepth])` — path-first (`FileIO.cs:274-275`)
- Migration = swap the two args. All callers are 2-arg (no 3-arg bitDepth caller exists in the repo).

**Complete caller list (source-tree, excludes regenerable `bin/` copies):**
| File:Line | Current call | Migrate to |
|-----------|--------------|-----------|
| `tests/test_full_song.flow:159` | `(exportWav mixed outPath)` | `(writeWav outPath mixed)` |
| `tests/demo_feature_showcase.flow:237` | `(exportWav final "feature_showcase.wav")` | `(writeWav "feature_showcase.wav" final)` |
| `tests/test_section_bare_expr.flow:20` | `(exportWav rendered "/tmp/test_bare_expr_output.wav")` | swap |
| `tests/test_section_gain_bare_expr.flow:34,35` | `(exportWav rendered1 "...")` ×2 | swap each |
| `tests/test_wav_loading.flow:6` | `(exportWav original "tests/test_output_roundtrip.wav")` | swap |
| `tests/test_writewav.flow:13` (+comments 1,12,14,23) | `(exportWav testBuf "...")` | **REWRITE — see below** |
| `flow-lang.Tests/FlowScriptData.cs:119-126` | per-script expected-output map for `test_writewav.flow` | **REWRITE — see below** |

**Special handling — `test_writewav.flow` + `FlowScriptData.cs` ASSERT the alias exists:**
- `tests/test_writewav.flow` is literally titled "Test writeWav (path-first) and exportWav (backwards compat)" and at line 13-14 prints `"PASS: exportWav(Buffer, String) backwards compat succeeded"`. This file's *purpose* is the alias. After removal, rewrite it to test only `writeWav` (drop the exportWav half), OR delete the exportWav-specific assertions.
- `FlowScriptData.cs:119-126` has an expected-substring entry pinning `"PASS: exportWav(Buffer, String) backwards compat succeeded"` for `test_writewav.flow`. This expected-output map entry MUST be updated to match the rewritten file's output, or the auto-discovered FlowScript Fact will fail.

**Verification:** Build (C# shims gone) + full `.flow` suite green (all migrated scripts still export WAV and print their PASS sentinels) + `FlowScriptData` expected-output map consistent + `grep -rn exportWav flow-lang flow-lang.Tests tests examples` returns only intentional comment residue (ideally zero).

---

### D-07 — test.flow legacy assertion half (REMOVE, port the one consumer)

**File:** `flow-lang/test.flow`

**KEEP (lines 1-29):** the `@test` module surface — `module test` (21) + the 6 `internal proc` decls (23-28: `test`, `assert`, `assertEq`, `assertNotesMatch`, `assertBytesEqual`, `assertWithinDb`). These map to the C# `TestFramework.TestFunctions` registrations.

**DELETE (lines 30-136):** the legacy pure-Flow library — `use "@std"`/`use "@collections"` (34-35, IF unused after deletion — verify), `notBool` (41-43), `printResult` (51-56), `assertTrue`/`assertFalse`/`assertEqual`/`assertNotEqual`/`assertLess`/`assertGreater`/`assertLessOrEqual`/`assertGreaterOrEqual` (61-98), `assertApproxEqual` (101-105), `runTest` (117-122), `summary` (127-136). (Note: `use "@std"`/`use "@collections"` at 34-35 are used only by the legacy half — delete them too; the `@test` surface at 1-29 needs no imports.)

**Sole consumer to port:** `tests/test_test_library.flow` (46 lines). Uses `assertTrue`/`assertFalse`/`assertEqual`/`assertNotEqual`/`assertLess`/`assertGreater`/`assertLessOrEqual`/`assertGreaterOrEqual`/`assertApproxEqual`/`runTest`/`summary`.

**Port semantics — NON-TRIVIAL (the assertion contracts differ):**
- Legacy assertions RETURN `Bool` and PRINT PASS/FAIL; the file deliberately exercises **9 FAIL cases** (e.g. `(assertTrue false ...)`, `(assertEqual 2 3 ...)`) expecting them to print FAIL and continue.
- Phase 35 `@test` assertions **THROW `AssertionException` on failure** (verified `TestFunctions.cs:60-115` → `AssertionHelpers.Assert*OrThrow`). A direct swap of the FAIL cases to `assertEq` would FAIL the test run.
- **Port shape:** wrap each test body in `(test "name" lazy(...))`. Convert each negative/FAIL case into a POSITIVE assertion of the negation using the registered `not` builtin (`BuiltInFunctions.cs:1139` — note the legacy file's claim "the interpreter does not register a built-in `not`" is STALE; `not` IS registered). E.g.:
  - `(assertTrue false ...)` → `(test "assertTrue FAIL case" lazy((assert (not false))))`
  - `(assertEqual 2 3 ...)` → `(test "assertEq int FAIL case" lazy((assert (not (equals 2 3)))))`
  - `(assertApproxEqual 1.0 1.5 0.001 ...)` → assert `(gt (abs (sub 1.0 1.5)) 0.001)` is true.
- Drop `runTest`/`summary` (no `@test` analog needed — the C# TestRunner walks the registry). End the ported file with a sentinel `(print "ALL TESTS REGISTERED")` (mirror `tests/test_test_framework.flow:36`).

**Execution-model note (validation):** `FlowScriptData.cs` auto-discovers `test_test_library.flow` and runs it via `FlowEngineRunner.RunFile` (the `flow-interpreter` path), which only REGISTERS `(test ...)` bodies and does NOT force them — so the ported file passes the C# suite as long as it returns success + prints its sentinel. To actually EXECUTE the bodies use `dotnet run --project flow-cli -- test tests/test_test_library.flow` (the `TestCommand` runner, `flow-cli/Commands/TestCommand.cs:47,79`). The planner should add a verification step that runs the ported file through `flow-cli test` to confirm all assertions pass (not just register).

**Reference port target:** `tests/test_test_framework.flow` is the canonical `@test`-surface example to mirror (same `use "@test"` + `(test "name" lazy(body))` + sentinel pattern).

---

### D-08 — ClampSamples thin-wrapper shims (INLINE)

**Shims to delete:**
- `flow-lang/Audio/PulseAudioSimpleBackend.cs:271` — `private static float[] ClampSamples(float[] samples) => AudioUtils.ClampSamples(samples);` (+ the `/// Delegates...` comment at 269-270)
- `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs:374` — same shim (+ comment 372-373)

**Callsites to rewrite (replace `ClampSamples(x)` → `AudioUtils.ClampSamples(x)`):**
- `PulseAudioSimpleBackend.cs:97` — `var clamped = ClampSamples(samples);`
- `PlaybackFunctions.cs:189` — `var clamped = ClampSamples(buffer.Data);`
- `PlaybackFunctions.cs:243` — `var clamped = ClampSamples(buffer.Data);`

**Namespace/using verified:**
- `PulseAudioSimpleBackend.cs` is in `namespace FlowLang.Audio;` — SAME namespace as `AudioUtils` (`flow-lang/Audio/AudioUtils.cs:1` → `namespace FlowLang.Audio;`). No `using` needed; `AudioUtils.ClampSamples` resolves directly.
- `PlaybackFunctions.cs` already has `using FlowLang.Audio;` (line 1). Resolves directly.
- `AudioUtils.ClampSamples(float[] samples)` signature confirmed at `AudioUtils.cs:12`.
- `CoreAudioBackend.cs:149` already calls `AudioUtils.ClampSamples` directly (no shim) — the precedent and proof the inlined form is correct.

**Verification:** Build + full suite green. Pure indirection removal — no behavior change, no byte impact (same `AudioUtils.ClampSamples` is invoked).

---

### D-09 — Phase35/diagnostics baselines (KEEP — confirm-gate FAILS)

**CONTEXT D-09 conditioned removal on confirming the golden test does NOT read the .txt files. The confirmation FAILS.**

`flow-lang.Tests/Phase35/DiagnosticRendererGoldenTests.cs` (verified):
- `:34-39` — `ReadBaseline(name)` → `Path.Combine(BaselineDir, name)` → `File.ReadAllText(path).Replace("\r\n","\n").TrimEnd('\n')`. `BaselineDir = AppContext.BaseDirectory/baselines/Phase35/diagnostics` (`:25`).
- `:77` — `var expected = ReadBaseline("unknown_identifier.txt");` then `Assert.Equal(expected, actual)`.
- `:116` — `var expected = ReadBaseline("type_mismatch.txt");` then `Assert.Equal(expected, actual)`.
- `flow-lang.Tests.csproj:95-102` — live `<None Include="baselines\Phase35\diagnostics\*.txt"><CopyToOutputDirectory>` rule copies them to `bin/`.

The files (`type_mismatch.txt` 214 B, `unknown_identifier.txt` 267 B, `.gitkeep`) are READ AT RUNTIME by two `[Fact]`s. **Removing them breaks the tests.** The audit's §1.6 premise ("inline string golden assertions, not file reads") is wrong.

**Planner action:** D-09 = NO-OP. Document in the plan that the confirm-grep was run, it found live reads, and per CONTEXT D-09's own condition the removal does NOT proceed. (Fold this into a single verification note; no commit needed.)

## D-12 — Progression DSL (KEEP + INVEST: add tests + extend a showcase)

**Files that STAY (no removal):**
- `flow-lang/Ast/Expressions/ProgressionExpression.cs` (record `ProgressionElement` + `ProgressionExpression`)
- `flow-lang/Runtime/ProgressionCompiler.cs` (public `Compile(ProgressionExpression, MusicalContext) → SequenceData`, `:50`)
- Parser arms: `Parser.cs:1414-1417` (dispatch on `TokenType.Progression`), `:1721+` (`ParseProgressionExpression`)
- Lexer: `SimpleLexer.cs:948` (`"progression" => TokenType.Progression`)
- Evaluator: `ExpressionEvaluator.cs:53` (dispatch), `:1055-1069` (`EvaluateProgression` — requires active `key` context, else errors)

**(a) Where progression unit tests should live + idiom to copy:**
- **Location:** `flow-lang.Tests/Unit/Phase46/ProgressionDslTests.cs` (no existing Progression test — the JamFunctionsTests "progression" hits are coincidental string mentions). Mirror the existing `Unit/PhaseNN/` convention.
- **Idiom to copy: `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs`** — the cited precedent for structured-Sequence assertions. Pattern:
  ```csharp
  [Collection("FlowScripts")]
  public class ProgressionDslTests {
      private static SequenceData RunProg(FlowEngineRunner r, string body, string var="s") {
          var (ok, _, err, n) = r.RunSource(
              "use \"@std\"\n" +
              "key Cmajor {\n" +
              $"  Sequence {var} = {body}\n" +   // body e.g. "progression | I IV V I |"
              "}\n");
          Assert.True(ok, $"failed: {err}"); Assert.Equal(0, n);
          return r.GetVariable(var).As<SequenceData>();
      }
  }
  ```
  Use `FlowEngineRunner.RunSource` + `GetVariable(name).As<SequenceData>()` (the fixture exposes `GetVariable`, `fixtures/FlowEngineRunner.cs:45`), then walk `seq.Bars[].MusicalNotes` (rest-filter as in `EuclideanSwingTests.HitNotes`).
- **Assertions to add (cover the DSL's documented behavior):** (1) `progression | I IV V I |` in `key Cmajor` yields 4 bars; (2) the `:N` bar-count suffix (`| I:2 V |`) yields the right bar count; (3) the `voices N` modifier path (`Parser.cs:1718` "progression [voices N] |…|"); (4) the no-key error case (`EvaluateProgression` reports "progression requires an active key context" — assert `errorCount > 0` + stderr substring); (5) voice-leading determinism — same input twice → identical pitches (`ProgressionCompiler.FindNearestPitchClass` is deterministic). Probe pitches via `MusicalNoteData` on the compiled bars.

**(b) Showcase example to extend — `examples/showcase.flow`:**
- It already has a `key Cmajor {` block (`:10`) and a hand-written pad at `:29` whose comment literally says *"long whole notes outline the I / IV / V / I shape"* (`Sequence pad = | C4w | F4w | G4w | C4w |`). This is the natural progression demo site.
- **Two options (planner discretion, byte-impact differs):**
  - **Option A (safe, recommended):** ADD a non-rendered demonstration line near `:29` (e.g. `Sequence progDemo = progression | I IV V I |` plus a `(print ...)` describing it) WITHOUT feeding it into the rendered `section showcase`/`Song`. Showcase renders WAV → any change to the rendered graph changes bytes; keeping `progDemo` out of the render preserves byte-identity of `flow_showcase.wav`.
  - **Option B (replaces pad):** Swap `:29`'s hand-written pad for `Sequence pad = progression | I IV V I |`. This is the more honest "showcase the feature" move but CHANGES rendered bytes (legitimately — voice-led chords ≠ root whole-notes). If chosen, the planner must update/refresh any pinned `flow_showcase.wav` baseline and treat it as an RMS-window regression (Phase 28 contract), NOT a byte-identical claim.
  - `examples/long_demo.flow:216` already demonstrates `progression | I:2 vi IV V I IV V I |` — so the "smaller curated showcase" gap is specifically `examples/showcase.flow`/`examples/tutorial.flow`. Recommend Option A on `examples/showcase.flow`.

## D-16 — Legacy keep-treatment doc notes (ADD short notes, NO advisories)

Add a one-to-two-line "legacy / superseded by X — kept as a usable surface" note (comment only — **no stderr advisory, no deprecation warning**) at:

| Surface | File:Location | Note content |
|---------|--------------|--------------|
| Timeline DAW layer | `flow-lang/StandardLibrary/Audio/Timeline.cs:7-9` (the `/// <summary>` on `Timeline`) | "Legacy DAW-style multitrack layer (pre-Phase-25). Superseded by the Song/Section render path (`SongRenderer`) as the canonical arrangement primitive — kept as a usable lower-level manual-mixing surface (shares the `Voice` type)." |
| Track builtins | `flow-lang/StandardLibrary/Audio/Track.cs` (class header) | same framing, cross-ref Song/Section |
| Track Flow wrappers | `flow-lang/composition.flow:1-5` (file header, above the `bpm`/`voiceAt`/… procs at 68-150) | "Legacy fluent Track wrappers — superseded by Song/Section; kept as a usable surface." |
| bars.flow API | `flow-lang/bars.flow:1-2` (file header) | "Legacy bar/measure-construction API. Superseded by the `\| C4 D4 E4 \|` note-stream literal syntax (`NoteStreamCompiler`) — kept as a usable measure-construction surface. Orthogonal to Phase 45 Beats (measure axis, not duration). Imported by `std.flow:6`." |
| Bars C# | `flow-lang/StandardLibrary/Bars.cs:5-7` (the `/// <summary>` on `Bars`) | same framing |

**Do NOT touch:** `std.flow:6` `use "@bars"` import STAYS (D-11). No `[deprecated]` attributes. No runtime warnings.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Proving D-03 didn't change bytes | a bespoke ad-hoc cmp script | the existing `RmsRegressionTests.AssertWavMatchesBaseline` (RMS) + a NEW exact `Assert.Equal(float[])` Fact captured pre-redirect | RMS catches perceptual drift; exact-equal catches the ±1-ULP byte divergence RMS misses |
| D-06 arg migration | new export helper | the existing path-first `writeWav` builtin | strictly-better equivalent already exists (D-01b) |
| D-07 assertion port | re-implement a Bool-returning assert layer | the Phase 35 `@test` `assert`/`assertEq` + `not` builtin | the canonical surface; FAIL cases become `(assert (not …))` |

## Common Pitfalls

### Pitfall 1: Treating D-03 oscillator redirect as "obviously byte-safe"
**What goes wrong:** Replacing the inline `(f·t)%1.0` / `Math.Sin(2π·f·t)` loops with `SynthUtils.Generate*` (incremental phase accumulation) shifts low-order FP bits → different WAV bytes → silently breaks the byte-identical contract for composer-callable `generateSine`/etc.
**How to avoid:** Capture a pre-redirect exact baseline; add an `Assert.Equal(float[])` Fact; if it diverges, keep the inline oscillator math and only redirect `BeatsToSeconds`+`CreateSilence`.
**Warning sign:** Phase 28 RMS baselines PASS but a fresh exact-byte compare FAILS.

### Pitfall 2: Removing the D-09 diagnostics .txt files
**What goes wrong:** Two `[Fact]`s `File.ReadAllText` them → build green, tests RED.
**How to avoid:** Honor CONTEXT D-09's own condition — the confirm-grep found live reads, so do NOT remove. KEEP.

### Pitfall 3: Forgetting `FlowScriptData.cs` expected-output map on D-06/D-07
**What goes wrong:** `FlowScriptData` pins per-script output substrings (`:30-35`, `:119-126`). `test_writewav.flow` has a pinned `"PASS: exportWav … backwards compat"` line; the auto-discovered Fact fails if the file's output changes without updating the map.
**How to avoid:** Update the expected-output map in lockstep with every `.flow` file you rewrite.

### Pitfall 4: Deleting `ToTimeline()` thinking it's part of TimelineMap (D-02)
**What goes wrong:** `BarType.ToTimeline()`/`SequenceType.ToTimeline()` are the PRIMARY render path's beat projection (used at `BarRenderer.cs:81,362`). Name overlaps `TimelineMap` but they're unrelated. Deleting them breaks all rendering.
**How to avoid:** Only delete the `TimelineMap`/`TimelineEntry`-typed members and the `*WithTimeline` overloads.

### Pitfall 5: D-07 port assuming `not` is unavailable
**What goes wrong:** The legacy `test.flow` comment says the interpreter doesn't register `not`. STALE — `not` is registered (`BuiltInFunctions.cs:1139`). The port relies on `(not …)` for FAIL-case inversion.

## Runtime State Inventory

> Rename/refactor-adjacent phase. All categories explicitly checked.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | **None** — no datastore keys/collections embed any removed identifier (TimelineMap/exportWav/legacy-assert names are code-only). Verified by grep of removed names against runtime. | none |
| Live service config | **None** — no n8n/Datadog/external-service config references these symbols (this is a single-process interpreter). | none |
| OS-registered state | **None** — no Task Scheduler / systemd / pm2 references. | none |
| Secrets/env vars | **None** — no env var or secret name references any removed symbol. | none |
| Build artifacts | `flow-lang.Tests/bin/{Debug,Release}/net10.0/Fixtures` + `…/audio.flow` (stale copies of edited stdlib). `bin/` is gitignored and regenerates on `dotnet build`. After D-05/D-06 edit `audio.flow`, the bin copy refreshes automatically. The stale capital `bin/.../Fixtures` artifact is harmless (gitignored). | `dotnet build` (auto) — no manual action |

**Canonical question — "after every file is updated, what runtime systems still cache the old string?":** Only the gitignored `bin/` output, which a normal build regenerates. No persistent runtime state.

## Validation Architecture

> nyquist_validation = true (`.planning/config.json`). Section REQUIRED.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (`flow-lang.Tests`, .NET 10) + Flow-script success suite + shell loop |
| Config | `flow-lang.Tests/flow-lang.Tests.csproj` |
| Quick run | `dotnet test --filter "FullyQualifiedName~<TestClass>"` (per-target) |
| Full suite | `dotnet test` (all xUnit) **+** `for t in tests/test_*.flow; do dotnet run --project flow-interpreter "$t"; done` |
| Flow-script discovery | `FlowScriptData.cs:8` auto-enumerates `tests/**/*.flow` → `FlowEngineRunner.RunFile` asserts success; per-script expected-output map at `:30-35`/`:119-126` |
| RMS regression | `flow-lang.Tests/Helpers/RmsRegressionTests.cs` — `AssertRmsWithinTolerance` / `AssertWavMatchesBaseline` (SPEC-8 ±0.5 dB / 100 ms); baselines `flow-lang.Tests/baselines/Phase28/{maple_leaf_opening,staccato_baseline}.wav` |
| Determinism | two-run cmp-clean (same SHA → byte-identical WAV); pinned in `FlowScriptData` "two runs byte-identical: PASSED" sentinels (`:237,246`) and the Phase 36 `*DeterminismTests` family |

### Per-Target Proof Map
| Target | Primary proof | Secondary proof |
|--------|--------------|-----------------|
| D-02 TimelineMap | build green + full xUnit suite (primary render path untouched → byte-identical) | `grep` zero TimelineMap refs |
| D-03 NoteSynthesizer ⚠️ | **NEW exact `Assert.Equal(float[])` Fact** vs pre-redirect baseline for all 4 synths | Phase 28 RMS baselines (perceptual) + two-run cmp-clean |
| D-04 Fixtures | `grep` no capital-F path strings + `git ls-files` no `flow-lang.Tests/Fixtures/` | full suite (fixtures resolve) |
| D-05 audio.flow decls | full `.flow` suite prints `createSineTone` PASS sentinels (stereo wrapper resolves) | two-run cmp-clean on a sine script |
| D-06 exportWav | build green (shims gone) + migrated `.flow` scripts export + print sentinels | `FlowScriptData` expected-output map consistent |
| D-07 test.flow | ported `test_test_library.flow` returns success via `FlowEngineRunner` (auto-suite) | `flow-cli test tests/test_test_library.flow` → all assertions PASS |
| D-08 ClampSamples | build green + full suite (same `AudioUtils.ClampSamples` invoked → no behavior change) | playback smoke (no audio diff) |
| D-09 diagnostics | KEEP — `DiagnosticRendererGoldenTests` two Facts stay green (files present) | n/a |
| D-12 Progression | NEW `ProgressionDslTests` green | showcase example runs (Option A: byte-identical `flow_showcase.wav`) |
| D-16 doc-notes | build green (comment-only) | full suite unchanged |

### Sampling Rate
- **Per task commit:** `dotnet build` + targeted `dotnet test --filter` for the touched area.
- **Per wave/atomic-commit:** full `dotnet test` + the touched `tests/test_*.flow`.
- **Phase gate (locked, D-18):** full `flow-lang.Tests` + ALL `tests/test_*.flow` + Phase 28 RMS baselines + two-run cmp-clean — all green before `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `flow-lang.Tests/Unit/Phase46/ProgressionDslTests.cs` — covers CLEAN-12 (mirror `EuclideanSwingTests`).
- [ ] NEW exact-byte synth Fact for D-03 (capture baseline from PRE-redirect `dev` build BEFORE editing `NoteSynthesizer.cs`) — covers CLEAN-03 byte contract. Without it the locked gate cannot prove byte-identity.
- [ ] Rewrite `tests/test_test_library.flow` to `@test` surface + update `FlowScriptData` map (CLEAN-07).
- [ ] Update `tests/test_writewav.flow` + `FlowScriptData.cs:119-126` expected-output (CLEAN-06).

## Security Domain

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | no | Pure internal refactor; no new input perimeter |
| V6 Cryptography | no | None |
| (all others) | no | This phase only removes/redirects existing internal code paths; adds no auth, network, file-parsing, or input surface. |

No threat-model change: every removal either deletes dead code (D-02, D-05, D-09-keep) or routes to an existing, already-tested code path (D-03→SynthUtils, D-06→writeWav, D-08→AudioUtils). No new trust boundary.

## State of the Art

| Audit Claim (2026-05-24) | Verified Current State (2026-05-30) | Impact |
|--------------------------|-------------------------------------|--------|
| D-04 `Fixtures/`+`fixtures/` both exist, 6 files each casing | Already merged to lowercase by Phase 44 commit `e0d7274` | D-04 → verification no-op |
| D-09 diagnostics .txt orphaned (inline golden assertions) | `.txt` files LIVE-READ by 2 Facts (`File.ReadAllText`) | D-09 → KEEP |
| D-05 "16 createSineTone declarations" | Exactly 2 internal decls (224, 227); Saw/Square/Triangle have none | D-05 scope smaller |
| D-03 "net behavior matches" (osc redirect) | Incremental-phase vs `(f·t)%1.0` → ±1-ULP byte divergence risk | D-03 needs exact-byte guard |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The D-03 oscillator redirect WILL diverge in bytes (analytical reasoning; dotnet-script unavailable to run the numeric check) | D-03 | If it happens to be byte-identical, the extra guard Fact is harmless overhead — but the guard is the SAFE default either way |
| A2 | `flow-midi.Tests/Fixtures/` lowercasing is out of scope (no sibling collision) | D-04 | Low — it's a single code file with no lowercase counterpart; not a latent FS bug |
| A3 | Leaving the dead C# `CreateSineTone` mono builtin registered (D-05) is acceptable | D-05 | Low — it's harmless undeclared dead code; D-05 says "internal proc decls ONLY" |

## Open Questions

1. **D-12 showcase — Option A (non-rendered demo) vs Option B (replace pad, refresh baseline)?**
   - Known: `examples/showcase.flow:29` pad already "outlines I/IV/V/I"; renders to a pinned WAV.
   - Unclear: whether the team prefers byte-identity (Option A) or a real feature demo with a baseline refresh (Option B).
   - Recommendation: Option A (safe, byte-identical) for this pure-cleanup phase; defer Option B to a future "showcase polish" pass.

2. **D-03 — accept inline-math retention if divergence confirmed?**
   - Known: redirecting only `BeatsToSeconds`+`CreateSilence` is byte-safe and still removes 8 helpers (~32 LOC).
   - Recommendation: if the exact-byte Fact fails, retain inline oscillator loops and document why; do NOT force an RMS-only regression for a "pure cleanup" phase.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 10 | build/test | ✓ (project targets net10.0) | net10.0 | — |
| dotnet-script | optional D-03 numeric pre-check | ✗ | — | Add an xUnit Fact instead (preferred anyway) |
| PulseAudio (libpulse) | D-08 touches playback backend | not required for build/test (compile-time only) | — | Tests don't need a live audio device for these changes |

**Missing with no fallback:** none. **Missing with fallback:** dotnet-script (use an xUnit Fact for the D-03 byte check — strictly better).

## Sources

### Primary (HIGH — verified this session against the `dev` tree)
- `flow-lang/Audio/TimelineMap.cs`, `SongRenderer.cs:450-554`, `BarRenderer.cs:305-389`, `SequenceRenderer.cs:123-165` — D-02
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:24-182`, `SynthUtils.cs:32-116,248` — D-03
- `git show 5f61a1e` / commit `e0d7274`, `git ls-files`, `flow-lang.Tests/*` path-string grep — D-04
- `flow-lang/audio.flow:224,227,352-411`, `Interpreter.cs:845-859`, `BuiltInFunctions.cs:767-777` — D-05
- `flow-lang/StandardLibrary/Audio/FileIO.cs:30-50,272-290`, `BuiltInFunctions.cs:709-721`, caller grep — D-06
- `flow-lang/test.flow`, `tests/test_test_library.flow`, `TestFramework/TestFunctions.cs`, `tests/test_test_framework.flow`, `flow-cli/Commands/TestCommand.cs` — D-07
- `flow-lang/Audio/PulseAudioSimpleBackend.cs:97,271`, `PlaybackFunctions.cs:189,243,374`, `AudioUtils.cs:1,12`, `CoreAudioBackend.cs:149` — D-08
- `flow-lang.Tests/Phase35/DiagnosticRendererGoldenTests.cs:25,34-39,77,116`, `flow-lang.Tests.csproj:95-102` — D-09
- `flow-lang/Ast/Expressions/ProgressionExpression.cs`, `Runtime/ProgressionCompiler.cs:50`, `ExpressionEvaluator.cs:1055-1069`, `Unit/Phase15/EuclideanSwingTests.cs`, `fixtures/FlowEngineRunner.cs`, `examples/showcase.flow` — D-12
- `flow-lang/StandardLibrary/Audio/Timeline.cs`, `Track.cs`, `composition.flow:68-150`, `bars.flow`, `StandardLibrary/Bars.cs` — D-16
- `.planning/config.json` (nyquist_validation), `FlowScriptData.cs:8` — Validation Architecture

### Secondary (MEDIUM)
- `.planning/STATE.md:809` (audit-time snapshot, now partly stale on D-04)
- `.planning/research/CODEBASE-BLOAT-AUDIT-2026-05-24.md` (primary input; corrected on D-04/D-05/D-09 above)

## Metadata

**Confidence breakdown:**
- Per-target maps (D-02/05/06/08): HIGH — exact lines + complete caller lists verified by grep/read.
- D-04 (already-done) / D-09 (keep): HIGH — git history + runtime-read confirmation.
- D-03 byte-risk: HIGH on the risk existing; A1 flags the un-run numeric check.
- D-07 port semantics: HIGH — assertion-throw contract verified in C#.
- D-12 invest: HIGH on idiom/location; Option A/B is a discretion call (Open Q1).

**Research date:** 2026-05-30
**Valid until:** ~30 days (stable internal code; re-verify line numbers if any other phase touches these files first)

## RESEARCH COMPLETE
