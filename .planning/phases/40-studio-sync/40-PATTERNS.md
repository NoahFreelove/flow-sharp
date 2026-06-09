# Phase 40: Studio Sync - Pattern Map

**Mapped:** 2026-06-06
**Files analyzed:** 18 (13 new, 5 edited)
**Analogs found:** 18 / 18 (every file has a verified in-tree analog — codebase is unusually well-prepared)

> Every analog below was read in full or at the cited line range this session. RESEARCH §Sources named these; this map verifies them and pins the exact excerpts the planner copies into PLAN actions. The clock thread (`MidiClock.cs`) is the ONLY genuinely-new mechanism with no analog for its timing core — flagged in §No Analog Found.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/Audio/IMidiBackend.cs` (+ `IMidiOutputHandle`) | interface | streaming (byte tx) | `flow-lang/Audio/IAudioBackend.cs` | exact (interface-for-interface) |
| `flow-lang/Audio/RtMidiMidiBackend.cs` | backend (impl) | streaming (byte tx) | `flow-lang/Audio/WebAudioBackend.cs` (interface impl + `IsAvailable` probe + charitable no-throw) | role-match |
| `flow-lang/Audio/NullMidiBackend.cs` | backend (silent fallback) | streaming (no-op) | `flow-lang/Audio/WebAudioBackend.cs` stub branch + `AudioPlaybackManager.DetectBackend` fallthrough | role-match |
| `flow-lang/Audio/MidiPlaybackManager.cs` | manager | request-response (lifecycle/probe) | `flow-lang/Audio/AudioPlaybackManager.cs` (`DetectBackend` + `IsAudioAvailable`) | exact |
| `flow-lang/Audio/MidiClock.cs` | service (timing thread) | event-driven (24 PPQN emit) + listener (slave) | `OscFunctions.StartListener` (slave half); **no analog for the master timing core** | partial (see §No Analog) |
| `flow-lang/StandardLibrary/Midi/MidiFunctions.cs` | builtin surface | request-response | `flow-lang/StandardLibrary/Network/OscFunctions.cs` (`Register` + gate) + `PlaybackFunctions.Register` | exact |
| `flow-lang/StandardLibrary/Midi/MidiClockFunctions.cs` | builtin surface | event-driven (handle-returning) | `OscFunctions.Register` (`oscListen`/`oscStop` handle pair) | exact |
| `flow-lang/StandardLibrary/Midi/MidiDeviceData.cs` | model (runtime handle state) | reference-identity | `flow-lang/StandardLibrary/Network/OscHandleData.cs` | exact |
| `flow-lang/StandardLibrary/Midi/ClockHandleData.cs` | model (runtime handle state) | reference-identity | `flow-lang/StandardLibrary/Network/OscHandleData.cs` | exact |
| `flow-lang/StandardLibrary/Midi/JackFunctions.cs` (best-effort) | builtin surface | request-response | `OscFunctions.Register` | role-match |
| `flow-lang/TypeSystem/SpecialTypes/MidiDeviceType.cs` | type (ref-identity Value) | n/a | `flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs` | exact |
| `flow-lang/TypeSystem/SpecialTypes/ClockHandleType.cs` | type (ref-identity Value) | n/a | `flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs` | exact |
| `flow-lang/TypeSystem/SpecialTypes/JackHandleType.cs` (best-effort) | type (ref-identity Value) | n/a | `flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs` | exact |
| `flow-lang/midi.flow` | config (stdlib module) | n/a | `flow-lang/osc.flow` | exact |
| `flow-lang/jack.flow` (best-effort) | config (stdlib module) | n/a | `flow-lang/osc.flow` | exact |
| `flow-lang/Audio/AudioCore.cs` **EDIT** | model | n/a (add `PlaybackStartTime`) | existing `AudioBuffer` fields (`Data`/`SampleRate`/`Frames` at lines 16/21/32) | self (additive) |
| `flow-lang/Core/FlowEngine.cs` **EDIT** | config (wiring) | n/a | `FlowEngine.cs:251-255` OSC register guard | self (additive) |
| `flow-lang/Runtime/ModuleLoader.cs` **EDIT** | route (module resolve) | n/a | `ModuleLoader.cs:56-93` `IsStrippedOnWeb` + advisory | self (additive) |
| `flow-lang/Runtime/ExecutionContext.cs` **EDIT** | runtime (gate bool) | n/a | `ExecutionContext.cs:437` `OscEnabled` + snapshot/restore `:1164`/`:1259` | self (additive) |
| `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` **EDIT** | service | request-response | `PlaybackFunctions.PlaySamples` at `:340-352` (set `PlaybackStartTime` origin here) | self (additive) |
| `flow-lang/flow-lang.csproj` **EDIT** | config | n/a | `flow-lang.csproj:107-141` Web-strip ItemGroups | self (additive) |
| `flow-lang.Tests/.../Phase40/*Tests.cs` (5 files) | test | varies | `OscFunctions` test seams + Phase39 charitable-skip + Phase47 scan | role-match |
| `flow-lang.Tests/.../Phase47/AssemblyReferenceScanTests.cs` **EDIT** | test (invariant) | n/a | self (add `JackSharp` to existing list at `:27-32`) | self (additive) |

---

## Pattern Assignments

### `flow-lang/Audio/IMidiBackend.cs` + `IMidiOutputHandle` (interface, streaming)

**Analog:** `flow-lang/Audio/IAudioBackend.cs` (read in full, 72 lines)

The whole-file shape transfers: a `: IDisposable` interface, a `string Name { get; }` property, a `bool IsInitialized { get; }` property, and method-level XML doc per member. Carry the exact doc-comment density.

**Property + IDisposable pattern** (lines 7-8, 48-53):
```csharp
public interface IAudioBackend : IDisposable
{
    string Name { get; }
    bool IsInitialized { get; }
    IReadOnlyList<string> GetDevices();   // ← ListPorts() analog
    bool SetDevice(string deviceName);
}
```

For `IMidiBackend` use the C# surface locked in CONTEXT (carried-forward, MIDI-RT-01): `ListPorts` / `OpenOutput` / `SendNoteOn` / `SendNoteOff` / `SendControlChange` / `SendSysex` / `Close` + `PortChanged` callback. RESEARCH Pattern 1 (lines 213-241) gives the literal target signatures including `SendProgramChange` (needed for D-40-02 GM routing) and `SendRaw(byte[])` (clock bytes — Open Q1). `OpenOutput` returns `IMidiOutputHandle?` — **null = charitable failure, NEVER throw** (mirror `GetDevices` returning empty list, IAudioBackend.cs:36 comment "May be empty").

---

### `flow-lang/Audio/RtMidiMidiBackend.cs` (backend impl, streaming)

**Analog:** `flow-lang/Audio/WebAudioBackend.cs` (interface impl + static `IsAvailable()` probe + class-level design-reference doc block) — read lines 1-60.

`WebAudioBackend` is the model for an `IAudioBackend`-shaped impl that (a) carries a heavy class XML doc enumerating each method's contract, (b) gates every native call behind an availability check, and (c) is `Compile Remove`'d on the Web target. RtMidiMidiBackend does the same against RtMidi.Core.

**Probe pattern** — model on `AudioPlaybackManager.IsAudioAvailable` (AudioPlaybackManager.cs:73-99), the canonical "does-not-throw feature detection":
```csharp
public bool IsAudioAvailable()
{
    try
    {
#if !FLOW_WEB
        return PulseAudioSimpleBackend.IsAvailable();
#else
        return WebAudioBackend.IsAvailable();
#endif
    }
    catch { return false; }
}
```
For RtMidi: a cheap `MidiDeviceManager.Default.OutputDevices` enumerate inside `try { } catch (DllNotFoundException) { return false; }` (Pitfall 2 — `librtmidi.so` may be absent → probe false → `NullMidiBackend`).

**RtMidi.Core send mapping** — RESEARCH Pattern 2 (lines 243-264) has the verified API. **Channel off-by-one hazard** (Pitfall 3): RtMidi `Channel.Channel1..16` is 1-based; `InstrumentRouting` returns 0-based (0, 9). One `ToRtChannel(int zeroBased)` helper, unit-tested for drum→ch9. **Anti-pattern (RESEARCH:315):** do NOT invent a typed `TimingClockMessage` — it does not exist; clock is raw bytes only (Open Q1).

**File MUST be `#if !FLOW_WEB`-guarded** and `Compile Remove`'d on Web (Pitfall 6).

---

### `flow-lang/Audio/MidiPlaybackManager.cs` (manager, request-response)

**Analog:** `flow-lang/Audio/AudioPlaybackManager.cs` (read in full, 183 lines) — **exact** structural template.

**Lock + lazy-detect + dispose lifecycle** (AudioPlaybackManager.cs:54-67, 134-179):
```csharp
public IAudioBackend GetBackend()
{
    lock (_lock)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlaybackManager));
        if (_backend != null) return _backend;
        _backend = DetectBackend();
        return _backend;
    }
}

private static IAudioBackend DetectBackend()
{
    if (WebAudioBackend.IsAvailable()) return new WebAudioBackend();
#if !FLOW_WEB
    if (PulseAudioSimpleBackend.IsAvailable()) return new PulseAudioSimpleBackend();
#endif
    throw new PlatformNotSupportedException("No audio output available...");
}
```

**For MIDI, the fallthrough must NOT throw** (charitable rule, RESEARCH Open Q2): instead of the final `throw new PlatformNotSupportedException`, return `new NullMidiBackend()` so a live session never dies on a missing `librtmidi.so`. Add an `IsMidiAvailable()` probe in the exact shape of `IsAudioAvailable` (lines 73-99). `Dispose` closes the device (lines 134-144 model — Runtime State Inventory line 341 mandates this).

---

### `flow-lang/StandardLibrary/Midi/MidiFunctions.cs` (builtin surface, request-response)

**Analog (registration mechanics + gate):** `flow-lang/StandardLibrary/Network/OscFunctions.cs` (read in full).
**Analog (manager-passed surface shape):** `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` (`Register(registry, manager)`).

**`Register` + marker + module-gate pattern** (OscFunctions.cs:96-132, 226-231):
```csharp
public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
{
    var sigMarker = new FunctionSignature("__enableOscModule", System.Array.Empty<FlowType>());
    registry.Register("__enableOscModule", sigMarker, _ =>
    {
        context.OscEnabled = true;       // ← context.MidiEnabled = true
        return Value.Void();
    });

    var sigSend = new FunctionSignature("oscSend",
        new FlowType[] { StringType.Instance, IntType.Instance, StringType.Instance },
        IsVarArgs: true,
        ParameterNames: new[] { "host", "port", "path" });
    registry.Register("oscSend", sigSend, args =>
    {
        RequireModuleActivated(context, "oscSend");
        // ... body ...
        return Value.Void();
    });
}

private static void RequireModuleActivated(FlowLang.Runtime.ExecutionContext context, string builtinName)
{
    if (!context.OscEnabled)
        throw new System.InvalidOperationException($"{builtinName} requires `use \"@osc\"`");
}
```
Every `midiOut`/`midiPorts`/`openMidiOutput`/`midiNoteOn`/`midiCC`/`midiSysex` body opens with `RequireModuleActivated(context, "midiX")` against the new `MidiEnabled` gate.

**Named-arg signature shape** (PlaybackFunctions.cs:62-68, OscFunctions.cs:115-118): pass `ParameterNames:` so D-36-11 universal named args work — required for the D-40-02 per-sequence override (`overrides=...`, named-arg form preferred).

**GM routing reuse (D-40-02 VERBATIM)** — `flow-lang/StandardLibrary/Notation/InstrumentRouting.cs:45`:
```csharp
public static (int gmProgram, int channel) ResolveGmProgram(string seqName)
// drum* → (0, 9); piano* → (0,0); brass* → (56,0); sax* → (65,0);
// flute* → (73,0); string* → (48,0); organ* → (19,0); bell* → (14,0)
```
`(midiOut song "port")` calls `InstrumentRouting.ResolveGmProgram(seq.Name)` per sequence → `dev.SendProgramChange(ToRtChannel(channel), gmProgram)` then streams notes. **Do not write a second table** (RESEARCH Don't-Hand-Roll). The named-arg override (D-40-02, Open Q4 → planner discretion, minimal form `Dict<String,Int>` name→channel) layers ON TOP.

**Input validation at the builtin boundary** (Security V5, RESEARCH:569): charitably clamp channel ∈ 0..15, pitch/vel/CC ∈ 0..127, length-cap sysex — clamp+advisory, never throw to native (Phase 44 input-perimeter precedent).

---

### `flow-lang/StandardLibrary/Midi/MidiClockFunctions.cs` (builtin surface, event-driven)

**Analog:** `OscFunctions.cs` `oscListen`/`oscStop` handle pair (registration: 139-161; lifecycle: `StartListener` 353-469, `StopListener` 471-481).

`clockMaster(device)` / `clockSlave("port")` return reference-identity handles exactly as `oscListen` returns `Value.OscHandle(...)`. The slave path IS the OSC listener pattern (see `MidiClock.cs` below). `clockStop` vs handle-dispose is Claude's discretion (D-40-03) — model `oscStop` (lines 152-161 + StopListener 471-481).

**Handle stop lifecycle** (StopListener, OscFunctions.cs:471-481):
```csharp
private static void StopListener(OscHandleData handle)
{
    try { handle.Cts.Cancel(); } catch { }
    try { handle.Receiver?.Dispose(); } catch { }
    try { handle.ListenerTask.Wait(TimeSpan.FromSeconds(1)); }
    catch (AggregateException) { }
    catch (Exception) { }
}
```

---

### `flow-lang/Audio/MidiClock.cs` (service, event-driven master + listener slave)

**Analog (slave half only):** `OscFunctions.StartListener` (OscFunctions.cs:353-469).
**Master timing core: NO ANALOG** — see §No Analog Found.

**Background `Task` + `Cts.Token.Register(dispose)` to break a blocked receive** (the load-bearing slave-listener excerpt, OscFunctions.cs:407-459):
```csharp
var cts = new CancellationTokenSource();
// Pitfall #5: Cts.Cancel() alone won't break the blocked Receive(). Register a
// callback that disposes the receiver → forces ObjectDisposedException → breaks loop.
var receiverRef = receiver;
cts.Token.Register(() => { try { receiverRef.Dispose(); } catch { } });

var task = Task.Run(() =>
{
    try { receiver.Connect(); } catch (Exception ex) { /* charitable WarnOnce, return */ }
    while (!cts.IsCancellationRequested)
    {
        Rug.Osc.OscPacket packet;
        try { packet = receiver.Receive(); }          // Blocking
        catch (OperationCanceledException) { break; }
        catch (ObjectDisposedException) { break; }
        catch (Exception ex) { Console.Error.WriteLine($"..."); continue; }   // never die
        DispatchPacket(packet, ...);
    }
}, cts.Token);
```
**Slave specifics** (RESEARCH Pattern 4, lines 288-300): count 0xF8 pulses, every 24 = 1 quarter, derive BPM from inter-pulse `Stopwatch` deltas, apply 8-pulse settle (average last 8) before writing `MusicalContext.Tempo`, mode (master⊕slave) switch only at bar boundary.

**Slave writes tempo** — `flow-lang/Runtime/MusicalContext.cs:43`:
```csharp
public double? Tempo { get; set; }      // master reads; slave drives
```

**Test seam for slave** — model `OscFunctions.HandlerInvokeOverride` + `ResetForTesting` (OscFunctions.cs:84-87, 636, 671-685): a `CaptureMidiBackend` / byte-injection seam that records sent byte arrays + lets `ClockSlaveTests` inject a synthetic 0xF8 stream without real ALSA (RESEARCH Validation §, line 545).

---

### `flow-lang/StandardLibrary/Midi/MidiDeviceData.cs` + `ClockHandleData.cs` (model, reference-identity)

**Analog:** `flow-lang/StandardLibrary/Network/OscHandleData.cs` (read in full, 60 lines) — **exact**.

**`required`-init record-shape carrying the live resources + Cts + Task** (OscHandleData.cs:31-60):
```csharp
public sealed class OscHandleData
{
    public required int Port { get; init; }
    public required string Path { get; init; }
    public required Rug.Osc.OscReceiver? Receiver { get; init; }   // null = charitable dead handle
    public required CancellationTokenSource Cts { get; init; }
    public required Task ListenerTask { get; init; }
    public Rug.Osc.OscPacket? PendingPacket { get; init; }         // dual-role discriminator
}
```
`MidiDeviceData` holds the opened RtMidi output handle + Name; `ClockHandleData` holds the clock thread + `CancellationTokenSource` (carry the `Cts`/`Task`/`required`-init shape verbatim). **Both files `#if !FLOW_WEB` + `Compile Remove` on Web** (OscHandleData.cs is stripped at csproj:125).

