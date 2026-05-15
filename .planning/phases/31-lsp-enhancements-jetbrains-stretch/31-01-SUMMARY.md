---
phase: 31-lsp-enhancements-jetbrains-stretch
plan: 01
subsystem: lsp
tags: [lsp, jetbrains, lsp4ij, omnisharp, system.commandline, stdlib-index, flow-cli]

# Dependency graph
requires:
  - phase: 17
    provides: flow-lsp Program bootstrap; StdlibSymbolIndex + LspFixtures test helper; CompletionHandler/HoverHandler/SignatureHelpHandler pure-static seams
  - phase: 30
    provides: unified `flow` CLI with 11 subcommands + System.CommandLine 2.0.7 + `flow install` PATH wiring
provides:
  - "`flow lsp` subcommand on the unified CLI delegating to FlowLsp.Program.Main via stdio"
  - "StdlibSymbolIndex.ProcsForModule(moduleName) reverse-lookup helper for Phase 31 analyzers + completion filters"
  - "LspFixtures.StdlibIndex() helper accessible from Phase 31 fact classes"
  - "31-DECISIONS.md locking D-11 (semicolon Option A) + D-12 (Unicode U+2026 ellipsis re-confirmation)"
  - "flow-lsp/Program.cs refactored from top-level statements to an explicit public static Main method (cross-assembly callability)"
