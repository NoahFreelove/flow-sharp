# Domain Pitfalls: v1.1 Bug Fixes & Polish

**Domain:** Flow language interpreter bug fixes and feature additions
**Researched:** 2026-04-02
**Focus:** Integration pitfalls when modifying an existing interpreter codebase

## Critical Pitfalls

Mistakes that cause regressions, silent failures, or rewrites.

### Pitfall 1: Sequence Overload Resolution -- Fixing the Wrong Layer

**What goes wrong:** The `transpose(Sequence, Semitone)` overload fails to match when called with a Sequence value. The natural instinct is to modify `OverloadResolver` or `FunctionSignature.Matches()` to be more permissive. This breaks other overloads or introduces ambiguity.

**Why it happens:** `SequenceType` inherits `FlowType.IsCompatibleWith()` which does strict `GetType() == other.GetType()` equality. But the actual runtime type check in `FunctionSignature.Matches()` calls `argType.IsCompatibleWith(paramType)` bidirectionally (lines 63-66, 100-104). The issue is likely that the Value's reported type does not match `SequenceType.Instance` -- either the Value is being created with a wrong type tag, or the type comparison is failing at a subtle level (e.g., `VoidType` wildcard matching is consuming the match before the specific `Sequence` overload gets a chance).

**Root cause investigation:** Check these in order:
1. What type does `Value.Sequence(data)` actually set? (It uses `SequenceType.Instance` -- verified in `Value.cs` line 47)
2. When `transpose` is called via flow operator `seq -> transpose(+2st)`, does the parser correctly inject `seq` as the first argument? The flow operator `->` transform inserts the left-hand side as the first argument, but `NoteStreamCompiler` may produce a value whose runtime type is not `SequenceType`.
3. Does `NoteStreamCompiler` return a `Value` with `SequenceType.Instance` as its type? If it returns via some intermediate (like wrapping in an array or returning a different type), the overload resolver will never match.

**Consequences:** Fixing the wrong layer (e.g., making `SequenceType.IsCompatibleWith` match `VoidType`) will make ALL overloads match Sequence arguments, creating ambiguity errors elsewhere.

**Prevention:**
- Add a failing test first: `Sequence s = | C4 D4 E4 |; Sequence t = s -> transpose(+2st);`
- Trace the actual `FlowType` of the argument at the point `OverloadResolver.Resolve()` is called. Print `argTypes[0].GetType().Name` and `argTypes[0].Name`.
- Fix at the narrowest point possible. If `NoteStreamCompiler` returns the wrong type, fix it there. If `SequenceType` needs an `IsCompatibleWith` override for a parent type, add it minimally.
- After the fix, run ALL 70+ test files -- overload resolution changes are high-blast-radius.

**Detection:** Any overload resolution change that touches `FlowType`, `FunctionSignature.Matches`, or `OverloadResolver` needs the full test suite run before commit.

### Pitfall 2: Sections Silently Dropping Bare Expressions -- The Scope Collection Trap

**What goes wrong:** `ExecuteSectionDeclaration` (Interpreter.cs lines 336-377) collects section content by scanning `_context.CurrentFrame.GetLocalVariables()` for `SequenceData` values. If a user writes a bare expression (e.g., a note stream `| C4 D4 E4 |` not assigned to a variable), the sequence is evaluated but never stored in a variable, so it is never collected. The section renders with 0 frames.

**Why it happens:** The code only looks at named variables in the current scope frame. Bare expressions produce values that go to `_lastExpressionValue` but are not captured by name in the frame.

**Consequences:** Users write `section intro { | C4 D4 E4 | }` and hear silence. There is no error, no warning. This is the worst kind of bug -- silent wrong behavior that makes users think they misunderstand the language.

**Prevention strategies (pick one):**
1. **Implicit naming:** If the last expression in a section body is a Sequence and has no variable name, auto-assign it (e.g., `_seq_0`, `_seq_1`). This mirrors how `proc` handles implicit returns.
2. **Collect from `_lastExpressionValue`:** After executing the section body, if `_lastExpressionValue` is a SequenceData, add it to the section's sequences dictionary.
3. **Emit a warning:** If a section body produces no named Sequence variables, emit a warning: "Section 'intro' has no sequences. Did you forget to assign your note stream to a variable?"