---

### `flow-lang/TypeSystem/SpecialTypes/MidiDeviceType.cs` / `ClockHandleType.cs` / `JackHandleType.cs` (type)

**Analog:** `flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs` (read in full, 39 lines) — **exact**.

**Sealed singleton ref-identity FlowType** (OscHandleType.cs:26-39):
```csharp
public sealed class OscHandleType : FlowType
{
    private OscHandleType() { }
    public static OscHandleType Instance { get; } = new();
    public override string Name => "OscHandle";
    public override int GetSpecificity() => 151;
    public override bool IsCompatibleWith(FlowType target) => target is OscHandleType;
    public override bool CanConvertTo(FlowType target) => target is OscHandleType;
}
```
Specificity values are Claude's discretion (D-40-03): RESEARCH Pattern 3 (line 279) suggests `MidiDeviceType => 152`, slotting above OscHandle=151. Pick distinct values for Clock/Jack (e.g. 153/154). Add matching `Value.MidiDevice(...)` factory (model `Value.OscHandle` at Value.cs:122-124, itself `#if`-guarded for Web).

---

### `flow-lang/midi.flow` / `jack.flow` (config, stdlib module)

**Analog:** `flow-lang/osc.flow` (read in full, 52 lines) — **exact**.

**`module` + `use "@std"` + `internal proc` decls + trailing marker call** (osc.flow:18-52):
```
module osc
use "@std"
internal proc __enableOscModule ()
internal proc oscSend (String: host, Int: port, String: path)
internal proc oscListen (Int: port, String: path, Function: handler)
internal proc oscStop (OscHandle: handle)
(__enableOscModule)
```
RESEARCH (lines 435-451) gives the literal `midi.flow` skeleton: `module midi`, decls for `midiPorts`/`openMidiOutput`/`midiOut`/`midiNoteOn`/`midiNoteOff`/`midiCC`/`midiSysex`/`clockMaster`/`clockSlave`, trailing `(__enableMidiModule)`. Use leading `Note:` doc comments like osc.flow:1-16. **Both files get `<None Remove>` on Web** (csproj:140-141 pattern).

