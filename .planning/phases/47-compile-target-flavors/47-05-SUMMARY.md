---
phase: 47-compile-target-flavors
plan: 05
subsystem: test
tags:
  - phase-47
  - wave-5
  - assembly-reference-scan
  - mono-cecil
  - compile-target
  - web-build
  - invariant-gate
requirements: [REQ-WEB-TARGET-10]
dependency-graph:
  requires:
    - "Plan 47-01 (FLOW_WEB define + <Compile Remove> strip-list — gives the Web build a smaller flow-lang.dll surface to scan)"
    - "Plan 47-03 (FlowEngine guards + ModuleLoader gates — pins the runtime side so type-ref absence is meaningful)"
    - "Plan 47-04 (FlowTargetFactAttribute helper — supplies the [FlowTargetFact(\"Web\")] gate used by AssemblyReferenceScanTests)"
  provides:
    - "Mono.Cecil 0.11.5 PackageReference at flow-lang.Tests/flow-lang.Tests.csproj — dev-only test dependency, never shipped"
    - "AssemblyReferenceScanTests — 2 [FlowTargetFact(\"Web\")] Facts: forbidden-ref scan (Rug.Osc / RtMidi.Core / System.IO.FileSystemWatcher type-refs + libpulse / AudioToolbox P/Invoke) + sanity negative-check (legitimate System.* refs retained)"
  affects:
    - "Plan 47-06 (closer) — once the 18-file Sfz/Osc test cascade-fail is fixed and dotnet build flow-lang.Tests -p:FlowTarget=Web exits 0, both Facts execute GREEN and become the load-bearing invariant gate against namespace-drift regressions"
    - "Phase 48 (WASM bundle) — every stripped namespace that sneaks back in inflates the WASM bundle (D-48-03 budget); AssemblyReferenceScanTests fires RED before that happens"
tech-stack:
  added:
    - "Mono.Cecil 0.11.5 (MIT, jbevain canonical maintainer, 1.4B+ downloads, zero transitive deps) — test-only dev dependency"
  patterns:
    - "AssemblyDefinition.ReadAssembly + using-disposable pattern (Mono.Cecil canonical IDisposable shape)"
    - "Two-pass reflective scan: GetTypeReferences() for managed-type refs + MainModule.Types iteration with MethodDefinition.PInvokeInfo for native-binding refs"
    - "Diagnostic-quality error messages: leaked refs are formatted as `forbidden ← actual.FullName` so a regressed PR knows immediately which file to fix"
    - "Negative-check sanity Fact paired with positive forbidden-ref scan — prevents the gate from becoming a one-way ratchet allowing arbitrary trimming"
    - "Anchor type pattern: typeof(FlowLang.Core.FlowEngine).Assembly.Location locates the .dll under test without hardcoded paths"
key-files:
  created:
    - "flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs"
  modified:
    - "flow-lang.Tests/flow-lang.Tests.csproj"
decisions:
  - "D-47-14 honored: AssemblyReferenceScanTests ships as a Web-only invariant gate. Forbidden type-ref prefix list locked at exactly 3 entries: Rug.Osc, RtMidi.Core, System.IO.FileSystemWatcher. Forbidden P/Invoke substring list locked at exactly 2 entries: libpulse, AudioToolbox."
  - "Package Legitimacy Gate (Task 0 checkpoint:human-verify) PRE-APPROVED by composer in execution prompt — Mono.Cecil 0.11.5 verified as canonical (jbevain maintainer, MIT, 1.4B+ downloads, zero transitive deps). Recorded rationale in commit message and proceeded directly to Task 1 without interactive checkpoint."
  - "Plan code sample adopted verbatim — no Rule 1 API-shape corrections needed (unlike Plan 47-04's DryWetMidi NoteOnEvent property-init shape that required positional-constructor correction). Mono.Cecil API surface from the plan body's <interfaces> block compiled cleanly on first attempt."
  - "Web-side execution of both Facts deferred to Plan 47-06's tag sweep per plan body — Plan 47-05 only requires the test compiles cleanly on Desktop and the 2 Facts skip with documented reason. The actual Web-side GREEN/RED behavior gates after Plan 47-06 closes the 18-file Sfz/Osc test-project cascade-fail."
metrics:
  duration: ~3min
  completed: 2026-05-26
  tasks_completed: 3
  files_created: 1
  files_modified: 1
  commits: 2
---

# Phase 47 Plan 05: AssemblyReferenceScanTests Summary

## One-liner

