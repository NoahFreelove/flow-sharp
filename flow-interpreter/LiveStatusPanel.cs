using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FlowLang.Diagnostics;

namespace FlowInterpreter;

/// <summary>
/// Severity classification for a Phase 38 advisory. Drives row-4 color in
/// ANSI mode and is otherwise inert in plain-line mode (which writes the
/// advisory body unchanged so the `[live]` / `[osc]` / `[audio-in]` prefix
/// already carries the cue).
/// </summary>
public enum AdvisoryLevel
{
    /// <summary>Informational; no color.</summary>
    Info,

    /// <summary>Success event (e.g. successful hot-swap); green in ANSI mode.</summary>
    Success,

    /// <summary>Recoverable advisory (e.g. file-scope edit, OSC type-tag fallback); yellow.</summary>
    Warning,

    /// <summary>Hard error or aborted operation; red.</summary>
    Error,
}

/// <summary>
/// Display snapshot of a single <c>live { }</c> block — consumed by
/// <see cref="LiveStatusPanel.PublishState"/> row 2. Plan 38-02 fills this
/// from the parsed AST; Plan 38-01 ships the panel surface with the
/// whole-script swap path emitting zero or one display via the sentinel
/// BlockId=0.
/// </summary>
/// <param name="Quantize">Stringified quantize unit (`1bar`, `2bar`, `q`, `h`, etc.).</param>
/// <param name="Line">1-indexed source line where the block opens.</param>
/// <param name="LastSwapBar">Bar number at the most recent swap (0 if never swapped).</param>
/// <param name="SecondsSinceSwap">Wall-clock seconds since the last swap, for the "Xs ago" suffix.</param>
public sealed record LiveBlockDisplay(
    string Quantize,
    int Line,
    int LastSwapBar,
    long SecondsSinceSwap);

/// <summary>
/// Phase 38 LIVE-02 ANSI live status panel + TTY-fallback renderer.
///
/// Two render modes, picked at construction time per the color-disable
/// detection block (UI-SPEC lines 113-118):
///
/// <list type="bullet">
/// <item><description><b>ANSI mode:</b> 4-row panel redrawn in place via
/// <c>\x1b[N;1H</c> cursor moves; row 2 omitted when zero live blocks;
/// row 4 sticky-cleared after 8 seconds.</description></item>
/// <item><description><b>Plain-line mode:</b> one
/// <c>[watch] tempo=N timesig=N/N bar=N voices=N/M</c> line per state
/// change; advisories emit unchanged as <c>[prefix] body</c>.</description></item>
/// </list>
///
/// The class is a top-level <see cref="FlowInterpreter"/>-namespace public
/// surface so flow-lang.Tests/Integration/Phase38 can construct it directly
/// against a <see cref="StringWriter"/> seam.
///
/// Thread-safety: a per-instance lock serializes PublishState / PublishAdvisory
/// against the heartbeat <see cref="Timer"/> tick (UI-SPEC line 169 — 2 Hz redraw
/// cap; tick runs OFF the audio thread per Pitfall #21).
/// </summary>
public sealed class LiveStatusPanel : IDisposable
{
    // ANSI escape sequences per RESEARCH §F lines 808-826.
    private const string AnsiReset = "\x1b[0m";
    private const string AnsiDim = "\x1b[2m";
    private const string AnsiBold = "\x1b[1m";
    private const string AnsiGreen = "\x1b[32m";
    private const string AnsiYellow = "\x1b[33m";
    private const string AnsiRed = "\x1b[31m";
    private const string AnsiCyan = "\x1b[36m";

    // Cursor moves used in real-terminal mode only — the test path writes to
    // a StringWriter and skips cursor relocation (we still emit row content
    // contiguously so tests can scrape it).
    private const string AnsiClearLine = "\x1b[2K";

    // Sticky-advisory clear timeout per UI-SPEC line 158.
    private static readonly TimeSpan StickyAdvisoryClearAfter = TimeSpan.FromSeconds(8);

    private readonly TextWriter _out;
    private readonly bool _isColorEnabled;
    private readonly bool _writesToStdout;
    private readonly object _gate = new();

