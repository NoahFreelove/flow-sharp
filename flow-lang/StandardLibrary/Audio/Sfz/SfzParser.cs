using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FlowLang.Diagnostics;

namespace FlowLang.StandardLibrary.Audio.Sfz;

/// <summary>
/// Phase 33 Plan 33-04 — hand-rolled SFZ-format parser. Mirrors Phase 32's
/// <see cref="FlowLang.StandardLibrary.Audio.Tuning.ScalaParser"/> shape:
/// single-pass line walker, explicit numeric posture
/// (<see cref="CultureInfo.InvariantCulture"/> + a NumberStyles mask that
/// excludes <c>AllowExponent</c> / <c>AllowThousands</c>), bounded loop with
/// <see cref="MaxRegionCount"/> DoS guard mirroring
/// <c>ScalaParser.MaxStepCount</c>.
///
/// <para><b>Whitelist — 20 opcodes</b> (Phase 37 SAMP-01 + SAMP-02 extends
/// Phase 33's 14-entry set with 6 new opcodes: round-robin pair
/// <c>seq_position</c>/<c>seq_length</c>, velocity-crossfade quad
/// <c>xfin_lovel</c>/<c>xfin_hivel</c>/<c>xfout_lovel</c>/<c>xfout_hivel</c>).
/// Phase 33 baseline (extended from SPEC-3's "13" per the Plan 33-01 VSCO-CE
/// 1.1.0 <c>&lt;control&gt;</c> audit, which found 15/15 probed VSCO patches
/// declare <c>default_path=</c> as their first header):
/// <c>sample</c>, <c>lokey</c>, <c>hikey</c>, <c>pitch_keycenter</c>,
/// <c>lovel</c>, <c>hivel</c>, <c>loop_mode</c>, <c>loop_start</c>,
/// <c>loop_end</c>, <c>ampeg_attack</c>, <c>ampeg_release</c>, <c>volume</c>,
/// <c>pan</c>, <c>default_path</c> (control-only). The set uses
/// <see cref="StringComparer.Ordinal"/> — case differences and unicode
/// look-alikes are rejected per T-33-OPCODE-01.</para>
///
/// <para><b>Headers — 4 types</b>: <c>&lt;control&gt;</c> (file-scope; supplies
/// <c>default_path=</c>), <c>&lt;global&gt;</c>, <c>&lt;group&gt;</c>,
/// <c>&lt;region&gt;</c>. Inheritance is flattened AT PARSE TIME per
/// CONTEXT § Claude's Discretion — the runtime never traverses headers.</para>
///
/// <para><b>Charitable interpretation</b>: unrecognized opcodes are silently
/// ignored and emit a one-shot stderr advisory via
/// <see cref="RenderingDiagnostics.WarnOnce"/>, keyed on
/// <c>sfz:opcode:{patchDescription}:{opcodeName}</c>. Iterative composer
/// workflows don't flood stderr on re-load.</para>
///
/// <para><b>WarnOnce contract</b>: this parser uses the global per-process
/// sentinel set owned by <see cref="RenderingDiagnostics"/>. The v1.4 signature
/// dropped the planned <c>diagnosticsSink</c> parameter — it had no consumer.
/// Test isolation lives in callers via
/// <see cref="RenderingDiagnostics.ResetForTesting"/>.</para>
///
/// <para><b>Pitfall 7</b>: SFZ <c>pan</c> is <c>[-100, +100]</c>; Flow's range
/// is <c>[-1.0, +1.0]</c>. Conversion <c>÷ 100.0</c> is applied at parse time.</para>
///
/// <para><b>Pitfall 8</b>: SFZ <c>volume</c> is dB; Flow stores linear
/// amplitude. Conversion <c>Math.Pow(10, dB / 20)</c> is applied at parse time.</para>
///
/// <para><b>D-02 last-declared-wins</b>: the <see cref="SfzData.Grid"/> is
/// built by iterating <see cref="SfzData.Regions"/> in declaration order and
/// writing <c>grid[k, v] = region</c> for every covered cell — later writes
/// overwrite earlier ones structurally.</para>
/// </summary>
public static class SfzParser
{
    /// <summary>
    /// DoS guard mirroring Phase 32 <c>ScalaParser.MaxStepCount = 10000</c>
    /// (T-33-PARSE-01). The largest known orchestral SFZ libraries
    /// (Sonatina, Salamander, VSCO-CE) ship under 2000 regions per patch.
    /// </summary>
    public const int MaxRegionCount = 10000;

