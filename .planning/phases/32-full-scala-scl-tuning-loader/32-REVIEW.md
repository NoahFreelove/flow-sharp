---
phase: 32-full-scala-scl-tuning-loader
reviewed: 2026-05-14T00:00:00Z
depth: standard
files_reviewed: 27
files_reviewed_list:
  - flow-lang/Ast/Statements/TuningContextStatement.cs
  - flow-lang/Core/FlowEngine.cs
  - flow-lang/Runtime/ExecutionContext.cs
  - flow-lang/Runtime/MusicalContext.cs
  - flow-lang/StandardLibrary/Audio/MidiExport.cs
  - flow-lang/StandardLibrary/Audio/PitchConversion.cs
  - flow-lang/StandardLibrary/Audio/SongRenderer.cs
  - flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs
  - flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs
  - flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs
  - flow-lang/StandardLibrary/Audio/Tuning/ScalaKbm.cs
  - flow-lang/StandardLibrary/Audio/Tuning/ScalaKbmParseException.cs
  - flow-lang/StandardLibrary/Audio/Tuning/ScalaKbmParser.cs
  - flow-lang/StandardLibrary/Audio/Tuning/ScalaParseException.cs
  - flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs
  - flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs
  - flow-lang/TypeSystem/SpecialTypes/TuningType.cs
  - flow-lang.Tests/Integration/Phase32/LastWinsTuningTests.cs
  - flow-lang.Tests/Integration/Phase32/ScalaTuningDeterminismTests.cs
  - flow-lang.Tests/Unit/Phase32/LoadScalaBuiltinFacts.cs
  - flow-lang.Tests/Unit/Phase32/NonOctavePitchFacts.cs
  - flow-lang.Tests/Unit/Phase32/RenderTuningExtensionFacts.cs
  - flow-lang.Tests/Unit/Phase32/ScalaKbmParserFacts.cs
  - flow-lang.Tests/Unit/Phase32/ScalaParserErrorFacts.cs
  - flow-lang.Tests/Unit/Phase32/ScalaParserFacts.cs
  - flow-lang.Tests/Unit/Phase32/TuningContextStatementFacts.cs
  - flow-lang.Tests/Unit/Phase32/TuningStackFacts.cs
  - flow-lang.Tests/Unit/Phase32/TuningTypeFacts.cs
  - flow-lang.Tests/Unit/Phase32/UnmappedKeyAdvisoryFacts.cs
findings:
  critical: 0
  warning: 8
  info: 6
  total: 14
status: issues_found
---

# Phase 32: Code Review Report

**Reviewed:** 2026-05-14
**Depth:** standard
**Files Reviewed:** 27
**Status:** issues_found

## Summary

The Phase 32 implementation of the Scala `.scl` / `.kbm` tuning loader is well-architected. Parser correctness on the happy paths is solid, the Pitfall 3 mutual-exclusion guard in `PitchConversion.NoteToFrequency` is correctly applied with the documented defense-in-depth (early-return-when-Custom-non-null PLUS `Custom is null` guard on the 12-TET short-circuit), `try/finally` around the tuning-block body in `Interpreter.ExecuteTuningContext` correctly preserves the Pitfall 2 contract, and the D-08 unmapped-key advisory + D-13 MIDI advisory both use the existing thread-safe `RenderingDiagnostics.WarnOnce` (`HashSet` + `lock` in `RenderingDiagnostics.cs`). Determinism tests are well-structured (two-runner pattern + `ResetForTesting` between runs).

That said, I found:

- 8 **WARNING**-level defects — off-by-one error in `ScalaKbmParser` diagnostic line numbers, a documented-but-not-implemented validation gap (0-step `.scl` files), dead-code paths, a silent stack-replacement in `SetFileScopeTuning` that contradicts its own docstring algorithm, a misleading "tuning resolved but unused" code path in `SongRenderer.RenderSectionWithTimeline`, and a weak unwind test that allows itself to pass with no assertion fired.
- 6 **INFO**-level items — unused parameter mirrors, minor doc/code drift.

No BLOCKER-tier issues found.

## Warnings

### WR-01: ScalaKbmParser diagnostic line numbers are off-by-one for post-header validation errors