    private readonly Timer? _heartbeat;

    // Cached last-published state for change detection (plain-line mode emits
    // exactly once per state change per UI-SPEC line 178).
    private double _lastTempo = double.NaN;
    private (int Num, int Den) _lastTimesig = (-1, -1);
    private int _lastBar = -1;
    private int _lastVoices = -1;
    private int _lastPoolSize = -1;

    // Cached last-published blocks so heartbeat redraws have access to row-count.
    // Audit-0609 §5.14: required to compute the correct advisory row position
    // and to trigger a full panel redraw from the heartbeat tick.
    private IReadOnlyList<LiveBlockDisplay> _lastBlocks = Array.Empty<LiveBlockDisplay>();
    private IReadOnlyDictionary<string, int> _lastPerInstrument = new Dictionary<string, int>();

    // Sticky advisory state (row 4).
    private string? _stickyAdvisory;
    private AdvisoryLevel _stickyLevel;
    private DateTime _stickyEmittedUtc;

    private bool _disposed;

    /// <summary>
    /// Constructs a panel writing to <paramref name="out"/> (defaults to
    /// <see cref="Console.Out"/>). When <paramref name="forceTtyMode"/> is
    /// <c>true</c> the panel ignores <see cref="Console.IsOutputRedirected"/>
    /// (so tests redirecting stdout can still exercise the ANSI path). The
    /// <c>NO_COLOR</c> / <c>TERM=dumb</c> / <c>--no-color</c> gates still
    /// suppress color/escapes per UI-SPEC color-disable detection block
    /// (lines 113-118).
    /// </summary>
    /// <param name="out">
    /// Destination writer. <c>null</c> → <see cref="Console.Out"/>. When the
    /// destination IS <see cref="Console.Out"/> and ANSI is enabled, the
    /// panel emits cursor-relocation escapes in addition to row content; for
    /// any other writer (test <see cref="StringWriter"/>) the panel emits
    /// row content only (cursor escapes would be noise in a string buffer).
    /// </param>
    /// <param name="forceTtyMode">
    /// Test seam — when <c>true</c>, bypasses <see cref="Console.IsOutputRedirected"/>
    /// detection. Other color-disable gates still apply.
    /// </param>
    /// <param name="cliArgs">
    /// Optional CLI arg list scanned for <c>--no-color</c>. Defaults to empty
    /// (call site can pass <c>Environment.GetCommandLineArgs()</c>).
    /// </param>
    public LiveStatusPanel(
        TextWriter? @out = null,
        bool forceTtyMode = false,
        IReadOnlyList<string>? cliArgs = null)
    {
        _out = @out ?? Console.Out;
        _writesToStdout = ReferenceEquals(_out, Console.Out);

        // Color-disable detection — copy of UI-SPEC lines 113-118 verbatim,
        // gated by forceTtyMode test seam (NO_COLOR / TERM=dumb / --no-color
        // still win — they apply to real composer use of the redirect too).
        var noColor = Environment.GetEnvironmentVariable("NO_COLOR");
        var term = Environment.GetEnvironmentVariable("TERM");
        bool noColorFlag = cliArgs?.Contains("--no-color") ?? false;
        bool ttyOk = forceTtyMode || !Console.IsOutputRedirected;
        _isColorEnabled = string.IsNullOrEmpty(noColor)
                          && !noColorFlag
                          && ttyOk
                          && term != "dumb";

        // Heartbeat timer ticks at 2 Hz (UI-SPEC line 169) to refresh the
        // "Xs ago" suffix and auto-clear the sticky advisory. Off the audio
        // thread per Pitfall #21. We only spin the timer when we have a
        // real-terminal stdout target — in test-writer mode the redraw is
        // event-driven via PublishState/PublishAdvisory.
        if (_isColorEnabled && _writesToStdout)
        {
            _heartbeat = new Timer(
                _ => OnHeartbeatTick(),
                state: null,
                dueTime: TimeSpan.FromMilliseconds(500),
                period: TimeSpan.FromMilliseconds(500));
        }
    }

