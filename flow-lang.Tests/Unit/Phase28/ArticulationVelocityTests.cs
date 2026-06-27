using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Ast.Expressions;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Unit.Phase28;

/// <summary>
/// Phase 28 (SPEC-4) Plan 02 acceptance facts pinning the LOCKED velocity rules
/// applied at <see cref="NoteStreamCompiler"/>:
///
///   Accent     +0.30 (clamped to 1.0)
///   Marcato    +0.30 (clamped to 1.0)  — composes with Staccato's 25% duration envelope
///   Sforzando   no scalar boost (envelope spike applied per-synth in Plan 28-03)
///   Staccato   unchanged
///   Tenuto     unchanged
///   Legato     unchanged
///   Normal     unchanged
///
/// Tolerance: ±0.02 in 0..1 space (~±2 MIDI velocity units) per SPEC-4 acceptance #6.
///
/// Sforzando has no parser articulation token (per <see cref="Parser"/> only `>`,
/// `stacc`, `ten`, `marc`, `leg` are recognized) — its Fact constructs a
/// <see cref="NoteElement"/> AST node directly. The Marcato_StaccEnvelope_AccentVelocity
/// cross-cut Fact (Task 5) verifies Marcato's two-rule composition end-to-end.
/// </summary>
public class ArticulationVelocityTests
{
    private const double BaseVelocity = 0.5;
    private const double Tolerance = 0.02;
    private const double ExpectedBoosted = BaseVelocity + 0.30; // 0.80

    private static SequenceData CompileWithVelocity(string source, double velocity = BaseVelocity)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        var program = parser.Parse();
        Assert.False(reporter.HasErrors, $"Parse errors: {reporter.FormatErrors()}");

        var stmt = program.Statements.OfType<FlowLang.Ast.Statements.ExpressionStatement>().Single();
        var noteStream = (NoteStreamExpression)stmt.Expression;

