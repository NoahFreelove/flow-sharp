---
phase: 40-studio-sync
reviewed: 2026-06-06T00:00:00Z
depth: standard
files_reviewed: 23
files_reviewed_list:
  - flow-lang/Audio/IMidiBackend.cs
  - flow-lang/Audio/MidiClock.cs
  - flow-lang/Audio/MidiPlaybackManager.cs
  - flow-lang/Audio/NullMidiBackend.cs
  - flow-lang/Audio/RtMidiMidiBackend.cs
  - flow-lang/StandardLibrary/Midi/MidiFunctions.cs
  - flow-lang/StandardLibrary/Midi/MidiClockFunctions.cs
  - flow-lang/StandardLibrary/Midi/JackFunctions.cs
  - flow-lang/StandardLibrary/Midi/MidiDeviceData.cs
  - flow-lang/StandardLibrary/Midi/ClockHandleData.cs
  - flow-lang/StandardLibrary/Midi/JackHandleData.cs
  - flow-lang/TypeSystem/SpecialTypes/MidiDeviceType.cs
  - flow-lang/TypeSystem/SpecialTypes/ClockHandleType.cs
  - flow-lang/TypeSystem/SpecialTypes/JackHandleType.cs
  - flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs
  - flow-lang/StandardLibrary/Audio/AudioCore.cs
  - flow-lang/Runtime/ModuleLoader.cs
  - flow-lang/Runtime/ExecutionContext.cs
  - flow-lang/Runtime/Value.cs
  - flow-lang/Core/FlowEngine.cs
  - flow-lang/Parsing/Parser.cs
  - flow-lang/Parsing/TypeParser.cs
  - flow-lang/midi.flow
  - flow-lang/jack.flow
findings:
  critical: 3
  warning: 7
  info: 4
  total: 14
status: fixed
fixed_at: 2026-06-06
fixed:
  critical: [CR-01, CR-02, CR-03]
  warning: [WR-01, WR-02, WR-03, WR-04, WR-05, WR-06, WR-07]
  info: [IN-02, IN-03]
deferred:
  info: [IN-01]
fix_commits:
  CR-01: 79eac8d
  CR-02: bd8cc95
  CR-03: 4dda142   # also IN-02 doc
  WR-01-02: 3e1032c
  WR-03: 6aa8c89
  WR-04: d79a6b7   # also IN-04
  WR-05: a5f5416
  WR-06: a869bf5
  WR-07: 33321f9
  IN-03: b973446
---

# Phase 40: Code Review Report

**Reviewed:** 2026-06-06
**Depth:** standard
**Files Reviewed:** 23
**Status:** fixed (all 3 critical + all 7 warning resolved; IN-02/IN-03/IN-04 also addressed; IN-01 deferred)

> **Fix pass (2026-06-06):** All 3 Critical + all 7 Warning findings fixed on
> branch `dev`, each in an atomic `fix(40): …` commit with a focused regression
> test. Both `dotnet build flow-lang -p:FlowTarget=Desktop` and `-p:FlowTarget=Web`
> exit 0; the Phase 40 suite is green (45/45). IN-02 (PlaybackStartTime seam) and
> IN-04 (mixed lock/Interlocked) were closed alongside CR-03 and WR-04
> respectively; IN-03 fixed standalone. IN-01 (PortChanged hot-plug) is left as a
> documented deferred surface per the project's "usage isn't a removal signal"
> rule — implementing a poll loop is out of trivial-fix scope.

## Summary

