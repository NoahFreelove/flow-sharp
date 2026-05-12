---
phase: 30-flow-cli-formal-install
plan: 01
subsystem: cli
tags: [system-commandline, dotnet10, cli-scaffolding, subcommand-routing, flow-cli]

# Dependency graph
requires:
  - phase: 28-articulation-multitrack-voicepool
    provides: stable .NET 10 baseline + 6-project solution layout
provides:
  - flow-cli project skeleton (builds clean, produces `flow.dll`)
  - System.CommandLine 2.0.7 RootCommand wiring
  - CommandRegistry.cs central listing of all 11 subcommand entries
  - VersionCommand.cs — `flow version` works end-to-end
  - 10 placeholder subcommand stubs (run/eval/repl/watch/play/render/flow2midi/midi2flow/check/new) reachable via `flow --help`
  - flow-sharp.sln updated with flow-cli project + configuration platforms
affects: [30-02 (real handlers), 30-03 (publish), 30-04 (install.sh), 31 (LSP packaging shares CLI conventions)]

# Tech tracking
tech-stack:
  added: [System.CommandLine 2.0.7]
  patterns:
    - Central CommandRegistry returning `Command[]` (Plan 30-02+ handlers plug in without touching Program.cs)
    - Placeholder builders with `TreatUnmatchedTokensAsErrors=false` so positional args reach the action body
    - AssemblyInformationalVersion + reflection for `version` output

key-files:
  created:
    - flow-cli/flow-cli.csproj
    - flow-cli/Program.cs
    - flow-cli/Commands/CommandRegistry.cs
    - flow-cli/Commands/VersionCommand.cs
  modified:
    - flow-sharp.sln

key-decisions:
  - "Use System.CommandLine 2.0.7 (locked by 30-RESEARCH Decision 2) over hand-rolling 11 × ~50 LOC of arg parsing"
  - "AssemblyName=flow so the built binary is named `flow.dll` (matches the published `flow` executable name)"
  - "Version pinned to 0.1.0 / InformationalVersion 0.1.0-phase30 (locks the version-string per RESEARCH Open Question 3)"
  - "10 of 11 subcommands ship as placeholders that exit 2; only `version` has a real implementation in this plan"
  - "Placeholders set TreatUnmatchedTokensAsErrors=false so callers can pass positional args (e.g. `flow run dummy.flow`) and reach the action body — Plan 30-02 replaces each placeholder with a real handler that declares its own args"

patterns-established:
  - "Pattern 1: CommandRegistry.BuildAllCommands() returns Command[] — Program.cs iterates and adds to RootCommand.Subcommands; new subcommands plug in here without touching Program.cs"
  - "Pattern 2: SetAction(parseResult => int) — synchronous Func returning the desired exit code (per System.CommandLine 2.0.7 post-stabilization API)"
  - "Pattern 3: Placeholder handlers in CommandRegistry are explicit and self-documenting (`[flow {name}] not yet implemented (Plan 30-02 will wire this)`) — readers immediately see which subcommands are stubs"

requirements-completed: [REQ-1, REQ-8]

# Metrics
duration: ~15min
completed: 2026-05-11
---

# Phase 30 Plan 01: flow-cli Project Scaffold Summary

**New flow-cli project wired with System.CommandLine 2.0.7 RootCommand, 11 registered subcommands (10 placeholders + working `version`), and added to flow-sharp.sln — `flow --help` and `flow version` work end-to-end while `dotnet run --project flow-interpreter` remains green (REQ-8).**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-05-11T02:36:30Z
- **Completed:** 2026-05-11T02:51:08Z
- **Tasks:** 2 / 2
- **Files created:** 4 (`flow-cli.csproj`, `Program.cs`, `CommandRegistry.cs`, `VersionCommand.cs`)
- **Files modified:** 1 (`flow-sharp.sln`)

## Accomplishments

- Stood up the `flow-cli` project with the locked stack (System.CommandLine 2.0.7, .NET 10, file-scoped namespaces, `RootNamespace=FlowCli`).
- `AssemblyName=flow` so the binary is literally named `flow.dll` (matches what `dotnet publish` will emit as the `flow` executable in Plan 30-03).
- All 11 subcommand names from SPEC REQ-1 are reachable via `flow --help`: `run`, `eval`, `repl`, `watch`, `play`, `render`, `flow2midi`, `midi2flow`, `check`, `version`, `new`.
- `flow version` exits 0 and prints `flow 0.1.0-phase30+<git-sha>` (AssemblyInformationalVersion + SourceLink build metadata).
- 10 placeholders exit with code 2 + stderr `[flow {name}] not yet implemented (Plan 30-02 will wire this)`, so callers always see a clear next-step hint instead of a silent crash.
- REQ-8 spot-check passed: `dotnet run --project flow-interpreter examples/showcase.flow` still writes WAV + MIDI cleanly (exit 0).
- Solution-wide `dotnet build` exits 0 with no new errors and no new warnings introduced by this plan.

