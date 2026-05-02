---
phase: 260502-lum-add-pretty-printing-for-printing-out-buf
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/StandardLibrary/BufferPrinter.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - tests/test_buffer_printing.flow
autonomous: true
requirements:
  - PRETTY-BUFFER-01
  - HEX-DUMP-01

must_haves:
  truths:
    - "Calling `(prettyBuffer buf)` from a .flow script prints a multi-line, human-readable summary of an AudioBuffer (frames, channels, sample rate, duration, peak, RMS) followed by a small ASCII waveform."
    - "Calling `(bufferHex buf)` from a .flow script prints the buffer's underlying float samples as little-endian IEEE-754 32-bit bytes in classic 16-bytes-per-row hex-editor format with offset prefix and ASCII gutter."
    - "Calling `(bufferHex buf offset length)` prints only the slice starting at byte offset `offset` for at most `length` bytes (clamped silently to the buffer's byte range; never throws on out-of-range — matches the project's charitable-interpretation memory)."
    - "Both functions return Void (they are side-effect printers, like the existing `print` and `visualize` builtins)."
    - "Both functions are in S-expression call form `(prettyBuffer buf)` / `(bufferHex buf)` — no infix syntax introduced."
    - "On an empty buffer (Frames == 0) both functions print a clear `(empty buffer)` line and return Void without throwing."
    - "All existing tests in tests/ continue to pass after the change (no regression in builtin registration or overload resolution)."
  artifacts:
    - path: "flow-lang/StandardLibrary/BufferPrinter.cs"
      provides: "C# implementation of prettyBuffer + bufferHex builtins (PrettyBuffer, BufferHex, BufferHexSlice methods + Register entry point)"
      min_lines: 120
      contains: "public static class BufferPrinter"
    - path: "flow-lang/StandardLibrary/BuiltInFunctions.cs"
      provides: "Calls BufferPrinter.Register(registry) from RegisterAllImplementations so both builtins are wired into the registry."
      contains: "BufferPrinter.Register"
    - path: "tests/test_buffer_printing.flow"
      provides: "End-to-end test exercising prettyBuffer + bufferHex (and bufferHex with offset/length) on a small constructed buffer."
      min_lines: 25
      contains: "prettyBuffer"
  key_links:
    - from: "flow-lang/StandardLibrary/BuiltInFunctions.cs"
      to: "flow-lang/StandardLibrary/BufferPrinter.cs"
      via: "BufferPrinter.Register(registry) call inside RegisterAllImplementations(InternalFunctionRegistry registry)"
      pattern: "BufferPrinter\\.Register"
    - from: "tests/test_buffer_printing.flow"
      to: "flow-lang/StandardLibrary/BufferPrinter.cs"
      via: "Calls (prettyBuffer buf) and (bufferHex buf) builtins resolved through InternalFunctionRegistry"
      pattern: "prettyBuffer|bufferHex"
    - from: "flow-lang/StandardLibrary/BufferPrinter.cs"
      to: "flow-lang/StandardLibrary/Audio/AudioCore.cs"
      via: "args[0].As<AudioBuffer>() and reads .Data, .Frames, .Channels, .SampleRate"
      pattern: "AudioBuffer"
---

<objective>
Add two new built-in printer functions for the `Buffer` type:

1. **`prettyBuffer(Buffer)`** — multi-line, human-readable summary (frames,
   channels, sample rate, duration, peak, RMS) plus a compact ASCII waveform.
2. **`bufferHex(Buffer)`** and **`bufferHex(Buffer, Int, Int)`** — classic
   hex-editor dump of the buffer's underlying float samples encoded as
   little-endian IEEE-754 32-bit bytes (16 bytes per row, offset prefix,
   ASCII gutter).

Purpose: Make Flow's `Buffer` type debuggable. Today `(print (str buf))` is
useless for buffers (and `str(Buffer)` is not even registered, so it would
throw). Composers iterating on synth code, sample import, or DSP pipelines
need to inspect buffers without exporting WAV every time.

