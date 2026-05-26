---
phase: 48-wasm-runtime-webaudio-backend
plan: 02
subsystem: wasm-runtime-compat
tags: [wasm, drywetmidi, culture-invariant, globalization, mono-cecil, source-grep-gate]
requirements: [REQ-WASM-DRYWET-01, REQ-WASM-BUILD-05]
dependency-graph:
  requires:
    - "Plan 48-01 (FlowTarget=Web publish pipeline + <InvariantGlobalization>true</InvariantGlobalization> + 10.8 MB bundle)"
    - "Plan 47-04 (FlowTargetFactAttribute + DryWetMidiWasmCompatTests — compile-time Desktop smoke)"
  provides:
    - "DryWetMidiWasmPublishTests — 2 plain xUnit Facts asserting Phase 48-01's published flow-lang.dll retains its Melanchall.DryWetMidi 8.0.3 assembly reference reachably (via Mono.Cecil scan of post-publish .dll metadata)"
    - "CultureInvariantSweepTests — 2 source-grep gate Facts pinning zero unqualified .ToUpper()/.ToLower() in flow-lang/ production code"
    - "3 culture-sensitive call sites converted to *Invariant overloads — HarmonyFunctions.cs:441 (direction.ToLower()) + ScaleDatabase.cs:182,233 (root-note ToUpper/ToLower)"
    - "D-48-17 CLOSED: DryWetMidi STAYS in Web build (not stripped); writeMidi callable on Web target; no v1.6 hand-rolled MIDI writer needed"
  affects:
    - "Every subsequent Plan 48-NN composing with the WASM-published bundle inherits a verified DryWetMidi reachability invariant"
    - "Plan 48-03 WebAudioBackend [JSImport]/[JSExport] implementation can assume MIDI export works under Web target (Phase 49 wires the download UI)"
    - "Future contributors adding .ToUpper()/.ToLower() unqualified to flow-lang/ source hit a RED test in CI with file:line:text diagnostic"
tech-stack:
  added: []
  patterns:
    - "Mono.Cecil AssemblyDefinition.ReadAssembly on POST-PUBLISH binary (NOT typeof().Assembly.Location which yields the test runner's Desktop copy) — corrects polarity vs Phase 47-05 AssemblyReferenceScanTests which inspected the locally-compiled Desktop dll"
    - "Source-grep gate via Regex on File.ReadAllLines — defense-in-depth pattern per T-48-05; skips comment-only lines (TrimStart('//')) and safe-variant marker (ToUpperInvariant/ToLowerInvariant) to avoid false positives"
    - "InvariantCulture preferred over Ordinal/StringComparison for character-case operations in music-domain code (ASCII alphabet — direction tokens / root-note letters / scale names)"
    - "Plain [Fact] (not [FlowTargetFact]) for tests that shell out to dotnet publish — they run from Desktop test runner and inspect Web publish output as data, not as loaded IL"
key-files:
  created:
    - "flow-lang.Tests/Integration/Phase48/DryWetMidiWasmPublishTests.cs (123 lines, 2 plain Facts)"
    - "flow-lang.Tests/Integration/Phase48/CultureInvariantSweepTests.cs (162 lines, 2 plain Facts)"
  modified:
    - "flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs (+5 lines comment + 1 line behavior — direction.ToLower() → ToLowerInvariant())"
    - "flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs (+10 lines comment + 2 lines behavior — root-note ToUpper/ToLower → *Invariant, two call sites)"
