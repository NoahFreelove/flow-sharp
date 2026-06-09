---
phase: 41-reach-v1-5-closer
reviewed: 2026-06-07T00:00:00Z
depth: standard
files_reviewed: 20
files_reviewed_list:
  - flow-lang/Lexing/SimpleLexer.cs
  - flow-lang/Lexing/TokenType.cs
  - flow-lang/Parsing/Parser.cs
  - flow-lang/Ast/Statements/ProcDeclaration.cs
  - flow-lang/StandardLibrary/BuiltInDocs.cs
  - flow-lang/Audio/WasapiBackend.cs
  - flow-lang/Audio/AudioPlaybackManager.cs
  - flow-lang/flow-lang.csproj
  - flow-cli/Commands/DocCommand.cs
  - flow-cli/Commands/CommandRegistry.cs
  - flow-cli/Doc/DocModel.cs
  - flow-cli/Doc/DocCollector.cs
  - flow-cli/Doc/DocExampleRunner.cs
  - flow-cli/Doc/DocGenerator.cs
  - flow-cli/Doc/HtmlEmitter.cs
  - flow-cli/Doc/MarkdownEmitter.cs
  - flow-cli/Doc/ContentHashCache.cs
  - scripts/publish.sh
  - flow-jetbrains/build.gradle.kts
  - examples/edm/pulse.flow
findings:
  critical: 3
  warning: 6
  info: 3
  total: 12
status: issues_found
---

# Phase 41: Code Review Report

**Reviewed:** 2026-06-07
**Depth:** standard
**Files Reviewed:** 20
**Status:** issues_found

## Summary

Phase 41 spans four distinct deliverables: (1) the `///` doc-comment grammar layered onto `SimpleLexer` and `Parser`; (2) `WasapiBackend`, a new Windows audio backend bridging NAudio's pull model to the push `IAudioBackend` contract; (3) the `flow doc` reference generator pipeline (`DocCollector`, `DocExampleRunner`, `DocGenerator`, `HtmlEmitter`, `MarkdownEmitter`, `ContentHashCache`); and (4) cross-cutting work — `scripts/publish.sh` five-RID binary packaging, the JetBrains plugin `build.gradle.kts` marketplace plumbing, and the EDM showcase `examples/edm/pulse.flow`.

The `///` lexer grammar and parser binding are structurally correct and the three-way isolation promised by the csproj (Desktop-only PackageReference, `<Compile Remove>`, `#if !FLOW_WEB`) for `WasapiBackend` holds. Three **blockers** are present: a doc-comment buffer leak across proc boundaries when a non-proc non-comment statement appears between a `///` block and a following proc; a resource leak in `WasapiBackend.Play` when the `BufferedWaveProvider`/`WasapiOut` pair is replaced mid-stream by `EnsureInitialized`; and a `DocExampleRunner` `Console.SetOut/SetError` restore gap that can permanently silence the CLI host process if a worker thread throws before the `finally` restore. Six warnings cover a busy-poll timing hazard in `WasapiBackend.Play`, a `ContentHashCache.Save` that is not atomic (partial write on crash loses the cache), Markdown injection in `MarkdownEmitter`, a missing `patterns.flow` in the publish script STDLIB gate, missing `SIGTERM/SIGINT` handling in `publish.sh`, and a stale `<Folder Include="bin\Debug\net9.0\">` in the csproj. Three informational items cover minor quality issues.

## Critical Issues

### CR-01: Doc-comment buffer leaks across proc boundary when a non-trivial statement intervenes

**File:** `flow-lang/Parsing/Parser.cs:129-133`

**Issue:** The "charitable orphan-drop" code path in `ParseStatement` clears `_pendingDocComment` only if the upcoming token is neither `TokenType.Proc` nor `TokenType.Internal`. However the check fires at the TOP of `ParseStatement` for every non-proc/non-internal token, including the ones inside proc bodies. If a proc body contains statements after a `///` doc-comment — for example, a variable declaration inside a proc — `_pendingDocComment` is cleared by the orphan-drop guard (correct behaviour). But consider this pattern:

```flow
/// doc for foo
Int x = 5     // <-- ParseStatement called; x is not Proc/Internal → orphan drop fires → _pendingDocComment = null ✓
proc foo()    // receives null — CORRECT
end
```

