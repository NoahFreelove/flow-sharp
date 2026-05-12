---
phase: 30-flow-cli-formal-install
plan: 03
subsystem: cli
tags: [tomlyn, xdg, config, flow-config, propagation, charitable-fallback]

# Dependency graph
requires:
  - phase: 30-flow-cli-formal-install
    provides: "30-01 — flow-cli console scaffold + System.CommandLine root command"
  - phase: 30-flow-cli-formal-install
    provides: "30-02 — Run/Play/Watch/Eval/Repl subcommand wiring with --device options"
provides:
  - "FlowConfigPoco record + static FlowConfig.Active singleton living in flow-lang/Runtime"
  - "FlowConfigLoader.LoadFromXdg() reads ~/.config/flow/config.toml via Tomlyn 2.3.2"
  - "Three-tier Tempo + TimeSignature fallback chain in ExecutionContext.GetMusicalContext()"
  - "ModuleLoader.AdditionalSearchPaths populated by FlowEngine from FlowConfig.ConfiguredStdlibSearchPaths"
  - "Run/Play/Watch commands consult FlowConfig.Active.DefaultAudioDevice when --device is unspecified"
affects: [30-04, 30-05, 30-07, 30-08, 30-09]

# Tech tracking
tech-stack:
  added: [Tomlyn 2.3.2]
  patterns: [static-singleton-config, charitable-fallback, three-tier-resolution-chain]

key-files:
  created:
    - flow-lang/Runtime/FlowConfig.cs
    - flow-cli/Config/FlowConfigLoader.cs
    - flow-lang.Tests/Integration/Phase30/FlowConfigPropagationTests.cs
  modified:
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/Runtime/ModuleLoader.cs
    - flow-lang/Core/FlowEngine.cs
    - flow-cli/flow-cli.csproj
    - flow-cli/Program.cs
    - flow-cli/Commands/RunCommand.cs
    - flow-cli/Commands/PlayCommand.cs
    - flow-cli/Commands/WatchCommand.cs

key-decisions:
  - "Singleton lives in flow-lang (not flow-cli) — interpreter reads without circular dep"
  - "Tomlyn dependency confined to flow-cli — flow-lang stays Tomlyn-free"
  - "Use Tomlyn 2.x System.Text.Json-style API (TomlSerializer.Deserialize + JsonNamingPolicy.SnakeCaseLower) — legacy Toml.ToModel<T> referenced in plan was removed in Tomlyn 2.0 redesign"
  - "Malformed default_timesig charitably falls back to 4/4 + single stderr warning per process (CLAUDE.md memory feedback_charitable_interpretation)"
  - "Missing config file: silent fallback to defaults (no warning, exit 0)"
  - "Test isolation via try/finally + FlowConfig.Reset() at every fact body — no [Collection]/fixture machinery needed"

patterns-established:
  - "Static-Singleton Config (RESEARCH Pattern 1): config loaded once at process startup, read directly from FlowConfig.Active wherever the engine needs it; no plumbing through 11 subcommands × N call frames"
  - "One-way write direction: flow-cli writes (LoadFromXdg) -> flow-lang reads. ModuleLoader stays unaware of the config source — paths are externally seeded by FlowEngine reading FlowConfig.ConfiguredStdlibSearchPaths"
  - "Three-tier fallback chain in ExecutionContext.GetMusicalContext: (1) call-stack-resolved value, (2) FlowConfig.Active override, (3) hard-coded baked default (120 BPM / 4/4)"
  - "Charitable malformed-input policy: log a single stderr Warning + apply baked default; do NOT abort. Warning latched so it appears once per process even though GetMusicalContext is called per-note in note streams"

requirements-completed: [REQ-4]

# Metrics
duration: 38min
completed: 2026-05-10
---

# Phase 30 Plan 03: Config File Format & Read Pipeline Summary

**~/.config/flow/config.toml loaded via Tomlyn 2.3.2 at flow-cli startup; all 4 optional keys (default_tempo, default_timesig, stdlib_search_path, default_audio_device) propagate through to interpreter behavior via a static FlowConfig.Active singleton in flow-lang/Runtime.**

## Performance

- **Duration:** 38 min
- **Started:** 2026-05-10
- **Completed:** 2026-05-10
- **Tasks:** 4
- **Files created:** 3 (FlowConfig.cs, FlowConfigLoader.cs, FlowConfigPropagationTests.cs)
- **Files modified:** 7 (ExecutionContext, ModuleLoader, FlowEngine, flow-cli.csproj, Program.cs, Run/Play/Watch commands)

## Accomplishments

