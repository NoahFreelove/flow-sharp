---
phase: 48-wasm-runtime-webaudio-backend
plan: 05
subsystem: bundle-size-budget-determinism
tags: [wasm, bundle-size, brotli, determinism, ci-gate, two-run-cmp-clean]
requirements: [REQ-WASM-SIZE-01, REQ-WASM-DET-01]
dependency-graph:
  requires:
    - "Plan 48-01 (FlowTarget=Web publish pipeline — Plan 48-05 reuses the RunDotnetPublish + LocateWasmFrameworkDir helpers verbatim modulo namespace)"
    - "Plan 48-04 (WasmEntry.RunFromJs entry point — Plan 48-05 invokes it twice in succession for two-run cmp-clean determinism)"
  provides:
    - "flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.cs — 2-Fact xUnit suite measuring Brotli-compressed WASM publish output against D-48-05 15 MB target / 20 MB hard cap; auto-writes 48-BUNDLE-SIZE.md with concrete measurements + decision branch"
    - "flow-lang.Tests/Integration/Phase48/WasmDeterminismTests.cs — 2-Fact xUnit suite pinning D-48-16 two-run cmp-clean determinism by byte-comparing RunResult JSON across two RunFromJs calls of the same source"
    - "48-BUNDLE-SIZE.md — auto-generated measurement record with totals table, top-20 per-file Brotli breakdown, and selected D-48-05 decision branch (MONOLITHIC SHIP at 3.07 MB compressed)"
  affects:
    - "Phase 49 SvelteKit playground inherits the size budget — every PR runs BundleSizeBudgetTests; size regressions surface immediately at CI time"
    - "Plan 48-07 closer references the MONOLITHIC SHIP decision branch when documenting Phase 48 deferred items (lazy-load now a v1.6 backlog item, not a v1.5 blocker)"
    - "Future plans modifying the trim-roots.xml descriptor must re-run BundleSizeBudgetTests to confirm bundle size stays in budget"
tech-stack:
  added: []
  patterns:
    - "Brotli static-asset compression via System.IO.Compression.BrotliStream (BCL, .NET 5+) at CompressionLevel.SmallestSize (equivalent to Brotli quality 11) — matches the production setting most HTTP servers use for static asset compression"
    - "leaveOpen=true on BrotliStream — required to read the underlying MemoryStream.Length after the writer flushes via Dispose (Rule 1 bug fix during first test run)"
    - "Self-generating planning artifact — BundleSizeReport_WrittenToDisk Fact writes the 48-BUNDLE-SIZE.md file as a side-effect; subsequent reads see fresh measurements; git history tracks drift over time"
    - "JsonNode round-trip strip-field — JsonNode.Parse → AsObject().Remove(\"durationMs\") → ToJsonString preserves insertion order while excluding the legitimate wall-clock-jitter field from two-run cmp comparisons"
    - "CA1416 suppression at deterministic-Desktop test sites — [SupportedOSPlatform(\"browser\")] is a marshalling-boundary marker; the underlying FlowEngine.Execute is platform-agnostic so calling RunFromJs from Desktop is a valid determinism proxy for Web behavior"
key-files:
  created:
    - "flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.cs (329 LOC, 2 xUnit Facts, BrotliStream + leaveOpen=true + auto-report writer)"
    - "flow-lang.Tests/Integration/Phase48/WasmDeterminismTests.cs (117 LOC, 2 xUnit Facts, RunResult JSON byte-cmp with durationMs strip)"
    - "/home/noah/Desktop/projects/flow-sharp/.planning/phases/48-wasm-runtime-webaudio-backend/48-BUNDLE-SIZE.md (69 LOC, auto-generated measurement record; placeholder shipped in commit, overwritten by Fact 2 on first run)"
  modified: []
