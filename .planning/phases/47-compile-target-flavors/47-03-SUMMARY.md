---
phase: 47-compile-target-flavors
plan: 03
subsystem: build
tags:
  - phase-47
  - wave-3
  - load-bearing
  - compile-target
  - web-build
requirements: [REQ-WEB-TARGET-04, REQ-WEB-TARGET-05, REQ-WEB-TARGET-06, REQ-WEB-TARGET-07]
dependency-graph:
  requires:
    - "Plan 47-01 (FLOW_WEB define + <Compile Remove> strip-list)"
    - "Plan 47-02 (WebAudioBackend stub + DetectBackend Web-first probe)"
  provides:
    - "FlowEngine.IsWebTarget + FlowEngine.SupportsLiveBlocks compile-time-constant static flags"
    - "#if !FLOW_WEB guards at FlowEngine.cs:185 (SfzBuiltins.Register) + 202 (OscFunctions.Register)"
    - "#if !FLOW_WEB guard at BuiltInFunctions.cs:1027 (Audio.InputFunctions.RegisterContextDependent)"
    - "#if !FLOW_WEB guards on all Sfz/Osc/PulseAudio consuming sites (7 files total — Rule 3 deviation)"
    - "ModuleLoader.LoadModule charitable WarnOnce advisory + ModuleLoadResult.Error for @sfz/@osc on Web target"
    - "Parser.ParseStatement parse-time gate on TokenType.Live with Rust-style ParseException pointing at source line"
    - "WebTargetGuardTests xUnit fixture (4 Facts pinning Desktop-side behavior)"
    - "Web build success: dotnet build flow-lang -p:FlowTarget=Web exits 0 (was 13 errors)"
    - "BuildConditioningSmokeTests.WebBuild_ExitCodeIsZero now GREEN (was expected-RED)"
  affects:
    - "Plan 47-04 (DryWetMidi WASM-compat + FlowTargetFact attribute sweep) — Web build is now clean for Plan 47-04 to layer test-attribute discrimination on top"
    - "Plan 47-05 (Mono.Cecil AssemblyReferenceScanTests) — consumes the clean Web-built flow-lang.dll for reference-scanning invariants"
    - "Plan 47-06 (closer) — VERIFICATION sweep + ROADMAP/STATE updates"
tech-stack:
  added: []
  patterns:
    - "Compile-time-constant static property via #if FLOW_WEB ternary initializer (zero-runtime-cost gate flag)"
    - "#if !FLOW_WEB guard at consuming sites of stripped types (Plan 47-01 strip-list extension)"
    - "Charitable WarnOnce stderr advisory with [target] prefix + sentinel key target:stripped-module:<path>"
    - "Parse-time Rust-style ParseException at block-syntax keyword (mirrors Phase 26 D-15 stray-arithmetic pattern at Parser.cs:286-290)"
    - "Compile-time constant fold: FlowEngine.IsWebTarget reads as Desktop=false constant — Roslyn dead-code-eliminates the entire if-body on Desktop builds"
key-files:
  created:
    - "flow-lang.Tests/Integration/Phase47/WebTargetGuardTests.cs"
  modified:
    - "flow-lang/Core/FlowEngine.cs"
    - "flow-lang/StandardLibrary/BuiltInFunctions.cs"
    - "flow-lang/Runtime/ModuleLoader.cs"
    - "flow-lang/Parsing/Parser.cs"
    - "flow-lang/Audio/AudioPlaybackManager.cs"
    - "flow-lang/Interpreter/Interpreter.cs"
    - "flow-lang/Runtime/ExecutionContext.cs"
    - "flow-lang/Runtime/Value.cs"
    - "flow-lang/StandardLibrary/Audio/SongRenderer.cs"
    - "flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs"