**Recommended approach:** Option 2 (collect from `_lastExpressionValue`) for the simplest fix, PLUS option 3 (warning) as a safety net. Do NOT auto-collect ALL expression results -- only the final one, matching the implicit return convention.

**Detection:** Test case: `section test { | C4 D4 E4 | }` followed by `Song s = [test]; Buffer b = renderSong(s, "piano");` -- verify `b` has non-zero frames.

### Pitfall 3: Error Accumulator Masking Real Failures

**What goes wrong:** `ErrorReporter` collects errors into a `List<FlowError>` and never throws. Code that calls `_errorReporter.ReportError(...)` continues executing. Downstream code then encounters invalid state (null values, wrong types, missing variables) and either crashes with an unrelated error or silently produces wrong output.

**Why it happens:** The error accumulation model is intentional (see PROJECT.md: "Soft-failure error model"). But it means every call site after `ReportError` must handle the error case explicitly -- and many don't.

**Consequences:** Users see a cryptic error (e.g., "NullReferenceException in SongRenderer") when the real error was "No matching overload for function 'transpose'" reported 10 lines earlier but swallowed.

**Prevention for v1.1 fixes:**
- When fixing any bug, check what the `ErrorReporter` does at that site. If `ReportError` is called but execution continues into code that assumes success, add an early return.
- Example in `OverloadResolver.Resolve()`: it reports an error and returns `null`. Every caller must handle `null` returns. Check all callers.
- Do NOT change the error model globally in v1.1. Instead, add a `--verbose` flag (already planned) that dumps the full error list before showing the final error.
- For the REPL, print accumulated errors after each statement, not just at the end of the program.

**Detection:** After each bug fix, run a script that triggers the original bug and verify the error message is clear and the real cause is visible, not masked.

### Pitfall 4: Static PlaybackFunctions._manager Gets Clobbered

**What goes wrong:** `PlaybackFunctions._manager` is a `static` field (PlaybackFunctions.cs line 15). When a background `FlowEngine` is created (e.g., for watch mode re-evaluation, editor background rendering, or test execution), `PlaybackFunctions.Register()` overwrites `_manager` with the new engine's `AudioPlaybackManager`. The original engine's playback now uses the wrong manager, causing: wrong audio output, use-after-dispose errors, or complete playback failure.

**Why it happens:** The static field pattern was a quick shortcut for giving static callback functions access to the manager. It works fine for single-engine scenarios but breaks with multiple engines.

**Consequences:** Watch mode (`--watch`) creates a new engine on file change. The old engine's `stop()` calls fail or no-op. The new engine's `play()` may try to use a disposed backend. Intermittent audio failures that are hard to reproduce.

**Current mitigation:** `LiveReloadManager` already has `GetManager()`/`SetManager()` to save/restore. This is a band-aid.

**Prevention for v1.1:**
- Do NOT try to refactor the static out in v1.1. That's a major architectural change (every built-in function callback would need a context parameter or closure capture).
- Instead, ensure every code path that creates a new `FlowEngine` saves and restores `_manager`:
  ```csharp
  var saved = PlaybackFunctions.GetManager();
  try { /* create and use background engine */ }
  finally { PlaybackFunctions.SetManager(saved); }
  ```
- Document the pattern clearly. Add a comment on the static field explaining why it exists and the save/restore contract.
- The `--verbose` flag should log when `_manager` is set, so clobbering is visible.

**Detection:** Run a script with `play()`, then modify it while `--watch` is active. Does the new version play correctly? Does `stop()` still work?

## Moderate Pitfalls

### Pitfall 5: Adding `//` Comments to a Hand-Written Lexer -- Token Boundary Collisions

**What goes wrong:** Adding `//` comment support to `SkipWhitespaceAndComments()` seems trivial, but `/` is already a token boundary character (line 772: `IsTokenBoundary` includes `/`). The lexer currently emits `TokenType.Slash` for `/`. If the comment check is not placed early enough in the skip loop, a sequence like `// comment` will be tokenized as `Slash, Slash, Identifier("comment")`.

**Why it happens:** `SkipWhitespaceAndComments()` runs before `NextToken()`, but the current comment detection (the `Note:` prefix on lines 801-808) only triggers at the start of a line. `//` comments should work anywhere.

**Implementation approach:**
```csharp
// In SkipWhitespaceAndComments(), add BEFORE the else-break:
else if (c == '/' && PeekNext() == '/')
{
    // Skip line comment
    while (!IsAtEnd() && Peek() != '\n')
        Advance();
}
```

