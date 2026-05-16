---
phase: 33-sfz-orchestral-sampler
reviewed: 2026-05-16T00:00:00Z
depth: standard
files_reviewed: 38
files_reviewed_list:
  - examples/symphony/README.md
  - examples/symphony/sfz_smoke.flow
  - flow-lang.Tests/Integration/Phase33/RepoSizeTests.cs
  - flow-lang.Tests/Integration/Phase33/SfzArticulationTests.cs
  - flow-lang.Tests/Integration/Phase33/SfzBindingTests.cs
  - flow-lang.Tests/Integration/Phase33/SfzConfigTests.cs
  - flow-lang.Tests/Integration/Phase33/SfzDeterminismTests.cs
  - flow-lang.Tests/Integration/Phase33/SfzGatingTests.cs
  - flow-lang.Tests/Integration/Phase33/SfzMidiExportTests.cs
  - flow-lang.Tests/Integration/Phase33/SfzSmokeTests.cs
  - flow-lang.Tests/Integration/Phase33/SfzSymbolLookupTests.cs
  - flow-lang.Tests/Tools/Phase33FixtureGenerator.cs
  - flow-lang.Tests/Unit/Phase33/SfzLoopCrossfadeTests.cs
  - flow-lang.Tests/Unit/Phase33/SfzParserTests.cs
  - flow-lang.Tests/Unit/Phase33/SfzRegionMatchTests.cs
  - flow-lang.Tests/Unit/Phase33/SfzTypeFacts.cs
  - flow-lang/Core/FlowEngine.cs
  - flow-lang/Interpreter/Interpreter.cs
  - flow-lang/Lexing/SimpleLexer.cs
  - flow-lang/Parsing/Parser.cs
  - flow-lang/Parsing/TypeParser.cs
  - flow-lang/Runtime/ExecutionContext.cs
  - flow-lang/Runtime/FlowConfig.cs
  - flow-lang/Runtime/Value.cs
  - flow-lang/StandardLibrary/Audio/MidiExport.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzData.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzLoopMode.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzParseException.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
  - flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs
  - flow-lang/StandardLibrary/Audio/SongRenderer.cs
  - flow-lang/StandardLibrary/InternalFunctionRegistry.cs
  - flow-lang/TypeSystem/SpecialTypes/SfzType.cs
  - flow-lang/flow-lang.csproj
  - flow-lang/sfz.flow
  - flow-lang/std.flow
findings:
  critical: 2
  warning: 8
  info: 6
  total: 16
status: issues_found
---

# Phase 33: Code Review Report

**Reviewed:** 2026-05-16
**Depth:** standard
**Files Reviewed:** 38
**Status:** issues_found

## Summary

Phase 33 ships an opt-in SFZ orchestral sampler (parser, renderer with equal-power crossfade, per-engine sample cache, GM-symbol dict surface, SongRenderer dispatch branch, MIDI-export prefix routing). The architecture mirrors Phase 32 (Tuning) closely, the test coverage is substantive (SPEC-1..SPEC-8 acceptance facts plus a two-run determinism gate), and the new dispatch branch in `SongRenderer.RenderSong` is correctly additive — Phase 29 byte-identical paths only fire on non-`sampler:` strings.

That said, the renderer has TWO real defects that can crash or silently corrupt audio under malformed-but-loadable input, and a handful of correctness/security/maintainability issues warrant fixes before this surface is exposed to composers via the v1.4 milestone.

## Critical Issues