Output:
- `flow-lang/StandardLibrary/BufferPrinter.cs` — implementation
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — one-line registration call
- `tests/test_buffer_printing.flow` — exercises both builtins
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md

<interfaces>
<!-- Key types and contracts the executor needs. Use these directly. -->

From flow-lang/StandardLibrary/Audio/AudioCore.cs (AudioBuffer):
```csharp
public class AudioBuffer
{
    public float[] Data { get; }       // interleaved LRLRLR... for stereo
    public int SampleRate { get; }     // e.g. 44100, 48000
    public int Channels { get; }       // 1 = mono, 2 = stereo
    public int Frames { get; }         // Data.Length == Frames * Channels
    public AudioBuffer(int frames, int channels, int sampleRate);
    public float GetSample(int frame, int channel);
    public void SetSample(int frame, int channel, float value);
    public void Fill(float value);
}
```

From flow-lang/Runtime/Value.cs:
```csharp
public class Value {
    public object? Data { get; }
    public FlowType Type { get; }
    public T As<T>();                            // throws InvalidCastException on mismatch
    public static Value Buffer(object? value);   // BufferType.Instance
    public static Value Void();                  // VoidType.Instance
    public static Value Int(int);
    public static Value String(string);
}
```

From flow-lang/StandardLibrary/InternalFunctionRegistry.cs (call shape — see existing
VisualizationFunctions.Register for the canonical pattern):
```csharp
public class InternalFunctionRegistry {
    public virtual void Register(
        string name,
        FunctionSignature signature,
        Func<IReadOnlyList<Value>, Value> implementation);
}
public record FunctionSignature(
    string Name,
    IReadOnlyList<FlowType> ParamTypes,
    bool IsVarArgs = false);
```

From flow-lang/TypeSystem/PrimitiveTypes/:
- `BufferType.Instance`
- `IntType.Instance`
- `VoidType.Instance`

Reference template (existing pattern that does the same thing for Sequences and Buffers):
flow-lang/StandardLibrary/VisualizationFunctions.cs — has `Register(InternalFunctionRegistry)`
that registers `visualize(Sequence)` and `visualize(Buffer)`. `VisualizeBuffer` already
shows the canonical "downmix to mono → ASCII grid → Console.Write" approach. Reuse the
same shape for `prettyBuffer`.

Wiring point in BuiltInFunctions.cs (line ~46, inside `RegisterAllImplementations(InternalFunctionRegistry)`):
```csharp
VisualizationFunctions.Register(registry);   // existing
BufferPrinter.Register(registry);            // ADD THIS
Composition.PolyrhythmFunctions.Register(registry);
```
</interfaces>

Test convention: tests are .flow scripts in `tests/`. There is no unit-test
framework — a test "passes" if `dotnet run --project flow-interpreter tests/<name>.flow`
runs to completion with exit code 0. Use `(print "...")` lines to delimit
sections and make output skimmable.
</context>

<tasks>

<task type="auto">
  <name>Task 1: Implement BufferPrinter.cs (prettyBuffer + bufferHex)</name>
  <files>flow-lang/StandardLibrary/BufferPrinter.cs</files>
  <action>
Create `flow-lang/StandardLibrary/BufferPrinter.cs` with namespace
`FlowLang.StandardLibrary` and `public static class BufferPrinter`.

Required signatures:
- `prettyBuffer(Buffer) -> Void`
- `bufferHex(Buffer) -> Void`
- `bufferHex(Buffer, Int, Int) -> Void`  (offset, length in bytes)

Public surface:
```csharp
public static void Register(InternalFunctionRegistry registry);
public static Value PrettyBuffer(IReadOnlyList<Value> args);
public static Value BufferHex(IReadOnlyList<Value> args);
public static Value BufferHexSlice(IReadOnlyList<Value> args);
```

Implementation rules (follow these exactly):