vs.

```flow
proc outer()
  /// doc for inner
  // (ordinary comment — ParseStatement returns null, does NOT clear _pendingDocComment)
  proc inner()   // <-- INNER proc is nested; _pendingDocComment still set from outer-scope ///
  end
end
```

In the nested case the ordinary-comment arm (`if (Match(TokenType.Comment)) return null;`) on line 108 returns early WITHOUT triggering the orphan-drop guard, so the pending doc-comment buffer from an inner `///` persists past a plain `//` comment line and gets consumed by the NEXT proc in declaration order, not the proc it precedes. More concretely: the orphan-drop clears only on non-proc tokens it sees before reaching `ParseProcDeclaration`. A plain `// comment` between `///` and `proc` returns `null` from `ParseStatement` at line 109 without touching `_pendingDocComment`, so the buffer is not cleared. This allows a `///` block that is NOT adjacent to a proc to bind to a subsequent proc separated by one or more plain comments, producing a wrong or misleading doc-comment binding.

Additionally, when `ParseStatement` is called recursively inside a proc body (which is the normal control flow for nested procs in Flow), the inner `ParseStatement` invocation can see `_pendingDocComment` left over from the OUTER scope's `///` buffer.

**Fix:** Clear `_pendingDocComment` in the plain-comment arm before returning null, so any pending buffer accumulated before a `// comment` line is always dropped:

```csharp
// Skip comments — clear any pending doc-comment so it cannot cross a plain
// comment line and bind to a later proc. Charitable: doc-comments that are
// separated from their proc by intervening ordinary comments are dropped.
if (Match(TokenType.Comment))
{
    _pendingDocComment = null;   // ADD THIS LINE
    return null;
}
```

---

### CR-02: `WasapiBackend.Play` — reference to `_provider`/`_out` captured before `EnsureInitialized` writes them, then used unsafely without null re-check

**File:** `flow-lang/Audio/WasapiBackend.cs:133-200`

**Issue:** `Play` calls `EnsureInitialized(sampleRate, channels)` on line 138, then takes the `_lock` to capture local references to `provider` and `output` on lines 145-150. This pattern is correct for the common re-entrant case. However, `EnsureInitialized` itself acquires `_lock` on line 124 to check `IsInitialized`. If a concurrent `Dispose()` call (which also acquires `_lock`) runs between the moment `EnsureInitialized` releases the lock after confirming `IsInitialized == true` and the moment `Play` re-acquires the lock to capture `provider`/`output`, the `CloseOutput()` inside `Dispose` will null both fields. The subsequent `if (_provider == null || _out == null) return;` guard on lines 147-148 handles this safely.

However the real race is in the busy-poll drain loop on lines 187-194. After the `lock(_lock)` block on lines 145-151 captures `provider` and `output`, both references are held without a lock while `Thread.Sleep(10)` executes. `Dispose()` (called from another thread) calls `CloseOutput()` which calls `_out.Stop()` and then `_out.Dispose()`. The local `output` reference now points to a disposed `WasapiOut` object. Reading `output.PlaybackState` on line 187 on a disposed `WasapiOut` is documented by NAudio to throw `ObjectDisposedException`. The catch-free loop will propagate that exception as an unhandled crash.

**Fix:** Wrap the drain-wait loop body in a try/catch for `ObjectDisposedException`/`InvalidOperationException`:

```csharp
while (provider.BufferedBytes > 0 && output.PlaybackState == PlaybackState.Playing)
{
    if (cancellationToken.IsCancellationRequested)
    {
        Stop();
        return;
    }
    try { Thread.Sleep(10); }
    catch (ObjectDisposedException) { return; }
    catch (InvalidOperationException) { return; }
    // Re-check PlaybackState inside a try for the race:
    try { if (output.PlaybackState != PlaybackState.Playing) break; }
    catch (ObjectDisposedException) { return; }
}
```

Alternatively, restructure to avoid holding the naked reference past the lock boundary by catching the exception at the loop level.

---

### CR-03: `DocExampleRunner.RunOne` — `Console.SetOut/SetError` restore is NOT guaranteed on timeout path; can permanently silence host CLI process