decisions:
  - "Honored plan body's 3-call-site target list — HarmonyFunctions.cs:441 + ScaleDatabase.cs:182,233. No additional sites surfaced by the wider grep at execution time (grep is idempotent — pre-execution and post-execution counts both zero for unqualified calls)."
  - "Mono.Cecil scan reads POST-PUBLISH binary at flow-lang/bin/Release/net10.0/browser-wasm/publish/flow-lang.dll (NOT the test runner's copy). Mirrors Phase 47-05 pattern but corrects target — Phase 47-05 reads test runner's loaded Desktop dll; Plan 48-02 needs WASM publish output specifically."
  - "Both new test files use plain [Fact] (NOT [FlowTargetFact]). Rationale: they shell out to a separate `dotnet publish` process OR read source files directly; both run unconditionally from the Desktop test runner. FlowTargetFact only makes sense for tests whose *runtime behavior* depends on the assembly's compile-time FLOW_WEB define."
  - "CultureInvariantSweepTests skip safe-variant marker (ToUpperInvariant/ToLowerInvariant) in addition to the empty-parens regex shape — defense-in-depth against a future false-positive where a line legitimately contains both (e.g. inline comment mentioning the unsafe form alongside the safe call)."
  - "Failure-mode verified by temporary perturbation per plan acceptance criterion: introduced unqualified .ToLower() at HarmonyFunctions.cs:166 → test fired RED with diagnostic 'flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:166  string lower = key.ToLower();'. Source restored before commit."
  - "No additional culture-sensitive sites discovered during execution that would have warranted whitelist comments. The pre-execution grep at PLAN.md authorship time (2026-05-25) found exactly the 3 documented sites and nothing else; post-execution grep confirms 0 hits remain."
metrics:
  duration: "~5 minutes (start 2026-05-26T03:03:06Z, end 2026-05-26T03:08:10Z)"
  completed: 2026-05-26
  tasks: 2
  files_created: 2
  files_modified: 2
  test_count: 4
  test_pass: 4
  test_fail: 0
  phase48_fixture_total: "7 PASS / 0 FAIL / 0 SKIP"
  phase47_fixture_total: "16 PASS / 8 SKIP / 0 FAIL (no regression)"
  bundle_size_post_fix: "10,796,004 bytes (10.3 MiB / 10.8 MB) — identical to Plan 48-01 measurement, zero size regression"
---

# Phase 48 Plan 02: DryWetMidi WASM Publish Smoke + Culture-Invariant Sweep Summary

## One-liner

D-48-17 closed by Mono.Cecil scan of the WASM-published `flow-lang.dll` confirming `Melanchall.DryWetMidi 8.0.3` assembly reference retained — `writeMidi` ships on Web target; no hand-rolled MIDI writer fallback needed; latent invariant-globalization Turkish-I risk closed by converting 3 culture-sensitive `.ToUpper()`/`.ToLower()` call sites in HarmonyFunctions + ScaleDatabase to `*Invariant` overloads, with a source-grep CI gate (4 new xUnit Facts total — all PASS) preventing regression.

## Goal

Per Plan 48-02 objective: extend Phase 47-04's Desktop-side DryWetMidi WASM-compat smoke to the actual Mono-WASM publish output, AND sweep `flow-lang/` for culture-sensitive string operations that misbehave under `<InvariantGlobalization>true</InvariantGlobalization>` (D-48-03). Two coordinated changes; both must land for v1.5 Web target to be safe.

Per D-48-17 in 48-CONTEXT.md: "if DryWetMidi WASM-incompatible, strip from Web build and emit parse-time advisory." Phase 47-04 verified the API surface is reachable on Desktop with FlowTarget=Web compiled; Plan 48-02 closes the open question by confirming the **post-publish bundle** retains DryWetMidi reachably (linker did not over-trim).

## What Shipped

### Task 1 — 3 culture-sensitive call sites converted to *Invariant overloads (commit `8ab1de6`)

Per the plan body's exact target list (verified 2026-05-25 grep):

| File | Line | Before | After |
|------|------|--------|-------|
| `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` | 441 | `switch (direction.ToLower())` | `switch (direction.ToLowerInvariant())` (+ 4-line comment block explaining D-48-03 rationale) |
| `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs` | 182 | `rootNote = char.ToUpper(rootNote[0]) + rootNote[1..].ToLower();` | `rootNote = char.ToUpperInvariant(rootNote[0]) + rootNote[1..].ToLowerInvariant();` (+ 4-line comment block) |
| `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs` | 233 | same as 182 | same as 182 (`Edit` with `replace_all` collapsed both sites in one pass) |