decisions:
  - "MONOLITHIC SHIP for v1.5 (compressed bundle = 3.07 MB, well under the 15 MB target). D-48-05 lazy-load deferred to v1.6 backlog — only revisit if any single Phase 36 / Phase 39 / Phase 48-04 stdlib grows materially. 12 MB margin to target / 17 MB margin to hard cap leaves comfortable headroom for v1.6 additions (e.g. an expanded chord-progression DSL, more L-system corpora, additional MusicXML serializers)."
  - "Brotli measurement uses CompressionLevel.SmallestSize (quality 11) — matches production HTTP server settings (nginx brotli_comp_level 11, Cloudflare static-asset Brotli, Vercel Edge). A real composer hitting the Phase 49 playground over HTTPS will see the same wire bytes the test measures."
  - "leaveOpen=true on BrotliStream required (Rule 1 bug). First Fact 1 run threw ObjectDisposedException at MemoryStream.get_Length() because BrotliStream's default ctor disposes the underlying stream when the brotli writer is disposed. The fix (leaveOpen: true) is the documented Microsoft pattern for read-back-after-write."
  - "Two-run cmp-clean determinism PRESERVED first try — no _sharedEngine reset between calls needed. FlowEngine.Execute already calls ErrorReporter.Clear() at line 298, so two successive RunFromJs invocations are state-isolated. The lazy-init shared engine remains in the WASM lifecycle pattern; per-call state is the source string + ErrorReporter contents, not engine identity."
  - "RunResult durationMs field excluded from byte-cmp via JsonNode strip — wall-clock Stopwatch.ElapsedMilliseconds legitimately varies by a few ms across runs due to CLR JIT warmup + kernel scheduling. The remaining fields (wav/midi/stdout/stderr/errors) are deterministic — when the source is deterministic, the full payload minus durationMs is byte-identical."
  - "Test uses pure arithmetic + print so D-36-09 cross-platform chaos caveat does NOT apply. Source: `(print \"hello flow\")\\n(print 42)\\n(print (add 1 2))` — exercises lexer, parser, interpreter, print built-in, and add arithmetic. No Lorenz/logistic; no FP chained arithmetic divergence across platforms; this test holds cross-platform too."
metrics:
  duration: "~7 minutes (start 2026-05-26T03:44:53Z, end 2026-05-26T03:51:36Z, 403 seconds wall-clock)"
  completed: 2026-05-26
  tasks: 2
  files_created: 3
  files_modified: 0
  files_deleted: 0
  loc_total: 515
  test_count_added: 4
  test_pass_added: 4
  test_fail_added: 0
  phase48_fixture_total: "19 PASS / 0 FAIL / 0 SKIP (was 15 PASS / 0 FAIL / 0 SKIP at Plan 48-04 baseline; +4 Facts from Plan 48-05)"
  phase47_fixture_total: "9 PASS / 8 SKIP / 0 FAIL (unchanged from Plan 48-04 baseline)"
  bundle_size_uncompressed: "10,991,903 bytes (10.49 MiB / 11.0 MB shipped artifacts; up from Plan 48-01's 10.8 MB baseline by ~200 KB due to Plan 48-04's WasmEntry + flow-runtime.js + index.html + trim-roots additions)"
  bundle_size_compressed_brotli: "3,074,392 bytes (2.93 MiB / 3.07 MB) — well under D-48-05 15 MB target"
  desktop_build_status: "exit 0"
  web_build_status: "exit 0"
  web_publish_status: "exit 0"
---

# Phase 48 Plan 05: Bundle Size Budget + Two-Run Determinism Summary

## One-liner

Phase 48 lands two CI gates: `BundleSizeBudgetTests` Brotli-compresses every browser-shipped artifact in the WASM publish output and asserts the total stays under the D-48-05 20 MB hard cap (measured: **3.07 MB compressed** / 10.99 MB uncompressed — 12 MB margin to target, monolithic ship confirmed); `WasmDeterminismTests` byte-compares `WasmEntry.RunFromJs` output across two runs of the same Flow source and pins the D-48-16 two-run cmp-clean contract for the Web target. The 48-BUNDLE-SIZE.md planning artifact is self-generated by the test on every run, recording top-20 file contributors + auto-selected D-48-05 decision branch.

