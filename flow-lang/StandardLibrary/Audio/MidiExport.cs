using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Flow.Music.IO;
using FlowLang.Music;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Flow's <c>writeMidi</c> binding. The evaluated song is compiled to a detached
/// composition snapshot and written by <see cref="MidiCompositionExporter"/>, so
/// Flow and native hosts share one MIDI timing/routing contract (see
/// docs/decisions/2026-09-24-composition-snapshot-boundary.md).
/// </summary>
public static class MidiExport
{

    // -----------------------------------------------------------------------
    // §5.4 in-memory MIDI capture sink (D-48-17 / D-48-18)
    //
    // ExportMidiInternal always serialises the MidiFile into a MemoryStream
    // and stores the resulting bytes here BEFORE any file-system write.  On
    // the Web target the file path points into the inert Emscripten VFS and
    // the playground never downloads from there; this side-channel lets
    // WasmEntry.RunFromJs drain the bytes into RunResult.Midi after each run.
    //
    // Thread-safety: Mono-WASM is single-threaded, so a plain static field
    // is safe in browser.  On Desktop (multi-threaded test runner) the field
    // is [ThreadStatic], giving each test thread its own slot — two-run
    // cmp-clean contract is preserved because the slot is drained by
    // DrainInMemorySink() at the start of every RunFromJs call.
    // -----------------------------------------------------------------------
    // Session-owned (RenderServices.LastMidiBytes); per thread outside a session.
    [ThreadStatic]
    private static byte[]? _fallbackSink;

    private static byte[]? InMemorySink
    {
        get => RenderServices.Current is { } render ? render.LastMidiBytes : _fallbackSink;
        set
        {
            if (RenderServices.Current is { } render) render.LastMidiBytes = value;
            else _fallbackSink = value;
        }
    }

    /// <summary>
    /// Returns the SMF bytes captured by the most recent <c>writeMidi</c>
    /// call on this thread and clears the slot.  Returns <c>null</c> if no
    /// <c>writeMidi</c> call has been made since the last drain.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="FlowLang.Runtime.WasmEntry.RunFromJs"/> after
    /// <see cref="FlowLang.Core.FlowEngine.Execute"/> returns so that the
    /// bytes populate <see cref="FlowLang.Runtime.RunResult.Midi"/> (D-48-18).
    /// Also called at the START of <c>RunFromJs</c> to clear any stale bytes
    /// from a previous run — two-run cmp-clean: same source with
    /// <c>writeMidi</c> → byte-identical <c>RunResult.Midi</c>.
    /// </remarks>
    public static byte[]? DrainInMemorySink(RenderServices? render = null)
    {
        if (render is not null)
        {
            var own = render.LastMidiBytes;
            render.LastMidiBytes = null;
            return own;
        }
        var bytes = InMemorySink;
        InMemorySink = null;
        return bytes;
    }


    private const int TicksPerQuarterNote = 480;

    /// <summary>
    /// TUP-06 / CONTEXT D-05 / D-USER-E: maximum TPQN supported by Flow's MIDI export.
    /// Songs whose tuplet denominator LCM forces TPQN above this cap raise a clear
    /// composer-facing error — no DAW imports correctly above this in field testing
    /// (per .planning/research/SUMMARY.md). 32767 is the SMF spec hard limit.
    /// </summary>
    private const int MaxTpqn = 9600;

    /// <summary>
    /// Recursive Euclidean GCD. Mirrors Phase 18 Fraction.cs idiom for stylistic
    /// consistency. Used by Lcm to compute requiredTPQN.
    /// </summary>
    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    /// <summary>
    /// Lcm(a, b) = a × b / Gcd(a, b). Two-line helper next to its sole caller.
    /// </summary>
    private static int Lcm(int a, int b) => a / Gcd(a, b) * b;

    /// <summary>
    /// MusicXML <c>divisions</c> (the MIDI export now resolves its own TPQN from the
    /// snapshot with the same formula over placed sections).
    /// TUP-06: pre-export pass over the Song collecting tuplet denominators from
    /// MusicalNoteData.DurationFraction values. Computes requiredTPQN = LCM(480,
    /// 2 × union(denoms)) per CONTEXT D-05. When zero tuplets are present (no
    /// note has DurationFraction), returns 480 unchanged (CONTEXT D-07 structural
    /// preservation of Phase 18 byte-identical contract for non-tuplet songs).
    ///
    /// When requiredTPQN exceeds MaxTpqn (9600), raises an InvalidOperationException
    /// with the LOCKED message format from CONTEXT D-06. The error fires BEFORE
    /// any DryWetMidi MidiFile allocation or disk I/O — atomic, no partial export.
    /// </summary>
    internal static int ComputeRequiredTpqn(SongData song)
    {
        var denominators = new HashSet<int>();
        foreach (var section in song.SectionRegistry.Values)
            foreach (var sequence in section.Sequences.Values)
                foreach (var bar in sequence.Bars)
                    foreach (var note in bar.MusicalNotes)
                        if (note.DurationFraction.HasValue)
                            denominators.Add(note.DurationFraction.Value.Denom);

        // CONTEXT D-07: zero tuplets → TPQN stays at 480 (Phase 18 byte-identical contract)
        if (denominators.Count == 0)
            return TicksPerQuarterNote;

        int requiredTpqn = TicksPerQuarterNote;
        foreach (var d in denominators)
            requiredTpqn = Lcm(requiredTpqn, 2 * d);

        if (requiredTpqn > MaxTpqn)
        {
            var sortedDenoms = denominators.OrderBy(x => x).ToArray();
            throw new InvalidOperationException(
                $"MIDI export requires TPQN={requiredTpqn}, exceeds cap {MaxTpqn} (locked v1.3 D-05). " +
                $"Tuplet ratios in this song: [{string.Join(", ", sortedDenoms)}]");
        }
        return requiredTpqn;
    }

