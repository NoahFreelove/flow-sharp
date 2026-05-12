# Phase 30: Flow CLI + Formal Install — Specification

**Created:** 2026-05-10
**Ambiguity score:** 0.14
**Requirements:** 8 locked

## Goal

A single self-contained `flow` Linux x64 binary, installable via a shell script to either system-wide (`/usr/local/bin/flow`) or per-user (`~/.local/bin/flow`) locations, that unifies the existing `flow-interpreter` and `flow-midi` entrypoints under a subcommand-routed surface: `run`, `eval`, `repl`, `watch`, `play`, `render`, `flow2midi`, `midi2flow`, `check`, `version`, `new`. Composers can use Flow without cloning the repo. `midi2flow` is the only genuinely-new piece of functionality (everything else wraps existing logic); it emits flat note-stream Flow source per MIDI track that round-trips back through `flow2midi` matching note count + pitch + duration.

## Background

Today (post-Phase 28 baseline):

**Existing entrypoints:**
- `flow-interpreter/Program.cs` — current CLI with bare-flag arg parsing supporting `-e`/`--eval`, `--watch`/`-w`, `--device`, `--verbose`, positional script path. Already prints usage as "flow <file>" / "flow -e <code>" — the binary is named "flow" in spirit but doesn't exist as an installable file.
- `flow-midi/Program.cs` — separate binary with arg parsing for `midi2flow` workflow: positional input, `-o` output, `--dump`, `-h`.
- `flow-midi/Conversion/FlowGenerator.cs` (309 lines) — already-implemented MIDI→Flow-source generator. `public static string Generate(MidiFile midi, QuantizeResult quantizeResult, string sourceFileName)`. Phase 30 wraps this via the new `flow midi2flow` subcommand.
- `flow-midi/Conversion/Quantizer.cs` — quantization pass that feeds the generator.

