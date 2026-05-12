---
phase: 30-flow-cli-formal-install
plan: 02
subsystem: cli
tags: [system.commandline, flow-cli, scaffolding, embedded-resources]

requires:
  - phase: 30-flow-cli-formal-install
    provides: "flow-cli host project (Program.cs + CommandRegistry placeholder skeleton) and version subcommand from Plan 30-01"

provides:
  - "9 real subcommand handlers (run, eval, repl, watch, play, render, flow2midi, check, new)"
  - "1 explicit Plan 30-09 deferral stub (midi2flow) with composer-facing forward-pointer message"
  - "Embedded default.flow scaffold template with {{PIECE_NAME}} substitution mechanism"
  - "ScaffoldEmitter helper (validation + no-overwrite + resource-stream reader)"
  - "CommandRegistry promoted from 10 placeholders + 1 real to 11 real Build() implementations (0 placeholders)"

affects: ["30-03-PLAN", "30-04-PLAN", "30-05-PLAN", "30-06-PLAN", "30-09-PLAN"]

tech-stack:
  added: []
  patterns:
    - "Subcommand-per-file Build() static method (one Command per file, internal static class, namespace FlowCli.Commands)"
    - "Embedded-resource templating with `{{PLACEHOLDER}}` string substitution at emit time"
    - "Charitable interpretation: --output is informational when the .flow script self-writes (warn on mismatch, do not fail)"
    - "FlowEngine.Execute with Console.Out muted as poor-man's parse-AND-execute check (until a true Parse-only API ships per RESEARCH Open Question 2)"

key-files:
  created:
    - "flow-cli/Commands/RunCommand.cs"
    - "flow-cli/Commands/EvalCommand.cs"
    - "flow-cli/Commands/ReplCommand.cs"
    - "flow-cli/Commands/WatchCommand.cs"
    - "flow-cli/Commands/PlayCommand.cs"
    - "flow-cli/Commands/RenderCommand.cs"
    - "flow-cli/Commands/Flow2MidiCommand.cs"
    - "flow-cli/Commands/Midi2FlowStubCommand.cs"
    - "flow-cli/Commands/CheckCommand.cs"
    - "flow-cli/Commands/NewCommand.cs"
    - "flow-cli/Scaffold/ScaffoldEmitter.cs"
    - "flow-cli/Scaffold/Templates/default.flow"
  modified:
    - "flow-cli/Commands/CommandRegistry.cs"
    - "flow-cli/flow-cli.csproj"
    - ".gitignore"

key-decisions:
  - "CheckCommand executes (mutes stdout) rather than parse-only — no FlowEngine.Parse() entrypoint exists; RESEARCH Open Question 2 documented the deferral"
  - "render / flow2midi emit a charitable yellow stderr warning when --output doesn't match the .flow script's actual write target — do not fail; the script remains source of truth"
  - "midi2flow stays an explicit stub returning exit code 2 with a 'Plan 30-09' forward-pointer message — distinct from Plan 30-01's generic placeholder"
  - "BuildPlaceholder helper deleted as dead code after final wire-in (rather than left behind for documentary purposes)"
  - "Scaffold template uses `use \"@std\" / @audio / @notation`, a single tempo/timesig/key/section block with C major melody, renderSong with piano, and (writeWav \"<name>.wav\") so `flow run <scaffold>` is end-to-end runnable"

patterns-established:
  - "Subcommand handler file structure: `using System.CommandLine` + namespace `FlowCli.Commands` + internal static class + public static Command Build() + parseResult.GetValue(arg)! pattern"
  - "Embedded-resource carve-out in .gitignore (mirrors Phase 27/28 patterns for examples/pragmas and examples/tests)"
  - "FileNotFound check up-front in the Build() action (uniform error message across run/play/render/flow2midi/check)"

requirements-completed: [REQ-1]

duration: 21min
completed: 2026-05-10
---

# Phase 30 Plan 02: Wire 10 Real Subcommands + 1 Explicit Stub Summary

**Promoted flow-cli from Plan 30-01's 10-placeholder skeleton to a fully-wired CLI: run/eval/repl/watch/play/render/flow2midi/check/new are real handlers wrapping flow-interpreter and flow-lang entrypoints; midi2flow returns exit 2 with an explicit Plan 30-09 deferral message; CommandRegistry holds zero placeholders.**

## Performance

- **Duration:** 21 min
- **Started:** 2026-05-11T02:41:00Z
- **Completed:** 2026-05-11T03:02:55Z
- **Tasks:** 3
- **Files modified:** 15 (12 created + 3 modified)

## Accomplishments