## Task Commits

Each task was committed atomically on `worktree-agent-afd8550e674110487` from base `4f89ae6`:

1. **Task 1: Create flow-cli project scaffold + csproj + add to solution** — `fa66c38` (feat)
   - New `flow-cli/flow-cli.csproj` with `.NET 10 / OutputType=Exe / RootNamespace=FlowCli / AssemblyName=flow / Version=0.1.0 / InformationalVersion=0.1.0-phase30`.
   - ProjectReferences: `flow-lang`, `flow-interpreter`, `flow-midi`.
   - PackageReference: `System.CommandLine 2.0.7`.
   - `flow-sharp.sln` gained the flow-cli Project entry (GUID `5617C149-81AB-4E7D-9D2B-8C14CA029968`) and 12 ProjectConfigurationPlatforms lines following the flow-midi pattern verbatim.
   - Stub `Program.cs` with `Main returning 0` so the csproj actually compiles (Task 1 acceptance demands `dotnet build flow-cli/flow-cli.csproj` exits 0); Task 2 replaces it.

2. **Task 2: Wire flow-cli RootCommand + 11 subcommand stubs (version real)** — `b57a1e8` (feat)
   - `Program.cs` rewritten to System.CommandLine wiring: build `RootCommand`, enumerate `CommandRegistry.BuildAllCommands()` into `root.Subcommands`, return `await root.Parse(args).InvokeAsync()`.
   - `Commands/CommandRegistry.cs` lists all 11 subcommand entries in the exact order specified by the plan; 10 are placeholders, the 11th is `VersionCommand.Build()`.
   - `Commands/VersionCommand.cs` reads `AssemblyInformationalVersionAttribute` via reflection (falls back to `AssemblyName.Version`, then `"unknown"`); prints `flow {ver}` and returns 0.
   - Placeholder builder sets `TreatUnmatchedTokensAsErrors = false` so callers can pass positional args (e.g. `flow run dummy.flow`) and the action body still runs — see Deviations section below.

_Total: 2 task commits + this SUMMARY.md (committed separately by the orchestrator)._

## Files Created/Modified

- `flow-cli/flow-cli.csproj` — new .NET 10 Exe project; references flow-lang/flow-interpreter/flow-midi; depends on System.CommandLine 2.0.7. `AssemblyName=flow`, `Version=0.1.0`, `InformationalVersion=0.1.0-phase30`.
- `flow-cli/Program.cs` — entrypoint; builds `RootCommand("Flow — a programming language for music")`, registers all 11 subcommands via `CommandRegistry.BuildAllCommands()`, returns `await root.Parse(args).InvokeAsync()`.
- `flow-cli/Commands/CommandRegistry.cs` — central listing of the 11 subcommand entries; `BuildPlaceholder(name, desc)` constructs each stub; `VersionCommand.Build()` plugs in the real `version` handler.
- `flow-cli/Commands/VersionCommand.cs` — prints `flow {InformationalVersion}` (or AssemblyVersion fallback) and exits 0.
- `flow-sharp.sln` — added flow-cli Project entry + 12 ProjectConfigurationPlatforms lines.

## Decisions Made