- **Static-singleton config holder in flow-lang/Runtime/FlowConfig.cs** — POCO record `FlowConfigPoco` with the 5 SPEC-4 keys (`InstallPath`, `DefaultAudioDevice`, `DefaultTempo`, `DefaultTimesig`, `StdlibSearchPath`), all nullable, plus `FlowConfig.Active` static + `FlowConfig.Reset()` test helper + `FlowConfig.ConfiguredStdlibSearchPaths` derived read.
- **flow-cli/Config/FlowConfigLoader.cs reads `~/.config/flow/config.toml`** via Tomlyn 2.3.2's `TomlSerializer.Deserialize<T>(text, options)` API with `JsonNamingPolicy.SnakeCaseLower` so snake_case TOML keys map automatically to PascalCase POCO properties. Missing file → silent fallback; malformed file or IO error → single 'Warning:' to stderr + fall back to defaults.
- **All 4 optional keys end-to-end wired:**
  - `default_tempo` → `ExecutionContext.GetMusicalContext()` Tempo `??=` chain (call-stack → FlowConfig → 120 baked)
  - `default_timesig` → `ExecutionContext.GetMusicalContext()` TimeSignature `??=` chain via `ParseTimesigOrDefault` helper (charitable fallback on malformed strings)
  - `stdlib_search_path` → `FlowConfig.ConfiguredStdlibSearchPaths` → `ModuleLoader.AdditionalSearchPaths` (seeded at `FlowEngine` ctor time)
  - `default_audio_device` → `RunCommand` + `PlayCommand` + `WatchCommand` all consult `FlowConfig.Active.DefaultAudioDevice` when `--device` is unspecified (`--device` always wins)