- 9 real subcommand handlers wired to existing flow-interpreter / flow-lang entrypoints
- Embedded scaffold template + emitter → `flow new <name>` produces a runnable musical fragment in one command
- `flow run /tmp/foo/foo.flow` (scaffold output) exits 0 end-to-end — composer onboarding path is live
- Backward compatibility preserved: `dotnet run --project flow-interpreter` continues to function unchanged
- 11/11 subcommand registry entries are now first-class Build() calls — zero placeholders remain

## Task Commits

Each task was committed atomically:

1. **Task 1: Wire run/eval/repl/watch/check (5 core-language commands)** — `48761cb` (feat)
2. **Task 2: Wire play/render/flow2midi + midi2flow explicit-stub** — `bc9bb8c` (feat)
3. **Task 3: Wire flow new scaffold + embedded default.flow template** — `8bcc8c0` (feat)
3a. **Deviation fix: track flow-cli/Scaffold/**/*.flow despite global *.flow ignore** — `ebb6802` (fix)

## Files Created/Modified

**Created (12):**
- `flow-cli/Commands/RunCommand.cs` — `flow run <script>`; wraps `FlowInterpreter.ScriptRunner.RunScript`
- `flow-cli/Commands/EvalCommand.cs` — `flow eval <code>`; mirrors flow-interpreter `RunFromString` (FlowEngine.Execute + red stderr on failure)
- `flow-cli/Commands/ReplCommand.cs` — `flow repl`; wraps `FlowInterpreter.Repl.Run`
- `flow-cli/Commands/WatchCommand.cs` — `flow watch <script>`; wraps `FlowInterpreter.LiveReloadManager`
- `flow-cli/Commands/PlayCommand.cs` — `flow play <script>`; forwards to ScriptRunner (script owns its own `(play …)` call)
- `flow-cli/Commands/RenderCommand.cs` — `flow render <script> -o <wav>`; charitable warning when --output doesn't match script's `(writeWav …)` path
- `flow-cli/Commands/Flow2MidiCommand.cs` — `flow flow2midi <script> -o <mid>`; same charitable warning pattern as RenderCommand
- `flow-cli/Commands/Midi2FlowStubCommand.cs` — explicit Plan 30-09 deferral stub; exit code 2
- `flow-cli/Commands/CheckCommand.cs` — `flow check <script>`; Console.Out muted FlowEngine.Execute; OK on success, formatted errors + exit 1 on failure
- `flow-cli/Commands/NewCommand.cs` — `flow new <name> [--dir <path>]`; wraps ScaffoldEmitter
- `flow-cli/Scaffold/ScaffoldEmitter.cs` — validates piece-name, refuses overwrite, reads embedded resource, substitutes `{{PIECE_NAME}}`, writes to disk
- `flow-cli/Scaffold/Templates/default.flow` — minimum-viable musical fragment (tempo 120 / timesig 4/4 / key Cmajor / section main / Song / renderSong / writeWav)

**Modified (3):**
- `flow-cli/Commands/CommandRegistry.cs` — promoted 10 BuildPlaceholder calls to real Build() calls; deleted the BuildPlaceholder helper as dead code
- `flow-cli/flow-cli.csproj` — added `<EmbeddedResource Include="Scaffold\Templates\default.flow" />` ItemGroup
- `.gitignore` — Phase 30 carve-out so `flow-cli/Scaffold/**/*.flow` is tracked despite global `*.flow` rule

## Decisions Made

- **CheckCommand mutes stdout rather than parse-only.** FlowEngine has no public Parse() entrypoint as of Phase 30 (RESEARCH Open Question 2 documented this as a deferred refactor). The current implementation wraps the full pipeline in `Console.SetOut(TextWriter.Null)` inside a try/finally to silence `(print …)` side-effects, then inspects the ErrorReporter. Audio playback calls in the script remain side-effectful — `check` is not advertised as a sandbox.
- **render / flow2midi emit a charitable warning, do not fail.** The .flow source is the single source of truth for its own output paths (project memory: charitable interpretation, ergonomics first). Auto-injection of writeWav/writeMidi targets from a CLI flag is deferred to ROADMAP v1.5+.
- **midi2flow is an EXPLICIT stub, not a generic placeholder.** Plan 30-01's BuildPlaceholder returned a vague "not yet implemented" message; Midi2FlowStubCommand returns exit 2 with a specific Plan 30-09 forward-pointer, accepts the documented input/output arguments so System.CommandLine doesn't reject them with "unrecognized arguments", and references the Bug B closure path.
- **BuildPlaceholder helper deleted.** Once all 11 Build() calls are real, the helper is dead code. Removing it makes `grep -c 'BuildPlaceholder' CommandRegistry.cs` return 0 (acceptance criterion) and keeps the file readable for the next reader.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Tracked `flow-cli/Scaffold/**/*.flow` despite global `*.flow` ignore**
- **Found during:** Task 3 commit verification (`git add` reported the embedded-resource template as ignored)
- **Issue:** The repo-wide `.gitignore` line `*.flow` (which keeps generated audio scripts out of source control) also matches `flow-cli/Scaffold/Templates/default.flow`. The embedded resource lives on disk locally so `dotnet build` succeeds, but a fresh clone would receive an assembly missing the resource — `flow new` would fail at runtime with "Embedded resource not found".
- **Fix:** Added a Phase 30 carve-out to `.gitignore` mirroring the existing Phase 27/28 patterns for `examples/pragmas/` and `examples/tests/`:
  ```
  !flow-cli/Scaffold/
  !flow-cli/Scaffold/**
  !flow-cli/Scaffold/**/*.flow
  ```
  then committed the template file.