---

## Shared Patterns

### Reference-identity handle lifecycle (D-40-03)
**Source:** `OscHandleData.cs` + `OscHandleType.cs` + `Value.OscHandle` (Value.cs:122-124) + `StartListener`/`StopListener` (OscFunctions.cs:407-481)
**Apply to:** every `MidiDevice` / clock / JACK handle. `Cts.Token.Register(() => dispose)` (OscFunctions.cs:413-414) is the load-bearing idiom to break a blocked receive; `StopListener` (471-481) is the stop sequence (Cancel → Dispose → Wait(1s) charitably).

### Charitable degradation — never throw (CONTEXT carried-forward + RESEARCH:317)
**Source:** `AudioPlaybackManager.IsAudioAvailable` try/catch→false (AudioPlaybackManager.cs:95-98) + `StartListener` dead-handle sentinel (OscFunctions.cs:397-405) + `RenderingDiagnostics.WarnOnce(string sentinelKey, string message)` (RenderingDiagnostics.cs:29)
**Apply to:** every backend probe, every device open, every clock/slave failure. Missing port/server/lib → `WarnOnce("midi-...", "[midi] ...")` + null/sentinel + continue. Use `[midi]` / `[clock]` prefix convention.

### Opt-in module gating (D-40-04, mirrors @osc/@sfz exactly)
Three coordinated edits — apply ALL three or the module half-loads:
1. **ExecutionContext gate bool** — `ExecutionContext.cs:437`:
   ```csharp
   public bool OscEnabled { get; set; } = false;   // ← add: public bool MidiEnabled { get; set; } = false;
   ```
   Also mirror the snapshot/restore at `:1164` (`OscEnabled = OscEnabled,`) and `:1259` (`OscEnabled = snap.OscEnabled;`) — add `MidiEnabled` to both.