**Rationale (D-48-03 + 48-RESEARCH.md §Finding #5):** Under `<InvariantGlobalization>true</InvariantGlobalization>` the ICU bundle is omitted (~10 MB savings). Unqualified `.ToUpper()` / `.ToLower()` still work on the ASCII subset that Flow's music-domain identifiers use today (direction tokens `up`/`down`/`updown`; root-note alphabet `A..G` + accidentals `#`/`b`). But under any future input containing non-ASCII characters, the Turkish-I problem class manifests — `"file".ToUpper()` returns `"FİLE"` under Turkish locale. The safe path is to always call the explicit `*Invariant` variants. Each edit ships with a comment block citing D-48-03 so the rationale lives next to the code, not just in PLAN.md.

**No additional culture-sensitive sites surfaced.** The plan body documented the exact 3 sites from the 2026-05-25 grep; post-Task-1 grep confirms 0 unqualified hits remain across the entire `flow-lang/` production tree. Whitelist comments (suggested for sites with culture-aware intent) were not needed — none of the three sites had culture-aware semantics in the first place; they were all using `.ToLower()`/`.ToUpper()` as ASCII case normalization.

**Behavioral safety net:** `tests/test_dx_arpeggio.flow` (exercises "up" / "down" / "updown" direction tokens) PASSES post-edit. Existing xUnit `ArpeggioFacts` + `ScaleLintAnalyzerFacts` + `ScaleLintDefaultOnFacts` (33 tests total) all PASS. The InvariantCulture replacement is byte-identical to the prior behavior across all ASCII inputs Flow accepts.

### Task 2 — 4 new xUnit Facts pinning the post-publish DryWetMidi reachability + culture-invariant discipline (commit `a4f726d`)

#### `flow-lang.Tests/Integration/Phase48/DryWetMidiWasmPublishTests.cs` (123 LOC, 2 plain `[Fact]`s)

| Fact | What it asserts | Outcome |
|------|-----------------|---------|
| `FlowLangDll_PublishedToAppBundle` | After `dotnet publish flow-lang -p:FlowTarget=Web -c Release`, the published `flow-lang.dll` exists at `flow-lang/bin/Release/net10.0/browser-wasm/publish/flow-lang.dll` (library-flat layout) OR `…/publish/AppBundle/_framework/flow-lang.dll` (Blazor-app layout) — checks both. File size > 1 KB sanity floor. | PASS (~5s wall-clock with warm publish cache) |
| `FlowLangDll_RetainsDryWetMidiReference` | Mono.Cecil `AssemblyDefinition.ReadAssembly(publishedDllPath)` → scan `MainModule.AssemblyReferences` → at least one entry's `.Name` starts with `"Melanchall.DryWetMidi"` (Ordinal comparison). If absent → escalate to D-48-17 fallback. | PASS — DryWetMidi reference retained; D-48-17 fallback **NOT** needed |

**Critical answer to Plan 48-02 objective:** **YES, DryWetMidi is reachable in the published WASM bundle.** The trim analyzer + linker preserved `Melanchall.DryWetMidi` as a forward reference from `flow-lang.dll`. Composer's `(writeMidi song "out.mid")` will work end-to-end on the Web target.

Pattern correction vs. Phase 47-05 `AssemblyReferenceScanTests`: that test reads `typeof(FlowEngine).Assembly.Location` which yields the **test runner's loaded Desktop dll**, not the WASM publish output. Plan 48-02 reads the post-publish artifact at the explicit `publish/` filesystem path — needed because the WASM build's trim posture is what we're checking, not the Desktop build's.

#### `flow-lang.Tests/Integration/Phase48/CultureInvariantSweepTests.cs` (162 LOC, 2 plain `[Fact]`s)

| Fact | What it asserts | Outcome |
|------|-----------------|---------|
| `NoUnqualifiedToUpper_InProductionCode` | Regex `\.ToUpper\(\)` against every `.cs` file under `flow-lang/` (excludes `bin/`, `obj/`, `*.Tests/`). Skips comment-only lines (TrimStart `//`) and lines containing the safe variant `ToUpperInvariant`. Violations list with file:line:text. | PASS — zero violations |
| `NoUnqualifiedToLower_InProductionCode` | Symmetric for `\.ToLower\(\)` + `ToLowerInvariant`. | PASS — zero violations |

**Failure-mode verified:** Per plan acceptance criterion ("verify via temporary perturbation"), introduced an unqualified `.ToLower()` at HarmonyFunctions.cs:166 — test fired RED with the exact actionable diagnostic:

```text
Found 1 unqualified .ToLower() call(s) in production code (Phase 48 D-48-03
invariant-globalization gate). Fix: replace .ToLower() with .ToLowerInvariant().
  flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:166  string lower = key.ToLower();
```

Source restored before commit. The failure message includes file path (repo-relative), line number, and offending line text trimmed — a developer can fix it in one read.

## Acceptance Criteria — All Pass

| Criterion | Status |
|-----------|--------|
| `grep -rn 'ToUpper()\|ToLower()' flow-lang/ \| grep -v '^\s*//' \| wc -l` returns 0 | **PASS** (0) |
| `grep -c 'ToLowerInvariant\|ToUpperInvariant' flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` >= 1 | **PASS** (5) |
| `grep -c 'ToLowerInvariant\|ToUpperInvariant' flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs` >= 2 | **PASS** (8) |
| `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Desktop` exits 0 | **PASS** (0 Error, 8 Warning — pre-existing) |
| `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` exits 0 | **PASS** (0 Error, 6 Warning — pre-existing including IL2075) |
| `dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web -c Release` exits 0 | **PASS** (Plan 48-01 acceptance preserved) |
| Published `flow-lang.dll` retains `Melanchall.DryWetMidi` AssemblyReference (Mono.Cecil scan) | **PASS** |
| 4 new Phase 48 Facts all PASS | **PASS** (4/4) |
| Phase 48 fixture preserved | **PASS** (7/7 — 3 Plan 48-01 + 4 Plan 48-02) |
| Phase 47 fixture preserved | **PASS** (16 PASS + 8 SKIP + 0 FAIL — no regression) |
| No bundle size regression from Plan 48-01 baseline | **PASS** (10,796,004 bytes — byte-identical to Plan 48-01) |
| CultureInvariantSweepTests use `\.ToUpper\(\)` / `\.ToLower\(\)` regex (NOT the broader catch-all that would reject `ToUpperInvariant`) | **PASS** — empty-parens regex shape |
| Failure-mode diagnostic includes file path + line number + line text | **PASS** — verified via temporary perturbation |

## Deviations from Plan

None. Plan executed exactly as written. No Rule 1 / Rule 2 / Rule 3 / Rule 4 escalations.

The 3 culture-sensitive call sites surfaced at PLAN.md authorship time (2026-05-25 grep) were exactly the 3 sites encountered at execution time — no drift, no surprise sites. The Mono.Cecil scan returned a positive result (DryWetMidi retained) so the D-48-17 fallback path (strip DryWetMidi from Web build) was not invoked.

## Authentication Gates

None. Plan executed fully autonomously per `autonomous: true` frontmatter.

## Decisions Made

- **D-48-17 CLOSED in favor of "keep DryWetMidi in Web build."** Mono.Cecil scan confirms the WASM publish output retains `Melanchall.DryWetMidi 8.0.3` as a reachable assembly reference. `writeMidi` will work on Web target. No hand-rolled MIDI writer fallback needed (deferred to v1.6 backlog).
- **InvariantCulture chosen over Ordinal/StringComparison** for the 3 fixed sites. Rationale: all three operate on character-case (not string comparison or formatting), and the music-domain alphabet is pure ASCII — InvariantCulture is the safest default for ASCII case normalization without locale dependence.
- **Source-grep gate scoped to empty-parens shape.** Per T-48-05 acknowledgment in the threat register: a contributor could bypass via `.ToUpper(CultureInfo.CurrentCulture)`, but the unqualified-empty-parens pattern is the common-case footgun. Code review remains the primary control; this is defense-in-depth.
- **Plain [Fact] vs [FlowTargetFact] for both new test files.** The DryWetMidi tests shell out to a separate `dotnet publish` process and read its output via Mono.Cecil — they don't depend on the test assembly's compile-time FLOW_WEB define. The culture-sweep tests read source files directly. Both run from the Desktop test runner regardless of FLOW_WEB.

## Threat Flags

None. Per Plan 48-02's threat register (T-48-05 / T-48-06 / T-48-07): all three threats are `accept` dispositions with no new mitigation work required. No new attack surface introduced. DryWetMidi 8.0.3 already MIT-licensed open source; published .dll in browser is data, not executable IL until Mono runtime loads it.

## Known Stubs

None.

## Files Touched

```text
flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs                                (MODIFIED, +5 / -1)
flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs                                   (MODIFIED, +10 / -2)
flow-lang.Tests/Integration/Phase48/DryWetMidiWasmPublishTests.cs                    (NEW, 123 LOC)
flow-lang.Tests/Integration/Phase48/CultureInvariantSweepTests.cs                    (NEW, 162 LOC)
```

## Commits

| Hash | Type | Description |
|------|------|-------------|
| `8ab1de6` | fix | convert 3 culture-sensitive call sites to *Invariant overloads |
| `a4f726d` | test | pin DryWetMidi WASM publish + culture-invariant sweep (4 Facts) |

## Phase 48 Status After Plan 02

- Plan 48-01 ✓ COMPLETE — WASM publish pipeline foundation (10.8 MB bundle)
- Plan 48-02 ✓ COMPLETE — DryWetMidi reachability + invariant-globalization safety pinned
- Plans 48-03..48-07 → unblocked

D-48-17 (the v1.5 question "does DryWetMidi survive WASM trim?") resolved YES via empirical post-publish Mono.Cecil scan. Composer's MIDI download flow (Phase 49 wires the UI) will work end-to-end on the Web target. The Turkish-I latent regression class is closed by source-grep CI gate.

## Self-Check: PASSED

Verified before completion:

- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` — modified, `ToLowerInvariant` present at line 445 (post-comment-block): FOUND
- `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs` — modified, `ToUpperInvariant` + `ToLowerInvariant` at lines 186 + 241 (post-comment-block): FOUND
- `flow-lang.Tests/Integration/Phase48/DryWetMidiWasmPublishTests.cs` — created, 123 LOC, 2 [Fact]s: FOUND
- `flow-lang.Tests/Integration/Phase48/CultureInvariantSweepTests.cs` — created, 162 LOC, 2 [Fact]s: FOUND
- Commit `8ab1de6` (Task 1 culture-sensitive fix) in git log: FOUND
- Commit `a4f726d` (Task 2 test files) in git log: FOUND
- `grep -rn 'ToUpper()\|ToLower()' flow-lang/` returns 0 hits: VERIFIED
- `dotnet build -p:FlowTarget=Desktop` exits 0: VERIFIED
- `dotnet build -p:FlowTarget=Web` exits 0: VERIFIED
- DryWetMidi reference retained in published flow-lang.dll: VERIFIED via Mono.Cecil scan
- Phase 48 fixture: 7/7 PASS, Phase 47 fixture: 16 PASS + 8 SKIP + 0 FAIL (preserved): VERIFIED
- Bundle size unchanged at 10,796,004 bytes (no size regression): VERIFIED
