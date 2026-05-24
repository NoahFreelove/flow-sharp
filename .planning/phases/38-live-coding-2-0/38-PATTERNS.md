# Phase 38: Live Coding 2.0 — Pattern Map

**Mapped:** 2026-05-23
**Files analyzed:** 26 (new + modified + new test files + new examples)
**Analogs found:** 26 / 26 (every new file has a concrete in-tree analog)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/Ast/Statements/LiveBlockStatement.cs` | AST node | n/a (data) | `flow-lang/Ast/Statements/MusicalContextStatement.cs` | exact (same kind — context-block AST) |
| `flow-lang/Lexing/SimpleLexer.cs` (modify) | lexer | request-response | itself (keyword table at lines 875-896) | self-modify |
| `flow-lang/Lexing/TokenType.cs` (modify) | enum decl | n/a | itself (lines 17-30 musical-context tokens) | self-modify |
| `flow-lang/Parsing/Parser.cs` (modify) | parser | request-response | itself (`ParseMusicalContextStatement` dispatch at lines 104-171) | self-modify |
| `flow-lang/Interpreter/Interpreter.cs` (modify) | interpreter | event-driven | itself (`case MusicalContextStatement ctx:` at line 115; `ExecuteMusicalContext` at line 147) | self-modify |
| `flow-lang/Runtime/LiveBlockRegistry.cs` (NEW) | runtime registry | event-driven / pub-sub | `flow-lang/Runtime/PrngRegistry.cs` (per-key registry + reset-at-boundary API) | role-match (registry pattern) |
| `flow-lang/Interpreter/LambdaCaptureAuditor.cs` (NEW) | AST walker / utility | transform | RESEARCH §C sketch (no exact existing analog — closest is `flow-lang/Diagnostics/UnusedImportAnalyzer.cs`-style read-only AST walker) | role-match |
| `flow-interpreter/LiveReloadManager.cs` (REWRITE) | orchestrator | event-driven / streaming | itself (preserved primitives: bar-boundary at 230, crossfade at 251, debounce at 274, RenderScript at 328) | self-rewrite |
| `flow-interpreter/LiveStatusPanel.cs` (NEW) | UI renderer / terminal | event-driven | UI-SPEC §"ANSI Live Status Panel"; closest in-tree color usage `LiveReloadManager.cs:60-64, 185-187, 209-211` | role-match (terminal output) |
| `flow-interpreter/Repl.cs` (EXTEND) | REPL orchestrator | request-response | itself (meta-command dispatch lines 210-220; `Console.ReadLine`-based input lines 105-208) | self-modify |
| `flow-interpreter/ReplLineEditor.cs` (or `FlowPromptCallbacks.cs` NEW) | input editor / wrapper | request-response | RESEARCH §G (PrettyPrompt `IPromptCallbacks` wrapping `CompletionHandler.BuildItems`); no existing wrapper | role-match |
| `flow-lang/StandardLibrary/Audio/InputFunctions.cs` (NEW) | builtin registration | request-response | `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` (sibling — registry-only builtins with `AudioPlaybackManager` dep) | exact (sibling Audio/*.cs) |
| `flow-lang/Audio/PulseAudioCaptureBackend.cs` (NEW) | P/Invoke backend | streaming | `flow-lang/Audio/PulseAudioSimpleBackend.cs` (~310 LOC playback P/Invoke; lines 273-311 P/Invoke surface) | exact (mirror, change direction flag) |
| `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` (EXTEND) | allocator helper | transform | itself (`AllocateWithPool` 124-169 + `AsyncLocal<int?>` instrumentation at 23-28) | self-modify |
| `flow-lang/StandardLibrary/Audio/Voice.cs` (EXTEND) | mutable data | n/a | itself (lines 1-40, add `Name` property + `CopyStateFrom`) | self-modify |
| `flow-lang/StandardLibrary/Network/OscFunctions.cs` (NEW) | builtin registration / network | event-driven / pub-sub | `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` (registration shape) + `flow-lang/Audio/PulseAudioSimpleBackend.cs` (Task.Run + CancellationToken lifecycle) | role-match |
| `flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs` (NEW) | type system | n/a | `flow-lang/TypeSystem/SpecialTypes/SfzType.cs` (sealed singleton reference-identity type) | exact |
| `flow-lang/Runtime/Value.cs` (EXTEND) | factory | n/a | itself (lines 50-101 — Tuning / Sfz / MarkovModel / LsystemModel reference-id factories) | self-modify |
| `flow-lang/StandardLibrary/VisualizationFunctions.cs` (EXTEND) | builtin renderer | transform | itself (note-placement loop lines 117-131; bottom separator pattern lines 166-177) | self-modify |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` (modify) | registry hub | n/a | itself (`RegisterAllImplementations` at line 35) | self-modify |
| `flow-lang/network.flow` OR `flow-lang/osc.flow` (NEW) | module activation | request-response | `flow-lang/sfz.flow` (Phase 33 `use "@sfz"` opt-in pattern); `flow-lang/notation-io.flow` (Phase 39) | exact (opt-in module activation) |
| `flow-lang/audio.flow` (EXTEND) | forward-decl | n/a | itself (lines 1-40 — `internal proc` forward decls) | self-modify |
| `flow-cli/Commands/WatchCommand.cs` (minor edit) | CLI orchestrator | request-response | itself (lines 1-50, constructor call to LiveReloadManager) | self-modify |
| `flow-lang/flow-lang.csproj` (modify) | project metadata | n/a | itself (lines 10-13 PackageReferences) | self-modify |
| `flow-interpreter/flow-interpreter.csproj` (modify) | project metadata | n/a | itself (lines 3-5 ProjectReference; +PackageReference) | self-modify |
| 23 xUnit tests `flow-lang.Tests/Integration/Phase38/*.cs` (NEW) | tests | request-response | `flow-lang.Tests/Integration/Phase37/GranularSynthesisTests.cs` + `LicenseAuditTests.cs` precedents | exact (per-phase Integration/Phase38/ directory pattern) |
| 5 `.flow` smoke tests `tests/test_live_*.flow` etc. (NEW) | `.flow` smoke | request-response | `tests/test_live_reload.flow` (20 LOC `tempo`/`section`/`renderSong`/`play`) + `tests/test_repl_autoimport.flow` (11 LOC `(print ...)` PASS sentinel) | exact |
| 5 examples `examples/live/*.flow` + `repl_session.md` (NEW) | tutorial chapter | n/a (docs) | `examples/notation/to_musicxml.flow` (1-30 narrated chapter pattern) + `examples/dsp/granular.flow` (Phase 37 chapter) | exact |

---

## Pattern Assignments

### `flow-lang/Ast/Statements/LiveBlockStatement.cs` (NEW — AST record)

**Analog:** `flow-lang/Ast/Statements/MusicalContextStatement.cs` (entire file, 22 lines)

**Full analog file** (lines 1-22):

```csharp
using FlowLang.Core;

namespace FlowLang.Ast.Statements;

/// <summary>
/// The type of musical context being set.
/// </summary>
public enum MusicalContextType { Timesig, Tempo, Swing, Key, Dynamics, Rit, Accel, Pan, Gain, ReverbTime, VoicePool, SustainPedal }

/// <summary>
/// A musical context block statement that sets tempo, time signature, swing, or key
/// for its body scope. e.g., tempo 120 { ... } or timesig 4/4 { ... }
/// </summary>
public record MusicalContextStatement(
    SourceLocation Location,
    MusicalContextType ContextType,
    Expression Value,                    // The value expression (e.g., 120 for tempo, 4 for timesig numerator)
    Expression? Value2,                  // Optional second value (denominator for timesig)
    IReadOnlyList<Statement> Body,
    Span? Span = null
) : Statement(Location);
```

**Pattern to copy:** record-inherits-Statement(Location) shape; `Body` is `IReadOnlyList<Statement>`; final positional `Span? Span = null` parameter. The `Expression Value` becomes the new node's `Expression QuantizeValue`. Add stable `int BlockId` field (FNV-1a of `SourceLocation` per RESEARCH §A line 446-457) so `LiveBlockRegistry` can key per-block pending buffers across re-renders. Per RESEARCH §A line 638-648 the registry record (`LiveBlockRegistration`) lives in `Runtime/`, not in `Ast/`.