2. **FlowEngine register guard** — `FlowEngine.cs:251-255`:
   ```csharp
   #if !FLOW_WEB
       FlowLang.StandardLibrary.Network.OscFunctions.Register(internalRegistry, _context);
   #endif
   ```
   Add `MidiFunctions.Register(...)` + `MidiClockFunctions.Register(...)` (+ `JackFunctions.Register` if shipped) inside an `#if !FLOW_WEB` block at the same site.
3. **ModuleLoader Web advisory** — `ModuleLoader.cs:56-62`:
   ```csharp
   private static bool IsStrippedOnWeb(string requestedPath) =>
       requestedPath == "@sfz" || requestedPath == "@osc";   // ← || "@midi" || "@jack"
   ```
   The existing `IsWebTarget` gate at `:86-93` then emits the charitable `[target] module '@midi' unavailable on Web target` advisory + returns `Error` — **no new code** beyond the predicate.

### Web-strip discipline (D-40-04 / Pitfall 6)
**Source:** `flow-lang.csproj:107-141` + `AssemblyReferenceScanTests.cs:27-32`
**Apply to:** every new MIDI backend/builtin/handle/type file + the new PackageReferences.
- PackageReference under `<ItemGroup Condition="'$(FlowTarget)' != 'Web'">` (model the Rug.Osc entry, csproj:110-112):
  ```xml
  <PackageReference Include="RtMidi.Core" Version="1.0.53" />
  <!-- <PackageReference Include="JackSharp" Version="0.4.0" /> if shipped -->
  ```
