# Flow restructuring handoff — 2026-09-24

Phases 0–2 are complete. Phase 3 (language-only runtime and pure standard library)
is **most of the way done**: the language is a separate, BCL-only assembly, the
music grammar is bound through an extension seam, and the standard library is split
into `@core` and `@std` with no implicit prelude. Three pieces of work and the
Phase 3 gate record remain (see "Next work").

The last full verification ran on 2026-09-23 against the final code (details
below). Writing this handoff did not rerun the suites.

## Read first

- [Roadmap](../2026-09-20-flow-restructuring-roadmap.md): §4 target architecture, §5.2 stdlib split, §10 Phase 3 gate.
- [Progress ledger](../progress/flow-restructuring.md): the "Phase 3 in progress" section at the bottom lists every slice (A–F2) with commits and verification.
- [Dependency direction decision](../../decisions/2026-09-22-dependency-direction.md): the "Classification updates" section explains every Phase 3 seam.
- [Semantic fixes](../../decisions/2026-09-22-phase1-semantic-fixes.md): Phase 1 fixes plus language fixes found during Phase 3.
- [Session lifetime](../../decisions/2026-09-22-session-lifetime.md) and the [Phase 2 record](../../baselines/phase2/README.md).
- `docs/TESTING.md`: tiers, snapshot regeneration, verifier usage.
- `CLAUDE.md`: updated project map (`flow-language/` vs `flow-lang/`), "Music grammar is bound" and "No implicit prelude" design notes, `@core`/`@std` module list.

## Repository and commit state

Workspace `/home/noah/Desktop/projects/flow-sharp`, branch `dev`, HEAD `2997ef2`.
**The working tree is clean and everything is committed. Nothing has been pushed.**

Commits since the Phase 0→1 handoff, oldest first:

| Commits | Work |
| --- | --- |
| `ad45aaf` `6a1f133` `03d5a18` `324c0f3` | Phase 1: language contracts, non-musical examples, dependency ratchet, minimal host, decision records |
| `0fc99b8` `dbd76d9` `522d6e8` | Phase 1 findings fixed (semantic fixes, OSC test race) |
| `eb2ec6c` `6745994` `5d3b30b` | Phase 2: engine sessions, cancellation/budgets, latest-request coordinator, process worker, fresh-clone verification |
| `94792af` `933f2d4` | Characterization suite (and the five language fixes it surfaced) |
| `ecd9db4` | Phase 3 A: syntax-level type names, `TypeCatalog`, `FlowLang.Syntax` |
| `59c05d5` | B + D1: unit-type traits; music factories to `MusicValue`; `ValueConversions` |
| `689b9dd` | D2: music session state to `FlowLang.Music` (`MusicSession`, scope slots, session extensions) |
| `3d64844` | Note-stream/progression compilers to `FlowLang.Music` |
| `241322e` | D3: interpreter evaluates music syntax through `DomainBindings` (edges → 0) |
| `1f57a18` | Fix: parameterized sections kept no note streams from nested blocks |
| `304f9d8` | E1: `BuildTarget`, `ValueComparisons`, `ValueFormatter` — language closure music-free |
| `8457c1b` | E2: `flow-language.dll` split out (127 files moved with history) |
| `81ff669` | F1: `@core` / `@std` split |
| `2997ef2` | F2: implicit `@std` prelude removed; lazy style packs |

Standing guidance from the owner: commit and update the progress ledger after
each piece of work; stage explicit paths (never `git add .`); don't push; don't
hand-edit the frozen `flow-runtime.js`; fix language issues found along the way,
in commits separate from extraction work; commit messages end with the
`Co-Authored-By` line.

## Owner decisions made this session (2026-09-23)

- **`@core` is the essential library; `@std` keeps the non-essential material and
  imports `@core`.** New general-purpose builtins go in `@core`.
- **No implicit prelude.** The bare language is meant to be nearly useless; scripts
  import what they use. The REPL, `flow -e`, `flow eval` and `flow doc` examples
  import `@std` for the user (`FlowEngine.ImportInteractiveDefaults()`); scripts
  (`flow run`, piped stdin, browser `RunFromJs`) do not. Every music module imports
  `@std`, so a script with one music import is unchanged.