**File:** `flow-lang/StandardLibrary/Audio/Tuning/ScalaKbmParser.cs:67-69, 77-79`
**Issue:** Inside `Parse(...)`, the `size > MaxMappingEntries` check at line 65-69 and the `firstMidi > lastMidi` check at line 75-80 both use the local `cursor` variable as the reported `line` in the thrown `ScalaKbmParseException`. But `cursor` is mutated by `NextField` (invoked through `ReadInt`) which advances `cursor` PAST the line it just read. By the time these checks run, `cursor` is one past the line containing the offending value, so the diagnostic reports a line number that is one greater than the actual line of the malformed field.

Compare with `ScalaParser.cs:99` which correctly captures `stepCountLine = lineCursor` INSIDE the loop after the increment — that's the line of the consumed token. `NextField` already returns a correct `line` value as its tuple's first field, but `ReadInt` discards that information.

**Fix:** Have `ReadInt` / `ReadDouble` return the line number alongside the value, or have them stash the line on a parser-owned field so post-validation diagnostics can reference it:

```csharp
private static int ReadInt(
    string[] lines, ref int cursor, string filePath,
    string expectedDesc, Func<int, bool> validate, out int sourceLine)
{
    (sourceLine, string token) = NextField(lines, ref cursor, filePath, expectedDesc);
    // ... rest unchanged
}

// Call site:
int size = ReadInt(lines, ref cursor, filePath, "size of map (non-negative integer)",
    validate: v => v >= 0, out int sizeLine);
if (size > MaxMappingEntries)
{
    throw new ScalaKbmParseException(filePath, sizeLine, 1,
        "size of map <= 10000", size.ToString(CultureInfo.InvariantCulture));
}
```

### WR-02: ResolvedTuning silently accepts 0-step ParsedScala that ScalaParser docs promise will be rejected

**File:** `flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs:77-91`, `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs:108-113`
**Issue:** `ScalaParser.Parse` explicitly documents (lines 108-113) that "Plan 32-03's ResolvedTuning builder will reject 0-step scales since they have no period". The actual `ResolvedTuning` constructor does NOT reject this case — it accepts `StepCents.Length == 0` and `PeriodCents == 0.0`. The result: every entry in `MidiToHz` becomes the same value (since `Math.Pow(2.0, 0.0 / 1200.0) == 1.0` for every degree), which is silent degenerate behavior rather than a clear error.

The 0-step path is exercised by `ScalaParser.Parse(content, …)` when a `.scl` declares `stepCount == 0`. The parser explicitly allows this (no throw), so a malformed-but-not-rejected input flows through to a broken `ResolvedTuning`. This contradicts both the parser's promise AND violates SPEC-7 (clear error semantics).

**Fix:** Add a guard at the top of `ResolvedTuning.PopulateMidiToHz()` (or in the ctor before that call):

```csharp
public ResolvedTuning(ParsedScala scl, ScalaKbm kbm)
{
    if (scl is null) throw new ArgumentNullException(nameof(scl));
    if (kbm is null) throw new ArgumentNullException(nameof(kbm));
    if (scl.StepCents.Length == 0 && scl.PeriodCents <= 0.0)
        throw new ArgumentException(
            $"Scala scale {scl.FilePath} has 0 steps — at least 1 step is required to define a period.",
            nameof(scl));
    // ...
}
```

### WR-03: ExecutionContext.SetFileScopeTuning empties the entire stack — docstring algorithm describes a different, safer sequence

**File:** `flow-lang/Runtime/ExecutionContext.cs:336-344`
**Issue:** The XML-doc lists a three-step algorithm: (1) pop any block frames, (2) pop the existing file-scope frame, (3) push the new tuning. The implementation collapses all three steps into `while (stack.Count > 0) stack.Pop()`, which works only because the global frame's tuning stack is supposed to never carry block frames at the moment this is called (see `ResetBlockTuningStack` being invoked first in `FlowEngine.Execute`). If any future refactor changes the call ordering — e.g., a code path that invokes `SetFileScopeTuning` while a block is still nominally on the global frame — the entire stack is silently destroyed with no diagnostic.