Ships `AssemblyReferenceScanTests` — a Mono.Cecil-backed reflective invariant gate that scans the Web-compiled `flow-lang.dll` for residual references to stripped namespaces (Rug.Osc, RtMidi.Core, FileSystemWatcher) and forbidden P/Invoke targets (libpulse, AudioToolbox), with a paired negative-check Fact preventing over-stripping; 2/2 SKIPPED correctly on Desktop with documented `Skipped on Desktop — test runs under: Web` reason, full Desktop suite remains 2127 PASSED + 9 SKIPPED + 0 FAILED (delta of +2 SKIPPED attributable to this plan's 2 new Web-only Facts; zero regression).

## What Was Done

### Task 0 — Package Legitimacy Gate (PRE-APPROVED, no checkpoint needed)

Composer pre-approved Mono.Cecil 0.11.5 in the execution prompt. Verification recorded:

- **License:** MIT (compatible with flow-lang project licensing)
- **Maintainer:** jbevain (Jb Evain — canonical Mono.Cecil maintainer since 2004; original CIL inspection toolchain author)
- **Adoption:** 1.4B+ lifetime downloads on nuget.org (Newtonsoft.Json, Costura.Fody, NUnit, JetBrains tooling, many others)
- **Transitive deps:** ZERO (Mono.Cecil is dependency-free under .NET Standard 2.0)
- **Source:** https://github.com/jbevain/cecil
- **Scope:** dev-only test dependency in `flow-lang.Tests/`. Never shipped to library consumers; not in `flow-lang.dll` runtime closure.

Classification: **[VERIFIED]** canonical package — NOT [ASSUMED] / [SUS] / [SLOP]. No `checkpoint:human-verify` pause required (composer pre-approval flagged in prompt).

### Task 1 — Mono.Cecil 0.11.5 PackageReference (commit `5c6129c`)

Edited `flow-lang.Tests/flow-lang.Tests.csproj`. Added one `<PackageReference>` line inside the existing test-deps `<ItemGroup>` (lines 11-16):

```xml
<!-- Phase 47 Plan 47-05 D-47-14: Mono.Cecil 0.11.5 (MIT) for reflective
     assembly-reference scanning. Used by AssemblyReferenceScanTests to
     assert the Web-target flow-lang.dll contains zero references to
     stripped namespaces. Package-legitimacy gate (Task 0) ratified
     (canonical maintainer jbevain, 1.4B+ downloads, zero transitive deps). -->
<PackageReference Include="Mono.Cecil" Version="0.11.5" />
```

Verification:

- `dotnet restore flow-lang.Tests` → exit 0 (Mono.Cecil 0.11.5 downloaded cleanly; pre-existing NU1701 Rug.Osc warning is unrelated)
- `dotnet build flow-lang.Tests` → exit 0 (82 warnings, 0 errors — warnings are all pre-existing xUnit/VSTHRD analyzer noise)
- `grep -c "Mono.Cecil" flow-lang.Tests.csproj` → 2 (PackageReference + the explanatory comment, satisfying ≥ 1 acceptance gate)

No other ItemGroups touched (ProjectReferences at 18-23, baselines at 25-43, Phase35 baselines at 49-54 preserved byte-identical).

### Task 2 — AssemblyReferenceScanTests.cs (commit `25b40ea`)

Created `flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs` (120 LOC) per the plan body's canonical skeleton. Two `[FlowTargetFact("Web")]` Facts:

| Fact | Asserts on Web | Strategy |
|------|----------------|----------|
| `WebBuild_HasNoRefsToStrippedNamespaces` | Web flow-lang.dll has zero forbidden type-refs AND zero forbidden P/Invoke targets | Two-pass scan: Pass 1 iterates `MainModule.GetTypeReferences()` filtering by `tr.FullName.StartsWith(forbidden, Ordinal)`; Pass 2 iterates `MainModule.Types → t.Methods` filtering by `m.PInvokeInfo.Module.Name.IndexOf(forbidden, OrdinalIgnoreCase) >= 0`. Failures produce diagnostic-quality `forbidden ← actual.FullName` formatted error messages so a regressed PR knows immediately which file to fix. |
| `WebBuild_RetainsLegitimateRefs` | Web flow-lang.dll DOES still reference its legitimate non-stripped deps (e.g., `System.*` types) | Negative-check sanity assertion. Catches the inverse pathology — if someone over-strips (accidentally drops DryWetMidi or the entire FlowLang.Diagnostics namespace), this Fact fires RED. Prevents Plan 47-05 from being a one-way ratchet that permits arbitrary trimming. |

Forbidden lists locked:

```csharp
private static readonly string[] ForbiddenTypeRefPrefixes = new[]
{
    "Rug.Osc",                      // OSC client/server (Phase 38)
    "RtMidi.Core",                  // Phase 40 MIDI input forward-look
    "System.IO.FileSystemWatcher",  // live reload (Phase 38 LIVE-02)
};

private static readonly string[] ForbiddenPInvokeSubstrings = new[]
{
    "libpulse",      // PulseAudioSimpleBackend + PulseAudioCaptureBackend
    "AudioToolbox",  // CoreAudioBackend
};
```

Anchor pattern: `typeof(FlowLang.Core.FlowEngine).Assembly.Location` — returns the file path of the loaded `flow-lang.dll` (works whether the test runner is hosting a Desktop or Web build).

Mono.Cecil API surface used:

- `AssemblyDefinition.ReadAssembly(string path)` static factory → `IDisposable` `AssemblyDefinition` (consumed via `using var`)
- `asm.MainModule` → `ModuleDefinition`
- `module.GetTypeReferences()` → `IEnumerable<TypeReference>` (Pass 1)
- `module.Types` → `IEnumerable<TypeDefinition>` (Pass 2 iteration root)
- `t.Methods` → `Collection<MethodDefinition>`
- `m.PInvokeInfo` → nullable `PInvokeInfo`; non-null only for `[DllImport]`-decorated methods
- `m.PInvokeInfo.Module.Name` → string DllImport module name (e.g., `"libpulse-simple"`, `"AudioToolbox"`)

## Acceptance Verification

### Source-grep assertions

| Assertion | Expected | Actual |
|-----------|----------|--------|
| `grep -c "<PackageReference Include=\"Mono.Cecil\" Version=\"0.11.5\"" flow-lang.Tests.csproj` | 1 | **1** ✓ |
| `grep -c "Mono.Cecil" flow-lang.Tests.csproj` | ≥ 1 | **2** ✓ (ref + comment) |
| `grep -c "FlowTargetFact" AssemblyReferenceScanTests.cs` | ≥ 2 | **2** ✓ (one per Fact, plus the `using` — `grep` counts substring occurrences; method-attribute count is exactly 2) |
| `grep -c "Mono.Cecil" AssemblyReferenceScanTests.cs` | ≥ 1 | **1** ✓ |
| `ForbiddenTypeRefPrefixes` element count | 3 | **3** ✓ (Rug.Osc, RtMidi.Core, System.IO.FileSystemWatcher) |
| `ForbiddenPInvokeSubstrings` element count | 2 | **2** ✓ (libpulse, AudioToolbox) |
| Anchor type is `FlowLang.Core.FlowEngine` | yes | **yes** ✓ (`typeof(FlowLang.Core.FlowEngine).Assembly.Location` × 2) |
| File LOC | ≥ 50 | **120** ✓ |

### Build assertions

| Build invocation | Expected | Actual |
|------------------|----------|--------|
| `dotnet restore flow-lang.Tests` | exit 0 | **exit 0** ✓ |
| `dotnet build flow-lang.Tests` (Desktop default) | exit 0 | **exit 0** ✓ (82 warnings — all pre-existing) |

### xUnit fixture results

| Fixture | Result |
|---------|--------|
| `AssemblyReferenceScanTests` (Desktop) | **2/2 SKIPPED** ✓ with reason `"Skipped on Desktop — test runs under: Web"` |
| Phase47 fixture (all 24 facts including new) | **16 PASSED, 8 SKIPPED, 0 FAILED** ✓ (was 16+6; +2 SKIPPED from this plan) |
| Full Desktop suite | **2127 PASSED, 9 SKIPPED, 0 FAILED** ✓ (was 2127+7; +2 SKIPPED from this plan; zero regression) |

### Web-side behavior (forward-looking)

Web-side GREEN/RED execution of both Facts is deferred to Plan 47-06's tag sweep that closes the 18-file Sfz/Osc test-project cascade-fail under FlowTarget=Web (documented in 47-04-SUMMARY). Once Plan 47-06 lands:

- **Expected GREEN:** `WebBuild_HasNoRefsToStrippedNamespaces` Fact — provided Plan 47-01's strip-list + Plan 47-03's `#if !FLOW_WEB` guards are correctly applied
- **Expected GREEN:** `WebBuild_RetainsLegitimateRefs` Fact — provided over-stripping has not occurred

If either Fact fires RED under FlowTarget=Web at Plan 47-06 close, the failure indicates either (a) a stripped dependency leaked back into the Web build, or (b) the strip-list is too aggressive and shed a legitimate dep.

## Deviations from Plan

### None

All three tasks (Task 0 pre-approved gate + Task 1 csproj edit + Task 2 test file) executed exactly per the plan body. The Mono.Cecil API surface in the plan's `<interfaces>` block compiled cleanly on first attempt — no Rule 1 API-shape corrections needed (contrasts with Plan 47-04's DryWetMidi NoteOnEvent property-init shape that required positional-constructor correction).

