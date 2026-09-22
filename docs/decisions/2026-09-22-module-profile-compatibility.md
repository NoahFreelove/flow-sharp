# Module profiles and compatibility

Status: accepted for restructuring Phase 1 (2026-09-22). Needed by Phases 2–3.

## Context

`new FlowEngine()` registers every builtin (about 600 signatures, most of them
musical), loads `@std` (which imports `@collections` and `@bars`), loads `@improv`
and its three style packs, and publishes static sample caches. It does this even
for a program that only uses collections; see
[the minimal-host record](../baselines/phase1/README.md#minimal-host). Scripts
depend on this implicit environment: `@std` exposes musical conversions and
formatters, and imports execute in the caller's scope.

Four mechanisms must stay independent (roadmap 5.1): installed packages, source
imports, host capabilities, and language compatibility.

## Decision

1. **Two named profiles, chosen by the host.**
   - `legacy` is today's environment exactly: all builtins registered, `@std` with
     its transitive `@collections` + `@bars` exports, shipped improv packs, and
     unqualified exports from every `use`.
   - `language` registers only language and pure standard-library builtins. `@std`
     resolves to a pure standard library without `@bars`. Musical modules are
     absent, and using one is a diagnostic ("module unavailable in the language
     profile"), not a crash.
2. **Unversioned files and every existing entry point use `legacy`.** This covers the
   CLI, REPL, watch mode, LSP, WASM `run` and `FlowEngine` constructors. No
   profile is guessed from filenames, imports or program execution.
3. **Profile is host configuration, not source syntax.** A pragma or `use` cannot
   switch profile or grant a capability. A future Studio project persists its
   profile explicitly.
4. **Capabilities are separate from profiles.** Output, files, audio devices, MIDI,
   network and clocks are host-provided services. The `legacy` profile on the Web
   target still lacks desktop capabilities (the current `IsWebTarget` checks
   become capability checks later).
5. **Existing import names keep working in `legacy`.** Moving an implementation
   between assemblies never changes a user-facing module name. Removing `@bars`
   from `@std` happens only in the `language` profile or through a separately
   announced default change with migration diagnostics.
6. **Module semantics are unchanged in both profiles.** Imports run once per
   session, execute in the caller's scope, are last-import-wins with the shadow
   advisory, and keep qualified `module.proc` access and per-file pragmas. The
   pinned behavior is in `contracts/language/modules/`, including the disputed
   circular-import and pragma-isolation entries.

## Consequences

- Phase 2/3 adds a profile parameter to the session builder while `FlowEngine`
  keeps constructing `legacy`.
- The language profile's `@std` needs its own source (or a descriptor-driven
  export list). Contracts and non-musical examples that avoid music builtins must
  pass under both profiles.

## Verification to add

- `contracts/language/**` except `music/`, and `examples/language/**`, pass under
  `legacy` (today) and under `language` (when it exists).
- A `language`-profile session constructs without loading music modules, style packs,
  sample caches or music/platform assemblies. The minimal-host probe fields become
  assertions.
- `use "@audio"` in the `language` profile yields a single "unavailable in this
  profile" diagnostic with a span.