The defensive comment on line 326-328 ("if `ResetBlockTuningStack` wasn't called at the prior REPL boundary, the global frame's stack should still only carry the bottom pragma frame") admits this is a fragile invariant.

**Fix:** Either tighten the implementation to match the docstring algorithm (count-bounded pops with an assert that no block frames remain), or amend the docstring to match the implementation (the simpler option — just say "REPLACES the entire global frame tuning stack with the new file-scope tuning; relies on prior `ResetBlockTuningStack` to have cleared block frames").

### WR-04: SongRenderer.RenderSectionWithTimeline resolves the tuning then discards it

**File:** `flow-lang/StandardLibrary/Audio/SongRenderer.cs:381-396`
**Issue:** This method calls `var renderTuning = ResolveRenderTuning(section.Context);` then writes `_ = renderTuning;` to suppress an unused-variable warning. The actual `SequenceRenderer.RenderSequenceToVoices(...)` call (lines 395-396) uses the LEGACY string-typed overload that bypasses the tuning entirely.

The comment says "Tuning is captured but not yet threaded through the timeline overload chain — Wave 3 widening if user-facing audio diff matters via this path." But a composer using the LSP-aware timeline path with a custom Scala tuning would silently get 12-TET audio rendered alongside their highlighted timeline, with no diagnostic — exactly the kind of silent breakage that the Phase 32 D-08 / D-13 advisories were designed to surface.

**Fix:** Either (a) thread `renderTuning` through `RenderSequenceToVoices` for the timeline path (the proper fix), or (b) fire a `WarnOnce` when this code path is exercised under a non-default tuning so the composer knows the timeline-render audio is 12-TET-stuck:

```csharp
if (renderTuning.Custom != null || renderTuning.System != TuningSystem.EqualTemperament)
{
    RenderingDiagnostics.WarnOnce(
        "renderSongWithTimeline-tuning-ignored",
        "[render] timeline-aware render path does not honor active tuning — audio renders at 12-TET (deferred to v1.5)");
}
```

### WR-05: LastWinsTuningTests.TuningBlock_BodyThrows_StackStillPops can pass with zero assertions fired

**File:** `flow-lang.Tests/Integration/Phase32/LastWinsTuningTests.cs:336-425`
**Issue:** The test asserts the after-throw frequency only when `File.Exists(wavAfterThrow)` is true (line 410). If the body-error mode aborts the whole eval so `wavAfterThrow` is never produced, the test still PASSES because no negative assertion runs.

The comment block (lines 417-424) explains the design intent — "if the follow-up render didn't fire, we still get clean test exit which means the try/finally didn't leave a dangling frame" — but in practice the test as written cannot distinguish between (a) try/finally correctly unwinding the stack, (b) try/finally LEAKING the partch frame but the eval also aborting, or (c) the test silently being broken because the .scl path doesn't resolve. All three scenarios produce a green test.

**Fix:** Add a positive assertion that the test actually exercised the unwind path. Either assert `File.Exists(wavAfterThrow)` is true unconditionally (making the test fail if the eval aborts), or add a follow-up render in a NEW `FlowEngineRunner` instance and assert the file produced there is at 12-TET (proves clean state, but this is what `TuningBlock_AfterClose_ActiveTuningReverts` already does — so this test should commit to the in-process assertion):

```csharp
Assert.True(File.Exists(wavAfterThrow),
    $"expected after-throw WAV to be produced; if it was not, the test cannot verify stack unwind");
double afterThrowHz = DominantFrequency(wavAfterThrow);
double delta = Math.Abs(afterThrowHz - baseline12tetHz);
Assert.True(delta < 1.0, ...);
```

### WR-06: ScalaParser stepCount < 0 check is dead code (defensive but misleading)

**File:** `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs:103-118`
**Issue:** The parser uses `NumberStyles.None` for step-count parsing (line 103). `NumberStyles.None` excludes `AllowLeadingSign`, so any input starting with `-` (e.g., `-5`) fails `int.TryParse` and throws at line 105-107 BEFORE the `stepCount < 0` check at line 114-118 ever runs. The check is unreachable. The test `MalformedStepCount_ProducesLineCol_NegativeIntegerRejected` confirms `-5` indeed surfaces via the TryParse-fail path with `Found == "-5"`, not via the negative-check path.