- `<Compile Remove>` each new backend/builtin/handle-type file under `<ItemGroup Condition="'$(FlowTarget)' == 'Web'">` (model lines 120-126); `<None Remove>` `midi.flow`/`jack.flow` (model lines 140-141).
- Extend `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` (currently `"Rug.Osc"`, `"RtMidi.Core"`, `"System.IO.FileSystemWatcher"` at lines 29-31) — **add `"JackSharp"`**. `RtMidi.Core` is ALREADY present (D-47-14 forward-look) — do not duplicate.

### MIDI-RT-04 alignment seam (introduce `AudioBuffer.PlaybackStartTime`)
**Source:** `AudioCore.cs:10-32` (existing `AudioBuffer` fields) + `PlaybackFunctions.PlaySamples` (PlaybackFunctions.cs:340-352)
**Apply to:** the playback path. Add a nullable origin field to `AudioBuffer` (alongside `Data`/`SampleRate`/`Frames`), set it the instant `backend.Play` begins:
```csharp
private static void PlaySamples(float[] samples, int sampleRate, int channels, AudioPlaybackManager manager)
{
    var ct = manager.StartPlayback();
    var backend = GetBackendOrThrow(manager);
    // ← set PlaybackStartTime = Stopwatch tick origin HERE, then dispatch
    //   scheduled MIDI off a sibling thread keyed off that origin
    try { backend.Play(samples, sampleRate, channels, ct); }
    catch (OperationCanceledException) { }
}
```
**Honesty contract (Pitfall 5):** buffer-relative ms alignment, NOT sample-accurate (blocking PulseAudio Simple has no pull-model callback). Do not write a verification claiming sample accuracy.