Phase 40 adds real-time MIDI output (RtMidi.Core), a MIDI clock master/slave, and
JACK transport sync, all opt-in (`@midi` / `@jack`) and Web-stripped. The
scaffolding is solid: the charitable contract (NullMidiBackend fallback, WarnOnce
on every native failure, dead-handle returns) is implemented thoroughly across
`IsAvailable` / `OpenOutput` / `ListPorts` / `clockSlave` / `jackSync`; the
`#if !FLOW_WEB` discipline is consistent; the channel/key off-by-one I was asked to
verify is **correct** (confirmed against RtMidi.Core 1.0.53: `Channel1 == 0`,
`Channel16 == 15`, `Key0 == 0`, `Key127 == 127`, so the direct `(Channel)c` /
`(Key)p` cast maps a 0-based channel/pitch onto the right enum ordinal; drum ch9 →
`Channel10` = GM percussion); the SendRaw reflection bridge is **cached per-handle**
(not per-pulse), null-guarded, and degrades to no-op; input clamping (channel
0..15, 7-bit, sysex cap) is present at the builtin boundary.

However, the review surfaced three blockers and several quality defects that
undermine the headline features:

1. The native `JackPositionT` struct is **under-sized** vs the real
   `jack_position_t`, so a live `jackSync` against a real JACK server overruns the
   pinned managed buffer (memory corruption). The test seam hides it.
2. The documented `overrides=` named-arg on `midiOut` **cannot be called** — the
   signatures register 2 `InputTypes` but 3 `ParameterNames`, which the
   OverloadResolver rejects with an arity error. The multitimbral remap feature is
   dead, and the failure is a hard error (un-charitable).
3. The high-level `midiOut` path emits NoteOn immediately followed by NoteOff with
   **zero inter-event timing**, so every note is zero-length and all notes fire
   simultaneously — the MIDI-RT-04 "play a song to hardware" feature produces no
   musical output.

Plus a LINK-02 determinism hole (clock/jack tempo writes the same `MusicalContext`
instance that sections capture for offline render), an unsynchronized cross-thread
tempo write, a slave-subscription leak/race, the confirmed-dead `PortChanged`
hot-plug surface, and an unused `ClockHandleData.Cts`.

## Critical Issues

### CR-01: JACK transport struct is under-sized — native query overruns managed buffer

**STATUS: RESOLVED** (fix commit `79eac8d`)

**File:** `flow-lang/StandardLibrary/Midi/JackFunctions.cs:75-105, 210`
**Issue:** `jack_transport_query(client, ref pos)` instructs the native library to
write a full `jack_position_t` (the native `sizeof`) into the address of the pinned
managed `JackPositionT`. The managed mirror is smaller than the real struct:
- It declares `public int tick_double` (4 bytes), but the current JACK
  `jack/types.h` field is `double tick_double` (8 bytes).
- It declares only `padding0..padding4` (5 × int32 = 20 bytes), but the canonical
  reserved tail is `int32_t padding[7]` (28 bytes).

That is a ≥12-byte under-size. Because `ref`-marshaling a blittable struct pins the
managed instance and hands native its address, the native side writes past the end
of the managed struct → corruption of adjacent stack/heap memory. The code comment
on line 98 even acknowledges "under-size would corrupt." The `TransportQueryOverride`
test seam bypasses `QueryTransport` entirely, so every test passes while real
hardware corrupts memory. The `try/catch` cannot catch a silent buffer overrun.

**Fix:** Make the managed struct at least as large as the native one, and match the
real field types. Pad generously (native `jack_position_t` is intentionally
over-allocated). For example:
```csharp
[StructLayout(LayoutKind.Sequential)]
private struct JackPositionT
{
    public ulong unique_1;
    public ulong usecs;
    public uint  frame_rate;
    public uint  frame;
    public int   valid;
    public int   bar;
    public int   beat;
    public int   tick;
    public double bar_start_tick;
    public float beats_per_bar;
    public float beat_type;
    public double ticks_per_beat;
    public double beats_per_minute;     // the field JACK-01 reads
    public double frame_time;
    public double next_time;
    public uint  bbt_offset;
    public float audio_frames_per_video_frame;
    public uint  video_offset;
    public double tick_double;          // FIX: double, not int
    // FIX: 7 padding int32s, not 5, to match int32_t padding[7]
    public int padding0; public int padding1; public int padding2; public int padding3;
    public int padding4; public int padding5; public int padding6;
    public ulong unique_2;
}
```
Better: over-allocate the tail to a fixed-size buffer (e.g. an extra 64 reserved
bytes) so a future JACK ABI bump cannot re-introduce the overrun.

