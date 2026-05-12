# Phase 30: Flow CLI + Formal Install — Research

**Researched:** 2026-05-10
**Domain:** .NET CLI packaging, MIDI round-trip quantization, shell install patterns
**Confidence:** HIGH on stack/packaging; MEDIUM on Bug B scope (verified by reading code + ragtime output, but no synthetic-fixture test has actually been run yet)

## Summary

Phase 30 is **two phases in a trench coat**. The SPEC frames it as "unification, subcommand routing, packaging" — that's accurate for 6 of the 8 requirements (REQ-1, 2, 3, 4, 7, 8). But REQ-5 and especially REQ-6 (round-trip with ±1-tick tolerance over 3 fixtures) are **load-bearing on a flow-midi rewrite** that the SPEC undersells with "if the existing FlowGenerator.cs already does this, no changes needed". The existing FlowGenerator emits structure that is in the right shape, but it sits on top of a Quantizer that produces output the composer has already pronounced wrong on `examples/ragtime.mid`. The visible artifacts in `examples/output/ragtime_imported.flow` confirm the symptoms: 4 entire bars of `_` at the start of two sequences, dotted-sixteenths (`D4s.`) followed by long rest tails where quarter-notes should be, and 355 rest tokens in a 374-line file (≈ one `_` per token-on-average — extreme over-resting).

**Primary recommendation:**

1. **Scope-correct the phase.** Frame REQ-5/6 as "midi2flow round-trip rebuild" — a substantive flow-midi rewrite, NOT a wrap. Plan-phase should structure this as 3-5 explicit tasks (synthetic fixture tests → Quantizer rewrite → RH/LH heuristic removal → FlowGenerator emit-format adjustments → integration tests).
2. **Subcommand framework: `System.CommandLine` 2.0.7 (stable, .NET 10).** It went stable Nov 2025 and the latest is 2.0.7 as of April 2026. Free `--help`, `--version`, type-safe binding; 11 subcommands × ~50 lines hand-rolled would be ~550 lines of duplicated boilerplate. The size impact is negligible vs. the 80-100 MB self-contained .NET runtime baseline.
3. **TOML parser: Tomlyn 2.3.2 stable.** Zero dependencies, NativeAOT-ready, targets net10.0 directly. Hand-rolling 5 keys is technically feasible but Tomlyn's TomlTable model + ToModel<T> POCO mapping is 3-line config-load code; hand-rolled would be ~80 lines + edge cases (quoted strings, comments, line continuations, blank lines).
4. **Config propagation: static singleton `FlowConfig.Active` populated by the CLI launcher at startup; `FlowEngine` reads `FlowConfig.Active.DefaultTempo` etc.** Argv plumbing through 11 subcommands × 5 keys is 55 wiring points; env vars introduce action-at-a-distance and pollute the test process environment. Static singleton is the standard pattern for app-wide config (analogous to `IConfiguration` in ASP.NET Core but heavier-handed for our scale).
5. **Install script: prebuilt-tarball model (CI publishes; install script downloads).** A `dotnet publish`-from-install-script approach requires .NET SDK on the user's machine, which defeats the whole point of self-contained bundling. CI builds the tarball and uploads to a GitHub release; install.sh downloads + extracts + symlinks. This also keeps install.sh under 200 lines POSIX-compatible bash.
6. **Public-domain MIDI fixtures: hand-author 3 small synthetic fixtures, NOT IMSLP/Mutopia downloads.** Provenance is cleaner (no CC-BY-NC-SA / CC-BY-SA attribution mess), file sizes are predictable, and the composer can target specific edge cases (quarter-with-eighth-eighth ragtime rhythm, two-voice counterpoint, chord-with-overlap). Use Phase 28's `WriteMidi` to GENERATE them from .flow source — this also gives bidirectional fixture provenance (flow2midi → midi2flow → flow2midi).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Subcommand dispatch | flow-cli (new project) | — | New entrypoint; existing `flow-interpreter/Program.cs` becomes a thin shell that calls into flow-cli or stays standalone for backward-compat |
| TOML config read | flow-cli/Config | — | Config is CLI-launcher concern; `FlowEngine` should not depend on TOML |
| Config propagation to engine | flow-cli (writes) + flow-lang (reads via static) | — | `FlowConfig` static lives in flow-lang so engine can read it without circular dep |
| Run/eval/repl/watch | flow-cli Commands | flow-interpreter (existing logic) | Wrap existing `ScriptRunner.RunScript`, `Repl.Run`, `LiveReloadManager` |
| Play/render | flow-cli Commands | flow-lang AudioPlaybackManager + FileIO | Wrap existing `WriteWav`, audio backend |
| flow2midi | flow-cli Commands | flow-lang MidiExport | Wrap existing `WriteMidi(filepath, song)` from Phase 28 |
| **midi2flow** | flow-cli Commands | **flow-midi (Quantizer + FlowGenerator)** | **Wraps flow-midi but flow-midi itself needs rework (REQ-5/6, Bug B)** |
| Quantization grid | flow-midi/Conversion/Quantizer.cs | — | Existing code; needs algorithm rework (see Bug B scope below) |
| MIDI binary parsing | flow-midi/Midi/MidiParser.cs | — | Hand-rolled, working correctly per Bug B "Eliminated" |
| check (syntax-only) | flow-cli Commands | flow-lang FlowEngine.Parse | New thin wrapper that parses without executing |
| version | flow-cli Commands | — | Print baked-in assembly version |
| new (scaffold) | flow-cli Commands | — | Static template emit |
| Install script | scripts/install.sh | — | Bash, no .NET runtime dep on user side |
| Smoke test | scripts/test-install.sh | scripts/install.sh | CI runs scripts/install.sh inside tempdir, then runs `flow version`/`flow check`/`flow render` |
| Self-contained publish | flow-cli/.csproj | dotnet SDK | `dotnet publish` invoked from CI, not from install.sh |

## Standard Stack

### Core (no new external deps; .NET base + existing)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET | 10.0.107 (verified `dotnet --version` on dev box) | Runtime | Already targeted by all flow-* projects [VERIFIED: `dotnet --version` returns 10.0.107] |
| C# 13 | net10.0 default | Language | Records, pattern matching, file-scoped namespaces already pervasive |
| DryWetMidi | 8.0.3 (current pinned version in flow-lang.csproj) | MIDI export | Already used by flow-lang/StandardLibrary/Audio/MidiExport.cs; 9.x is not stable on NuGet yet (latest stable on NuGet remains 8.0.3 family) [VERIFIED: flow-lang.csproj] |

### New for this phase

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| **System.CommandLine** | 2.0.7 (stable, released 2026-04-21) | Subcommand routing | Stable as of .NET 10 ship in Nov 2025; reaches 2.0.7 by April 2026. Provides `RootCommand`/`Command`/`Option`/`Argument` types, auto-generated `--help`/`--version`, tab-completion. AOT-compatible and trim-friendly (relevant for size if Phase 31+ ever wants AOT) [VERIFIED: nuget.org/packages/System.CommandLine listing] |
| **Tomlyn** | 2.3.2 (stable) | TOML config | Zero dependencies, NativeAOT-ready, targets net10.0 + net8.0 + netstandard2.0. ~4.7M downloads on NuGet (as of Jan 2022 baseline; growing). Trivia-preserving syntax tree if we ever need round-trip config writing. BSD-2-Clause license [VERIFIED: nuget.org/packages/Tomlyn] |

**Installation:**

```bash
cd flow-cli
dotnet add package System.CommandLine --version 2.0.7
dotnet add package Tomlyn --version 2.3.2
```

**Version verification commands (run during plan-phase to confirm currency):**

```bash
dotnet nuget locals all --list  # sanity check NuGet cache config
# Lookup latest:
curl -s "https://api.nuget.org/v3-flatcontainer/system.commandline/index.json" | jq -r '.versions[]' | tail -5
curl -s "https://api.nuget.org/v3-flatcontainer/tomlyn/index.json"            | jq -r '.versions[]' | tail -5
```

### Alternatives Considered (rejected)

| Instead of | Could Use | Why Rejected |
|------------|-----------|--------------|
| System.CommandLine | Hand-rolled switch/case (matches existing flow-interpreter/Program.cs style) | 11 subcommands × ~50 lines = ~550 lines of duplicated arg-parse boilerplate. No `--help` generation. No type-safe binding. Doesn't scale. The size argument that historically favored hand-rolled (avoid pulling a parser DLL) is moot here: the self-contained .NET runtime + DryWetMidi is already ~80 MB; System.CommandLine adds <1 MB |
| System.CommandLine | CommandLineParser 2.9.1 | Attribute-driven, decent ergonomics. But Microsoft-owned (Microsoft.CommandLine) is now stable; choosing the Microsoft-owned dependency for a Microsoft-runtime tool aligns with the project's "minimal external deps" philosophy (CommandLineParser is third-party) |
| System.CommandLine | Cocona | Cocona is convention-over-configuration (methods become commands); too much magic for a tool that prizes explicitness. Also pulls in DI containers |
| Tomlyn | Hand-rolled | Feasible — 5 keys, all scalar (string/int) per SPEC-4. Hand-rolled would be ~80 lines. But: TOML 1.0 has subtle rules (basic strings, literal strings, multi-line, comments, BOM, blank lines). Tomlyn handles them all; hand-rolling re-invents this wheel. The library has zero deps so the trade is even more lopsided in favor of using it |
| Tomlyn | JSON (System.Text.Json, in BCL — zero new deps) | Considered. Would be a SPEC change — SPEC-4 locks config format to TOML. NOT recommended to deviate; TOML is more human-editable for end users (composers, not coders) |
| Tomlyn | YAML (YamlDotNet) | Heavier dep, more error-prone (whitespace-sensitive). SPEC locks TOML; don't deviate |

