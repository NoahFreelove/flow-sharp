---
phase: 42-type-system-stdlib-audit
reviewed: 2026-05-24T00:00:00Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - scripts/StdlibAuditor/StdlibAuditor.csproj
  - scripts/StdlibAuditor/Program.cs
  - flow-lang.Tests/Integration/Phase42/AuditHarnessTests.cs
  - scripts/audit/clamp-grep.sh
  - scripts/audit/flow-callers.sh
  - flow-lang.Tests/Integration/Phase42/ClampGrepConsistencyTests.cs
  - flow-lang.Tests/Integration/Phase42/AuditReportShapeTests.cs
findings:
  critical: 0
  warning: 4
  info: 3
  total: 7
status: issues_found
---

# Phase 42: Code Review Report

**Reviewed:** 2026-05-24T00:00:00Z
**Depth:** standard
**Files Reviewed:** 7
**Status:** issues_found

## Summary

Phase 42 delivers a read-only audit harness: a reflective .NET console tool (`scripts/StdlibAuditor/`), two bash grep extractors (`scripts/audit/`), and three xUnit fixtures (`flow-lang.Tests/Integration/Phase42/`). The zero-production-code invariant is respected across all seven files. No secrets, no injection surfaces, no auth paths.

Four findings degrade reliability or create silent correctness gaps; three are informational quality items.

The most actionable finding is a classic Process deadlock pattern in `ClampGrepConsistencyTests.RunBashScript` (WR-01): sequential `stdout.ReadToEnd()` then `stderr.ReadToEnd()` can deadlock if a script writes enough to stderr. In practice these scripts write minimal stderr so it will not trigger today, but it is a latent correctness defect. The remaining warnings are: stale class-level XML doc in `Program.cs` that describes the wrong registry approach (WR-02), dead duplicate constant fields in the `Program` class that diverge silently from `AuditExtractor` (WR-03), and an incomplete core-file assertion in `InventoryFiles_LandInPhase42DataDir` that does not verify `flow-call-sites.txt` (WR-04).

## Warnings

### WR-01: `RunBashScript` Has Latent Deadlock on Large Stderr Output

**File:** `flow-lang.Tests/Integration/Phase42/ClampGrepConsistencyTests.cs:305-307`

**Issue:** `RunBashScript` reads stdout to completion before reading stderr:

```csharp
string stdout = proc!.StandardOutput.ReadToEnd();   // blocks until EOF
string stderr = proc.StandardError.ReadToEnd();      // only starts here
proc.WaitForExit();
```

If the child process fills the OS stderr pipe buffer (~64 KB on Linux) before exiting — for example if an audit script falls into an error path that emits many lines to stderr — the child blocks waiting for the parent to drain stderr, while the parent is blocked inside `stdout.ReadToEnd()`. This is a documented `Process` deadlock pattern in the .NET docs. The current scripts emit only short stderr messages so it will not trigger today, but any future script that errors verbosely will hang the test forever.

**Fix:** Read stdout and stderr concurrently:

```csharp
using var proc = Process.Start(psi);
Assert.NotNull(proc);
var stdoutTask = proc!.StandardOutput.ReadToEndAsync();
var stderrTask = proc.StandardError.ReadToEndAsync();
proc.WaitForExit();
string stdout = await stdoutTask;
string stderr = await stderrTask;
```

Because `RunBashScript` is synchronous, the simplest safe alternative without making the method `async` is:

```csharp
var stdoutTask = Task.Run(() => proc!.StandardOutput.ReadToEnd());
string stderr = proc.StandardError.ReadToEnd();
proc.WaitForExit();
string stdout = stdoutTask.Result;
```

---

### WR-02: `Program` Class-Level XML Doc Describes the Wrong Registry Approach

**File:** `scripts/StdlibAuditor/Program.cs:13-18`

**Issue:** The `<summary>` block for the `Program` class states:

> "Reflects over FlowType + every signature registered via `BuiltInFunctions.RegisterSignaturesOnly` (NOT `RegisterAllImplementations`…)"