    /// <summary>
    /// NumberStyles for floating-point opcode values
    /// (<c>ampeg_attack</c>, <c>ampeg_release</c>, <c>volume</c>, <c>pan</c>).
    /// Excludes <c>AllowExponent</c> and <c>AllowThousands</c> per T-33-NUM-01
    /// + the Phase 32 D-18 precedent — rejects <c>1.5e2</c> and <c>100,5</c>.
    /// </summary>
    private const NumberStyles FloatStyle =
        NumberStyles.Float & ~NumberStyles.AllowExponent & ~NumberStyles.AllowThousands;

    /// <summary>
    /// 20-opcode whitelist — Phase 33's 14 plus the Phase 37 SAMP-01/02 six
    /// (round-robin pair + velocity-crossfade quad). Case-sensitive Ordinal
    /// compare per T-33-OPCODE-01 — rejects unicode tricks and case-fold
    /// variants.
    /// </summary>
    private static readonly HashSet<string> KnownOpcodes = new(StringComparer.Ordinal)
    {
        "sample",
        "lokey",
        "hikey",
        "pitch_keycenter",
        "lovel",
        "hivel",
        "loop_mode",
        "loop_start",
        "loop_end",
        "ampeg_attack",
        "ampeg_release",
        "volume",
        "pan",
        "default_path",
        // Phase 37 SAMP-01 (RESEARCH §Pattern 5) — round-robin pair.
        "seq_position",
        "seq_length",
        // Phase 37 SAMP-02 (RESEARCH §Pattern 6) — velocity-crossfade quad.
        "xfin_lovel",
        "xfin_hivel",
        "xfout_lovel",
        "xfout_hivel",
    };

    /// <summary>
    /// Parse an SFZ file's content into a fully-flattened
    /// <see cref="SfzData"/>: regions list (declaration order preserved),
    /// 128×128 <c>(pitch, velocity)</c> grid, sorted-by-pitch index for
    /// nearest-pitch fallback. Header inheritance + dB→linear + pan
    /// normalization + default_path cascade + backslash → OS-separator
    /// normalisation are all applied here.
    ///
    /// <para>Diagnostics (unknown opcodes, malformed numerics, unknown
    /// <c>loop_mode</c> values) route through the global
    /// <see cref="RenderingDiagnostics.WarnOnce"/> sentinel set —
    /// per-process, per-key dedup. <paramref name="patchDescription"/>
    /// disambiguates patches in the sentinel key.</para>
    /// </summary>
    /// <param name="content">Raw .sfz file content (newline-delimited).</param>
    /// <param name="filePath">Origin path — used to compute
    /// <see cref="SfzData.BasePath"/> + threaded into
    /// <see cref="SfzParseException"/> diagnostics.</param>
    /// <param name="patchDescription">Used in the WarnOnce sentinel key
    /// (so two patches with the same misspelled opcode each fire once
    /// rather than the first one suppressing the second).</param>
    public static SfzData Parse(string content, string filePath, string patchDescription)
    {
        var lines = content.Split('\n');

        // Inheritance accumulators. Each is a key→value opcode map populated
        // as we walk the file. Headers reset the appropriate accumulator;
        // opcodes following a header land in the most-recently-selected
        // accumulator.
        var controlOpcodes = new Dictionary<string, string>(StringComparer.Ordinal);
        var globalOpcodes = new Dictionary<string, string>(StringComparer.Ordinal);
        var groupOpcodes = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string>? regionOpcodes = null;

        var regions = new List<SfzRegion>();

        // Which accumulator does the next bareword opcode land in?
        var target = HeaderKind.None;

        string basePath = Path.GetDirectoryName(filePath) ?? "";
        string? description = null;

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            int lineNumber = lineIndex + 1;
            var raw = StripCr(lines[lineIndex]);

            // Strip line comments. We use the simple "first //" rule per the
            // 13-opcode subset where sample paths are barewords (no quotes).
            int comment = raw.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0) raw = raw[..comment];