**File:** `flow-cli/Doc/DocExampleRunner.cs:85-131`

**Issue:** The `finally` block on lines 111-114 that restores `Console.Out` / `Console.Error` is inside the worker thread lambda. When `worker.Join(_timeoutMs)` times out (line 121), the method returns immediately with a timeout annotation. The worker thread continues running in the background (it is `IsBackground = true` and the code correctly acknowledges that `Thread.Abort` is gone). The worker's `finally` block WILL eventually run when the worker completes — but only on the background thread. Between the timeout return and the background-thread completion, the host process has `Console.Out = TextWriter.Null` if the worker has already called `Console.SetOut(TextWriter.Null)` before the timeout but has not yet reached the `finally` restorer.

This means that in the typical timeout scenario — a long-running `.flow` program that spins for `_timeoutMs` seconds — the host process's `Console.Out` is `TextWriter.Null` for the duration of that background thread's remaining life. All subsequent `Console.WriteLine(...)` calls in `DocCommand` (e.g., printing the entry count, file paths, cache stats) are silently dropped. This is a silent correctness failure that makes it appear to the user that the `flow doc` command did nothing.

Additionally, if a second call to `RunOne` starts a new worker that also calls `Console.SetOut(TextWriter.Null)`, and both background threads eventually hit their `finally` restores, the second thread's restore may overwrite the first thread's saved `savedOut` — whichever restores last wins. This is a race on the global `Console.Out` state.

**Fix:** Do the redirect/restore in the CALLING thread's context, not inside the worker. Only pass the `FlowEngine` execution to the worker. The simplest correct structure:

```csharp
// In the CALLER (RunOne), before starting the worker:
var savedOut = Console.Out;
var savedErr = Console.Error;
Console.SetOut(TextWriter.Null);
Console.SetError(TextWriter.Null);
try
{
    worker.Start();
    if (!worker.Join(_timeoutMs))
        return $"[example failed] timed out after {_timeoutMs} ms";
    // ...
}
finally
{
    Console.SetOut(savedOut);
    Console.SetError(savedErr);
}
```

This guarantees restore on both the normal path and the timeout path, and eliminates the race between multiple concurrent `RunOne` calls.

## Warnings

### WR-01: `WasapiBackend.Play` busy-polls at 10 ms granularity on audio thread — risks audible glitch on buffer near-full

**File:** `flow-lang/Audio/WasapiBackend.cs:171-179`

**Issue:** The feed loop (lines 162-184) calls `Thread.Sleep(10)` while waiting for room in the `BufferedWaveProvider`. The provider's `WaveFormat.AverageBytesPerSecond` chunk size is approximately one second of audio. With a 5-second `BufferDuration` and 1-second chunks, the first feed fills the buffer in ~5 sleeps. The `PlayThread` inside `WasapiOut` drains at realtime rate. If `Thread.Sleep(10)` oversleeps (common under Windows scheduler jitter), the buffer can empty before the next chunk is added, causing an audible dropout (the WAV provider returns silence frames while the buffer is empty). The chosen `DefaultLatencyMs = 100` and a 1-second `chunkBytes` are incompatible: at 44100 Hz stereo float, 1 second is ~352 KB but the WASAPI latency buffer is only 100 ms. Any sleep overshoot that lets `BufferedBytes` drop to zero creates an underrun.

**Fix:** Reduce `chunkBytes` to match the WASAPI latency hint (e.g., `provider.WaveFormat.AverageBytesPerSecond / 10` for ~100 ms chunks) and reduce the sleep duration to 5 ms so the feed loop tracks the drain rate more closely:

```csharp
int chunkBytes = provider.WaveFormat.AverageBytesPerSecond / 10; // ~100 ms per feed
// ... in the wait loop:
Thread.Sleep(5);
```

---

### WR-02: `ContentHashCache.Save` writes the JSON file non-atomically — corrupt/partial cache on process kill

**File:** `flow-cli/Doc/ContentHashCache.cs:121-131`