This is harmless defensively but creates noise — a future maintainer reading the code may rearrange these checks and miss that the first throws-on-sign already covers the second.

**Fix:** Either remove the dead `if (stepCount < 0)` block, or add a comment that this is reached only if a future refactor relaxes the NumberStyles to allow leading sign. Preferable: remove and rely on `validate: v => v > 0` semantics inline:

```csharp
if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out stepCount))
{
    throw new ScalaParseException(filePath, stepCountLine, 1,
        "step count (positive integer)", token);
}
if (stepCount > MaxStepCount) { ... }
// (no more redundant `< 0` check)
break;
```

### WR-07: ScalaKbmParser accepts leading `+` and 0 referenceHz validates by `> 0.0` but `+0` is rejected silently

**File:** `flow-lang/StandardLibrary/Audio/Tuning/ScalaKbmParser.cs:140-154` (`ReadDouble`)
**Issue:** `ReadDouble` uses `CentsStyle = NumberStyles.Float & ~NumberStyles.AllowExponent & ~NumberStyles.AllowThousands`. `NumberStyles.Float` keeps `AllowLeadingSign`, so `+440.0` is accepted. That's reasonable, but the same call path is used for `referenceHz` with `validate: v => v > 0.0`. A subtle UX issue: `+0.0` parses successfully but fails validation → throws "expected reference frequency (positive Hz), got '+0.0'" — that's fine. But `+440.0` is accepted as a valid frequency, which is non-standard SCL formatting (the .kbm spec is ambiguous, real-world files always use unsigned). Composers don't author `+440.0`, so this is permissive in a way the codebase doesn't otherwise advertise.

ScalaParser explicitly notes in its docstring (line 38-46, D-18 strict-reject section) that it strict-rejects `1.5e2`, `100,5`, and `3 / 2`. But it does NOT reject leading `+` on cents — same gap.

**Fix:** If the design intent is strict-reject for unconventional encodings, also reject leading `+`:

```csharp
const NumberStyles CentsStyle =
    NumberStyles.Float
    & ~NumberStyles.AllowExponent
    & ~NumberStyles.AllowThousands
    & ~NumberStyles.AllowLeadingSign;
// Then handle negative cents via an explicit leading-`-` check before TryParse:
bool isNegative = token.StartsWith("-");
var numericToken = isNegative ? token.Substring(1) : token;
if (!double.TryParse(numericToken, CentsStyle, CultureInfo.InvariantCulture, out double c)) { ... }
if (isNegative) c = -c;
```

(Or document explicitly that leading `+` is tolerated — but this conflicts with the D-18 strict-reject ethos.)

### WR-08: MusicalContext.Clone does not copy the obsolete `Tuning` field

**File:** `flow-lang/Runtime/MusicalContext.cs:122-147`
**Issue:** The `[Obsolete]` `Tuning` property (line 72) is documented as "no longer read by any production code path — kept transitionally because direct deletion broke the Phase 23 readers' compile step." But `Clone()` does NOT propagate this field. If any unmigrated test or third-party tool reads `Clone(myContext).Tuning`, it gets `null` rather than the source's value — silent regression of the transitional contract.

Two paths:

**Fix A (preferred):** Delete the obsolete field outright if Plan 32-06 has landed; the docstring claims this is the cleanup point. The `[Obsolete]` attribute is only there because the Task 1 / Task 2 split couldn't atomically migrate the readers.

**Fix B:** If the field must stay transitionally, propagate it in Clone:

```csharp
public MusicalContext Clone()
{
    var clone = new MusicalContext
    {
        // ... existing copies
#pragma warning disable CS0618 // Type or member is obsolete
        Tuning = Tuning,
#pragma warning restore CS0618
    };
    // ... TuningStack copy
    return clone;
}
```

## Info

### IN-01: ScalaParser parses the description with `Trim()` — trailing whitespace inside the description is silently stripped