### CR-01: AssembleBody pre-attack head can read past sample bounds (IndexOutOfRangeException)

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs:277-279`

**Issue:** The "Stage 1: pre-attack" loop reads `source.Data[dst]` without bounds-checking against `source.Frames`. The clamp at line 239 (`effectiveLoopEnd = Math.Min(region.LoopEnd, source.Frames - 1)`) explicitly handles only the `LoopEnd` Pitfall 3 case; an analogous case where `region.LoopStart > source.Frames` (a malformed SFZ declares the loop start past the sample body) is unguarded. With `headEnd = Math.Min(region.LoopStart, targetFrames)` and no `Math.Min(..., source.Frames)`, the for-loop walks `dst` up to `headEnd - 1` and dereferences `source.Data[dst]` past the array bounds for any source whose frames < `region.LoopStart`. This crashes with `IndexOutOfRangeException` — a malformed `.sfz` file (or one where the user replaces a stereo sample with a shorter mono variant) will harden-crash the renderer.

For a stereo source (`source.Channels == 2`), `source.Data.Length == source.Frames * 2`, so the read starts to fail at `dst >= source.Frames * 2`, but the audio output is wrong from `dst >= source.Frames` onward (it reads into the "next channel" of an interleaved layout). Same crash class either way.

**Fix:**
```csharp
// Stage 1: pre-attack [0, LoopStart) plays once at the head.
int dst = 0;
int sourceLen = source.Frames;  // mono assumption — stereo handled separately
int headEnd = Math.Min(Math.Min(region.LoopStart, targetFrames), sourceLen);
for (; dst < headEnd; dst++) fitted[dst] = source.Data[dst];
// If region.LoopStart > source.Frames, zero-fill the gap up to authored LoopStart
// so srcReadPos = region.LoopStart is still a valid jump-target on entry to Stage 2.
int gapEnd = Math.Min(region.LoopStart, targetFrames);
for (; dst < gapEnd; dst++) fitted[dst] = 0f;
```

### CR-02: Sfz variable reassignment does not update SfzPatchRegistry (last-bound-wins violation)

**File:** `flow-lang/Interpreter/Interpreter.cs:646-657, 757-792`

**Issue:** `ExecuteVariableDeclaration` (line 650-654) writes to `SfzPatchRegistry` when `varDecl.Type is SfzType`, satisfying the "first declaration registers" half of Pitfall 10. But `ExecuteAssignment` (line 757-792) — the path for `v = newValue` after the initial declaration — does NOT mirror this. Per CLAUDE.md ("Last-bound-wins per variable name within an ExecutionContext — reassigning a same-name variable overwrites the prior registry entry, matching Flow's variable-shadowing semantics"), the contract is broken on reassignment. A composer who writes:

```flow
Sfz v = (loadSfz "/p1.sfz")
v = (loadSfz "/p2.sfz")
Buffer mix = (renderSong song "sampler:v")  // renders p1, not p2
```

silently gets the wrong patch. The dispatch error message (which would normally guide the composer) doesn't fire because the registry HAS an entry — it's just stale.

**Fix:** Add a parallel update branch to `ExecuteAssignment` after the existing `_context.SetVariable(...)` call:
```csharp
// Phase 33 D-12 / Pitfall 10: keep the SfzPatchRegistry in sync with reassigns.
if (existingValue.Type is FlowLang.TypeSystem.SpecialTypes.SfzType
    && newValue.Data is FlowLang.StandardLibrary.Audio.Sfz.SfzData newSfzData)
{
    _context.SfzPatchRegistry[assignment.Name] = newSfzData;
}
```

## Warnings

### WR-01: Stereo SFZ samples render incorrectly (interleaved-channel confusion)

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs:247, 264, 270, 279, 305, 320-324`

**Issue:** `AssembleBody` treats `source.Data` as a flat mono stream throughout, indexing it with frame numbers (`source.Data[srcReadPos]`, `source.Data[i]`, etc). `FileIO.LoadWavInternal` happily loads stereo WAVs (channels = 2), and `AudioBuffer.Data` is interleaved L,R,L,R,... for stereo. When a stereo sample is loaded, frame N corresponds to `Data[N*2]` (left) and `Data[N*2+1]` (right), NOT `Data[N]`. The current code reads what is effectively a 2:1 ratio of L-only samples interspersed with stale R-channel data, producing distorted audio. VSCO-CE's commercial sample libraries are typically stereo for keys/strings/orchestral pads.

The output is also forced through `ToMonoBuffer` / `ToStereoBufferWithPan` based only on pan, not on source channel count — losing native stereo content even when no pan is requested.

**Fix:** Either
- (a) Detect `source.Channels > 1` at the top of `Render` and downmix the source to mono before `AssembleBody` (loses stereo nuance but is correct), or
- (b) Make `AssembleBody` channel-aware and emit a stereo `fitted` buffer when source is stereo.

Option (a) is the minimum-viable fix and matches Phase 29's `SampledInstrumentRenderer` behavior.

