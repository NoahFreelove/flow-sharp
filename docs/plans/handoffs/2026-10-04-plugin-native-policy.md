# Plugin native-call policy — 2026-10-04

P7-17 applies a default-deny native invocation surface to declared plugin builds.
Backend readiness and general plugin authoring remain active work.

## Implemented

- `InternalFunctionRegistry` optionally guards each registered native delegate at
  invocation. Declarations/imports remain available; denied calls throw before
  entering the implementation. Captured delegates and `ReplaceAll` replacements
  retain the guard. The original parameterless constructor remains available.
- `EngineOptions.NativeFunctionPolicy` exposes this host boundary without changing
  ordinary engines when no policy is supplied. Policies are per engine/registry,
  not global. Flow code cannot replace the host policy.
- Declared `GeneratorBuildRequest.Plugin` builds populate an exact-signature set
  from `CoreLibrary` (including context callbacks/iteration guard) and detached
  `DawFunctions`/`DawGraphFunctions`. Those families perform in-memory work;
  printing targets the existing null output sink. Registry lookup uses the native
  signature, so a different surface declaration cannot rename a denied function
  into a permitted one.
- Other native families are denied even when a bundled module declares them.
  This includes file export/loading, audio device playback, MIDI/network device
  access and other legacy APIs outside the approved registration families. New
  approved families need explicit inclusion/review in `PluginNativePolicy`.
- The worker installs the policy before source evaluation, covering top-level
  initializers and dependency code as well as the exported builder. Normal
  plugin graph construction and pinned helper modules continue to work.

## Verification

All **343** affected backend/module tests passed, zero failures:
`/tmp/flow-plugin-capabilities-regression.log`.

Seven focused policy/plugin tests passed before the final compatibility constructor
adjustment (included in the regression above):
`/tmp/flow-plugin-capabilities-final.log`. New evidence includes a real isolated
plugin initializer attempting `writeMidi`: explicit native-policy rejection,
no file created, no accepted-state change. Registry tests use a side-effect counter
to verify captured delegates and replacements never enter denied implementations.
A separate unrestricted engine still imports and executes normally.

`git diff --check` passed. Full core/Web/hardware gates were not repeated in this
step; the immediately preceding import-provider change passed the full core gate.

## Scope and next

This is a registered-native-call policy, not an OS security sandbox or memory cap.
Review language-level music hooks and future native registrations when extending
plugin functionality. Ordinary generator requests without a plugin package keep
existing compatibility behavior; do not claim every script/worker is restricted.
Explicit host-resolved asset access and broader offline DSP operations need
capability-aware APIs rather than reopening unrestricted legacy IO.

Next implement composable signal/parameter primitives and public authored plugin
examples, with instrument/event contracts and runtime parameter mapping. State
and tail continuity, MIDI recording and integrated project workflows remain open.
JUI is ready for later frontend work; native UI and historical callback-gap
investigation remain deferred.