**Specific traps:**
1. **Division operator ambiguity:** `x / y` must NOT be treated as a comment. The fix is safe because `SkipWhitespaceAndComments()` only runs between tokens -- by the time we're skipping whitespace, we've already emitted the first `/` as a `Slash` token. But verify with test: `Int x = 10 / 2` must still work.
2. **URLs in strings:** `"http://example.com"` must not trigger comment skipping. Safe because `SkipWhitespaceAndComments` is not called inside `ScanString`, but verify.
3. **Note: prefix comments:** The existing `Note:` comment syntax (line 801) should be preserved for backward compatibility. Both `//` and `Note:` should work.
4. **Block comments (`/* */`):** Do NOT add these in v1.1. They require brace-depth tracking and interact badly with note streams (`| C4 /* skip */ D4 |`). `//` is sufficient.

**Detection:** Test with: `Int x = 10 / 2 // this is half`, `Int y = x // reuse`, and verify division still works and comments are stripped.

### Pitfall 6: Renaming `exportWav` to `writeWav` -- Breaking User Scripts

**What goes wrong:** Renaming a built-in function breaks every existing script that uses the old name. The rename is in 3 places: C# registration in `BuiltInFunctions.cs` (lines 387-397), `audio.flow` stdlib declarations (lines 33, 36), and user scripts (`examples/showcase.flow` line 75).

**Why it happens:** No deprecation mechanism exists. The rename just removes the old name.

**Prevention:**
1. **Register BOTH names** pointing to the same implementation. Register `writeWav` as the primary name and `exportWav` as an alias.
2. **Add a deprecation warning:** When `exportWav` is called, print a warning: "Warning: exportWav is deprecated, use writeWav instead." Implement this by wrapping the old registration in a lambda that prints the warning then delegates.
3. **Update all .flow files** in the repo (stdlib and examples) to use the new name.
4. **Update CLAUDE.md** documentation to reference `writeWav`.
5. **Remove `exportWav`** in a future milestone (v1.2), not v1.1.

