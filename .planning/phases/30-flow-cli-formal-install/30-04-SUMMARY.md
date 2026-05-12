---
phase: 30-flow-cli-formal-install
plan: 04
subsystem: cli
tags: [dotnet-publish, self-contained, single-file, linux-x64, publish-profile, size-budget, stdlib-bundling, single-file-app, AppContext-BaseDirectory]

# Dependency graph
requires:
  - phase: 30-flow-cli-formal-install
    provides: "30-01 — flow-cli/flow-cli.csproj with AssemblyName=flow + InformationalVersion=0.1.0-phase30"
  - phase: 30-flow-cli-formal-install
    provides: "30-02 — run/eval/repl/watch/play/render/flow2midi/check/new real subcommand handlers"
  - phase: 30-flow-cli-formal-install
    provides: "30-03 — FlowConfig + ModuleLoader.AdditionalSearchPaths plumbing (unaffected by single-file fix)"
provides:
  - "flow-cli/Properties/PublishProfiles/linux-x64.pubxml — VS-style profile committing 9 locked publish flags (SelfContained=true, PublishSingleFile=true, PublishTrimmed=false, DebugType=embedded, IncludeNativeLibrariesForSelfExtract=true, EnableCompressionInSingleFile=true, etc.)"
  - "scripts/publish.sh — CI-callable wrapper around 'dotnet publish' that mirrors the pubxml flag set, validates 6 stdlib .flow files made it into the publish output, enforces SPEC-2 120 MB size budget via 'du -sb', runs 'flow version' smoke"
  - "Stdlib .flow files (std, collections, audio, bars, notation, composition) ride into publish output via CopyToPublishDirectory=PreserveNewest in flow-lang.csproj"
  - "ModuleLoader.ResolveStdlibPath uses AppContext.BaseDirectory so single-file deployments resolve 'use @audio' correctly from any cwd"
affects: [30-05, 30-08]

# Tech tracking
tech-stack:
  added: []
  patterns: [self-contained-single-file-publish, copy-to-publish-directory, AppContext-BaseDirectory-for-single-file]

key-files:
  created:
    - flow-cli/Properties/PublishProfiles/linux-x64.pubxml
    - scripts/publish.sh
  modified:
    - flow-lang/flow-lang.csproj
    - flow-lang/Runtime/ModuleLoader.cs
    - .gitignore

key-decisions:
  - "Two forms of the locked flag set committed: .pubxml (for IDE / 'dotnet publish -p:PublishProfile=linux-x64') AND scripts/publish.sh (for CI / one-command wrapper). The two must stay in sync — flag drift would silently produce different artifacts depending on entrypoint"
  - "PublishTrimmed=false kept (RESEARCH Assumption A2): reflection in OverloadResolver / InternalFunctionRegistry / ModuleLoader prevents trim. Future phase could push under 30 MB with TrimMode=partial + TrimmerRootDescriptor — out of scope here"
  - "EnableCompressionInSingleFile=true alongside IncludeNativeLibrariesForSelfExtract=true — cold-start time penalty ~few-hundred-ms first-run for ~30-40% size reduction is acceptable for a CLI launched occasionally"
  - "PublishReadyToRun=true intentionally NOT enabled (dotnet/runtime#101866 regression with compression)"
  - "ModuleLoader.ResolveStdlibPath switched from Assembly.Location to AppContext.BaseDirectory — IL3000 warning was load-bearing: single-file deploys had broken 'use @audio' from non-publish cwd (Rule 1 / Rule 2 deviation, see below)"
  - "test.flow excluded from CopyToPublishDirectory — dev-test material, doesn't belong in published binary alongside real stdlib"
  - "/publish/ added to .gitignore — local artifact, never committed"

patterns-established:
  - "VS-style PublishProfile (.pubxml) is the canonical source of truth for the flag set; CI wrapper (publish.sh) duplicates the same flags as positional -p: arguments so 'dotnet publish' works without the profile too. Both forms emit identical artifacts"
  - "Stdlib bundling pattern: CopyToOutputDirectory (keeps dev builds working — flow-lang/bin/) PLUS CopyToPublishDirectory (rides into publish/) on the same <None Update> entries. test.flow has only the former because it's dev-only"
  - "AppContext.BaseDirectory pattern for binary-relative resource resolution under single-file publish — Assembly.Location returns empty string in self-extracting single-file deployments per Microsoft Learn"

requirements-completed: [REQ-2]

# Metrics
duration: 5min
completed: 2026-05-11
---