This is the inverse of what the code actually does. `AuditExtractor.Build()` (called by `Main`) constructs a `FlowEngine` — explicitly because `RegisterSignaturesOnly` misses the context-bound paths (SfzBuiltins, NotationIoBuiltins, OscFunctions, etc.). The `AuditExtractor` class doc (lines 232-252) correctly describes the `FlowEngine` approach, but the `Program` class doc is never corrected and directly contradicts it.

Any future maintainer reading the tool summary will be told to use `RegisterSignaturesOnly`, which would drop ~93 signatures from the graph (320 vs. 413) and silently corrupt the audit output.

**Fix:** Replace the two stale sentences (lines 13-18) with:

```csharp
/// Reflects over <see cref="FlowType"/> + every signature registered via a
/// short-lived <see cref="FlowEngine"/> construction — which is the only path
/// that covers context-bound registrations (SfzBuiltins, NotationIoBuiltins,
/// OscFunctions, MarkovFunctions, etc.) that neither
/// <see cref="BuiltInFunctions.RegisterAllImplementations"/> nor
/// <see cref="BuiltInFunctions.RegisterSignaturesOnly"/> alone covers.
```

---

### WR-03: Dead Duplicate Constant Fields in `Program` Class Create Silent Drift Risk

**File:** `scripts/StdlibAuditor/Program.cs:42-61`

**Issue:** The `Program` class declares `ReferenceIdentityTypeNames` (lines 42-49) and `MusicTypeCompanions` (lines 53-61) as `private static readonly` fields. These are never referenced anywhere in `Program`'s code — all logic runs through `AuditExtractor.Build()`, which has its own copies of the same constants (lines 213-231). The `Program` class copies are pure dead code.

The danger is maintenance drift: if a new reference-identity type is added (e.g., an `OscHandleType` successor), a developer updating the constants may update only one of the two copies, leaving the other silently stale. Because `Program`'s copies are never used, the divergence will never produce a compile error or test failure.

**Fix:** Remove the dead constants from the `Program` class entirely (lines 42-61). If they are ever needed in `Main` directly, reference `AuditExtractor`'s constants instead (make them `internal` rather than `private`).

---

### WR-04: `InventoryFiles_LandInPhase42DataDir` Does Not Assert `flow-call-sites.txt`

**File:** `flow-lang.Tests/Integration/Phase42/ClampGrepConsistencyTests.cs:234-248`

**Issue:** The `core` array in `InventoryFiles_LandInPhase42DataDir` lists six expected files:

```csharp
string[] core =
{
    "input-clamps.txt",
    "all-clamps.txt",
    "advisory-sites.txt",
    "charitable-sites.txt",
    "summary.txt",
    "flow-proc-decls.txt",   // from flow-callers.sh
    // flow-call-sites.txt    <-- missing
};
```

`flow-callers.sh` produces both `flow-proc-decls.txt` and `flow-call-sites.txt`. Only the first is asserted in the core list. A regression that silently stops producing `flow-call-sites.txt` (e.g., a `sort | uniq -c | sort -rn` pipeline failure) would pass this test. Since `FlowCallers_DeclaresKnownStdlibProcs` only validates `flow-proc-decls.txt`, the call-site frequency table has no regression pin at all.

**Fix:** Add `"flow-call-sites.txt"` to the `core` array. Optionally add a companion fact asserting `flow-call-sites.txt` has at least one line (parallel to `AllClamps_CountWithinTolerance`).

---

## Info

### IN-01: `WriteAtomic` Has No `try/finally` to Clean Up `.tmp` File on Exception

**File:** `scripts/StdlibAuditor/Program.cs:145-155`

**Issue:** `WriteAtomic` writes to `finalPath + ".tmp"` and then moves it. If `File.WriteAllText` succeeds but `File.Move` throws (e.g., cross-device move, permissions issue on the target), the `.tmp` file is left on disk. Subsequent runs will also leave `.tmp` files; they accumulate silently. Since the audit always writes to the same paths, this is a low-severity litter issue rather than a data-loss issue.

