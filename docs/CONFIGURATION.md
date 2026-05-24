<!-- generated-by: gsd-doc-writer -->
# Configuration

Flow's runtime configuration lives in three places: a single TOML file at
`~/.config/flow/config.toml` (read at CLI startup), file-scope `enable <pragma>;`
declarations inside individual `.flow` source files, and a small set of
environment variables / runtime built-ins. This document is the source of truth
for all three.

## Config file: `~/.config/flow/config.toml`

- **Loader:** [`flow-cli/Config/FlowConfigLoader.cs`](../flow-cli/Config/FlowConfigLoader.cs) (Tomlyn 2.3.2)
- **POCO mapping:** [`flow-lang/Runtime/FlowConfig.cs`](../flow-lang/Runtime/FlowConfig.cs) (`FlowConfigPoco`)
- **Path:** `$HOME/.config/flow/config.toml` — **hard-coded**.
  `$XDG_CONFIG_HOME` is intentionally NOT honored, per Phase 30 SPEC-4
  ([`FlowConfigLoader.cs:40-47`](../flow-cli/Config/FlowConfigLoader.cs)). The
  goal is a single, predictable install-doc path. If you need a non-default
  location, symlink it.

The loader runs once at CLI startup (before any `FlowEngine` is constructed) and
populates the static `FlowConfig.Active` singleton that `flow-lang` reads
through.

### Charitable load policy

The loader never aborts the CLI on config-file issues:

| Situation | Behavior |
|-----------|----------|
| File missing | Silent fallback to `FlowConfigPoco.Defaults` (all keys null, all baked-in defaults active). |
| Malformed TOML | Single `Warning: ... could not be parsed: <reason>` line to stderr, then fall back to defaults. CLI continues. |
| IO error | Same as malformed — stderr warning + defaults. |

Source: [`FlowConfigLoader.cs:56-77`](../flow-cli/Config/FlowConfigLoader.cs).
The contract mirrors the project-wide "charitable interpretation" principle —
the composer should never be locked out of their tools because a config file
got corrupted.

TOML keys are `snake_case`; they auto-map to PascalCase POCO properties via
`JsonNamingPolicy.SnakeCaseLower`. No per-property attributes needed.

### Schema (6 keys, all optional)

#### `install_path` (string)

Marker recording where the `flow` binary was installed. Written by
[`scripts/install.sh`](../scripts/install.sh) on first install and never read by
the runtime — its presence in the file just makes the install location visible
to a composer inspecting their config.

```toml
install_path = "/home/composer/.local/share/flow/v1.4.0"
```

#### `default_audio_device` (string)

PulseAudio device name used when the CLI is invoked without `--device`. Consumed
by the `run`, `play`, and `watch` commands via the
`device ??= FlowConfig.Active.DefaultAudioDevice` fallback in
[`flow-cli/Commands/RunCommand.cs`](../flow-cli/Commands/RunCommand.cs),
[`PlayCommand.cs`](../flow-cli/Commands/PlayCommand.cs), and
[`WatchCommand.cs`](../flow-cli/Commands/WatchCommand.cs).

If both `--device` and `default_audio_device` are absent, the backend picks the
PulseAudio default sink.

```toml
default_audio_device = "alsa_output.usb-FocusriteScarlett"
```

List available devices at runtime with the `(audioDevices)` builtin.

#### `default_tempo` (int, BPM)

Three-tier fallback chain for tempo resolution in
[`flow-lang/Runtime/ExecutionContext.cs:455-466`](../flow-lang/Runtime/ExecutionContext.cs):

1. Call-stack-resolved value (active `tempo N { ... }` block).
2. `FlowConfig.Active.DefaultTempo` (this key).
3. Hard-coded `120.0` BPM baked default.

```toml
default_tempo = 90
```

This is **not** advisory — it's the second-tier fallback that the renderer
actually consults when no `tempo` block is active.

#### `default_timesig` (string, `"N/M"`)

Same three-tier chain as `default_tempo`, parsed by `ParseTimesigOrDefault` in
[`ExecutionContext.cs:480-501`](../flow-lang/Runtime/ExecutionContext.cs):

1. Active `timesig N/M { ... }` block.
2. `FlowConfig.Active.DefaultTimesig` (this key).
3. Baked `4/4` default.