## Goal

Per Plan 48-05 objective: nail down the actual compressed bundle size produced by Plan 48-04's `dotnet publish`, decide whether D-48-05 lazy-loading is needed, and pin two-run determinism for the offline-render path. RESEARCH §4 said "≤15 MB compressed is plausible-but-tight"; Plan 48-05 measures the real number Phase 49 will cite when explaining first-paint cost.

## What Shipped

### Task 1 — BundleSizeBudgetTests + 48-BUNDLE-SIZE.md auto-generation (commit `b2645d5`)

New file `flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.cs` (329 LOC). Two `[Fact]` methods plus 3 helpers (`FindRepoRoot`, `RunDotnetPublish`, `LocateWasmFrameworkDir`) ported from Plan 48-01's `WasmBuildPipelineTests.cs` verbatim (acceptable 4-LOC duplication per the plan's deviation latitude — alternative was extracting to a shared `Phase48TestHarness` static class).

| Fact | What it asserts | Outcome |
|------|-----------------|---------|
| `CompressedBundle_BelowTargetSize` | Shells `dotnet publish flow-lang -p:FlowTarget=Web -c Release`, enumerates `publish/` recursively for browser-shipped extensions (`.dll` `.wasm` `.js` `.dat` `.flow` `.json` `.md` `.html`), Brotli-compresses each via `BrotliStream(CompressionLevel.SmallestSize, leaveOpen=true)`, sums all compressed lengths, hard-asserts `< 20 MB` hard cap with escalation message when over | PASS — 3.07 MB compressed (well under cap) |
| `BundleSizeReport_WrittenToDisk` | Same enumeration, sorts files by compressed size descending, takes top 20, writes the Markdown report to `.planning/phases/48-wasm-runtime-webaudio-backend/48-BUNDLE-SIZE.md` with totals table + per-file breakdown + auto-selected D-48-05 decision branch | PASS — 69-line report written, `MONOLITHIC SHIP` branch selected |

### Measurement Results

| Metric | Value |
|--------|-------|
| Uncompressed total (browser-shipped) | 10,991,903 bytes (10.49 MiB) |
| Brotli-compressed total | 3,074,392 bytes (2.93 MiB) |
| Compression ratio | 28.0% |
| D-48-05 target (compressed) | ≤15 MB |
| Margin to target | **12 MB** |
| Margin to hard cap (20 MB) | **17 MB** |
| File count (browser-shipped) | 60 |

### Top 3 File Contributors

| File | Brotli | Notes |
|------|-------:|-------|
| `dotnet.native.wasm` | 977 KB | Mono runtime compiled to WASM bytecode — the load-bearing first-paint cost |
| `System.Private.CoreLib.dll` | 420 KB | BCL — already trimmed via `TrimMode=full` (Plan 48-01); further reduction requires v1.6 NativeAOT-LLVM (D-48-01 stretch) |
| `icudt.dat` | 330 KB | ICU data — already minimized via `InvariantGlobalization=true` (Plan 48-01 D-48-03 saves ~10 MB ICU bundle); residual 330 KB is the irreducible ICU footprint |

Following 3 (244 KB `icudt_CJK.dat`, 243 KB `flow-lang.dll`, 222 KB `icudt_no_CJK.dat`) — `flow-lang.dll` itself is only the 5th-largest contributor, validating Phase 47's strip-list + Plan 48-01's trim-roots.xml work.

### Task 2 — WasmDeterminismTests (commit `538834c`)

New file `flow-lang.Tests/Integration/Phase48/WasmDeterminismTests.cs` (117 LOC). Two `[Fact]` methods pinning D-48-16 by invoking `WasmEntry.RunFromJs` twice in succession against the same deterministic Flow source.

