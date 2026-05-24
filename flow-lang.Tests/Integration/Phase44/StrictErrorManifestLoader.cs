using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-00 — strict-error-manifest.csv loader.
///
/// <para>
/// The CSV at <c>.planning/phases/44-strict-mode/strict-error-manifest.csv</c>
/// is the AUTHORITATIVE inventory of every Phase 44 strict-mode site. This
/// helper exposes partitioned <c>IEnumerable&lt;object[]&gt;</c> sequences for
/// xUnit <c>[Theory]</c> + <c>[MemberData]</c> consumption from:
/// </para>
///
/// <list type="bullet">
///   <item>Plan 44-05 — 13 §6a input-perimeter clamp Theory rows (via
///         <see cref="LoadInScopeSites"/> filtered by param != null).</item>
///   <item>Plan 44-06 — HIGH-priority advisory sites (via
///         <see cref="LoadHighPrioritySites"/>).</item>
///   <item>Plan 44-07 — MED/LOW-priority advisory sites (via
///         <see cref="LoadMedLowPrioritySites"/>).</item>
///   <item>Plan 44-08 — 5 carve-out sites (via
///         <see cref="LoadCarveOutSites"/>) for the anti-Pitfall-2 regression
///         pin (carve-out sites STAY charitable in both modes).</item>
/// </list>
///
/// <para>
/// CSV parsing uses a small hand-rolled RFC 4180 reader that handles quoted
/// fields with embedded commas — every Phase 44 sentinel_body contains
/// <c>outside [0.0, 1.0]</c>-shaped commas. Per D-v1.5-06 (CLAUDE.md
/// "Conventions"): no PRNG sites; deterministic across re-runs.
/// </para>
///
/// <para>
/// Repo root resolution mirrors Phase 42 <c>ClampGrepConsistencyTests.FindRepoRoot</c>
/// — walks up from <c>AppContext.BaseDirectory</c> looking for
/// <c>flow-sharp.sln</c>.
/// </para>
/// </summary>
public static class StrictErrorManifestLoader
{
    /// <summary>
    /// Relative path (from repo root) to the curated CSV manifest.
    /// </summary>
    public const string ManifestRelPath =
        ".planning/phases/44-strict-mode/strict-error-manifest.csv";

    /// <summary>
    /// Expected first row of the CSV. The Wave 0 sanity Fact pins this
    /// verbatim. Downstream Plans 44-05..44-08 also read column ordering
    /// from <see cref="StrictErrorRow"/> so changing the header here without
    /// updating consumers will trip the sanity Fact first.
    /// </summary>
    public const string ExpectedHeader =
        "file_path,line,builtin,tag,sentinel_body,priority,carve_out,axis,param,range";

