using System;
using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using Xunit;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-02 Task 2 — Facts pinning the D-05 call-boundary snapshot
/// of <c>ExecutionContext.CallerStrictMode</c> at builtin and user-proc
/// invocation sites in <c>ExpressionEvaluator.EvaluateFunctionCall</c>, plus
/// the Interpreter push/pop of <c>StrictMode = proc.IsStrict</c> at
/// <c>ExecuteUserFunctionWithCaptures</c> entry/exit.
///
/// <para>
/// Observation strategy mirrors <see cref="ModuleLoaderStrictPropagationTests"/>:
/// a test-only <c>__strictProbe</c> builtin is registered via
/// <c>engine.Context.InternalRegistry.Register</c>; its body captures
/// <c>engine.Context.CallerStrictMode</c> at invocation time into a
/// closed-over <c>bool?</c>. After Execute, the test reads the capture
/// to verify the D-05 snapshot semantics.
/// </para>
///
/// <para>
/// Why this contract matters (D-03 + Anti-Pattern 2): stdlib leaf sites
/// must read <c>CallerStrictMode</c> (the immediate caller's bit), NOT
/// <c>StrictMode</c> (the declaring-file bit of the stdlib module itself,
/// which is always false because stdlib modules ship without
/// <c>enable strict;</c>). The snapshot lets non-strict stdlib bodies
/// internally use charitable Clamp / WarnOnce paths while still raising
/// <c>[strict]</c> errors when their caller is strict.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class CallerStrictModeSnapshotTests : IDisposable
{
    private readonly string _tempDir;

    public CallerStrictModeSnapshotTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "flow-44-02-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    /// <summary>
    /// Build a sandbox engine and register a parameterless test-only
    /// <c>__strictProbe</c> builtin that snapshots <c>engine.Context.CallerStrictMode</c>
    /// every time it's invoked into a closed-over field accessible via
    /// <paramref name="getLastCapture"/>. Returns the engine.
    /// </summary>
    private static FlowEngine NewEngineWithProbe(out Func<bool?> getLastCapture)
    {
        bool? captured = null;
        var engine = new FlowEngine();
        var sig = new FunctionSignature(
            Name: "__strictProbe",
            InputTypes: Array.Empty<FlowType>(),
            IsVarArgs: false);
        engine.Context.InternalRegistry.Register(
            "__strictProbe",
            sig,
            _ =>
            {
                captured = engine.Context.CallerStrictMode;
                return Value.Void();
            });
        getLastCapture = () => captured;
        return engine;
    }

    [Fact]
    public void Fact_StrictFileCallingBuiltin_LeafSeesCallerStrictTrue()
    {
        var engine = NewEngineWithProbe(out var get);
        try
        {
            // Pattern from 44-01: declare the test-only proc at language level
            // FIRST so the C#-registered impl is reachable via the bare name.
            var src =
                "enable strict;\n"
                + "internal proc __strictProbe ()\n"
                + "(__strictProbe)\n";
            var ok = engine.Execute(src, "<top>");
            Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");
            Assert.True(get().HasValue, "__strictProbe must have been invoked.");
            Assert.True(get()!.Value,
                "leaf builtin invoked from a strict file must see CallerStrictMode=true (D-05).");
        }
        finally { engine.Dispose(); }
    }

    [Fact]
    public void Fact_NonStrictFileCallingBuiltin_LeafSeesCallerStrictFalse()
    {
        var engine = NewEngineWithProbe(out var get);
        try
        {
            var src =
                "internal proc __strictProbe ()\n"
                + "(__strictProbe)\n";
            var ok = engine.Execute(src, "<top>");
            Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");
            Assert.True(get().HasValue, "__strictProbe must have been invoked.");
            Assert.False(get()!.Value,
                "leaf builtin invoked from a non-strict file must see CallerStrictMode=false (D-05).");
        }
        finally { engine.Dispose(); }
    }

    [Fact]
    public void Fact_StrictCallsNonStrictModuleThatCallsBuiltin_LeafSeesCallerStrictFalse()
    {
        // D-03 + Anti-Pattern 2: a strict outer calls a non-strict middle proc
        // (declared in an inner non-strict module). When middle's body invokes
        // the probe, CallerStrictMode reflects middle's StrictMode (false),
        // NOT the outer-outer strict file's bit. This pins the "stdlib stays
        // charitable internally" invariant.
        var innerPath = Path.Combine(_tempDir, "inner_nonstrict.flow");
        File.WriteAllText(innerPath,
            "proc middle ()\n"
            + "    (__strictProbe)\n"
            + "end proc\n");

        var engine = NewEngineWithProbe(out var get);
        try
        {
            var innerPosix = innerPath.Replace('\\', '/');
            var outerSrc =
                "enable strict;\n"
                + "internal proc __strictProbe ()\n"
                + "use \"" + innerPosix + "\"\n"
                + "(middle)\n";
            var ok = engine.Execute(outerSrc, "<top>");
            Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");
            Assert.True(get().HasValue, "__strictProbe must have been invoked from inside middle.");
            Assert.False(get()!.Value,
                "stdlib-style non-strict proc invoking a leaf builtin must see "
                + "CallerStrictMode=false even when its outer-outer caller is strict (D-03).");

            // Outer's file-scope bit is still true after the import + call return.
            Assert.True(engine.Context.StrictMode,
                "outer strict file's StrictMode must be RESTORED to true after the import + call return.");
        }
        finally { engine.Dispose(); }
    }

    [Fact]
    public void Fact_NestedCalls_StackDisciplined()
    {
        // Outer strict file calls non-strict middle proc which calls strict leaf
        // proc which calls the probe. The probe must see CallerStrictMode
        // reflecting the IMMEDIATE caller (leaf proc, strict), NOT the
        // outer-outer or middle frames. After unwind, the outer's StrictMode
        // stays true (the per-proc push/pops balanced).
        var middleNonStrict = Path.Combine(_tempDir, "middle_nonstrict.flow");
        var leafStrict = Path.Combine(_tempDir, "leaf_strict.flow");

        File.WriteAllText(leafStrict,
            "enable strict;\n"
            + "proc leaf ()\n"
            + "    (__strictProbe)\n"
            + "end proc\n");

        var leafPosix = leafStrict.Replace('\\', '/');
        File.WriteAllText(middleNonStrict,
            "use \"" + leafPosix + "\"\n"
            + "proc middle ()\n"
            + "    (leaf)\n"
            + "end proc\n");

        var engine = NewEngineWithProbe(out var get);
        try
        {
            var middlePosix = middleNonStrict.Replace('\\', '/');
            var outerSrc =
                "enable strict;\n"
                + "internal proc __strictProbe ()\n"
                + "use \"" + middlePosix + "\"\n"
                + "(middle)\n";
            var ok = engine.Execute(outerSrc, "<top>");
            Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");

            Assert.True(get().HasValue, "__strictProbe must have been invoked from inside leaf.");
            Assert.True(get()!.Value,
                "leaf proc declared in a strict file invoking __strictProbe must see "
                + "CallerStrictMode=true (reflects leaf's StrictMode, the immediate caller).");

            Assert.True(engine.Context.StrictMode,
                "outer strict file's StrictMode must be RESTORED to true after all per-proc pushes pop.");
        }
        finally { engine.Dispose(); }
    }

    [Fact]
    public void Fact_ThrowInStrictProc_RestoresBitOnUnwind()
    {
        // A proc declared in a strict file divides by zero (a runtime error in
        // Flow). Execute returns false, but the outer engine.Context.StrictMode
        // must still equal its pre-call value (try/finally guarantee — Anti-Pattern 1).
        //
        // We invoke the proc from a NON-strict outer to make the test signal
        // unambiguous: pre-call StrictMode is false; after the import + failed
        // call, StrictMode must still be false.
        var strictProcPath = Path.Combine(_tempDir, "strict_failing_proc.flow");
        File.WriteAllText(strictProcPath,
            "enable strict;\n"
            + "proc divByZero ()\n"
            + "    Int x = (div 1 0)\n"
            + "    x\n"
            + "end proc\n");

        var engine = NewEngineWithProbe(out _);
        try
        {
            var strictPosix = strictProcPath.Replace('\\', '/');
            var outerSrc =
                "use \"" + strictPosix + "\"\n"
                + "(divByZero)\n";
            engine.Execute(outerSrc, "<top>");
            // Don't assert Execute's bool — we only care about the post-call StrictMode value.
            Assert.False(engine.Context.StrictMode,
                "outer non-strict file's StrictMode must STAY false after a strict-proc "
                + "invocation that errors mid-body — try/finally must restore the prev bit.");
        }
        finally { engine.Dispose(); }
    }

    [Fact]
    public void Fact_QualifiedCall_SnapshotsCallerStrict()
    {
        // Phase 43 qualified-call branch in ExpressionEvaluator (lines 240-256).
        // Strict outer module-qualified-calls a proc registered in a non-strict
        // module. The proc body invokes the probe. The qualified-call branch
        // must snapshot CallerStrictMode just like the unqualified branch.
        //
        // The proc's IsStrict is false (its declaring file is non-strict), so
        // inside the proc body StrictMode==false; the probe at the leaf sees
        // CallerStrictMode==false (proc's own bit, NOT the outer's strict bit).
        var modulePath = Path.Combine(_tempDir, "qualmod.flow");
        File.WriteAllText(modulePath,
            "module qualmod\n"
            + "internal proc __strictProbe ()\n"
            + "proc invokeProbe ()\n"
            + "    (__strictProbe)\n"
            + "end proc\n");

        var engine = NewEngineWithProbe(out var get);
        try
        {
            var modPosix = modulePath.Replace('\\', '/');
            var outerSrc =
                "enable strict;\n"
                + "use \"" + modPosix + "\"\n"
                + "(qualmod.invokeProbe)\n";
            var ok = engine.Execute(outerSrc, "<top>");
            Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");
            Assert.True(get().HasValue,
                "__strictProbe must have been invoked via the qualified call (qualmod.invokeProbe).");
            Assert.False(get()!.Value,
                "leaf builtin invoked from a non-strict proc reached via qualified call "
                + "must see CallerStrictMode=false (D-05 covers BOTH unqualified + qualified branches).");
        }
        finally { engine.Dispose(); }
    }
}