**Issue:** `File.WriteAllText(Path.Combine(outDir, CacheFileName), json)` on line 130 overwrites the existing `.flowdoc-cache.json` in-place. If the process is killed (Ctrl-C, OOM, power loss) mid-write, the cache file will be partially written. On the next `flow doc` run, `JsonSerializer.Deserialize` will throw on the truncated JSON, `Load` catches and returns an empty cache, and a full regen occurs. This is the recoverable (though inconvenient) path. More problematic: a partially-written cache whose JSON happens to parse (truncated at an entry boundary) could silently serve stale hashes for the entries that DID survive the truncation, causing `IsUnchanged` to return `true` for entries whose underlying content was updated between the truncated save and the next load. Stale "unchanged" judgments mean updated `///` text does NOT regenerate the reference docs.

**Fix:** Use atomic write-via-tempfile:

```csharp
var targetPath = Path.Combine(outDir, CacheFileName);
var tmpPath = targetPath + ".tmp";
File.WriteAllText(tmpPath, json);
File.Move(tmpPath, targetPath, overwrite: true);
```

`File.Move` with `overwrite: true` is atomic on POSIX (rename syscall) and near-atomic on NTFS. This eliminates the partial-write window.

---

### WR-03: `MarkdownEmitter` does not escape Markdown special characters in `m.Name` and `m.Signature` — potential doc injection

**File:** `flow-cli/Doc/MarkdownEmitter.cs:55-62`

**Issue:** The entry header `sb.Append("### ").Append(m.Name)` (line 55) and the signature block `` sb.Append("```flow\n").Append(m.Signature) `` (line 61) insert `m.Name` and `m.Signature` directly into the Markdown output without escaping. While `m.Name` is a function name and unlikely to contain problematic characters, `m.Signature` is synthesized from proc parameter types and names harvested from arbitrary user `.flow` files via `DocCollector`. A proc with a parameter named `` `foo` `` or containing a `#` or `[` or `]` could produce malformed Markdown that breaks rendering or injects unexpected formatting. For the HTML emitter this is not an issue because `WebUtility.HtmlEncode` is applied consistently (line 119 of `HtmlEmitter.cs`). The Markdown emitter has no equivalent guard.

The `EscCell` helper on line 86 escapes `|` and `\n` for table cells, but `EmitEntry` does not apply `EscCell` or any equivalent to the section heading or the fenced-code-block body.