affects: [31-02, 31-03, 31-04, 31-05, 31-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "thin-wrapper CLI subcommand delegating to another project's Main (matches Phase 30 ReplCommand idiom)"
    - "shared LspFixtures helper exposing index constructors for Phase 31 fact classes (lifts the Phase 17 local-Indices() pattern)"
    - "plan-phase decisions file (31-DECISIONS.md) sibling to CONTEXT.md/RESEARCH.md/PATTERNS.md for downstream-plan citation"

key-files:
  created:
    - flow-cli/Commands/LspCommand.cs
    - .planning/phases/31-lsp-enhancements-jetbrains-stretch/31-DECISIONS.md
  modified:
    - flow-cli/Commands/CommandRegistry.cs
    - flow-cli/flow-cli.csproj
    - flow-lsp/Program.cs
    - flow-lsp/Symbols/StdlibSymbolIndex.cs
    - flow-lang.Tests/Unit/Phase17/LspFixtures.cs

key-decisions:
  - "D-11 locked: `;` line comment is position-sensitive (Option A) — column-0 only; mid-line `;` stays a Semicolon terminator"
  - "D-12 locked: re-confirm Unicode U+2026 `…` ellipsis per CONTEXT D-01, overriding the planning-orchestrator's ASCII-`...` note"
  - "Refactor flow-lsp/Program.cs from top-level statements to public static Main — top-level Program.<Main>$ is internal and not callable from flow-cli (Rule 3 auto-fix)"

patterns-established:
  - "CLI subcommand → external Program.Main delegation: use fully-qualified `FlowLsp.Program.Main(...)` to disambiguate from auto-generated `FlowCli.Program`"
  - "StdlibSymbolIndex reverse-lookup helper: linear walk over bounded _byName.Values is the right cost model for the ~100-entry stdlib"
  - "LspFixtures.StdlibIndex() helper: future Phase 31 facts call this directly instead of recreating the Phase 17 HoverHandlerTests.Indices() local helper"

requirements-completed: [SPEC-1, SPEC-2, SPEC-3, SPEC-4, SPEC-7]

# Metrics
duration: ~12 min
completed: 2026-05-12
---

# Phase 31 Plan 01: LSP Wave-0 Scaffolding Summary

**`flow lsp` subcommand wiring + StdlibSymbolIndex reverse-lookup helper + locked plan-phase decisions (D-11 semicolon-Option-A, D-12 Unicode ellipsis re-confirm) — zero behavioural changes, three scaffolding pieces that unblock every downstream Phase 31 plan.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-12T22:04Z (approximate — orchestrator-supplied wall start)
- **Completed:** 2026-05-12T22:16:33Z
- **Tasks:** 3 / 3
- **Files modified:** 7 (2 created, 5 modified) — counts the unavoidable flow-lsp/Program.cs refactor and the flow-cli.csproj reference

## Accomplishments

- `flow lsp` is now a registered subcommand on the unified CLI; `dotnet run --project flow-cli -- lsp --help` prints "Start the Flow Language Server (stdio LSP)".
- StdlibSymbolIndex exposes `ProcsForModule(string moduleName)` — the reverse-lookup helper that Plan 31-02 (UnusedImportAnalyzer) and Plan 31-04 (CompletionHandler.FilterByImports) consume directly.
- LspFixtures exposes `StdlibIndex()` — Phase 31 fact classes (UnusedImportAnalyzerFacts, CompletionFilterFacts) can now construct the index in one call.
- 31-DECISIONS.md locks D-11 (`;` Option A position-sensitive — mirrors the existing `Note:` arm at SimpleLexer.cs:1144) and D-12 (Unicode `…` U+2026 ellipsis re-confirmation per CONTEXT D-01).
- flow-lsp's top-level Program.cs is now an explicit `public static class Program { public static async Task<int> Main(string[] args) }` — the cross-assembly callability fix that `flow lsp` requires. `dotnet run --project flow-lsp` continues to work unchanged.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add `flow lsp` subcommand + register it** — `c1e0a5d` (feat)
2. **Task 2: Extend StdlibSymbolIndex with ProcsForModule + LspFixtures.StdlibIndex** — `b7202b9` (feat)
3. **Task 3: Record plan-phase locked decisions in 31-DECISIONS.md** — `82e06d5` (docs)

Plan metadata commit (this SUMMARY + STATE/ROADMAP updates) will follow.

## Files Created/Modified

**Created**
- `flow-cli/Commands/LspCommand.cs` — System.CommandLine subcommand named `"lsp"` that delegates to `FlowLsp.Program.Main(Array.Empty<string>()).GetAwaiter().GetResult()`.
- `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-DECISIONS.md` — plan-phase decisions D-11 + D-12 + REQ-7 stretch-bar wording clarification.

**Modified**
- `flow-cli/Commands/CommandRegistry.cs` — inserted `LspCommand.Build()` row at the end of `BuildAllCommands()` (12 subcommands total) + updated leading comment.
- `flow-cli/flow-cli.csproj` — added `<ProjectReference Include="..\flow-lsp\flow-lsp.csproj" />`.
- `flow-lsp/Program.cs` — converted top-level statements to `public static class Program { public static async Task<int> Main(string[] args) }`. Body is byte-identical to the previous top-level statement block; only the wrapping changed. `namespace FlowLsp;` added so cross-assembly callers can reference `FlowLsp.Program.Main`.
- `flow-lsp/Symbols/StdlibSymbolIndex.cs` — added public `IEnumerable<StdProc> ProcsForModule(string moduleName)` with a triple-slash docblock citing the Phase 31 consumers.
- `flow-lang.Tests/Unit/Phase17/LspFixtures.cs` — added `public static StdlibSymbolIndex StdlibIndex() => new(new ParseSession());` with a `using FlowLsp.Symbols;` import.

## Decisions Made

- **D-11 [semicolon-comment-position] locked Option A.** `;` at column 0 (with optional whitespace, gated by `IsStartOfLineContent()`) is a line comment; mid-line `;` stays a `TokenType.Semicolon` statement-terminator. RESEARCH grep audit confirms zero column-0 `;` in any of the 647 in-repo `.flow` files — REQ-6 migration is zero, byte-identical determinism preserved by construction.
- **D-12 [varargs-ellipsis-character] re-confirmed Unicode U+2026.** The planning-orchestrator note proposing ASCII `...` is explicitly superseded. CONTEXT D-01 is authoritative. Pitfall 3 mitigation: Plan 31-05 uses `LspMappings.BuildParameters` to populate `SignatureInformation.Parameters` with explicit `ParameterInformation` ranges instead of relying on byte-offset math.
- **REQ-7 stretch-bar wording clarification (not a new decision).** Scaffolding ALWAYS lands per CONTEXT D-10; "deferred" means gate-unmet, never files-removed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Refactored flow-lsp/Program.cs from top-level statements to explicit public Main**

- **Found during:** Task 1 (`flow lsp` subcommand wiring).
- **Issue:** The plan instructs `LspCommand` to call `FlowLsp.Program.Main(Array.Empty<string>()).GetAwaiter().GetResult()`, but flow-lsp/Program.cs was written as C# top-level statements. The compiler-synthesized `<Main>$` method on the auto-generated `Program` class is `internal` and cannot be invoked cross-assembly — initial build attempt failed with `CS0122: 'Program.Main(string[])' is inaccessible due to its protection level`.
- **Fix:** Wrapped the entire top-level statement block in `public static class Program { public static async Task<int> Main(string[] args) { ... return 0; } }` inside `namespace FlowLsp;`. Body byte-identical to the previous top-level code; only the wrapping changed. `dotnet run --project flow-lsp` continues to invoke the same logic via the standard runtime entry-point lookup.
- **Files modified:** `flow-lsp/Program.cs`.
- **Verification:** `dotnet build flow-cli/flow-cli.csproj` exits 0; `dotnet run --project flow-cli -- lsp --help` prints the new description; existing `dotnet build flow-lsp/flow-lsp.csproj` still succeeds.
- **Committed in:** `c1e0a5d` (Task 1 commit).

**2. [Rule 3 — Blocking] Fully-qualified `FlowLsp.Program.Main` to disambiguate from `FlowCli.Program`**

- **Found during:** Task 1 second build attempt.
- **Issue:** With `using FlowLsp;` at the top of `LspCommand.cs`, the unqualified `Program.Main(...)` reference resolved to `FlowCli.Program` (closer namespace wins). Build still failed with the same CS0122 because `FlowCli.Program.Main` is `internal`.
- **Fix:** Dropped the `using FlowLsp;` import and used the fully-qualified type name `FlowLsp.Program.Main(...)` inline. Single-line change.
- **Files modified:** `flow-cli/Commands/LspCommand.cs`.
- **Verification:** Build succeeds; help output correct.
- **Committed in:** `c1e0a5d` (same Task 1 commit — caught and fixed before the commit was created).

---

**Total deviations:** 2 auto-fixed (2 × Rule 3 — both downstream consequences of the same root cause: the plan assumed an explicit `Program.Main` already existed in flow-lsp, but it didn't.)

**Impact on plan:** Both fixes were on the critical path to delivering the plan-stated outcome. The flow-lsp/Program.cs refactor is the minimum surgical change consistent with what the plan expected; the body is byte-identical and `dotnet run --project flow-lsp` standalone behavior is preserved. No scope creep.

## Issues Encountered

- `.planning/config.json` was modified by the orchestrator (auto_chain flag flip) ahead of execution; that file was deliberately excluded from the per-task commits to keep them focused on the plan's `files_modified` whitelist.
- No test regressions: Phase 17 test suite (117 tests) passes cleanly after Tasks 1 + 2.

## Threat Flags

None — this is a scaffolding plan with no new endpoints, no auth surface, no file-access pattern changes, and no schema changes at trust boundaries. The three threats in the plan's `<threat_model>` (T-31-01-01..03) are all dispositioned `accept`.

## User Setup Required

None — no external service configuration required. Future JetBrains plugin work (Plan 31-08) will require LSP4IJ-aware IntelliJ Community 2024.2+, but that's deferred to that plan.

## Next Phase Readiness

- **Plan 31-02** (UnusedImportAnalyzer) can call `stdlib.ProcsForModule("harmony")` directly.
- **Plan 31-04** (CompletionHandler.FilterByImports) consumes `StdlibSymbolIndex.ModuleNames` (existing) + `ProcsForModule` (new) for the import-aware filter.
- **Plan 31-05** (LspMappings.FormatSignature with U+2026) has D-12 explicitly locked as a citable decision ID.
- **Plan 31-08** (FlowLanguageServerFactory.kt) can wire `GeneralCommandLine("flow", "lsp")` with no further CLI prerequisites — `flow install` (Phase 30) handles PATH for free.
- Wave 1 / 2 plans have zero file conflicts with this scaffolding.

## Self-Check: PASSED

- Verified `flow-cli/Commands/LspCommand.cs` exists.
- Verified `flow-cli/Commands/CommandRegistry.cs` contains the literal token `LspCommand.Build()`.
- Verified `flow-cli/flow-cli.csproj` contains `flow-lsp/flow-lsp.csproj` ProjectReference.
- Verified `flow-lsp/Symbols/StdlibSymbolIndex.cs` contains `public IEnumerable<StdProc> ProcsForModule` (grep count = 1).
- Verified `flow-lang.Tests/Unit/Phase17/LspFixtures.cs` contains `public static StdlibSymbolIndex StdlibIndex` (grep count = 1).
- Verified `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-DECISIONS.md` exists, contains `**D-11` (count 1), `**D-12` (count 1), the literal U+2026 `…` (count 4), CONTEXT D-01 (count 2), CONTEXT D-10 (count 4), and the closer "Locked 2026-05-12" (count 1).
- Verified `flow lsp --help` exits 0 and prints "Start the Flow Language Server".
- Verified all three task commits exist in `git log`: `c1e0a5d`, `b7202b9`, `82e06d5`.
- Verified Phase 17 test suite (117 tests) passes with zero regressions after Tasks 1 + 2.

---
*Phase: 31-lsp-enhancements-jetbrains-stretch*
*Completed: 2026-05-12*