Parser is charitable: malformed strings (non-`N/M`, non-positive integers, or a
denominator that isn't a power of 2) trigger **one** stderr warning per process
and fall back to `4/4`. Validation lives at
[`ExecutionContext.cs:484-498`](../flow-lang/Runtime/ExecutionContext.cs).

```toml
default_timesig = "3/4"
```

#### `stdlib_search_path` (list of strings)

Extra search paths consulted by `ModuleLoader` when resolving `use "..."`
imports. Tried **after** the `@stdlib` branch but **before** relative-path
resolution
([`flow-lang/Runtime/ModuleLoader.cs:170-200`](../flow-lang/Runtime/ModuleLoader.cs)).

Resolution order for `use "foo"`:

1. If it starts with `@`, resolve from `AppContext.BaseDirectory` (bundled stdlib).
2. Otherwise, for each path in `stdlib_search_path`: check `<path>/foo.flow`.
3. If absolute, return as-is.
4. If relative, resolve relative to the importing file's directory.

Seeded into the loader's `AdditionalSearchPaths` by `FlowEngine` at construction
time ([`flow-lang/Core/FlowEngine.cs:199-206`](../flow-lang/Core/FlowEngine.cs)).
Empty list = zero-cost no-op for existing scripts.

```toml
stdlib_search_path = ["/usr/share/my-flow-modules", "/home/composer/flow-libs"]
```

#### `sfz_root` (string)

Root directory for the SFZ orchestral sampler library (e.g. VSCO Community CE
1.1.0). When the composer writes `(loadSfz #violin)`, the runtime looks up
`#violin` in the 20-entry GM symbol dict in
[`flow-lang/sfz.flow`](../flow-lang/sfz.flow) (mapping `#violin → "SViolinVib.sfz"`,
`#flute → "FluteSusVib.sfz"`, `#drums → "GM-StylePerc.sfz"`, etc.) and joins
the relative path under `sfz_root`.

Read once per `ExecutionContext` and cached in `ExecutionContext.ResolvedSfzRoot`.
A `null` value combined with a Symbol-form `(loadSfz #x)` raises a
`MissingSfzRootError`. The String-form `(loadSfz "/abs/path.sfz")` bypasses
`sfz_root` entirely and never errors on a missing config key.

```toml
sfz_root = "/home/composer/samples/vsco-ce-1.1.0"
```

### Example complete file

```toml
# Flow default config -- auto-generated by install.sh
install_path = "/home/composer/.local/share/flow/v1.4.0"

default_audio_device = "alsa_output.usb-FocusriteScarlett"
default_tempo = 90
default_timesig = "3/4"
stdlib_search_path = ["/usr/share/my-flow-modules"]
sfz_root = "/home/composer/samples/vsco-ce-1.1.0"
```

## File-scope pragmas (`enable <name>;`)

Pragmas are per-file, **top-of-file only** declarations that toggle parser or
renderer behavior for a single `.flow` source file. They run in a pre-lex
transformation pass ([`flow-lang/Lexing/PragmaScanner.cs`](../flow-lang/Lexing/PragmaScanner.cs))
before `SimpleLexer` sees the file.

### Syntax

```flow
enable hAsB;
enable justIntonation;

# Rest of file follows...
```

A pragma must appear in the **prefix region** of the file — once a non-pragma
statement starts, no more `enable` declarations are accepted (error D-11
"pragma-after-statement").

### Known pragmas

Closed-set registry in
[`flow-lang/Lexing/PragmaRegistry.cs:27-36`](../flow-lang/Lexing/PragmaRegistry.cs):

| Pragma | Effect |
|--------|--------|
| `hAsB` | Inside note streams, accept `H` as a synonym for `B` (German notation). |
| `justIntonation` | 5-limit just-intonation render-time tuning rooted at the active key tonic. |
| `pythagorean` | 3-limit Pythagorean (chain-of-fifths) tuning rooted at the active key tonic. |
| `equalTemperament` | 12-tone equal temperament (default). Explicit form for tooling-visible intent. |
| `scaleLint` | Phase 31 D-03: scale-lint is now default-on; this pragma is accepted as a no-op for backward compatibility. |
| `matchExhaustive` | Phase 35 D-v1.5-05: promote non-exhaustive `match` warnings to errors. |

Unknown pragma names raise error D-12 with a Levenshtein-based did-you-mean
suggestion ([`PragmaRegistry.cs:48-62`](../flow-lang/Lexing/PragmaRegistry.cs))
within `max(2, len/3)` edit distance.

### Module isolation

Pragmas do **not** propagate across `use` imports. Each imported file gets its
own `PragmaSet` computed independently
([`flow-lang/Lexing/PragmaSet.cs:5-14`](../flow-lang/Lexing/PragmaSet.cs),
[`ModuleLoader.cs:77`](../flow-lang/Runtime/ModuleLoader.cs)). A file that
declares `enable matchExhaustive;` does not impose that constraint on the
modules it imports, and vice versa.

This is intentional: pragmas are a per-file authorship choice, not a transitive
build flag.

## Environment variables

Two environment variables are honored by the runtime / tooling:

### `FLOW_SUPPRESS_PLAYBACK=1`

Routes `(play)` and `(loop)` calls to an internal capture buffer instead of
PulseAudio. Auto-enabled by the test harness via a `ModuleInitializer` in
[`flow-lang.Tests/TestAssemblyInit.cs`](../flow-lang.Tests/TestAssemblyInit.cs)
so the suite runs headless. Useful for CI runners without an audio backend.

Implementation: [`flow-lang/Audio/AudioPlaybackManager.cs:18-25`](../flow-lang/Audio/AudioPlaybackManager.cs).

```bash
FLOW_SUPPRESS_PLAYBACK=1 dotnet run --project flow-interpreter examples/showcase.flow
```

Retrieve the captured buffer with `AudioPlaybackManager.GetCapturedBuffer()`
from C# host code.

### `FLOW_LSP_PATH`

Fallback discovery path for the `flow-lsp` binary, used by the JetBrains plugin
when `flow` is not on `PATH`
([`flow-jetbrains/.../FlowLanguageServerFactory.kt:29`](../flow-jetbrains/src/main/kotlin/dev/flowlang/jetbrains/FlowLanguageServerFactory.kt)).

```bash
export FLOW_LSP_PATH=/opt/flow/bin/flow
```

## Audio backend runtime knobs

The audio backend is owned by a per-`FlowEngine` `AudioPlaybackManager`
singleton ([`flow-lang/Audio/AudioPlaybackManager.cs`](../flow-lang/Audio/AudioPlaybackManager.cs)).
Backend auto-detection happens on the first `GetBackend()` call; on Linux,
this currently resolves to `PulseAudioSimpleBackend` (PulseAudio + PipeWire
compatible via the PA compatibility layer). The `IAudioBackend` abstraction
exists for portability, but `PulseAudioSimpleBackend` is the only backend
that ships today — macOS and Windows targets receive the LSP server only,
with no playback path. WAV / MIDI / MusicXML / LilyPond / ABC / MML / SFZ /
Scala / config loading remain fully cross-platform.

### `setMaxVoices(Int max)` builtin

Overrides the per-FlowEngine voice ceiling at runtime
([`flow-lang/StandardLibrary/BuiltInFunctions.cs:977-986`](../flow-lang/StandardLibrary/BuiltInFunctions.cs)).
Default is `32` voices ([`AudioPlaybackManager.cs:31`](../flow-lang/Audio/AudioPlaybackManager.cs)).
Throws if `max < 1`.

```flow
(setMaxVoices 64)
```

This is the runtime equivalent of the `voicePool N { ... }` musical-context
block, but applies globally to the engine rather than scoping to a block. See
the Phase 28 voice-pool allocation notes in `CLAUDE.md` for the
steal-oldest policy that activates when voice count exceeds the pool.

### `--device <name>` CLI flag

Overrides `default_audio_device` per-invocation. Available on `flow run`,
`flow play`, and `flow watch`:

```bash
flow play song.flow --device alsa_output.pci-0000_00_1f.3.analog-stereo
```

Discover device names with the `(audioDevices)` builtin from a `.flow`
script. There is no dedicated CLI subcommand for enumeration — the
PulseAudio Simple API used by `PulseAudioSimpleBackend` does not support
device enumeration, so `(audioDevices)` returns an empty array against
that backend. Use `pactl list short sinks` (or your distro's PulseAudio
front-end) to discover the device-name string to pass via `--device`.

## User style packs (`~/.config/flow/styles/*.flow`)

The `@improv` stdlib loads composer-editable rule packs in two passes
([`flow-lang/StandardLibrary/Improv/StyleRegistry.cs:189-211`](../flow-lang/StandardLibrary/Improv/StyleRegistry.cs)):

1. **Shipped packs** at `{AppContext.BaseDirectory}/improv/styles/*.flow` —
   the bundled `jazz.flow`, `blues.flow`, `classical.flow` baselines.
2. **User packs** at `~/.config/flow/styles/*.flow` — composer additions and
   overrides.

Files in both directories are loaded in deterministic alphabetical order. A
user pack registering the same `#name` as a shipped pack **overrides** the
shipped version (last-write-wins) and emits a one-shot stderr advisory.

Each pack typically contains a single top-level `(registerStyle #name pack)`
call. The pack `Dict` shape (`scale_weights` / `interval_transitions` /
`rhythmic_template` / `articulation_distribution`) is documented at
[`flow-lang/improv/styles/README.md`](../flow-lang/improv/styles/README.md).

Per the threat model, user `.flow` files run with the same privilege as any
other Flow code — the rule-pack convention is documented but not enforced.
Malformed packs emit a one-shot stderr advisory and FlowEngine init continues;
one bad pack will not crash the engine.

Example user override at `~/.config/flow/styles/my-jazz.flow`:

```flow
use "@improv";

(registerStyle #jazz
  (dict
    Note: ... composer's rule pack ...
  ))
```

## Per-environment overrides

Flow doesn't have a built-in concept of environment-tagged config files (no
`config.dev.toml` / `config.prod.toml`). Effective overrides:

- **Per-invocation:** Use CLI flags like `--device` to override
  `default_audio_device` for a single run.
- **Per-file:** Use `enable <pragma>;` declarations at the top of individual
  `.flow` files.
- **Per-environment:** Wrap the `flow` invocation in a shell script that
  exports `FLOW_SUPPRESS_PLAYBACK=1` (for headless CI) or temporarily swaps
  the config file via symlink.
- **Per-call:** Use the `(setMaxVoices N)` builtin inside the `.flow` script
  itself to override the voice ceiling for a specific render.