**Fix:** For the heading, apply a minimal Markdown escape over `m.Name` (at minimum escape `\`, `#`, `[`, `]`, backtick, `*`, `_`). The signature and example blocks are already inside fenced `` ``` `` regions, so injection via the body content is limited to `` ``` `` sequences. Add a guard:

```csharp
// Guard: a ``` in a signature or example body would break the fenced block.
private static string EscFenced(string s) =>
    s.Replace("```", "\\`\\`\\`");
```

Apply `EscFenced` to `m.Signature` and each `m.Examples[i]` before appending into `` ```flow `` blocks.

---

### WR-04: `scripts/publish.sh` STDLIB_FILES list is missing `patterns.flow` and `generative.flow` — publish verification gap

**File:** `scripts/publish.sh:57`

**Issue:** The `STDLIB_FILES` array used to verify that stdlib `.flow` files landed in the publish output directory is:

```bash
STDLIB_FILES=(std.flow collections.flow audio.flow bars.flow notation.flow composition.flow)
```

This list omits `patterns.flow`, `generative.flow`, and `improv.flow`, all of which have `CopyToPublishDirectory=PreserveNewest` in `flow-lang.csproj` and are used by the Phase 36 `@patterns`/`@generative`/`@improv` opt-in modules. A publish that accidentally loses these files (e.g., a csproj regression) would pass the script's stdlib check but produce a broken binary where `use "@patterns"` fails at runtime. The verify loop on lines 110-116 would not catch this omission.

**Fix:** Add the missing files to `STDLIB_FILES`:

```bash
STDLIB_FILES=(std.flow collections.flow audio.flow bars.flow notation.flow composition.flow \
              patterns.flow generative.flow improv.flow)
```

---

### WR-05: `scripts/publish.sh` has no SIGINT/SIGTERM trap — published artifacts may be left in partially-written state

**File:** `scripts/publish.sh:45-207`

**Issue:** The script uses `set -euo pipefail` (line 45), which is correct and will exit on most command failures. However there is no `trap` for `SIGINT` (Ctrl-C) or `SIGTERM`. If the user interrupts the script while `dotnet publish` or the `tar`/`zip` packaging step is running, the partially-written archive file remains in `$PUBLISH_ROOT`. On a subsequent interrupted run a different RID's archive could be present alongside a valid one. The `sha256sum` sidecar for the partially-written archive does not get produced (the script exits before reaching that step), so the file appears unvalidated. This can confuse human reviewers comparing artifact sets for the release.

**Fix:** Add a cleanup trap at the top of the script that removes the in-progress archive on interrupt:

```bash
_CURRENT_ARCHIVE=""
trap 'if [[ -n "$_CURRENT_ARCHIVE" ]]; then rm -f "$_CURRENT_ARCHIVE"; fi; exit 130' INT TERM
```

Set `_CURRENT_ARCHIVE="$ARCHIVE"` just before the `tar`/`zip` call and clear it afterward.

---

### WR-06: Stale `<Folder Include="bin\Debug\net9.0\">` in `flow-lang.csproj` references a .NET 9 artifact path in a .NET 10 project

**File:** `flow-lang/flow-lang.csproj:403`

**Issue:** The item group at line 403 contains:

```xml
<Folder Include="bin\Debug\net9.0\" />
```

The project targets `net10.0` (line 4). This `<Folder>` item is a residual artifact from a previous SDK version and refers to a directory that will never exist under a `net10.0` build. While this does not cause a build failure (MSBuild ignores missing `<Folder>` items), it is misleading: it implies a `net9.0` build output exists, which it does not. Any tooling that reads the project file to enumerate output directories (e.g., IDE indexers, publish scripts that enumerate build outputs) may attempt to reference the nonexistent directory.

**Fix:** Remove the stale folder item:

```xml
<!-- Remove this line: -->
<Folder Include="bin\Debug\net9.0\" />
```

## Info

### IN-01: `DocExampleRunner` timeout worker cannot be cancelled — background thread runs to completion or process exit

**File:** `flow-cli/Doc/DocExampleRunner.cs:121-127`

**Issue:** When `worker.Join(_timeoutMs)` times out, the worker continues executing on a background thread for the full remaining duration of the `.flow` program's execution. The comment on line 123 acknowledges this ("we cannot preempt the worker — Thread.Abort is gone in modern .NET"). This is a known platform limitation, but the implication is that for a corpus of N examples each with a long-running bug, `RunAll` will have N background threads running concurrently (one per timed-out example), potentially consuming substantial CPU and memory. A future mitigation would be to pass a `CancellationToken` to `FlowEngine.Execute` so a cooperative stop is possible.

---

### IN-02: `DocCollector.ParseDocComment` treats prose after a code fence as summary if no examples precede it

**File:** `flow-cli/Doc/DocCollector.cs:208-213`

**Issue:** The summary-accumulation condition at line 209 is `else if (examples.Count == 0)`. This means that any non-fence line that appears after ALL examples have been parsed is silently discarded (neither summary nor example). A doc-comment structure like:

```
/// Summary line
///
/// ```
/// example code
/// ```
///
/// Notes about the example (prose after the last fence)
```

The "Notes about the example" lines are dropped. This is a minor quality issue — composers who write post-fence prose will silently lose it — but it is at least consistently silent (no error). Worth documenting as a known limitation in the `ParseDocComment` summary.

---

### IN-03: `HtmlEmitter.Anchor` collapses all special characters to hyphens, producing non-unique anchors for categories/entries sharing the same letters

**File:** `flow-cli/Doc/HtmlEmitter.cs:121-130`

**Issue:** The `Anchor` helper maps any non-alphanumeric character to `-` and then trims leading/trailing hyphens. For the TOC category anchors this is benign (CLAUDE.md category names are well-separated). However for the per-entry anchors (`id="fn-<anchor>"`), two built-in functions whose names produce the same anchor after the transform would silently collide. For example, if any two entries produce the same lower-case alphanumeric string, both get the same `id` attribute — browsers pick the FIRST match for anchor navigation, and the second entry is unreachable via the TOC. Current function names do not trigger this (they are already alphanumeric), but if a proc name contains non-ASCII or special characters the anchor collision risk increases.

---

_Reviewed: 2026-06-07_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
