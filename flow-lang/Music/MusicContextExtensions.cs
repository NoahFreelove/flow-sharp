using FlowLang.Runtime;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Music;

/// <summary>
/// Music members on the language runtime: the scoped <see cref="MusicalContext"/> of
/// a frame, and the per-context <see cref="MusicSession"/> state.
/// </summary>
public static class MusicContextExtensions
{
    extension(StackFrame frame)
    {
        /// <summary>This frame's musical context overrides. Null means "inherit".</summary>
        public MusicalContext? MusicalContext
        {
            get => frame.GetScope<MusicalContext>();
            set => frame.SetScope(value);
        }
    }

    extension(ExecutionContext context)
    {
        public MusicSession Music => context.GetExtension<MusicSession>();

        public Dictionary<string, List<SectionData>> SectionRegistry => context.Music.SectionRegistry;
        public Dictionary<string, SectionData> SectionRegistryFlat() => context.Music.SectionRegistryFlat();
        public Dictionary<Value, DictData> StyleRegistry => context.Music.StyleRegistry;
        public HashSet<string> StyleOverrideAdvisoriesEmitted => context.Music.StyleOverrideAdvisoriesEmitted;

        public bool SuppressStyleOverrideAdvisory
        {
            get => context.Music.SuppressStyleOverrideAdvisory;
            set => context.Music.SuppressStyleOverrideAdvisory = value;
        }

        public bool SfzEnabled
        {
            get => context.Music.SfzEnabled;
            set => context.Music.SfzEnabled = value;
        }

        public Dictionary<Value, string> SfzInstruments => context.Music.SfzInstruments;
#if !FLOW_WEB
        public Dictionary<string, StandardLibrary.Audio.Sfz.SfzData> SfzPatchRegistry => context.Music.SfzPatchRegistry;
#endif
        public HashSet<string> SfzDiagnostics => context.Music.SfzDiagnostics;

        public string? ResolvedSfzRoot
        {
            get => context.Music.ResolvedSfzRoot;
            set => context.Music.ResolvedSfzRoot = value;
        }

        /// <summary>
        /// The musical context in effect: innermost block value per property, then
        /// config defaults. Read-only; memoized until scope state changes.
        /// </summary>
        public MusicalContext GetMusicalContext() => context.Music.Resolve(context);

        /// <summary>Sets the current frame's musical context (a context block's frame).</summary>
        public void SetCurrentFrameMusicalContext(MusicalContext? musicalContext) =>
            context.CurrentFrame.MusicalContext = musicalContext;

        /// <summary>
        /// Phase 32 D-12: replaces the file-scope tuning (the bottom of the global
        /// frame's tuning stack) with <paramref name="renderTuning"/>. Called by the
        /// pragma bridge only when a tuning pragma is present, so REPL evaluations
        /// without one keep the previous tuning.
        /// </summary>
        public void SetFileScopeTuning(RenderTuning renderTuning)
        {
            var global = context.GlobalFrame.MusicalContext ??= new MusicalContext();
            global.TuningStack.Clear();
            global.TuningStack.Push(renderTuning);
            context.NotifyScopeChanged();
        }

        /// <summary>Pushes a <c>tuning t { }</c> block's tuning onto the current frame.</summary>
        public void PushTuning(RenderTuning renderTuning)
        {
            var current = context.CurrentFrame.MusicalContext ??= new MusicalContext();
            current.TuningStack.Push(renderTuning);
            context.NotifyScopeChanged();
        }

        /// <summary>Pops the current frame's block tuning; push/pop must be balanced.</summary>
        public void PopTuning()
        {
            var current = context.CurrentFrame.MusicalContext;
            if (current == null || current.TuningStack.Count == 0)
                throw new InvalidOperationException(
                    "PopTuning called with an empty TuningStack — push/pop must be balanced (Phase 32 D-12).");
            current.TuningStack.Pop();
            context.NotifyScopeChanged();
        }

        /// <summary>
        /// REPL boundary (Phase 32 D-14): drops block tunings above the file-scope
        /// pragma tuning, which stays sticky across evaluations.
        /// </summary>
        public void ResetBlockTuningStack()
        {
            var global = context.GlobalFrame.MusicalContext;
            if (global == null) return;
            while (global.TuningStack.Count > 1)
                global.TuningStack.Pop();
            context.NotifyScopeChanged();
        }
    }
}
