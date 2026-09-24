# Phase 3 record: language-only runtime and core library

Recorded 2026-09-24 on Linux, .NET SDK 10.0.112 / runtime 10.0.12.
Implementation revision: `e2bd5f8`.
The [restructuring roadmap](../../plans/2026-09-20-flow-restructuring-roadmap.md)
Phase 3 gate is complete.

## Gate evidence

| Gate | Evidence |
| --- | --- |
| Non-musical CLI, REPL and embedding run from a language-only artifact | `scripts/LanguageHost` references only `flow-language`. `LanguageHostTests` runs all seven `examples/language/` programs in separate processes, checks exact `.out` files, tests a stateful line REPL and injected embedding sinks, and checks absent script prelude/music support. The published Release host also ran all seven from `/tmp`, with exact output and empty stderr. |
| Language dependency closure excludes music assets/native audio packages | `LanguageClosureTests` inspect the assembly's BCL-only references. Host tests inspect output files, loaded assemblies and Linux mapped native objects. Published `.deps.json` contains only the host and `flow-language`, both project entries. No package entries, music assembly, sample assets or native audio libraries. See `language-host.json`, `language-artifact.json` and `language-examples.json`. |
| Compatibility host still runs the music corpus | Unchanged overload, value, module and public API gates; the full corpus passes under the music `FlowEngine`. Full ordinary/long/platform verification passes in both the working checkout and a fresh local clone. |

## Resulting boundary

`flow-language` owns the lexer/parser/AST, type and value machinery, lexical
scopes, interpreter, diagnostics, session contracts, `CoreLibrary`, and
`core.flow` / `collections.flow`. Its project references only the BCL.
`flow-lang` owns music evaluation bindings, music factories and conversions,
musical scopes/session state, audio/platform services and the compatibility host.

The essential `@core` library supplies 121 declarations plus `@collections`.
The compatibility aggregate `@std` imports it and adds music/non-essential
material. Script imports are explicit; interactive music entry points import
`@std`, and the language-only example host imports `@core`. Music style packs
load lazily. Both assemblies copy their own module files and embed them on Web
under stable `FlowLang.Stdlib.<name>.flow` resource names.

Core registration has no music dependency. Internal registration slices let the
music facade preserve the existing interleaving and overload tie order. Existing
public `StdLib`/`Collections` APIs forward to extracted core code while retaining
their music-specific implementations. The music layer provides collection note
ordering through `ValueComparisons.RegisterOrdering`; numeric comparison and
equality remain unchanged.

This session completed F3 (`5bd3f12`, C# implementations), F4 (`66cfc91`, module
assets), and G (`9e80de3`, host/gate). Earlier Phase 3 slices and decisions are in
the [progress ledger](../../plans/progress/flow-restructuring.md).

## Reproduction

```bash
dotnet publish scripts/LanguageHost -c Release -o /tmp/flow-language-host
/tmp/flow-language-host/LanguageHost --json /tmp/closure.json examples/language/collections.flow
dotnet test flow-lang.Tests --filter FullyQualifiedName~LanguageHostTests
python3 scripts/ci/verify.py --tier all --artifacts /tmp/flow-phase3-all

git clone --local --no-hardlinks /path/to/flow-sharp /tmp/flow-phase3-fresh
cd /tmp/flow-phase3-fresh
dotnet publish scripts/LanguageHost -c Release -o /tmp/flow-phase3-fresh-language
python3 scripts/ci/verify.py --tier all --artifacts /tmp/flow-phase3-fresh-results
```

The standalone host publishes before the rest of the fresh clone builds, proving
that no music build is needed. It is framework-dependent: .NET 10 itself is a
prerequisite, not bundled. `language-host.json` lists every loaded assembly and
Linux shared object for a representative run, rather than only the difference
from process startup. Native library discovery is Linux-only.

The refreshed compatibility probe is `minimal-host.json`: engine construction
loads zero modules and zero improv styles. The music host still registers its
music builtins and references music/audio packages by design.

## Verification

| Run | Main suite passed | MIDI passed | Failed | Skipped | Tracked mutations |
| --- | ---: | ---: | ---: | ---: | ---: |
| Working checkout (`9e80de3`) | 2,908 | 21 | 0 | 19 | 0 |
| Fresh clone (`e2bd5f8`) | 2,906 | 21 | 0 | 19 | 0 |

The working checkout discovers two ignored local Flow scripts
(`test_break_builtin.flow`, `test_markov_corpus_array.flow`) absent from a clone;
the tracked suite is identical. Full verifier summaries are committed alongside
this record as `verification-working.json` and `verification-fresh.json`.

The initial overlapping runs exposed a fixed `/tmp/flow_strict_showcase.wav`
fixture collision. Fresh-clone corpus comparison also exposed absolute checkout
paths entering random seeds. The corpus harness now supplies repository-relative
source names; runtime semantics are unchanged, and only three buffer hashes changed
on two-pass regeneration. A later serial run exposed the OSC disposal test asserting
before its background listener connected; the test now waits for connected state and always disposes its
engine on failure. The final fresh clone ran alone and passed all checks.

The trimmed Web publish contains core resources in `flow-language` and music
resources in `flow-lang`. Its AppBundle was booted under Node through the unchanged
`flow-runtime.js`: core reduce/range and music-unit arithmetic printed `10` and
`150ms`, with an empty errors array. It emitted the existing stripped-`loadSfz`
advisory and a Mono symbol-file warning, neither blocking execution.

## Remaining scope

- Music-shaped BCL-only descriptors (`Buffer`, `Envelope`, `OscillatorState`,
  `Voice`, `Track`) and Markov/L-system data containers remain in the language
  assembly. They introduce no music package or native dependency; moving them
  remains optional cleanup for the later music-model extraction.
- The language-only host is an embedding example and line REPL, not a replacement
  for the full CLI/editor. It does not implement a multiline editor or expose the
  full host cancellation/job API.
- Platform prerequisites and Web-only facts still skip in the Desktop runner;
  no hardware listening or cross-browser audio claim is made.
- The freshly published Web artifact is verified; `flow-site/static/wasm` remains
  the older committed bundle. Refreshing it and browser UAT are separate work.
- The pre-existing reflection-fallback IL2075 trim warning remains. No semantic
  language changes were required by this extraction.
