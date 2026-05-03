---
id: SEED-002
status: dormant
planted: 2026-05-02
planted_during: v1.3 Composer DX Tier B/C — Phase 22 closure
trigger_when: Starting v1.4, or any milestone focused on packaging, distribution, external-language interop, or end-user tooling
scope: Medium
---

# SEED-002: Flow CLI + Formal Install

## Why This Matters

Today there is no `flow` binary. Running a script means
`dotnet run --project flow-interpreter path/to/script.flow` from the source
tree, which is fine for development but makes Flow effectively unusable
for anyone who doesn't have the repo cloned. The interpreter project
already implements run/eval/repl/watch — what's missing is packaging,
installation, and a few new utility subcommands.

A `flow` CLI also unblocks two natural follow-ons:

- **MIDI ↔ Flow conversion** — turning `.mid` files into editable Flow
  source (`midi2flow`) is the missing piece for collaboration with
  composers using DAWs. The MIDI parser already exists in `flow-midi/`;
  the new code is an AST→source pretty-printer.
- **External-language interop** (the rest of the v1.4 milestone) — once
  Flow has a real binary, scripting it from other languages or build
  systems becomes trivial.

The README currently overstates ergonomics by implying Flow is
self-contained; an installable binary closes that gap.

## When to Surface

**Trigger:** Starting v1.4, or any milestone focused on packaging,
distribution, external-language interop, or end-user tooling.

This seed should be presented during `/gsd-new-milestone` when the
milestone scope matches any of:
- Milestone explicitly scoped to "v1.4"
- Milestone goals mention "CLI", "install", "packaging", "distribution",
  "binary", "external", "interop", "FFI", "embed", or "end-user"
- Milestone follows v1.3 close (next-in-line slot)

## Scope Estimate

**Medium** — likely a phase or two. Rough phase shape:

1. **`flow-cli` project + subcommand framework** — new C# project sibling
   to `flow-lang` / `flow-interpreter`. Subcommand parser
   (System.CommandLine or hand-rolled). Wire `run`, `eval`, `repl`,
   `watch` as thin shells over existing interpreter entrypoints.
2. **New utility subcommands** — `play <f>` (render + PulseAudio),
   `render <f> -o out.wav`, `flow2midi <f> -o out.mid` (thin shell over
   `MidiExport`), `check <f>` (parse + type-check, no execute),
   `version`, `new <name>` (scaffold).
3. **Install pipeline** — `dotnet publish` profile + install script.
   Decide self-contained (~80MB, no .NET runtime needed) vs
   framework-dependent (~1MB, needs .NET 10). Drop launcher at
   `/usr/local/bin/flow` (NOT `/usr/bin/` — distro-managed).
4. **XDG config** — `~/.config/flow/config.toml` with at minimum
   `install_path`, `default_audio_device`. Read by launcher and
   propagated to interpreter as env vars or argv.
5. **`midi2flow`** — AST→Flow-source pretty-printer over the existing
   `flow-midi` parser. This is the only genuinely new feature in the
   bundle; the rest is wiring. Could ship as its own phase if scope
   tightens.

## Decisions Pending

These came up during capture and should be locked in spec-phase, not now:

- **Distribution model** — bash launcher (small, requires .NET 10
  runtime) vs self-contained binary (large, zero runtime deps)
- **Install location** — `/opt/flow/` (system-wide, requires sudo) vs
  `~/.local/share/flow/` (per-user, no sudo)
- **Config schema keys** — install_path is required; what else
  (default tempo, default audio backend, stdlib search path override?)
- **v1 subcommand cut** — which from the list above ship in the first
  phase vs defer
- **`midi2flow` source-emit style** — flat note-stream `| C4 D4 E4 |`
  vs reconstructed `section` / `tempo` / `key` blocks. The latter is
  much harder; the former is good enough for round-trip.

## Breadcrumbs

Existing code likely to be touched / wrapped:

- `flow-interpreter/Program.cs` — current CLI entrypoint with run/eval/
  repl/watch flags; the `flow-cli` subcommand handlers wrap this layer
- `flow-lang/Core/FlowEngine.cs` — orchestrator the CLI dispatches to
- `flow-lang/Runtime/ModuleLoader.cs:133` `ResolveStdlibPath` —
  **good news:** already resolves stdlib `.flow` files relative to the
  loaded assembly directory, so a publish that copies the stdlib
  alongside the DLL works without code changes. No resolver rewrite
  needed for the install case.
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` — `flow2midi`
  subcommand wraps this directly
- `flow-midi/` — the parser side that `midi2flow` consumes
- `flow-lang/Audio/AudioPlaybackManager.cs` — `play` subcommand entry
- `flow-lang/StandardLibrary/Audio/FileIO.cs` — `render` subcommand
  writes via `WriteWav` here
- `flow-lang.sln` — needs new `flow-cli` project added
- README — installation section currently nonexistent / aspirational;
  needs the new `flow` install instructions

Repo files that don't exist yet but should:
- `flow-cli/Program.cs`
- `flow-cli/Commands/{Run,Eval,Repl,Watch,Play,Render,Flow2Midi,Midi2Flow,Check,Version,New}.cs`
- `flow-cli/Config/FlowConfig.cs` (config.toml reader)
- `flow-cli/flow-cli.csproj`
- `scripts/install.sh` (publish + copy + symlink + write default config)
- `flow-lang/Emit/FlowSourceEmitter.cs` (the new midi2flow target)

Related decisions in PROJECT.md:
- "Linux primary (PulseAudio dependency), but IAudioBackend abstraction
  exists for portability" — install script Linux-first; macOS/Windows
  can layer on later
- "Existing .flow scripts and test suite must continue to work" — CLI
  must preserve existing `dotnet run --project flow-interpreter ...`
  invocations during transition

## Notes

- Captured 2026-05-02 right after Phase 22 closure during a session
  exploring whether Flow could be installed to `/usr/bin/`.
- User intuition was sound: a small launcher pointing at a config that
  points at the install root is the right shape — same model as
  `node` / `nvm`, `python` / `pyenv`, `dotnet` itself.
- `/usr/local/bin/` is the correct install target on Linux for
  user-installed binaries; `/usr/bin/` is reserved for distro-managed
  packages.
- This seed sits naturally next to SEED-001 (LSP / JetBrains) since
  both surface during v1.4 — together they form the "ergonomics &
  distribution" half of v1.4 alongside the language work.