---

### `flow-lang/Lexing/TokenType.cs` (modify — add `Live` token)

**Analog (same file, lines 17-30):**

```csharp
    Timesig,
    Tempo,
    Swing,
    Key,
    ...
    VoicePool,          // Phase 28 (SPEC-7) — voicePool N { ... } musical-context block
    SustainPedal,       // sustainPedal { ... } musical-context block — extends note durations
    Tuning,             // Phase 32 (SPEC-2) — tuning <expr> { ... } musical-context block (D-13)
```

**Pattern to copy:** add `Live,         // Phase 38 (LIVE-01) — live <quantize> { ... } block (D-38-02)` in the same block. Trailing comment cites the phase + REQ + decision per Tuning precedent.

---

### `flow-lang/Lexing/SimpleLexer.cs` (modify — add `live` keyword entry)

**Analog (same file, lines 875-896):**

```csharp
{
    "proc" => TokenType.Proc,
    ...
    "voicePool" => TokenType.VoicePool,
    "sustainPedal" => TokenType.SustainPedal,
    "tuning" => TokenType.Tuning,
    "match" => TokenType.Match,
    ...
}
```

**Pattern to copy:** add `"live" => TokenType.Live,` in the keyword switch. Per RESEARCH §A line 630, slot it adjacent to `voicePool`/`sustainPedal`/`tuning` (the Phase 28+ musical-context keyword cluster).

---

### `flow-lang/Parsing/Parser.cs` (modify — add `live` dispatch + `ParseLiveBlockStatement`)

**Analog (same file, lines 104-171):**

```csharp
// Musical context blocks
if (Match(TokenType.Timesig))
    return ParseMusicalContextStatement(MusicalContextType.Timesig);
if (Match(TokenType.Tempo))
    return ParseMusicalContextStatement(MusicalContextType.Tempo);
...
// Phase 28 SPEC-7: voicePool N { ... } context block. Integer N only (no float).
if (Check(TokenType.VoicePool) && _current + 1 < _tokens.Count
    && _tokens[_current + 1].Type is TokenType.IntLiteral)
{
    Advance(); // consume `voicePool`
    return ParseMusicalContextStatement(MusicalContextType.VoicePool);
}
...
// Phase 32 D-13: `tuning <expr> { ... }` musical-context block.
if (Match(TokenType.Tuning))
    return ParseTuningContextStatement();
```

**Pattern to copy:** add `if (Match(TokenType.Live)) return ParseLiveBlockStatement();` after the `Tuning` dispatch (~line 171). `ParseLiveBlockStatement` body sketch in RESEARCH §A lines 414-457:
- consume optional quantize literal (NoteValue token OR Int + optional `bar`/`bars` identifier suffix)
- `Expect(TokenType.LBrace, ...)` then loop `ParseStatement()` until `RBrace`
- compute `BlockId = ComputeBlockId(location)` (FNV-1a of source location)

The `voicePool` branch at lines 150-155 is the precedent for "look-ahead to a `IntLiteral` then advance keyword" pattern that quantize parsing reuses.

---

### `flow-lang/Interpreter/Interpreter.cs` (modify — add `LiveBlockStatement` execution branch)

**Analog (same file, lines 110-145 statement dispatch; 147+ `ExecuteMusicalContext` body):**

```csharp
case SectionDeclaration section:
    ExecuteSectionDeclaration(section);
    break;

case MusicalContextStatement ctx:
    ExecuteMusicalContext(ctx);
    break;

case TuningContextStatement tctx:
    ExecuteTuningContext(tctx);
    break;

case ExpressionStatement exprStmt:
    var value = _evaluator.Evaluate(exprStmt.Expression);
    _lastExpressionValue = value;  // Store for REPL
    break;
```

`ExecuteMusicalContext` opens with the scope-stack idiom (line 147):

```csharp
private void ExecuteMusicalContext(MusicalContextStatement ctx)
{
    _context.PushFrame();
    try
    {
        var musicalCtx = new MusicalContext();
        switch (ctx.ContextType)
        {
            case MusicalContextType.Timesig:
                var num = _evaluator.Evaluate(ctx.Value);
                ...
```

**Pattern to copy:**

1. Add `case LiveBlockStatement live: ExecuteLiveBlock(live); break;` alongside the existing `case MusicalContextStatement ctx:` branch.
2. `ExecuteLiveBlock(live)` mirrors `ExecuteMusicalContext` but its body:
   - Calls `RenderingDiagnostics.WarnOnce($"live-determinism-optout:{live.Location.Line}", "[live] entering live block at line {N} — opts OUT of two-run cmp-clean determinism")` per UI-SPEC advisory catalog row "Live determinism opt-out".
   - Resolves `QuantizeValue` to `double quantizeBeats` (NoteValue → beats; Bar → numerator × beats-per-bar from active `MusicalContext`).
   - Pushes a `LiveBlockRegistration` (Runtime new type per RESEARCH §A line 638-648) into `_context.LiveBlockRegistry` (a new property on `ExecutionContext`).
   - Executes the body once via the existing statement loop (so initial render captures the per-block buffer).
3. The PushFrame / try / finally / PopFrame discipline at lines 149-150 + the matching PopFrame at end of `ExecuteMusicalContext` is the exact scope-stack idiom to reuse.

---

### `flow-lang/Runtime/LiveBlockRegistry.cs` (NEW)

**Analog:** `flow-lang/Runtime/PrngRegistry.cs` (lines 100-126 — keyed registry + `ResetAtRenderBoundary()` API)

```csharp
// Source: flow-lang/Runtime/PrngRegistry.cs:115-126
/// <summary>
/// Called at <c>renderSong</c> / <c>writeWav</c> / <c>exportWav</c> entry.
/// Clears the per-site cache so the next pass starts from fresh reseeded
/// <see cref="Random"/>s. The render-boundary salt stays constant in v1.5;
/// Phase 38's <c>live</c> opt-out (per RESEARCH Open Question 3) may turn
/// it into a non-deterministic input.
/// </summary>
public void ResetAtRenderBoundary()
{
    _registry.Clear();
    _drawCounts.Clear();
}
```

**Pattern to copy:**
- Keyed `ConcurrentDictionary<int, LiveBlockRegistration>` keyed by `BlockId` (FNV-1a of SourceLocation, deterministic across runs).
- `Register(LiveBlockRegistration reg)` adds-or-replaces; called by `Interpreter.ExecuteLiveBlock`.
- `Snapshot()` returns `IReadOnlyDictionary<int, LiveBlockRegistration>` for the swap-callback consumer (LiveReloadManager).
- The Phase 36 `ResetAtRenderBoundary()` precedent at line 122 is the model for `Clear()` — called from `LiveReloadManager.StagePendingBuffers` AFTER staging new pending buffers but BEFORE the streaming loop swaps them in (RESEARCH §D).

---

### `flow-lang/Interpreter/LambdaCaptureAuditor.cs` (NEW — stale-closure detection helper)

**Analog:** RESEARCH §C lines 706-744 (concrete implementation sketch). No exact in-tree analog of "AST-walking read-only auditor"; closest read-only walker is the existing `_evaluator.Evaluate` switch-dispatch pattern in `flow-lang/Interpreter/ExpressionEvaluator.cs` (per CLAUDE.md "Pattern matching for node dispatch rather than visitor pattern").

**Pattern to copy:** static class with one public method `CollectFileScopeReferences(IReadOnlyList<Statement> body) -> HashSet<string>`. Internal `WalkStatement` + `WalkExpression` switch-on-AST-record-type per CLAUDE.md C# Conventions ("Pattern matching switch expressions for node dispatch"). Consumed by `LiveReloadManager.StagePendingBuffers` at swap time:

```csharp
// From RESEARCH §C line 747-759
var refs = LambdaCaptureAuditor.CollectFileScopeReferences(newLiveBlock.Body);
var newFileScope = engine.Context.GlobalFrame;
foreach (var name in refs)
{
    if (!newFileScope.HasVariable(name) && !newFileScope.HasFunction(name))
    {
        _panel.PublishAdvisory(
            $"[live] stale closure: references removed binding '{name}' at line {newLiveBlock.Location.Line} — keeping previous version",
            AdvisoryLevel.Error,
            dedupKey: $"live-stale-closure:{name}:{newLiveBlock.Location.Line}");
        return; // skip this swap; previous buffer continues
    }
}
```

---

### `flow-interpreter/LiveReloadManager.cs` (REWRITE — preserve primitives, replace orchestration)

**Analog:** itself. **The bar-boundary detection, crossfade, and capture-mode render primitives are EXPLICITLY preserved** per CONTEXT D-38-06 and RESEARCH §A "PRIMITIVES PRESERVED." The orchestration around them changes.

**Primitives to PRESERVE byte-identical** (CONTEXT D-38-06):

1. **`CheckBarBoundary` at lines 230-245** (bar-counter math from sample position):
```csharp
// Source: flow-interpreter/LiveReloadManager.cs:230-245
private (bool IsAtBoundary, int BarNumber) CheckBarBoundary(int samplePosition)
{
    double secondsPerBeat = 60.0 / _currentTempo;
    double secondsPerBar = secondsPerBeat * _currentBeatsPerBar;
    int samplesPerBar = (int)(secondsPerBar * _currentSampleRate) * _currentChannels;
    if (samplesPerBar <= 0) return (true, 1);
    int barNumber = samplePosition / samplesPerBar + 1;
    int positionInBar = samplePosition % samplesPerBar;
    bool isAtBoundary = positionInBar < ChunkSamples;
    return (isAtBoundary, barNumber);
}
```

2. **`ApplyCrossfade` at lines 251-268** (64-sample equal-power):
```csharp
// Source: flow-interpreter/LiveReloadManager.cs:251-268 — DO NOT CHANGE
private static void ApplyCrossfade(float[] oldBuffer, int oldPosition, float[] newBuffer, int newPosition)
{
    int fadeLength = Math.Min(CrossfadeSamples, newBuffer.Length - newPosition);
    int oldRemaining = oldBuffer.Length - oldPosition;
    fadeLength = Math.Min(fadeLength, oldRemaining);
    if (fadeLength <= 0) return;
    for (int i = 0; i < fadeLength; i++)
    {
        float t = (float)i / fadeLength; // 0.0 -> 1.0
        float oldSample = oldBuffer[oldPosition + i];
        float newSample = newBuffer[newPosition + i];
        newBuffer[newPosition + i] = oldSample * (1.0f - t) + newSample * t;
    }
}
```

3. **`RenderScript` at lines 328-370** (capture-mode FlowEngine) — keep signature shape; extend to also return per-block buffers via additional out-param `out Dictionary<int, LiveBlockBuffer>? perBlockBuffers` (RESEARCH §F line 500).

**Orchestration to REWRITE:**