    /// <summary>
    /// Phase 33 D-15 — strips the <c>sampler:</c> prefix from a sequence name
    /// so the GM-program lookup AND the SequenceTrackName meta-event both
    /// see the canonical instrument name (e.g. <c>"sampler:violin"</c> →
    /// <c>"violin"</c>).
    ///
    /// Phase 39 D-39-20 — implementation moved to
    /// <see cref="FlowLang.StandardLibrary.Notation.InstrumentRouting.StripSamplerPrefix"/>
    /// as the single source of truth across MIDI / MusicXML / LilyPond emit
    /// paths. This wrapper preserves the public method signature for
    /// backwards compat with existing callers (test fixtures, Phase 33 SFZ
    /// dispatcher).
    /// </summary>
    public static string StripSamplerPrefix(string name)
        => FlowLang.StandardLibrary.Notation.InstrumentRouting.StripSamplerPrefix(name);

    /// <summary>
    /// Phase 28 SPEC-6 + Phase 33 D-15 / D-16: maps a Sequence's name to a
    /// (GM program, MIDI channel) pair using case-insensitive prefix matching.
    /// Drum sequences route to channel 9 (GM percussion). All other instrument
    /// prefixes default to channel 0. Unrecognized names default to GM 0
    /// (acoustic grand piano), channel 0.
    ///
    /// Phase 33 D-15: the <c>sampler:</c> prefix is STRIPPED at the top so
    /// <c>sampler:violin</c> routes the same as <c>violin</c> — composers who
    /// use the SFZ pipeline get sensible GM programs in the exported .mid
    /// file even when the receiving DAW doesn't have the SFZ samples
    /// installed. Pitfall 6: this strip MUST be the first statement after
    /// the empty-name check or the prefix would bleed through and route
    /// every sampler instrument to the GM-0 fallback.
    ///
    /// Phase 33 D-16: 12 new entries — violin (40), viola (41), cello (42),
    /// contrabass (43), oboe (68), clarinet (71), bassoon (70), horn (60),
    /// trombone (57), tuba (58), timpani (47, ch 9), choir (52), harp (46),
    /// guitar (24), harpsichord (6), celeste (8). The new <c>horn</c> entry
    /// MUST come BEFORE the existing <c>brass</c> check because the Phase 28
    /// brass→56 entry historically also matched <c>horn*</c> sequences;
    /// Phase 33 reassigns <c>horn → 60</c> (French horn) per D-16.
    ///
    /// Mapping rules (Phase 28 + Phase 33 additions, ordering significant):
    ///   sampler:* → strip prefix, then continue
    ///   Phase 33 (more-specific-first):
    ///     violin* → (40, 0), viola* → (41, 0), cello* → (42, 0),
    ///     contrabass* → (43, 0), oboe* → (68, 0), clarinet* → (71, 0),
    ///     bassoon* → (70, 0), horn* → (60, 0)  [BEFORE brass],
    ///     trombone* → (57, 0), tuba* → (58, 0),
    ///     timpani* → (47, 9)  [channel 9 = percussion],
    ///     choir* → (52, 0), harp* → (46, 0), guitar* → (24, 0),
    ///     harpsichord* → (6, 0), celeste* → (8, 0)
    ///   Phase 28 (UNCHANGED):
    ///     piano* → (0, 0), brass* → (56, 0), sax* → (65, 0),
    ///     flute* → (73, 0), string* → (48, 0), organ* → (19, 0),
    ///     bell* → (14, 0), drum* → (0, 9), default → (0, 0)
    /// </summary>
    /// <summary>
    /// Phase 39 D-39-20 — implementation moved to
    /// <see cref="FlowLang.StandardLibrary.Notation.InstrumentRouting.ResolveGmProgram"/>
    /// as the single source of truth across MIDI / MusicXML / LilyPond emit
    /// paths. The 17-entry routing table + ordering contract lives there;
    /// this wrapper preserves the public method signature so existing
    /// callers (SongRenderer SFZ dispatcher, test fixtures, Phase 28 + 33
    /// byte-identical contracts) continue to resolve identically.
    /// </summary>
    public static (int gmProgram, int channel) ResolveGmProgram(string seqName)
        => FlowLang.StandardLibrary.Notation.InstrumentRouting.ResolveGmProgram(seqName);