### CR-02: `midiOut overrides=` named-arg can never be passed — resolver rejects the call

**STATUS: RESOLVED** (fix commit `bd8cc95`)

**File:** `flow-lang/StandardLibrary/Midi/MidiFunctions.cs:166-192` (and
`midi.flow:14-16`)
**Issue:** Both `midiOut` overloads register with **2** `InputTypes`
(`Song/Sequence`, `String`) but **3** `ParameterNames`
(`song/seq`, `port`, `overrides`), and are not varargs. The lambda then reads
`args.Count > 2 ? ReadOverrides(args[2]) : null`, but that branch is unreachable:
- For a fixed (non-varargs) signature, `FunctionSignature.Matches` requires
  `argTypes.Count == InputTypes.Count` (FunctionSignature.cs:149), so 3 positional
  args never match a 2-`InputTypes` signature.
- A named call `(midiOut song "p" overrides=(dict ...))` is rejected by the
  resolver: `positionalArgTypes.Count(2) + namedArgTypes.Count(1) != InputTypes.Count(2)`
  → "function 'midiOut' expects 2 arguments, got 2 positional + 1 named"
  (OverloadResolver.cs:259-265). It also trips the `slot >= InputTypes.Count` guard
  at OverloadResolver.cs:286 (`overrides` lives at name-index 2 ≥ 2).

So the documented multitimbral channel-remap feature (CLAUDE.md / `midi.flow`
"An overrides= named-arg Dict remaps channels") is dead code that surfaces as a
hard parse/resolve **error** when a composer uses it — a charitable-rule violation
on a documented feature.

**Fix:** Register `overrides` as a real parameter slot. Add an explicit
3-`InputTypes` overload (with `DictType` as the third slot) alongside the 2-arg one,
or collapse to one signature whose `InputTypes` matches its `ParameterNames`:
```csharp
var sigOutSong = new FunctionSignature("midiOut",
    new FlowType[] { SongType.Instance, StringType.Instance, DictType.Instance },
    ParameterNames: new[] { "song", "port", "overrides" });
// + keep the 2-arg overload for the no-overrides call, OR make overrides optional
//   via a defaulted-arg mechanism the resolver actually honors.
```
Then the lambda's `args[2]` access is reachable. Add a test that actually calls
`midiOut` with `overrides=` and asserts the channel remap.

### CR-03: high-level `midiOut` sends NoteOn+NoteOff with zero timing — no notes sound in time

**STATUS: RESOLVED** (fix commit `4dda142`)

**File:** `flow-lang/StandardLibrary/Midi/MidiFunctions.cs:318-330`
**Issue:** The per-sequence note loop does:
```csharp
handle.SendNoteOn(channel, pitch, vel);
handle.SendNoteOff(channel, pitch);
```
back-to-back, inside two tight `foreach` loops, with **no delay between NoteOn and
NoteOff and no delay between notes**. Every note is therefore zero-duration, and
all notes in the song are dispatched essentially simultaneously. The result on real
hardware is a click/no-sound burst, not the song — the headline MIDI-RT-04 "a
hardware port sounds identical to the exported .mid" promise (D-40-02) is not met.
The Phase 40 `PlaybackStartTime` alignment seam added to `AudioBuffer` /
`PlaySamples` is never consulted here, so there is no scheduler at all.

**Fix:** Schedule events against a wall-clock timeline derived from the active
tempo + each note's beat position/duration (the "best-effort ms" the docstring
claims). Minimally, sleep the note's duration between On and Off and advance an
accumulator across notes/bars; ideally key dispatch off the
`AudioBuffer.PlaybackStartTime` origin the seam was built for. Until timing exists,
the docstring/`midi.flow` claims about audible output should not stand.

## Warnings