**`PrettyBuffer`:**
1. `var buf = args[0].As<AudioBuffer>();`
2. If `buf.Frames == 0`: `Console.WriteLine("(empty buffer)");` return Void.
3. Compute: durationSeconds = `(double)buf.Frames / buf.SampleRate`;
   peak = max absolute sample over `buf.Data`; rms = sqrt(mean(square(samples))).
   Convert peak/RMS to dBFS with the convention `20*log10(max(value, 1e-12))`
   (clamp the floor so silent buffers don't print -Infinity — print "-inf dB" instead).
4. Print a header block (use `Console.WriteLine`):
   ```
   Buffer:
     frames      : <Frames>
     channels    : <Channels> (mono|stereo|N-channel)
     sample rate : <SampleRate> Hz
     duration    : <duration:F3> s
     peak        : <peak:F4> (<peakDb:F2> dBFS)
     rms         : <rms:F4>  (<rmsDb:F2> dBFS)
   ```
5. Then a 60-column ASCII waveform: downmix to mono (mean of channels per frame),
   bucket into 60 columns over `Frames`, render with characters chosen by amplitude
   in this lookup (centered on a midline):
   - Track per-bucket min/max of the mono signal.
   - 11 rows tall (5 above midline, 1 midline, 5 below). Use `*` for filled cells,
     `-` for the midline elsewhere, space otherwise. Frame the waveform with `|`
     on each side.
   Reuse the algorithm in `VisualizationFunctions.VisualizeBuffer` as a starting
   point (downmix loop, subsampling step, min/max bucketing) — but keep the
   waveform smaller (60×11 instead of 80×20) so prettyBuffer fits in a typical
   terminal alongside the header.

**`BufferHex` (no slice args):**
1. `var buf = args[0].As<AudioBuffer>();`
2. If `buf.Frames == 0`: `Console.WriteLine("(empty buffer)"); return Value.Void();`
3. Encode samples as little-endian IEEE-754 32-bit:
   `byte[] bytes = MemoryMarshal.AsBytes(buf.Data.AsSpan()).ToArray();`
   (System is already little-endian on every platform Flow targets;
   `BitConverter.IsLittleEndian` is true. Add a `Debug.Assert(BitConverter.IsLittleEndian)`
   for documentation per the project's "silent and documented assumptions" memory.)
4. Delegate to a private `DumpHex(byte[] bytes, long absoluteOffset, int length)` helper
   that prints `Math.Min(length, bytes.Length - 0)` bytes starting at index 0 of `bytes`
   with `absoluteOffset = 0`.

**`BufferHexSlice` (offset, length args):**
1. Same encoding step.
2. Read `int offset = args[1].As<int>(); int length = args[2].As<int>();`
3. **Charitable clamping** (per the project's charitable-interpretation memory —
   no exceptions for out-of-range; document the assumption inline):
   - if `offset < 0`: silently treat as 0
   - if `offset >= bytes.Length`: print `(empty slice)` and return Void
   - if `length < 0`: silently treat as 0
   - clamp `length` to `bytes.Length - offset`
4. Call `DumpHex` with the clamped offset and length, passing
   `absoluteOffset = (long)offset` so the printed offset column starts at the
   user-requested offset (not 0).

**Private `DumpHex(byte[] bytes, int startIndex, long absoluteOffset, int length)`:**
- 16 bytes per row.
- Each row format (use a `StringBuilder` and a final single `Console.Write`):
  `OOOOOOOO  bb bb bb bb bb bb bb bb  bb bb bb bb bb bb bb bb  |ascii.gutter....|`
  - `OOOOOOOO` = 8-hex-digit row offset = `absoluteOffset + (rowStart - startIndex)`
    formatted as `X8` (uppercase or lowercase — pick lowercase to match the spec
    example in the task brief).
  - Bytes printed as lowercase 2-digit hex (`x2`), space-separated, with a double
    space between byte 7 and byte 8.
  - ASCII gutter: printable bytes (0x20..0x7E) printed as-is, all others as `.`.
  - Final partial row must align: pad missing hex slots with three spaces each
    (`"   "`) so the ASCII gutter still lines up. ASCII gutter only includes the
    bytes actually present.
- After the last data row, print one final line with the end offset:
  `OOOOOOOO` (8 lowercase hex digits) followed by a newline. This makes it easy
  to see the total byte count at a glance.

Imports required: `System.Diagnostics`, `System.Runtime.InteropServices`,
`System.Text`, `FlowLang.Runtime`, `FlowLang.StandardLibrary.Audio`,
`FlowLang.TypeSystem`, `FlowLang.TypeSystem.PrimitiveTypes`.

**`Register(InternalFunctionRegistry registry)`:**
```csharp
var prettySig = new FunctionSignature("prettyBuffer", [BufferType.Instance]);
registry.Register("prettyBuffer", prettySig, PrettyBuffer);

var hexSig = new FunctionSignature("bufferHex", [BufferType.Instance]);
registry.Register("bufferHex", hexSig, BufferHex);

var hexSliceSig = new FunctionSignature(
    "bufferHex",
    [BufferType.Instance, IntType.Instance, IntType.Instance]);
registry.Register("bufferHex", hexSliceSig, BufferHexSlice);
```
The overload resolver disambiguates by arity (1 vs 3 args) — no Void-wildcard
gymnastics needed.

DO NOT touch `Value.ToString()` or the existing `str(...)` overloads.
This plan is purely additive — adding new builtins, not changing how `print` /
`str` render Buffer. (The user asked for new explicit builtins, and changing
`str(Buffer)` would alter behavior for existing scripts.)
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp && dotnet build flow-lang/flow-lang.csproj -nologo -v q 2>&1 | tail -5</automated>
  </verify>
  <done>
flow-lang/StandardLibrary/BufferPrinter.cs exists, compiles cleanly, and exports
`BufferPrinter.Register`, `BufferPrinter.PrettyBuffer`, `BufferPrinter.BufferHex`,
`BufferPrinter.BufferHexSlice` matching the signatures above. No changes to
existing files yet — that is task 2.
  </done>
</task>

<task type="auto">
  <name>Task 2: Wire BufferPrinter into BuiltInFunctions + write the test script</name>
  <files>flow-lang/StandardLibrary/BuiltInFunctions.cs, tests/test_buffer_printing.flow</files>
  <action>

**Edit `flow-lang/StandardLibrary/BuiltInFunctions.cs`:**

In `RegisterAllImplementations(InternalFunctionRegistry registry)` (around line 33-50)
add a single line:

```csharp
VisualizationFunctions.Register(registry);
BufferPrinter.Register(registry);            // <-- ADD
Composition.PolyrhythmFunctions.Register(registry);
```

That is the ONLY change to BuiltInFunctions.cs. Do not modify `RegisterSignaturesOnly`
— it already calls `RegisterAllImplementations(proxy)`, so the new registrations
flow through to the LSP automatically.

**Create `tests/test_buffer_printing.flow`:**

```flow
Note: Test prettyBuffer + bufferHex builtins
use "@std"
use "@audio"

(print "=== Test prettyBuffer + bufferHex ===")

Note: Build a small mono buffer at 48 kHz, 64 frames (256 bytes encoded)
Buffer buf = (createBuffer 64 1 48000)

Note: Fill with a simple ramp using setSample so we have non-trivial bytes
Int i = 0
(setSample buf 0 0 0.0)
(setSample buf 1 0 0.25)
(setSample buf 2 0 0.5)
(setSample buf 3 0 0.75)
(setSample buf 4 0 1.0)
(setSample buf 5 0 negHalf)
(print "--- prettyBuffer (ramp) ---")
(prettyBuffer buf)

(print "--- bufferHex (full) ---")
(bufferHex buf)

(print "--- bufferHex (offset 16, length 32) ---")
(bufferHex buf 16 32)

Note: Out-of-range slice should not throw (charitable interpretation)
(print "--- bufferHex (offset 9999, length 16) — silent empty slice ---")
(bufferHex buf 9999 16)

Note: Empty buffer should print (empty buffer) and not throw
Buffer empty = (createBuffer 0 1 48000)
(print "--- prettyBuffer (empty) ---")
(prettyBuffer empty)
(print "--- bufferHex (empty) ---")
(bufferHex empty)

(print "=== Done ===")
```

Note: `setSample` takes `Buffer, Int frame, Int channel, Double value`; `0.25`
parses as Double in Flow. The `negHalf` token may need to be a literal — if Flow
does not support `-0.5` directly in the lexer, declare it via `Double negHalf = (sub 0.0 0.5)`
at the top, OR just use `0.5` for sample 5. Adjust to match the actual lexer
behavior; the goal is a non-zero, non-uniform set of samples, not specific values.
Run the test and look at the output to verify it parses; adjust syntax if Flow
rejects any literal.
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp && dotnet build -nologo -v q 2>&1 | tail -3 && dotnet run --project flow-interpreter tests/test_buffer_printing.flow 2>&1 | tail -40</automated>
  </verify>
  <done>
- BuiltInFunctions.cs has exactly one new line (`BufferPrinter.Register(registry);`)
  inside `RegisterAllImplementations(InternalFunctionRegistry registry)`.
- `tests/test_buffer_printing.flow` runs to completion with exit code 0.
- The output contains: a multi-line "Buffer:" header from prettyBuffer; an
  ASCII waveform line; at least one hex-dump row matching the regex
  `^[0-9a-f]{8}  ([0-9a-f]{2} ){8} ([0-9a-f]{2} ?){1,8}.*\|.*\|$`; and
  the "(empty buffer)" line for both empty-buffer cases.
- `dotnet build` of the whole solution still succeeds (no other tests broken).
  </done>
</task>

</tasks>

<verification>
After both tasks, run the full quick-smoke set to confirm no regressions:

```bash
cd /home/noah/Desktop/projects/flow-sharp
dotnet build -nologo -v q
dotnet run --project flow-interpreter tests/test_buffer_printing.flow
# Spot-check a couple of unrelated tests still pass:
dotnet run --project flow-interpreter tests/test_fade.flow
dotnet run --project flow-interpreter tests/test_math.flow
```

Manual eyeball of the hex output: the first row should look like

```
00000000  00 00 00 00 00 00 80 3e  00 00 00 3f 00 00 40 3f  |.......>...?..@?|
```

(IEEE-754 little-endian 0.0, 0.25, 0.5, 0.75 — the exact bytes are
`00 00 00 00`, `00 00 80 3e`, `00 00 00 3f`, `00 00 40 3f`). If those four
4-byte groups appear in order, the hex encoding is correct.
</verification>

<success_criteria>
- `(prettyBuffer buf)` and `(bufferHex buf)` are callable from any .flow script
  after `use "@audio"` (or even without — they don't depend on @audio, just on
  having a Buffer value).
- `bufferHex` arity-3 overload (`(bufferHex buf offset length)`) is resolved by
  the overload resolver by arity match.
- Out-of-range offset/length silently clamps; never throws.
- Empty buffer prints `(empty buffer)` for both functions; never throws.
- No regression in any existing test in `tests/`.
- No external dependency added (still pure C# + System.*).
- No change to `Value.ToString()` or existing `str(...)` overloads — purely
  additive surface area.
</success_criteria>

<output>
After completion, create
`.planning/quick/260502-lum-add-pretty-printing-for-printing-out-buf/260502-lum-SUMMARY.md`
documenting:
- Final function signatures registered
- Sample output (one prettyBuffer block, one hex dump row, one slice example)
- Any deviations from the plan (e.g. literal-syntax adjustments in the test script)
</output>