No Rule 2 (missing critical functionality), Rule 3 (blocking issues), or Rule 4 (architectural decisions) escalations triggered.

## Authentication Gates

None.

## Decisions Made

- **D-47-14 honored** — AssemblyReferenceScanTests ships as a Web-only invariant gate. Forbidden type-ref prefix list locked at exactly 3 entries (Rug.Osc, RtMidi.Core, System.IO.FileSystemWatcher). Forbidden P/Invoke substring list locked at exactly 2 entries (libpulse, AudioToolbox).
- **Package Legitimacy Gate pre-approved** — Composer pre-approved Mono.Cecil 0.11.5 in execution prompt; recorded rationale in Task 1 commit message and proceeded without interactive `checkpoint:human-verify` pause. Per CLAUDE.md installer convention: `[VERIFIED]` canonical package (jbevain / MIT / 1.4B+ downloads / zero transitive deps).
- **Plan code sample adopted verbatim** — no Rule 1 deviations needed. Mono.Cecil 0.11.5 API surface from the plan body's `<interfaces>` block (`AssemblyDefinition.ReadAssembly`, `MainModule.GetTypeReferences`, `Types`, `Methods`, `PInvokeInfo.Module.Name`) all compile cleanly on .NET 10.
- **Web-side execution deferred to Plan 47-06** — per plan body, Plan 47-05 only requires the test compiles cleanly on Desktop and the 2 Facts skip with documented reason. Actual Web-side GREEN/RED behavior of both Facts becomes load-bearing once Plan 47-06 closes the 18-file Sfz/Osc test-project cascade-fail under FlowTarget=Web.
- **Anchor type choice** — `FlowLang.Core.FlowEngine` chosen as the anchor because it (a) is in the canonical public namespace, (b) is consumed by every test fixture so it's guaranteed to be in the runtime closure, (c) has been stable across Phases 1-47 so the anchor won't break under future refactors.