    /// <summary>
    /// Publishes a state snapshot. In ANSI mode this triggers a full panel
    /// redraw if state has changed since the last call. In plain-line mode
    /// emits exactly one <c>[watch] tempo=N timesig=N/N bar=N voices=N/M</c>
    /// line per state change (no-op on identical state) per UI-SPEC line 178.
    /// </summary>
    public void PublishState(
        double tempo,
        (int Numerator, int Denominator) timesig,
        int bar,
        IReadOnlyList<LiveBlockDisplay> blocks,
        int activeVoices,
        int poolSize,
        IReadOnlyDictionary<string, int> perInstrumentCount)
    {
        lock (_gate)
        {
            if (_disposed) return;

            bool stateChanged = tempo != _lastTempo
                                || timesig.Numerator != _lastTimesig.Num
                                || timesig.Denominator != _lastTimesig.Den
                                || bar != _lastBar
                                || activeVoices != _lastVoices
                                || poolSize != _lastPoolSize;

            // Update the cached snapshot first so heartbeat ticks see fresh
            // state even if we short-circuit the redraw.
            _lastTempo = tempo;
            _lastTimesig = (timesig.Numerator, timesig.Denominator);
            _lastBar = bar;
            _lastVoices = activeVoices;
            _lastPoolSize = poolSize;
            // Audit-0609 §5.14: cache blocks/perInstrument so heartbeat
            // redraws have access to the current panel layout.
            _lastBlocks = blocks;
            _lastPerInstrument = perInstrumentCount;

            if (_isColorEnabled)
            {
                // ANSI redraw — always redraw on state publish in tests
                // (real heartbeat does change-detection separately).
                RenderAnsiPanel(blocks, perInstrumentCount);
            }
            else if (stateChanged)
            {
                // Plain-line: one line per state change per UI-SPEC line 178.
                _out.WriteLine(
                    $"[watch] tempo={FormatTempo(tempo)} " +
                    $"timesig={timesig.Numerator}/{timesig.Denominator} " +
                    $"bar={bar} " +
                    $"voices={activeVoices}/{poolSize}");
                _out.Flush();
            }
        }
    }