        var compiler = new NoteStreamCompiler();
        var ctx = new MusicalContext { TimeSignature = new TimeSignatureData(4, 4), Velocity = velocity };
        return compiler.Compile(noteStream, ctx);
    }

    /// <summary>
    /// Compiles a single C4 quarter note with the given <paramref name="articulation"/> by
    /// constructing the AST directly. Used where no parser articulation token exists
    /// (Sforzando) or where we want to bypass the parser's articulation-token surface.
    /// </summary>
    private static MusicalNoteData CompileSingleC4q(Articulation articulation, double velocity = BaseVelocity)
    {
        var loc = new SourceLocation(1, 1, "<test>");
        var note = new NoteElement(
            Location: loc,
            NoteName: "C4",
            DurationSuffix: "q",
            IsDotted: false,
            IsTied: false,
            CentOffset: null,
            Velocity: null,
            ArticulationMark: articulation);
        var bar = new NoteStreamBar(loc, new NoteStreamElement[] { note });
        var stream = new NoteStreamExpression(loc, new[] { bar });

        var compiler = new NoteStreamCompiler();
        var ctx = new MusicalContext { TimeSignature = new TimeSignatureData(4, 4), Velocity = velocity };
        var seq = compiler.Compile(stream, ctx);
        return seq.Bars[0].MusicalNotes.Single(n => !n.IsRest);
    }

    [Fact]
    public void Velocity_Accent_PlusThirtyPercent()
    {
        var seq = CompileWithVelocity("| C4q > |");
        var note = seq.Bars[0].MusicalNotes.Single(n => !n.IsRest);
        Assert.Equal(Articulation.Accent, note.Articulation);
        Assert.InRange(note.Velocity, ExpectedBoosted - Tolerance, ExpectedBoosted + Tolerance);
    }

    [Fact]
    public void Velocity_Marcato_PlusThirtyPercent()
    {
        var seq = CompileWithVelocity("| C4q marc |");
        var note = seq.Bars[0].MusicalNotes.Single(n => !n.IsRest);
        Assert.Equal(Articulation.Marcato, note.Articulation);
        Assert.InRange(note.Velocity, ExpectedBoosted - Tolerance, ExpectedBoosted + Tolerance);
    }

    [Fact]
    public void Velocity_Sforzando_NoScalarBoost()
    {
        // Regression guard: prior code overrode velocity to 0.95 for Sforzando, clobbering
        // the composer's intended dynamic. SPEC-4 routes Sforzando through a time-varying
        // envelope spike at the synth layer (Plan 28-03), so the compiler-layer velocity
        // passes through the composer's input unchanged.
        var note = CompileSingleC4q(Articulation.Sforzando, BaseVelocity);
        Assert.Equal(Articulation.Sforzando, note.Articulation);
        Assert.InRange(note.Velocity, BaseVelocity - Tolerance, BaseVelocity + Tolerance);
    }

    [Fact]
    public void Velocity_Staccato_Unchanged()
    {
        var seq = CompileWithVelocity("| C4q stacc |");
        var note = seq.Bars[0].MusicalNotes.Single(n => !n.IsRest);
        Assert.Equal(Articulation.Staccato, note.Articulation);
        Assert.InRange(note.Velocity, BaseVelocity - Tolerance, BaseVelocity + Tolerance);
    }

    [Fact]
    public void Velocity_Tenuto_Unchanged()
    {
        var seq = CompileWithVelocity("| C4q ten |");
        var note = seq.Bars[0].MusicalNotes.Single(n => !n.IsRest);
        Assert.Equal(Articulation.Tenuto, note.Articulation);
        Assert.InRange(note.Velocity, BaseVelocity - Tolerance, BaseVelocity + Tolerance);
    }

    [Fact]
    public void Velocity_Legato_Unchanged()
    {
        var seq = CompileWithVelocity("| C4q leg |");
        var note = seq.Bars[0].MusicalNotes.Single(n => !n.IsRest);
        Assert.Equal(Articulation.Legato, note.Articulation);
        Assert.InRange(note.Velocity, BaseVelocity - Tolerance, BaseVelocity + Tolerance);
    }

    [Fact]
    public void Velocity_Normal_Unchanged()
    {
        var seq = CompileWithVelocity("| C4q |");
        var note = seq.Bars[0].MusicalNotes.Single(n => !n.IsRest);
        Assert.Equal(Articulation.Normal, note.Articulation);
        Assert.InRange(note.Velocity, BaseVelocity - Tolerance, BaseVelocity + Tolerance);
    }

    [Fact]
    public void Velocity_AccentClampedAtOne()
    {
        // Base 0.9 + 0.30 = 1.20 → clamps to 1.0 per Math.Min(velocity + 0.30, 1.0).
        var seq = CompileWithVelocity("| C4q > |", velocity: 0.9);
        var note = seq.Bars[0].MusicalNotes.Single(n => !n.IsRest);
        Assert.Equal(Articulation.Accent, note.Articulation);
        Assert.InRange(note.Velocity, 1.0 - Tolerance, 1.0 + 1e-9);
    }

    // ===== Task 5 — Marcato composes Staccato envelope + Accent velocity =====

    [Fact]
    public void Marcato_StaccEnvelope_AccentVelocity()
    {
        // Cross-cut Fact: a single Marcato note must satisfy BOTH locked rules.
        //   1. Compiler velocity == 0.5 + 0.30 = 0.80 (Accent's boost)
        //   2. BarRenderer audible duration == 0.5 × 0.25 = 0.125 sec (Staccato's 25%)
        var seq = CompileWithVelocity("| C4q marc |");
        var note = seq.Bars[0].MusicalNotes.Single(n => !n.IsRest);
        Assert.Equal(Articulation.Marcato, note.Articulation);

        // (1) Velocity check
        Assert.InRange(note.Velocity, ExpectedBoosted - Tolerance, ExpectedBoosted + Tolerance);

        // (2) Audible-duration check via BarRenderer
        var bar = new BarData(new[] { note }, new TimeSignatureData(4, 4));
        var voices = BarRenderer.RenderBarToVoices(bar, "sine", 44100, 120.0);
        Assert.Single(voices);
        double audibleSec = AudibleDurationSeconds(voices[0].Buffer);
        const double expectedSec = 0.5 * 0.25; // 0.125
        Assert.InRange(audibleSec, expectedSec * 0.95, expectedSec * 1.05);
    }

    private static double AudibleDurationSeconds(AudioBuffer buffer)
    {
        const double threshold = 0.001;
        int firstAudible = -1;
        int lastAudible = -1;
        for (int frame = 0; frame < buffer.Frames; frame++)
        {
            bool audible = false;
            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                if (Math.Abs(buffer.GetSample(frame, ch)) > threshold)
                {
                    audible = true;
                    break;
                }
            }
            if (audible)
            {
                if (firstAudible < 0) firstAudible = frame;
                lastAudible = frame;
            }
        }
        if (firstAudible < 0) return 0.0;
        return (lastAudible - firstAudible + 1) / (double)buffer.SampleRate;
    }
}
