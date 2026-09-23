using FlowLang.Ast;
using FlowLang.Ast.Elements;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Interpreter;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem.SpecialTypes;
using RuntimeContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Music;

/// <summary>
/// Evaluates the music expressions bound through <see cref="DomainBindings"/>: unit
/// and pitch literals, chord and beat literals, note streams, progressions and songs
/// (including parameterized section calls).
/// </summary>
internal sealed class MusicExpressions
{
    private readonly ExpressionEvaluator _evaluator;
    private readonly RuntimeContext _context;
    private readonly ErrorReporter _errorReporter;
    private readonly IFunctionInvoker _invoker;

    public MusicExpressions(ExpressionEvaluator evaluator)
    {
        _evaluator = evaluator;
        _context = evaluator.Context;
        _errorReporter = evaluator.Errors;
        _invoker = evaluator.Invoker;
    }

    private Value Evaluate(Expression expr) => _evaluator.Evaluate(expr);

    public static Value? ParseLiteral(string text)
    {
        // Try to parse as Note (A-G with optional octave and alteration)
        try
        {
            var (note, octave, alteration) = NoteType.Parse(text);
            return MusicValue.Note(text); // Store original text
        }
        catch
        {
            // Not a note, continue
        }

        // Try to parse as Semitone (+/-Nst)
        if (text.EndsWith("st"))
        {
            string numberPart = text.Substring(0, text.Length - 2);
            if (int.TryParse(numberPart, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int semitoneValue))
            {
                return MusicValue.Semitone(semitoneValue);
            }
        }

        // Try to parse as Cent (+/-Nc)
        // InvariantCulture pinned so '.' always reads as the decimal point —
        // in comma-decimal locales (de-DE/fr-FR/...) a bare TryParse reads '.'
        // as a thousands separator and silently 10x-corrupts the value.
        if (text.EndsWith("c") && text.Length > 1)
        {
            string numberPart = text.Substring(0, text.Length - 1);
            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double centValue))
            {
                return MusicValue.Cent(centValue);
            }
        }

        // Try to parse as Time (Nms or Ns)
        if (text.EndsWith("ms"))
        {
            string numberPart = text.Substring(0, text.Length - 2);
            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double msValue))
            {
                return MusicValue.Millisecond(msValue);
            }
        }
        else if (text.EndsWith("s") && !text.EndsWith("ms"))
        {
            string numberPart = text.Substring(0, text.Length - 1);
            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double sValue))
            {
                return MusicValue.Second(sValue);
            }
        }

        // Try to parse as Decibel (+/-NdB or NdB)
        if (text.EndsWith("dB"))
        {
            string numberPart = text.Substring(0, text.Length - 2);
            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dbValue))
            {
                return MusicValue.Decibel(dbValue);
            }
        }

        // Phase 26.2 ERG-04: Try to parse as Hertz (NHz or NkHz). Check kHz BEFORE Hz
        // because EndsWith("Hz") is also true for "kHz" strings — matches HertzType.Parse ordering.
        if (text.EndsWith("kHz"))
        {
            string numberPart = text.Substring(0, text.Length - 3);
            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double kHzValue))
            {
                return MusicValue.Hertz(kHzValue * 1000.0);  // canonical Hz
            }
        }
        else if (text.EndsWith("Hz"))
        {
            string numberPart = text.Substring(0, text.Length - 2);
            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hzValue))
            {
                return MusicValue.Hertz(hzValue);
            }
        }

        // If we can't parse it as a special literal, return null
        // and the caller will treat it as a regular string
        return null;
    }

    /// <summary>
    /// Evaluates a note stream expression into a Sequence value using the active musical context.
    /// </summary>
    public Value EvaluateChordLiteral(ChordLiteralExpression chordLit)
    {
        if (ChordParser.TryParse(chordLit.ChordText, out var chordData))
        {
            return MusicValue.Chord(chordData!);
        }

        _errorReporter.ReportError($"Invalid chord symbol: '{chordLit.ChordText}'", chordLit.Location);
        return Value.Void();
    }

    /// <summary>
    /// Phase 45 D-10 — evaluates a <see cref="BeatLiteralExpression"/> (<c>Nb</c>)
    /// applying the eval-time true-to-sig multiplier:
    /// <code>final = pragma_on ? raw × (4.0 / denom) : raw</code>
    /// where <c>denom</c> is the active <see cref="MusicalContext.TimeSignature"/>
    /// denominator (defaulting to 4 — i.e. 4/4 identity — when no timesig is set,
    /// per D-02 / Pitfall 4). The pragma bit lives on
    /// <see cref="FlowLang.Runtime.ExecutionContext.BeatTrueToSig"/> (set by
    /// the declaring file's <c>enable beat-true-to-sig;</c>, file-scoped per D-04).
    /// With pragma OFF the multiplier is always 1.0 (raw passes through); with
    /// pragma ON in 4/4 (or no timesig) the multiplier is 4/4 = 1.0 (identity) —
    /// activation never corrupts scripts that never set a non-quarter meter.
    /// Internal storage stays quarter-relative (<see cref="MusicValue.Beat(double)"/>),
    /// so every downstream Beat consumer is unaffected (construction-only desugar).
    /// </summary>
    public Value EvaluateBeatLiteral(BeatLiteralExpression beatLit)
    {
        // D-02 three-tier fallback: GetMusicalContext() resolves call-stack →
        // FlowConfig → default 4/4. TimeSignature?.Denominator ?? 4 keeps the
        // divide-by-zero-proof identity default (T-45-09 mitigation).
        int denom = _context.GetMusicalContext().TimeSignature?.Denominator ?? 4;
        double multiplier = _context.BeatTrueToSig ? (4.0 / denom) : 1.0;
        return MusicValue.Beat(beatLit.RawValue * multiplier);
    }

    public Value EvaluateNoteStream(NoteStreamExpression noteStream)
    {
        var context = _context.GetMusicalContext();
        // TUP-05: thread the engine's ErrorReporter so ValidateBarFit can emit
        // Info-severity bar-overflow diagnostics. Backward-compatible defaulted-parameter
        // pattern — Plan 19-01/19-02 unit Facts continue using the parameterless ctor.
        var compiler = new NoteStreamCompiler(_errorReporter);
        var sequence = compiler.Compile(noteStream, context, _context);
        return MusicValue.Sequence(sequence);
    }

    public Value EvaluateProgression(ProgressionExpression progression)
    {
        var context = _context.GetMusicalContext();
        if (context.Key == null)
        {
            _errorReporter.ReportError(
                "progression requires an active key context (use `key Cmajor { ... }`)",
                progression.Location);
            return Value.Void();
        }

        var compiler = new ProgressionCompiler();
        var sequence = compiler.Compile(progression, context);
        return MusicValue.Sequence(sequence);
    }

    public Value EvaluateSong(SongExpression song)
    {
        var sectionRefs = new List<SongSectionRef>();
        var flatRegistry = new Dictionary<string, SectionData>();

        // Phase 36 Plan 36-10 (D-36-13) — when the parser populated
        // song.Elements (mixed BareSectionElement + SectionCallElement), the
        // ELEMENT path is canonical. Each SectionCallElement materializes a
        // SectionData via OverloadResolver dispatch + synthetic-frame body
        // execution, registered under a unique synthetic name so the
        // downstream renderer sees a flat registry of zero-arg-shaped
        // entries (preserves SongRenderer / MidiExport / SfzSampleCache
        // backward compatibility).

        var elements = song.Elements;
        if (elements != null)
        {
            int callIdx = 0;
            foreach (var elem in elements)
            {
                if (elem is BareSectionElement bare)
                {
                    if (!_context.SectionRegistry.TryGetValue(bare.Name, out var existing))
                    {
                        _errorReporter.ReportError(
                            $"Undefined section '{bare.Name}' in song arrangement", song.Location);
                        return Value.Void();
                    }
                    // Bare reference dispatches to the zero-arg overload (Parameters==null)
                    // if present, else the LAST-registered overload.
                    SectionData? target = null;
                    foreach (var s in existing)
                        if (s.Parameters == null) { target = s; break; }
                    target ??= existing[existing.Count - 1];
                    if (bare.RepeatCount <= 0)
                    {
                        _errorReporter.ReportError(
                            $"Repeat count must be positive, got {bare.RepeatCount} for section '{bare.Name}'",
                            song.Location);
                        return Value.Void();
                    }
                    flatRegistry[bare.Name] = target;
                    sectionRefs.Add(new SongSectionRef(bare.Name, bare.RepeatCount));
                }
                else if (elem is SectionCallElement call)
                {
                    var materialized = EvaluateSectionCallToData(call);
                    if (materialized == null)
                        return Value.Void();  // diagnostic already emitted
                    var syntheticName = $"{call.Name}#{callIdx++}";
                    flatRegistry[syntheticName] = materialized;
                    sectionRefs.Add(new SongSectionRef(syntheticName, call.RepeatCount));
                }
            }
        }
        else
        {
            // Pre-Phase-36 path (defensive — parser should always populate Elements now)
            foreach (var sectionRef in song.Sections)
            {
                if (!_context.SectionRegistry.TryGetValue(sectionRef.Name, out var existing))
                {
                    _errorReporter.ReportError(
                        $"Undefined section '{sectionRef.Name}' in song arrangement", song.Location);
                    return Value.Void();
                }
                if (sectionRef.RepeatCount <= 0)
                {
                    _errorReporter.ReportError(
                        $"Repeat count must be positive, got {sectionRef.RepeatCount} for section '{sectionRef.Name}'",
                        song.Location);
                    return Value.Void();
                }
                SectionData? target = null;
                foreach (var s in existing)
                    if (s.Parameters == null) { target = s; break; }
                target ??= existing[existing.Count - 1];
                flatRegistry[sectionRef.Name] = target;
                sectionRefs.Add(new SongSectionRef(sectionRef.Name, sectionRef.RepeatCount));
            }
        }

        var songData = new SongData(sectionRefs, flatRegistry);
        return MusicValue.Song(songData);
    }

    /// <summary>
    /// Phase 36 Plan 36-10 (SECT-01) — dispatches a section call through
    /// OverloadResolver, evaluates the matched section's body under a
    /// synthetic frame with bound parameter values (Pitfall 7 dynamic
    /// scope — the synthetic frame inherits the CALLSITE's MusicalContext,
    /// not the declaration's), and returns the materialized SectionData
    /// (sequences harvested from the body's local variables + bare-expr capture).
    /// Returns <c>null</c> on dispatch failure (diagnostic already emitted).
    /// </summary>
    private SectionData? EvaluateSectionCallToData(SectionCallElement call)
    {
        if (!_context.SectionRegistry.TryGetValue(call.Name, out var candidates))
        {
            _errorReporter.ReportError(
                $"Undefined section '{call.Name}' in song arrangement", call.Location);
            return null;
        }

        // Evaluate positional args
        var posValues = new List<Value>();
        foreach (var argExpr in call.PositionalArgs)
            posValues.Add(Evaluate(argExpr));

        // Evaluate named args
        Dictionary<string, Value>? namedValues = null;
        if (call.NamedArgs != null && call.NamedArgs.Count > 0)
        {
            namedValues = new Dictionary<string, Value>();
            foreach (var (n, vexpr) in call.NamedArgs)
                namedValues[n] = Evaluate(vexpr);
        }

        // OverloadResolver dispatch — scan candidates for a match
        var matched = SectionOverloadDispatch.Resolve(
            call.Name,
            candidates,
            posValues,
            namedValues,
            _context,
            _errorReporter,
            _evaluator,
            call.Location);

        if (matched == null)
            return null;  // diagnostic emitted by dispatcher

        var (section, finalArgValues, bindings) = matched.Value;

        // Synthetic-frame execution
        _context.PushFrame();
        try
        {
            foreach (var (n, v) in bindings)
                _context.DeclareVariable(n, v);

            var musicalContext = _context.GetMusicalContext();

            // Re-run the body. Same shape as Interpreter.ExecuteSectionDeclaration's
            // body-execution block — we mirror it here because the section is
            // re-evaluated per call site with different bindings.
            var bareExprSeqs = new List<SequenceData>();
            // Note: we don't have access to _activeSectionBareExpressions from
            // the ExpressionEvaluator. The section body's bare-expression
            // sequences are captured via the local-variable scan + a manual
            // post-pass.

            // Audit §2.3 — fence the body re-execution against return-flag leakage.
            // This re-execution happens during SONG evaluation, which may itself be
            // inside a user proc. Without save/restore, (a) a return flag leaked from
            // BEFORE this call would make ExecuteStatement's top guard skip the whole
            // section body, and (b) a `return` INSIDE the called section would become
            // the enclosing proc's return value. Save+clear before, restore (and
            // report any in-section return) after — mirroring
            // Interpreter.ExecuteUserFunctionWithCaptures' discipline.
            var interp = _invoker as FlowLang.Interpreter.Interpreter;
            Value? savedReturn = interp?.SaveAndClearReturnValue();
            try
            {
                if (section.Body != null)
                {
                    foreach (var stmt in section.Body)
                    {
                        // Use the parent Interpreter via _context.Invoker indirection;
                        // Since we don't have direct Interpreter ref here, fall through
                        // to a dispatched re-execution by invoking ExecuteStatement
                        // through the ExecutionContext's invoker.
                        _invoker.ExecuteStatement(stmt);

                        if (stmt is ExpressionStatement
                            && _invoker.LastExpressionValue?.Data is SequenceData exprSeq)
                        {
                            bareExprSeqs.Add(exprSeq);
                        }
                    }
                }
            }
            finally
            {
                interp?.RestoreReturnValueAfterSection(savedReturn, call.Location);
            }

            var sequences = new Dictionary<string, SequenceData>();
            foreach (var (n, val) in _context.CurrentFrame.GetLocalVariables())
            {
                if (val.Data is SequenceData seq)
                    sequences[n] = seq;
            }
            for (int i = 0; i < bareExprSeqs.Count; i++)
            {
                if (!sequences.ContainsValue(bareExprSeqs[i]))
                    sequences[$"_anon_{i}"] = bareExprSeqs[i];
            }

            return new SectionData(
                section.Name,
                sequences,
                musicalContext,
                call.Location);
        }
        finally
        {
            _context.PopFrame();
        }
    }
}
