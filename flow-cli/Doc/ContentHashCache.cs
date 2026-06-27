using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FlowCli.Doc;

// Phase 41 Plan 41-03 DOC-01 — per-entry content-hash incremental cache (D-09 /
// RESEARCH Pitfall 6).
//
// The cache is a small JSON sidecar (`.flowdoc-cache.json`) under the output
// dir mapping a per-entry key (Source:Signature — stable across re-gen) to the
// content hash of EVERY input that affects that entry's rendered output:
//
//   hash = SHA256( GeneratorVersion + name + signature + summary + params +
//                  example bodies + example failures )
//
// On re-gen, an entry whose hash is unchanged is SKIPPED (no re-render needed);
// an edited entry (changed `///` text, changed BuiltInDocs summary, changed
// example body, or a newly-failing example) gets a new hash and REGENERATES.
//
// GeneratorVersion is bumped whenever an emitter TEMPLATE changes so a template
// edit forces a full regen even when no entry content changed (Pitfall 6: a
// stale cache must never survive a template change). The emitters themselves
// always re-write index.html / reference.md as a whole (the cache governs the
// "did anything change?" decision + per-entry skip accounting, not partial-file
// surgery — a single-file static reference is rewritten atomically, which is
// simpler and still diffable).
public sealed class ContentHashCache
{
    // BUMP THIS whenever HtmlEmitter or MarkdownEmitter output templates change.
    public const string GeneratorVersion = "41-03.1";

    public const string CacheFileName = ".flowdoc-cache.json";

    private readonly Dictionary<string, string> _entries;

    private ContentHashCache(Dictionary<string, string> entries)
    {
        _entries = entries;
    }

    /// <summary>Stable per-entry cache key — survives re-gen as long as the
    /// entry's identity (source + signature) is unchanged.</summary>
    public static string KeyFor(DocModel m) => $"{m.Source}:{m.Signature}";

    /// <summary>Content hash over every input that affects the rendered entry.</summary>
    public static string HashFor(DocModel m)
    {
        var sb = new StringBuilder();
        sb.Append(GeneratorVersion).Append('␟');
        sb.Append(m.Name).Append('␟');
        sb.Append(m.Signature).Append('␟');
        sb.Append(m.Summary ?? "").Append('␟');
        sb.Append(m.Category).Append('␟');
        foreach (var p in m.Params)
            sb.Append(p.Name).Append('=').Append(p.Description).Append('␞');
        sb.Append('␟');
        foreach (var ex in m.Examples)
            sb.Append(ex).Append('␞');
        sb.Append('␟');
        foreach (var f in m.ExampleFailures)
            sb.Append(f).Append('␞');

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <summary>Load the sidecar cache from <paramref name="outDir"/>, or an
    /// empty cache when none exists / it is unreadable / its GeneratorVersion
    /// no longer matches (a version mismatch invalidates the whole cache).</summary>
    public static ContentHashCache Load(string outDir)
    {
        var path = Path.Combine(outDir, CacheFileName);
        if (!File.Exists(path))
            return new ContentHashCache(new());
        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<CacheDto>(json);
            if (dto is null || dto.GeneratorVersion != GeneratorVersion || dto.Entries is null)
                return new ContentHashCache(new());
            return new ContentHashCache(new(dto.Entries, StringComparer.Ordinal));
        }
        catch
        {
            return new ContentHashCache(new());
        }
    }

    /// <summary>True when this entry is unchanged vs. the loaded cache (skip on
    /// re-gen). False when new or edited (regenerate).</summary>
    public bool IsUnchanged(DocModel m) =>
        _entries.TryGetValue(KeyFor(m), out var prior) && prior == HashFor(m);

    /// <summary>
    /// Compute the skip/regenerate split for <paramref name="models"/> against
    /// the loaded cache. Returns counts only — the emitters always rewrite the
    /// whole single-file reference, but the split is the observable D-09 cache
    /// behavior the tests pin.
    /// </summary>
    public CacheDecision Decide(IReadOnlyList<DocModel> models)
    {
        int skipped = 0, regenerated = 0;
        var changedKeys = new List<string>();
        foreach (var m in models)
        {
            if (IsUnchanged(m))
            {
                skipped++;
            }
            else
            {
                regenerated++;
                changedKeys.Add(KeyFor(m));
            }
        }
        return new CacheDecision(skipped, regenerated, changedKeys);
    }

    /// <summary>Rebuild + persist the cache to reflect the current model set.</summary>
    public void Save(string outDir, IReadOnlyList<DocModel> models)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var m in models)
            entries[KeyFor(m)] = HashFor(m);

        Directory.CreateDirectory(outDir);
        var dto = new CacheDto { GeneratorVersion = GeneratorVersion, Entries = entries };
        var json = JsonSerializer.Serialize(dto, CacheJsonOptions);

        // WR-02: atomic write-via-tempfile. An in-place File.WriteAllText that is
        // killed mid-write (Ctrl-C / OOM / power loss) leaves a truncated cache; one
        // that happens to parse could serve STALE "unchanged" hashes and suppress
        // regen of edited entries. Write to a sibling .tmp then File.Move(overwrite),
        // which is a rename syscall — atomic on POSIX, near-atomic on NTFS — so a
        // reader only ever sees a complete cache or the previous complete cache.
        var targetPath = Path.Combine(outDir, CacheFileName);
        var tmpPath = targetPath + ".tmp";
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, targetPath, overwrite: true);
    }

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        WriteIndented = true,
    };

    private sealed class CacheDto
    {
        public string GeneratorVersion { get; set; } = "";
        public Dictionary<string, string>? Entries { get; set; }
    }
}

public sealed record CacheDecision(int Skipped, int Regenerated, IReadOnlyList<string> ChangedKeys);