**Specific files to update:**
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` -- registration
- `flow-lang/audio.flow` -- stdlib internal proc declarations
- `examples/showcase.flow` -- example usage
- Any test files using `exportWav`

**Detection:** `grep -r "exportWav" .` after the change to find any missed references.

### Pitfall 7: Math Stdlib Functions Shadowing Musical Keywords

**What goes wrong:** Adding `min`, `max`, `abs`, `sin`, `cos`, `sqrt` etc. to the standard library. The function name `sin` collides with potential musical usage (e.g., a user naming a variable `sin` for "sine wave", or future `sin` as a shorthand). More importantly, `min` and `max` might collide with future musical constructs.

**Why it happens:** Mathematical function names are short and common. In a music-oriented language, some names have dual meanings.

**Prevention:**
- `sin`, `cos`, `sqrt`, `abs`, `min`, `max`, `pow`, `floor`, `ceil`, `round` are safe -- they don't conflict with any current keywords or token types (checked against the keyword list in `ScanIdentifierOrKeyword`, lines 571-607).
- Register them in a new `RegisterMath` method in `BuiltInFunctions.cs`, keeping them organized.
- Use `Double` as the input/output type (not `Float` or `Number`). Flow's numeric widening chain is `Int -> Long -> Float -> Double -> Number`. Using `Double` means `Int` arguments will auto-convert, which is the expected behavior for math functions.
- BUT: `sin(Int)` and `sin(Double)` should both work. Register two overloads: one for `Int` (that converts to double internally) and one for `Double`. Otherwise users get "No matching overload for sin with argument types (Int)".
- For `min` and `max`, support two arguments: `min(Double, Double)` and `min(Int, Int)`. Do NOT try to make them variadic in v1.1 -- that's a separate feature.

**Specific trap -- `random` collision:** There is already a `random` built-in. Make sure `floor(random())` works by verifying `random()` returns `Double`, not `Float`.

**Detection:** Test: `Int x = 5; Double y = sin(x);` -- this must work (Int->Double conversion). Test: `Double z = min(3, 7);` -- both Int literals must convert.

### Pitfall 8: Tempo Ramps -- Time-Varying BPM Breaks the Rendering Pipeline

**What goes wrong:** The entire rendering pipeline assumes constant BPM within a section. `SongRenderer.RenderSection()` reads `section.Context?.Tempo` once (line 62) and passes it as a scalar `double bpm` to all downstream functions. `SequenceRenderer.RenderSequenceToVoices()` uses this constant BPM to calculate note positions. Introducing tempo ramps (gradual BPM change from X to Y over N beats) requires position calculation to be a function of time, not a simple multiplication.

**Why it happens:** Constant BPM is a reasonable first implementation. But tempo ramps fundamentally change the math: beat position is no longer `beat * (60/bpm)` in seconds, it's an integral over the tempo curve.

**Consequences of a naive implementation:** If you just linearly interpolate BPM and recalculate per-note positions, notes will be placed at approximately correct times, but:
1. The total section duration will be wrong (you need to integrate the tempo curve, not average it).
2. Notes that span the ramp will have wrong durations (a half note starting at 120 BPM and ending at 60 BPM should be longer than `2 beats * (60/90)` seconds).
3. The existing `rit` (ritardando) and `accel` (accelerando) tokens/AST nodes already exist in the lexer/parser -- but do they actually affect rendering? If not, they're already broken.

**Correct approach:**
1. Model tempo as a function of beat position: `double TempoAtBeat(double beat)`.
2. Convert beat positions to time positions by numerical integration: `double TimeAtBeat(double beat) = integral(60/TempoAtBeat(b), 0, beat)`.
3. For linear ramps, this has a closed-form solution: if tempo goes from T1 to T2 over B beats, `time(b) = B * 60 * ln(T1 + (T2-T1)*b/B) / (T2-T1)` (when T1 != T2).
4. Pass the tempo function (or a tempo map / list of tempo events) down through the rendering pipeline instead of a scalar `bpm`.

**Prevention for v1.1:**
- Do NOT try to make the entire pipeline tempo-function-aware in one pass. That's a rewrite of `SongRenderer`, `SequenceRenderer`, and `MixVoicesToStereoBuffer`.
- Instead, implement tempo ramps as a **pre-processing step**: before rendering, expand the tempo ramp into a series of constant-tempo sub-sections. A ramp from 120 to 60 over 8 beats becomes 8 one-beat segments at 120, 112.5, 105, 97.5, 90, 82.5, 75, 67.5 BPM. Render each segment at its constant tempo, then concatenate.
- The granularity (1 beat per segment) is perceptually smooth enough for most music. Users won't hear the staircase.
- This approach requires minimal changes to the existing pipeline -- just a loop that calls `RenderSection` multiple times with different BPMs.

**Detection:** Render a section with a tempo ramp and measure the total duration. If the ramp is 120->60 over 4 beats, the total duration should be approximately 2.77 seconds (integrated), not 2.0 seconds (at average 90 BPM) or 3.0 seconds (at 60 BPM).

## Minor Pitfalls

### Pitfall 9: `--verbose` Flag Noise Level

**What goes wrong:** Adding a `--verbose` flag that dumps ALL interpreter state (every variable assignment, every function call, every overload resolution). The output is so noisy that the actual problem is buried in hundreds of lines.

**Prevention:** Make `--verbose` show: (1) errors and warnings from `ErrorReporter`, (2) function resolution failures (which overloads were tried and why they failed), (3) section rendering summary (section name, sequence count, total beats, rendered frames). Do NOT show: every variable assignment, every expression evaluation, or every token. If deeper debugging is needed later, add `--debug` or `--trace` levels.

### Pitfall 10: `mix()` Buffer Length/Channel Mismatch

**What goes wrong:** `mix()` for layering buffers assumes all input buffers have the same length and channel count. If buffers differ, samples are read out of bounds or channels are misaligned.

**Prevention:** `mix()` should: (1) use the longest buffer's length (shorter buffers contribute silence after they end), (2) use the maximum channel count (mono buffers are duplicated to stereo), (3) normalize by the number of buffers (divide by count) to prevent clipping. Document that `mix()` does NOT do time-alignment -- it starts all buffers at beat 0.

### Pitfall 11: Per-Section Volume Applied Twice

**What goes wrong:** Adding per-section `gain` in `SongRenderer.RenderSection()` and also having the user apply `gain()` in their script. The volume is doubled (or the gain is applied in dB when the user expects linear, or vice versa).

**Prevention:** Per-section gain should be in linear amplitude (0.0 to 1.0), applied once during the final mix stage. Document clearly: "Section gain is applied during `renderSong`. If you also apply `gain()` to a buffer, both will take effect." Use the name `volume` (not `gain`) for the section-level control to distinguish from the DSP `gain()` function.

### Pitfall 12: New Synth Presets Breaking Existing Tests

**What goes wrong:** Adding strings/organ/bell synthesizers changes the default instrument mapping or introduces a synthesizer that produces different audio characteristics (louder, different harmonics). Existing test scripts that compare audio output or rely on specific rendering behavior start failing.

**Prevention:** New synthesizers should be additive -- only used when explicitly requested via instrument name. The default synthesizer (when no instrument is specified) must not change. Verify: run all test files after adding new synths, before adding any tests that USE the new synths.

### Pitfall 13: REPL Auto-Imports Masking Import Bugs

**What goes wrong:** REPL auto-imports `@std` (which pulls in `@collections` and `@bars`). Scripts that work in the REPL but fail when run as files because the user forgot `use "@std"`. This is already a known issue ("some tests fail due to missing `use "@std"` imports" per PROJECT.md).

**Prevention:** Auto-imports should be REPL-only (already the plan). Add a clear error message when a function is not found: "Function 'map' not found. Did you mean to add `use \"@collections\"`?" This requires a mapping of common functions to their modules, which is a small lookup table.