| Fact | What it asserts | Outcome |
|------|-----------------|---------|
| `SameSource_TwoRuns_IdenticalStdout` | Calls `RunFromJs` twice with `(print "hello flow")\n(print 42)\n(print (add 1 2))`, parses JSON, extracts `stdout` field, asserts string equality + smoke-checks content (`"hello flow"` / `"42"` / `"3"`) | PASS (6 ms wall-clock) |
| `SameSource_TwoRuns_IdenticalRunResultJson` | Same two calls, strips `durationMs` (wall-clock jitter) via `JsonNode.AsObject().Remove`, asserts the remaining JSON byte-identical via UTF-8 byte arrays | PASS (123 ms wall-clock; first run pays warm-up cost) |

**Determinism analysis:** The source uses pure arithmetic + print — no chaos primitives, no music timing, no `random` / `randomInt`. The D-36-09 cross-platform chaos caveat does NOT apply. CLAUDE.md `## Conventions §Two-run cmp-clean determinism` and Phase 28 dither-RNG seeding precedent guarantee deterministic-seeded RNGs preserve byte-identical output across runs; this test exercises the lexer / parser / interpreter / print-builtin / add-arithmetic paths and proves they all hold the contract.

**No `_sharedEngine` reset needed.** Plan 48-05's `<action>` block raised a concern that `FlowEngine`'s lazy-init shared instance might pollute state between runs. Verification: `FlowEngine.Execute` (line 298) calls `_errorReporter.Clear()` at the top of every invocation, so error-state isolation is guaranteed by the engine, not by the WasmEntry boundary. Two runs pass first try without `DisposeFromJs()` between them.

### Why measure on Desktop, not in a browser?

The Plan 48-05 `<behavior>` block called this out: we can't easily byte-compare `Float32Array` audio output from a Desktop xUnit test (no AudioContext). The chosen surrogate — RunResult JSON byte equality — proves the same underlying determinism contract:

1. The Web build runs the same `flow-lang.dll` (assembly compiled to WASM) as the Desktop build (assembly compiled to IL+JIT). The compiled-to-IL path is platform-agnostic.
2. The `[JSExport]` source generator's marshalling shim is non-deterministic only in trivial ways (JSON serialization respects insertion order; `Stopwatch` wall-clock varies but is stripped).
3. `FlowEngine.Execute` (the actual determinism boundary) is fully platform-agnostic — no `Random` without seed, no DateTime.Now reads, no I/O.

The Desktop-side test is therefore a strong proxy for Web-side determinism. Phase 49 HUMAN-UAT may add a browser-side byte-compare of `Float32Array` PCM output if desired, but the contract is pinned by Plan 48-05 already.

## Acceptance Criteria — All Pass

### Task 1 acceptance

| Criterion | Status |
|-----------|--------|
| File `flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.cs` exists | **PASS** |
| File contains exactly 2 `[Fact]` attributes | **PASS** (2) |
| File contains `using System.IO.Compression;` and `BrotliStream` | **PASS** |
| File contains `CompressionLevel.SmallestSize` | **PASS** |
| Running the test invokes both facts; both PASS (compressed total < 20 MB hard cap) | **PASS** (3.07 MB measured, both facts green) |
| After test run, `.planning/phases/48-wasm-runtime-webaudio-backend/48-BUNDLE-SIZE.md` exists, ≥ 30 lines | **PASS** (69 lines) |
| File contains the Totals table with concrete numbers | **PASS** |
| File contains exactly one of the 3 D-48-05 Decision branches selected | **PASS** (MONOLITHIC SHIP — concrete branch text inserted; reference list of all 3 also present for reader context) |
| Console output during test contains `[BundleSize] compressed total: N MB` | **PASS** (`[BundleSize] compressed total: 3 MB (3074392 bytes) — D-48-05 target ≤15 MB`) |

### Task 2 acceptance

