using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Phase 43 Plan 43-04 D-08 — Beat ↔ Second tempo-aware conversion builtins.
///
/// Registers <c>beatToSec(Beat) → Second</c> and <c>secToBeat(Second) → Beat</c>
/// via the context-dependent registration channel so each call reads
/// <c>context.GetMusicalContext().Tempo</c> fresh. When no <c>tempo N { ... }</c>
/// block is active, the conversion defaults to 120 BPM and emits a one-shot
/// stderr advisory via <see cref="RenderingDiagnostics.WarnOnce"/> (dedup'd
/// per process per sentinel key — same channel + discipline used by
/// MidiExport / HarmonyFunctions / live-block timeout advisories).
///
/// Closes the AUDIT.md §1 BeatType-orphan anchor + §2 Beat↔Second conversion
/// gap surfaced by Phase 42. Mirrors the canonical Phase 22 DX-12 recipe at
/// <see cref="EffectsFunctions.RegisterContextDependent"/> (EffectsFunctions.cs:359-389)
/// — the closure captures <paramref name="context"/> so the active tempo
/// resolves fresh on every call.
///
/// Wired into the registration chain from
/// <see cref="BuiltInFunctions.RegisterContextDependentFunctions"/> alongside
/// the other context-bound audio registrations.
/// </summary>
public static class BeatConversionFunctions
{
    /// <summary>
    /// Registers <c>beatToSec</c> + <c>secToBeat</c>. Both read the active tempo
    /// from <see cref="MusicalContext.Tempo"/> on every call (closure-captured
    /// <paramref name="context"/>) and emit a one-shot
    /// <c>[<name>] no active tempo — defaulting to 120 BPM</c> advisory when
    /// the default fires.
    ///
    /// Per Phase 30 Plan 30-03 REQ-4, <see cref="FlowLang.Runtime.ExecutionContext.GetMusicalContext"/>
    /// returns a three-tier-fallback context that ALWAYS reports a non-null
    /// <see cref="MusicalContext.Tempo"/> (final tier: hard-coded 120 BPM). To
    /// distinguish "no tempo block in scope" from "tempo N { } block sets the
    /// tempo," the advisory branch walks the call-stack frame chain directly
    /// and checks whether any frame asserted a tempo of its own.
    /// </summary>
    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        // beatToSec(Beat) → Second
        var beatToSecSig = new FunctionSignature(
            "beatToSec",
            [BeatType.Instance],
            ParameterNames: ["beats"]);
        registry.Register("beatToSec", beatToSecSig, args =>
        {
            // BeatType backs double per BeatType.cs:25-28; same convention as
            // Cent/Millisecond/Decibel — `args[0].As<double>()` reads it directly.
            double beats = args[0].As<double>();

            // Read effective tempo through the three-tier fallback helper (always non-null).
            double bpm = context.GetMusicalContext().Tempo ?? 120.0;

            // Separately detect whether any *explicit* tempo block is in scope by walking
            // the StackFrame chain. GetMusicalContext() injects the 120 BPM default at
            // tier 3, so we can't use its return value to detect "no tempo block".
            if (!AnyFrameHasTempo(context.CurrentFrame))
            {
                RenderingDiagnostics.WarnOnce(
                    "beatToSec-no-tempo",
                    "[beatToSec] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)");
            }

            double seconds = beats * (60.0 / bpm);
            return Value.Second(seconds);
        });

        // secToBeat(Second) → Beat (symmetric inverse)
        var secToBeatSig = new FunctionSignature(
            "secToBeat",
            [SecondType.Instance],
            ParameterNames: ["seconds"]);
        registry.Register("secToBeat", secToBeatSig, args =>
        {
            double seconds = args[0].As<double>();

            double bpm = context.GetMusicalContext().Tempo ?? 120.0;
            if (!AnyFrameHasTempo(context.CurrentFrame))
            {
                RenderingDiagnostics.WarnOnce(
                    "secToBeat-no-tempo",
                    "[secToBeat] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)");
            }

            double beats = seconds * (bpm / 60.0);
            return Value.Beat(beats);
        });
    }

    /// <summary>
    /// Walks the <see cref="StackFrame"/> parent chain (innermost → global)
    /// looking for an explicit <see cref="MusicalContext.Tempo"/> assignment.
    /// Returns <c>true</c> only when some frame in scope set a tempo
    /// (via a <c>tempo N { ... }</c> block or a top-level pragma). Returns
    /// <c>false</c> when every frame's <see cref="MusicalContext"/> is null
    /// or has a null Tempo — the signal that the 120-BPM default tier fired.
    /// </summary>
    private static bool AnyFrameHasTempo(StackFrame? frame)
    {
        for (var f = frame; f != null; f = f.Parent)
        {
            if (f.MusicalContext is { Tempo: not null }) return true;
        }
        return false;
    }
}