Both are recorded in memory, CLAUDE.md, the wiki (`wiki/Standard-Library.md`) and
the contracts `modules.core-library` / `modules.no-prelude`.

## Architecture as it stands

- **`flow-language/` (`flow-language.dll`)**: lexer, parser, AST, type system, values,
  interpreter, diagnostics. It references only the BCL, and `LanguageClosureTests`
  enforce that.
- **`flow-lang/`**: the music library and music host (`FlowEngine`); references
  `flow-language`. Public API and namespaces are unchanged.
- **Extension seams the music layer plugs into** (all in `flow-language`):

| Seam | Purpose |
| --- | --- |
| `TypeCatalog` | Binds grammar type names to types. Music types register from `MusicTypeCatalog`'s module initializer. |
| `DomainBindings` (`ExecutionContext.Bindings`) | Evaluates the music grammar: expression/statement evaluators by node type, plus literal parsers, constants, member resolvers, defaults, declaration observers, patterns. `MusicBindings.Create()` supplies them; `FlowEngine` installs them. A context without bindings reports "<construct> is not available". |
| `StackFrame.GetScope/SetScope` + `ScopeVersion` | Per-frame scope state; `MusicalContext` lives here. |
| `ExecutionContext.GetExtension<T>` + `ISessionExtension` | Per-context state, snapshotted for tests; `MusicSession` lives here. |
| `ValueConversions`, `ValueComparisons`, `FlowType.Format` / `ValueFormatter` | Domain conversions, comparisons and formatting. |
| `BuildTarget` | Web/Desktop compile constants; `FlowEngine.IsWebTarget` delegates to it. |

- C# 14 extension members (`FlowLang.Music.MusicContextExtensions`) keep the old call
  shapes, e.g. `ctx.GetMusicalContext()` and `frame.MusicalContext`.
- The ratchet baseline `docs/baselines/phase1/language-dependency-edges.json` is
  **empty**. `FlowLang.Music` is a forbidden namespace for language code.

## Verification and caveats

Last full run (2026-09-23, final code): `scripts/ci/verify.py --tier all` gave the
results below, with **zero tracked-content changes**.

| Suite | Passed | Failed |
| --- | ---: | ---: |
| Core + long + platform | 2,896 | 0 |
| MIDI | 21 | 0 |

The 19 skips are unchanged in kind: Web-only facts in the Desktop runner, MIDI and
CoreAudio prerequisites, and `mscore`.

- The Web build succeeds.
- `dotnet publish flow-lang -p:FlowTarget=Web -c Release` bundles
  `flow-language.wasm`.
- The published AppBundle was booted under **Node** through `flow-runtime.js`
  (verified after E2) and ran a music script correctly.
- Smoke-tested by hand: `flow run` without imports fails on `print`; `-e` works;
  `jam` loads styles lazily.

Things that bit me, which the next session should know:

- **Stale test DLLs.** After builds driven by my using-fixer script, `dotnet test --no-build` sometimes ran an old `flow-lang.Tests` binary. Run `dotnet build flow-lang.Tests` explicitly before regenerating baselines or snapshots. The verifier always builds, so its results are trustworthy.
- **Snapshots.** Regenerate with `FLOW_UPDATE_SNAPSHOTS=1` and the dependency ratchet with `FLOW_UPDATE_DEPENDENCY_BASELINE=1`. The AST and corpus file lists come from `git ls-files`, so **stage new `.flow` files before regenerating**, and commit the new `Snapshots/ast/*.txt` files; I missed two and had to amend.
- **Public API gate.** Removals need an entry in `Snapshots/api/allowed-removals.txt`. The failure message no longer truncates at 40 entries; the old cap had hidden 72 removals, now allow-listed. The surface spans both assemblies.
- **`.gitignore`** ignores `*.flow`, `*.md` and `tests/` globally. Stdlib `.flow` files are now allow-listed (`!flow-lang/*.flow`, `!flow-language/*.flow`). Tracked files under `tests/` need `git add -f`.
- **Corpus test.** It pins microphone capture to silence at 44.1 kHz; the host's mic rate had changed a diagnostic.
- **Test assembly initializer.** `flow-lang.Tests/MusicLibraryInitializer.cs` runs the music library's initializer up front. Without it, a parse-only test could see an empty type catalog.
- **Browser bundle.** The committed `flow-site/static/wasm` bundle is **stale**: it predates every Phase 3 change, including the prelude removal. The playground changes only after `bash flow-site/scripts/sync-runtime.sh` and a commit, and after that playground code needs a `use` line. The bundled examples already have one.
- **Web-only facts** (`[FlowTargetFact("Web")]`) still skip; nothing runs a Web-flavored test build.