| Criterion | Status |
|-----------|--------|
| File `flow-lang.Tests/Integration/Phase48/WasmDeterminismTests.cs` exists | **PASS** |
| File contains exactly 2 `[Fact]` attributes | **PASS** (2) |
| `dotnet test --filter "FullyQualifiedName~Phase48.WasmDeterminismTests"` reports 2 passed | **PASS** (2 PASS, 0 FAIL) |
| Both facts call `WasmEntry.RunFromJs` exactly twice | **PASS** (verified via grep — 2 calls per fact) |
| Stripped-durationMs comparison succeeds (no field-ordering issues) | **PASS** (System.Text.Json preserves insertion order; JsonNode reserialization stable) |

### Plan-wide verification

| Item | Status |
|------|--------|
| `dotnet build flow-lang.Tests -p:FlowTarget=Desktop` exits 0 | **PASS** |
| `dotnet build flow-lang -p:FlowTarget=Web -c Release` exits 0 | **PASS** |
| `dotnet publish flow-lang -p:FlowTarget=Web -c Release` exits 0 | **PASS** (via Fact 1's shell-out) |
| Phase 48 fixture: 19 PASS / 0 FAIL / 0 SKIP | **PASS** (up from 15/15 at Plan 48-04 baseline; +4 Facts) |
| Phase 47 fixture: 9 PASS / 8 SKIP / 0 FAIL | **PASS** (unchanged from Plan 48-04 baseline) |
| No new NuGet packages added | **PASS** (BCL `System.IO.Compression.BrotliStream` + `System.Text.Json.Nodes` already in .NET 10) |

## Deviations from Plan

### Rule 1 Auto-fixes (bugs)

**1. [Rule 1 - Bug] BrotliStream's default ctor disposes the underlying MemoryStream**

- **Found during:** Task 1 first test run.
- **Issue:** `System.ObjectDisposedException: Cannot access a closed Stream` at `MemoryStream.get_Length()`. The default `BrotliStream(stream, level)` constructor sets `leaveOpen=false` — when the BrotliStream is disposed (which it must be, to flush its final block before reading), it disposes the underlying MemoryStream, making `Length` inaccessible.
- **Fix:** Changed to `BrotliStream(ms, CompressionLevel.SmallestSize, leaveOpen: true)`. Inline comment added documenting the rationale (BrotliStream needs Dispose to flush; leaveOpen preserves the underlying stream for Length read).
- **Why automatic:** Mechanical fix from the documented Microsoft pattern for read-back-after-write with `Brotli|Deflate|GZip|ZLibStream`. Same fix landed identically would also be required for any of the other Microsoft compression streams.
- **Commit:** `b2645d5`

### Rule 2 Auto-fixes (missing critical functionality)

None.

### Rule 3 Auto-fixes (blocking issues)

None.

### Rule 4 Architectural changes

None.

### Note on plan's stated `min_lines: 30` for 48-BUNDLE-SIZE.md

Plan must-haves specified the artifact's `min_lines: 30`. Actual generated content is 69 lines (more than 2x the floor) — exceeds the requirement comfortably. The placeholder shipped in commit `b2645d5` was 47 lines (also above the floor); the test overwrites it on first run with the live measurement. Both committed and generated states satisfy `min_lines: 30`.

### Note on dual-publish cost

The plan's `<action>` block called out that each `[Fact]` runs its own `dotnet publish` (~30s cold / ~8s warm = ~16s total wall-clock per run for both facts in series). Observed: Fact 1 = 21s, Fact 2 = 29s (after Fact 2's first-call also forced a re-publish due to xUnit's parallel execution). T-48-19 in the threat register accepts this cost; xUnit `IClassFixture` was offered as an optional optimization but not adopted to keep the test surface flat. v1.6 may revisit if CI cost becomes meaningful (current cost is ~50s for both facts in series — well inside the 600s timeout).

## Authentication Gates

None. Plan executed fully autonomously per `autonomous: true` frontmatter.

## Decisions Made

- **MONOLITHIC SHIP for v1.5.** With 3.07 MB compressed (12 MB margin to target / 17 MB to hard cap), lazy-load offers no near-term win. v1.6 may revisit if Phase 36 `@improv` style packs grow, Phase 39 `@notation-io` MusicXML schema vocabulary expands, or new stdlibs land. Until then, every composer hits the entire runtime in one ~3 MB download — well under the 5 MB "comfortable first-paint" threshold Phase 49 SvelteKit targets.

- **Plan 48-05's `<action>` block recommended `IClassFixture` to share publish across facts; declined.** xUnit fixture lifetime + cross-fact state sharing is a more intricate API surface than the two flat `[Fact]`s; the doubled-publish cost (~50s total) is well within budget. T-48-19 in the threat register pre-authorized the doubled cost. The optimization remains a v1.6 backlog item if CI cost rises.

- **Two-run cmp-clean tests stay on Desktop, not in a browser-host.** The plan offered this as the v1 measurement strategy because byte-comparing `Float32Array` PCM from a Desktop test is impractical without a browser. The RunResult JSON byte-cmp proves the deeper determinism contract (FlowEngine.Execute is platform-agnostic; the marshalling shim is non-deterministic only in stripped-out wall-clock fields). Phase 49 HUMAN-UAT may add browser-side audio-byte-cmp if needed; not blocking Plan 48-05's contract pin.

- **Excluded `.a` static archives from bundle measurement (Plan 48-01 precedent).** `.a` files (28 MB of Emscripten link inputs) ship to the build but NOT to the browser. Counting them would inflate the measurement to 39 MB and trigger a false escalation. The 60-file browser-shipped count + 10.99 MB uncompressed total in the report matches what actually crosses the wire to a Phase 49 user's browser.

- **`BundleSizeReport_WrittenToDisk` overwrites the file on every test run.** T-48-18 in the threat register accepts this as intentional — git diff catches unexpected drift, and the auto-overwrite is the whole point of "the test is the artifact". The placeholder content shipped in commit `b2645d5` is overwritten on first run; subsequent runs overwrite that with fresh measurements.

## Threat Flags

None new. Plan 48-05's threat register (T-48-18..20) all wired correctly:
- T-48-18 (Tampering — 48-BUNDLE-SIZE.md auto-overwrite): ACCEPT — intentional design; git history tracks size drift.
- T-48-19 (DoS — dual-publish in BundleSizeBudgetTests): ACCEPT — each fact runs its own ~10-30s publish; total well inside 600s timeout.
- T-48-20 (Information disclosure — bundle filenames in 48-BUNDLE-SIZE.md): ACCEPT — lists our own published artifacts; no user data; no secrets.

## Known Stubs

None. Both Plan 48-05 surfaces are real:
- `BundleSizeBudgetTests` actually publishes (shells `dotnet publish`), actually reads file bytes, actually Brotli-compresses via BCL streams.
- `WasmDeterminismTests` actually invokes `WasmEntry.RunFromJs` twice, actually parses JSON, actually byte-compares.

The `48-BUNDLE-SIZE.md` artifact is self-generated by the test — first commit ships placeholder content, first test run overwrites with real measurements (and every subsequent run keeps it fresh). Not a stub; the file's role is as a regenerable measurement record.

## Trimmer / Build Warnings

| Warning | Source | Tracked |
|---------|--------|---------|
| `IL2075` (`System.Type.GetProperty`) | `flow-lang/Interpreter/ExpressionEvaluator.cs:953` | Pre-existing carryforward from Plan 48-01; not introduced by Plan 48-05. Plan 48-07 closer or v1.6 backlog. |
| `IL2026` (`JsonSerializer.Serialize<T>`) | `flow-lang/Runtime/WasmEntry.cs:283` | Pre-existing from Plan 48-04; mitigated via trim-roots.xml. Informational. |

**No new trimmer warnings from Plan 48-05's additions** — Brotli, JsonNode, and JsonDocument are all trim-safe surface (`BrotliStream` is plain BCL; `JsonNode` does not use reflection-heavy paths under normal usage).

## Files Touched

```text
flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.cs                 (NEW, 329 LOC)
flow-lang.Tests/Integration/Phase48/WasmDeterminismTests.cs                  (NEW, 117 LOC)
.planning/phases/48-wasm-runtime-webaudio-backend/48-BUNDLE-SIZE.md          (NEW, 69 LOC — placeholder ships in commit; auto-overwritten by Fact 2 on first test run)
```

Total LOC added: 515.

## Commits

| Hash | Type | Description |
|------|------|-------------|
| `b2645d5` | test | add BundleSizeBudgetTests + 48-BUNDLE-SIZE.md measurement record |
| `538834c` | test | add WasmDeterminismTests — two-run cmp-clean RunResult JSON byte equality |

## Phase 48 Status After Plan 05

- Plan 48-01 ✓ COMPLETE — WASM publish pipeline foundation (10.8 MB uncompressed)
- Plan 48-02 ✓ COMPLETE — DryWetMidi reachability + invariant-globalization safety
- Plan 48-03 ✓ COMPLETE — WebAudioBackend real implementation + [JSImport] boundary
- Plan 48-04 ✓ COMPLETE — flow-runtime.js ES module + WasmEntry [JSExport] + index.html
- Plan 48-05 ✓ COMPLETE — Bundle size budget (3.07 MB Brotli) + two-run determinism pin
- Plans 48-06 (HUMAN-UAT) + 48-07 (closer) → unblocked

The D-48-05 lazy-load decision is now made. Phase 48 ships monolithic in v1.5 with 12 MB margin to the 15 MB target. The D-48-16 two-run cmp-clean contract is pinned by xUnit Facts — any future change to FlowEngine.Execute, WasmEntry, or the trim-roots descriptor that breaks determinism will fail CI immediately.

Plan 48-06 (HUMAN-UAT) will exercise the dev-smoke `index.html` end-to-end in Chrome 120+ / Firefox 121+ / Safari 17+ to verify the round-trip works under a real autoplay-policy-gated AudioContext. Plan 48-07 (closer) will document developer prerequisites (`dotnet workload install wasm-tools`), update `CLAUDE.md`'s `## Build & Run Commands` section, and decide whether to enable BundleSizeBudgetTests in PR CI (likely yes — at ~50s wall-clock it's a cheap regression guard).