- **System.CommandLine 2.0.7 confirmed.** 30-RESEARCH already locked the choice; verified by inspecting `~/.nuget/packages/system.commandline/2.0.7/lib/net8.0/System.CommandLine.xml` for `Command.SetAction(Func<ParseResult,int>)`, `Command.Subcommands`, `Command.TreatUnmatchedTokensAsErrors`, and `ParseResult.InvokeAsync` — all present.
- **Stub Main in Task 1 commit.** Task 1's acceptance criterion requires `dotnet build flow-cli/flow-cli.csproj` to exit 0, but a .NET Exe project with zero `Main` is a hard CS5001 error. Resolution: ship a one-line `Main returning 0` stub in Task 1 and replace it in Task 2. Documented as a Task 1 file in this summary.
- **`TreatUnmatchedTokensAsErrors = false` on every placeholder.** Forced by the Task 2 acceptance criterion that `flow run dummy.flow` exit 2 with stderr "not yet implemented". Without this flag System.CommandLine rejects `dummy.flow` as an unrecognized token (exit 1) before the action runs. See Deviations Rule-1 entry below.
- **No DryWetMidi PackageReference in flow-cli.csproj.** Already transitive via flow-lang → flow-midi → DryWetMidi. Adding a direct reference would duplicate. (Verified by the plan's `contains` note in artifacts.)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Added stub `Main returning 0` to Task 1's commit**

- **Found during:** Task 1 (Create flow-cli project scaffold)
- **Issue:** Task 1 acceptance criterion #2 — `dotnet build flow-cli/flow-cli.csproj` exits 0 — fails as written. A .NET Exe with no `Main` method produces `CSC : error CS5001: Program does not contain a static 'Main' method`. The plan structure has the real `Program.cs` arriving in Task 2.
- **Fix:** Wrote a 6-line stub `Program.cs` (`namespace FlowCli; class Program { static int Main(string[] args) => 0; }`) as part of the Task 1 commit. Task 2 replaces it with the real System.CommandLine wiring. The Task 1 commit message explicitly calls this out so the diff is self-documenting.
- **Files modified:** `flow-cli/Program.cs` (created in Task 1, rewritten in Task 2).
- **Verification:** `dotnet build flow-cli/flow-cli.csproj -c Debug` now exits 0 after Task 1 commit (`fa66c38`).
- **Committed in:** `fa66c38` (Task 1 commit).

**2. [Rule 1 — Bug] Set `TreatUnmatchedTokensAsErrors = false` on every placeholder**

- **Found during:** Task 2 verification of acceptance criterion #4 (`flow run dummy.flow` must exit 2 with stderr "not yet implemented").
- **Issue:** The first cut of `BuildPlaceholder` constructed a `Command` with no positional arguments. When the user invokes `flow run dummy.flow`, System.CommandLine rejects `dummy.flow` as "Unrecognized command or argument" and exits 1 before the action body runs. Acceptance criterion fails.
- **Fix:** Added `TreatUnmatchedTokensAsErrors = false` to the placeholder builder via object-initializer syntax. Now extra positional args are silently tolerated, the action body fires, "not yet implemented" goes to stderr, and the process exits 2. This is correct stub behavior — Plan 30-02 replaces each placeholder with a real handler that declares its own args/options, at which point the flag is moot.
- **Files modified:** `flow-cli/Commands/CommandRegistry.cs`.
- **Verification:** `dotnet run --project flow-cli -- run dummy.flow` now prints `[flow run] not yet implemented (Plan 30-02 will wire this)` to stderr, stdout is empty, exit code is 2.
- **Committed in:** `b57a1e8` (Task 2 commit).

**3. [Note — not a rule, charitable interpretation] Plan Task 1 acceptance criterion `grep -c 'flow-cli' flow-sharp.sln >= 2` is unsatisfiable as written**

- **Found during:** Task 1 verification.
- **Issue:** `grep -c` counts matching *lines*, not occurrences. After Task 1 there is exactly **one** line in `flow-sharp.sln` that contains the string `flow-cli` (the Project entry — which itself contains the substring three times: project name, path segment, path filename). The `GlobalSection(ProjectConfigurationPlatforms)` rows reference the project by GUID, not by name, so they will never match `grep 'flow-cli'`.
- **Resolution:** Treated as an over-strict / mis-typed acceptance check. The substantive intent — "flow-cli appears in the solution file" — is met. The authoritative cross-check `dotnet sln list | grep -c flow-cli` returns `1` (as expected; the dotnet CLI emits one line per project). No code change required.
- **Verification:** `dotnet sln list | grep -c flow-cli` → `1` ✓. `dotnet build` exits 0 ✓. `dotnet build flow-cli/flow-cli.csproj` works ✓.

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug) + 1 charitable-interpretation note on an over-strict criterion.
**Impact on plan:** No scope creep. Both auto-fixes were necessary to make Task 1's build pass and Task 2's `flow run dummy.flow` stub behave correctly. Charitable-interpretation note is documentary only.

## Issues Encountered

- None beyond the two auto-fixes above.

## Verifications Run (acceptance criteria)

Solution + cross-project:

- `dotnet build` (solution-wide) — 0 errors, 9 warnings (all pre-existing in flow-lang and flow-lang.Tests; none introduced by this plan).
- `dotnet build flow-interpreter -c Debug` — 0 errors / 0 warnings.
- `dotnet run --project flow-interpreter examples/showcase.flow` — exit 0; writes `examples/output/flow_showcase.{wav,mid}` (REQ-8 backward-compat spot-check).

