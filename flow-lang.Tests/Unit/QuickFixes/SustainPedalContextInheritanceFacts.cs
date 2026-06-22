using System.Collections.Generic;
using System.Linq;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.QuickFixes;

/// <summary>
/// Regression facts for the <c>sustainPedal { ... }</c> no-op bug
/// (debug session sustain-pedal-no-effect, 2026-06-22).
///
/// ROOT CAUSE: <see cref="ExecutionContext.GetMusicalContext"/> resolves the
/// active musical context by walking the call stack and merging each frame's
/// <see cref="MusicalContext"/> fields with <c>??=</c> (innermost-wins). The
/// merge loop inherited TimeSignature/Tempo/Swing/Key/Velocity/Pan/Gain/
/// ReverbTime/TuningStack/VoicePoolSize but DROPPED the SustainPedal bool?.
/// So a <c>section</c> nested inside a <c>sustainPedal { }</c> block snapshotted
/// a context with SustainPedal=null, SongRenderer's
/// <c>section.Context?.SustainPedal == true</c> read false, and the correct
/// render-side tail-extension at BarRenderer never fired — producing
/// byte-identical audio with and without the pedal.
///
/// FIX: one line — <c>resolved.SustainPedal ??= frame.MusicalContext.SustainPedal;</c>
/// — added to the GetMusicalContext frame-merge loop.
///
/// These facts pin BOTH halves of the path so it cannot silently regress:
///   1. The render-side mechanism: sustainPedalActive=true lengthens every
///      rendered Voice buffer by exactly SustainTailSeconds worth of frames.
///   2. The context-resolution path (the actual bug): a section declared inside
///      a sustainPedal { } block captures SustainPedal=true onto its snapshot.
///   3. The determinism guard: a script WITHOUT the block leaves the flag unset,
///      so the inheritance fix can never turn the pedal on accidentally (this is
///      what protects the no-pedal byte-identical / RMS-baseline contract).
/// </summary>
public class SustainPedalContextInheritanceFacts
{
    private const string Prelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    private const int SampleRate = 44100;
    private const double Bpm = 120.0;

    // ===== Fact 1 — render-side mechanism: tail extension lengthens voices =====

    [Fact]
    public void SustainPedal_ExtendsRenderedVoiceBufferFrameCount()
    {
        // Build a real bar of four quarter notes, then render it twice through
        // BarRenderer: once with the pedal OFF, once ON. Each note's rendered
        // Voice buffer must be longer by exactly SustainTailSeconds worth of
        // frames (the tail is appended to durationBeats, then synthesized).
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(Prelude + @"
Sequence src = | C4q C4q C4q C4q |
");
        Assert.Equal(0, errorCount);

        var src = runner.GetVariable("src").As<SequenceData>();
        Assert.NotEmpty(src.Bars);
        var bar = src.Bars[0];

        // Organ: full sustain, near-instant release — the synth honors the
        // extended duration without a decay envelope masking the change.
        var synth = SynthesizerFactory.Create("organ");

        var dry = BarRenderer.RenderBarToVoices(
            bar, synth, SampleRate, Bpm, RenderTuning.Default, sustainPedalActive: false);
        var wet = BarRenderer.RenderBarToVoices(
            bar, synth, SampleRate, Bpm, RenderTuning.Default, sustainPedalActive: true);

        Assert.Equal(dry.Count, wet.Count);
        Assert.NotEmpty(wet);

        // Expected extra frames = SustainTailSeconds * sampleRate. The tail is
        // converted to beats then back to frames by the synth at this bpm, so a
        // few frames of rounding slack is allowed, but the buffer MUST grow.
        int expectedTailFrames = (int)(MusicalContext.SustainTailSeconds * SampleRate);
        for (int i = 0; i < wet.Count; i++)
        {
            int dryFrames = dry[i].Buffer.Frames;
            int wetFrames = wet[i].Buffer.Frames;
            Assert.True(
                wetFrames > dryFrames,
                $"voice {i}: sustain pedal did not extend buffer — dry={dryFrames}, wet={wetFrames}");
            int grew = wetFrames - dryFrames;
            Assert.True(
                System.Math.Abs(grew - expectedTailFrames) <= SampleRate / 100, // ±10ms slack
                $"voice {i}: extension {grew} frames not ~= SustainTailSeconds {expectedTailFrames} frames");
        }
    }

    // ===== Fact 2 — context-resolution path: the actual root-cause surface =====

    [Fact]
    public void SustainPedalBlock_ReachesSectionContext()
    {
        // A section declared INSIDE a sustainPedal { } block must capture
        // SustainPedal=true onto its context snapshot. This is the exact merge
        // path GetMusicalContext dropped before the fix.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(Prelude + @"
tempo 88 { timesig 4/4 {
    sustainPedal {
        section t { Sequence s = | C4q C4q C4q C4q | }
    }
}}
");
        Assert.Equal(0, errorCount);

        var registry = runner.GetEngine().Context.SectionRegistry;
        Assert.True(registry.TryGetValue("t", out List<SectionData>? sections),
            $"section 't' not registered. stderr: {stderr}");
        var t = sections!.Single();

        Assert.NotNull(t.Context);
        Assert.True(t.Context!.SustainPedal == true,
            $"section.Context.SustainPedal expected true, was {(t.Context.SustainPedal?.ToString() ?? "null")} — " +
            "GetMusicalContext frame-merge dropped the pedal flag again");

        // Sibling context fields set on enclosing frames must still inherit too
        // (proves the merge loop as a whole still works, not just this one line).
        Assert.Equal(88.0, t.Context.Tempo);
        Assert.NotNull(t.Context.TimeSignature);
    }

    // ===== Fact 3 — determinism guard: no block ⇒ flag stays unset =====

    [Fact]
    public void NoSustainPedalBlock_LeavesSectionContextPedalUnset()
    {
        // The inheritance fix must NEVER turn the pedal on for a script that
        // doesn't ask for it — this is what preserves the byte-identical /
        // RMS-baseline contract for every existing script.
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(Prelude + @"
tempo 88 { timesig 4/4 {
    section t { Sequence s = | C4q C4q C4q C4q | }
}}
");
        Assert.Equal(0, errorCount);

        var registry = runner.GetEngine().Context.SectionRegistry;
        Assert.True(registry.TryGetValue("t", out List<SectionData>? sections));
        var t = sections!.Single();

        Assert.NotNull(t.Context);
        // Not true (null is fine) — SongRenderer's `== true` check then no-ops,
        // and the render takes the identical dry path.
        Assert.False(t.Context!.SustainPedal == true,
            "section with no sustainPedal block must NOT have the pedal flag set");
    }
}