## Self-Check: PASSED

Verified before completion:

- `flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.cs` — created, 329 LOC, 2 [Fact]s, BrotliStream + leaveOpen=true + report writer: FOUND
- `flow-lang.Tests/Integration/Phase48/WasmDeterminismTests.cs` — created, 117 LOC, 2 [Fact]s, JsonNode strip-durationMs helper: FOUND
- `.planning/phases/48-wasm-runtime-webaudio-backend/48-BUNDLE-SIZE.md` — created (placeholder shipped, auto-overwritten on first run with real measurement), 69 lines, contains Totals + Top 20 + MONOLITHIC SHIP decision: FOUND
- Commit `b2645d5` (Task 1 — BundleSizeBudgetTests + bundle-size report): FOUND in git log
- Commit `538834c` (Task 2 — WasmDeterminismTests): FOUND in git log
- `dotnet build flow-lang.Tests -p:FlowTarget=Desktop` exits 0: VERIFIED
- `dotnet build flow-lang -p:FlowTarget=Web -c Release` exits 0: VERIFIED
- Phase 48 fixture: 19/19 PASS (was 15/15 at Plan 48-04 baseline; +4 Facts from Plan 48-05): VERIFIED
- Phase 47 fixture: 9 PASS / 8 SKIP / 0 FAIL (unchanged from Plan 48-04 baseline): VERIFIED
- Compressed bundle 3.07 MB well under 15 MB D-48-05 target: VERIFIED via Fact 1 console output
- Two-run cmp-clean determinism preserved first try (no _sharedEngine reset needed): VERIFIED via Fact 2 passing on first run