4. **Debounce constant at line 277 — change `500` to `200`** (D-38-05 / Pitfall #21):
```csharp
// Source: flow-interpreter/LiveReloadManager.cs:274-279 (existing)
private void TriggerBackgroundRender()
{
    var now = DateTime.Now;
    if ((now - _lastChangeTime).TotalMilliseconds < 500)   // ← CHANGE to 200
        return;
    _lastChangeTime = now;
```

5. **Add 30s CancellationToken wrap around the worker `Task.Run`** per RESEARCH §E lines 786-800 (Option A — Task.Run + Wait(timeout)). The 30s cap PROTECTS the composer; worker leak is documented in a code-comment per RESEARCH §E line 600.

6. **Replace the inline Cyan/Green/Red `Console.ForegroundColor` calls** (lines 60-64, 185-187, 209-211, 288-301, 317-319) with `_panel.PublishAdvisory(...)` (new field) — the panel handles ANSI cursor moves + TTY fallback per UI-SPEC §"Color".

7. **Replace `_pendingBuffer` (single field at line 23) with `Dictionary<int, LiveBlockBuffer>` keyed by `BlockId`** per RESEARCH §F line 500 — supports D-38-02 multi-block independent swap.

8. **Add `StagePendingBuffers` + `PreserveVoiceState` callback steps** per RESEARCH §F lines 530-535:
```csharp
// Per RESEARCH §F line 530-535
StagePendingBuffers(perBlockBuffers!, musicalContext);
_engineForPrng?.Context.PrngRegistry.ResetAtRenderBoundary();  // LIVE-03 PRNG reseed
PreserveVoiceState(perBlockBuffers!);                          // LIVE-03 name-key voice diff
```

The `StreamingLoop` at lines 143-224 stays structurally the same; only the swap branch at lines 159-189 changes from single-`_pendingBuffer` to per-block dictionary lookup at each bar boundary.

---

### `flow-interpreter/LiveStatusPanel.cs` (NEW — ANSI 4-row redraw, TTY fallback)

**Analog:** UI-SPEC §"ANSI Live Status Panel" lines 122-180 specifies the 4-row layout, cell contract, redraw cadence (2 Hz heartbeat via `System.Threading.Timer`), and TTY-detection fallback.

**Closest in-tree analog for "ANSI + Console output":** `flow-interpreter/LiveReloadManager.cs:60-64` + `185-187` + `209-211`:

```csharp
// Source: flow-interpreter/LiveReloadManager.cs:60-64 (initial render error path)
if (initialBuffer == null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Initial execution failed: {errors}");
    Console.Error.WriteLine("Cannot start live reload without a valid audio buffer.");
    Console.ResetColor();
    return;
}

// Source: lines 185-187 (success swap)
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"Reloaded at bar {barNumber}");
Console.ResetColor();

// Source: lines 209-211 (write error)
Console.ForegroundColor = ConsoleColor.Red;
Console.Error.WriteLine($"Audio write error: {ex.Message}");
Console.ResetColor();
```

**Pattern to extend per UI-SPEC + RESEARCH §F:**

- TTY-detection block (LOCKED — copy verbatim per UI-SPEC §"Color color-disable detection" line 113-118):
```csharp
isColorEnabled = !Environment.GetEnvironmentVariable("NO_COLOR")
              && !cliArgs.Contains("--no-color")
              && !Console.IsOutputRedirected
              && Environment.GetEnvironmentVariable("TERM") != "dumb";
```
- 2 Hz redraw heartbeat via `System.Threading.Timer(500ms)` on its own thread — **NEVER on the audio thread** per Pitfall #21 lock.
- ANSI escape sequence table per RESEARCH §F lines 808-826 (`\x1b[2K` line clear, `\x1b[<N>;1H` row positioning, `\x1b[1m` bold, `\x1b[2m` dim, `\x1b[31m`/`32m`/`33m`/`36m` red/green/yellow/cyan).
- `PublishAdvisory(string body, AdvisoryLevel level, string? dedupKey = null)` API — wraps `RenderingDiagnostics.WarnOnce` when dedupKey is set so the row-4 sticky advisory and the stderr `[live]`/`[osc]` log stay in sync.
- Plain-line fallback: when `Console.IsOutputRedirected || TERM=dumb`, emit `[watch] tempo=120 timesig=4/4 bar=47 voices=8/32` style one-per-state-change lines per UI-SPEC §"TTY-Detection Fallback" line 174-180.

---

### `flow-interpreter/Repl.cs` (EXTEND — add `:help fn`, swap line input to PrettyPrompt)

**Analog (same file):**

Meta-command dispatch at lines 210-220:

```csharp
// Source: flow-interpreter/Repl.cs:210-220
private bool HandleCommand(string command)
{
    return command.ToLower() switch
    {
        ":quit" or ":q" or ":exit" => false,
        ":help" or ":h" => ShowHelp(),
        ":clear" or ":cls" => ClearScreen(),
        ":stop" => StopAudio(),
        _ => UnknownCommand(command)
    };
}
```

`Console.ReadLine`-based input at lines 105-208 (the 100+ line `ReadCompleteInput` / `ReadBackslashContinuation` / `IsInputComplete` paren-counting pipeline).

**Pattern to copy:**

1. **`:help <name>` extension** (D-38-09) — add a branch in `HandleCommand` that splits on whitespace; if a second token is present, look it up via `BuiltInDocs.TryGet(name)` (Phase 31 API; see HoverHandler analog below). UI-SPEC §"`:help fn` Meta-Command" line 259-280 specifies output format:
   - header: proc-name **bold + green** (ANSI `\x1b[1m\x1b[32m`)
   - signature: **dim** (`\x1b[2m`)
   - body: default
   - example: 1-line example with `--` comment annotation
2. **PrettyPrompt swap** — replace the existing `Console.ReadLine`-based `ReadCompleteInput` (lines 105-147) + `IsInputComplete` (lines 182-208) with `PrettyPrompt.Prompt` calls. The existing `IsInputComplete` lexer-based paren-counting logic is REUSED inside `IPromptCallbacks.DetermineKeyPressBehaviorAsync` to drive multi-line continuation (preserves backslash + paren-balance semantics).
3. **Tab completion wiring** — in the `IPromptCallbacks.GetCompletionItemsAsync` override, call `CompletionHandler.BuildItems(...)` from the flow-lsp ProjectReference (see CompletionHandler analog below). RESEARCH §G lines 871-901 shows the exact wiring (instantiate 4 indices once at Repl ctor; query `BuildItems` on each Tab).
4. **`Ctrl+C` handler at lines 31-37 PRESERVED** — PrettyPrompt's `IPromptCallbacks` exposes a hook for this so audio-stop continues to work.

---

### `flow-lang/StandardLibrary/Audio/InputFunctions.cs` (NEW — `(micBuffer)` builtin)

**Analog:** `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` (entire file shape — registration + per-signature delegates that consume an `AudioPlaybackManager`).

**Closest signature line analog (`PlaybackFunctions.cs:22-26`):**

```csharp
// Source: flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs:22-26
public static void Register(InternalFunctionRegistry registry, AudioPlaybackManager manager)
{
    // play(Buffer) -> Void
    var playBufferSig = new FunctionSignature("play", [BufferType.Instance],
        ParameterNames: ["buf"]);
    registry.Register("play", playBufferSig, args => PlayBuffer(args, manager));
```

**Pattern to copy:**

- `public static class InputFunctions { public static void Register(InternalFunctionRegistry registry) { ... } }` — no `AudioPlaybackManager` dep needed (capture has its own backend instance per RESEARCH §I line 962-963 sibling-class recommendation).
- Build a `FunctionSignature("micBuffer", [SecondType.Instance], ParameterNames: ["duration"])` plus a `(Double)` overload via the existing Music Types Quick Reference idiom (Second.IsCompatibleWith(Double) — see CLAUDE.md table line ~600).
- Inside `MicBuffer(args)`:
  1. `RenderingDiagnostics.WarnOnce("audio-in-attenuate:open", "[audio-in] mic stream attenuated -20 dB on open to prevent feedback")` per UI-SPEC advisory catalog row "Audio-input feedback guard"
  2. Open `PulseAudioCaptureBackend` (new sibling class, see next entry); `CaptureSamples(int frames, int channels)` returns raw float[]
  3. Optional resample via `ResampleLinear` (RESEARCH §J lines 1041-1066, ~30 LOC); fires `[audio-in] resampling capture stream from <N> Hz to 44100 Hz (linear interpolation)` once per native rate
  4. Apply scalar -20 dB attenuation (multiply by 10^(-20/20) = 0.1)
  5. Wrap result in `AudioBuffer` and return `Value.Buffer(...)`.
- Wire into `BuiltInFunctions.RegisterAllImplementations` at line 35-54 alongside `VisualizationFunctions.Register(registry)` (line 49):
```csharp
// Source: flow-lang/StandardLibrary/BuiltInFunctions.cs:49
VisualizationFunctions.Register(registry);
// ADD: Audio.InputFunctions.Register(registry);    // Phase 38 AUDIO-IN-01
```

---

### `flow-lang/Audio/PulseAudioCaptureBackend.cs` (NEW — `PA_STREAM_RECORD` P/Invoke)

**Analog:** `flow-lang/Audio/PulseAudioSimpleBackend.cs` (entire file, 313 lines — sibling-class mirror per RESEARCH §I line 962 "Recommend new sibling").

**P/Invoke surface to mirror** (`flow-lang/Audio/PulseAudioSimpleBackend.cs:273-311`):

```csharp
// Source: flow-lang/Audio/PulseAudioSimpleBackend.cs:273-311 — DIRECTION-SWAPPED MIRROR
private const int PA_STREAM_PLAYBACK = 1;      // ← change to PA_STREAM_RECORD = 2
private const int PA_SAMPLE_FLOAT32LE = 5;

[StructLayout(LayoutKind.Sequential)]
private struct pa_sample_spec
{
    public int format;
    public uint rate;
    public byte channels;
}

[DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
private static extern IntPtr pa_simple_new(
    IntPtr server,
    [MarshalAs(UnmanagedType.LPStr)] string name,
    int dir,
    IntPtr dev,
    [MarshalAs(UnmanagedType.LPStr)] string streamName,
    ref pa_sample_spec ss,
    IntPtr channelMap,
    IntPtr attr,
    out int error);

// ← NEW (mirror of pa_simple_write at line 301-302; verified from pulseaudio/src/pulse/def.h)
[DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
private static extern int pa_simple_read(IntPtr s, IntPtr data, nuint bytes, out int error);

[DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
private static extern void pa_simple_free(IntPtr s);
// ... pa_simple_drain / pa_simple_flush / pa_strerror unchanged
```

**`Initialize` mirror** (`flow-lang/Audio/PulseAudioSimpleBackend.cs:36-78`):

```csharp
// Source: flow-lang/Audio/PulseAudioSimpleBackend.cs:36-78 — DIRECTION-SWAPPED
_connection = pa_simple_new(
    IntPtr.Zero,         // default server
    "flow-lang",
    PA_STREAM_PLAYBACK,  // ← change to PA_STREAM_RECORD
    IntPtr.Zero,         // default device
    "playback",          // ← change to "capture"
    ref sampleSpec,
    IntPtr.Zero,
    IntPtr.Zero,
    out error);
```

**`CaptureSamples` method** — mirrors `Play` at lines 80-156 but uses `pa_simple_read` instead of `pa_simple_write`; full body in RESEARCH §I lines 990-1021.

**Locking idiom** — preserve the `lock (_lock)` discipline around `_connection`-touching code per `PulseAudioSimpleBackend.cs:43-77, 122-129`.

---

### `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` (EXTEND — add `DiffByVoiceName`)

**Analog:** itself, lines 23-28 (`AsyncLocal<int?>` instrumentation precedent) + lines 124-169 (`AllocateWithPool`).

```csharp
// Source: flow-lang/StandardLibrary/Audio/VoiceAllocator.cs:23-28
private static readonly AsyncLocal<int?> _lastPoolSizeUsedForTests = new();
public static int? LastPoolSizeUsedForTests
{
    get => _lastPoolSizeUsedForTests.Value;
    set => _lastPoolSizeUsedForTests.Value = value;
}
```

**Pattern to copy:** add new `public static (List<Voice> Preserved, List<Voice> Dropped, List<Voice> Added) DiffByVoiceName(IReadOnlyList<Voice> prev, IReadOnlyList<Voice> next)` static helper alongside `Allocate` and `AllocateWithPool`. Body per RESEARCH §B lines 662-684:

```csharp
public static (List<Voice> Preserved, List<Voice> Dropped, List<Voice> Added)
    DiffByVoiceName(IReadOnlyList<Voice> prev, IReadOnlyList<Voice> next)
{
    var prevByName = prev.ToDictionary(v => v.Name, v => v, StringComparer.Ordinal);
    var nextByName = next.ToDictionary(v => v.Name, v => v, StringComparer.Ordinal);
    var preserved = new List<Voice>();
    var dropped = new List<Voice>();
    var added = new List<Voice>();
    foreach (var (name, voice) in prevByName)
    {
        if (nextByName.TryGetValue(name, out var newVoice)) preserved.Add(newVoice);
        else dropped.Add(voice);
    }
    foreach (var (name, voice) in nextByName)
        if (!prevByName.ContainsKey(name)) added.Add(voice);
    return (preserved, dropped, added);
}
```

Reuse the existing `ApplyFadeOut(Voice voice, int sampleRate)` helper at lines 87-104 for the `dropped` voices.

---

### `flow-lang/StandardLibrary/Audio/Voice.cs` (EXTEND — add `Name` + `CopyStateFrom`)

**Analog:** itself (lines 1-40).

**CRITICAL FINDING:** Voice currently has NO `Name` property (verified — see lines 1-40 entire class). RESEARCH §B line 696 confirms: *"Plan 38-03 may need to add a `Name` property if not already there"* — audit confirms YES, it needs adding.

**Pattern to copy (extend the existing class):**

```csharp
// Source: flow-lang/StandardLibrary/Audio/Voice.cs:1-40 (existing)
public class Voice
{
    public AudioBuffer Buffer { get; }
    public double OffsetBeats { get; set; }
    public double Gain { get; set; }
    public double Pan { get; set; }

    public Voice(AudioBuffer buffer, double offsetBeats)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        OffsetBeats = offsetBeats;
        Gain = 1.0;
        Pan = 0.0;
    }
    // ADD: public string Name { get; init; } = "";  // e.g. "piano:0", "drums:1"
    // ADD: public void CopyStateFrom(Voice prev) — transfer playback offset + envelope phase
}
```

`Name` is set by `SongRenderer` at voice-allocation time as `"{instrumentLabel}:{ordinalIdx}"` matching the Phase 28 doc convention. `CopyStateFrom` body sketched per RESEARCH §B step 3 line 690-693 ("transfer the previous voice's playback offset + envelope state").

---

### `flow-lang/StandardLibrary/Network/OscFunctions.cs` (NEW — 5 OSC builtins)

**Analog:** `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` (registration shape — sibling pattern under a new `Network/` subdirectory) + `flow-lang/Audio/PulseAudioSimpleBackend.cs:80-155` (Task.Run + CancellationToken lifecycle for the listener background loop).

**Registration shape (mirror of PlaybackFunctions.cs:20-73):**

```csharp
// Mirror of: flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs:20-30
public static void Register(InternalFunctionRegistry registry)
{
    // oscSend(String host, Int port, String path, ...varargs) -> Void
    var sendSig = new FunctionSignature("oscSend",
        [StringType.Instance, IntType.Instance, StringType.Instance],
        ParameterNames: ["host", "port", "path"],
        Varargs: true);
    registry.Register("oscSend", sendSig, OscSend);

    var listenSig = new FunctionSignature("oscListen",
        [IntType.Instance, StringType.Instance, FunctionType.Instance],
        ParameterNames: ["port", "path", "handler"]);
    registry.Register("oscListen", listenSig, OscListen);
    // ... oscStop / oscBundle / oscSendBundle similarly
}
```

**Lifecycle pattern (mirror of `PulseAudioSimpleBackend.Play` lines 80-155):**

The blocking listener loop in `OscListen` mirrors `Play`'s `Task.Run`-with-CancellationToken pattern. Full skeleton per RESEARCH §K lines 1093-1118:

```csharp
var receiver = new OscReceiver(port);
var cts = new CancellationTokenSource();
var task = Task.Run(() =>
{
    receiver.Connect();
    while (!cts.IsCancellationRequested)
    {
        try
        {
            OscPacket packet = receiver.Receive();   // Blocking
            DispatchPacket(packet, path, handler, 0);
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[osc] receive error on port {port}: {ex.Message}");
        }
    }
    receiver.Dispose();
}, cts.Token);
return Value.OscHandle(new OscHandleData {
    Port = port, Path = path, Receiver = receiver, Cts = cts, ListenerTask = task
});
```

**Type-tag inference dispatch** per RESEARCH §L lines 1180-1201:
```csharp
private static object[] InferOscArgs(IReadOnlyList<Value> flowArgs)
{
    var oscArgs = new object[flowArgs.Count];
    for (int i = 0; i < flowArgs.Count; i++)
    {
        var v = flowArgs[i];
        oscArgs[i] = v.Type switch
        {
            IntType => (int)v.Data!,
            LongType => (long)v.Data!,
            FloatType => (float)(double)v.Data!,
            DoubleType => (double)v.Data!,
            StringType => (string)v.Data!,
            SymbolType => (string)v.Data!,
            BoolType => (bool)v.Data!,
            BufferType => AudioBufferToBlob((AudioBuffer)v.Data!),
            _ => throw new ArgumentException(
                $"[osc] unsupported arg type at index {i}: {v.Type.Name} — use Int/Long/Float/Double/String/Symbol/Bool/Buffer")
        };
    }
    return oscArgs;
}
```

**Bundle nesting depth-cap 8** per D-38-15 (mirrors Phase 36 T-36-17 / Phase 39 D-39-19 DoS-guard precedent) per RESEARCH §K lines 1119-1147.

**Rate-limit gate** (D-38-14 sample-and-hold) per RESEARCH §M lines 1218-1228:
```csharp
private static readonly ConcurrentDictionary<string, long> _lastFireTimeMs = new();
private const int RateLimitWindowMs = 5;   // 1/200Hz = 5ms

private static void InvokeHandlerWithRateLimit(string path, Value handler, OscMessage msg)
{
    var nowMs = Environment.TickCount64;
    var lastMs = _lastFireTimeMs.GetOrAdd(path, 0L);
    if (nowMs - lastMs < RateLimitWindowMs) return;
    _lastFireTimeMs[path] = nowMs;
    // ... invoke flow lambda
}
```

---

### `flow-lang/TypeSystem/SpecialTypes/OscHandleType.cs` (NEW)

**Analog:** `flow-lang/TypeSystem/SpecialTypes/SfzType.cs` (entire file, 36 lines).

**Full analog file:**

```csharp
// Source: flow-lang/TypeSystem/SpecialTypes/SfzType.cs:22-35
public sealed class SfzType : FlowType
{
    private SfzType() { }
    public static SfzType Instance { get; } = new();
    public override string Name => "Sfz";
    public override int GetSpecificity() => 150;
    public override bool IsCompatibleWith(FlowType target) => target is SfzType;
    public override bool CanConvertTo(FlowType target) => target is SfzType;
}
```

**Pattern to copy verbatim** (replace class name + Name string + specificity to 151 per CLAUDE.md "Specificity 150 — slotted above all existing music types"; MarkovModel=148, LsystemModel=149, Sfz=150, OscHandle=151).

---

### `flow-lang/Runtime/Value.cs` (EXTEND — add `OscHandle` factory)

**Analog:** itself, lines 60-101 (Tuning / Sfz / MarkovModel / LsystemModel factories).

**Closest line analog (`Value.cs:60-72`):**

```csharp
// Source: flow-lang/Runtime/Value.cs:60-72
public static Value Tuning(StandardLibrary.Audio.Tuning.ResolvedTuning resolved)
    => new(resolved, TuningType.Instance);

/// <summary>
/// Phase 33 Plan 33-02 — wraps a <see cref="StandardLibrary.Audio.Sfz.SfzData"/>
/// reference in a Flow <see cref="Value"/> typed as <see cref="SfzType.Instance"/>.
/// Identity follows reference equality per CONTEXT § "Claude's Discretion": two
/// <c>(loadSfz #violin)</c> calls produce distinct Values even with identical
/// resolved paths (Phase 33 doesn't cache at the value layer; mirrors Phase 32's
/// <see cref="Value.Tuning"/> contract).
/// </summary>
public static Value Sfz(StandardLibrary.Audio.Sfz.SfzData data)
    => new(data, SfzType.Instance);
```

**Pattern to copy:** add `public static Value OscHandle(StandardLibrary.Network.OscHandleData handle) => new(handle, OscHandleType.Instance);` with the analogous xmldoc citing D-38-16 + reference-identity precedent.

---

### `flow-lang/StandardLibrary/VisualizationFunctions.cs` (EXTEND — articulation glyphs + tick row + `(inspect)` alias)

**Analog:** itself, lines 117-131 (note-placement loop) + 166-177 (bottom separator pattern) + 19-29 (Register entry).

**Note-placement loop to extend** (`flow-lang/StandardLibrary/VisualizationFunctions.cs:117-131`):

```csharp
// Source: flow-lang/StandardLibrary/VisualizationFunctions.cs:117-131
// Fill in notes
foreach (var (midi, label, startBeat, duration) in noteEvents)
{
    int row = maxMidi - midi; // top = highest pitch
    int startCol = (int)Math.Round(startBeat * columnsPerBeat);
    int endCol = (int)Math.Round((startBeat + duration) * columnsPerBeat);
    endCol = Math.Min(endCol, gridWidth);

    for (int c = startCol; c < endCol; c++)
    {
        if (c >= 0 && c < gridWidth)
            grid[r, c] = '#';
    }
}
```

**Pattern to extend** per RESEARCH §N lines 1258-1300 (locked ASCII glyph table per UI-SPEC §"Glyph Inventory"):

```csharp
// Modified per RESEARCH §N — articulation glyph at startCol only; '#' for sustain cells
foreach (var (midi, label, startBeat, duration, articulation) in noteEvents)
{
    int row = maxMidi - midi;
    int startCol = (int)Math.Round(startBeat * columnsPerBeat);
    int endCol = (int)Math.Round((startBeat + duration) * columnsPerBeat);
    endCol = Math.Min(endCol, gridWidth);
    char onsetGlyph = articulation switch
    {
        Articulation.Accent => '>',
        Articulation.Staccato => '.',
        Articulation.Marcato => '^',
        Articulation.Tenuto => '_',
        Articulation.Sforzando => '!',
        _ => '#'  // Normal — Legato handled by separate gap-cell pass
    };
    for (int c = startCol; c < endCol; c++)
        if (c >= 0 && c < gridWidth)
            grid[row, c] = (c == startCol) ? onsetGlyph : '#';
}
```

**Bottom separator pattern to mirror for tick-mark row** (`VisualizationFunctions.cs:166-177`):

```csharp
// Source: flow-lang/StandardLibrary/VisualizationFunctions.cs:166-177
sb.Append(new string(' ', labelWidth));
sb.Append(" +");
for (int c = 0; c < gridWidth; c++)
{
    if (barLineColumns.Contains(c) && c > 0)
        sb.Append('+');
    else
        sb.Append('-');
}
sb.AppendLine("+");
```

The NEW tick-mark row (added ABOVE the first pitch row per UI-SPEC §"Tick-Mark Row" lines 217-228) reuses this `+`/`-`/`|` rendering shape but prepends bar numbers at each bar's first beat.

**Register `(inspect seq)` alias** — extend the `Register` method at lines 19-29:

```csharp
// Source: flow-lang/StandardLibrary/VisualizationFunctions.cs:19-29
public static void Register(InternalFunctionRegistry registry)
{
    var sig = new FunctionSignature("visualize", [SequenceType.Instance],
        ParameterNames: ["seq"]);
    registry.Register("visualize", sig, Visualize);

    var sig2 = new FunctionSignature("visualize", [BufferType.Instance],
        ParameterNames: ["buf"]);
    registry.Register("visualize", sig2, VisualizeBuffer);
    // ADD: var sig3 = new FunctionSignature("inspect", [SequenceType.Instance], ParameterNames: ["seq"]);
    // ADD: registry.Register("inspect", sig3, Visualize);  // Same dispatch
}
```

---

### `flow-lang/StandardLibrary/BuiltInFunctions.cs` (modify — register InputFunctions + OscFunctions)

**Analog:** itself, line 35-54 (`RegisterAllImplementations`).

```csharp
// Source: flow-lang/StandardLibrary/BuiltInFunctions.cs:35-54
public static void RegisterAllImplementations(InternalFunctionRegistry registry)
{
    RegisterStdLib(registry);
    RegisterMath(registry);
    ...
    VisualizationFunctions.Register(registry);
    BufferPrinter.Register(registry);
    Composition.PolyrhythmFunctions.Register(registry);
    Composition.VariationFunctions.Register(registry);
    Audio.Vocalization.VocalizationFunctions.Register(registry);
    // ADD: Audio.InputFunctions.Register(registry);        // Phase 38 AUDIO-IN-01
    // ADD: Network.OscFunctions.Register(registry);        // Phase 38 OSC-01..02 (gated by @osc enable flag)
}
```

The `@osc` enable flag follows the Phase 33 SFZ pattern — `ExecutionContext.OscEnabled` boolean flipped by the `__enableOscModule` internal proc; builtins registered always but error if flag is false (per `sfz.flow` lines 14-25 comment block).

---

### `flow-lang/osc.flow` (NEW — `use "@osc"` activation module)

**Analog:** `flow-lang/sfz.flow` (entire file, 77 lines) + `flow-lang/notation-io.flow` (entire file, 42 lines).

**Closest analog (notation-io.flow lines 1-42 — simplest opt-in module shape):**

```
Note: Notation IO — opt-in via `use "@notation-io"`
Note: Phase 39 D-39-01 — single-module surface for the 4 notation IO builtins:
Note:   writeMusicXML(String, Song) → Void          [Plan 39-01 XML-01]
Note:   writeLilyPond(String, Song) → Void           [Plan 39-02 LILY-01]
Note:   abc(String) → Section | Array[Section]       [Plan 39-03 ABC-01 / ABC-02]
Note:   mml(String) → Sequence                       [Plan 39-04 MML-01]

use "@std"

Note: ===== Internal C# Function Declarations =====

internal proc __enableNotationIoModule ()

internal proc writeMusicXML (String: path, Song: song)
internal proc writeLilyPond (String: path, Song: song)
internal proc abc (String: source)
internal proc mml (String: source)

(__enableNotationIoModule)
```

**Pattern to copy:** identical structure — header comment + `use "@std"` + `internal proc __enableOscModule()` + 5 `internal proc` forward decls for `oscSend` / `oscListen` / `oscStop` / `oscBundle` / `oscSendBundle` + trailing `(__enableOscModule)` side-effect line.

Add `osc.flow` to `flow-lang.csproj` `<None Update="osc.flow">` clause per `flow-lang.csproj:43-46` notation-io.flow precedent.

---

### `flow-lang/audio.flow` (EXTEND — add `micBuffer` forward decl)

**Analog:** itself, lines 6-31 (`internal proc` forward decls).

```
Note: Source: flow-lang/audio.flow:7-22 (existing internal proc pattern)
internal proc createBuffer(Int: frames, Int: channels, Int: sampleRate)
internal proc getFrames(Buffer: buffer)
internal proc getSample(Buffer: buffer, Int: frame, Int: channel)
internal proc setSample(Buffer: buffer, Int: frame, Int: channel, Double: value)
```

**Pattern to copy:** add `internal proc micBuffer(Second: duration)` plus the `(Double: duration)` overload per CLAUDE.md Music Types §"Second IsCompatibleWith Double, Float" — both arities must be declared.

---

### `flow-cli/Commands/WatchCommand.cs` (minor edit — pass new manager config if needed)

**Analog:** itself, lines 36-46.

```csharp
// Source: flow-cli/Commands/WatchCommand.cs:36-46
device ??= FlowConfig.Active.DefaultAudioDevice;
if (!File.Exists(script.FullName))
{
    Console.Error.WriteLine($"Error: File not found: {script.FullName}");
    return 1;
}
var fullPath = Path.GetFullPath(script.FullName);
using var manager = new LiveReloadManager(fullPath, device);
manager.Run();
return 0;
```

**Pattern to copy:** the `LiveReloadManager(fullPath, device)` constructor signature should remain backwards-compatible — the rewrite of LiveReloadManager keeps `(string filePath, string? deviceName = null)` per CONTEXT D-38-01 ("composers with existing .flow files keep working without edits"). If new optional args (e.g., `bool enableAnsiPanel`) are added, they must default to "current behavior" so this 50-line file requires only a comment update.

---

### `flow-lang/flow-lang.csproj` (modify — add Rug.Osc + osc.flow CopyToOutputDirectory)

**Analog:** itself, lines 10-13 (`<PackageReference>`) + lines 43-46 (notation-io.flow `<None Update>`).

```xml
<!-- Source: flow-lang/flow-lang.csproj:10-13 -->
<ItemGroup>
  <PackageReference Include="Melanchall.DryWetMidi" Version="8.0.3" />
  <PackageReference Include="Pidgin" Version="3.5.1" />
</ItemGroup>

<!-- Source: flow-lang/flow-lang.csproj:43-46 -->
<None Update="notation-io.flow">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</None>
```

**Pattern to copy:** add `<PackageReference Include="Rug.Osc" Version="1.2.5" />` inside the existing `<ItemGroup>` at lines 10-13; add a matching `<None Update="osc.flow">` block alongside the other `.flow` module declarations.

---

### `flow-interpreter/flow-interpreter.csproj` (modify — add PrettyPrompt + flow-lsp ProjectReference)

**Analog:** itself (entire file, 16 lines).

```xml
<!-- Source: flow-interpreter/flow-interpreter.csproj:3-5 -->
<ItemGroup>
  <ProjectReference Include="..\flow-lang\flow-lang.csproj" />
</ItemGroup>
```

**Pattern to copy:** add a second `<ProjectReference Include="..\flow-lsp\flow-lsp.csproj" />` in the same `<ItemGroup>` per D-38-12 in-process-LSP embed (RESEARCH §G line 868); add a new `<ItemGroup>` with `<PackageReference Include="PrettyPrompt" Version="4.1.1" />` per D-38-11 / RESEARCH §H pick.

---

### `flow-lang.Tests/Integration/Phase38/*.cs` (NEW — 23 xUnit tests)

**Analog:** `flow-lang.Tests/Integration/Phase37/GranularSynthesisTests.cs` (entire file, ~45 lines for the header pattern).

```csharp
// Source: flow-lang.Tests/Integration/Phase37/GranularSynthesisTests.cs:1-39
using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-01 — granular builtin composability with reverb / gain / pan /
/// filter. Filled by Plan 37-01 Task 3 (this plan) alongside
/// <c>GranularEngine.cs</c> + <c>GranularFunctions.cs</c>.
/// </summary>
[Collection("FlowScripts")]
public class GranularSynthesisTests : IDisposable
{
    public GranularSynthesisTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }
    ...
}
```

**Pattern to copy:**

- Directory: `flow-lang.Tests/Integration/Phase38/` (NEW — matches existing Phase29/Phase30/.../Phase37 directories).
- `[Collection("FlowScripts")]` decorator + `IDisposable` ctor/Dispose calling `RenderingDiagnostics.ResetForTesting()` + `FlowConfig.Reset()` — required for advisory-dedup tests so each Fact starts clean.
- xmldoc cites the phase + REQ + plan number (Phase 37 DSP-01 → Phase 38 LIVE-01 / REPL-01 / AUDIO-IN-01 / OSC-01).
- Test method naming: descriptive `Verb_Condition_Outcome` (e.g., `EditWithStaleClosure_KeepsPreviousBuffer_EmitsDedupAdvisory` per RESEARCH §O line 1321).

---

### `tests/test_live_*.flow` + `test_repl_*.flow` + `test_audio_in_*.flow` + `test_osc_*.flow` + `test_visualize_*.flow` (NEW — `.flow` smoke tests)

**Analog:** `tests/test_live_reload.flow` (entire file, 20 lines) + `tests/test_repl_autoimport.flow` (entire file, 11 lines).

**Full analog (`tests/test_live_reload.flow:1-20`):**

```
Note: Test script for live reload (--watch mode)
Note: Run: dotnet run --project flow-interpreter -- --watch tests/test_live_reload.flow
Note: Edit notes while playing to test bar-boundary reload

use "@std"
use "@audio"

tempo 120 {
  timesig 4/4 {
    key Cmajor {
      section main {
        Sequence melody = | C4q D4q E4q F4q | G4q A4q B4q C5q |
      }
      Song song = [main]
      Buffer result = (renderSong song "piano")
      (play result)
    }
  }
}
```

**Full analog (`tests/test_repl_autoimport.flow:1-11`):**

```
Note: This tests that explicit imports still work in script mode
use "@std"
use "@audio"
use "@collections"
(print "Script mode: explicit imports work")
Int[] nums = (list 1 2 3)
(print (concat "List length: " (str (len nums))))
(print "PASS: script mode with explicit imports")
```

**Pattern to copy:**
- Header `Note:` block describing what + how to run.
- `use "@std"` + relevant module imports.
- Body uses standard Flow syntax.
- Console output is the verification harness — print a `PASS: ...` sentinel on success (the existing `test_*.flow` files all follow this convention).

---

### `examples/live/*.flow` + `repl_session.md` (NEW — 5 composer-facing chapters)

**Analog:** `examples/notation/to_musicxml.flow` (header lines 1-30 narrated-chapter pattern) + `examples/dsp/granular.flow` (lines 1-30 Phase 37 chapter shape with W4 LOCK + composer-facing knobs section).

**Full analog header (`examples/notation/to_musicxml.flow:1-23`):**

```
Note: =====================================================================
Note: to_musicxml.flow — Phase 39 MusicXML export chapter
Note: =====================================================================
Note:
Note: Composer writes a 4-bar piano piece in C major, exports it as MusicXML
Note: 3.1 partwise. The output opens directly in MuseScore (D-v1.5-08 reference
Note: consumer), Sibelius, Dorico, Finale, or LilyPond.
Note:
Note: Run:
Note:   dotnet run --project flow-interpreter examples/notation/to_musicxml.flow
Note:
Note: Output:
Note:   examples/notation/output/to_musicxml.musicxml — well-formed MusicXML 3.1
Note:
Note: Verify (optional, requires MuseScore in PATH):
Note:   mscore examples/notation/output/to_musicxml.musicxml
Note:
Note: The MusicXML round-trip CI gate (XML-02) charitable-skips when mscore is
Note: absent per D-39-08.

use "@notation-io"
use "@std"
```

**Pattern to copy:** five chapters per CONTEXT `<canonical_refs>` line 151-156:
- `hello_live.flow` — minimal `live 1bar { ... }` with one synth voice
- `multi_block.flow` — `live 1bar { drums }` + `live 2bar { pad }` (D-38-02 demo)
- `mic_granular.flow` — `(micBuffer 4s) -> (granular ...) -> play`
- `osc_controller.flow` — `(oscListen 7777 "/fader/1" handler)` + `(oscSend "localhost" 7777 "/fader/1" 0.5)` round-trip
- `repl_session.md` — narrated MD transcript (not a `.flow` chapter; mirrors `examples/notation/README.md` shape per `examples/notation/` directory listing)

Each `.flow` chapter follows the `Note: ===== ... =====` header + `Run:` + `Output:` + `use "@..."` import block convention.

---

## Shared Patterns

### Pattern S1: Reference-identity Value type for runtime resources

**Source:** Phase 32 Tuning + Phase 33 Sfz + Phase 36 MarkovModel/LsystemModel — all 4 ship the same shape.

**Apply to:** `OscHandleType.cs` (Phase 38 NEW), `Value.OscHandle(...)` factory.

**Canonical excerpt** (`flow-lang/Runtime/Value.cs:71-72`):

```csharp
public static Value Sfz(StandardLibrary.Audio.Sfz.SfzData data)
    => new(data, SfzType.Instance);
```

`SfzType.cs:22-35` is the type-side singleton; `Value.cs:71` is the factory side. Both are 1-line copies with class/namespace substitution.

---

### Pattern S2: Charitable-interpretation stderr advisory with WarnOnce dedup

**Source:** `flow-lang/Diagnostics/RenderingDiagnostics.cs:29-36`

```csharp
public static void WarnOnce(string sentinelKey, string message)
{
    lock (_lock)
    {
        if (!_emitted.Add(sentinelKey)) return;
    }
    Console.Error.WriteLine(message);
}
```

**Apply to:** ALL Phase 38 stderr advisories per UI-SPEC §"Advisory Catalog" lines 322-341 (10+ advisory dedup-key/message pairs). Each call site passes a unique sentinel:
- `"live-timeout:{line}"` (D-38-07)
- `"live-stale-closure:{name}:{line}"` (D-38-07)
- `"live-parse:{line}"`
- `"live-determinism-optout:{line}"` (D-v1.5-07, one-shot per live block per process)
- `"live-fscope-edit:{filepath}:{line}"` (D-38-04)
- `"audio-in-attenuate:open"` (Pitfall #24)
- `"audio-in-resample:{N}"`
- `"osc-bind:{port}"`
- `"osc-infer:{path}:{arg-index}"` (D-38-13)
- `"osc-bundle-depth:{path}"` (D-38-15)

Test isolation: `RenderingDiagnostics.ResetForTesting()` in test ctor/Dispose per Phase 37 `GranularSynthesisTests` precedent.

---

### Pattern S3: Module-activation `use "@name"` opt-in

**Source:** `flow-lang/sfz.flow` (full file shape) + `flow-lang/notation-io.flow` (simpler shape).

**Apply to:** `flow-lang/osc.flow` (NEW). Always:
1. Header `Note:` block citing the phase + decisions.
2. `use "@std"` at top.
3. `internal proc __enableXxxModule()` marker (no args for simplest case; takes a dict for sfz-like config).
4. One `internal proc` per public builtin (forward declaration with type sig).
5. Trailing side-effect line `(__enableXxxModule)` flips the `ExecutionContext.XxxEnabled` flag.
6. Add the `.flow` file to `flow-lang.csproj` `<None Update="...">` block per existing notation-io.flow entry.

---

### Pattern S4: Builtin registration in `RegisterAllImplementations`

**Source:** `flow-lang/StandardLibrary/BuiltInFunctions.cs:35-54`

```csharp
public static void RegisterAllImplementations(InternalFunctionRegistry registry)
{
    RegisterStdLib(registry);
    ...
    Audio.Vocalization.VocalizationFunctions.Register(registry);
}
```

**Apply to:** `Audio.InputFunctions.Register(registry)` + `Network.OscFunctions.Register(registry)` calls added in the same block. The audio-manager-bound 2-arg overload at lines 59-64 is for `PlaybackFunctions` only — InputFunctions doesn't need it (owns its own backend per RESEARCH §I sibling-class recommendation), so it goes in the no-arg overload.

---

### Pattern S5: 2-overload `Register` for builtins (Music Types Quick Reference compatibility)

**Source:** `flow-lang/StandardLibrary/VisualizationFunctions.cs:19-29` (Sequence + Buffer overloads) + general CLAUDE.md Music Types Quick Reference (Second `IsCompatibleWith Double|Float`).

**Apply to:** `InputFunctions.MicBuffer` registers BOTH `[SecondType.Instance]` AND `[DoubleType.Instance]` overloads so `(micBuffer 4s)` and `(micBuffer 4.0)` both resolve. OverloadResolver picks the exact-match per CLAUDE.md `OverloadResolver.cs` rules.

---

### Pattern S6: PRNG routing via `Runtime/PrngRegistry`

**Source:** `flow-lang/Runtime/PrngRegistry.cs:115-126` + CI gate `PrngRegistryNewRandomGateTests` (per CLAUDE.md "PRNG Routing (Phase 36 D-v1.5-06)").

**Apply to:** Phase 38 has TWO consumers:
1. `LiveReloadManager.StagePendingBuffers` calls `engine.Context.PrngRegistry.ResetAtRenderBoundary()` per swap (RESEARCH §D).
2. `OscFunctions` MUST NOT introduce any `new Random(...)` constructions (the CI gate fires on `flow-lang/StandardLibrary/Network/*`). If any stochastic behavior is needed (e.g., randomized backoff for `oscListen` retry), route via `PrngRegistry.GetRandom((SourceLocation, "osc_listener"))` per Phase 36 precedent.

---

## No Analog Found

All 26 file targets have a concrete in-tree analog with file:line citations. The only "thin" analog is `LambdaCaptureAuditor.cs` — the AST-walking read-only auditor — for which the closest in-tree precedent is the switch-on-AST-record-type dispatch pattern in `flow-lang/Interpreter/ExpressionEvaluator.cs` rather than a dedicated existing auditor class. RESEARCH §C lines 706-744 provides the full sketch; this is `role-match` not `exact`, but the dispatch style is identical to the rest of the interpreter.

| File | Role | Data Flow | Reason | Mitigation |
|------|------|-----------|--------|------------|
| `flow-lang/Interpreter/LambdaCaptureAuditor.cs` | AST walker | transform | No dedicated read-only AST auditor in current tree | Switch-dispatch idiom from `ExpressionEvaluator.cs` + sketch in RESEARCH §C lines 706-744 is the substitute |

---

## Metadata

**Analog search scope:**
- `flow-lang/Ast/Statements/` (all 9 statement records)
- `flow-lang/Lexing/` (TokenType.cs + SimpleLexer.cs)
- `flow-lang/Parsing/Parser.cs` (musical-context dispatch block)
- `flow-lang/Interpreter/` (Interpreter.cs + ExpressionEvaluator.cs)
- `flow-lang/Runtime/` (Value.cs + PrngRegistry.cs + ExecutionContext.cs)
- `flow-lang/TypeSystem/SpecialTypes/` (Tuning/Sfz/MarkovModel/LsystemModel types)
- `flow-lang/StandardLibrary/Audio/` (30 files — Voice, VoiceAllocator, PlaybackFunctions, Synthesizers, etc.)
- `flow-lang/StandardLibrary/` (VisualizationFunctions, BuiltInFunctions, BuiltInDocs)
- `flow-lang/Diagnostics/RenderingDiagnostics.cs`
- `flow-lang/Audio/PulseAudioSimpleBackend.cs`
- `flow-lang/*.flow` (sfz.flow + notation-io.flow + audio.flow opt-in module patterns)
- `flow-interpreter/Repl.cs` + `LiveReloadManager.cs`
- `flow-lsp/Handlers/CompletionHandler.cs` + `HoverHandler.cs`
- `flow-lang.Tests/Integration/Phase37/` (~16 xUnit precedents)
- `tests/test_*.flow` (existing convention)
- `examples/notation/` + `examples/dsp/` + `examples/generative/` (Phase 36/37/39 chapter patterns)

**Files scanned:** ~50 (Read calls) + ~10 (Bash grep/ls) + 3 phase-context docs (CONTEXT, RESEARCH, UI-SPEC).

**Pattern extraction date:** 2026-05-23

**Critical pre-execution audit flagged for planner:**
- **Voice.Name property** does NOT exist on `flow-lang/StandardLibrary/Audio/Voice.cs:6-40` (verified). Plan 38-03 must ADD it as a first task (set by SongRenderer at allocation as `"{instrument}:{ordinal}"` per Phase 28 CLAUDE.md doc convention). The diff helper at `DiffByVoiceName` blocks on this.
- **FlowEngine.Execute does NOT accept a CancellationToken** at `flow-lang/Core/FlowEngine.cs:221` (verified — `public bool Execute(string source, string? fileName = null)`). RESEARCH §E Option A (Task.Run + Wait timeout, cooperative orphan-worker leak) is the recommended Plan 38-01 approach; Option B (thread CT through Interpreter) is heavier and only revisited if HUMAN-UAT reports worker accumulation.
- **`LiveReloadManager._pendingBuffer` is currently a single field** (line 23) — Plan 38-01 + 38-02 promotes it to `Dictionary<int, LiveBlockBuffer>` keyed by `BlockId` per D-38-02 multi-block independent swap.
- **Phase 38 test directory does NOT yet exist** — Plan 38-01 first task creates `flow-lang.Tests/Integration/Phase38/`. The Phase 36 tests live at `flow-lang.Tests/Phase36/` (no `Integration/` prefix) but Phase 37 + Phase 39 use the `Integration/Phase37/` and `Integration/Phase39/` shape — Phase 38 follows the more recent Phase 37/39 convention.