## Next work: finish Phase 3

The Phase 3 gate: non-musical CLI/REPL/embedding examples run from a language-only
artifact; the dependency closure excludes music assets and native audio packages;
the compatibility host still runs the music corpus. The last two already hold. The
first needs:

1. **Move `@core`'s C# implementations into `flow-language`.** Today every builtin is registered from `flow-lang/StandardLibrary/BuiltInFunctions.cs` (`RegisterStdLib`, `RegisterMath`, `RegisterCollections`, `RegisterContextDependentFunctions`, `RegisterDict`, plus `StdLib.cs`, `Collections.cs`, `ConversionFunctions.cs`, `UnitArithmetic.cs`). These mix core and music overloads. Split them into a `CoreLibrary.Register(registry[, context])` in `flow-language`, and a music registration in `flow-lang` for the music-typed overloads (`str(Semitone)`, `mul(Millisecond, Double)`, …).
   - Use `core.flow`'s 121 declarations and `collections.flow` as the checklist: every declaration there needs an implementation in `flow-language`.
   - Keep overload declaration order stable: the resolver breaks ties by order. The overload and value characterization snapshots will catch drift.
   - `RegisterSignaturesOnly` (used by the LSP) must keep covering everything.
2. **Move `core.flow` and `collections.flow` into `flow-language/`.** Give them `CopyToOutputDirectory`/`CopyToPublishDirectory`, and embed them for Web with the `FlowLang.Stdlib.<name>` logical name. `ModuleLoader` already searches every loaded assembly for embedded modules. Update `scripts/publish.sh` `STDLIB_FILES` and the Phase 48 embed tests.
3. **Build a language-only host.** For example, a small `scripts/LanguageHost` project, or a flow-interpreter profile flag. It references only `flow-language`, installs no bindings, registers `CoreLibrary`, and runs `examples/language/*.flow`: they already import only `@core`, and their `.out` files are the expected output. Add a test that runs them through that host. Record loaded assemblies and native libraries; the Phase 1/2 `MinimalHost` shows how.
4. **Optional for the gate:** move the music-shaped type descriptors still in `flow-language/TypeSystem/PrimitiveTypes` (`BufferType`, `EnvelopeType`, `OscillatorStateType`, `VoiceType`, `TrackType`) and the Markov/L-system model data in `Runtime` to the music layer. They carry no music dependencies today, but they are music concepts. Update the WASM trim roots if they move.
5. **Close the phase.**
   - Write `docs/baselines/phase3/README.md`: what moved, the gate evidence, the language-only host's closure.
   - Do a fresh-clone verification, as in Phase 2.
   - Update the ledger's "Current milestone" header, CLAUDE.md and `docs/ARCHITECTURE.md`.
   - Refresh the MinimalHost baseline (engine construction now loads zero modules).

## Loose ends noticed, not fixed

- `ValueFormatter.Number` uses `ToString("G10")` with the current culture. A comma-decimal locale may print `1,5`. Unverified, and pre-existing.
- IL2075 trim warning in `ExpressionEvaluator.EvaluateMemberAccess`: the reflection member fallback. Pre-existing.
- `UnitArithmetic.TryAsSeconds` is now unused (public API; remove with an allow-list entry).
- Skipping a middle named argument (`(jam over=x seed=1)`) doesn't resolve. This is documented as v1.6 backlog.

### Suggested resume prompt

> Continue the Flow restructuring roadmap: finish Phase 3. Read
> `docs/plans/handoffs/2026-09-24-phase3-in-progress.md` and the progress ledger
> first. Move `@core`'s C# implementations and `core.flow`/`collections.flow` into
> `flow-language`, build a language-only host that runs `examples/language/`,
> verify the Phase 3 gate, write the Phase 3 baseline record, and update and
> commit the ledger after each slice.