### Determinism invariant (LINK-02 / CONTEXT carried-forward)
Clock/Link tempo are `play`/`loop`/`preview`-only inputs — NEVER reach `writeWav`/`writeMidi`. The `OfflineRenderDeterminismTests` gate is writable even if Link is deferred (RESEARCH:535).

### CI test gates
**Charitable-skip model** — `MusicXmlRoundTripTests.CharitableSkipWhenMscoreAbsent` (Phase39 file, lines 87-119):
```csharp
[Fact]
public void CharitableSkipWhenMscoreAbsent()
{
    if (MscoreAvailable()) return;       // ← probe librtmidi.so / snd-virmidi
    // ... redirect Console.Error, WarnOnce("midi-virtual-absent", "[midi] ..."), assert advisory, PASS
}
```
Apply to `VirtualMidiTests` + `ClockMasterTests` — probe for `librtmidi.so`/`snd-virmidi`; absent → `WarnOnce` + PASS.

**In-process loopback test seam** — model `OscFunctions.HandlerInvokeOverride` (OscFunctions.cs:636) + `DispatchPacketForTesting` (671-685) + `ResetForTesting` (84-87) + `PulseAudioCaptureBackend.IsAvailable` probe shape. A `CaptureMidiBackend` records sent byte arrays so byte/rate assertions need no real ALSA (RESEARCH:545, 555). Always restore override to null in test teardown (OscFunctions.cs:632-636 contract).