- **Files modified:** `.gitignore`, `flow-cli/Scaffold/Templates/default.flow` (newly tracked)
- **Verification:** `git check-ignore -v flow-cli/Scaffold/Templates/default.flow` now resolves to the negative rule; `git ls-files | grep default.flow` returns the path.
- **Committed in:** `ebb6802` (fix commit following Task 3's main commit; pre-commit head guard requires new commit per worktree rules — no amend)

---

**Total deviations:** 1 auto-fixed (Rule 3 blocking issue)
**Impact on plan:** Necessary for shippability — without the fix the binary's embedded resource would resolve only on this worktree and disappear for any fresh clone or CI build. No scope creep.

## Issues Encountered

- The plan's automated verification command for Task 1 specified `eval 'Int x = 5; (print (str x))'` without a `use "@std"` import. Both `flow eval` AND the legacy `dotnet run --project flow-interpreter -e` reject this with `Function 'str' / 'print' not found` because the stdlib is not auto-imported in script mode (only REPL auto-imports). This matches existing flow-interpreter behaviour, so backward-compat is preserved; the verification with an explicit `use "@std"` prefix passes cleanly (`exit 0`, prints `5`). Documented here so the Plan 30-04 documentation phase can either update the example or call out the import requirement.

## Backward Compatibility Status

- `dotnet build flow-interpreter` → 0 errors, 0 warnings
- `dotnet run --project flow-interpreter examples/showcase.flow` → exit 0 (unchanged behaviour, identical stdout)
- Existing test suite (`tests/test_*.flow`) and `flow-lang.Tests` not touched by this plan

## Render/Flow2Midi Charitable-Warning Design Tradeoff

The plan calls out one Phase-30-specific design decision worth recording for downstream consumers:

`flow render examples/showcase.flow -o /tmp/my.wav` does NOT make showcase.flow write to `/tmp/my.wav` — it makes showcase.flow run and whatever the script's own `(writeWav …)` path was is honoured. The CLI then checks `File.Exists(--output)` and emits a yellow stderr warning if the paths don't match, but the exit code reflects the script's own success/failure.

This honours the project's two memories: (a) **charitable interpretation** — prefer silent-and-documented assumptions over errors when the user's intent is unambiguous, and (b) **ergonomics first** — composers iterate by editing the .flow file, not by re-typing CLI flags. ROADMAP v1.5+ owns the work to optionally auto-inject the writeWav target from --output via a preprocessor pass; for Phase 30 the script is the source of truth.

## Next Phase Readiness

- 30-03 (man-page / shell completions generation) can now scan a complete CommandRegistry with real Argument/Option declarations to emit accurate help text — no placeholders to special-case.
- 30-04 (CLI documentation pass) has 10 real handlers + 1 documented stub to write about.
- 30-09 (midi2flow real implementation after flow-midi rewrite) has the exact stub message + path documented; replacing `Midi2FlowStubCommand.Build()` with a real handler is a one-line registry change.

## Self-Check: PASSED

Verified post-write:
- FOUND: flow-cli/Commands/RunCommand.cs
- FOUND: flow-cli/Commands/EvalCommand.cs
- FOUND: flow-cli/Commands/ReplCommand.cs
- FOUND: flow-cli/Commands/WatchCommand.cs
- FOUND: flow-cli/Commands/PlayCommand.cs
- FOUND: flow-cli/Commands/RenderCommand.cs
- FOUND: flow-cli/Commands/Flow2MidiCommand.cs
- FOUND: flow-cli/Commands/Midi2FlowStubCommand.cs
- FOUND: flow-cli/Commands/CheckCommand.cs
- FOUND: flow-cli/Commands/NewCommand.cs
- FOUND: flow-cli/Scaffold/ScaffoldEmitter.cs
- FOUND: flow-cli/Scaffold/Templates/default.flow
- FOUND commit: 48761cb (Task 1)
- FOUND commit: bc9bb8c (Task 2)
- FOUND commit: 8bcc8c0 (Task 3)
- FOUND commit: ebb6802 (Task 3 fix — Rule 3 deviation)

---
*Phase: 30-flow-cli-formal-install*
*Completed: 2026-05-10*