    /// <summary>
    /// Key signature lookup: Flow key string -> (sharps/flats, minor flag).
    /// MIDI encodes sharps as positive, flats as negative; minor = 1.
    /// </summary>
    internal static IReadOnlyDictionary<string, (sbyte sharpsFlats, byte minor)> KeySignatureMap =>
        Flow.Music.IO.KeySignatures.Map;

    /// <summary>
    /// Phase 23 Plan 23-03 Task 2 + Phase 32 D-12 / Pitfall 6: context-dependent registration
    /// for <c>writeMidi</c>. Mirrors <see cref="Harmony.HarmonyFunctions.RegisterContextDependent"/>
    /// shape — closure over <see cref="FlowLang.Runtime.ExecutionContext"/> so
    /// <see cref="WriteMidi(IReadOnlyList{Value}, FlowLang.Runtime.ExecutionContext)"/>
    /// can read <see cref="Music.MusicalContext.ActiveTuning"/> at call time and emit the
    /// D-13 one-shot warning when EITHER the resolved <see cref="RenderTuning.System"/> is
    /// non-EQ OR a custom Scala tuning is active (<c>Custom != null</c>). MIDI bytes
    /// themselves are UNCHANGED — still 12-TET.
    /// </summary>
    public static void RegisterContextDependent(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        var writeMidiSignature = new FunctionSignature("writeMidi", [StringType.Instance, SongType.Instance],
            ParameterNames: ["path", "song"]);
        registry.Register("writeMidi", writeMidiSignature, args => WriteMidi(args, context));
    }

    /// <summary>
    /// Flow-callable entry point: writeMidi(String filepath, Song song) -> Void.
    /// Context-free overload preserved for backwards compat (e.g., direct test invocation,
    /// proxy paths). Phase 23: registration migrated to the 2-arg overload below so writeMidi
    /// can emit the D-13 non-12-TET advisory warning.
    /// </summary>
    public static Value WriteMidi(IReadOnlyList<Value> args)
    {
        string filepath = args[0].As<string>();
        var song = args[1].As<SongData>();

        if (string.IsNullOrWhiteSpace(filepath))
            throw new ArgumentException("MIDI filepath cannot be null or empty");

        ExportMidiInternal(filepath, song);
        return Value.Void();
    }

    /// <summary>
    /// Phase 23 Plan 23-03 Task 2 / D-13: context-aware overload. Emits a one-shot
    /// stderr warning when called under non-12-TET tuning so composers know that
    /// faithful microtonal MIDI export (per-channel pitch-bend) is a v1.6+ backlog item.
    /// MIDI bytes are UNCHANGED — still 12-TET output. The warning is purely advisory.
    /// </summary>
    public static Value WriteMidi(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var musicalCtx = context.GetMusicalContext();
        // Phase 32 D-12 + Pitfall 6: predicate fires under EITHER a non-EQ Phase 23
        // system OR a custom Scala tuning (RenderTuning.Custom != null). The MIDI bytes
        // themselves remain 12-TET — the advisory is purely informational so composers
        // know microtonal MIDI export with per-channel pitch-bend is a future deliverable.
        var activeTuning = musicalCtx?.ActiveTuning ?? RenderTuning.Default;
        if (activeTuning.Custom != null || activeTuning.System != TuningSystem.EqualTemperament)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (context.CallerStrictMode)
            {
                context.ErrorReporter.ReportError(
                    $"[strict] [midi] velocity floor applied — tuning != equalTemperament, MIDI export emits 12-TET pitches without pitch-bend at {context.CurrentCallSite}",
                    context.CurrentCallSite);
            }
            else
            {
                RenderingDiagnostics.WarnOnce(
                    "writemidi-non-equal-temperament",
                    "[midi] tuning != equalTemperament; MIDI export emits 12-TET pitches without pitch-bend (faithful microtonal MIDI is a v1.6+ backlog item)");
            }
        }
        return WriteMidi(args);
    }

    private static void ExportMidiInternal(string filepath, SongData song)
    {
        // Build and validate in memory first: tuplet-resolution or range failures
        // leave no partial file on disk.
        var bytes = MidiCompositionExporter.ToBytes(CompositionCompiler.Compile(song, "writeMidi"));
        // §5.4 — capture SMF bytes for WasmEntry.RunFromJs before any file-system write.
        InMemorySink = bytes;
        File.WriteAllBytes(filepath, bytes);
    }
}