### WR-02: SFZ sample paths bypass directory traversal guards

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs:145, SfzBuiltins.cs:215, 234`

**Issue:** Three file-load sites accept paths without traversal-guard validation:

1. `SfzSampleCache.EagerLoad` line 145: `string fullPath = Path.Combine(patch.BasePath, region.SamplePath);` — if `region.SamplePath` is absolute (e.g., `/etc/passwd`) or contains `..` traversal (e.g., `../../sensitive.wav`), `Path.Combine` either ignores BasePath (absolute case) or resolves the traversal upward.
2. `SfzBuiltins.LoadSfzSymbol` line 215: `File.ReadAllText(absolutePath)` — `Path.Combine(resolvedRoot, relativePath)` similarly accepts `../` in the dict-mapped relative path.
3. `SfzBuiltins.LoadSfzString` line 234: composer-supplied absolute path passed through verbatim.

The threat surface is limited (audio file read for cases 1+3, text file read for case 2 — but the SFZ parser silently ignores unknown opcodes, so the file content is mostly discarded after read). However, a malicious .sfz fixture downloaded from an untrusted source could be used to enumerate files on the user's machine via the parse-error messages (which echo path strings back to stderr). The String overload (case 3) is a composer-supplied path so traversal is by design — but the Symbol overload's dict-resolved path SHOULD be constrained to `sfz_root` per Phase 33's spec.

**Fix:** For cases 1+2, normalize and verify the resolved path is rooted under the expected base:
```csharp
string fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
string baseFull = Path.GetFullPath(basePath);
if (!fullPath.StartsWith(baseFull + Path.DirectorySeparatorChar, StringComparison.Ordinal)
    && !string.Equals(fullPath, baseFull, StringComparison.Ordinal))
    throw new InvalidOperationException(
        $"SFZ sample path '{relativePath}' escapes patch base directory '{baseFull}'");
```

### WR-03: SfzParser does not strip leading whitespace after `=`

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs:269-304`

**Issue:** The opcode key-value parser at lines 269 (`var key = trimmed[cursor..eq].Trim();`) strips leading/trailing whitespace from the KEY, but the value (line 304) only `TrimEnd()`s. For input `volume= -6` (space after `=`), the value becomes `" -6"` (with leading space). `int.TryParse` and `double.TryParse` with `NumberStyles.None` reject leading whitespace, so the value silently falls back to the default (1.0 linear / 0 dB). For `sample= foo.wav`, the leading space becomes part of the resolved file path → `File.Exists` returns false → silent missing-sample at render time. The SFZ format spec is forgiving about whitespace; many real-world .sfz files in commercial libraries have `key= value`-style spacing.

**Fix:** Strip leading whitespace as well:
```csharp
var value = trimmed[valStart..valEnd].Trim();
```

### WR-04: First non-header line silently captured as patch Description

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs:156-162`

**Issue:** The Description-capture branch fires on the first non-comment, non-blank, non-header line — but a typical SFZ file's first non-header line is an opcode like `ampeg_attack=0.005` (when the file opens with `<global>`). This line becomes `SfzData.Description`, which is then injected into every `RenderingDiagnostics.WarnOnce` sentinel key (e.g., `sfz:opcode:ampeg_attack=0.005:fil_type`) and surfaces in composer-facing error messages. The smoke fixture's structure (`<global>` first) reproduces this — `Description` ends up as `"ampeg_attack=0.005"`, not the filename.

The sentinel-key collision risk is low in practice (each file's first opcode tends to be unique enough), but the diagnostic UX is degraded — composers see meaningless string literals where they expected file names.

**Fix:** Restrict description capture to comment lines or a more deliberate header. Simplest robust fix: drop the heuristic and fall back to `Path.GetFileName(filePath)` (line 397's existing fallback) for ALL files:
```csharp
description = Path.GetFileName(filePath);
```
…or check that the captured line "looks like" a comment-style description (no `=` character).

### WR-05: Nearest-pitch fallback regions are not eager-loaded

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs:166-189, SfzRenderer.cs:127-150`

**Issue:** `CollectRegionsFromBar` only adds regions that `patch.Grid[midi, vel]` directly resolves to (line 174: `var region = patch.Grid[midi, vel]; if (region is not null) needed.Add(region);`). When the grid cell is null (note pitch outside ALL region coverage), the comment at line 176-177 says "render-time nearest-pitch fallback will load the fallback region's WAV on first render" — but that's NOT what happens. `SfzRenderer.Render` calls `_cache.GetVarispeed(...)` (line 158); if the underlying raw cache lacks the sample, `GetVarispeed` returns null (SfzSampleCache.cs:83), the renderer fires the `[sfz] sample '...' not loaded` advisory and renders silence. There is no "load on first render" code path.