## Architecture Patterns

### System Architecture Diagram

```
                                ┌────────────────────────┐
                user invokes →  │    flow <subcommand>   │
                                │     (flow-cli/Program) │
                                └────────────┬───────────┘
                                             │
                                  ┌──────────┴──────────┐
                                  │  System.CommandLine │
                                  │   RootCommand +     │
                                  │   11 sub-Commands   │
                                  └──────────┬──────────┘
                                             │
   ┌────────────────────────────────────┬────┴────┬────────────────────────────────────┐
   ↓                                    ↓         ↓                                    ↓
 startup (every cmd):           ┌─ Run ─┴─ Eval ─┴─ Repl ─ Watch ─┐               ┌─ Midi2Flow ─┐
 1. read config.toml            │  (wraps flow-interpreter:       │               │  (NEW path) │
 2. populate FlowConfig.Active  │   ScriptRunner / Repl /         │               │             │
 (Tomlyn → POCO → static)       │   LiveReloadManager)            │               └──────┬──────┘
                                └────────┬────────────────────────┘                      │
                                         │                                               │
                                         ↓                                               ↓
                              ┌─ Play ─ Render ─ Flow2Midi ─┐                  ┌────────────────────┐
                              │  (wraps FlowEngine + Audio  │                  │ flow-midi/         │
                              │   playback / FileIO /       │                  │   MidiParser →     │
                              │   MidiExport)               │                  │   Quantizer        │
                              └────────┬────────────────────┘                  │     (REWORK)  →    │
                                       │                                       │   FlowGenerator    │
                                       ↓                                       │     (REWORK)       │
                              ┌───────────────────┐                            └──────────┬─────────┘
                              │   FlowEngine      │                                       │
                              │   (existing)      │                                       ↓
                              │                   │                                  .flow source
                              │   ↓ reads         │                                  written to disk
                              │   FlowConfig.     │
                              │   Active.*        │
                              └───────────────────┘

 install.sh (POSIX bash, < 200 lines):
   1. Determine install root (default ~/.local/share/flow, --system → /usr/local/share/flow)
   2. Download tarball from GitHub release (curl) OR use --local-tarball flag for CI/dev
   3. Extract → $install_root/flow-vX.Y.Z/
   4. Symlink $bin_root/flow → $install_root/flow-vX.Y.Z/flow
   5. Idempotent: ln -sfn always overwrites
   6. Write ~/.config/flow/config.toml if absent (default keys)
   7. Print PATH warning if $bin_root NOT in PATH

 test-install.sh (CI smoke):
   1. tempdir = $(mktemp -d)
   2. dotnet publish flow-cli -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
   3. ./install.sh --install-root $tempdir --local-tarball ./publish-output/
   4. PATH=$tempdir/bin:$PATH; flow version; flow check; flow render
   5. Cleanup tempdir
```

### Recommended Project Structure

```
flow-cli/
├── flow-cli.csproj            # references flow-lang, flow-midi, flow-interpreter
├── Program.cs                 # System.CommandLine RootCommand wiring
├── Commands/
│   ├── RunCommand.cs          # flow run script.flow
│   ├── EvalCommand.cs         # flow eval "code"
│   ├── ReplCommand.cs         # flow repl
│   ├── WatchCommand.cs        # flow watch script.flow
│   ├── PlayCommand.cs         # flow play script.flow
│   ├── RenderCommand.cs       # flow render script.flow -o out.wav
│   ├── Flow2MidiCommand.cs    # flow flow2midi script.flow -o out.mid
│   ├── Midi2FlowCommand.cs    # flow midi2flow in.mid -o out.flow
│   ├── CheckCommand.cs        # flow check script.flow (syntax only)
│   ├── VersionCommand.cs      # flow version
│   └── NewCommand.cs          # flow new my-piece (scaffold)
├── Config/
│   ├── FlowConfig.cs          # Tomlyn read + POCO + static singleton (lives in flow-cli)
│   └── FlowConfigPoco.cs      # [DataContract]-style POCO for TomlMapping
└── Scaffold/
    ├── Templates/
    │   └── default.flow       # Embedded resource — the "new my-piece" output
    └── ScaffoldEmitter.cs

scripts/
├── install.sh                 # POSIX bash, ~150-200 lines
├── test-install.sh            # POSIX bash, ~80-100 lines
└── publish.sh                 # CI helper: dotnet publish → tar.gz (~30 lines)

flow-lang.Tests/
├── Integration/
│   └── Phase30/
│       └── Midi2FlowRoundTripTests.cs
└── Fixtures/
    └── midi/
        ├── README.md          # Provenance + licensing
        ├── LICENSE.txt        # CC0 1.0
        ├── ragtime_q_ee.mid       # Quarter + eighth-eighth rhythm pattern
        ├── two_voice_counterpoint.mid  # RH+LH style
        └── drum_loop.mid      # Simple 4-on-the-floor for channel 9 path
```

### Pattern 1: Static-Singleton Config Population

**What:** Config loaded once at CLI launcher startup, stored on a static class, read by FlowEngine via static property access. No threading of config through 11 subcommands × N call frames.

**When to use:** Read-mostly app-wide configuration that changes only at process boundary.

**Example:**

```csharp
// flow-cli/Config/FlowConfig.cs
public static class FlowConfig
{
    public static FlowConfigPoco Active { get; private set; } = FlowConfigPoco.Defaults;

    public static void LoadFromXdg()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "flow", "config.toml");
        if (!File.Exists(path)) return; // silent fallback per SPEC-4
        try
        {
            var text = File.ReadAllText(path);
            var model = Toml.ToModel<FlowConfigPoco>(text);
            Active = model;
        }
        catch (TomlException ex)
        {
            Console.Error.WriteLine($"Warning: {path} could not be parsed: {ex.Message}");
            Console.Error.WriteLine("Falling back to defaults.");
        }
    }
}

public record FlowConfigPoco
{
    public string? InstallPath { get; init; }
    public string? DefaultAudioDevice { get; init; }
    public int? DefaultTempo { get; init; }
    public string? DefaultTimesig { get; init; }
    public List<string>? StdlibSearchPath { get; init; }

    public static FlowConfigPoco Defaults => new()
    {
        InstallPath = null,
        DefaultAudioDevice = null,
        DefaultTempo = null,    // null = use existing baked-in default (120)
        DefaultTimesig = null,  // null = use existing baked-in default (4/4)
        StdlibSearchPath = null
    };
}
```

[CITED: github.com/xoofx/Tomlyn — `Toml.ToModel<T>` is the public API for POCO mapping]

**Why this beats env-vars:** Env vars require a flow-lang ↔ env-var-name contract that needs documenting; static singleton has compile-time discoverability via Go-to-Definition.

**Why this beats argv plumbing:** 11 commands × 5 keys = 55 wiring points if every command takes 5 optional flags. Maintenance burden. With static singleton, adding a 6th config key is one POCO field + one `if (FlowConfig.Active.NewKey is not null) ...` site.

**Engine read site (in flow-lang):**

```csharp
// flow-lang/Runtime/MusicalContext.cs — when no `tempo` block is active
public double EffectiveTempo => Tempo
    ?? FlowConfig.Active?.DefaultTempo   // type FlowConfigPoco lives in flow-cli;
    ?? 120.0;                            // engine reads via interface or weak ref...
```

**Note on layering:** flow-lang must NOT reference flow-cli (circular). Two options:
1. Move `FlowConfig` into `flow-lang/Runtime/FlowConfig.cs`, populate it from flow-cli at startup
2. Define an `IFlowDefaults` interface in flow-lang; flow-cli implements it and sets a static reference

**Recommended:** Option 1 (move FlowConfig into flow-lang). It's a runtime concern, not a CLI concern. The Tomlyn DEPENDENCY stays in flow-cli; FlowConfig.Active is just a POCO struct in flow-lang.

### Pattern 2: System.CommandLine Root + Subcommand Registration

**Example (sketch):**

