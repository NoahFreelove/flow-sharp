using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 §2.5 (approved decision D5) — user-proc / lambda call frames
/// are now CALL BOUNDARIES with LEXICAL variable scope. A proc body sees its own
/// params + locals + injected closure captures + GLOBALS — but NOT the caller's
/// locals.
///
/// <para>
/// Before this fix, every proc-call frame parented to <c>CurrentFrame</c> and
/// <see cref="FlowLang.Runtime.StackFrame"/>'s variable accessors walked the whole
/// parent chain, so a proc body could READ the caller's locals (behavior depended
/// on who called it) and an assignment to a name not declared in the proc (e.g. a
/// typo'd parameter) silently walked up and MUTATED the caller's same-named local.
/// </para>
///
/// <para>
/// The boundary is set ONLY at <c>Interpreter.ExecuteUserFunctionWithCaptures</c>'s
/// <c>PushFrame(isCallBoundary: true)</c> site. Block frames (musical-context /
/// loop / section-call / pattern-match / live) keep their lexical parent-walk, and
/// musical-context dynamic scope (a separate <c>_callStack</c> walk) is unaffected.
/// </para>
/// </summary>
[Collection("FlowScripts")]   // serialize Console.SetOut redirection (RESEARCH Pitfall 4)
public class ProcScopeBoundaryTests
{
    // --- WRITE-THROUGH BLOCKING (the core defect) ---

    /// <summary>
    /// The verifier's distinguishing repro: proc g declares a local Int x = 1 and
    /// calls f; f's bare `x = 5` assignment must NOT mutate g's local. Before the
    /// fix the parent-walk reached g's frame and overwrote it (print → 5). After
    /// the fix the write cannot cross the call boundary, x stays 1, and f's
    /// assignment to an undeclared name surfaces the composer-facing
    /// undeclared-variable diagnostic.
    /// </summary>
    [Fact]
    public void NestedWriteThrough_DoesNotMutateCallerLocal()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
proc f ()
    x = 5
end proc

proc g ()
    Int x = 1
    (f)
    (print (str x))
end proc

(g)
");
        // g's local x is untouched by f's write-through attempt.
        Assert.Contains("1", stdout);
        Assert.DoesNotContain("5", stdout);
        // f's `x = 5` targets a name that is neither local to f nor global →
        // the existing undeclared-variable diagnostic fires.
        Assert.True(errorCount >= 1, "Assignment to undeclared 'x' in f must report an error.");
        Assert.Contains("Variable 'x' not found", stderr);
    }

    // --- GLOBALS STAY WRITABLE FROM A PROC (scripts rely on this) ---

    /// <summary>
    /// A top-level (global) binding stays writable from inside a proc — the
    /// boundary walk redirects to the global frame, so global mutation is
    /// preserved. This is the one case the simple "parent call frames to global"
    /// alternative would also handle, kept here to lock the contract.
    /// </summary>
    [Fact]
    public void GlobalBinding_RemainsWritableFromProc()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
Int counter = 0

proc bump ()
    counter = 10
end proc

(bump)
(print (str counter))
");
        Assert.Equal(0, errorCount);
        Assert.Contains("10", stdout);
    }

    // --- PARAMS / LOCALS SHADOW GLOBALS CORRECTLY ---

    /// <summary>
    /// A parameter named the same as a global shadows it inside the body, and the
    /// global is unchanged after the call. Writing to the param must not leak to
    /// the global (the param is local to the boundary frame).
    /// </summary>
    [Fact]
    public void ProcParam_ShadowsGlobal_WithoutLeaking()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
Int x = 100

proc f (Int: x)
    (print (str x))
end proc

(f 7)
(print (str x))
");
        Assert.Equal(0, errorCount);
        // Body sees the param (7); top-level global is still 100 after the call.
        var lines = stdout.Replace("\r", "").Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("7", lines);
        Assert.Contains("100", lines);
    }

    /// <summary>
    /// A proc-local declaration shadows a global; the local is dropped when the
    /// frame pops, so the global is unchanged after the call.
    /// </summary>
    [Fact]
    public void ProcLocal_ShadowsGlobal_WithoutLeaking()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
Int x = 100

proc f ()
    Int x = 7
    (print (str x))
end proc

(f)
(print (str x))
");
        Assert.Equal(0, errorCount);
        var lines = stdout.Replace("\r", "").Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("7", lines);
        Assert.Contains("100", lines);
    }

    // --- READ BLOCKING (shipped: full gate green) ---

    /// <summary>
    /// READ-blocking: f cannot read g's local. Before the fix, `(print (str y))`
    /// inside f resolved y from g's caller frame and printed 42. After the fix the
    /// boundary blocks the read; y is undeclared in f's lexical scope (f's locals +
    /// globals), so it is not found.
    /// </summary>
    [Fact]
    public void ReadBlocking_ProcCannotReadCallerLocal()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, _) = runner.RunSource(@"
use ""@std""
proc f ()
    (print (str y))
end proc

proc g ()
    Int y = 42
    (f)
end proc

(g)
");
        // The caller's local y is NOT visible inside f — it must not print 42.
        Assert.DoesNotContain("42", stdout);
    }

    /// <summary>
    /// READ-blocking: a proc CAN still read a global it does not shadow. This pins
    /// that the boundary redirect reaches the global frame for reads, not just
    /// writes.
    /// </summary>
    [Fact]
    public void ReadBlocking_ProcCanReadGlobal()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, _, errorCount) = runner.RunSource(@"
use ""@std""
Int g = 99

proc f ()
    (print (str g))
end proc

(f)
");
        Assert.Equal(0, errorCount);
        Assert.Contains("99", stdout);
    }
}
