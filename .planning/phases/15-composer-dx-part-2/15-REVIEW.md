---
phase: 15-composer-dx-part-2
reviewed: 2026-04-25T00:00:00Z
depth: standard
files_reviewed: 23
files_reviewed_list:
  - flow-lang/Ast/Statements/MusicalContextStatement.cs
  - flow-lang/Runtime/MusicalContext.cs
  - flow-lang/Runtime/ExecutionContext.cs
  - flow-lang/Lexing/TokenType.cs
  - flow-lang/Lexing/SimpleLexer.cs
  - flow-lang/Parsing/Parser.cs
  - flow-lang/Interpreter/Interpreter.cs
  - flow-lang/StandardLibrary/Audio/DSP/Reverb.cs
  - flow-lang/StandardLibrary/Audio/SongRenderer.cs
  - flow-lang/StandardLibrary/Audio/SynthUtils.cs
  - flow-lang/StandardLibrary/Audio/FileIO.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/std.flow
  - flow-lang.Tests/Shared/MidiReadHelpers.cs
  - flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs
  - flow-lang.Tests/FlowScriptData.cs
  - flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs
  - flow-lang.Tests/Unit/Phase15/ReverbApplyRt60Tests.cs
  - flow-lang.Tests/Integration/Phase15/ReverbTimeRenderTests.cs
  - flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs
  - flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs
  - flow-lang.Tests/Fixtures/FlowEngineRunner.cs
  - flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs
findings:
  critical: 0
  warning: 4
  info: 6
  total: 10
status: issues_found
---

# Phase 15: Code Review Report

**Reviewed:** 2026-04-25
**Depth:** standard
**Files Reviewed:** 23
**Status:** issues_found

## Summary

Phase 15 implements two new requirements (DX-07 reverbTime context block + DX-09 euclidean swing/humanize) cleanly. The architecture is consistent with established patterns: musical context block follows the same template as `tempo`/`gain`/`pan`; the euclidean overloads use a local `new Random(seed)` per call (D-17) which correctly isolates from the global PRNG; the Schroeder RT60 → feedback formula is mathematically sound and correctly clamped.