```csharp
// flow-cli/Program.cs
using System.CommandLine;

class Program
{
    static async Task<int> Main(string[] args)
    {
        FlowConfig.LoadFromXdg();

        var root = new RootCommand("Flow — a programming language for music");
        root.Subcommands.Add(RunCommand.Build());
        root.Subcommands.Add(EvalCommand.Build());
        root.Subcommands.Add(ReplCommand.Build());
        root.Subcommands.Add(WatchCommand.Build());
        root.Subcommands.Add(PlayCommand.Build());
        root.Subcommands.Add(RenderCommand.Build());
        root.Subcommands.Add(Flow2MidiCommand.Build());
        root.Subcommands.Add(Midi2FlowCommand.Build());
        root.Subcommands.Add(CheckCommand.Build());
        root.Subcommands.Add(VersionCommand.Build());
        root.Subcommands.Add(NewCommand.Build());

        return await root.Parse(args).InvokeAsync();
    }
}

// flow-cli/Commands/RunCommand.cs
static class RunCommand
{
    public static Command Build()
    {
        var scriptArg  = new Argument<FileInfo>("script") { Description = "Path to .flow script" };
        var deviceOpt  = new Option<string?>("--device") { Description = "PulseAudio device name" };
        var verboseOpt = new Option<bool>("--verbose", "-v");

        var cmd = new Command("run", "Execute a Flow script");
        cmd.Add(scriptArg);
        cmd.Add(deviceOpt);
        cmd.Add(verboseOpt);
        cmd.SetAction(parseResult =>
        {
            var script = parseResult.GetValue(scriptArg)!;
            var device = parseResult.GetValue(deviceOpt);
            var verbose = parseResult.GetValue(verboseOpt);
            return new ScriptRunner().RunScript(script.FullName, device, verbose);
        });
        return cmd;
    }
}
```

[CITED: learn.microsoft.com/en-us/dotnet/standard/commandline — `RootCommand`, `Command`, `Argument`, `Option`, `SetAction` are the post-2.0-stable APIs (the older `Handler.SetHandler` pattern was deprecated during the 2.0 stabilization)]

### Anti-Patterns to Avoid

- **Don't share `FlowEngine` instances across subcommand invocations.** Each subcommand call constructs a fresh engine; that matches the current `flow-interpreter` pattern and the test harness. Sharing would leak audio backend handles.
- **Don't propagate config via env vars.** Polluted process env hurts test isolation and surfaces "what env vars affect Flow?" as an undocumented contract.
- **Don't have install.sh run `dotnet publish`.** Defeats the entire point of self-contained packaging. Install.sh's only job is to fetch + extract + symlink + write default config.
- **Don't put `--device` / `--verbose` only on `flow run`.** They apply equally to `flow play`, `flow render`, `flow watch`. Use a `--device` "global" option pattern or copy onto each audio-using subcommand. (System.CommandLine has shared options; use them.)
- **Don't auto-publish on every `flow` invocation.** The launcher startup cost is the launcher startup cost; Tomlyn read of a 5-key file is ~1 ms. Don't add file-watcher / hot-reload at this layer.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Subcommand routing for 11 commands with `--help`/`--version` | Switch-case dispatch + per-command flag parsing | System.CommandLine 2.0.7 | 11 × ~50 LOC → 11 × ~15 LOC, with free help text generation, suggestion-on-typo, and Bash/Zsh completion scripts via `dotnet completions` |
| TOML parsing | Bespoke string parser | Tomlyn 2.3.2 | TOML 1.0 has 30+ syntax rules; Tomlyn is zero-dep and battle-tested. Hand-roll = bug factory |
| MIDI binary parsing | New parser | Existing `flow-midi/Midi/MidiParser.cs` | Already works (per Bug B "Eliminated" section: tick durations from source are correct) — defect is downstream in Quantizer |
| MIDI binary writing | New writer | Existing `flow-lang/StandardLibrary/Audio/MidiExport.cs` via DryWetMidi | Phase 28 ships this. Don't duplicate |
| Linux audio backend | New audio path | Existing `PulseAudioSimpleBackend.cs` | Already works; just propagate `--device` through CLI |
| `~/.config` directory lookup | Custom platform check | `Environment.GetFolderPath(SpecialFolder.UserProfile)` + `.config/flow/config.toml` | XDG spec compliance on Linux is well-defined. Don't over-engineer (no need for full `xdg-utils` integration this phase) |
| Single-file executable packaging | Custom tar+launcher script | `dotnet publish -p:PublishSingleFile=true --self-contained true -r linux-x64` | One CLI flag does the whole job |
| Idempotent symlink creation | Conditional `ln` + cleanup | `ln -sfn $target $linkpath` (POSIX) | `-s` = symbolic, `-f` = force overwrite, `-n` = treat existing symlink-to-dir as a file. Three flags = full idempotency |
| PATH detection | Custom shell PATH parser | `case ":$PATH:" in *":$bin_dir:"*) ;; *) echo "Warning: $bin_dir not on PATH" ;; esac` | One-liner; works in any POSIX shell |
| Embedded resource files (`flow new` template) | File-system lookups relative to executable | `[EmbeddedResource]` in csproj + `Assembly.GetManifestResourceStream` | Single-file publish bundles these into the binary; no separate template dir to ship |

**Key insight:** The packaging machinery is well-trodden ground. The novel work is REQ-5/6 (midi2flow quality). Don't sink effort into custom CLI infrastructure when System.CommandLine + Tomlyn give us 90% off-the-shelf.

## Runtime State Inventory

> This phase is NEW infrastructure (new `flow-cli` project, new CLI surface) — there is some pre-existing runtime state to migrate. Listing explicitly:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — Flow has no persisted state outside .flow source and .wav/.mid outputs | None |
| Live service config | None — no daemons or background services | None |
| OS-registered state | None pre-Phase-30. After Phase 30: `~/.local/bin/flow` symlink + `~/.local/share/flow/` install dir + `~/.config/flow/config.toml` config file. These are NEW and are what install.sh creates | install.sh must be idempotent (re-runs upgrade in place); `flow new my-piece` warns if directory exists |
| Secrets/env vars | None — Flow has no API keys or credentials | None |
| Build artifacts | Existing `flow-interpreter/bin/`, `flow-midi/bin/`, `flow-lang/bin/`. After Phase 30: new `flow-cli/bin/` and the `publish/` output directory | install.sh consumes a tar.gz built by `scripts/publish.sh`; intermediate `flow-cli/bin/` is dev-only |
| Backward compat | The two existing executables (`flow-interpreter`, `flow-midi`) remain in the solution per SPEC-8. Documentation should treat them as dev entrypoints; `flow` is the user entrypoint | Document; no removal |

**Nothing found in category:** Stored data, Live service config, Secrets/env vars — all confirmed by reading the codebase. Flow is a script-on-disk tool with no daemonized components.

## Bug B Scope Assessment — load-bearing for REQ-5/6

This is the section the planner should read most carefully. The SPEC says:

> "If the existing `FlowGenerator.cs` already does this, no changes needed; if it emits a different style, adjust to flat" (REQ-5)
>
> "most of the logic exists, the work is unification" (Background)

This UNDERSELLS the work. Bug B reveals existing flow-midi output FAILS the SPEC-6 acceptance contract on the ONE fixture the composer has tested (`examples/ragtime.mid`). The defects are not in the output STYLE (which is roughly flat already, with section/Song scaffolding) — they are in the QUANTIZATION ALGORITHM itself. Three concrete defects, all visible in `examples/output/ragtime_imported.flow`:

### Defect 1: Quarter notes rendered as sixteenth-dotted + 5 rests

**Evidence:** First non-rest bar of `track_ch2_rh_seq`:
```
| _ D4s. _ _ _ _ _ _ _ _ _ _ _ _ _ D4s. _ _ _ _ _ |
```