The result: a song with a melody outside the patch's covered range silently degenerates to a sequence of silences, with a single "not loaded" advisory the composer may not associate with the misaligned range. Nearest-pitch fallback works in unit tests because the test patches happen to declare regions whose samples are explicitly loaded, but production `loadSfz` patches will hit this when the composer's range exceeds the patch's coverage.

**Fix:** In `CollectRegionsFromBar`, when `patch.Grid[midi, vel]` is null, walk the same `SortedByPitch` + `FindAnyRegionAtPitch` logic that `SfzRenderer.Render` uses, and add the fall-through region to `needed` so its WAV is loaded.

### WR-06: WarnOnce dedup is process-global — per-engine resets do not clear it

**File:** `flow-lang/Diagnostics/RenderingDiagnostics.cs:21, flow-lang/Runtime/ExecutionContext.cs:124-132`

**Issue:** `RenderingDiagnostics._emitted` is a static HashSet (process-wide). The `SfzDiagnostics` field on `ExecutionContext` (lines 124-132) advertises itself as "per-context rather than per-process so each FlowEngine instance gets a fresh slate" — but it is never actually used. All Phase 33 advisory sites (`SfzParser.cs:239,263,309,319,341,483,524,541`, `SfzRenderer.cs:111,145,161`, `SfzBuiltins.cs:260`) call `RenderingDiagnostics.WarnOnce` (the global helper), not the per-context set. So:

- `ExecutionContext.SfzDiagnostics` is dead code.
- The per-process WarnOnce dedup means a one-shot REPL session that fires "missing sfz_root" and then has the user fix their config will NOT re-emit the warning — even though the underlying state has changed. Tests rely on `RenderingDiagnostics.ResetForTesting()` in setup/teardown to work around this; production usage has no such reset hook.

**Fix:** Either
- (a) Wire the SFZ advisories through `ctx.SfzDiagnostics` (a per-context HashSet) instead of `RenderingDiagnostics.WarnOnce`, OR
- (b) Remove the `SfzDiagnostics` field from `ExecutionContext` since it is unused, AND document the "warnings stay quiet for the rest of the process once fired" tradeoff.

### WR-07: `firstIteration` local variable is set but never read

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs:295, 331`

**Issue:** `bool firstIteration = true;` is assigned at line 295 and updated at line 331 (`firstIteration = false;`) but never read inside the loop body. The comment block at lines 290-293 hints that the first iteration was supposed to be special-cased ("First iteration is special: it plays the FULL body once...including the entire pre-crossfade region"). Either the special case logic was removed but the bookkeeping variable was left behind, or the special case was supposed to be implemented but never landed. Either way the current loop is correct (every iteration treats the body identically), so this is dead code.

**Fix:** Remove the variable and the corresponding `firstIteration = false;` assignment. If a future change re-introduces a first-iteration special case, restore them then.

### WR-08: SFZ patch eager-load idempotency keys can collide

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs:112-113`

**Issue:** `string key = $"sfz:{patch.GetHashCode()}:{song.GetHashCode()}";` — `SfzData` and `SongData` are records with structural `GetHashCode`. A 32-bit hash composed of two 32-bit hashes is reasonably collision-resistant, but the failure mode is "the second EagerLoad call silently skips even though the (patch, song) pair is genuinely different" — the wrong sample set ends up loaded for the second song. The probability is low but the silent-corruption posture of the failure is bad: no exception, no advisory, just wrong audio.

**Fix:** Compose the key from object identity tokens rather than structural hashes:
```csharp
string key = $"sfz:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(patch)}:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(song)}";
```
…or use `ConditionalWeakTable<(SfzData, SongData), object>` if reference identity is the desired contract per the SfzData reference-identity comment in `Value.cs:71`.

## Info

### IN-01: SongRenderer.RenderSongWithSfz throws "no SfzSampleCache published" but tests construct caches directly

**File:** `flow-lang/StandardLibrary/Audio/SongRenderer.cs:502-505`

