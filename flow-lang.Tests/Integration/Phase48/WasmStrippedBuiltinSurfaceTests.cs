using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 (debug session wasm-boot-no-app-bundle, CYCLE 6) — Web-target
/// charitable handling of stdlib `internal proc` surfaces whose C# impl is
/// STRIPPED at compile time (csproj `&lt;Compile Remove&gt;` per Phase 47
/// D-47-03).
///
/// The defect: `audio.flow` declares `internal proc micBuffer(Second|Double)`
/// and `std.flow` declares `internal proc loadSfz(Symbol|String)`, but their
/// implementations (`InputFunctions.cs`, `Sfz/**`) are stripped on Web. Before
/// the cycle-6 fix, <c>Interpreter.ExecuteProcDeclaration</c> hit a hard
/// <c>_errorReporter.ReportError("No C# implementation found ...")</c> for each
/// such overload; the accumulated errors made <c>ModuleLoader</c> fail the WHOLE
/// import — so <c>use "@audio"</c> failed (taking createSineTone/play down with
/// it) and the engine-ctor <c>@std</c> bootstrap was dirtied by loadSfz errors.
///
/// The fix: on the Web target only, <c>ExecuteProcDeclaration</c> SKIPS a
/// missing-impl internal proc and emits a one-shot
/// <c>[target] builtin '...' unavailable on Web target</c> advisory (keyed per
/// proc-name via <see cref="RenderingDiagnostics.WarnOnce"/>). Desktop keeps the
/// hard error (a missing impl there is a genuine bug).
///
/// Facts are <c>[FlowTargetFact("Web")]</c> — they RUN only when the test
/// assembly is built with <c>-p:FlowTarget=Web</c> (which defines FLOW_WEB so
/// <see cref="FlowEngine.IsWebTarget"/> is true in-process and the charitable
/// branch actually executes). On a Desktop runner they SKIP — exactly mirroring
/// Phase 47 <c>WebTargetModuleLoaderTests</c>. This is the documented way to
/// exercise the Web branch without a browser; the human in-browser re-smoke
/// remains the final closure.
/// </summary>
public class WasmStrippedBuiltinSurfaceTests
{
    [FlowTargetFact("Web")]
    public void UseAudioImport_Succeeds_WhenMicBufferImplStripped_OnWeb()
    {
        RenderingDiagnostics.ResetForTesting();
        var origErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        bool ok;
        try
        {
            // `use "@audio"` declares the micBuffer surface whose impl is
            // stripped on Web. Pre-fix this whole import FAILED. Post-fix it
            // succeeds (micBuffer charitably skipped) and createSineTone binds.
            var src = "use \"@audio\"\nBuffer tone = (createSineTone 440Hz 1.0 0.5)\n";
            var engine = new FlowEngine();
            ok = engine.Execute(src, "<test>");
        }
        finally
        {
            Console.SetError(origErr);
        }

        Assert.True(ok, "Web build must IMPORT @audio successfully with micBuffer charitably skipped.");
        var stderr = sw.ToString();
        Assert.Contains("[target] builtin 'micBuffer' unavailable on Web target", stderr);
    }

    [FlowTargetFact("Web")]
    public void CreateSineTone_Resolves_AfterAudioImport_OnWeb()
    {
        RenderingDiagnostics.ResetForTesting();
        var src = "use \"@audio\"\nBuffer tone = (createSineTone 440Hz 1.0 0.5)\n";
        var engine = new FlowEngine();
        engine.Execute(src, "<test>");

        // The bug took createSineTone down with the failed @audio import. Assert
        // its surface RESOLVES now — no "Function not found", no "Failed to import".
        Assert.DoesNotContain(engine.ErrorReporter.Errors,
            e => e.ToString().Contains("createSineTone") && e.ToString().Contains("not found"));
        Assert.DoesNotContain(engine.ErrorReporter.Errors,
            e => e.ToString().Contains("Failed to import"));
    }

    [FlowTargetFact("Web")]
    public void MicBuffer_Call_DegradesTo_FunctionNotFound_OnWeb()
    {
        RenderingDiagnostics.ResetForTesting();
        var src = "use \"@audio\"\nBuffer m = (micBuffer 0.5)\n";
        var engine = new FlowEngine();
        engine.Execute(src, "<test>");

        // The stripped builtin yields a NORMAL "function not found" (acceptable),
        // NOT a hard "No C# implementation found" import failure.
        Assert.Contains(engine.ErrorReporter.Errors,
            e => e.ToString().Contains("micBuffer") && e.ToString().Contains("not found"));
        Assert.DoesNotContain(engine.ErrorReporter.Errors,
            e => e.ToString().Contains("No C# implementation found"));
        Assert.DoesNotContain(engine.ErrorReporter.Errors,
            e => e.ToString().Contains("Failed to import"));
    }

    [FlowTargetFact("Web")]
    public void ExplicitStdImportIsCleanWhenLoadSfzImplIsStrippedOnWeb()
    {
        using var diagnostics = new StringWriter();
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = diagnostics });
        Assert.Empty(engine.ErrorReporter.Errors);
        Assert.Equal("", diagnostics.ToString()); // The engine has no implicit prelude.
        Assert.True(engine.Execute("use \"@std\"", "<std>"));
        Assert.DoesNotContain(engine.ErrorReporter.Errors,
            e => e.ToString().Contains("No C# implementation found") || e.ToString().Contains("loadSfz"));
        Assert.Contains("[target] builtin 'loadSfz' unavailable on Web target", diagnostics.ToString());
    }

    [FlowTargetFact("Web")]
    public void StrippedBuiltinAdvisoryIsDeduplicatedWithinEachIndependentSession()
    {
        for (int i = 0; i < 2; i++)
        {
            using var diagnostics = new StringWriter();
            using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = diagnostics });
            Assert.True(engine.Execute("use \"@audio\"", "<first>"));
            Assert.True(engine.Execute("use \"@audio\"", "<second>"));
            int occurrences = diagnostics.ToString().Split("[target] builtin 'micBuffer' unavailable").Length - 1;
            Assert.Equal(1, occurrences); // A previous engine must not suppress this engine's warning.
        }
    }
}
