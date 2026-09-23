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

## Known limits

- Music-shaped types that already live in language namespaces are not edges:
  `TypeSystem.PrimitiveTypes` Buffer/Envelope/OscillatorState/Voice/Track types,
  `Runtime.MusicalContext`, `NoteStreamCompiler`, `ProgressionCompiler`,
  and `WasmEntry`. Phase 3 moves them. The ratchet
  then catches references back to them.
- The check is namespace-based within one assembly. After extraction, assembly
  references enforce the same rule, and the Web-target `AssemblyReferenceScanTests`
  remain in force.