**Issue:** The throw assumes any sampler:NAME caller goes through `FlowEngine`. The unit tests in `SfzLoopCrossfadeTests` and `SfzArticulationTests` deliberately construct `SfzSampleCache` directly and call `SfzRenderer.Render` (not `SongRenderer.RenderSong`), bypassing the static. That works today because tests don't exercise this throw site; but if a future test wires a `sampler:NAME` SongData call without a FlowEngine, it'll hit a confusing error message that lies (the cache exists, just wasn't published statically).

**Fix:** Reword to "FlowEngine.CurrentSfzSampleCache was not set — sampler:NAME dispatch requires construction through FlowEngine.RegisterContextDependent" (or similar — surface the actual contract).

### IN-02: SfzParser strict numeric rejects valid SFZ values

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs:518`

**Issue:** `int.TryParse(raw, NumberStyles.None, ...)` rejects leading `+` (e.g., `lokey=+60`). The SFZ format spec allows signed integer notation. For `volume`/`pan`/`ampeg_*`, the double parser uses `NumberStyles.Float` which DOES allow leading sign — so the inconsistency is between integer opcodes (no sign) and floating opcodes (signed allowed). This will reject some valid `.sfz` files in the wild.

**Fix:** Use `NumberStyles.Integer` (allows leading sign) instead of `NumberStyles.None` for integer opcodes:
```csharp
if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
```

### IN-03: SfzBuiltins error path for sfz_root=="" hits dedup'd WarnOnce forever

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs:254-268`

**Issue:** If `FlowConfig.Active.SfzRoot == ""` (not null but empty), the predicate `string.IsNullOrEmpty(fromConfig)` is true, the WarnOnce fires (process-global dedup), and the throw fires. After the user fixes their config and re-runs, the WarnOnce is suppressed (it has the same sentinel key) — but the throw is per-call, so they DO get the exception. Only the stderr advisory is squelched. Composers who fix-and-re-run during the same REPL session see the exception without the helpful guidance. (Same root cause as WR-06.)

**Fix:** See WR-06.

### IN-04: SfzData.SortedByPitch enumeration order before Array.Sort is HashSet-dependent

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs:382-393`

**Issue:** `pitchSet` is a `HashSet<int>`; `foreach (var p in pitchSet) sortedByPitch[idx++] = p;` writes pitches in HashSet enumeration order, which `Array.Sort` then re-orders by ascending int. The end result IS deterministic (the sort makes it so), but the temporary intermediate ordering is not — and a future refactor that drops the explicit `Array.Sort` would silently re-introduce non-determinism. The two-run cmp-clean determinism contract is currently satisfied, but the code is one careless edit away from breaking it.

**Fix:** Build `sortedByPitch` directly with `pitchSet.OrderBy(p => p).ToArray()` so the determinism is structural rather than incidental.

### IN-05: Description/diagnostics reference patchDescription that may itself be the filename, leaking absolute paths to stderr

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs:217, 236`

**Issue:** `patchDescription: Path.GetFileNameWithoutExtension(absolutePath)` strips the extension but the resulting basename is then injected into all WarnOnce sentinel keys + composer-facing messages. For absolute-path overload usage, the basename leaks the user's filesystem layout to stderr. (`/home/alice/.flow/samples/private/MyExperiment.sfz` → `[sfz] unrecognized opcode 'foo' in 'MyExperiment'`.) Low severity — the basename is much less revealing than the full path — but worth noting if any of these messages get logged/uploaded.

**Fix:** No code change required; document the leak in the SFZ surface XML doc.

### IN-06: SfzParser comment says "13 opcodes" but the whitelist is 14

**File:** `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs:146, 240, 264, 311, 319`

**Issue:** Several comments and advisory messages reference "13 opcodes" as the surface size. The actual whitelist is 14 (the 13 from the spec + `default_path` per the VSCO-CONTROL-DECISION audit). Specifically: the AllKnownOpcodes_Parse test (`SfzParserTests.cs:117-118`) declares "all 13 base opcodes (the 14th, default_path, belongs to <control>)" — so the count distinction is intentional. The XML doc on SfzParser.cs:20 says "Whitelist — 14 opcodes ... (extended from SPEC-3's 13)". This is consistent. But the inline comment on line 146 says "We use the simple 'first //' rule per the 13-opcode subset" — that wording is stale (no longer just 13). Cosmetic.

**Fix:** Update line 146's comment to reference 14 opcodes (or simply "the current whitelist") for consistency.

---

_Reviewed: 2026-05-16_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