**Existing audio + render surface:**
- `flow-lang/StandardLibrary/Audio/AudioPlaybackManager.cs` — PulseAudio playback path for `flow play`.
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` — `WriteMidi(filepath, song)` for `flow flow2midi`.
- `flow-lang/StandardLibrary/Audio/FileIO.cs` — `WriteWav` for `flow render`.
- `flow-lang/Core/FlowEngine.cs` — orchestrator the CLI dispatches to.
- `flow-lang/Runtime/ModuleLoader.cs` — `ResolveStdlibPath` already resolves stdlib relative to the loaded assembly directory, so a `dotnet publish` that copies stdlib alongside the DLL works without resolver changes.

**Distribution gap:**
- No `flow` binary; users must run `dotnet run --project flow-interpreter path/to/script.flow` from the source tree.
- No install script, no published artifact, no `~/.config/flow/config.toml`.
- README implies Flow is installable but currently isn't.

**Solution structure:**
- `flow-sharp.sln` has 6 projects: `flow-lang`, `flow-interpreter`, `flow-midi`, `flow-lang.Tests`, `flow-lsp`, `Migrate26`. Phase 30 adds a 7th: `flow-cli`.

This phase consolidates rather than rewrites — most of the logic exists, the work is unification, subcommand routing, packaging, and the new `midi2flow` CLI surface (whose engine already lives in `FlowGenerator.cs`).

## Requirements

1. **Unified `flow` binary via new `flow-cli` project**: A single executable that routes all subcommands.
   - Current: Two separate executables (`flow-interpreter`, `flow-midi`) with disjoint arg parsing
   - Target: New `flow-cli/flow-cli.csproj` sibling to existing projects. Single `Program.cs` parses `flow <subcommand> [args...]` and dispatches to the right handler. Handlers live in `flow-cli/Commands/{Run,Eval,Repl,Watch,Play,Render,Flow2Midi,Midi2Flow,Check,Version,New}.cs`. Subcommand framework: `System.CommandLine` (already in .NET) or hand-rolled (light dependency footprint). Decision locked in plan-phase
   - Acceptance: `flow run script.flow`, `flow eval "code"`, `flow repl`, `flow watch script.flow`, `flow play script.flow`, `flow render script.flow -o out.wav`, `flow flow2midi script.flow -o out.mid`, `flow midi2flow in.mid -o out.flow`, `flow check script.flow`, `flow version`, `flow new my-piece` — all 11 subcommands exit 0 on valid input

2. **Self-contained Linux x64 binary via `dotnet publish`**: One artifact, no .NET runtime install required on the target machine.
   - Current: No published artifact; running Flow requires `dotnet` on PATH
   - Target: `dotnet publish flow-cli -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false` produces a single `flow` binary plus a `Samples/` (Phase 29) + stdlib `.flow` files in the publish directory. Total bundle size budget: ≤ 120 MB (allows .NET runtime + DryWetMidi + stdlib + Phase 29 5 MB samples). Trimmed=false because reflection in `gsd-sdk` / overload resolver / module loader may pull surprising types
   - Acceptance: Publish produces a self-contained directory. Running `./flow run script.flow` from the publish dir works on a clean Linux x64 system with PulseAudio installed and no .NET runtime

3. **Install script with system-wide + per-user modes**: `scripts/install.sh` supports both install locations via a flag.
   - Current: No install script
   - Target: `scripts/install.sh` defaults to per-user mode (`~/.local/share/flow/` + symlink to `~/.local/bin/flow`). Flag `--system` switches to system-wide mode (`/usr/local/share/flow/` + symlink to `/usr/local/bin/flow`, requires `sudo`). Script runs `dotnet publish`, copies the published directory to the install root, creates the symlink, writes a default `~/.config/flow/config.toml` if absent. Idempotent — re-running upgrades in place
   - Acceptance: `./install.sh` (no flag) installs to `~/.local/share/flow/` without `sudo`; `flow version` works from a new shell. `./install.sh --system` installs to `/usr/local/share/flow/` with `sudo`; same `flow version` works for any user

4. **XDG config at `~/.config/flow/config.toml`**: All 4 config keys read by the launcher and propagated to the interpreter.
   - Current: No config file; no config reader
   - Target: New `flow-cli/Config/FlowConfig.cs` reads `~/.config/flow/config.toml` (TOML format via Tomlyn or hand-rolled — decided in plan-phase). Required key: `install_path` (where stdlib lives post-install). Optional keys: `default_audio_device` (PulseAudio device name passed to `--device` if not overridden), `default_tempo` (integer BPM applied to scripts with no `tempo` block), `default_timesig` (string like `"4/4"` applied to scripts with no `timesig` block), `stdlib_search_path` (colon-separated list appended to ModuleLoader's search paths). Config values are propagated to FlowEngine via either env vars or argv; design decided in plan-phase
   - Acceptance: A `config.toml` containing all 5 keys is read at `flow run` startup; the active values are reflected in interpreter behavior (e.g. running a tempo-less script under `default_tempo = 100` renders at 100 BPM). Missing config file is a silent fallback to baked-in defaults

5. **`midi2flow` produces round-trippable flat note-stream output**: The new CLI surface for `FlowGenerator.cs` emits one `Sequence` per MIDI track with minimal scaffolding (tempo / timesig / key from MIDI meta events).
   - Current: `flow-midi/Program.cs` already runs the generator; output style is whatever the existing `FlowGenerator.Generate(...)` produces
   - Target: `flow midi2flow input.mid -o output.flow` wraps the existing generator. Output structure: optional `tempo N { timesig N/M { key X { ... } } }` wrapping the section; one `Sequence trackN = | C4q D4q ... |` per MIDI track inside a single `section roundtrip { ... }`; one final `Song s = [roundtrip]`. NO pattern reconstruction (no `repeat`, `(transpose)`, `(retrograde)`, etc. — flat output only). If the existing `FlowGenerator.cs` already does this, no changes needed; if it emits a different style, adjust to flat
   - Acceptance: `flow midi2flow tests/fixtures/sample.mid -o /tmp/sample.flow` produces a `.flow` file that parses successfully (`flow check /tmp/sample.flow` exits 0). Per-track sequences are present. Section + Song wrappers present

6. **Round-trip note-count + pitch + duration match**: `midi2flow` → `flow2midi` round-trip preserves musical content up to micro-timing.
   - Current: No round-trip test; no acceptance contract
   - Target: New integration test `flow-lang.Tests/Integration/Phase30/Midi2FlowRoundTripTests.cs`. For each fixture: `flow midi2flow input.mid -o out.flow && flow flow2midi out.flow -o roundtrip.mid`. Parse both `input.mid` and `roundtrip.mid` via DryWetMidi. Assert: equal note count per track, equal (pitch, duration-rational) pairs in order. Tolerate ≤ ±1 MIDI tick drift per event (rounding noise). Fixtures: 3 small public-domain MIDI files (e.g. Bach 2-part invention, Joplin Maple Leaf opening, simple drum loop) committed to `flow-lang.Tests/fixtures/midi/`
   - Acceptance: Round-trip test passes for all 3 fixtures with note-count + pitch + duration match, ±1 tick tolerance

7. **Smoke test for install pipeline**: `scripts/test-install.sh` verifies a fresh publish + install actually works.
   - Current: No automated install verification
   - Target: New `scripts/test-install.sh` that: (a) creates a clean tempdir, (b) runs `./install.sh` with a flag pointing the install root at the tempdir (e.g. `--install-root /tmp/flow-test-NNN`), (c) sets `PATH=$tempdir/bin:$PATH`, (d) runs `flow version` (must exit 0, must print a semver string), (e) runs `flow check examples/showcase.flow` (must exit 0), (f) runs `flow render examples/showcase.flow -o /tmp/test.wav` (must exit 0, must produce non-empty WAV), (g) cleans up tempdir. Script exits 0 on success, non-zero with diagnostic on any step failure. CI-runnable
   - Acceptance: `bash scripts/test-install.sh` exits 0 on a clean checkout after `dotnet build` succeeds

8. **Backward compatibility — `dotnet run --project flow-interpreter` continues to work**: Existing development workflow preserved during transition.
   - Current: `dotnet run --project flow-interpreter path/to/script.flow` is the dev entrypoint
   - Target: `flow-interpreter` project remains in the solution and continues to run scripts identically. New `flow-cli` project depends on (or duplicates the thin Main shell from) `flow-interpreter`'s entrypoint logic. Documentation updated to recommend `flow` for end users; `dotnet run --project flow-interpreter` remains the dev-mode invocation
   - Acceptance: All 4 .flow scripts that were tested in Phase 27 (tutorial.flow, showcase.flow, h_alias.flow, microtonal_ji.flow) continue to exit 0 under `dotnet run --project flow-interpreter` after Phase 30 ships

## Boundaries

**In scope:**
- New `flow-cli/flow-cli.csproj` project + `Program.cs` + `Commands/*.cs` subcommand handlers
- 11 subcommands: `run`, `eval`, `repl`, `watch`, `play`, `render`, `flow2midi`, `midi2flow`, `check`, `version`, `new`
- `dotnet publish` profile for self-contained Linux x64 single-file binary
- `scripts/install.sh` with `--system` flag toggle (per-user default)
- `scripts/test-install.sh` smoke test
- `flow-cli/Config/FlowConfig.cs` TOML reader for `~/.config/flow/config.toml`
- Config keys: `install_path`, `default_audio_device`, `default_tempo`, `default_timesig`, `stdlib_search_path`
- New integration test class `flow-lang.Tests/Integration/Phase30/Midi2FlowRoundTripTests.cs`
- 3 public-domain MIDI fixtures under `flow-lang.Tests/fixtures/midi/`
- README updates documenting install + `flow` usage
- `flow new my-piece` scaffold template (decide template content during plan-phase — likely a minimal tempo/timesig/key block + one section + one sequence + writeWav call)

**Out of scope:**
- macOS / Windows builds — Linux x64 only this phase; cross-platform deferred to v1.5+
- Package manager distribution (apt, brew, snap, .deb, .rpm) — install script only; native packaging deferred to v1.5+
- Auto-update mechanism (`flow update`) — users re-run install script for upgrades; deferred to v1.5+
- GUI launcher / installer — CLI only this phase
- Framework-dependent binary alternate — self-contained only this phase (smaller artifact deferred)
- Plugin / extension system for loading third-party Flow modules from arbitrary paths — `stdlib_search_path` config key handles power-user case; richer plugin system deferred
- Structured MIDI→Flow source emit (pattern reconstruction, `repeat`/`(transpose)` recognition) — flat note-stream only this phase
- Live-update of installed binary while a `flow` process is running — install requires no active `flow` processes
- macOS keychain / Windows registry config storage — config.toml only

**Adjacent problems excluded:**
- LSP installation alongside `flow` binary — Phase 31 owns LSP packaging
- Sample library distribution alongside `flow` binary — Phase 29 already bundles ≤ 5 MB samples in `flow-lang/Samples/` which the publish step picks up; no separate sample-pack subcommand
- `.flow` file-association registration in desktop environments — deferred
- Telemetry / crash reporting — never in scope without explicit opt-in (out of scope this phase)

## Constraints

- **Linux x64 only**: Phase 30 ships one binary for one platform. PulseAudio remains the audio dependency.
- **Self-contained bundle size budget**: ≤ 120 MB total (.NET runtime + DryWetMidi + stdlib + Phase 29 5 MB samples + any other assets). CI enforces via `du -sh` on the publish directory.
- **Install location compatibility**: `/usr/local/bin/flow` is the ONLY system-wide target (NOT `/usr/bin/` which is reserved for distro-managed packages). `~/.local/bin/flow` is the ONLY per-user target.
- **PATH expectation**: System-wide install: `/usr/local/bin` is on default PATH on all major Linux distros. Per-user install: `~/.local/bin` is on default PATH for systemd-based distros from ~2020 onward. Install script prints a warning if PATH doesn't contain the target.
- **Config file format**: TOML 1.0 spec. Either Tomlyn package (small, well-maintained NuGet) or hand-rolled (zero dependency, supports only the 5 keys we need). Decision in plan-phase.
- **Subcommand framework**: `System.CommandLine` is in .NET base; gives free `--help` / `--version` / completion scripts. Alternative: hand-rolled (matches existing flow-interpreter style; ~50 lines). Decision in plan-phase.
- **No new external runtime dependencies**: Self-contained binary bundles .NET + DryWetMidi (already in flow-lang). No additional NuGet packages should land in the publish output beyond what flow-lang already pulls.
- **Backward compatibility**: `dotnet run --project flow-interpreter ...` continues to work for the entire transition period. Phase 30 does NOT delete `flow-interpreter`'s Program.cs; it may internally restructure entrypoint logic so both projects share the dispatcher.
- **Test runtime budget**: Round-trip tests (3 fixtures) must run within 15 seconds total. Install smoke test must run within 60 seconds.
- **MIDI fixture license**: All 3 round-trip MIDI fixtures must be public-domain or CC0. License file co-located.

## Acceptance Criteria

- [ ] `flow-cli/` project exists in solution; builds via `dotnet build`
- [ ] `flow-cli/Commands/*.cs` contains handlers for all 11 subcommands (Run, Eval, Repl, Watch, Play, Render, Flow2Midi, Midi2Flow, Check, Version, New)
- [ ] `dotnet publish flow-cli -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true` produces a self-contained `flow` binary
- [ ] `du -sh` on the published directory ≤ 120 MB
- [ ] `scripts/install.sh` (no flag) installs to `~/.local/share/flow/` with symlink at `~/.local/bin/flow` without `sudo`
- [ ] `scripts/install.sh --system` installs to `/usr/local/share/flow/` with symlink at `/usr/local/bin/flow` (requires `sudo`)
- [ ] `scripts/test-install.sh` runs full pipeline (publish + install to tempdir + run smoke commands) and exits 0
- [ ] `~/.config/flow/config.toml` with all 5 keys is read by the launcher; values reflect in runtime (verified via integration test reading config + asserting `FlowEngine.MusicalContext.Tempo` matches `default_tempo`)
- [ ] `flow run script.flow`, `flow check script.flow` produce expected outputs
- [ ] `flow eval "Int x = 5; (print (str x))"` prints `5` to stdout
- [ ] `flow render examples/showcase.flow -o /tmp/out.wav` produces non-empty WAV
- [ ] `flow play examples/showcase.flow` exits 0 on a PulseAudio-equipped system (manual UAT, not CI-able)
- [ ] `flow flow2midi examples/showcase.flow -o /tmp/out.mid` produces non-empty MIDI
- [ ] `flow midi2flow flow-lang.Tests/fixtures/midi/sample.mid -o /tmp/sample.flow` produces parseable Flow source (verified by `flow check /tmp/sample.flow` exiting 0)
- [ ] `flow new my-piece` scaffolds a working starter project that renders without error
- [ ] `flow version` prints a semver string matching the current release tag
- [ ] 3 round-trip MIDI fixtures pass `Midi2FlowRoundTripTests` (note-count + pitch + duration match, ±1 tick tolerance)
- [ ] `dotnet run --project flow-interpreter examples/showcase.flow` still works (backward compat)
- [ ] All 4 v1.3 Phase 27 fixtures (tutorial.flow, showcase.flow, h_alias.flow, microtonal_ji.flow) continue to render under both `flow run` and `dotnet run --project flow-interpreter`
- [ ] README.md updated with install instructions + `flow` subcommand reference
- [ ] Full unit suite GREEN (no regressions to Phase 28 / earlier facts)

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                                                                  |
|--------------------|-------|------|--------|----------------------------------------------------------------------------------------|
| Goal Clarity       | 0.92  | 0.75 | ✓      | All 11 subcommands locked; single binary, single platform                              |
| Boundary Clarity   | 0.85  | 0.70 | ✓      | macOS/Win, package managers, auto-update, GUI all explicit-deferred                    |
| Constraint Clarity | 0.80  | 0.65 | ✓      | 120 MB cap, /usr/local + ~/.local locked, TOML format, ±1 tick round-trip tolerance    |
| Acceptance Criteria| 0.85  | 0.70 | ✓      | 22 pass/fail criteria; smoke test gated, round-trip test gated                         |
| **Ambiguity**      | 0.14  | ≤0.20| ✓      | Gate passed                                                                            |

## Interview Log

| Round | Perspective       | Question summary                                                                                | Decision locked                                                                                            |
|-------|-------------------|-------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------|
| 1     | Researcher        | v1 subcommand cut?                                                                              | All 4 groups: core (run/eval/repl/watch), audio (play/render), MIDI (flow2midi/midi2flow), utility (check/version/new) |
| 1     | Researcher        | Distribution model?                                                                             | Self-contained Linux x64 binary (~80 MB target, ≤120 MB cap)                                              |
| 1     | Researcher        | Cross-platform scope?                                                                           | Linux x64 only this phase; macOS/Win deferred                                                              |
| 2     | Boundary Keeper   | Install location — system-wide or per-user?                                                     | Both supported: install.sh defaults to per-user; --system flag enables system-wide                         |
| 2     | Boundary Keeper   | Config schema?                                                                                  | install_path + default_audio_device + default_tempo + default_timesig + stdlib_search_path (5 keys)        |
| 2     | Boundary Keeper   | Top out-of-scope items?                                                                         | apt/brew/snap, auto-update, GUI launcher (plugin system stays in scope via stdlib_search_path config)      |
| 3     | Failure Analyst   | Install verification mechanism?                                                                 | Smoke script: installs to tempdir, sets PATH, runs flow on fixture, checks output (CI-runnable)            |
| 3     | Failure Analyst   | midi2flow round-trip quality?                                                                   | Note count + pitch + duration match; tolerate ±1 tick drift; 3 public-domain MIDI fixtures                 |
| 3     | Failure Analyst   | midi2flow source-emit style?                                                                    | Flat note-stream per track + minimal scaffolding (no pattern reconstruction)                               |

---

*Phase: 30-flow-cli-formal-install*
*Spec created: 2026-05-10*
*Next step: /gsd-discuss-phase 30 — implementation decisions (subcommand framework choice, TOML parser choice, config propagation mechanism, install.sh dependency on dotnet sdk vs prebuilt artifact)*