**Fix:**

```csharp
private static void WriteAtomic(string finalPath, string contents)
{
    var dir = Path.GetDirectoryName(Path.GetFullPath(finalPath));
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);

    string tempPath = finalPath + ".tmp";
    try
    {
        File.WriteAllText(tempPath, contents);
        File.Move(tempPath, finalPath, overwrite: true);
    }
    catch
    {
        try { File.Delete(tempPath); } catch { /* best-effort */ }
        throw;
    }
}
```

---

### IN-02: Asymmetric-Pair Count Is 2x the Number of Unique Asymmetric Pairs

**File:** `scripts/StdlibAuditor/Program.cs:126`, `scripts/StdlibAuditor/Program.cs:358-381`

**Issue:** The asymmetry loop iterates all ordered pairs `(i, j)` with `i != j`. For any truly asymmetric type pair `(A, B)`, both `(i=A, j=B)` and `(i=B, j=A)` satisfy the condition and are added to the list. The console summary at line 126 reports `graph.Asymmetries.Count` as "asymmetric pairs," but this count is double the number of unique unordered pairs. The same double-counting appears in `AuditHarnessTests.cs` (lines 151-165).

The JSON output is also redundant: each logical pair appears as two entries with swapped `a`/`b` fields. Downstream AUDIT.md authors reading the JSON may interpret the inflated count as more findings than actually exist.

**Fix (option A — deduplicate):** Add `i < j` guard to the outer loop so each pair is visited once:

```csharp
for (int i = 0; i < discovered.Count; i++)
for (int j = i + 1; j < discovered.Count; j++)
{
    // ... check both directions, emit one AsymmetryEntry per pair
}
```

**Fix (option B — clarify):** Keep the current double-iteration but rename the field to `asymmetric_directed_edges` in the JSON and update the console summary to say "directed asymmetric edges (N/2 unique pairs)" so readers are not misled.

---

### IN-03: `flow-callers.sh` Call-Site Grep Silently Misses Zero-Argument S-Expression Calls

**File:** `scripts/audit/flow-callers.sh:131-136`

**Issue:** The call-site frequency grep uses the pattern `[a-zA-Z_][a-zA-Z0-9_]*[ (]` — an identifier followed by a space or `(`. In Flow's prefix S-expression syntax, a zero-argument call is written `(name)`. In this form the identifier is followed by `)`, which does not match `[ (]`. As a result, any builtin called exclusively as `(name)` (e.g. `(play)`, `(stop)`, `(print)`) contributes zero entries to `flow-call-sites.txt`.

Verified:
```bash
$ printf "(play)" | grep -ho "[a-zA-Z_][a-zA-Z0-9_]*[ (]"
# no output, exit 1
$ printf "(play arg)" | grep -ho "[a-zA-Z_][a-zA-Z0-9_]*[ (]"
play
```

A builtin called only in zero-arg form would appear to have no `.flow` callers and be falsely elevated as a dead-end candidate in AUDIT.md §4. The RESEARCH §Pattern 4 dead-end check (`grep -c "^NAME$" flow-proc-decls.txt`) partially mitigates this for builtins that are also `.flow` proc-declared, but builtins only invoked from Flow scripts (not declared as procs) would be missed.

**Fix:** Extend the pattern to also match identifier-before-`)`:

```bash
grep -rho "[a-zA-Z_][a-zA-Z0-9_]*[( )]" "${FLOW_FILES[@]}" 2>/dev/null \
    | sed 's/[( )]$//' \
    | sort | uniq -c | sort -rn \
    > "$OUT_DIR/flow-call-sites.txt"
```

Or alternatively, add a second pass that captures `(name)` patterns specifically:

```bash
grep -rho "([a-zA-Z_][a-zA-Z0-9_]*)" "${FLOW_FILES[@]}" 2>/dev/null \
    | sed 's/[()]//g'
```

---

_Reviewed: 2026-05-24T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