**File:** `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs:78`
**Issue:** The partch_43.scl fixture's description line ends with many trailing spaces (~80 cols of padding for alignment). `description = stripped.Trim()` strips both leading AND trailing whitespace, so the canonical-form description loses its intentional padding. The Huygens-Fokker spec says "leading and trailing spaces should be removed" so this is spec-compliant, but the docstring (line 24-28) says "verbatim first non-comment line (trimmed)" — the parenthetical hints at this, but a casual reader of the inline comment "scl.Description ... verbatim" may not realize Trim() is applied.

**Fix:** No functional change needed. Sharpen the doc comment to say "verbatim first non-comment line, with surrounding whitespace removed".

### IN-02: Empty `tuning t { }` blocks are syntactically valid but produce no diagnostic

**File:** `flow-lang/Interpreter/Interpreter.cs:353-414`, `flow-lang/Parsing/Parser.cs:760-773`
**Issue:** A `tuning t { }` block with an empty body parses cleanly and produces a push/pop pair that briefly enters and exits the tuning context. The tests rely on this exact behavior (see `LastWinsTuningTests.TuningBlock_AfterClose_ActiveTuningReverts` which uses `tuning t { }` as a no-op probe). A composer who accidentally wrote `tuning t { (notes) }` and forgot to wrap in a `section` block would get silent behavior with no audible output AND no warning — though this is consistent with Flow's "no warnings for empty bodies" pattern elsewhere.

**Fix:** No action needed. Document if user feedback shows confusion.

### IN-03: `SequenceData` traversal in `ScalaBuiltins.FireUnmappedAdvisoryIfNeeded` re-scans every load

**File:** `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs:102-123`
**Issue:** Every `(loadScala ...)` call re-scans the 128-entry `MidiToHz` table for zeros, even though the result is dedup-keyed by description. A score that loads the same tuning 100 times in a loop pays the scan 100 times. Trivial cost (128 cmps × 100 = 12,800 ops) — well below any user-perceptible threshold.

**Fix:** No action needed. Cache the "has any unmapped" boolean on `ResolvedTuning` if profiling ever flags this.

### IN-04: ScalaKbmParser unmapped-x check is case-sensitive — uppercase `X` is rejected

**File:** `flow-lang/StandardLibrary/Audio/Tuning/ScalaKbmParser.cs:101-105`
**Issue:** The check `if (token == "x")` is exact-case. The comment says "Unmapped: literal lowercase `x`. NOT `X`, NOT `?`." which is consistent with the Huygens-Fokker spec. But charitable interpretation (per MEMORY profile) would suggest accepting `X` and emitting a warning. Currently uppercase `X` throws "mapping entry (non-negative integer or 'x'), got 'X'". Acceptable per spec but not maximally charitable.

**Fix:** Document the strict-reject behavior in user-facing docs. No code change needed.

### IN-05: TuningContextStatement.ExecuteTuningContext silently ignores returns from inside the body

**File:** `flow-lang/Interpreter/Interpreter.cs:390-408`
**Issue:** The loop body checks `if (_returnValue != null) break;` (line 407) — so if a `return X` statement fires inside a `tuning { ... }` block body, the loop exits early. But there is no explicit handling/passthrough — the `finally` clause's `PopTuning()` still fires. This is correct behavior and matches `ExecuteMusicalContext`'s pattern. Just noting for completeness — no defect.

**Fix:** None.

### IN-06: ResolvedTuning Ratios dictionary is shared by reference with ParsedScala

**File:** `flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs:85`
**Issue:** `Ratios = scl.Ratios;` shares the dictionary by reference. Since `ParsedScala.Ratios` is typed as `IReadOnlyDictionary<int, (int, int)>` and the backing concrete type is `Dictionary<…>`, a cast-and-mutate would mutate the resolved tuning's ratios too. Practically impossible in this codebase (no one casts the IReadOnlyDictionary back), but theoretically a defense-in-depth gap. The `StepCents` field is similarly handed off (with `Array.AsReadOnly` — same issue, the underlying array is reachable to anyone with the underlying array reference).

**Fix:** Optional. If hardening for library use:

```csharp
Ratios = new Dictionary<int, (int Num, int Den)>(scl.Ratios);
```

---

_Reviewed: 2026-05-14_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
