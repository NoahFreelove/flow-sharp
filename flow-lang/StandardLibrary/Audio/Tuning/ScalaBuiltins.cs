using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Phase 32 Plan 32-04 — registration entry point for the
/// <c>(loadScala "path")</c> 1-arg + <c>(loadScala "scl" "kbm")</c> 2-arg
/// builtins. Both return a Flow <see cref="Value"/> typed as
/// <see cref="TuningType"/> wrapping a <see cref="ResolvedTuning"/>.
///
/// D-08 (unmapped MIDI key advisory): at load time, after the
/// <see cref="ResolvedTuning"/> is built, scan the per-key
/// <see cref="ResolvedTuning.MidiToHz"/> table inside the KBM's
/// <c>[FirstMidi, LastMidi]</c> range for any zero entries; if found, fire
/// <see cref="RenderingDiagnostics.WarnOnce"/> with a sentinel keyed by the
/// tuning's <see cref="ResolvedTuning.Description"/> — at most one advisory
/// per description per process.
///
/// File access: <see cref="File.ReadAllText(string)"/> opens any file the
/// running user can read (threat T-32-IO-01, disposition: accept — matches
/// Flow's existing <c>writeWav</c> / <c>writeMidi</c> file-IO posture).
/// </summary>
public static class ScalaBuiltins
{
    /// <summary>
    /// Wire both <c>(loadScala String) → Tuning</c> overloads and the
    /// <c>(str Tuning) → String</c> string-conversion overload into the
    /// internal function registry. Called from <c>FlowEngine</c>'s startup
    /// path alongside the existing <c>Register*</c> calls.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        // Phase 44 Plan 44-07: legacy entry. Tests / harnesses that don't have
        // an ExecutionContext (e.g. unit tests for the parser surface) call this
        // overload; the strict-mode branch is skipped and the original
        // charitable WarnOnce path runs. Real composer surface goes through
        // RegisterContextDependent at FlowEngine init for [strict] elevation.
        RegisterImpl(registry, ctx: null);
    }

    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        RegisterImpl(registry, context);
    }

    private static void RegisterImpl(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext? ctx)
    {
        // 1-arg: loadScala(String) → Tuning
        var sigOne = new FunctionSignature("loadScala", [StringType.Instance],
            ParameterNames: ["path"]);
        registry.Register("loadScala", sigOne, args => LoadScalaOneArg(args, ctx));

        // 2-arg: loadScala(String, String) → Tuning
        var sigTwo = new FunctionSignature("loadScala",
            [StringType.Instance, StringType.Instance],
            ParameterNames: ["sclPath", "kbmPath"]);
        registry.Register("loadScala", sigTwo, args => LoadScalaTwoArg(args, ctx));

        // (str Tuning) → String  per CONTEXT D-04 description format
        var sigStrTuning = new FunctionSignature("str", [TuningType.Instance],
            ParameterNames: ["tuning"]);
        registry.Register("str", sigStrTuning, StrTuning);
    }

    private static Value LoadScalaOneArg(System.Collections.Generic.IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext? ctx)
    {
        string sclPath = args[0].As<string>();
        string sclContent = File.ReadAllText(sclPath);
        var parsedScl = ScalaParser.Parse(sclContent, sclPath);
        var kbm = ScalaKbmParser.Default(parsedScl);
        var resolved = new ResolvedTuning(parsedScl, kbm);
        FireUnmappedAdvisoryIfNeeded(resolved, kbm, ctx);
        return Value.Tuning(resolved);
    }

    private static Value LoadScalaTwoArg(System.Collections.Generic.IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext? ctx)
    {
        string sclPath = args[0].As<string>();
        string kbmPath = args[1].As<string>();
        string sclContent = File.ReadAllText(sclPath);
        var parsedScl = ScalaParser.Parse(sclContent, sclPath);
        string kbmContent = File.ReadAllText(kbmPath);
        var partialKbm = ScalaKbmParser.Parse(kbmContent, kbmPath);
        // ScalaKbmParser.Parse leaves Period = 0.0 (see its XML-doc) — overlay
        // the .scl's PeriodCents per D-07 so the resolved KBM auto-adopts the
        // tuning's period for downstream rendering.
        var kbm = new ScalaKbm(
            partialKbm.Size,
            partialKbm.FirstMidi,
            partialKbm.LastMidi,
            partialKbm.MiddleNote,
            partialKbm.ReferenceNote,
            partialKbm.ReferenceHz,
            partialKbm.FormalOctave,
            partialKbm.Mapping,
            period: parsedScl.PeriodCents);
        var resolved = new ResolvedTuning(parsedScl, kbm);
        FireUnmappedAdvisoryIfNeeded(resolved, kbm, ctx);
        return Value.Tuning(resolved);
    }

    private static Value StrTuning(System.Collections.Generic.IReadOnlyList<Value> args)
    {
        var resolved = args[0].As<ResolvedTuning>();
        return Value.String(resolved.ToString());
    }

    /// <summary>
    /// D-08: scan the MIDI→Hz table inside the KBM's <c>[FirstMidi, LastMidi]</c>
    /// range; if any entry is 0.0 (unmapped via the <c>x</c> keymap encoding), fire
    /// the one-shot stderr advisory. Sentinel keyed by tuning Description so the
    /// same description doesn't spam each unmapped note — matches Phase 23 D-13's
    /// "one warning per tuning name per process" pattern (CONTEXT § Specifics).
    /// </summary>
    private static void FireUnmappedAdvisoryIfNeeded(ResolvedTuning resolved, ScalaKbm kbm, FlowLang.Runtime.ExecutionContext? ctx)
    {
        bool anyUnmapped = false;
        // Bounds-clamp [FirstMidi, LastMidi] to the table's 0..127 range — defends
        // against a malformed KBM with out-of-range integers (Plan 32-02's parser
        // already validates the range, but defense-in-depth keeps the loop safe).
        int lo = kbm.FirstMidi < 0 ? 0 : kbm.FirstMidi;
        int hi = kbm.LastMidi > 127 ? 127 : kbm.LastMidi;
        for (int midi = lo; midi <= hi; midi++)
        {
            if (resolved.MidiToHz[midi] == 0.0)
            {
                anyUnmapped = true;
                break;
            }
        }
        if (!anyUnmapped) return;

        // Phase 44 Plan 44-07 Pattern S3: strict-mode branch. Unmapped MIDI keys
        // under a custom .scl file are typically a composer-authored omission —
        // strict mode escalates to a composer-visible [strict] error so the
        // composer can decide between filling the gap or accepting the rests.
        if (ctx is not null && ctx.CallerStrictMode)
        {
            ctx.ErrorReporter.ReportError(
                $"[strict] [tuning] malformed .scl line — unmapped MIDI keys under '{resolved.Description}' (rendered as rest) at {ctx.CurrentCallSite}",
                ctx.CurrentCallSite);
            return;
        }

        RenderingDiagnostics.WarnOnce(
            sentinelKey: $"tuning:unmapped:{resolved.Description}",
            message: $"[tuning] unmapped MIDI keys under '{resolved.Description}' — rendered as rest");
    }
}