The most consequential change is the **reseed-at-entry RNG pattern** in `SynthUtils.cs` and `FileIO.cs`. This intentional design enables byte-identical output (ROADMAP #2) but **introduces thread-safety regressions** (WR-01, WR-02): the static mutable `Rng` and `Random` fields are reassigned without synchronization on every `renderSong` / `ExportWavInternal` entry, and `NextDouble()` is called concurrently with the reassignment if two callers run in parallel. `System.Random` is not thread-safe, so simultaneous renders or exports can corrupt internal state. Phase 15 test runs are single-threaded so existing tests pass, but the contract is fragile.

Other findings are smaller: a stale `Synchronize()` recovery list missing several Phase 14/15 keywords (WR-03), a typo-prone `_position--` rewind that could underflow in a never-reached corner (WR-04), and several quality-of-life improvements (commented sentinels, magic numbers, etc.).

No security vulnerabilities, no crashes, no data-loss risks, and no logic errors that affect production audio output. The strict refactor in `Reverb.ProcessChannel` is correctly pinned by SHA-256 byte equivalence.

## Warnings

### WR-01: Static mutable RNG in `SynthUtils.Rng` is not thread-safe across reseed + use

**File:** `flow-lang/StandardLibrary/Audio/SynthUtils.cs:19,26,124`
**Issue:**
The `Rng` field is a `private static Random` reassigned via `ResetNoiseRng()` and read concurrently from `GenerateWhiteNoise()` (and via the synthesizers — e.g., `PianoSynthesizer.cs:71`). There are two thread-safety hazards:

1. **Torn reads on the field reference itself** are unlikely on x86-64 (reference writes are atomic), but the bigger problem is logical correctness: if thread A is calling `ResetNoiseRng()` while thread B is mid-`renderSong` rendering a voice and calling `Rng.NextDouble()`, B's per-sample noise stream is silently rewound mid-render — producing a buffer that does not correspond to ANY contiguous PRNG sequence. The byte-identity contract (ROADMAP #2) is therefore guaranteed only when renders are single-threaded AND not interleaved with other engines.
2. **`System.Random` is explicitly documented as NOT thread-safe.** Two threads calling `Rng.NextDouble()` concurrently can corrupt the internal state, producing duplicate or all-zero samples.

The existing tests (`EuclideanByteIdenticalTests.SameSeed_ByteIdenticalWav`, `Rt60_*` tests) construct two `FlowEngineRunner` instances sequentially, so the bug never surfaces. But: (a) the public `RenderSong` and `ExportWavInternal` are exposed via the InternalFunctionRegistry to user scripts that could be invoked from multiple threads in a host application (e.g., the LSP); (b) the `RenderSongWithTimeline` path in editor previews could race with a separate user-initiated `renderSong`.

**Fix:**
Either gate the field with a lock (matches `ExecutionContext.RandLock` pattern):

```csharp
private static readonly object _noiseRngLock = new();
private static Random Rng = new Random(SynthNoiseSeed);

public static void ResetNoiseRng()
{
    lock (_noiseRngLock) { Rng = new Random(SynthNoiseSeed); }
}

public static void GenerateWhiteNoise(float[] buffer, double amplitude)
{
    lock (_noiseRngLock)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] += (float)(amplitude * (Rng.NextDouble() * 2.0 - 1.0));
    }
}
```

Or — preferable for performance — switch to `[ThreadStatic]` and reseed per thread, which preserves byte-identical output within each thread without locking.

---

### WR-02: Static mutable `FileIO.Random` is not thread-safe across reseed + use

**File:** `flow-lang/StandardLibrary/Audio/FileIO.cs:25,79,239-240`
**Issue:**
Same class of bug as WR-01. `ExportWavInternal` reassigns `Random = new Random(DitherSeed)` at line 79, then `GenerateTpdfDither()` reads `Random.NextDouble()` from `WriteSamples` → `FloatToInt16`/`WriteInt24`. Two concurrent `writeWav` calls would race on the field reassignment AND on the unsynchronized `NextDouble()` reads. Since the static field is shared across the whole process, even unrelated background exports interleave.

The case for this being an actual concurrency hazard is stronger than WR-01 because file I/O is naturally a candidate for offloading to a background thread — a future LSP or watch-mode feature might write WAV asynchronously while the user continues editing.

**Fix:** Same options as WR-01 — either `lock` around reassign + use, or move to a non-static instance owned by the caller:

```csharp
private static void ExportWavInternal(AudioBuffer buffer, string filepath, int bitDepth)
{
    // ... validation ...
    var ditherRng = new Random(DitherSeed);  // local, not static
    using var fileStream = new FileStream(filepath, FileMode.Create, FileAccess.Write);
    using var writer = new BinaryWriter(fileStream);
    WriteRiffHeader(writer, fileSize);
    WriteFmtChunk(writer, buffer, bitDepth, bytesPerSample);
    WriteDataChunk(writer, buffer, bitDepth, bytesPerSample, ditherRng);  // pass through
}
```

This also makes the determinism contract explicit at the type level rather than implicit in static-field initialization order.

---

### WR-03: `Parser.Synchronize()` does not list `Gain`, `ReverbTime`, `For`, or `While` as recovery sync points

**File:** `flow-lang/Parsing/Parser.cs:1206-1229`
**Issue:**
The `Synchronize()` method is called after a `ParseException` to advance the parser to the next "safe" statement boundary. The recovery `is`-pattern list at lines 1217-1222 covers `Proc`, `Return`, `Use`, `Internal`, `Timesig`, `Tempo`, `Swing`, `Key`, `Dynamics`, `Rit`, `Accel`, `Pan`, `Section` — but **omits `Gain`, `ReverbTime`, `For`, `While`** (all of which are valid statement starts that were added in Phase 13/15).

Effect: after a parse error, error recovery may consume more tokens than necessary before resuming, suppressing what would otherwise be useful follow-on diagnostics. This is not a correctness bug for the happy path, but it degrades the multi-error recovery story (ErrorReporter is designed to accumulate multiple errors per pass).

**Fix:** Extend the pattern:
```csharp
if (CurrentToken.Type is TokenType.Proc or TokenType.Return
    or TokenType.Use or TokenType.Internal
    or TokenType.Timesig or TokenType.Tempo
    or TokenType.Swing or TokenType.Key
    or TokenType.Dynamics or TokenType.Rit or TokenType.Accel
    or TokenType.Pan or TokenType.Gain or TokenType.ReverbTime
    or TokenType.For or TokenType.While
    or TokenType.Section)
{
    return;
}
```

---

### WR-04: `_position--` rewind in `ScanIdentifierOrKeyword` can corrupt column tracking on multi-line tokens

**File:** `flow-lang/Lexing/SimpleLexer.cs:660-661`
**Issue:**
The note-with-duration-suffix recovery path:

```csharp
_position--;
_column--;
```

unconditionally decrements `_column` after rewinding `_position` by 1. If the duration-suffix character happened to be the first character on a new line (i.e., `_column` was reset to `1` by `Advance()`'s newline branch), then `_column` becomes `0` (or, on a subsequent character, negative). This is a latent bug: `Advance()` does not enforce a positive `_column`, and downstream `SourceLocation` consumers print column 0 in error messages.

In practice the duration suffix (`w`/`h`/`q`/`e`/`s`/`t`) is always part of an identifier scan that started on a previous character on the same line, so `_column` was incremented and decrementing it is safe. But the invariant is held only by happy accident; the loop body of `ScanIdentifierOrKeyword` doesn't bound it, and a future lexer change could break this.

**Fix:** Either guard the decrement, or capture the `_column` before the suffix character was consumed and restore it explicitly:

```csharp
// Rewind position by 1 so the duration suffix becomes a separate token
_position--;
if (_column > 1) _column--;
```

A more principled fix: in `Advance()`, save the previous `(line, column)` and let rewind restore it. Out of scope for v1.

## Info

### IN-01: `Reverb.Apply(roomSize, …)` parameter names use `float` for `roomSize`/`damping`/`mix`, but RT60 overload uses `double` for `rt60Seconds`

**File:** `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs:26,77`
**Issue:** The two `Apply` overloads have inconsistent numeric precision in their signatures:
- `Apply(AudioBuffer, float roomSize, float damping, float mix)` — all `float`
- `Apply(AudioBuffer, double rt60Seconds, float damping, float mix)` — `rt60Seconds` is `double`, others remain `float`

This is intentional (RT60 needs double precision for the Schroeder formula `Math.Pow(10.0, -3.0 * avgDelaySeconds / rt60Seconds)`), but the asymmetry is surprising and may confuse future maintainers expecting parameter-list parallelism.

**Fix:** Document the choice in an XML doc comment on the `rt60Seconds` parameter, or promote `damping` and `mix` to `double` in the new overload for consistency (the inner clamps already coerce to `float`).

---

### IN-02: `BuildEuclideanSequence` uses `Math.Max(0.0, Math.Min(1.0, v))` instead of `Math.Clamp`

**File:** `flow-lang/StandardLibrary/BuiltInFunctions.cs:1274`
**Issue:** Line 1274 uses the long form `v = Math.Max(0.0, Math.Min(1.0, v))`. Elsewhere in the same file and in `Interpreter.cs`/`Reverb.cs`, `Math.Clamp(v, 0.0, 1.0)` is the established idiom (e.g., `Interpreter.cs:189` uses `Math.Clamp(vel, 0.0, 1.0)` for the matching dynamics path).

**Fix:**
```csharp
v = Math.Clamp(v, 0.0, 1.0);
```
Equivalent semantics, more readable, consistent with the rest of the codebase.

---

### IN-03: Outdated comment in `FlowScriptData.cs` describes test scripts as "Wave 0 placeholder" after they've been promoted to real implementations

**File:** `flow-lang.Tests/FlowScriptData.cs:208,217,226`
**Issue:**
The comments around the three Phase 15 sentinel entries say:
- Line 208: `Wave 0 placeholder — body is a sentinel-only print; Plan 03 replaces the body with a real reverbTime render while preserving these two sentinels.`
- Line 217: `Wave 0 placeholder — Plan 06 replaces the body with a real euclidean swing call while preserving this sentinel.`
- Line 226: `Wave 0 placeholder — Plan 06 replaces the body with euclidean humanize + writeMidi + byte-identical-two-runs check while preserving both sentinels.`

If Plans 03 and 06 have completed (the integration tests `EuclideanByteIdenticalTests` and `ReverbTimeRenderTests` exist and pass), the "Wave 0 placeholder" wording is stale and misleading.

**Fix:** Update the comments to reflect the current state — "Phase 15 DX-07: gates real reverbTime render via test_reverb_time.flow" etc. This is comment hygiene only; no behavioral change.

---

### IN-04: `MusicalContext.ToString()` lists eight fields but `MusicalContext.Clone()` could lose a future field if the list is not kept in sync manually

**File:** `flow-lang/Runtime/MusicalContext.cs:52-62,97-109`
**Issue:** Clone, ToString, and `ExecutionContext.GetMusicalContext()`'s 8-field walk + early-break predicate (lines 193-206) are now triplicated maintenance points. Any future axis (e.g., `humanize`, `swingFeel`) requires four coordinated edits. This is a known consequence of the "8-field walk" pattern but worth flagging — a missed sync was the root cause of the F-22 regression risk that the new test pins.

**Fix:** Out of scope for v1. A future refactor could replace the per-field cascade with a `foreach` over a property-info list, or use `record` value semantics (the current class wouldn't lose much by becoming `record class MusicalContext { ... }`). The current code is correct; flagging only as a maintainability risk.

---

### IN-05: `MidiReadHelpers.ReadAllBytes` is a thin one-line wrapper around `File.ReadAllBytes` with no added value

**File:** `flow-lang.Tests/Shared/MidiReadHelpers.cs:22`
**Issue:** `public static byte[] ReadAllBytes(string midiPath) => File.ReadAllBytes(midiPath);` adds no value — call sites could call `File.ReadAllBytes` directly. The other two helpers (`GetVelocityBytes`, `GetNoteNumbers`) genuinely encapsulate DryWetMidi parsing and are useful.

**Fix:** Either delete `ReadAllBytes` (no callers in the reviewed file set use it) or rename to make its purpose clearer (e.g., `ReadMidiFileBytes` to signal "this is for cross-Fact byte comparison"). I would lean toward deletion — `EuclideanByteIdenticalTests.cs:83-84` calls `File.ReadAllBytes` directly, confirming the wrapper isn't being adopted.

---

### IN-06: Magic number `0.63` for default mid-forte velocity is duplicated across three files

**File:**
- `flow-lang/Interpreter/Interpreter.cs:189` (implicit via dynamics)
- `flow-lang/Parsing/Parser.cs:499` (`new LiteralExpression(dynToken.Location, 0.63)` fallback)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:1242` (`context.GetMusicalContext().Velocity ?? 0.63`)

**Issue:** The "default mf = 0.63" constant appears as a literal in at least three places. The `BuildEuclideanSequence` site has a comment pointing at `NoteStreamCompiler.cs:341`, which is presumably a fourth location. Drift between these copies would silently change the meaning of "no dynamics specified" in different code paths.

**Fix:** Promote to a named constant on `MusicalContext`:
```csharp
public const double DefaultVelocity = 0.63;  // mid-forte (mf)
```
Then reference `MusicalContext.DefaultVelocity` from each site. Out of v1 scope but worth a Phase 16 cleanup task.

---

_Reviewed: 2026-04-25_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