            var trimmed = raw.Trim();
            if (trimmed.Length == 0) continue;

            // Capture the first non-comment line as Description (per
            // Plan 33-04 spec: "the first non-comment non-blank line if
            // present and not a header"). Note: a header line still moves
            // us into a state — description simply stays null in that case.
            if (description is null && !LooksLikeHeader(trimmed))
            {
                description = trimmed;
                // Fall through — the description line might still contain
                // opcodes, but in practice for SFZ it doesn't. Still parse it
                // as an opcode line below for charitable interpretation.
            }

            // Walk the trimmed line token-by-token. Whitespace separates
            // tokens; a token starting with '<' opens a header.
            int cursor = 0;
            while (cursor < trimmed.Length)
            {
                // Skip leading whitespace.
                while (cursor < trimmed.Length && IsSpaceOrTab(trimmed[cursor])) cursor++;
                if (cursor >= trimmed.Length) break;

                if (trimmed[cursor] == '<')
                {
                    // Read header up to '>'.
                    int hEnd = trimmed.IndexOf('>', cursor);
                    if (hEnd < 0)
                    {
                        throw new SfzParseException(
                            filePath, lineNumber, cursor + 1,
                            "closing '>' for SFZ header",
                            trimmed[cursor..]);
                    }
                    var header = trimmed[(cursor + 1)..hEnd];
                    cursor = hEnd + 1;

                    // Before entering a new region, flush the pending region.
                    if (target == HeaderKind.Region && regionOpcodes is not null)
                    {
                        regions.Add(BuildRegion(
                            regionOpcodes, controlOpcodes, basePath, patchDescription));
                        if (regions.Count > MaxRegionCount)
                        {
                            throw new SfzParseException(
                                filePath, lineNumber, 1,
                                "region count <= " + MaxRegionCount,
                                regions.Count.ToString(CultureInfo.InvariantCulture));
                        }
                    }

                    switch (header)
                    {
                        case "control":
                            target = HeaderKind.Control;
                            controlOpcodes.Clear();
                            // <control> is file-scope; clearing <global>/<group>
                            // is NOT desired (they may legitimately appear after).
                            break;
                        case "global":
                            target = HeaderKind.Global;
                            globalOpcodes.Clear();
                            groupOpcodes.Clear();
                            regionOpcodes = null;
                            break;
                        case "group":
                            target = HeaderKind.Group;
                            groupOpcodes.Clear();
                            regionOpcodes = null;
                            break;
                        case "region":
                            target = HeaderKind.Region;
                            // Start a fresh region with inherited values.
                            regionOpcodes = new Dictionary<string, string>(StringComparer.Ordinal);
                            foreach (var kv in globalOpcodes) regionOpcodes[kv.Key] = kv.Value;
                            foreach (var kv in groupOpcodes) regionOpcodes[kv.Key] = kv.Value;
                            // Early DoS guard — if we have already accumulated
                            // MaxRegionCount completed regions, opening one more
                            // would push us past the cap on flush.
                            if (regions.Count >= MaxRegionCount)
                            {
                                throw new SfzParseException(
                                    filePath, lineNumber, 1,
                                    "region count <= " + MaxRegionCount,
                                    (regions.Count + 1).ToString(CultureInfo.InvariantCulture));
                            }
                            break;
                        default:
                            // Unknown header — silently advisory + skip.
                            RenderingDiagnostics.WarnOnce(
                                $"sfz:header:{patchDescription}:{header}",
                                $"[sfz] unrecognized header '<{header}>' in '{patchDescription}' — ignoring");
                            target = HeaderKind.None;
                            break;
                    }
                    continue;
                }

                // Read an opcode token: key=value, where value is the rest of
                // the line up to the next whitespace OR end-of-line. SFZ allows
                // values to contain spaces (e.g. `default_path=Solo Violin\`),
                // BUT the SFZ convention is that an opcode terminator is the
                // next `key=` pattern OR end-of-line. We approximate this by
                // taking value-up-to-whitespace-followed-by-key= or EOL.

                // Read key (up to '=').
                int eq = trimmed.IndexOf('=', cursor);
                if (eq < 0)
                {
                    // No '=' on this line — the remainder is garbage / advisory.
                    var garbage = trimmed[cursor..].TrimEnd();
                    if (garbage.Length > 0)
                    {
                        RenderingDiagnostics.WarnOnce(
                            $"sfz:syntax:{patchDescription}:{garbage}",
                            $"[sfz] unrecognized token '{garbage}' in '{patchDescription}' — ignoring");
                    }
                    break;
                }
                var key = trimmed[cursor..eq].Trim();
                cursor = eq + 1;

                // Read value: scan until we find a candidate `nextKey=` pattern
                // (whitespace followed by an identifier-then-`=`) OR EOL.
                int valStart = cursor;
                int valEnd = trimmed.Length;
                while (cursor < trimmed.Length)
                {
                    if (IsSpaceOrTab(trimmed[cursor]))
                    {
                        // Peek: is the next non-whitespace token a `key=` pattern?
                        int peek = cursor + 1;
                        while (peek < trimmed.Length && IsSpaceOrTab(trimmed[peek])) peek++;
                        // A key is at least one identifier char and an '=' must
                        // follow somewhere before the next whitespace.
                        int peekEnd = peek;
                        while (peekEnd < trimmed.Length && !IsSpaceOrTab(trimmed[peekEnd])) peekEnd++;
                        var nextTok = trimmed[peek..peekEnd];
                        if (nextTok.Contains('=') && IsIdentifierStart(trimmed, peek))
                        {
                            // Found a next opcode — the value ends here.
                            valEnd = cursor;
                            break;
                        }
                        // Whitespace inside value — fall through and consume it.
                    }
                    else if (trimmed[cursor] == '<')
                    {
                        valEnd = cursor;
                        break;
                    }
                    cursor++;
                }
                if (valEnd == trimmed.Length) valEnd = cursor;
                var value = trimmed[valStart..valEnd].TrimEnd();

                // Route key=value into the active accumulator.
                if (!KnownOpcodes.Contains(key))
                {
                    RenderingDiagnostics.WarnOnce(
                        $"sfz:opcode:{patchDescription}:{key}",
                        $"[sfz] unrecognized opcode '{key}' in '{patchDescription}' — ignoring");
                    continue;
                }

                // default_path is control-only. Any other location → advisory.
                if (key == "default_path" && target != HeaderKind.Control)
                {
                    RenderingDiagnostics.WarnOnce(
                        $"sfz:opcode_misplaced:{patchDescription}:default_path",
                        $"[sfz] 'default_path' only valid inside <control>; ignoring stray occurrence in '{patchDescription}'");
                    continue;
                }

                switch (target)
                {
                    case HeaderKind.Control:
                        controlOpcodes[key] = value;
                        break;
                    case HeaderKind.Global:
                        globalOpcodes[key] = value;
                        break;
                    case HeaderKind.Group:
                        groupOpcodes[key] = value;
                        break;
                    case HeaderKind.Region:
                        if (regionOpcodes is null) break;  // defensive
                        regionOpcodes[key] = value;
                        break;
                    default:
                        // Opcode encountered with no active header — charitable advisory.
                        RenderingDiagnostics.WarnOnce(
                            $"sfz:orphan_opcode:{patchDescription}:{key}",
                            $"[sfz] opcode '{key}' outside any header in '{patchDescription}' — ignoring");
                        break;
                }
            }
        }

        // Flush trailing region.
        if (target == HeaderKind.Region && regionOpcodes is not null)
        {
            regions.Add(BuildRegion(regionOpcodes, controlOpcodes, basePath, patchDescription));
            if (regions.Count > MaxRegionCount)
            {
                throw new SfzParseException(
                    filePath, lines.Length, 1,
                    "region count <= " + MaxRegionCount,
                    regions.Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        // Build the (pitch, velocity) lookup grid. D-02 last-declared-wins is
        // structurally enforced by iterating in declaration order: later
        // writes overwrite earlier ones.
        var grid = new SfzRegion?[128, 128];
        foreach (var region in regions)
        {
            int kLo = Math.Max(0, region.LoKey);
            int kHi = Math.Min(127, region.HiKey);
            int vLo = Math.Max(0, region.LoVel);
            int vHi = Math.Min(127, region.HiVel);
            for (int k = kLo; k <= kHi; k++)
            {
                for (int v = vLo; v <= vHi; v++)
                {
                    grid[k, v] = region;
                }
            }
        }

        // SortedByPitch: ascending unique pitches with any region coverage.
        var pitchSet = new HashSet<int>();
        for (int k = 0; k < 128; k++)
        {
            for (int v = 0; v < 128; v++)
            {
                if (grid[k, v] is not null) { pitchSet.Add(k); break; }
            }
        }
        var sortedByPitch = new int[pitchSet.Count];
        int idx = 0;
        foreach (var p in pitchSet) sortedByPitch[idx++] = p;
        Array.Sort(sortedByPitch);

        // Description fallback: if no non-header non-comment line found, use
        // the filename.
        description ??= Path.GetFileName(filePath);

        return new SfzData(description, basePath, regions, grid, sortedByPitch);
    }

    // ----- internal helpers ---------------------------------------------

    private enum HeaderKind { None, Control, Global, Group, Region }

    private static bool LooksLikeHeader(string s)
        => s.Length >= 2 && s[0] == '<';

    private static bool IsSpaceOrTab(char c) => c == ' ' || c == '\t';

    private static bool IsIdentifierStart(string s, int i)
    {
        if (i < 0 || i >= s.Length) return false;
        var c = s[i];
        return char.IsLetter(c) || c == '_';
    }

    /// <summary>Trim a trailing carriage return so we handle both LF and CRLF.</summary>
    private static string StripCr(string line)
        => line.Length > 0 && line[^1] == '\r' ? line[..^1] : line;

    /// <summary>
    /// Build one fully-flattened <see cref="SfzRegion"/> from the merged
    /// opcode dictionary. Applies defaults per the SFZ spec, the dB→linear
    /// conversion (Pitfall 8), pan normalisation (Pitfall 7), and the
    /// <c>default_path</c> cascade (VSCO-CONTROL-DECISION FOUND).
    /// </summary>
    private static SfzRegion BuildRegion(
        Dictionary<string, string> region,
        Dictionary<string, string> control,
        string basePath,
        string patchDescription)
    {
        // sample — required-ish (we accept missing; SamplePath = empty string
        // and downstream cache fails on lookup).
        string sampleRaw = region.TryGetValue("sample", out var s) ? s : "";

        // Resolve sample path: if <control> default_path is set, prepend it.
        // Normalise Windows backslashes to the OS separator at the same time.
        string samplePath;
        if (control.TryGetValue("default_path", out var dp) && !string.IsNullOrEmpty(dp))
        {
            var normDp = NormaliseSeparators(dp);
            var normSample = NormaliseSeparators(sampleRaw);
            // Path.Combine handles trailing separator on default_path.
            samplePath = Path.Combine(normDp, normSample);
        }
        else
        {
            samplePath = NormaliseSeparators(sampleRaw);
        }

        // Numeric opcodes — strict parse with charitable fallback to spec
        // default + WarnOnce advisory.
        int pitchKeycenter = ReadInt(region, "pitch_keycenter", 60, patchDescription);
        int loKey = ReadInt(region, "lokey", 0, patchDescription);
        int hiKey = ReadInt(region, "hikey", 127, patchDescription);
        int loVel = ReadInt(region, "lovel", 1, patchDescription);
        int hiVel = ReadInt(region, "hivel", 127, patchDescription);
        int loopStart = ReadInt(region, "loop_start", 0, patchDescription);
        int loopEnd = ReadInt(region, "loop_end", 0, patchDescription);
        double ampegAttack = ReadDouble(region, "ampeg_attack", 0.0, patchDescription);
        double ampegRelease = ReadDouble(region, "ampeg_release", 0.001, patchDescription);
        double volumeDb = ReadDouble(region, "volume", 0.0, patchDescription);
        double panSfz = ReadDouble(region, "pan", 0.0, patchDescription);

        // Pitfall 8: dB → linear amplitude.
        double volumeLinear = Math.Pow(10.0, volumeDb / 20.0);
        // Pitfall 7: SFZ pan [-100, +100] → Flow pan [-1.0, +1.0].
        double panFlow = panSfz / 100.0;

        // Phase 37 SAMP-01 round-robin opcodes (RESEARCH §Pattern 5).
        // Sentinel defaults (1, 1) preserve Phase 33 behavior when absent
        // — a 1-alternate "group" is functionally a plain region.
        int seqPosition = ReadInt(region, "seq_position", 1, patchDescription);
        int seqLength = ReadInt(region, "seq_length", 1, patchDescription);

        // Pitfall 1 + Security Domain DoS guard: spec caps seq_position at 100;
        // clamp seq_length values that exceed 100 with one-shot WarnOnce.
        if (seqLength > 100)
        {
            RenderingDiagnostics.WarnOnce(
                $"sfz:opcode_value:{patchDescription}:seq_length:{seqLength}",
                $"[sfz] seq_length={seqLength} exceeds spec max 100 in '{patchDescription}' — clamping to 100");
            seqLength = 100;
        }

        // Phase 37 SAMP-02 velocity-crossfade opcodes (RESEARCH §Pattern 6).
        // Sentinel defaults (-1) mean "no xfade band declared" — region falls
        // back to Phase 33 hard-switch behavior at lovel/hivel boundaries.
        int xfinLoVel = ReadIntAllowingNegative(region, "xfin_lovel", -1, patchDescription);
        int xfinHiVel = ReadIntAllowingNegative(region, "xfin_hivel", -1, patchDescription);
        int xfoutLoVel = ReadIntAllowingNegative(region, "xfout_lovel", -1, patchDescription);
        int xfoutHiVel = ReadIntAllowingNegative(region, "xfout_hivel", -1, patchDescription);

        // loop_mode — value mapping with charitable fallback.
        SfzLoopMode loopMode;
        if (region.TryGetValue("loop_mode", out var lmStr))
        {
            switch (lmStr)
            {
                case "no_loop":          loopMode = SfzLoopMode.NoLoop; break;
                case "one_shot":         loopMode = SfzLoopMode.OneShot; break;
                case "loop_continuous":  loopMode = SfzLoopMode.LoopContinuous; break;
                case "loop_sustain":     loopMode = SfzLoopMode.LoopSustain; break;
                default:
                    RenderingDiagnostics.WarnOnce(
                        $"sfz:opcode_value:{patchDescription}:loop_mode:{lmStr}",
                        $"[sfz] unknown loop_mode value '{lmStr}' in '{patchDescription}' — falling back to no_loop");
                    loopMode = SfzLoopMode.NoLoop;
                    break;
            }
        }
        else if (loopStart > 0 || loopEnd > 0)
        {
            // SFZ spec: declaring loop_start/end without loop_mode implies
            // loop_continuous.
            loopMode = SfzLoopMode.LoopContinuous;
        }
        else
        {
            loopMode = SfzLoopMode.NoLoop;
        }

        return new SfzRegion(
            samplePath,
            pitchKeycenter,
            loKey, hiKey,
            loVel, hiVel,
            loopMode,
            loopStart, loopEnd,
            ampegAttack, ampegRelease,
            volumeLinear, panFlow,
            seqPosition, seqLength,
            xfinLoVel, xfinHiVel,
            xfoutLoVel, xfoutHiVel);
    }

    private static int ReadInt(
        Dictionary<string, string> region, string key, int fallback, string patchDescription)
    {
        if (!region.TryGetValue(key, out var raw)) return fallback;
        // NumberStyles.None — strict integer; rejects leading '+'/' '/'.0',
        // thousands separators, decimal points. Mirrors ScalaParser.cs:103.
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var v))
        {
            // Allow negative ints for opcodes that explicitly accept them
            // (pan, transpose). For the 7 int opcodes in our whitelist
            // (lokey/hikey/lovel/hivel/loop_start/loop_end/pitch_keycenter)
            // the value must be non-negative — NumberStyles.None enforces that.
            RenderingDiagnostics.WarnOnce(
                $"sfz:opcode_value:{patchDescription}:{key}:{raw}",
                $"[sfz] invalid value for {key} in '{patchDescription}' — '{raw}' rejected, using default");
            return fallback;
        }
        return v;
    }

    /// <summary>
    /// Like <see cref="ReadInt"/> but permits a leading sign so the Phase 37
    /// SAMP-02 xfin/xfout opcodes can carry the <c>-1</c> sentinel from inline
    /// SFZ patches. Used only by the Phase 37 opcode quad (xfin_lovel,
    /// xfin_hivel, xfout_lovel, xfout_hivel); the Phase 33 baseline keeps the
    /// strict <see cref="NumberStyles.None"/> path.
    /// </summary>
    private static int ReadIntAllowingNegative(
        Dictionary<string, string> region, string key, int fallback, string patchDescription)
    {
        if (!region.TryGetValue(key, out var raw)) return fallback;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
        {
            RenderingDiagnostics.WarnOnce(
                $"sfz:opcode_value:{patchDescription}:{key}:{raw}",
                $"[sfz] invalid value for {key} in '{patchDescription}' — '{raw}' rejected, using default");
            return fallback;
        }
        return v;
    }

    private static double ReadDouble(
        Dictionary<string, string> region, string key, double fallback, string patchDescription)
    {
        if (!region.TryGetValue(key, out var raw)) return fallback;
        // Use FloatStyle (no exponent, no thousands) + InvariantCulture.
        // AllowLeadingSign is part of NumberStyles.Float, so we accept the
        // negative values that volume/pan/ampeg_* legitimately use.
        if (!double.TryParse(raw, FloatStyle, CultureInfo.InvariantCulture, out var v))
        {
            RenderingDiagnostics.WarnOnce(
                $"sfz:opcode_value:{patchDescription}:{key}:{raw}",
                $"[sfz] invalid value for {key} in '{patchDescription}' — '{raw}' rejected, using default");
            return fallback;
        }
        return v;
    }

    /// <summary>
    /// Normalise Windows backslashes to the OS path separator. Required for
    /// the VSCO-CE <c>default_path=Strings\Solo Violin\Arco Vib\</c> pattern
    /// (Linux primary per CLAUDE.md).
    /// </summary>
    private static string NormaliseSeparators(string p)
    {
        if (string.IsNullOrEmpty(p)) return p;
        // Always normalise to OS-native. On Linux, this rewrites every '\'
        // to '/'. On Windows, '\\' is already the separator so the result
        // is a no-op (replacing '\\' with '\\').
        return p.Replace('\\', Path.DirectorySeparatorChar);
    }
}