`D4s.` is a dotted-sixteenth = 0.375 × 480 = 180 ticks. A bar of 4/4 at TPQN=480 is 1920 ticks. The bar contains roughly 21 tokens (counting `_`s as separate tokens because Flow's auto-fit rest division splits them). The composer authored a quarter note here; it has become `D4s.` + 5 rests.

**Root cause:** `Quantizer.SnapDurationCapped` (Quantizer.cs:568-602) uses STRICT cap — only grid values where `gridTicks <= capTicks` are eligible. If `availableTicks` (line 424: `Math.Min(nextEventTick, barEnd) - cursor`) is even 1 tick less than 480, the Q-grid (1.0 × 480 = 480) is rejected and the next-best is dotted-eighth (0.75 × 480 = 360) or eighth (0.5 × 480 = 240).

**Why availableTicks drops below 480:** In Format 0 single-track MIDI with the SplitByChannel branch active (line 145), `PairNotes` orders by StartTick. If two channels overlap or if a NoteOn fires 1 tick early or late from the channel-other-than-current, the grouping logic and cursor arithmetic don't perfectly snap to multiples of 480. The bar-fit clamp then strictly under-quantizes.

**Why so many rests:** `AddRests` (Quantizer.cs:604-637) tries to find a duration that "evenly divides the gap" with `count <= 16` and tolerance `tpqn * 0.1`. If the gap is, say, 300 ticks (480 - 180 dotted-sixteenth), 300 / 60 = 5 = exactly 5 thirty-seconds, but the 0.1 × 480 = 48-tick tolerance means many large counts are acceptable. So gap = 300 → 5 rests of 32nd-note duration each.

### Defect 2: Auto-fit collapses multi-duration bars into one auto-fit duration

**Evidence:** `D4s. _ _ _ _ _` — note the `s.` suffix is PRESENT in output. That means `CanAutoFit` (FlowGenerator.cs:239-281) returned `false` for this track. But look at `track_ch1_rh_seq = | _ | _ | _ | _ | _ F5+e _ F5e _ F5+e. _ |`. Eighth notes mixed with dotted-eighths — `CanAutoFit` returns false. Good.

The defect here is different: `_` rests with NO SUFFIX in Flow's NoteStreamCompiler (line 187 above, `auto-fit`) divide remaining bar time EQUALLY among all suffix-less elements. So 4 entire empty bars of `_` is interpreted as 4 whole-bar rests. That's actually CORRECT in isolation (whole-bar rest IS what the empty bar means) — BUT it's wrong here because the composer didn't author 4 bars of silence; the quantizer-side bar-emission logic created those empty bars because no note-onsets fell in the first 4 bars of THIS sequence after RH/LH splitting.

**Root cause:** `QuantizeSpans` (Quantizer.cs:346) computes `totalBars` from `maxTick` GLOBALLY, then emits a bar for every barIdx from 0 to totalBars. Tracks that have no notes early on get `_` placeholder bars. The trailing-rest-trim at the end (line 475) handles the END, but not the START.

### Defect 3: Aggressive RH/LH pitch-split heuristic

**Evidence:** `track_ch2_rh_seq` + `track_ch2_lh_seq` AND `track_ch1_rh_seq` + `track_ch1_lh_seq` — channels 1 and 2 BOTH split into RH/LH by pitch.

**Root cause:** `AddSplitTracks` (Quantizer.cs:201-247) splits any track whose pitch range exceeds 24 semitones (2 octaves). The split point is biased toward MIDI 60 (middle C) if the median pitch is within 12 semitones of it. For ragtime piano (a Format 0 single-track MIDI with channels 1 and 2 representing RH and LH separately), this DOUBLE-SPLITS — first by channel, then again by pitch within each channel. So a 4-track output (`ch1_rh`, `ch1_lh`, `ch2_rh`, `ch2_lh`) emerges from a 2-channel source.

**SPEC-5 says:** "one `Sequence trackN = | ... |` per MIDI track inside a single `section roundtrip { ... }`". The pitch-split heuristic violates this — it creates MORE sequences than tracks/channels.

### Required Rework Scope (PRESCRIPTIVE — for the planner)

Three layers of rework, in dependency order:

**Layer 1: Test infrastructure (Wave 0 for REQ-6)**

Create `flow-midi.Tests/` (a NEW xUnit project — flow-midi has no test project yet, confirmed by `ls`). Add synthetic-fixture tests:
- `QuarterNoteRhythmTest` — 4 quarter notes in 4/4 → assert generated .flow has 4 `q` tokens and zero `_q` rests
- `QuarterEighthEighthTest` — `<Q, E, E>` pattern → assert generated .flow has `q`, `e`, `e` in that order
- `ChordTest` — `[C4 E4 G4]q` source → assert generated .flow has all 3 chord tones preserved
- `RoundTripTest` (the SPEC-6 contract) — uses `MidiExport.WriteMidi` to GENERATE the fixture from authored .flow, then runs midi2flow, then flow2midi, then re-parses both MIDIs via DryWetMidi and asserts note-count + pitch + duration match

**Layer 2: Quantizer algorithm rework**

- **Remove the pitch-split RH/LH heuristic entirely** (Quantizer.cs:201-247 `AddSplitTracks`). SPEC-5 says one Sequence per MIDI track; just emit one Sequence per channel (Format 0) or per track (Format 1+). The composer-authored RH/LH ASSIGNMENT in the MIDI is the channel/track assignment; flow-midi should respect that, not heuristically re-derive it.
- **Add proper quarter-note snapping:** when `availableTicks >= 480 - tolerance` (e.g. 470), allow the Q grid to win. Replace strict `gridTicks <= capTicks` with `gridTicks <= capTicks + tolerance` where tolerance = small fraction of TPQN (e.g. `tpqn / 32` = 15 ticks at TPQN=480). The bar-fit invariant can tolerate this because the trailing-rest-fill (line 463-468) absorbs over-shoots ≤ tolerance.
- **Fix the AddRests over-emission:** when the remaining gap is `< tpqn / 8` (< 32nd note), emit ONE rest with the right suffix, not 5+ thirty-second rests. The "auto-fit rests divide remainder equally" downstream behavior is correct in NoteStreamCompiler; the bug is on the EMIT side emitting too many `_` placeholders.
- **Fix the leading-empty-bar emission:** in `QuantizeSpans`, find each track's FIRST and LAST note onset; only emit bars from firstNoteOnset's bar index to lastNoteOnset's bar index. Trailing-trim already exists; add leading-trim.

**Layer 3: FlowGenerator emit adjustments**

- **Match SPEC-5 output structure exactly:** one section `roundtrip`, one Sequence per track named `track1`, `track2`, ... (or use MIDI track name if present and sanitizable). The current output uses `track_ch2_rh_seq` etc.; SPEC asks for flat track-numeric naming.
- **Emit `Song s = [roundtrip]` and DROP the `(play output)` tail.** The current generator emits `(play output)` (line 123, FlowGenerator.cs); for `midi2flow` output, we want a render-or-not-decided file. The composer's reproduction in Bug B even had to `sed` this line away. Better: emit `Buffer output = (renderSong s "piano")` + `(writeWav "output.wav" output)` — match the typical UAT pattern, but document.
- **Drop the auto-fit suffix-elision** when round-trip fidelity matters. Auto-fit produces ambiguous output (depends on bar size); explicit durations on every note are unambiguous. The CanAutoFit optimization saves a few chars in ergonomic mode but loses round-trip determinism. Add a `--explicit-durations` flag, default ON for midi2flow.

**Scope summary:** Layer 1 = 1 new test project + 3-4 test files. Layer 2 = ~150 LOC delta in Quantizer.cs (removals net positive — `AddSplitTracks` is a 47-line method that goes away). Layer 3 = ~30 LOC delta in FlowGenerator.cs. Plus the SPEC-6 round-trip test in `flow-lang.Tests/Integration/Phase30/`.

**Honest assessment for planner:** This is at least 4 plan units, not 1. Suggested decomposition:

| Plan | Title | Layer |
|------|-------|-------|
| midi-test-infra | Create flow-midi.Tests project + synthetic-fixture suite | 1 |
| quantizer-rewrite | Remove RH/LH heuristic, fix Q-snapping, fix rest emission, fix leading bars | 2 |
| flowgen-format | Emit flat structure per SPEC-5; drop auto-fit for round-trip fidelity | 3 |
| roundtrip-integration | flow-lang.Tests/Integration/Phase30/Midi2FlowRoundTripTests.cs with 3 fixtures | 1+3 |

## Subcommand Framework Decision

**Recommendation: System.CommandLine 2.0.7 (stable)** [VERIFIED: nuget.org/packages/System.CommandLine]

| Criterion | System.CommandLine 2.0.7 | Hand-rolled (existing flow-interpreter style) |
|-----------|--------------------------|---------------------------------------------|
| Stable on .NET 10 | Yes (2.0.0 stabilized Nov 2025; 2.0.7 is the April 2026 patch) | N/A |
| Per-subcommand LOC | ~15-25 | ~40-60 |
| Total LOC for 11 subcommands | ~200 + central Program.cs ~50 = 250 | ~550 + shared helpers ~50 = 600 |
| Free `--help` per subcommand | Yes | Manual |
| Free `flow --version` | Yes (auto from assembly attrs) | Manual |
| Type-safe arg binding | Yes (`Option<T>`, `Argument<T>`) | Manual parse + try/catch |
| Suggestion on typo (`flwo run` → "Did you mean: flow run?") | Yes | Manual |
| Bash/Zsh completion script generation | Yes via `dotnet completions` | None |
| AOT/trim compatibility | Yes as of 2.0 stable | Trivially yes |
| Self-contained binary size impact | <1 MB | 0 |
| Dependency footprint | One Microsoft-owned NuGet ref | Zero |
| Time-to-implement (11 subcommands) | ~1-1.5 days | ~3-4 days |
| Maintenance burden | Low (Microsoft maintains it) | Higher (we maintain) |

**The "minimal dependencies" principle does NOT veto this.** CLAUDE.md's anti-dependency stance specifically targets NWaves, NAudio, managed-midi — third-party libraries that duplicate hand-rolled functionality. System.CommandLine is:
1. **Microsoft-owned** (same trust level as the runtime)
2. **Stable** (no longer beta as of Nov 2025)
3. **A subcommand framework, not a duplicate of any flow-* logic** (no overlap with NoteStreamCompiler / FlowEngine / etc.)
4. **Size-cheap** vs. the 80+ MB runtime baseline

Rejection of CommandLineParser, Cocona explained in "Alternatives Considered" above.

## TOML Parser Decision

**Recommendation: Tomlyn 2.3.2** [VERIFIED: nuget.org/packages/Tomlyn]

| Criterion | Tomlyn 2.3.2 | Hand-rolled |
|-----------|--------------|-------------|
| Dep count | Zero deps | Zero deps |
| LOC required | ~3 (`Toml.ToModel<FlowConfigPoco>(text)`) | ~80-120 with comment/blank-line/quoted-string handling |
| TOML 1.0/1.1 spec compliance | Full | Limited to our 5 keys |
| Quoted/literal string handling | Yes | Must implement |
| Comments / blank lines | Yes | Must implement |
| Array of strings (for `stdlib_search_path`) | Yes | Must implement (or use colon-separated string per SPEC-4) |
| NativeAOT-ready | Yes (per Tomlyn README) | Yes |
| Error messages with span | Yes (TomlException with line/col) | Manual |
| Maintenance | xoofx, active | We own |
| Size impact | ~200 KB DLL | 0 |
| License | BSD-2-Clause | N/A |

The case for hand-rolling is the same "minimal deps" principle — but Tomlyn has ZERO deps itself, is small, is NativeAOT-compatible, and gives us proper error messages out of the box. The 80-120 line hand-rolled version's edge cases (string escapes, multi-line strings, key naming rules) are exactly the bugs that get discovered AFTER a user files an issue.

**Take Tomlyn.**

## dotnet publish Profile

**Recommended invocation:**

```bash
dotnet publish flow-cli/flow-cli.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:DebugType=embedded \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o publish/flow-linux-x64
```

**Flag-by-flag rationale:**

| Flag | Value | Rationale |
|------|-------|-----------|
| `-c Release` | — | Standard for distribution |
| `-r linux-x64` | — | SPEC-2 locks platform; constraint says Linux x64 only |
| `--self-contained true` | — | SPEC-2 requires "no .NET runtime install on target" |
| `-p:PublishSingleFile=true` | — | SPEC-2 explicitly requires single binary |
| `-p:PublishTrimmed=false` | — | SPEC-2 locks this OFF. Confirmed correct: flow-lang uses reflection in `OverloadResolver`, dynamic invocation in `InternalFunctionRegistry`, and `Assembly.GetManifestResourceStream`-style loading for `.flow` stdlib. PartialTrim or aggressive trim WOULD prune used types in these paths [ASSUMED — would need explicit trim audit to verify which symbols get pruned; for SPEC compliance, keep trimming off] |
| `-p:DebugType=embedded` | — | Embeds PDB into the binary rather than emitting a sibling `.pdb`. For a tool we ship, we want stack traces with line numbers if a user reports a crash. Cost: ~3-5 MB. Alternative: `none` (smaller, but no line numbers in stack traces) |
| `-p:IncludeNativeLibrariesForSelfExtract=true` | — | Bundles libcoreclr.so etc. into the single binary; .NET extracts them to `~/.net/` on first run. Without this, native libs sit alongside the binary as separate `.so` files — defeats the "single file" promise. With it, true single-file [CITED: docs.microsoft.com/dotnet/core/deploying/single-file/overview] |
| `-p:EnableCompressionInSingleFile=true` | — | Reduces published size ~30-40%. Cost: cold-start time penalty (a few hundred ms for first run as .NET decompresses). For a CLI tool launched occasionally, this is acceptable. NOTE: do NOT combine with `PublishReadyToRun=true` — known regression per dotnet/runtime#101866 [CITED: github.com/dotnet/runtime/issues/101866] |

**Expected size:** Baseline self-contained .NET 10 hello-world is ~60-100 MB before compression, ~35-55 MB compressed. Add DryWetMidi (~3 MB), Tomlyn (~200 KB), System.CommandLine (~1 MB), flow-lang code (~2 MB), stdlib .flow files (~50 KB), Phase 29 samples (≤ 5 MB). Estimated total: **~50-75 MB compressed, well under the 120 MB SPEC-2 cap.** [VERIFIED: Microsoft Learn docs and Stacklesson; ASSUMED for the exact number — verify by running the published build]

**Trimming addendum:** If a future phase wants to push under 30 MB, investigate `PublishTrimmed=true` with `TrimMode=partial` and `TrimmerRootDescriptor` files annotating reflection-targeted types in flow-lang. NOT in scope for Phase 30.

## Install Script Pattern

**Recommendation: prebuilt-tarball model, no .NET SDK dependency on user side.**

### Architecture

```
[Developer]                    [GitHub Release]              [End User]
dotnet publish        →        flow-v0.1.0-linux-x64.tar.gz
                                          ↓
                                          ↓ curl
                                          ↓
                                ./install.sh
                                  ├─ extract → ~/.local/share/flow/flow-v0.1.0/
                                  ├─ symlink ~/.local/bin/flow → ../share/flow/flow-v0.1.0/flow
                                  ├─ write ~/.config/flow/config.toml (if absent)
                                  └─ PATH warning if needed
```

### install.sh skeleton (POSIX bash, target ~150 lines)

```bash
#!/usr/bin/env bash
set -euo pipefail

VERSION="${FLOW_VERSION:-0.1.0}"
TARBALL_URL="${FLOW_TARBALL_URL:-https://github.com/noahfreelove/flow-sharp/releases/download/v${VERSION}/flow-v${VERSION}-linux-x64.tar.gz}"
SYSTEM_INSTALL=0
LOCAL_TARBALL=""
INSTALL_ROOT=""  # override for tests

while [[ $# -gt 0 ]]; do
  case "$1" in
    --system) SYSTEM_INSTALL=1; shift ;;
    --local-tarball) LOCAL_TARBALL="$2"; shift 2 ;;
    --install-root) INSTALL_ROOT="$2"; shift 2 ;;
    --help|-h) print_usage; exit 0 ;;
    *) echo "Unknown flag: $1"; exit 1 ;;
  esac
done

if [[ $SYSTEM_INSTALL -eq 1 ]]; then
  SHARE_ROOT="${INSTALL_ROOT:-/usr/local/share/flow}"
  BIN_ROOT="${INSTALL_ROOT:+$INSTALL_ROOT/bin}"
  BIN_ROOT="${BIN_ROOT:-/usr/local/bin}"
  CONFIG_ROOT="${HOME}/.config/flow"  # config stays per-user even on system install
else
  SHARE_ROOT="${INSTALL_ROOT:-$HOME/.local/share/flow}"
  BIN_ROOT="${INSTALL_ROOT:+$INSTALL_ROOT/bin}"
  BIN_ROOT="${BIN_ROOT:-$HOME/.local/bin}"
  CONFIG_ROOT="${HOME}/.config/flow"
fi

mkdir -p "$SHARE_ROOT" "$BIN_ROOT" "$CONFIG_ROOT"

# Fetch tarball
if [[ -n "$LOCAL_TARBALL" ]]; then
  cp "$LOCAL_TARBALL" "/tmp/flow-install.tar.gz"
else
  command -v curl >/dev/null || { echo "Error: curl required"; exit 1; }
  curl -fsSL "$TARBALL_URL" -o "/tmp/flow-install.tar.gz"
fi

# Extract (idempotent — version-stamped dir)
tar -xzf /tmp/flow-install.tar.gz -C "$SHARE_ROOT"
rm /tmp/flow-install.tar.gz

# Idempotent symlink
ln -sfn "$SHARE_ROOT/flow-v$VERSION/flow" "$BIN_ROOT/flow"

# Default config if absent
if [[ ! -f "$CONFIG_ROOT/config.toml" ]]; then
  cat > "$CONFIG_ROOT/config.toml" <<EOF
# Flow default config — auto-generated by install.sh
install_path = "$SHARE_ROOT/flow-v$VERSION"
# Uncomment and set values to override built-in defaults:
# default_audio_device = "alsa_output.usb-..."
# default_tempo = 120
# default_timesig = "4/4"
# stdlib_search_path = ["/usr/share/my-flow-modules"]
EOF
fi

# PATH check
case ":$PATH:" in
  *":$BIN_ROOT:"*) ;;
  *)
    echo "Warning: $BIN_ROOT is not on your PATH."
    if [[ $SYSTEM_INSTALL -eq 0 ]]; then
      echo "Add this to your ~/.bashrc or ~/.zshrc:"
      echo "  export PATH=\"\$HOME/.local/bin:\$PATH\""
    fi
    ;;
esac

echo "Installed flow v$VERSION to $SHARE_ROOT/flow-v$VERSION"
echo "Symlinked $BIN_ROOT/flow → $SHARE_ROOT/flow-v$VERSION/flow"
echo "Run: flow version"
```

### Why prebuilt-tarball, not SDK-on-user

| Aspect | Tarball (recommended) | SDK-from-source |
|--------|------------------------|------------------|
| User dependency | curl, tar, bash | full .NET 10 SDK + git |
| Install time | ~5-15s (download + extract) | ~2-5 min (restore + build + publish) |
| Reproducibility | Bytewise identical across users | Depends on user's NuGet cache + SDK version |
| Defeats self-contained promise? | No | Yes (user needs SDK to install a "no-runtime-required" binary) |
| Cold-system test (test-install.sh) | Works on minimal Linux | Requires SDK in CI |

### Idempotency

- `ln -sfn` overwrites existing symlink (the `-f` is the magic; `-n` ensures it doesn't recurse into an existing symlinked dir)
- `mkdir -p` is idempotent
- Tarball extraction into a version-stamped dir (`flow-v0.1.0/`) means re-install just overwrites the same version, or creates a new version dir alongside
- Default config write is gated on `[[ ! -f $CONFIG_ROOT/config.toml ]]` — never overwrites user-customized config

### PATH detection

The POSIX-safe pattern: `case ":$PATH:" in *":$bin:"*) ;; *) warn ;; esac`. Surrounding both sides with `:` colons avoids prefix/suffix false matches.

## Config Propagation Mechanism

**Recommendation: Static singleton populated at CLI startup, lives in flow-lang for engine access.**

### Three options compared

| Approach | Code complexity | Test isolation | Discoverability | Layering |
|----------|----------------|----------------|-----------------|----------|
| Env vars | Low to set, high to enumerate | Bad — pollutes process env | Bad — no compile-time list | Clean (no flow-lang change) |
| Argv plumbing | 11 cmds × 5 keys = 55 wiring sites | Excellent | Good — every flag is explicit | Clean |
| **Static singleton** | Low — one read site per key in flow-lang | Good with explicit reset in test fixtures | Excellent — IDE Go-to-Definition | Requires FlowConfig in flow-lang |

**Static singleton wins because:**
- Adding a config key is +1 POCO field + +1 read site (vs. +11 argv sites for argv approach)
- Test isolation is `FlowConfig.Active = FlowConfigPoco.Defaults` at test setup (provide static reset method)
- flow-lang-housing of FlowConfig keeps the dependency direction clean: flow-cli depends on flow-lang (already does), flow-lang doesn't depend on flow-cli

### Implementation

```csharp
// flow-lang/Runtime/FlowConfig.cs  (NEW)
namespace FlowLang.Runtime;

public record FlowConfigPoco
{
    public string? InstallPath { get; init; }
    public string? DefaultAudioDevice { get; init; }
    public int? DefaultTempo { get; init; }
    public string? DefaultTimesig { get; init; }
    public List<string>? StdlibSearchPath { get; init; }

    public static FlowConfigPoco Defaults { get; } = new();
}

public static class FlowConfig
{
    public static FlowConfigPoco Active { get; set; } = FlowConfigPoco.Defaults;

    public static void Reset() => Active = FlowConfigPoco.Defaults;  // for tests
}
```

```csharp
// flow-cli/Config/FlowConfigLoader.cs  (NEW)
using FlowLang.Runtime;
using Tomlyn;

public static class FlowConfigLoader
{
    public static void LoadFromXdg()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "flow", "config.toml");
        if (!File.Exists(path)) return;
        try
        {
            FlowConfig.Active = Toml.ToModel<FlowConfigPoco>(File.ReadAllText(path));
        }
        catch (TomlException ex)
        {
            Console.Error.WriteLine($"Warning: {path}: {ex.Message}");
        }
    }
}
```

```csharp
// flow-lang/Runtime/MusicalContext.cs  (modify read site)
public int EffectiveTempo => Tempo ?? FlowConfig.Active.DefaultTempo ?? 120;
```

**SPEC-4 acceptance verification:** Integration test loads `config.toml` with `default_tempo = 100`, then runs a tempo-less script through `FlowEngine.Execute`, then asserts the rendered audio has the right BPM (via FFT or directly via `MusicalContext.EffectiveTempo`).

## Public-Domain MIDI Fixtures

**Recommendation: hand-author 3 synthetic fixtures, generated FROM .flow source via `flow flow2midi` (Phase 28 MidiExport).**

### Why NOT IMSLP or Mutopia

- IMSLP MIDIs of Bach Inventions are CC-BY-NC-SA 3.0 [VERIFIED: imslp.org page for BWV 772]. The "NC" (non-commercial) clause makes them unsuitable for a project that may be redistributed via any commercial channel (NuGet, future App Store, etc.). The "SA" share-alike clause requires us to license derivatives under CC-BY-NC-SA, which conflicts with the project's likely MIT-style license.
- Mutopia Project files are a mix — some CC-BY-SA, some CC-BY, some Public Domain. Per-file license check required [VERIFIED: mutopiaproject.org/legal.html]. Maple Leaf Rag on Mutopia is in the public domain (Joplin died 1917; composition is PD) but the MIDI rendering of the score by a Mutopia contributor may have a different license per file.
- For ragtime specifically: `examples/ragtime.mid` is committed at the repo root and is the composer's authored source. **Its license should be confirmed**: if it's the composer's own arrangement of a public-domain score, declare it CC0. Don't co-opt it as the round-trip fixture without explicit licensing.

### Recommended approach

Hand-author 3 small .flow files, generate the .mid fixtures via the Phase 28 MidiExport. This:
- Gives us provably-CC0 fixtures (we authored them)
- Lets us target SPECIFIC quantization edge cases the algorithm needs to pass
- Provides bidirectional provenance: `fixture.flow → fixture.mid → out.flow → out2.mid` — Layer 4 of round-trip
- Keeps file sizes small (<2 KB each)

### Three fixtures

| Fixture | What it tests | Source pattern |
|---------|--------------|----------------|
| `ragtime_q_ee.mid` | Quarter + eighth-eighth rhythm (Bug B's failing pattern) | `\| C4q E4e G4e \| C4q E4e G4e \| × 4 bars` |
| `two_voice_counterpoint.mid` | Two voices in same channel, RH/LH-style without explicit pitch split | Bach-like: two voice blocks `\| {voice C4q D4q E4q F4q} {voice C5q D5q E5q F5q} \|` (Phase 28 voice-block polyphony) |
| `drum_loop.mid` | Channel 9 GM percussion, simple beat | `\| drumKick drumSnare drumKick drumSnare \|` on channel 9 |

Each .flow source is committed alongside the .mid fixture; the test setup can either consume the .mid directly OR regenerate it from .flow (more robust if MidiExport ever changes).

### Provenance file

`flow-lang.Tests/Fixtures/midi/README.md`:

```markdown
# MIDI Round-Trip Fixtures

All fixtures in this directory are authored by the Flow project and released
under Creative Commons CC0 1.0 Universal (Public Domain Dedication).

| File | Source | What it tests |
| ---- | ------ | ------------- |
| ragtime_q_ee.mid | ragtime_q_ee.flow + writeMidi | Q+E+E quantization (Bug B regression) |
| two_voice_counterpoint.mid | two_voice_counterpoint.flow + writeMidi | Voice-block polyphony round-trip |
| drum_loop.mid | drum_loop.flow + writeMidi | Channel 9 percussion routing |
```

## `flow new` Scaffold Template

**Recommendation: single-file scaffold, ~30 lines, embedded as resource.**

A multi-file scaffold introduces directory-structure questions that don't yet have answers in Flow (no module manifests, no `flow.json`-equivalent, no per-project stdlib search path config — those are out of scope per SPEC).

`flow new my-piece` creates `./my-piece.flow` (or `./my-piece/my-piece.flow` if `--dir` is passed) with:

```
// my-piece.flow — generated by `flow new`
//
// Run with:    flow run my-piece.flow
// Render to:   flow render my-piece.flow -o my-piece.wav
// Play live:   flow play my-piece.flow

use "@std"
use "@audio"
use "@notation"

tempo 120 {
    timesig 4/4 {
        key Cmajor {

            section main {
                Sequence melody = | C4q D4q E4q F4q | G4q F4q E4q D4q | C4w |
            }

            Song song = [main]
            Buffer output = (renderSong song "piano")
            (writeWav "my-piece.wav" output)
        }
    }
}
```

Three reasons single-file beats multi-file scaffold:

1. **Discoverability:** the user sees the entire Flow surface they need in one file
2. **Friction:** no need to `cd` into a directory
3. **Composer ergonomics > engineering elegance** (CLAUDE.md core principle)

The `(writeWav ...)` line gives `flow render` SOMETHING to do, and the comment block at the top documents the 3 main subcommands.

For `flow new my-piece --dir`, scaffold:

```
my-piece/
├── my-piece.flow      # same as above
└── README.md          # 3-line intro
```

But default to single-file. The `--dir` flag is a future enhancement; can be deferred.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| System.CommandLine beta with `IConsole` + `SetHandler` | Stable 2.0 with `SetAction(parseResult => ...)` | Nov 2025 stable, 2.0.7 April 2026 | Cleaner API; deprecated handler pattern still works but isn't idiomatic |
| Self-contained needed extra `.so` files alongside binary | `IncludeNativeLibrariesForSelfExtract=true` bundles everything | .NET 6+ | True single-file possible |
| Trimming + reflection-using code was a minefield | `TrimMode=partial` + descriptor files made trimming usable for some apps | .NET 7+ | Still risky for flow-lang's overload resolver; leave OFF per SPEC |
| `Pidgin` parser combinator in flow-lang.csproj | Hand-rolled SimpleLexer/Parser is the active parser | Existing — Pidgin unused | Remove from csproj? Out of scope for Phase 30 — note for cleanup |

**Deprecated/outdated:**
- `System.CommandLine` `SetHandler(...)` pattern: still works but no longer the idiom. Use `SetAction(parseResult => ...)` instead.
- `PublishReadyToRun=true` combined with compression: known regression — don't combine.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Phase 30 self-contained build will fit in ~50-75 MB compressed | dotnet publish profile | If wrong (e.g. comes out at 100+ MB), SPEC-2's 120 MB cap is still satisfied; only need replanning if it exceeds 120 MB |
| A2 | `PublishTrimmed=true` would prune reflection-targeted types in flow-lang's OverloadResolver and InternalFunctionRegistry | dotnet publish profile | Validates SPEC-2's decision to keep trimming OFF; if we ever want a smaller binary, this needs a trim audit |
| A3 | `examples/ragtime.mid` license is unclear and shouldn't be the official SPEC-6 fixture | Public-domain MIDI fixtures | If composer confirms it's CC0/PD-authored, we could use it instead of `ragtime_q_ee.mid` |
| A4 | The composer hears 5 distinct attacks per quarter-note bar (the "grace-note" percept) because of leading-eighth-rest + dotted-sixteenth + multiple thirty-second rests | Bug B Defect 1 | Verified by reading the generated .flow output (`_ D4s. _ _ _ _ _` pattern). MEDIUM confidence (no actual A/B listen test done) |
| A5 | Removing `AddSplitTracks` (RH/LH pitch heuristic) won't break existing flow-midi UAT cases | Bug B Layer 2 | Should be verified by running existing UAT before the change. If a composer relies on pitch-split for some piano MIDI, the SPEC-5 "one Sequence per track" target overrides; that's a SPEC decision |
| A6 | Tomlyn's `Toml.ToModel<T>` POCO mapping handles all 5 of our config keys (string, int, string-list) | TOML parser decision | LOW risk — Tomlyn explicitly supports POCO mapping in its main API. Worst case: 1-line fix to use `Toml.Parse(text).ToTable()` and read keys manually |
| A7 | `~/.local/bin` is on PATH for systemd-based distros from ~2020 onward | install.sh | SPEC explicitly lists this assumption. Mitigation already in place: warn-if-missing |
| A8 | Including `Phase 29 samples` in the publish-output directory will cost ≤5 MB | dotnet publish profile size | SPEC says Phase 29 already bundles ≤5 MB; trust the spec |

## Open Questions

1. **What's the license of `examples/ragtime.mid`?**
   - What we know: It's committed at the repo root and was generated by some external process (presumably the composer's authoring tool)
   - What's unclear: Is it the composer's own authored arrangement of public-domain ragtime material? Or a downloaded MIDI?
   - Recommendation: Plan-phase should ask the composer. Don't use it as the SPEC-6 fixture unless it can be declared CC0 with confidence; hand-author the 3 fixtures instead

2. **Should `flow check` validate types or just syntax?**
   - What we know: SPEC says `flow check script.flow exits 0` for valid input
   - What's unclear: Does "valid" mean parseable, or successfully type-checked, or successfully type-checked AND ran without runtime errors?
   - Recommendation: parse-only (syntax) for v1. Type-check needs a typecheck-without-execute mode in FlowEngine that doesn't exist yet — plan-phase decision

3. **What's the canonical version number for Phase 30 (`flow version`)?**
   - What we know: SPEC says "semver string matching the current release tag"
   - What's unclear: Is this Phase 30 = v0.1.0, or does the project have a different versioning scheme?
   - Recommendation: plan-phase locks the version; for now `flow version` returns `Assembly.GetExecutingAssembly().GetName().Version` which is driven by `flow-cli.csproj`'s `<Version>` property

4. **What should `flow watch` do for non-.flow file changes (e.g. imported `.flow` module changes)?**
   - What we know: existing `LiveReloadManager` watches the entry script
   - What's unclear: should we watch imported modules too?
   - Recommendation: out of scope for Phase 30; existing behavior preserved

5. **Should the `--system` install affect `~/.config/flow/config.toml` placement?**
   - What we know: SPEC says install location toggles via `--system`; config location not addressed
   - What's unclear: should system install write `/etc/flow/config.toml`?
   - Recommendation: per-user config always (`~/.config/flow/config.toml`), even with `--system` install. System config (`/etc/flow/config.toml`) is a future enhancement. Document this

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | dotnet build, dotnet publish, all xUnit tests | ✓ | 10.0.107 | — |
| PulseAudio | `flow play` runtime (NOT install time) | ✗ on dev box | — | `flow render`/`flow flow2midi`/`flow midi2flow` work without; `flow play` warns and exits |
| ffmpeg | none (Flow uses hand-rolled WAV write) | ✗ | — | Not needed |
| bash 4+ | install.sh, test-install.sh | ✓ (default on all Linux) | typically 5.x | POSIX-compatible script works on bash 3.2+ |
| curl | install.sh tarball fetch | ✓ on most distros | — | wget fallback could be added; ~5 LOC |
| tar | install.sh extraction | ✓ (universally available) | — | — |
| git | not used by install pipeline | — | — | — |

**Missing dependencies with no fallback:**
- None — `flow play` gracefully degrades without PulseAudio (existing behavior; the playback manager already handles this)

**Missing dependencies with fallback:**
- PulseAudio absence at dev-box test time means we cannot end-to-end test `flow play` from CI. Per SPEC ACK list, this is a "manual UAT, not CI-able" check.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 + Microsoft.NET.Test.Sdk 17.13.0 (existing in flow-lang.Tests) |
| Config file | flow-lang.Tests/flow-lang.Tests.csproj (existing) |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase30" --logger "console;verbosity=minimal"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| REQ-1 | All 11 subcommands exit 0 on valid input | Integration | `dotnet test flow-lang.Tests --filter "Phase30.SubcommandSmoke"` | ❌ Wave 0 |
| REQ-2 | Publish produces self-contained binary | Build smoke | `bash scripts/test-publish.sh` | ❌ Wave 0 |
| REQ-3 | install.sh installs to ~/.local/share/flow without sudo | Integration | `bash scripts/test-install.sh` | ❌ Wave 0 |
| REQ-3 | --system install requires sudo | Manual UAT (sudo behavior) | manual | n/a |
| REQ-4 | config.toml values reflect in interpreter behavior | Integration | `dotnet test flow-lang.Tests --filter "Phase30.ConfigPropagation"` | ❌ Wave 0 |
| REQ-5 | midi2flow emits parseable Flow source | Integration | `dotnet test flow-lang.Tests --filter "Phase30.Midi2FlowParseable"` | ❌ Wave 0 |
| REQ-5 | Output structure: one Sequence per track, single section, single Song | Unit | `dotnet test flow-midi.Tests --filter "FlowGenerator.EmitsOneSequencePerTrack"` | ❌ Wave 0 (entire flow-midi.Tests project missing) |
| REQ-6 | Round-trip note-count + pitch + duration match, ±1 tick tolerance, 3 fixtures | Integration | `dotnet test flow-lang.Tests --filter "Phase30.Midi2FlowRoundTrip"` | ❌ Wave 0 |
| REQ-6 (subtest) | Quarter notes preserved (Bug B regression) | Unit (synthetic fixture) | `dotnet test flow-midi.Tests --filter "Quantizer.QuarterNoteRhythm"` | ❌ Wave 0 |
| REQ-6 (subtest) | Q+E+E pattern preserved (Bug B regression) | Unit (synthetic fixture) | `dotnet test flow-midi.Tests --filter "Quantizer.QuarterEighthEighthPattern"` | ❌ Wave 0 |
| REQ-6 (subtest) | Chord notes preserved (Bug B regression) | Unit (synthetic fixture) | `dotnet test flow-midi.Tests --filter "Quantizer.ChordRoundTrip"` | ❌ Wave 0 |
| REQ-7 | test-install.sh exits 0 | Bash smoke | `bash scripts/test-install.sh` | ❌ Wave 0 |
| REQ-8 | dotnet run --project flow-interpreter still works for 4 Phase 27 fixtures | Smoke | manual or bash | ⚠ existing scripts may cover |

### Sampling Rate

- **Per task commit:** `dotnet test flow-lang.Tests --filter "Phase30"` (subset, fast)
- **Per wave merge:** `dotnet test` (full suite — must stay GREEN per ACK list)
- **Phase gate:** Full suite + `bash scripts/test-install.sh` exit 0 before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `flow-midi.Tests/` — entirely new xUnit project (mirrors flow-lang.Tests setup)
- [ ] `flow-midi.Tests/flow-midi.Tests.csproj` — xunit.v3 + ProjectReference to flow-midi
- [ ] `flow-midi.Tests/Quantizer/QuantizerTests.cs` — synthetic-fixture tests for Bug B regression
- [ ] `flow-midi.Tests/Conversion/FlowGeneratorTests.cs` — output-shape tests
- [ ] `flow-lang.Tests/Integration/Phase30/Midi2FlowRoundTripTests.cs` — REQ-6 acceptance test
- [ ] `flow-lang.Tests/Integration/Phase30/SubcommandSmokeTests.cs` — REQ-1 acceptance
- [ ] `flow-lang.Tests/Integration/Phase30/ConfigPropagationTests.cs` — REQ-4 acceptance
- [ ] `flow-lang.Tests/Fixtures/midi/{ragtime_q_ee,two_voice_counterpoint,drum_loop}.mid` + source `.flow` + LICENSE + README
- [ ] `scripts/test-publish.sh` — smoke test that `dotnet publish` succeeds and du ≤120MB

## Security Domain

Phase 30 introduces minor security surface increases. ASVS review:

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | n/a — Flow has no user accounts |
| V3 Session Management | no | n/a |
| V4 Access Control | yes | install.sh respects user perms; doesn't escalate without --system flag |
| V5 Input Validation | yes | TOML config parsing (Tomlyn handles), MIDI binary parsing (existing MidiParser, malformed-file resistant per try/catch in Program.cs:93) |
| V6 Cryptography | no | n/a — no secrets, no auth tokens |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed MIDI causes panic / OOM during midi2flow | Denial-of-Service | Existing MidiParser uses `FormatException`/`NotSupportedException` for header validation; wrap subcommand in try/catch (already done at Program.cs:93) |
| Malformed config.toml crashes flow on startup | Denial-of-Service | Tomlyn raises TomlException; wrap and warn, fall back to defaults per SPEC-4 "Missing config file is a silent fallback to baked-in defaults" — extend to ALL parse failures |
| install.sh downloads tampered tarball | Tampering | v1: trust GitHub HTTPS. v1.5+: add SHA256 verification (`shasum -a 256 -c flow.tar.gz.sha256`). Plan-phase decision |
| install.sh writes to user-controlled paths during --system | Elevation of Privilege | Symlink + extract paths are hardcoded constants in install.sh, not user-controlled. Sudo is required for --system; user reads the script before running it (recommended practice for any curl-pipe-bash) |
| Stdlib_search_path config allows arbitrary path traversal | Information Disclosure | ModuleLoader.ResolvePath uses Path.GetFullPath which CAN reach anywhere on the filesystem. ALREADY the behavior for relative imports; config-path traversal is no worse |

The script-distributed-via-curl-pipe-bash pattern has well-known risks. Phase 30's install.sh is downloaded explicitly and runs locally — same model as rustup, nvm, etc. Future: offer a GPG-signed release artifact, but out of scope for v1.

## Project Constraints (from CLAUDE.md)

These directives shape Phase 30's design:

- **Minimal dependencies.** This research recommends 2 new dependencies (System.CommandLine 2.0.7, Tomlyn 2.3.2). Both are zero-or-Microsoft-owned-deps and address infrastructure that we'd otherwise hand-roll badly. **Verify with composer that 2 new deps is acceptable.**
- **Pre-public.** Breaking changes can land in one commit. So if flow-midi rework necessitates a CLI flag rename or output structure change, no deprecation period needed.
- **Charitable interpretation.** Missing config.toml → silent fallback. Missing audio device → warning, not error. Malformed config.toml → warning + defaults. This research's recommendations align.
- **Genre-agnostic.** The 3 round-trip fixtures span ragtime, counterpoint, and percussion — three distinct genres.
- **Ergonomics first.** `flow run script.flow` MUST be shorter than `dotnet run --project flow-interpreter script.flow`. The CLI accomplishes this.
- **Music > rigid correctness.** Quantizer rework should not OVER-correct. If a source MIDI has 1-tick drift between two notes that musically are a chord, group them as a chord (existing GroupSimultaneous tolerance of 10 ticks is right).
- **Pidgin parser combinator still referenced but unused** (flow-lang.csproj). Note for future cleanup; not Phase 30 scope.

## Sources

### Primary (HIGH confidence)
- `flow-midi/Conversion/Quantizer.cs` — DurationGrid contents, SnapDurationCapped strict-cap logic, AddSplitTracks heuristic, GroupSimultaneous tolerance, AddRests over-emission, leading-bar emission [VERIFIED: code read]
- `flow-midi/Conversion/FlowGenerator.cs` — emit structure (section + Song wrappers + auto-fit) [VERIFIED: code read]
- `flow-midi/Midi/MidiParser.cs` — confirmed correct per Bug B "Eliminated" [VERIFIED: code read]
- `flow-interpreter/Program.cs` — existing hand-rolled arg-parse style [VERIFIED: code read]
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` — Phase 28 MIDI write surface [VERIFIED: header read]
- `flow-lang/Runtime/ModuleLoader.cs` — ResolveStdlibPath uses Assembly.Location [VERIFIED: code read]
- `flow-lang/flow-lang.csproj` — DryWetMidi 8.0.3, Pidgin 3.5.1 [VERIFIED]
- `examples/output/ragtime_imported.flow` — actual broken output [VERIFIED: 374 lines read]
- `.planning/debug/midi-import-quarter-quantize.md` — composer-authored evidence [VERIFIED: full read]
- `.planning/phases/30-flow-cli-formal-install/30-SPEC.md` — locked SPEC [VERIFIED: full read]
- `dotnet --version` → 10.0.107 [VERIFIED on dev box]
- [NuGet Gallery: System.CommandLine](https://www.nuget.org/packages/System.CommandLine) — stable 2.0.7 (April 2026), 3.0.0-preview.3 available

### Secondary (MEDIUM confidence)
- [NuGet Gallery: Tomlyn](https://www.nuget.org/packages/Tomlyn) — version 2.3.2 stable, 0 deps, NativeAOT ready
- [Microsoft Docs: Create a single file for application deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [Microsoft Docs: System.CommandLine overview](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
- [System.CommandLine 2.0 beta5 roadmap](https://github.com/dotnet/command-line-api/issues/2576)
- [Mutopia Project license](https://www.mutopiaproject.org/legal.html) — mixed CC-BY-SA/CC-BY/PD
- [IMSLP Bach BWV 772 page](https://imslp.org/wiki/Invention_in_C_major,_BWV_772_(Bach,_Johann_Sebastian)) — MIDI files CC-BY-NC-SA 3.0
- [How to write idempotent Bash scripts](https://arslan.io/2019/07/03/how-to-write-idempotent-bash-scripts/) — ln -sfn pattern

### Tertiary (LOW confidence)
- [Tomlyn xoofx GitHub](https://github.com/xoofx/Tomlyn) — used for ToModel<T> API confirmation
- [PublishReadyToRun + EnableCompressionInSingleFile regression](https://github.com/dotnet/runtime/issues/101866) — known issue to avoid

## Metadata

**Confidence breakdown:**
- Standard stack (System.CommandLine, Tomlyn versions): HIGH — verified on NuGet pages April 2026
- Architecture (CLI structure, project layout): HIGH — follows established .NET CLI patterns and existing flow-* conventions
- Bug B scope (Quantizer rework): MEDIUM-HIGH — code-read evidence is strong (ragtime_imported.flow output confirms the algorithmic defects), but no synthetic-fixture test has been RUN to verify the proposed fixes actually fix the symptoms. The planner should treat the proposed Layer 2 changes as "best-guess root cause, validated by reading"
- Install script pattern: HIGH — well-known idempotent shell idioms, supported by web search
- TOML/MIDI fixture provenance: MEDIUM — license analysis based on linked pages; per-file license verification needed before commit
- Self-contained binary size estimate (~50-75 MB): MEDIUM — based on .NET ecosystem norms; actual measurement needed after first `dotnet publish` run

**Research date:** 2026-05-10
**Valid until:** 2026-06-10 (System.CommandLine release cadence is monthly; Tomlyn is stable; .NET 10 is LTS)

---

*Phase: 30-flow-cli-formal-install*
*Next step: /gsd-discuss-phase 30 — 4 deferred decisions (subcommand framework, TOML parser, config propagation, install.sh dependency); confirm Bug B scope expansion before plan-phase.*