flow-cli specific:

- `dotnet build flow-cli/flow-cli.csproj -c Debug` — 0 errors / 0 warnings.
- `dotnet sln flow-sharp.sln list | grep -c flow-cli` → 1 ✓.
- `grep -c 'System.CommandLine' flow-cli/flow-cli.csproj` → 1 ✓.
- `grep 'Version="2.0.7"' flow-cli/flow-cli.csproj` → match ✓.
- `grep '<AssemblyName>flow</AssemblyName>' flow-cli/flow-cli.csproj` → match ✓.
- 3 ProjectReference entries (flow-lang, flow-interpreter, flow-midi) ✓.
- `dotnet run --project flow-cli -- --help` → exit 0; lists all 11 subcommand names (`run`, `eval`, `repl`, `watch`, `play`, `render`, `flow2midi`, `midi2flow`, `check`, `version`, `new`) ✓.
- `dotnet run --project flow-cli -- version` → exit 0; prints `flow 0.1.0-phase30+fa66c38e23ff0b8c0e4a6827bae294dcf09182d7` (matches regex `^flow [0-9]+\.[0-9]+\.[0-9]+`) ✓.
- `dotnet run --project flow-cli -- run dummy.flow` → exit 2; stderr contains "not yet implemented" ✓.
- `dotnet run --project flow-cli -- unknowncmd` → exit 1 with usage hint ✓.
- `dotnet run --project flow-cli -- run --help` → exit 0 (System.CommandLine auto-generates per-subcommand help) ✓.
- `grep -c 'BuildPlaceholder\|VersionCommand.Build' flow-cli/Commands/CommandRegistry.cs` → 12 (≥ 11) ✓.

## User Setup Required

None — no external service configuration required for this plan. (Plan 30-04 will add the install script; Plan 30-03 will produce the publishable binary.)

## Known Stubs

10 of the 11 subcommands ship as deliberate placeholders that print `[flow {name}] not yet implemented (Plan 30-02 will wire this)` to stderr and exit 2:

| Subcommand | File / Builder            | Replaced by   |
|------------|---------------------------|---------------|
| run        | CommandRegistry placeholder | Plan 30-02   |
| eval       | CommandRegistry placeholder | Plan 30-02   |
| repl       | CommandRegistry placeholder | Plan 30-02   |
| watch      | CommandRegistry placeholder | Plan 30-02   |
| play       | CommandRegistry placeholder | Plan 30-02   |
| render     | CommandRegistry placeholder | Plan 30-02   |
| flow2midi  | CommandRegistry placeholder | Plan 30-02   |
| midi2flow  | CommandRegistry placeholder | Plan 30-02   |
| check      | CommandRegistry placeholder | Plan 30-02   |
| new        | CommandRegistry placeholder | Plan 30-02 (or later) |

These are intentional scaffolding stubs — Plan 30-01's objective is the foundation, not the handler implementations. The plan body documents this explicitly. No data-flow stubs (no hardcoded empty arrays / placeholder UI strings reaching a renderer); the only "stub" surface is the exit-2 message itself.

## Next Phase Readiness

- Foundation is ready for Plan 30-02 to wire real handlers (`run`, `eval`, `repl`, `watch`, `play`, `render`, `flow2midi`, `check`) into the registry. Replacing a placeholder is a one-line edit to `CommandRegistry.BuildAllCommands()` — no Program.cs change needed.
- Plan 30-03 (`dotnet publish` profile) can target `flow-cli` directly: `dotnet publish flow-cli -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true` will produce a `flow` executable.
- No blockers. `version` works end-to-end so the smoke-test scaffold in Plan 30-07 has at least one always-green probe.

## Self-Check: PASSED

Verified by direct inspection:

```bash
$ ls flow-cli/flow-cli.csproj flow-cli/Program.cs flow-cli/Commands/CommandRegistry.cs flow-cli/Commands/VersionCommand.cs
# all 4 present

$ git log --oneline -3
b57a1e8 feat(30-01): wire flow-cli RootCommand + 11 subcommand stubs (version real)
fa66c38 feat(30-01): scaffold flow-cli project with System.CommandLine 2.0.7
4f89ae6 plan(30): 9 plans + RESEARCH + VALIDATION — flow CLI + Bug B closure
```

Both task commits found in `git log`; all 4 created files exist; verification commands exit as expected.

---

*Phase: 30-flow-cli-formal-install*
*Plan: 01*
*Completed: 2026-05-11*