decisions:
  - "D-47-08 honored: ACTUAL call sites used (FlowEngine.cs:185/202 + BuiltInFunctions.cs:1027) per PATTERNS.md Discrepancy 1 — NOT the misnamed RegisterSfz()/RegisterOsc() wrapper methods from CONTEXT.md prose (those don't exist in code)"
  - "D-47-09 honored: parse-time gate for live blocks (block syntax requires Rust-style diagnostic, not runtime advisory); module-load gate for @sfz/@osc uses charitable WarnOnce + ModuleLoadResult.Error"
  - "D-47-10 honored: IsWebTarget + SupportsLiveBlocks are compile-time-constant static properties — no runtime mutation, Roslyn constant-folds Desktop code paths"
  - "D-47-11 + D-47-12 honored: only @sfz + @osc trigger the ModuleLoader gate (Phase 29 sampled instruments fall back transparently via SampleCache null-return; pure-Flow stdlibs @improv/@patterns/@generative/@notation-io stay on Web)"
  - "Rule 3 deviation: extended Task 1's scope to 7 additional consumer-site files (AudioPlaybackManager, Interpreter, ExecutionContext Snapshot/Restore, Value factories, SongRenderer sampler dispatch + adapter class, TestSnapshot SfzPatchRegistry property). The plan's <success_criteria> required Web build to succeed end-to-end — without these guards 10 errors remained after the plan's named scope"
  - "Charitable interpretation per CLAUDE.md: SongRenderer sampler:NAME dispatch on Web target throws InvalidOperationException with target-aware message rather than silently no-oping — composers get clear remediation"
  - "PATTERNS.md §Discrepancy 2 'IsAudioAvailable open question' from Plan 47-02 SUMMARY line 191 closed: the body now branches via #if to return WebAudioBackend.IsAvailable() on Web target"
metrics:
  duration: ~25min
  completed: 2026-05-25
  tasks_completed: 5
  files_created: 1
  files_modified: 10
  commits: 5
---

# Phase 47 Plan 03: FlowEngine + Parser + ModuleLoader Web-Target Guards Summary

## One-liner

Load-bearing plan that flips `dotnet build flow-lang -p:FlowTarget=Web` from 13-errors-RED to 0-errors-GREEN: added `FlowEngine.IsWebTarget`/`SupportsLiveBlocks` compile-time-constant static flags, `#if !FLOW_WEB` guards on `SfzBuiltins.Register`/`OscFunctions.Register`/`InputFunctions.RegisterContextDependent` plus 7 additional consumer-site files (Rule 3 deviation), `ModuleLoader` charitable WarnOnce advisory for `@sfz`/`@osc` imports, and `Parser` Rust-style `ParseException` for `live { }` blocks — pinned by a 4-Fact xUnit fixture that flipped `BuildConditioningSmokeTests.WebBuild_ExitCodeIsZero` from expected-RED to GREEN.

## What Was Done

### Task 1 — FlowEngine static flags + SfzBuiltins/OscFunctions guards (commit `dfa359f`)

Two additions to `flow-lang/Core/FlowEngine.cs`:

**Static properties** (inserted after `CurrentExecutionContext` at line ~99):

```csharp
public static bool IsWebTarget { get; } =
#if FLOW_WEB
    true;
#else
    false;
#endif

public static bool SupportsLiveBlocks { get; } = !IsWebTarget;
```

Both are compile-time constants — Roslyn constant-folds the Desktop branch to `false`/`true` respectively, dead-code-eliminating any `if (IsWebTarget) ...` block on Desktop builds. Mirrors PATTERNS.md §Pattern 3 (static type-flag init) shape exactly.

**Registration guards** at the actual call sites per PATTERNS.md §Discrepancy 1:

- `SfzBuiltins.Register(internalRegistry, _context)` at line ~189 wrapped in `#if !FLOW_WEB`
- `OscFunctions.Register(internalRegistry, _context)` at line ~206 wrapped in `#if !FLOW_WEB`

Also wrapped (Rule 3 — required to actually link the Web build with the field types stripped):
- `using FlowLang.StandardLibrary.Audio.Sfz;` (line 9)
- `private readonly SfzSampleCache _sfzSampleCache;` field (line 34)
- `public static SfzSampleCache? CurrentSfzSampleCache { get; private set; }` (line 93)
- `_sfzSampleCache = new SfzSampleCache();` + `CurrentSfzSampleCache = _sfzSampleCache;` constructor uses (lines 139, 143)
- `if (ReferenceEquals(CurrentSfzSampleCache, _sfzSampleCache)) CurrentSfzSampleCache = null;` Dispose-time clear (line ~471)

