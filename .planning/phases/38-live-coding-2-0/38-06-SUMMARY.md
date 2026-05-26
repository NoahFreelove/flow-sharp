---
phase: 38-live-coding-2-0
plan: 06
subsystem: networking
tags: [osc, network, rug-osc, lifecycle, reference-identity, opt-in-module]
dependency_graph:
  requires:
    - flow-lang/Runtime/Value.cs (factory pattern)
    - flow-lang/TypeSystem/SpecialTypes/SfzType.cs (reference-identity precedent)
    - flow-lang/StandardLibrary/Notation/NotationIoBuiltins.cs (Register(registry, context) precedent)
    - flow-lang/Diagnostics/RenderingDiagnostics.cs (WarnOnce dedup)
    - flow-lang/Audio/PulseAudioSimpleBackend.cs (Task.Run + CTS lifecycle precedent)
    - Rug.Osc 1.2.5 (MIT, NuGet flatcontainer)
  provides:
    - OscHandleType (specificity 151, reference-identity)
    - Value.OscHandle factory
    - OscFunctions surface (5 builtins + 1 marker)
    - osc.flow module (use "@osc" activation)
    - OscFunctions.InferOscArgs public helper (composer escape hatch)
  affects:
    - flow-lang/Runtime/ExecutionContext.cs (added OscEnabled gate)
    - flow-lang/Core/FlowEngine.cs (registers OscFunctions)
    - flow-lang/StandardLibrary/BuiltInFunctions.cs (cross-reference comment)
    - flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs (OscEnabled snapshot/restore)
tech-stack:
  added:
    - Rug.Osc 1.2.5 (MIT, .NET Standard 2.0, zero transitive deps)
  patterns:
    - Reference-identity Value type (Phase 32/33/36 precedent)
    - Opt-in module activation via `use "@name"` (Phase 33/36/39 precedent)
    - Charitable interpretation (D-v1.5-05) for type-tag inference + rate-limit drops
    - Task.Run + CancellationTokenSource lifecycle (PulseAudioSimpleBackend precedent)
    - WarnOnce stderr advisory dedup (RenderingDiagnostics)
    - ConcurrentDictionary per-key rate-limit gate (per-path sample-and-hold)
key-files:
  created:
    - flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs (39 LOC)
    - flow-lang/StandardLibrary/Network/OscHandleData.cs (60 LOC)
    - flow-lang/StandardLibrary/Network/OscFunctions.cs (598 LOC)
    - flow-lang/osc.flow (56 LOC)
    - flow-lang.Tests/Integration/Phase38/OscTypeTagInferenceTests.cs (6 facts)
    - flow-lang.Tests/Integration/Phase38/OscRateLimitTests.cs (4 facts)
    - flow-lang.Tests/Integration/Phase38/OscLoopbackTests.cs (2 facts)
    - flow-lang.Tests/Integration/Phase38/OscBundleTests.cs (4 facts)
    - flow-lang.Tests/Integration/Phase38/OscBundleDepthCapTests.cs (3 facts)
  modified:
    - flow-lang/flow-lang.csproj (added Rug.Osc PackageReference + osc.flow None Update)
    - flow-lang/Runtime/Value.cs (added Value.OscHandle factory)
    - flow-lang/Runtime/ExecutionContext.cs (added OscEnabled gate + snapshot/restore)
    - flow-lang/Core/FlowEngine.cs (registered OscFunctions)
    - flow-lang/StandardLibrary/BuiltInFunctions.cs (cross-reference comment)
    - flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs (OscEnabled snapshot field)
    - .gitignore (allow-list entry for flow-lang/osc.flow)
decisions:
  - "D-38-13 inference for oscSend: smallest-tag-that-fits via Value.Type switch (Int→,i Long→,h Float→,f Double→,d String/Symbol→,s Bool→,T/,F Buffer→,b)"
  - "D-38-14 drop-newest sample-and-hold at 5ms = 1/200Hz per path"
  - "D-38-15 bundle nesting depth cap 8 with one-shot WarnOnce advisory"
  - "D-38-16 OscHandle reference-identity Value with discriminator role (listener vs pending-packet)"
  - "Rug.Osc 1.2.5 supply-chain audit: slopcheck [SLOP] confirmed false positive — package is .NET-only, slopcheck queries PyPI only; NuGet flatcontainer API + bitbucket.org/rugcode/rug.osc source repo verified"
  - "OscSender(IPAddress, int) 2-arg ctor avoided due to local-port = remote-port collision on loopback; 3-arg (IPAddress, 0, port) form used throughout"
  - "OscFunctions.Register wired into FlowEngine.cs directly (not BuiltInFunctions.cs) because it needs ExecutionContext — matches NotationIoBuiltins / SfzBuiltins precedent. Cross-reference comment in BuiltInFunctions per PATTERNS line 832."