- **8 xunit.v3 propagation facts in flow-lang.Tests/Integration/Phase30/** — 1000/1000 full suite GREEN (was 992 baseline + 8 new).
- **Phase 18/25/27 byte-identical determinism preserved** — tutorial.flow + showcase.flow wrap their content in explicit `tempo`/`timesig` blocks so the new fallback layer is unreachable; verified by 14/14 ByteIdentical tests GREEN.

## Task Commits

Each task was committed atomically:

1. **Task 1: FlowConfig singleton + ExecutionContext/ModuleLoader/FlowEngine wiring** — `475838c` (feat)
2. **Task 2: Tomlyn 2.3.2 loader + Program.cs startup hook** — `f8ca1ed` (feat)
3. **Task 3: 8 xUnit propagation facts** — `a34c904` (test)
4. **Task 4: DefaultAudioDevice fallback in Run/Play/Watch** — `8116b2f` (feat)

## Files Created/Modified

### Created

- `flow-lang/Runtime/FlowConfig.cs` — `FlowConfigPoco` record (5 nullable SPEC-4 keys) + `static class FlowConfig { Active, ConfiguredStdlibSearchPaths, Reset() }`. flow-lang owns the type; flow-cli writes to it; engine reads.
- `flow-cli/Config/FlowConfigLoader.cs` — `LoadFromXdg()` static reads `~/.config/flow/config.toml` via Tomlyn 2.3.2. Charitable: missing → silent; malformed → 'Warning:' + defaults. Tomlyn dependency lives ONLY here.
- `flow-lang.Tests/Integration/Phase30/FlowConfigPropagationTests.cs` — 8 facts pinning all 4 propagation paths + null-fallback + active-block-precedence baseline facts. Try/finally + `FlowConfig.Reset()` at every body for cross-test isolation.

### Modified

- `flow-lang/Runtime/ExecutionContext.cs` — `GetMusicalContext()` now uses 3-tier fallback chain for Tempo + TimeSignature; new private `ParseTimesigOrDefault` helper does charitable "N/M" parse (power-of-2 denominator validated; malformed → 4/4 + single stderr Warning latched per process).
- `flow-lang/Runtime/ModuleLoader.cs` — added instance-mutable `List<string> AdditionalSearchPaths`; `ResolvePath` consults it BEFORE the relative-resolution branch (and AFTER the `@`-prefix stdlib branch). Loader stays unaware of the config source.
- `flow-lang/Core/FlowEngine.cs` — ctor seeds `moduleLoader.AdditionalSearchPaths` from `FlowConfig.ConfiguredStdlibSearchPaths` immediately after `ModuleLoader` construction. Empty list when no config.toml loaded → zero-cost no-op.
- `flow-cli/flow-cli.csproj` — added `PackageReference Include="Tomlyn" Version="2.3.2"` next to existing `System.CommandLine` 2.0.7.
- `flow-cli/Program.cs` — `FlowConfigLoader.LoadFromXdg()` called as first line of Main, before `RootCommand` is built (FlowEngine reads the config at ModuleLoader-seed time, so it must be active before any command runs).
- `flow-cli/Commands/RunCommand.cs`, `PlayCommand.cs`, `WatchCommand.cs` — `device ??= FlowConfig.Active.DefaultAudioDevice` after the `GetValue(deviceOpt)` call. CLI value always wins; the fallback only kicks in when `--device` is absent.

## Decisions Made

1. **Singleton lives in flow-lang, not flow-cli** (per RESEARCH.md Pattern 1 recommendation). flow-cli depends on flow-lang via ProjectReference, not the other way around; putting `FlowConfig` in flow-lang lets the engine read without a circular dependency, while keeping Tomlyn confined to flow-cli.
2. **Use Tomlyn 2.x System.Text.Json-style API.** The plan referenced the legacy `Toml.ToModel<T>(text)` API (per RESEARCH.md). Tomlyn 2.0 was a major redesign that removed `Toml.ToModel` and `Toml.FromModel`, replacing them with `TomlSerializer.Deserialize<T>` / `TomlSerializer.Serialize` and a `TomlSerializerOptions { PropertyNamingPolicy }` configuration object. Used `JsonNamingPolicy.SnakeCaseLower` so snake_case TOML keys map to PascalCase POCO properties without per-property `[JsonPropertyName]` attributes. See Deviations § Rule 3 below.
3. **Test isolation via try/finally + `FlowConfig.Reset()`.** No xUnit fixture / `[Collection]` machinery needed — each fact wraps its body in `try { ... } finally { FlowConfig.Reset(); }` so test ordering cannot leak state across the static singleton.
4. **Charitable malformed-input policy.** Per CLAUDE.md `feedback_charitable_interpretation` memory: malformed `default_timesig` (e.g. "not a timesig", or "5/3" with non-power-of-2 denominator) does NOT abort the interpreter. Instead it falls back silently to 4/4 + emits a single stderr Warning at first encounter (a static latch prevents spamming the warning on every `GetMusicalContext` call, since note streams call it per-note).
5. **`stdlib_search_path` exposed indirectly via `FlowConfig.ConfiguredStdlibSearchPaths`.** This is a derived read (`Active.StdlibSearchPath ?? new List<string>()`) that gives callers (here, `FlowEngine`) a never-null surface to iterate over. FlowEngine seeds `ModuleLoader.AdditionalSearchPaths` once at construction; the loader then consults them at `ResolvePath` time without ever importing `FlowConfig`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Tomlyn API mismatch — plan referenced legacy `Toml.ToModel<T>` removed in Tomlyn 2.0**

- **Found during:** Task 2 (FlowConfigLoader.cs build)
- **Issue:** The plan + RESEARCH.md sketched `var model = Toml.ToModel<FlowConfigPoco>(text)`. Tomlyn 2.3.2 (the locked version per `Recommended Stack: Tomlyn 2.3.2`) is a major redesign — `Toml.ToModel` and `Toml.FromModel` are removed. The packaged readme.md inside the NuGet artifact (`~/.nuget/packages/tomlyn/2.3.2/readme.md`) confirms: "Tomlyn v1 is a major redesign with breaking changes from earlier versions. It uses a **`System.Text.Json`-style API** with `TomlSerializer`, `TomlSerializerOptions`, and resolver-based metadata." Build error: `CS0103: The name 'Toml' does not exist in the current context`.
- **Fix:** Rewrote FlowConfigLoader.cs to use `TomlSerializer.Deserialize<FlowConfigPoco>(text, options)` with a static `TomlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }`. The naming-policy option replaces the implicit snake_case → PascalCase auto-mapping that the legacy `Toml.ToModel` did out-of-the-box; `SnakeCaseLower` is the System.Text.Json policy with identical mapping semantics (`default_tempo` ↔ `DefaultTempo`).
- **Files modified:** `flow-cli/Config/FlowConfigLoader.cs` (added `using System.Text.Json;`, added `SerializerOptions` static field, swapped the deserialize call)
- **Verification:** `dotnet build flow-cli` → 0 errors. Missing-config smoke (silent fallback, exit 0). Malformed-config smoke ('Warning:' to stderr + exit 0). 8/8 Phase30 facts GREEN.
- **Committed in:** `f8ca1ed` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking — library version drift). No scope creep — the propagation surface is exactly as specified; only the loader's internal API call site changed to match the actual library version.

**Impact on plan:** Functional equivalence is intact. The 4 optional-key propagation paths all work; the 8 xUnit facts pin them; the smoke tests confirm charitable behavior on missing/malformed config. Future RESEARCH.md updates should note that Tomlyn 2.x's API is System.Text.Json-style, not the legacy `Toml.ToModel<T>` referenced in Plan 30-03's research section.

## Issues Encountered

- **Worktree branch tip predated `3063a60` base commit.** The worktree's HEAD pointed at `be8c966` (a pre-phase-30 release tag) and `.planning/` was absent. Resolved by `git fetch origin && git reset --hard 3063a60` before reading the plan. This is the documented startup-time recovery path in the agent harness.
- **Tomlyn upstream docs are still showing pre-2.0 API** in some sites (e.g. the Context7 mirrored github README extract still references `Toml.ToModel`). The packaged `readme.md` inside the 2.3.2 NuGet artifact correctly documents the new API. Fixed by reading the packaged readme directly.

## Acceptance Criteria — final tally

- [x] All 4 tasks executed; each committed individually (Task 1: `475838c`; Task 2: `f8ca1ed`; Task 3: `a34c904`; Task 4: `8116b2f`)
- [x] `flow-lang/Runtime/FlowConfig.cs` static singleton with all 5 keys (4 optional + InstallPath placeholder, all nullable)
- [x] `flow-cli/Config/FlowConfigLoader.cs` loads `~/.config/flow/config.toml` via Tomlyn 2.3.2 — actually via `TomlSerializer.Deserialize<T>` (Tomlyn 2.x API). Charitable on missing/malformed.
- [x] `flow-cli/Program.cs` calls `FlowConfigLoader.LoadFromXdg()` first thing in Main
- [x] `flow-lang/Runtime/ExecutionContext.cs` `DefaultTempo` + `DefaultTimesig` fallback wired with `ParseTimesigOrDefault` charitable helper + single-shot warning latch
- [x] `flow-lang/Runtime/ModuleLoader.cs` honors `FlowConfig.Active.ConfiguredStdlibSearchPaths` (via `AdditionalSearchPaths` seeded by FlowEngine)
- [x] `flow-cli/Commands/{Run,Play,Watch}Command.cs` all contain `device ??= FlowConfig.Active.DefaultAudioDevice` (grep verifies)
- [x] `flow-lang.Tests/Integration/Phase30/FlowConfigPropagationTests.cs` has 8 Facts (≥4 optional-key + 4 baseline)
- [x] `dotnet build` solution-wide → 0 errors
- [x] `dotnet test flow-lang.Tests --filter FlowConfigPropagation` → 8/8 PASS
- [x] `dotnet test flow-lang.Tests` → 1000/1000 PASS (was 992 baseline + 8 new — net new tests only, no regressions)
- [x] flow-midi.Tests baseline unchanged: 5/13 GREEN (matches Wave 1 baseline; Plan 30-07 flips Bug B facts)
- [x] SUMMARY.md created at `.planning/phases/30-flow-cli-formal-install/30-03-SUMMARY.md`
- [x] No modifications to STATE.md or ROADMAP.md (parallel-executor constraint)

## Next Phase Readiness

- **REQ-4 acceptance fully met:** composer can write `~/.config/flow/config.toml` with all 5 keys; each optional key now visibly affects interpreter behavior.
- **Patterns established for future phases:**
  - Static-singleton config holder + one-way `flow-cli → flow-lang` write direction is now the documented pattern. Future config keys (e.g. Plan 32 Scala SCL paths) can extend `FlowConfigPoco` with one more nullable field + one engine read site + one Fact, without re-plumbing the loader.
  - Three-tier fallback chain (`call-stack ?? FlowConfig ?? baked`) generalizes to any future "scope-able with a config default" knob — e.g. `default_swing`, `default_key`, `default_velocity` would slot in identically.
  - Charitable-fallback pattern for malformed config strings: single stderr Warning latched per process + apply baked default + continue. Tested via the `Malformed_Default_Timesig_Falls_Back_To_4_4_Silently` fact.
- **No blockers for Plans 30-04 / 30-05 / 30-07 / 30-08 / 30-09.** The FlowConfig.Active surface is locked + tested; subsequent plans can read additional keys without coordination.

## Self-Check: PASSED

- File `flow-lang/Runtime/FlowConfig.cs` — FOUND
- File `flow-cli/Config/FlowConfigLoader.cs` — FOUND
- File `flow-lang.Tests/Integration/Phase30/FlowConfigPropagationTests.cs` — FOUND
- Commit `475838c` — FOUND
- Commit `f8ca1ed` — FOUND
- Commit `a34c904` — FOUND
- Commit `8116b2f` — FOUND
- `dotnet test flow-lang.Tests` 1000/1000 PASS — VERIFIED
- `dotnet test flow-lang.Tests --filter Phase30.FlowConfigPropagationTests` 8/8 PASS — VERIFIED

---
*Phase: 30-flow-cli-formal-install*
*Plan: 03*
*Completed: 2026-05-10*