## Phase-Specific Warnings

| Phase/Task | Likely Pitfall | Severity | Mitigation |
|------------|---------------|----------|------------|
| Fix Sequence overload resolution | Fixing wrong layer (Pitfall 1) | CRITICAL | Trace actual types at resolution point; fix narrowest layer |
| Fix sections dropping bare expressions | Silent wrong behavior (Pitfall 2) | CRITICAL | Collect `_lastExpressionValue` as implicit sequence |
| Improve error reporting | Error accumulator masking (Pitfall 3) | CRITICAL | Add early returns after `ReportError`; `--verbose` flag |
| Add `//` comments | Token boundary collision (Pitfall 5) | MODERATE | Add in `SkipWhitespaceAndComments` before else-break; test division still works |
| Rename `exportWav` to `writeWav` | Breaking user scripts (Pitfall 6) | MODERATE | Register both names; deprecation warning on old name |
| Add math stdlib | Type overload matching (Pitfall 7) | MODERATE | Register Int and Double overloads; verify numeric widening |
| Add `mix()` | Buffer length mismatch (Pitfall 10) | LOW | Use longest buffer; normalize by count |
| Per-section volume | Double application (Pitfall 11) | LOW | Apply once in renderSong; use name `volume` not `gain` |
| New synth presets | Existing test breakage (Pitfall 12) | LOW | Additive only; default synth unchanged |
| Tempo ramps | Pipeline assumes constant BPM (Pitfall 8) | MODERATE | Pre-process ramp into constant-tempo sub-sections |
| `--verbose` flag | Noise level (Pitfall 9) | LOW | Show resolution failures and section summaries only |
| REPL auto-imports | Masking import bugs (Pitfall 13) | LOW | REPL-only; add "did you mean to import X?" errors |
| Static `_manager` clobbering | Watch mode / background engine (Pitfall 4) | CRITICAL | Save/restore around background engine creation |

## Ordering Recommendation

Based on pitfall severity and dependency chains:

1. **Fix overload resolution first** (Pitfall 1) -- unblocks transform functions, which are needed to test other features.
2. **Fix section bare expressions** (Pitfall 2) -- unblocks correct section rendering, needed for tempo ramps and per-section volume.
3. **Fix error masking** (Pitfall 3) -- makes debugging all subsequent features easier.
4. **Fix static `_manager`** (Pitfall 4) -- unblocks watch mode reliability.
5. **Then add features** in any order, as they are largely independent.

## Sources

- Codebase analysis: `OverloadResolver.cs`, `FunctionSignature.cs`, `FlowType.cs`, `SequenceType.cs`, `SimpleLexer.cs`, `Interpreter.cs`, `PlaybackFunctions.cs`, `SongRenderer.cs`, `BuiltInFunctions.cs`, `ErrorReporter.cs`, `MusicalContext.cs`
- Tempo ramp integration math: standard calculus for variable-rate time mapping (closed-form for linear ramps)
- Deprecation patterns: standard library versioning practices (Python `warnings.warn`, Node.js `util.deprecate`)
