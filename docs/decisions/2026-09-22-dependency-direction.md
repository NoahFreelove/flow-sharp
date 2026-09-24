# Dependency direction

Status: accepted for restructuring Phase 1 (2026-09-22). Enforced from Phase 1 by a ratchet.

## Decision

The logical components and allowed dependencies are those in roadmap section 4.1.
Language never depends on music, audio, IO, platform, UI or host code. Music model
never depends on the language or runtime. Hosts compose components. Until the
assemblies are split, the rule is checked at namespace level inside `flow-lang.dll`.

| Logical component | Namespaces today |
| --- | --- |
| Language | `FlowLang.Lexing`, `Parsing`, `Ast*`, `Interpreter`, `Diagnostics`, `Runtime`, `TypeSystem` (except `SpecialTypes`), `StandardLibrary.Dict` |
| Forbidden from language | `FlowLang.Audio`, `StandardLibrary.{Audio*, Composition, Harmony, Improv, Midi, Network, Notation, Patterns, Transforms, Generative}`, `TypeSystem.SpecialTypes`, and the DryWetMidi, NAudio and Rug.Osc packages |
| Hosting (facade, may depend on everything) | `FlowLang.Core` (`FlowEngine`) |

`FlowLang.StandardLibrary` (root) is left unclassified for now. It holds both
general builtins and the registration hub for every musical module, and gets split
in Phase 3.

## Ratchet

`flow-lang.Tests/Contracts/DependencyDirectionTests.cs` reads `flow-lang.dll` with
Mono.Cecil. It records every edge from a language type (including its
compiler-generated nested types) to a forbidden namespace, whether through base
types, fields, signatures, locals or IL operands. The 38 edges from 19 types on
2026-09-22 are pinned in
[`language-dependency-edges.json`](../baselines/phase1/language-dependency-edges.json).

- A new edge fails the core test tier.
- A removed edge also fails until the baseline is regenerated with
  `FLOW_UPDATE_DEPENDENCY_BASELINE=1`, so the file stays exact and only shrinks.
- Changing the namespace classification is a decision update to this file, not a
  baseline refresh.

## Classification updates

- Phase 3: `FlowLang.Syntax` (grammar tables: note, chord, numeral and type-name
  rules, `Articulation`) is part of the language.
- Phase 3: `FlowLang.Runtime.WasmEntry` is host glue and is excluded by name. It
  cannot move out of `FlowLang.Runtime` because the frozen `flow-runtime.js` binds
  `exports.FlowLang.Runtime.WasmEntry.*` by full name.
- Phase 3: `FlowLang.Music` is a music namespace (forbidden to the language). It
  holds `MusicalContext` and `MusicSession`, the music state that used to live on
  `ExecutionContext` (sections, style packs, SFZ registries, tuning stack
  operations, the memoized context resolution). The language offers two
  domain-neutral seams for it: typed per-frame scope state
  (`StackFrame.GetScope<T>`/`SetScope<T>`, with `ExecutionContext.ScopeVersion`
  for memoization) and per-context extensions (`ExecutionContext.GetExtension<T>`,
  snapshotted for test isolation through `ISessionExtension`). C# 14 extension
  members in `MusicContextExtensions` keep the old call shapes
  (`ctx.GetMusicalContext()`, `frame.MusicalContext`), so an edge to
  `FlowLang.Music` now marks exactly the code that still uses them.
- Phase 3: the interpreter evaluates domain constructs only through
  `FlowLang.Interpreter.DomainBindings` (per context, `ExecutionContext.Bindings`):
  evaluators keyed by expression and statement node type, plus unit/pitch literal
  parsers, identifier constants, member resolvers, declared-type defaults,
  declaration observers and pattern matchers. `MusicBindings` (in `FlowLang.Music`)
  installs the music meaning of the grammar; `FlowEngine`, the music host, installs
  it on every engine. A context without bindings reports each music construct at its
  location (`note stream is not available: ...`) and runs everything else. With this,
  the language namespaces have **no** music or platform edges (baseline empty).
- Phase 3: the language is a separate assembly, `flow-language.dll`
  (`flow-language/`), referenced by `flow-lang` (the music library and host). It
  references only the BCL, so the compiler now enforces the direction.
  `LanguageClosureTests` pin three facts: the assembly's references are BCL-only, it
  contains no music or platform namespaces, and `flow-lang` declares
  language-namespace types only for the host glue in `DependencyDirectionTests.HostTypes`
  (`WasmEntry` and its result types, `FlowConfigLoader`). The public API is
  unchanged: types kept their namespaces, and the API snapshot spans both assemblies.
  Before the split, domain comparisons (`ValueComparisons`), formatting
  (`FlowType.Format`, `ValueFormatter`) and build-target facts (`BuildTarget`) moved
  behind language seams so the language's transitive closure did not reach music.

## Known limits

- Music-shaped types that still live in language namespaces are not edges:
  `TypeSystem.PrimitiveTypes` Buffer/Envelope/OscillatorState/Voice/Track types and
  the Markov/L-system model data in `Runtime`. Phase 3 moves them with the assembly
  split (`MusicalContext` and the note-stream/progression compilers have moved). The ratchet
  then catches references back to them.
- The ratchet itself is namespace-based; the assembly split now backs it with the
  compiler. The Web-target `AssemblyReferenceScanTests` scan both assemblies.