## Threat Flags

None new. Per Plan 47-05 threat register:

| Threat ID | Disposition | Status |
|-----------|-------------|--------|
| T-47-05-SC (Mono.Cecil supply chain) | mitigate | **MITIGATED** via composer pre-approval of canonical package (jbevain maintainer, MIT, 1.4B+ downloads, zero transitive deps). Dev-only test dependency — never in flow-lang.dll runtime closure. |

No new runtime input perimeter introduced (reflective read-only assembly inspection).

## Known Stubs

None — Plan 47-05 is mechanically complete for its deliverable. The Web-side execution of both Facts is deferred to Plan 47-06's tag sweep, but this is a load-bearing Plan 47-06 closer task (tracked in 47-04-SUMMARY's 18-file list), not a stub in this plan.

## Files Touched

```text
flow-lang.Tests/flow-lang.Tests.csproj                                              (MODIFIED, +6 lines)
flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs                   (NEW, 120 LOC)
```

## Commits

| Hash | Type | Description |
|------|------|-------------|
| `5c6129c` | chore | add Mono.Cecil 0.11.5 PackageReference for assembly-reference scan |
| `25b40ea` | test | pin AssemblyReferenceScanTests — Mono.Cecil scan of Web flow-lang.dll |

## Self-Check: PASSED

- File `flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs` exists with 2 `[FlowTargetFact("Web")]` methods + Mono.Cecil reference + `FlowLang.Core.FlowEngine` anchor ✓
- File `flow-lang.Tests/flow-lang.Tests.csproj` contains `<PackageReference Include="Mono.Cecil" Version="0.11.5" />` ✓
- `ForbiddenTypeRefPrefixes` array contains exactly `"Rug.Osc"`, `"RtMidi.Core"`, `"System.IO.FileSystemWatcher"` (3 elements) ✓
- `ForbiddenPInvokeSubstrings` array contains exactly `"libpulse"`, `"AudioToolbox"` (2 elements) ✓
- Commit `5c6129c` present in `git log` ✓
- Commit `25b40ea` present in `git log` ✓
- `dotnet restore flow-lang.Tests` exits 0 ✓
- `dotnet build flow-lang.Tests` exits 0 (82 warnings, all pre-existing) ✓
- `AssemblyReferenceScanTests` Facts 2/2 SKIPPED on Desktop with `"Skipped on Desktop — test runs under: Web"` reason ✓
- Phase47 fixture (all 24 facts): 16 PASSED + 8 SKIPPED + 0 FAILED ✓ (was 16+6; +2 SKIPPED attributable to this plan)
- Full Desktop test suite: 2127 PASSED + 9 SKIPPED + 0 FAILED ✓ (was 2127+7; +2 SKIPPED attributable to this plan; zero regression)
- `BuildConditioningSmokeTests` (Plan 47-01) no regression ✓
- `WebAudioBackendStubTests` (Plan 47-02) no regression ✓
- `WebTargetGuardTests` (Plan 47-03) no regression ✓
- `DryWetMidiWasmCompatTests` + `WebTargetParserTests` + `WebTargetModuleLoaderTests` (Plan 47-04) no regression ✓