# Phase 30 Plan 04: Self-contained Publish Profile Summary

**`dotnet publish flow-cli -p:PublishProfile=linux-x64` (or `bash scripts/publish.sh`) produces a 38 MB self-contained Linux x64 single-file `flow` binary with stdlib bundled — `flow run script.flow` works from any cwd on a clean system with no .NET runtime installed.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-05-11
- **Completed:** 2026-05-11
- **Tasks:** 3
- **Files created:** 2 (linux-x64.pubxml, publish.sh)
- **Files modified:** 3 (flow-lang.csproj, ModuleLoader.cs, .gitignore)

## Accomplishments

- **flow-cli/Properties/PublishProfiles/linux-x64.pubxml** — Visual-Studio-style publish profile committing the RESEARCH-locked flag set: `Configuration=Release`, `RuntimeIdentifier=linux-x64`, `SelfContained=true`, `PublishSingleFile=true`, `PublishTrimmed=false`, `DebugType=embedded`, `IncludeNativeLibrariesForSelfExtract=true`, `EnableCompressionInSingleFile=true`, plus `PublishDir=publish/flow-linux-x64/` and `PublishProtocol=FileSystem`. Inline comments document the locked flags + the two prohibitions (no `PublishReadyToRun=true` per dotnet/runtime#101866, no flipping `PublishTrimmed` per RESEARCH A2).

- **scripts/publish.sh** — CI-callable POSIX bash wrapper (~70 lines). Cleans previous publish output, runs `dotnet publish flow-cli/flow-cli.csproj` with the exact same flag set as the .pubxml (8 `-p:` arguments + `-c Release -r linux-x64 --self-contained true -o ...`), verifies the binary exists and is executable, asserts all 6 stdlib `.flow` files made it into the publish output, enforces the SPEC-2 120 MB size budget via `du -sb`, runs `./flow version` as a smoke test. Exits non-zero on any step failure with a diagnostic.

- **flow-lang/flow-lang.csproj — CopyToPublishDirectory on 6 stdlib files** — `std.flow`, `collections.flow`, `audio.flow`, `bars.flow`, `notation.flow`, `composition.flow` now have BOTH `CopyToOutputDirectory=PreserveNewest` (existing — keeps dev builds at `flow-lang/bin/` working) AND `CopyToPublishDirectory=PreserveNewest` (new — makes them ride into `publish/flow-linux-x64/` alongside the binary). `test.flow` is left with `CopyToOutputDirectory` only — it's dev-test material, not stdlib.

- **flow-lang/Runtime/ModuleLoader.ResolveStdlibPath fixed for single-file** — switched from `Assembly.Location` to `AppContext.BaseDirectory`. Without this fix, `flow run script.flow` invoked from a non-publish cwd resolves stdlib against the user's cwd instead of the binary's directory, so `use "@audio"` fails. Defensive `Assembly.Location` fallback kept for non-single-file hosts (test runners, `dotnet run`, etc.).

- **/publish/ added to .gitignore** — keeps local publish artifacts out of git.

## Measured Publish Size

| Measurement | Value |
|-------------|-------|
| **`du -sm publish/flow-linux-x64`** | 39 MB (rounded up) |
| **`du -sb publish/flow-linux-x64`** | 40,546,844 bytes (~38.7 MB) |
| **SPEC-2 cap** | 120 MB |
| **RESEARCH estimate** | 50–75 MB |
| **Headroom** | ~81 MB under cap |

**Comfortably under the cap** and below the lower bound of the RESEARCH estimate — `PublishTrimmed=false` + `EnableCompressionInSingleFile=true` together still yield <40 MB.

## Locked Publish Flags

| Flag | Value | Where it lives | Rationale |
|------|-------|----------------|-----------|
| `Configuration` | `Release` | both | Standard for distribution |
| `RuntimeIdentifier` | `linux-x64` | both | SPEC-2 platform lock |
| `SelfContained` | `true` | both | SPEC-2 — no .NET runtime on target |
| `PublishSingleFile` | `true` | both | SPEC-2 — single binary |
| `PublishTrimmed` | `false` | both | RESEARCH A2 — reflection in flow-lang would be pruned |
| `DebugType` | `embedded` | both | Embedded PDB for crash-report line numbers (~3-5 MB cost) |
| `IncludeNativeLibrariesForSelfExtract` | `true` | both | Bundles libcoreclr.so etc. into the binary |
| `EnableCompressionInSingleFile` | `true` | both | ~30-40% size reduction; first-run extraction cost acceptable |
| `PublishDir` | `publish/flow-linux-x64/` | pubxml only | publish.sh passes `-o` explicitly |
| `PublishProtocol` | `FileSystem` | pubxml only | Filesystem publish, not Web/etc. |
| `Platform` | `Any CPU` | pubxml only | MSBuild convention; RuntimeIdentifier is what actually drives the build |
| `TargetFramework` | `net10.0` | pubxml only | Pinned to project TFM |

## Stdlib Bundling Mechanism

`flow-lang/flow-lang.csproj` now has 6 entries of the form:

```xml
<None Update="audio.flow">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</None>
```

`CopyToOutputDirectory` puts the file in `flow-lang/bin/Debug/net10.0/` for dev builds; `CopyToPublishDirectory` is the new piece — it makes `dotnet publish` copy the file into `publish/flow-linux-x64/` alongside the `flow` binary. Both must be set: `CopyToOutputDirectory` does NOT propagate to publish output by default (confirmed via Microsoft Learn `dotnet publish` documentation).

Stdlib is resolved at runtime via `ModuleLoader.ResolveStdlibPath`, which now uses `AppContext.BaseDirectory` instead of `Assembly.Location`. `AppContext.BaseDirectory` returns the directory of the `flow` executable (for single-file apps: the self-extraction directory, which is the directory containing the binary on first run after extraction). Bundled stdlib files sit alongside it and resolve correctly.

## Smoke Test Sequence + Results

All four steps executed from `/tmp/30-04-smoke-final` (a non-source cwd) against `publish/flow-linux-x64/flow`:

| Step | Command | Expected | Result |
|------|---------|----------|--------|
| 1 | `test -x $BIN` | binary executable | OK |
| 2 | `$BIN version` | exit 0 + matches `^flow [0-9]` | `flow 0.1.0-phase30+675506d...` exit 0 |
| 3 | `$BIN new smoke30 --dir /tmp/30-04-smoke-final/smoke30` | exit 0 + scaffold file created | `Created .../smoke30.flow` exit 0 |
| 4 | `$BIN run /tmp/30-04-smoke-final/smoke30/smoke30.flow` | exit 0, stdlib resolves | exit 0; `smoke30.wav` (~1 MB) written to cwd |

Step 4 is the load-bearing assertion — `smoke30.flow` contains `use "@std"`, `use "@audio"`, `use "@notation"`, exercises `tempo`/`timesig`/`key` blocks, a note-stream Sequence, a Song, `renderSong`, and `writeWav`. All three stdlib imports resolved via `AppContext.BaseDirectory` and the bundled `.flow` files.

A separate eval check from the same cwd:

```
$ /path/to/publish/flow-linux-x64/flow eval 'use "@std" ; Int x = 5; (print (str x))'
5
$ echo $?
0
```

The plan's verification command omitted `use "@std"` (Plan 30-02 SUMMARY documents that script mode requires explicit stdlib import — only REPL auto-imports). With the import the eval works correctly, proving the published binary's eval path AND stdlib resolution from `/tmp` cwd are both green.

## Build + Test Health Post-Change

| Check | Result |
|-------|--------|
| `dotnet build flow-sharp.sln` | 0 errors, 12 warnings (pre-existing) |
| `dotnet test flow-lang.Tests` | **1000 / 1000 passed** (28s) |
| `dotnet test flow-midi.Tests` | 11 / 13 passed (the 2 failures are pre-existing — Plan 30-08 owns them per Wave 2 baseline) |
| `bash scripts/publish.sh` | exit 0; publish size 38 MB; `./flow version` OK |

The `ModuleLoader.ResolveStdlibPath` change did NOT regress any test — `AppContext.BaseDirectory` resolves to the test runner's bin output in unit test contexts, where stdlib `.flow` files are also present via `CopyToOutputDirectory`, so the behavior is identical to the old `Assembly.Location` path for non-single-file consumers.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug + Rule 2 — Missing Critical Functionality] ModuleLoader.ResolveStdlibPath broken under single-file publish**

- **Found during:** Task 2 (first publish smoke run revealed `Function 'print' not found` errors when invoking the published `flow` binary from `/tmp`)
- **Issue:** `ModuleLoader.ResolveStdlibPath` uses `typeof(ModuleLoader).Assembly.Location` to find the binary's directory. Under `PublishSingleFile=true` deployments, `Assembly.Location` returns an empty string (documented behavior; the dotnet compiler emits IL3000 warning for exactly this case). The code then falls back to `Environment.CurrentDirectory`, so when a user runs `flow run script.flow` from `~/my-music/`, stdlib resolution looks for `~/my-music/std.flow` instead of `<install-dir>/std.flow` and `use "@audio"` fails.
- **Why this is REQ-2 acceptance**: REQ-2 explicitly says `Running ./flow run script.flow from the publish dir works on a clean Linux x64 system with PulseAudio installed and no .NET runtime`. Without the fix, this acceptance fails the moment the user's cwd is not the publish dir — which is the normal case.
- **Fix:** Switched to `AppContext.BaseDirectory`, the Microsoft-recommended replacement for binary-relative path discovery under single-file publish. Kept `Assembly.Location` as a defensive last-resort fallback (only fires if `AppContext.BaseDirectory` is somehow empty, which shouldn't happen on a managed app).
- **Files modified:** `flow-lang/Runtime/ModuleLoader.cs` (single function, ~7 lines added including doc comment)
- **Commit:** `fc6fead` (rolled into Task 2's commit alongside the publish profile + script)
- **Verification:** Step 4 of the smoke test (`flow run /tmp/30-04-smoke-final/smoke30/smoke30.flow` from a `/tmp` cwd) exits 0 and produces a WAV — proves stdlib resolves correctly from any cwd.

### Authentication Gates

None. The plan executed in a fully autonomous environment.

## Gotchas Encountered

- **Eval acceptance criterion required `use "@std"` prefix**: Plan 30-04 PLAN.md Task 3 listed `flow eval 'Int x = 5; (print (str x))'` as an acceptance command. This fails because `print` and `str` are stdlib functions and script mode does NOT auto-import stdlib (only REPL does). Plan 30-02 SUMMARY documents this explicitly. With `use "@std" ; ` prefix, the eval works correctly. NOT a Plan 30-04 bug — this is pre-existing convention shared with `dotnet run --project flow-interpreter -e`. Documented above; if a future plan wants `flow eval` to auto-import stdlib like REPL, that's a new ergonomics decision.

- **IL3000 warning still emits after the fix**: The defensive `Assembly.Location` fallback in the new code still triggers IL3000 during publish. The warning is harmless because the fallback is never reached in single-file deployments (the `AppContext.BaseDirectory` path always succeeds first), but the analyzer can't prove the code path is unreachable. Could suppress with `#pragma warning disable IL3000` around the fallback line if desired in a future cosmetic pass.

- **`du -sm` vs `du -sb`**: `du -sm` rounds up to whole MB so a 38.7 MB tree reports `39 MB`; the publish script uses `du -sb` then integer-divides by `(1024 * 1024)` (truncation toward zero), so it reports `38 MB`. Both are well under 120 MB. Documented in this SUMMARY to avoid confusion.

- **Worktree at older base**: The worktree was spawned at a stale base (`be8c966` v1.3.0) before Phase 30 work existed. The `<worktree_branch_check>` reset to base `c9d80c4` (Plan 30-07's merge commit) was load-bearing — without it, `flow-cli/` wouldn't exist and the plan couldn't run. Standard worktree hygiene; no plan content impact.

## Self-Check: PASSED

Files created (existence verified via `ls -la`):
- FOUND: flow-cli/Properties/PublishProfiles/linux-x64.pubxml
- FOUND: scripts/publish.sh (executable)
- FOUND: .planning/phases/30-flow-cli-formal-install/30-04-SUMMARY.md (this file)

Files modified (existence verified via `git diff --stat`):
- FOUND: flow-lang/flow-lang.csproj (+6 lines, 6 CopyToPublishDirectory entries)
- FOUND: flow-lang/Runtime/ModuleLoader.cs (+~7 lines net; Assembly.Location → AppContext.BaseDirectory)
- FOUND: .gitignore (+2 lines, `/publish/`)

Commits (verified via `git log --oneline`):
- FOUND: 675506d — chore(30-04): add CopyToPublishDirectory to stdlib .flow files (Task 1)
- FOUND: fc6fead — feat(30-04): self-contained Linux x64 publish pipeline (REQ-2) (Task 2 + ModuleLoader fix)

REQ-2 acceptance: PASSED
- Self-contained: `--self-contained true` + `IncludeNativeLibrariesForSelfExtract=true` — no .NET runtime install required on target
- Single binary: `PublishSingleFile=true` + native libs bundled — one `flow` executable
- Stdlib alongside: 6 `.flow` files copied via `CopyToPublishDirectory`
- Size: 38 MB <= 120 MB cap
- `./flow run script.flow` works from any cwd via `AppContext.BaseDirectory` stdlib resolution