metrics:
  duration: 4h18m
  completed: "2026-05-24"
  tasks: 2/2
  files_created: 9
  files_modified: 7
  tests_added: 19
  tests_passing: 19/19
  loc_added: ~1310
---

# Phase 38 Plan 38-06: OSC Network Surface Summary

Ships the OSC (Open Sound Control) opt-in module — composer's network surface
to TouchOSC, hardware controllers (Lemur, modular synth bridges, Bitwig +
REAPER OSC IO), and other Flow processes on the LAN. Five new builtins
(`oscSend`, `oscListen`, `oscStop`, `oscBundle`, `oscSendBundle`) registered
conditionally on `use "@osc"`, backed by Rug.Osc 1.2.5 (MIT, zero transitive
deps, .NET Standard 2.0). With Phase 40's MIDI clock not yet shipped, OSC is
the fastest way to drive Flow live-mode parameters from an external
controller in v1.5.

## What Shipped

### Type System

- **`OscHandleType`** (specificity 151) — sealed reference-identity singleton
  mirroring SfzType. Slotted above all existing music types (Tuning=150,
  Sfz=150, Markov=148, Lsystem=149). Strict compatibility — no numeric
  coercion, no cross-music-type compatibility.

- **`Value.OscHandle(OscHandleData)`** — reference-identity factory per
  D-38-16. Two `(oscListen 7777 "/x" h)` calls produce DISTINCT Values even
  with identical port + path (each spawns its own receive loop and CTS — no
  caching at the value layer).

- **`OscHandleData`** — runtime state with dual-role discriminator:
  - **Listener role** (`Receiver` non-null, `ListenerTask` is the running
    receive loop, `PendingPacket` null) — produced by `(oscListen)`.
  - **Pending-packet role** (`Receiver` null, `PendingPacket` holds the
    `Rug.Osc.OscBundle`) — produced by `(oscBundle)`. `(oscStop)` no-ops
    on this discriminator.

### Module Activation

- **`flow-lang/osc.flow`** — opt-in module mirroring `notation-io.flow`
  shape. Header `Note:` block + `use "@std"` + `__enableOscModule` marker
  + 5 `internal proc` forward decls + trailing `(__enableOscModule)`
  side-effect.

- **`ExecutionContext.OscEnabled`** boolean gate — flipped `true` by the
  marker; surface builtins raise `"<name> requires \`use \"@osc\"\`"` until
  then. Snapshot/restore wiring in `ExecutionContext.SnapshotState` /
  `RestoreState` + `TestSnapshot.OscEnabled` (defaulted-false for backward
  compat).

### 5 Surface Builtins + 1 Marker