Web build progressed: 13 errors → 10 errors after this commit.

### Task 2 — BuiltInFunctions.cs guard + Rule 3 sweep of all remaining Sfz/Osc consumers (commit `9600ddb`)

Wrapped 7 additional consuming sites in `#if !FLOW_WEB` to close the remaining 10 Web build errors:

| File | What was guarded |
|---|---|
| `StandardLibrary/BuiltInFunctions.cs:1027` | `Audio.InputFunctions.RegisterContextDependent` call (Task 2 as plan-written) |
| `Audio/AudioPlaybackManager.cs:73-95` | `IsAudioAvailable()` body — Web branch returns `WebAudioBackend.IsAvailable()` (closes 47-02 SUMMARY's "Open question" line 191) |
| `Interpreter/Interpreter.cs:927-936` | Sfz-type variable declaration handler (`SfzType` + `SfzPatchRegistry`) |
| `Runtime/ExecutionContext.cs:547` | `SfzPatchRegistry` property declaration |
| `Runtime/ExecutionContext.cs:1119-1216` | `SnapshotState` + `RestoreState` SfzPatchRegistry clone/restore branches |
| `Runtime/Value.cs:71` | `Value.Sfz(SfzData)` static factory |
| `Runtime/Value.cs:115` | `Value.OscHandle(OscHandleData)` static factory |
| `StandardLibrary/Audio/SongRenderer.cs:6` | `using FlowLang.StandardLibrary.Audio.Sfz;` |
| `StandardLibrary/Audio/SongRenderer.cs:167-176` | `sampler:NAME` dispatch — Web target throws `InvalidOperationException` with target-aware message (charitable interpretation per CLAUDE.md) |
| `StandardLibrary/Audio/SongRenderer.cs:545-716` | `RenderSongWithSfz` method + `SfzNoteSynthesizer` adapter class |
| `StandardLibrary/TestFramework/TestSnapshot.cs:4` | `using FlowLang.StandardLibrary.Audio.Sfz;` |
| `StandardLibrary/TestFramework/TestSnapshot.cs:60` | `SfzPatchRegistry` property declaration |

Web build: 10 errors → **0 errors**. Desktop build: still 0 errors (byte-identical contract preserved — all guards are around opt-in `use "@sfz"`/`use "@osc"` paths that were already composer-opt-in).

### Task 3 — ModuleLoader stripped-module gate (commit `905b819`)

Added a `private static bool IsStrippedOnWeb(string)` helper at top of `ModuleLoader` returning true only for `"@sfz"` / `"@osc"`. Inserted the gate inside `LoadModule` immediately after the `_loadedModules.Contains` short-circuit:

```csharp
if (Core.FlowEngine.IsWebTarget && IsStrippedOnWeb(path))
{
    Diagnostics.RenderingDiagnostics.WarnOnce(
        $"target:stripped-module:{path}",
        $"[target] module '{path}' unavailable on Web target — line {errorLocation.Line}. " +
        $"Build with FlowTarget=Desktop to enable, or run with `flow run script.flow` locally.");
    return ModuleLoadResult.Error;
}
```

Per D-47-11 + D-47-12: only `@sfz` and `@osc` trigger the gate. `@notation-io` / `@improv` / `@patterns` / `@generative` stay on Web (pure-Flow stdlib + hand-rolled XmlWriter, no native deps). `FlowEngine.IsWebTarget` is a compile-time constant → entire if-body dead code on Desktop.

Per-process per-sentinel-key dedup via existing `RenderingDiagnostics.WarnOnce` infra — multiple `use "@sfz"` across files only fires the advisory once. Composer UX matches Phase 36 PAT-02 / Phase 38 D-38-13 charitable patterns.

### Task 4 — Parser parse-time live-block gate (commit `d0b8b11`)

Wrapped the existing `if (Match(TokenType.Live)) return ParseLiveBlockStatement();` dispatch (Parser.cs:220) with a `SupportsLiveBlocks` check:

```csharp
if (Match(TokenType.Live))
{
    if (!Core.FlowEngine.SupportsLiveBlocks)
    {
        var liveTok = PreviousToken;
        throw new ParseException(
            $"`live` block requires Desktop target — line {liveTok.Location.Line}. " +
            $"Build with FlowTarget=Desktop or run with `flow run script.flow` locally.");
    }
    return ParseLiveBlockStatement();
}
```

Mirrors Phase 26 D-15 stray-arithmetic ParseException at lines 286-290 (existing precedent). `PreviousToken` is the consumed `live` keyword token; `liveTok.Location.Line` quotes the source line.

D-47-09 cascade-break preservation: `LiveBlockStatement.cs` + `LiveBlockRegistry.cs` stay in the Web build (Plan 47-01 strip-list correctly excluded them — Interpreter.cs case-dispatch + ExecutionContext property would cascade-break if stripped). The parse-time throw prevents `ParseLiveBlockStatement()` from ever firing under Web; the AST types remain referenceable but unreachable at runtime.

Desktop verification: 16/16 LiveBlock tests stay GREEN — the throw branch is dead code on Desktop (Roslyn constant-fold).

### Task 5 — WebTargetGuardTests xUnit fixture (commit `8f6b814`)

Created `flow-lang.Tests/Integration/Phase47/WebTargetGuardTests.cs` (74 LOC) with 4 `[Fact]` methods pinning Desktop-side behavior:

| Fact | Asserts |
|---|---|
| `IsWebTarget_IsFalse_OnDesktopBuild` | `FlowEngine.IsWebTarget == false` (test assembly compiles without FLOW_WEB) |
| `SupportsLiveBlocks_IsTrue_OnDesktopBuild` | `FlowEngine.SupportsLiveBlocks == true` (parser branch not gated on Desktop) |
| `LiveBlock_Parses_OnDesktop` | `live 1bar { Int x = 1; (print x); }` parses + executes successfully via `engine.Execute(src, "<test>")` |
| `UseSfzImport_NoTargetAdvisory_OnDesktop` | `use "@sfz";` on Desktop loads sfz.flow without stderr containing `[target] module '@sfz' unavailable on Web target` (asserts the ModuleLoader gate is dead code on Desktop) |

Web-side counterparts (asserting `IsWebTarget=true`, live block throws ParseException, @sfz emits advisory) defer to Plan 47-04's `FlowTargetFact` attribute.

**All 4 Facts GREEN** (114ms total). **`BuildConditioningSmokeTests.WebBuild_ExitCodeIsZero` flipped from expected-RED to GREEN** — the load-bearing acceptance gate is closed.

## Acceptance Verification

### Source-grep assertions

| Assertion | Expected | Actual |
|---|---|---|
| `grep -c "public static bool IsWebTarget" flow-lang/Core/FlowEngine.cs` | 1 | **1** ✓ |
| `grep -c "public static bool SupportsLiveBlocks" flow-lang/Core/FlowEngine.cs` | 1 | **1** ✓ |
| `grep -c "#if !FLOW_WEB" flow-lang/Core/FlowEngine.cs` | ≥ 2 | **8** ✓ |
| `grep -c "#if FLOW_WEB" flow-lang/Core/FlowEngine.cs` | ≥ 1 | **1** ✓ |
| `grep -c "#if !FLOW_WEB" flow-lang/StandardLibrary/BuiltInFunctions.cs` | ≥ 1 | **1** ✓ |
| `grep -c "private static bool IsStrippedOnWeb" flow-lang/Runtime/ModuleLoader.cs` | 1 | **1** ✓ |
| `grep -c "target:stripped-module:" flow-lang/Runtime/ModuleLoader.cs` | ≥ 1 | **1** ✓ |
| `grep -c "FlowEngine.IsWebTarget" flow-lang/Runtime/ModuleLoader.cs` | ≥ 1 | **2** ✓ |
| `grep -c "Core.FlowEngine.SupportsLiveBlocks" flow-lang/Parsing/Parser.cs` | ≥ 1 | **1** ✓ |
| `grep -c "live..* block requires Desktop target" flow-lang/Parsing/Parser.cs` | 1 | **1** ✓ |

### Build assertions

| Build invocation | Expected | Actual |
|---|---|---|
| `dotnet build flow-lang -p:FlowTarget=Desktop` | exit 0 | **exit 0** ✓ |
| `dotnet build flow-lang -p:FlowTarget=Web` | exit 0 (load-bearing) | **exit 0** ✓ (13 errors → 0) |
| `dotnet build flow-lang.Tests` | exit 0 | **exit 0** ✓ |

### xUnit fixture results

| Test fixture | Result |
|---|---|
| `WebTargetGuardTests` | **4/4 GREEN** ✓ (114ms) |
| `BuildConditioningSmokeTests` | **3/3 GREEN** ✓ (was 2/3 with WebBuild expected-RED — now GREEN) |
| `WebAudioBackendStubTests` | **7/7 GREEN** ✓ (no regression from Plan 47-02) |
| `LiveBlock*` (Phase 38) | **16/16 GREEN** ✓ (no Desktop parse regression) |
| Full Desktop suite (2nd run) | **2125 passed, 1 skipped, 0 failed** ✓ |

Note on the 1st full-suite run: a flaky `FlowConfigPropagationTests.Setting_Active_DefaultTempo_Propagates_To_New_MusicalContext` failure surfaced (test parallelization race in Phase 30 FlowConfig singleton state). The test passes in isolation and on the 2nd full run. Not introduced by Plan 47-03 — confirmed by isolation-run + 2nd-run-clean.

## Deviations from Plan

### Rule 3 auto-fix — extended Task 1's scope to 7 additional consumer-site files

**Trigger:** Plan 47-03 explicitly scopes Tasks 1+2 to FlowEngine.cs + BuiltInFunctions.cs:1027. After applying those guards the Web build still had 10 errors in 4 additional files (SongRenderer.cs, TestSnapshot.cs, ExecutionContext.cs, Value.cs) referencing the now-stripped Sfz/Network types. The plan's `<success_criteria>` requires "Build with FlowTarget=Web SUCCEEDS" — without the additional guards, the load-bearing acceptance gate (`BuildConditioningSmokeTests.WebBuild_ExitCodeIsZero`) cannot flip from RED to GREEN.

**Issue:** 10 consumer-site Sfz/Network type references that the plan's named-file scope didn't cover.

**Fix:** Applied #if !FLOW_WEB guards at each consumer site. Each guard is mechanically straightforward and exactly matches the strip-list contract from Plan 47-01.

**Files modified (Rule 3 auto-fix):**
- `flow-lang/Audio/AudioPlaybackManager.cs` (IsAudioAvailable() — also closes Plan 47-02 SUMMARY's "Open question" line 191)
- `flow-lang/Interpreter/Interpreter.cs` (Sfz-type variable declaration handler)
- `flow-lang/Runtime/ExecutionContext.cs` (SfzPatchRegistry property + Snapshot/Restore branches)
- `flow-lang/Runtime/Value.cs` (Sfz + OscHandle factories)
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` (sampler:NAME dispatch + RenderSongWithSfz + SfzNoteSynthesizer adapter)
- `flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs` (SfzPatchRegistry property)

**Commit:** included in `9600ddb` (alongside Task 2's plan-named change to BuiltInFunctions.cs:1027). Commit message documents the Rule 3 expansion explicitly.

### No Rule 1 / Rule 2 / Rule 4 escalations

No bugs auto-fixed (Rule 1), no missing-functionality additions (Rule 2), no architectural escalations (Rule 4). Tasks 3 + 4 + 5 executed verbatim from the plan body.

## Authentication Gates

None.

## Decisions Made

- **Honored D-47-08** — guard at actual call sites (FlowEngine.cs:185/202 + BuiltInFunctions.cs:1027) per PATTERNS.md Discrepancy 1, NOT the invented `RegisterSfz()`/`RegisterOsc()` wrapper methods from CONTEXT.md prose.
- **Honored D-47-09** — parse-time gate for `live` blocks (block syntax → Rust-style ParseException), runtime gate for `@sfz`/`@osc` imports (charitable WarnOnce advisory).
- **Honored D-47-10** — IsWebTarget + SupportsLiveBlocks are compile-time constants via `#if FLOW_WEB` ternary initializer; no `private set;` runtime mutation.
- **Honored D-47-11 + D-47-12** — only `@sfz` + `@osc` trigger the ModuleLoader gate. Phase 29 sampled instruments fall back transparently via SampleCache null-return (existing pattern, zero new code).
- **Charitable interpretation per CLAUDE.md** — SongRenderer sampler:NAME dispatch on Web target throws InvalidOperationException with target-aware "build with FlowTarget=Desktop" message rather than silently no-oping. Composers get clear remediation.
- **Closed Plan 47-02 open question** — AudioPlaybackManager.IsAudioAvailable() body wrapped with #if !FLOW_WEB. On Web target it returns WebAudioBackend.IsAvailable() (honest feature detection — Phase 48 will fill in the JS-interop bodies).

## Threat Flags

None. Per Plan 47-03 threat register: no new attack surface introduced. The ModuleLoader gate uses existing WarnOnce dedup (per-process per-sentinel-key) so repeated `use "@sfz"` cannot flood stderr. No new package references added.

## Known Stubs

None — Plan 47-03 is mechanically complete. The `WebAudioBackend` stub from Plan 47-02 is unchanged and tracked separately.

## Files Touched

```
flow-lang/Core/FlowEngine.cs                                  (+56 lines)
flow-lang/StandardLibrary/BuiltInFunctions.cs                 (+9 lines)
flow-lang/Runtime/ModuleLoader.cs                             (+38 lines)
flow-lang/Parsing/Parser.cs                                   (+22 lines)
flow-lang/Audio/AudioPlaybackManager.cs                       (+8 lines / IsAudioAvailable Web branch)
flow-lang/Interpreter/Interpreter.cs                          (+5 lines / Sfz-type guard)
flow-lang/Runtime/ExecutionContext.cs                         (+13 lines / property + Snapshot/Restore guards)
flow-lang/Runtime/Value.cs                                    (+6 lines / Sfz + OscHandle factory guards)
flow-lang/StandardLibrary/Audio/SongRenderer.cs               (+18 lines / dispatch + RenderSongWithSfz + adapter)
flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs       (+8 lines / using + SfzPatchRegistry property)
flow-lang.Tests/Integration/Phase47/WebTargetGuardTests.cs    (NEW, 74 lines)
```

## Commits

| Hash | Type | Description |
|---|---|---|
| `dfa359f` | feat | wire FlowEngine IsWebTarget + SupportsLiveBlocks static flags |
| `9600ddb` | feat | close Web build via #if !FLOW_WEB guards on all Sfz/Osc consumers |
| `905b819` | feat | add ModuleLoader stripped-module gate with charitable advisory |
| `d0b8b11` | feat | add parse-time gate for live blocks under Web target |
| `8f6b814` | test | pin Desktop-side Web-target guard behavior (4 Facts) |

## Self-Check: PASSED

- File `flow-lang/Core/FlowEngine.cs` exists and contains `public static bool IsWebTarget` + `public static bool SupportsLiveBlocks` ✓
- File `flow-lang/Runtime/ModuleLoader.cs` exists and contains `IsStrippedOnWeb` + `target:stripped-module:` ✓
- File `flow-lang/Parsing/Parser.cs` exists and contains `Core.FlowEngine.SupportsLiveBlocks` ✓
- File `flow-lang.Tests/Integration/Phase47/WebTargetGuardTests.cs` exists with 4 [Fact] methods ✓
- Commit `dfa359f` present in `git log` ✓
- Commit `9600ddb` present in `git log` ✓
- Commit `905b819` present in `git log` ✓
- Commit `d0b8b11` present in `git log` ✓
- Commit `8f6b814` present in `git log` ✓
- `dotnet build flow-lang -p:FlowTarget=Desktop` exits 0 ✓
- `dotnet build flow-lang -p:FlowTarget=Web` exits 0 (was 13 errors at Plan 47-02 close) ✓
- `WebTargetGuardTests` 4/4 GREEN ✓
- `BuildConditioningSmokeTests` 3/3 GREEN (WebBuild_ExitCodeIsZero flipped from RED to GREEN) ✓
- `WebAudioBackendStubTests` 7/7 GREEN (no regression from Plan 47-02) ✓
- Full Desktop test suite: 2125 passed, 1 skipped, 0 new failures vs Plan 47-02 baseline ✓