### WR-01: clock/JACK tempo writes the same MusicalContext that offline render captures (LINK-02)

**STATUS: RESOLVED** (fix commit `3e1032c`)

**File:** `flow-lang/StandardLibrary/Midi/MidiClockFunctions.cs:84-86`,
`flow-lang/Audio/MidiClock.cs:327-328`, `flow-lang/StandardLibrary/Midi/JackFunctions.cs:164-166`
**Issue:** `clockSlave` and `jackSync` obtain `context.GetMusicalContext()` and write
`mctx.Tempo = bpm`. `GetMusicalContext()` returns a **cached resolved snapshot**
(`_cachedMusicalContext`, ExecutionContext.cs:857-911). Sections capture that very
snapshot at build time (`Interpreter.cs:712,762`:
`var musicalContext = _context.GetMusicalContext(); new SectionData(..., musicalContext, ...)`),
and offline render reads `section.Context?.Tempo` (SongRenderer.cs:277). So a slave
or jackSync tempo write that lands before a section is defined leaks the live clock
tempo into `renderSong`/`writeWav`/`writeMidi` — violating the LINK-02 determinism
contract the `midi.flow`/`jack.flow` headers assert ("NEVER reaches
writeWav/writeMidi"). The contract is documented but not enforced in code.

**Fix:** Do not mutate the shared resolved context. Write the live tempo into a
dedicated sink (e.g. a separate "live clock tempo" field consumed only by
play/loop/preview), or push a transient frame that is never captured by
`SectionData`. Add a test that runs a slave/jackSync, defines a section, and asserts
the rendered offline tempo is unchanged.

### WR-02: cross-thread tempo write is unsynchronized (torn read of `double?`)

**STATUS: RESOLVED** (fix commit `3e1032c`)

**File:** `flow-lang/Audio/MidiClock.cs:327-328`
**Issue:** The slave background thread executes `_context.Tempo = bpm` on a
`MusicalContext.Tempo` declared as a plain `double?` auto-property
(MusicalContext.cs:43) with no synchronization. The main interpreter thread reads
`GetMusicalContext().Tempo` concurrently. `Nullable<double>` is a 16-byte struct;
reads/writes are not atomic, so a reader can observe a torn value (HasValue from one
write, Value from another). Same applies to jackSync's `mctx.Tempo = bpm` if any
async path exists.

**Fix:** Route live tempo through a synchronized primitive — e.g. an
`Interlocked.Exchange` on a backing `long` (bits), or a `volatile`-guarded plain
`double` with a separate `bool` flag written under a lock, or a dedicated
thread-safe "live tempo" accessor. Keep the read side consistent.

### WR-03: slave subscription leaks / can be missed on a Stop/Start race (real-hardware path)

**STATUS: RESOLVED** (fix commit `6aa8c89`)

**File:** `flow-lang/Audio/MidiClock.cs:261-282`
**Issue:** In the real-hardware `StartSlave` path, `clock._slaveSubscription = unsub`
is assigned **inside** the `Task.Run` body after `TrySubscribe` succeeds. `Stop()`
disposes `_slaveSubscription` from the caller thread (MidiClock.cs:395). Two
problems: (1) if `Stop()` runs before the task body assigns `_slaveSubscription`,
the unsubscriber is never disposed and the RtMidi input device stays open/subscribed
(leak). (2) The task body has no `finally` to dispose `unsub` itself — when the
token is cancelled and `WaitHandle.WaitOne()` returns, the body just `return`s,
relying solely on the racy `Stop()` read. The output handle / device is leaked.

**Fix:** Dispose the unsubscriber in a `finally` inside the task body (so the owning
thread always tears down), and/or assign `_slaveSubscription` under a lock /
re-check `_cts.IsCancellationRequested` after subscribing and immediately dispose if
already cancelled. Mirror the OscFunctions teardown ownership precisely.

### WR-04: `_pulseCount` is never advanced by the master — AtBarBoundary/PulseCount wrong for masters

**STATUS: RESOLVED** (fix commit `d79a6b7`)

**File:** `flow-lang/Audio/MidiClock.cs:129-165, 303-308, 359-380`
**Issue:** The master loop counts with a **local** `pulseIndex` (line 136, 163) and
never touches the shared `_pulseCount` field, which is only incremented in the slave
`OnClockPulse` (line 308). Yet `AtBarBoundary()` and `PulseCount` read `_pulseCount`
(lines 375, 380), and the docstring claims "The master thread + slave both advance a
pulse counter; either side's count gates a switch" (line 371-372). Consequences for
a master clock: `PulseCount` always reports 0, and `RequestModeSwitch` always sees
`AtBarBoundary() == true` (0 % pulsesPerBar == 0), so a master→slave switch is never
deferred to a real bar boundary — contradicting CLOCK-02's bar-boundary gate.

**Fix:** Increment the shared counter from the master loop too (e.g.
`Interlocked.Increment(ref _pulseCount)` per pulse instead of, or in addition to,
the local `pulseIndex`), or compute `AtBarBoundary` from `pulseIndex` for masters.
Pick one counter as the single source of truth and use it consistently.

### WR-05: `BufferToSysexBytes` emits unframed data — sysex lacks 0xF0/0xF7 envelope

**STATUS: RESOLVED** (fix commit `a5f5416`)

**File:** `flow-lang/StandardLibrary/Midi/MidiFunctions.cs:235-257`
**Issue:** A System Exclusive message must be framed `0xF0 <data...> 0xF7`.
`BufferToSysexBytes` produces only clamped 0..127 data bytes with no `0xF0` start /
`0xF7` end, and hands them to `new SysExMessage(data)`. RtMidi.Core's
`SysExMessage` encodes the array as-is; an unframed payload is not a valid sysex
message and most devices/`SendMessage` paths will reject or mishandle it (a
standalone `SysExMessage.Encode()` on an unframed array even NRE'd in my probe —
inconclusive outside a device context, but indicative). At best `midiSysex` never
delivers a usable message; the send is try/caught so it fails silently.

**Fix:** Frame the payload before constructing the message (prepend `0xF0`, append
`0xF7`) — or, if composers are expected to supply already-framed bytes, document
that and validate the framing with a charitable advisory. Add a test asserting the
on-wire bytes start with `0xF0` and end with `0xF7`.

### WR-06: `ClockHandleData.Cts` is created but never used — leaked CTS + false docstring

**STATUS: RESOLVED** (fix commit `a869bf5`)

**File:** `flow-lang/StandardLibrary/Midi/ClockHandleData.cs:36-39`,
`flow-lang/StandardLibrary/Midi/MidiClockFunctions.cs:70,91`
**Issue:** Both `clockMaster` and `clockSlave` set `Cts = new CancellationTokenSource()`
on the handle, but nothing ever reads/cancels/disposes `handle.Cts`. `clockStop`
calls `handle.Clock.Stop()`, which cancels the clock's **internal** `_cts`, not the
handle's. The handle's `Cts` is a leaked `IDisposable`, and the docstring
("(clockStop) cancels it charitably via MidiClock.Stop()", ClockHandleData.cs:37-38)
is false — `MidiClock.Stop()` has no reference to `handle.Cts`.

**Fix:** Remove the `Cts` field from `ClockHandleData` (the `MidiClock` already owns
its `_cts`), or actually wire it (pass it into `MidiClock` and cancel/dispose it in
`clockStop`). Correct the docstring either way.

### WR-07: empty/absent port-name substring match opens an arbitrary device

**STATUS: RESOLVED** (fix commit `33321f9`)

**File:** `flow-lang/Audio/RtMidiMidiBackend.cs:100-122` (and the input twin
`flow-lang/Audio/MidiClock.cs:460-461`)
**Issue:** `OpenOutput`/`TrySubscribe` select the first device whose
`Name.Contains(port, OrdinalIgnoreCase)`. `string.Contains("")` returns true for
every device, so `(openMidiOutput "")` (or any empty/whitespace port) silently binds
to whatever device happens to be first — a surprising mis-route rather than a
charitable dead handle. Likewise a too-broad substring can match an unintended
device.

**Fix:** Treat empty/whitespace `port` as "no match" → return null + WarnOnce (dead
handle), consistent with the absent-port path. Optionally prefer an exact
case-insensitive name match before falling back to substring.

## Info

### IN-01: `PortChanged` hot-plug event is declared but never raised (dead surface)

**STATUS: DEFERRED** (documented; poll-based hot-plug out of trivial-fix scope)

**File:** `flow-lang/Audio/RtMidiMidiBackend.cs:47,145`; `IMidiBackend.cs:53-59`;
`NullMidiBackend.cs:33-37`
**Issue:** The MIDI-RT-01 hot-plug callback `PortChanged` is part of the
`IMidiBackend` contract but is never invoked anywhere in the codebase (only assigned
`null` in `Dispose`, which is what suppresses the CS0414 the focus note mentioned).
There is no poll loop (the 40-RESEARCH "poll-based" Pattern 1/A7 it documents). The
surface is incomplete/dead — no composer or internal consumer is wired to it.

**Fix:** Either implement the poll-based raise (a timer comparing `ListPorts()`
snapshots that fires `PortChanged`) or drop the event from the interface for v1.5
and document hot-plug as deferred, so the contract does not advertise a no-op.

### IN-02: `AudioBuffer.PlaybackStartTime` is written but never read

**STATUS: ADDRESSED** (CR-03 scheduler realizes the seam intent; doc updated, commit `4dda142`)

**File:** `flow-lang/StandardLibrary/Audio/AudioCore.cs:34-45`;
`flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs:355-362`
**Issue:** The Phase 40 alignment seam stamps `PlaybackStartTime` the instant before
`backend.Play`, but no code reads it (confirmed: zero readers). The "real-time MIDI
scheduler keys note dispatch off this origin" is aspirational — `midiOut` (CR-03)
never consults it. It is currently dead state plus a benign cross-thread write to a
composer-shared buffer instance.

**Fix:** Acceptable to keep as a forward seam, but mark it clearly as unused-for-now
(or wire CR-03's scheduler to it). If kept, note that it mutates a shared buffer
Value from the playback thread.

### IN-03: master thread `Priority` set in initializer without the promised try/catch

**STATUS: RESOLVED** (fix commit `b973446`)

**File:** `flow-lang/Audio/MidiClock.cs:117-124`
**Issue:** `Priority = ThreadPriority.AboveNormal` is set in the `new Thread { ... }`
object initializer; the comment promises "charitable fall-through if denied," but
there is no try/catch — a `ThreadStateException`/security failure would propagate out
of `StartMaster`. Low risk on Linux (priority changes are typically no-ops there),
but the code does not match its stated charitable intent.

**Fix:** Set `Priority` after construction inside a `try { _masterThread.Priority = ...; } catch { }`,
or drop the comment's promise.

### IN-04: `_pulseCount` mixes `lock`-guarded writes with `Interlocked` reads

**STATUS: RESOLVED** (fix commit `d79a6b7`)

**File:** `flow-lang/Audio/MidiClock.cs:305-308,375,380`
**Issue:** `OnClockPulse` increments `_pulseCount++` under `lock(_slaveLock)`, while
`AtBarBoundary`/`PulseCount` read it via `Interlocked.Read` outside the lock. The
read stays atomic, so this is not a correctness blocker, but mixing the two
synchronization styles on one field is inconsistent and easy to get wrong on a later
edit (paired with WR-04, the counter ownership is muddled).

**Fix:** Pick one discipline. Use `Interlocked.Increment(ref _pulseCount)` for the
write and `Interlocked.Read` for the read (drop the lock for this field), so the
field is uniformly lock-free.

---

_Reviewed: 2026-06-06_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