---

## No Analog Found

| File / Concern | Role | Data Flow | Reason |
|----------------|------|-----------|--------|
| `MidiClock.cs` — master 24 PPQN timing core | service | event-driven (timed emit) | No in-tree dedicated timing thread with `Stopwatch` deadline + sub-ms spin-wait. The CLOSEST precedents are the Phase 38 `flow watch` 2 Hz heartbeat-off-the-audio-thread loop (`flow-interpreter/`, RESEARCH:369) and the spin-wait discipline in RESEARCH Pitfall 4. The slave HALF reuses `OscFunctions.StartListener` verbatim, but the master pulse loop is genuinely new — use RESEARCH §Clock + Pitfall 4 (dedicated thread, `Stopwatch`-based deadline, short final spin-wait, NOT `Thread.Sleep`) rather than a codebase analog. |
| Clock raw-byte send (0xF8/FA/FB/FC) | backend method | byte tx | RtMidi.Core has no typed clock message; the `internal SendMessage(byte[])` access path is **Open Q1** (reflection vs vendored shim vs direct C-API P/Invoke) — resolve in the FIRST plan via a 1-task spike before writing clock tasks. No existing Flow code reaches a library's internal members; this is net-new. |

> Both gaps are isolated to the clock mechanism — RESEARCH §"Don't Hand-Roll" key insight (line 332): "The clock is the ONLY genuinely-new mechanism. Everything else is assembling existing, proven Flow patterns." Concentrate spike budget on Open Q1 + Q2.

## Metadata

**Analog search scope:** `flow-lang/Audio/`, `flow-lang/StandardLibrary/{Network,Audio,Notation}/`, `flow-lang/TypeSystem/SpecialTypes/`, `flow-lang/Runtime/`, `flow-lang/Core/`, `flow-lang/*.flow`, `flow-lang.Tests/Integration/Phase{39,47}/`
**Files scanned:** ~16 read (in full or targeted ranges), all cited with file:line
**Pattern extraction date:** 2026-06-06
**Determinism note:** every analog line number was verified against the live tree this session, not carried from RESEARCH unverified.