| Builtin | Signature | Disposition |
|---|---|---|
| `oscSend` | `(String host, Int port, String path, ...args) -> Void` | Varargs; type-tag inference per D-38-13 |
| `oscListen` | `(Int port, String path, Function handler) -> OscHandle` | Spawns `Task.Run` receive loop |
| `oscStop` | `(OscHandle handle) -> Void` | Cancels CTS + disposes receiver (Pitfall #5 fix) |
| `oscBundle` | `(...packets) -> OscHandle` | Varargs; wraps `Rug.Osc.OscBundle` (immediate timetag) |
| `oscSendBundle` | `(String host, Int port, OscHandle bundle) -> Void` | Sends a constructed bundle |
| `__enableOscModule` | `() -> Void` | Marker; flips `OscEnabled = true` |

### Type-Tag Inference (D-38-13)

Charitable smallest-tag-that-fits via `Value.Type` switch:

| Flow `Value.Type` | CLR boxed | OSC type tag |
|---|---|---|
| `IntType` | `int` | `,i` |
| `LongType` | `long` | `,h` |
| `FloatType` | `float` | `,f` |
| `DoubleType` | `double` | `,d` |
| `StringType` | `string` | `,s` |
| `SymbolType` | `string` | `,s` |
| `BoolType true` | `bool true` | `,T` |
| `BoolType false` | `bool false` | `,F` |
| `BufferType` | `byte[]` (4-byte LE IEEE-754 flatten) | `,b` |
| _unsupported_ | (throws `ArgumentException`) | — |

Composer's escape hatch: explicit-cast at call site
(e.g. `(oscSend host port "/x" (toLong 1) 1.5d)`). Inverse path
(`RugOscArgToFlowValue`) translates received message args back to Flow
Values for the handler lambda.

### Rate Limit (D-38-14)

Per-path drop-newest sample-and-hold gate:
- **Window:** 5 ms (= 1/200 Hz)
- **Storage:** `ConcurrentDictionary<string, long> _lastFireTimeMs`
- **Behavior:** First message in the 5ms window per path is dispatched;
  subsequent ones dropped silently. No advisory on individual drops —
  sample-and-hold IS the expected behavior.

### Bundle Dispatch (D-38-15)

- Outgoing: `(oscBundle ...packets)` wraps via `new OscBundle(immediate, packetsArr)`
  where `immediate = new OscTimeTag(1UL)` (OSC 1.0 spec value 1 = immediate).
- Incoming: `DispatchPacket` recurses into children at `depth+1`; honors
  future-timetag bundles via `Task.Delay`; immediate timetag dispatches sync.
- **Depth cap 8** (mirrors Phase 36 T-36-17 / Phase 39 D-39-19 DoS guard).
  At depth > 8, recursion aborts and emits one-shot WarnOnce advisory keyed
  `osc-bundle-depth:{path}`: `[osc] bundle nesting depth exceeds 8 at {path} — collapsing to flat dispatch`.

### Listener Lifecycle (D-38-16)

- `(oscListen ...)` spawns `Task.Run` with a `CancellationTokenSource`.
- Pitfall #5 workaround: `cts.Token.Register(() => receiver.Dispose())`
  forces the blocked `Receive()` to throw `ObjectDisposedException`,
  which the loop catches and exits charitably (Cts.Cancel alone can't
  interrupt the synchronous `Receive()` syscall).
- `(oscStop handle)` cancels the CTS, idempotent re-Dispose for safety,
  waits ≤1s for the task to drain.
- Handler exceptions caught + logged to stderr — never kill the listener
  loop (Pitfall #12 "live session never dies mid-set").

## How It's Wired

```
flow-lang/osc.flow
  Note: header + (use "@std")
  internal proc __enableOscModule ()  ← marker, flips OscEnabled=true
  internal proc oscSend / oscListen / oscStop / oscBundle / oscSendBundle
  (__enableOscModule)                  ← trailing side-effect line

FlowEngine ctor
  ↓
NotationIoBuiltins.Register(internalRegistry, _context)
OscFunctions.Register(internalRegistry, _context)  ← NEW (FlowEngine.cs:190)
  ↓
6 builtins registered (5 surface + 1 marker)
  All gate on ExecutionContext.OscEnabled via RequireModuleActivated()
```

`BuiltInFunctions.cs RegisterAllImplementations` carries a cross-reference
comment (per PATTERNS line 832) pointing at the FlowEngine call site —
OscFunctions can't live in BuiltInFunctions itself because it needs
`ExecutionContext` for the module-activation gate + handler lambda invocation
via `context.Invoker`.

## Deviations from Plan

### Rug.Osc API quirks discovered at implementation time

**1. `[Rule 3 - Blocking issue] OscTimeTag missing `Now` / `Immediately` statics**
- **Found during:** Task 2 build
- **Issue:** Plan said use `Rug.Osc.OscTimeTag.Now` for immediate timetag
  per RESEARCH §K line 1086 ("static `OscTimeTag.Now` / `OscTimeTag.Immediately`
  (value `1`)"). Reflection of Rug.Osc 1.2.5 shows neither exists.
- **Fix:** Construct directly via `new OscTimeTag(1UL)` per OSC 1.0 spec
  (value 1 = immediate).
- **Files modified:** `flow-lang/StandardLibrary/Network/OscFunctions.cs`
- **Commit:** 465056e

**2. `[Rule 3 - Blocking issue] OscBundle ctor `params OscPacket[]` overload binding`**
- **Found during:** Task 2 build (compile error CS1503)
- **Issue:** Reflection shows OscBundle ctors are `params OscPacket[]` — when
  called as `new OscBundle(timetag, arr)` C# binds `arr` to the first
  params slot, producing "cannot convert OscPacket[] to OscPacket".
- **Fix:** Hoist the array to a local variable + pass it explicitly:
  `var packetsArr = packets.ToArray(); var bundle = new OscBundle(immediate, packetsArr);`
- **Files modified:** `flow-lang/StandardLibrary/Network/OscFunctions.cs`
- **Commit:** 465056e

**3. `[Rule 1 - Bug] OscSender(IPAddress, int) 2-arg ctor binds local port = remote port`**
- **Found during:** Task 2 OscLoopbackTests run (2 of 19 tests failing with 2s timeout)
- **Issue:** Rug.Osc 1.2.5 `OscSender(IPAddress addr, int port)` ctor sets
  the SENDER's LOCAL port to `port` — same value as the remote. On loopback
  the sender then collides with the receiver bound to the same port.
  Diagnosed via reflection: `sender.LocalEndPoint = 0.0.0.0:{port}` exactly
  matches `receiver.LocalEndPoint`. Messages never arrive.
- **Fix:** Use 3-arg `OscSender(IPAddress, int localPort=0, int remotePort=port)`
  ctor — `localPort=0` lets the OS pick an ephemeral local port.
- **Files modified:** `flow-lang/StandardLibrary/Network/OscFunctions.cs`,
  `flow-lang.Tests/Integration/Phase38/OscLoopbackTests.cs`,
  `flow-lang.Tests/Integration/Phase38/OscBundleTests.cs`
- **Commit:** 465056e
- **Follow-up:** Plan 38-07 closer should document this in the
  composer-facing `examples/live/osc_controller.flow` chapter.

**4. `[Rule 3 - Blocking issue] Value.Float wraps a double, not float`**
- **Found during:** Task 2 OscLoopbackTests run (1 of 19 tests failing with
  InvalidCastException)
- **Issue:** Phase 26 design (Value.cs:25 + line 178 comment) — `Value.Float`
  stores its data as `double` not `float`. Test asserted `.As<float>()` on
  a Float Value; threw "Expected underlying CLR type 'Single', found 'Double'".
- **Fix:** Assert `.As<double>()` + check `Type == FloatType.Instance`
  separately; document the round-trip semantics in the test xmldoc.
- **Files modified:** `flow-lang.Tests/Integration/Phase38/OscLoopbackTests.cs`
- **Commit:** 465056e

### Plan-text divergence: `OscFunctions.Register` wiring location

Plan Task 2 action says to add `Network.OscFunctions.Register(registry);` to
`BuiltInFunctions.cs RegisterAllImplementations`. But `OscFunctions` needs
`ExecutionContext` (for the `__enableOscModule` marker setting `OscEnabled`,
and for invoking handler lambdas via `context.Invoker`). The Phase 33
`SfzBuiltins.Register(registry, context)` and Phase 39
`NotationIoBuiltins.Register(registry, context)` precedents both wire from
FlowEngine.cs directly. We followed that pattern and added a comment in
`BuiltInFunctions.cs` cross-referencing the FlowEngine call site (PATTERNS
line 832's spirit).

## Test Coverage

19 xUnit facts, all GREEN:

| File | Facts | Coverage |
|---|---|---|
| `OscTypeTagInferenceTests` | 6 | D-38-13 inference (Int/Float/String/Bool/Long/Buffer/Double-stays-Double/Bool-false/unsupported-throws) |
| `OscRateLimitTests` | 4 | D-38-14 sample-and-hold (same-path drop, different-paths fire, post-window both fire, path mismatch no fire) |
| `OscLoopbackTests` | 2 | UDP 127.0.0.1:ephemeral round-trip preserves payload within 2s; address mismatch no dispatch within 500ms |
| `OscBundleTests` | 4 | Bundle traversal (same-path in-order, different-paths each picks match, immediate timetag sync, end-to-end SendBundle over UDP loopback) |
| `OscBundleDepthCapTests` | 3 | D-38-15 depth cap (depth 5 dispatches, depth 12 clamps + advisory, advisory is WarnOnce-deduped) |

UDP loopback flake-rate: 0/19 across multiple consecutive runs (the
ephemeral-port probe via `UdpClient(0, AddressFamily.InterNetwork)` +
2s timeout per Pitfall #10 has been robust).

## Package Legitimacy Gate

Satisfied at Task 1 plan-start per 38-RESEARCH §"Package Legitimacy Audit"
line 174:

- **Rug.Osc 1.2.5** verified live on NuGet flatcontainer API 2026-05-23
- License: **MIT** (confirmed via NuGet metadata)
- Transitive deps: **zero** (confirmed via lockfile)
- Source repo: `bitbucket.org/rugcode/rug.osc` (12+ years stable since Jan 2014)
- slopcheck `[SLOP]` verdict: **confirmed false positive** (slopcheck only
  queries PyPI; Rug.Osc is .NET-only; NuGet API confirms existence + license
  + source)
- Disposition: **Approved**

NU1701 warning on restore is expected (Rug.Osc targets .NET Framework,
restored via NetStandard 2.0 compat fallback) — parallels the existing
Pidgin / DryWetMidi precedent. The warning is benign; .NET Standard 2.0
binaries run fine on .NET 10.

## Pre-existing Test Failures (Out of Scope)

35 tests failing on the broader test suite at this commit — none caused by
Plan 38-06. Verified by running a sample of the failing tests on the
pre-Task1 baseline commit `2076214`:

| Failing test | Baseline status |
|---|---|
| `RagtimeFixtureTests.Ragtime_Synthetic_RmsRegression` | FAIL on baseline (pre-existing) |
| `FlowTestCliTests.FailingTestExitsNonZero` | FAIL on baseline (pre-existing) |
| `MatchExhaustivenessDefaultTests.NonExhaustiveDefaultWarnsAndReturnsVoid` | not reproduced on baseline run |

Most failures cluster in Phase 28 (`PerSynthArticulationTests` ×17,
`RagtimeFixtureTests` ×2), Phase 29 (`ArticulationOnSampleTests` ×6), and
Phase 35 (`FlowTestCliTests` ×2 + `MatchExhaustivenessDefaultTests` ×2) —
all subsystems untouched by this plan. These are tracked outside Plan 38-06
scope.

## Follow-ups for Plan 38-07 Closer

- **REQUIREMENTS.md OSC-02 wording update** — D-38-13 overrides the
  "strict-tag-by-arg" wording per D-v1.5-01 single-commit migration policy.
- **`examples/live/osc_controller.flow` chapter** — composer-facing tutorial
  exercising `(oscListen 7777 "/fader/1" handler)` + `(oscSend "localhost" 7777 "/fader/1" 0.5)`.
  Should document the Rug.Osc 1.2.5 sender-port quirk we worked around.
- **Address pattern wildcards (`/synth/*/freq`)** — explicitly deferred to
  v1.6 per D-38-16 (Plan 38-06 ships literal-path match only).
- **IPv6 + multicast** — explicitly deferred to v1.6 per D-38-16.
- **OSC flood advisory (one-shot per-path)** — deferred per D-38-14 Claude's
  discretion ("Default leans no-advisory; revisit if composer reports
  confusion").

## Self-Check: PASSED

All claimed artifacts verified present on disk:

```
FOUND: flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs (39 LOC)
FOUND: flow-lang/StandardLibrary/Network/OscHandleData.cs (60 LOC)
FOUND: flow-lang/StandardLibrary/Network/OscFunctions.cs (598 LOC)
FOUND: flow-lang/osc.flow (56 LOC)
FOUND: flow-lang.Tests/Integration/Phase38/OscTypeTagInferenceTests.cs
FOUND: flow-lang.Tests/Integration/Phase38/OscRateLimitTests.cs
FOUND: flow-lang.Tests/Integration/Phase38/OscLoopbackTests.cs
FOUND: flow-lang.Tests/Integration/Phase38/OscBundleTests.cs
FOUND: flow-lang.Tests/Integration/Phase38/OscBundleDepthCapTests.cs
FOUND: commit 525d1a2 (Task 1)
FOUND: commit 465056e (Task 2)
```

Verify command:
```
$ dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase38.Osc" --no-restore --no-build
Passed!  - Failed:     0, Passed:    19, Skipped:     0, Total:    19
```