    /// <summary>
    /// Publishes a one-line advisory. In ANSI mode the row 4 sticky updates;
    /// in plain-line mode the body emits to stderr only (house contract:
    /// advisories → stderr, matching D-48-15 and the WarnOnce convention).
    ///
    /// Audit-0609 §5.14 fix: advisories were previously written to stdout
    /// (_out defaults to Console.Out) AND duplicated to stderr via WarnOnce.
    /// Now they are emitted to stderr exclusively; the WarnOnce call is
    /// dropped because WarnOnce itself already writes to stderr, and calling
    /// it from here created a duplicate on first emission.
    /// </summary>
    public void PublishAdvisory(
        string body,
        AdvisoryLevel level,
        string? dedupKey = null)
    {
        lock (_gate)
        {
            if (_disposed) return;

            _stickyAdvisory = body;
            _stickyLevel = level;
            _stickyEmittedUtc = DateTime.UtcNow;

            // ANSI mode: render row 4 only (cheap incremental redraw).
            if (_isColorEnabled)
            {
                WriteAnsiAdvisoryRow(body, level);
            }
            else
            {
                // Plain-line mode: advisories go to stderr per the house contract
                // (D-48-15: "advisories → stderr").
                //
                // Audit-0609 §5.14 fix: the original code wrote to _out (stdout)
                // AND called WarnOnce (stderr) → advisory appeared on both streams.
                // The _out.WriteLine call is removed. WarnOnce is kept because it:
                //  1. Registers the dedupKey in RenderingDiagnostics so test
                //     instrumentation (WasWarnedForTesting / TimeoutRevertTests) works.
                //  2. Suppresses duplicate advisory spam per live-session.
                // When dedupKey is null (single-shot advisories) we dedup by body.
                FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
                    dedupKey ?? body,
                    body);
            }
        }
    }

    /// <summary>
    /// 2 Hz heartbeat — refreshes the "Xs ago" suffix in row 2 and clears
    /// the sticky advisory if the 8-second window has elapsed. Runs on a
    /// dedicated Timer thread (off the audio thread per Pitfall #21).
    ///
    /// Audit-0609 §5.14 fix: the original implementation only set
    /// _stickyAdvisory = null but never repainted, so the cleared advisory
    /// persisted on the terminal until a subsequent PublishState call. Now
    /// a full panel redraw is triggered whenever the advisory is cleared
    /// (which blanks the advisory row) or whenever live blocks exist (to keep
    /// the "Xs ago" suffix current).
    /// </summary>
    private void OnHeartbeatTick()
    {
        bool needsRedraw = false;
        lock (_gate)
        {
            if (_disposed) return;

            // Clear sticky advisory after 8 seconds.
            if (_stickyAdvisory != null
                && (DateTime.UtcNow - _stickyEmittedUtc) >= StickyAdvisoryClearAfter)
            {
                _stickyAdvisory = null;
                needsRedraw = true;
            }

            // Refresh "Xs ago" suffix in the live-blocks row.
            if (_lastBlocks.Count > 0)
            {
                needsRedraw = true;
            }

            if (needsRedraw && _isColorEnabled)
            {
                RenderAnsiPanel(_lastBlocks, _lastPerInstrument);
            }
        }
    }

    /// <summary>
    /// Renders the full ANSI panel (rows 1, 2 [optional], 3, 4 [optional]).
    /// Cursor relocation escapes are emitted ONLY when writing to
    /// <see cref="Console.Out"/>; tests writing to a <see cref="StringWriter"/>
    /// get row content only.
    /// </summary>
    private void RenderAnsiPanel(
        IReadOnlyList<LiveBlockDisplay> blocks,
        IReadOnlyDictionary<string, int> perInstrumentCount)
    {
        var sb = new StringBuilder();

        // Row 1 — Tempo / TimeSig / Bar. Labels dim per Typography table.
        if (_writesToStdout) sb.Append("\x1b[1;1H").Append(AnsiClearLine);
        sb.Append(AnsiDim).Append("Tempo: ").Append(AnsiReset)
          .Append(FormatTempo(_lastTempo)).Append(" BPM")
          .Append(' ').Append(AnsiDim).Append("|").Append(AnsiReset).Append(' ')
          .Append(AnsiDim).Append("TimeSig: ").Append(AnsiReset)
          .Append(_lastTimesig.Num).Append('/').Append(_lastTimesig.Den)
          .Append(' ').Append(AnsiDim).Append("|").Append(AnsiReset).Append(' ')
          .Append(AnsiDim).Append("Bar: ").Append(AnsiReset)
          .Append(_lastBar)
          .Append('\n');

        // Row 2 — Live blocks (omitted entirely if zero per UI-SPEC line 145).
        int currentTerminalRow = 2;
        if (blocks.Count > 0)
        {
            if (_writesToStdout) sb.Append($"\x1b[{currentTerminalRow};1H").Append(AnsiClearLine);
            sb.Append(AnsiDim).Append("Live blocks: ").Append(AnsiReset);
            for (int i = 0; i < blocks.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                var b = blocks[i];
                sb.Append("live ").Append(b.Quantize).Append(" @ L").Append(b.Line)
                  .Append(" (last swap bar ").Append(b.LastSwapBar)
                  .Append(", ").Append(FormatAgo(b.SecondsSinceSwap)).Append(" ago)");
            }
            sb.Append('\n');
            currentTerminalRow++;
        }

        // Row 3 — Voices N/M | instrument breakdown.
        if (_writesToStdout) sb.Append($"\x1b[{currentTerminalRow};1H").Append(AnsiClearLine);
        sb.Append(AnsiDim).Append("Voices: ").Append(AnsiReset)
          .Append(_lastVoices).Append('/').Append(_lastPoolSize);
        if (perInstrumentCount.Count > 0)
        {
            sb.Append(' ').Append(AnsiDim).Append("|").Append(AnsiReset);
            // UI-SPEC line 154: descending count, alphabetic tie-break.
            var ordered = perInstrumentCount
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal);
            foreach (var kv in ordered)
            {
                sb.Append(' ').Append(kv.Key).Append(':').Append(kv.Value);
            }
        }
        sb.Append('\n');
        currentTerminalRow++;

        // Row 4 (or 3 if no blocks row) — Sticky advisory.
        // Audit-0609 §5.14 fix: always emit this row — if the advisory is null
        // we clear the line so stale text from a previous advisory does not
        // persist on the terminal. Previously this block was skipped when null,
        // so the heartbeat's clear of _stickyAdvisory had no visual effect.
        if (_writesToStdout) sb.Append($"\x1b[{currentTerminalRow};1H").Append(AnsiClearLine);
        if (_stickyAdvisory != null)
        {
            sb.Append(LevelToColor(_stickyLevel))
              .Append(_stickyAdvisory)
              .Append(AnsiReset);
        }
        // When _stickyAdvisory == null, AnsiClearLine above already blanked the row.
        sb.Append('\n');

        _out.Write(sb.ToString());
        _out.Flush();
    }

    /// <summary>
    /// Incremental row-4 redraw used by <see cref="PublishAdvisory"/> in
    /// ANSI mode (cheaper than a full panel redraw).
    ///
    /// Audit-0609 §5.14 fix: the original implementation hardcoded row 4
    /// which is wrong when the live-blocks row (row 2) is absent — the
    /// advisory would then render on row 4 instead of row 3, leaving a
    /// blank row between the Voices row and the advisory. Now we compute
    /// the advisory row dynamically from _lastBlocks.Count:
    ///   row 1: Tempo/TimeSig/Bar (always present)
    ///   row 2: Live blocks (present only when _lastBlocks.Count > 0)
    ///   row 3 (or 2 if no blocks): Voices N/M breakdown
    ///   row 4 (or 3 if no blocks): Advisory
    /// </summary>
    private void WriteAnsiAdvisoryRow(string body, AdvisoryLevel level)
    {
        var sb = new StringBuilder();
        if (_writesToStdout)
        {
            // Row layout: 1=header, (2=blocks if present), N=voices, N+1=advisory.
            // _lastBlocks is cached by PublishState so we always have current data.
            int advisoryRow = _lastBlocks.Count > 0 ? 4 : 3;
            sb.Append($"\x1b[{advisoryRow};1H").Append(AnsiClearLine);
        }
        sb.Append(LevelToColor(level)).Append(body).Append(AnsiReset).Append('\n');
        _out.Write(sb.ToString());
        _out.Flush();
    }

    private static string LevelToColor(AdvisoryLevel level) => level switch
    {
        AdvisoryLevel.Success => AnsiGreen,
        AdvisoryLevel.Warning => AnsiYellow,
        AdvisoryLevel.Error => AnsiRed,
        _ => string.Empty,
    };

    /// <summary>
    /// Tempo format per UI-SPEC line 140: integer when whole, single decimal
    /// otherwise (e.g. <c>120</c> vs. <c>92.5</c>).
    /// </summary>
    private static string FormatTempo(double tempo)
    {
        if (double.IsNaN(tempo)) return "—";
        if (Math.Abs(tempo - Math.Round(tempo)) < 1e-9)
        {
            return ((int)Math.Round(tempo)).ToString();
        }
        return tempo.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// "ago" format per UI-SPEC line 148: <c>&lt;60s</c> → <c>Ns</c>,
    /// <c>&lt;60m</c> → <c>NmNs</c>, <c>≥60m</c> → <c>NhNmNs</c>.
    /// </summary>
    private static string FormatAgo(long seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        if (seconds < 3600)
        {
            long m = seconds / 60;
            long s = seconds % 60;
            return $"{m}m{s}s";
        }
        long h = seconds / 3600;
        long mRem = (seconds % 3600) / 60;
        long sRem = seconds % 60;
        return $"{h}h{mRem}m{sRem}s";
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _heartbeat?.Dispose();
    }
}
