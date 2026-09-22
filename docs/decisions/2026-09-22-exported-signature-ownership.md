# Exported signature ownership

Status: accepted for restructuring Phase 1 (2026-09-22). Needed by Phases 3–4.

## Context

A native builtin currently needs two hand-maintained declarations. The C#
`registry.Register(name, FunctionSignature, impl)` call supplies the implementation,
parameter types and names. A matching `internal proc` line in a stdlib `.flow`
module makes it callable after `use` (549 `internal proc` lines today). A C#
overload without a `.flow` line is unreachable, and neither side is checked against
the other.

`BuiltInFunctions.RegisterSignaturesOnly` already produces the full signature set
without implementations. The LSP and `StdlibAuditor` use it. Flow-authored procs
(stdlib helpers, improv packs, user modules) are declared only in source.

## Decision

1. **Native builtins: the C# registration is authoritative.** Its `FunctionSignature`
   (name, parameter types, parameter names, varargs, return type when known) becomes
   the module descriptor's signature. In Phase 3 each registration also names its
   owning module and required capabilities.
2. **`.flow` `internal proc` lines become a derived export list.** First step (Phase 3):
   a test fails when an `internal proc` has no matching registered signature, or
   when a registered signature is not exported by any module and is not on an
   explicit "internal only" list. Second step: generate those lines, or replace them
   with descriptor-driven exports. Either way, one catalog.
3. **Flow-authored procs: the source is authoritative.** Analysis discovers their
   signatures by parsing, never by executing module bodies.
4. **Export visibility is per module.** Only procs a module declares or re-exports
   become visible through `use`, as today. Qualified access uses the module's
   `module NAME` identity.
5. **Tools read metadata without executing.** The LSP, documentation generator and
   analyzer consume descriptors and parsed source. They never construct an engine,
   audio manager or sample cache to learn signatures.

## Consequences

- Adding a builtin becomes one C# registration plus a module assignment. This
  retires the recurring "forgot the `.flow` surface" failure mode.
- Descriptor identity must be stable across sessions for LSP caching and future
  project files.

## Verification to add

- A consistency test over `RegisterSignaturesOnly` and all shipped `.flow` modules,
  with an explicit allowlist for known intentional differences.
- A metadata-only test: an LSP completion and hover run without constructing
  `FlowEngine`, `AudioPlaybackManager`, `SampleCache` or `SfzSampleCache`.