    /// <summary>
    /// Cached parsed rows. Filled on first read; safe under single-threaded
    /// xUnit collection isolation (FlowScripts collection disables
    /// parallelization for stdlib-touching tests).
    /// </summary>
    private static IReadOnlyList<StrictErrorRow>? _cache;
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Loads + parses + caches every row of the manifest (including
    /// carve-outs). Returns a stable list ordering matching CSV row order.
    /// </summary>
    public static IReadOnlyList<StrictErrorRow> LoadAll()
    {
        if (_cache is not null) return _cache;
        lock (_cacheLock)
        {
            if (_cache is not null) return _cache;

            string repoRoot = FindRepoRoot();
            string csvPath = Path.Combine(repoRoot, ManifestRelPath);
            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException(
                    $"strict-error-manifest.csv not found at {csvPath}", csvPath);
            }

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length == 0)
            {
                throw new InvalidDataException(
                    $"strict-error-manifest.csv is empty at {csvPath}");
            }

            // Header line MUST match (sanity Fact pins this; loader trusts).
            // No assertion here — Wave 0 sanity Fact is the gate.
            var rows = new List<StrictErrorRow>(capacity: lines.Length);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                rows.Add(ParseRow(lines[i], lineNumber: i + 1));
            }
            _cache = rows;
            return _cache;
        }
    }

    /// <summary>
    /// IEnumerable of object[] for xUnit <c>[MemberData]</c>: yields the
    /// in-scope rows (carve_out=false). Order is CSV row order — stable +
    /// deterministic per D-v1.5-06.
    /// </summary>
    public static IEnumerable<object[]> LoadInScopeSites()
    {
        return LoadAll()
            .Where(r => !r.CarveOut)
            .Select(r => new object[]
            {
                r.FilePath, r.Line, r.Builtin, r.Tag, r.SentinelBody, r.Priority,
            });
    }

    /// <summary>
    /// IEnumerable of object[] for xUnit: yields the 5 carve-out rows. Used
    /// by Plan 44-08's anti-Pitfall-2 regression pin — these MUST stay
    /// charitable (WarnOnce, not ErrorReporter) in both modes.
    /// </summary>
    public static IEnumerable<object[]> LoadCarveOutSites()
    {
        return LoadAll()
            .Where(r => r.CarveOut)
            .Select(r => new object[]
            {
                r.FilePath, r.Line, r.Builtin, r.Tag, r.SentinelBody,
            });
    }

    /// <summary>
    /// HIGH-priority in-scope rows for Plan 44-06 (SFZ + Patterns + DSP +
    /// Render + Match + 13 §6a clamps).
    /// </summary>
    public static IEnumerable<object[]> LoadHighPrioritySites()
    {
        return LoadAll()
            .Where(r => !r.CarveOut && r.Priority == "HIGH")
            .Select(r => new object[]
            {
                r.FilePath, r.Line, r.Builtin, r.Tag, r.SentinelBody,
            });
    }

    /// <summary>
    /// MED + LOW-priority in-scope rows for Plan 44-07 (Chaos + Generative +
    /// ABC + MML + Tuning + OSC + AudioIn + Piano + MIDI + Harmony + Beat +
    /// Gain).
    /// </summary>
    public static IEnumerable<object[]> LoadMedLowPrioritySites()
    {
        return LoadAll()
            .Where(r => !r.CarveOut && (r.Priority == "MED" || r.Priority == "LOW"))
            .Select(r => new object[]
            {
                r.FilePath, r.Line, r.Builtin, r.Tag, r.SentinelBody, r.Priority,
            });
    }

    // -------------------------------------------------------------------------
    // CSV parser (RFC 4180 minimal subset — handles quoted fields with embedded
    // commas, which every Phase 44 sentinel_body uses for `outside [a, b]`).
    // -------------------------------------------------------------------------

    private static StrictErrorRow ParseRow(string line, int lineNumber)
    {
        var fields = SplitCsv(line);
        if (fields.Count != 10)
        {
            throw new InvalidDataException(
                $"strict-error-manifest.csv line {lineNumber}: expected 10 columns, " +
                $"got {fields.Count}. Line: {line}");
        }

        return new StrictErrorRow(
            FilePath: fields[0],
            Line: int.Parse(fields[1]),
            Builtin: fields[2],
            Tag: fields[3],
            SentinelBody: fields[4],
            Priority: fields[5],
            CarveOut: bool.Parse(fields[6]),
            Axis: fields[7],
            Param: fields[8],
            Range: fields[9]);
    }

    /// <summary>
    /// Minimal RFC 4180 CSV field splitter. Handles:
    ///   - Unquoted fields (no embedded commas / quotes / newlines).
    ///   - Quoted fields (start with <c>"</c>, end with <c>"</c>, may contain
    ///     <c>""</c> as an escaped quote and arbitrary commas).
    /// Does NOT handle embedded newlines (Phase 44 manifest has none).
    /// </summary>
    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        int i = 0;
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        while (i < line.Length)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote inside quoted field.
                        current.Append('"');
                        i += 2;
                        continue;
                    }
                    // End of quoted field.
                    inQuotes = false;
                    i++;
                    continue;
                }
                current.Append(c);
                i++;
            }
            else
            {
                if (c == ',')
                {
                    result.Add(current.ToString());
                    current.Clear();
                    i++;
                    continue;
                }
                if (c == '"' && current.Length == 0)
                {
                    inQuotes = true;
                    i++;
                    continue;
                }
                current.Append(c);
                i++;
            }
        }
        result.Add(current.ToString());
        return result;
    }

    /// <summary>
    /// Mirrors Phase 42 ClampGrepConsistencyTests.FindRepoRoot — walks up from
    /// the test assembly location looking for <c>flow-sharp.sln</c>.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "flow-sharp.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException(
                "Could not locate flow-sharp.sln walking up from " +
                AppContext.BaseDirectory);
        return dir.FullName;
    }
}

/// <summary>
/// One row of strict-error-manifest.csv. Field names match CSV column
/// headers. Constructed exclusively by
/// <see cref="StrictErrorManifestLoader.LoadAll"/>.
/// </summary>
public sealed record StrictErrorRow(
    string FilePath,
    int Line,
    string Builtin,
    string Tag,
    string SentinelBody,
    string Priority,
    bool CarveOut,
    string Axis,
    string Param,
    string Range);
