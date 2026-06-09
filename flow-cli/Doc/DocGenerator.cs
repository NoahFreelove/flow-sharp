namespace FlowCli.Doc;

// Phase 41 Plan 41-03 DOC-01/02 — the end-to-end `flow doc` pipeline, factored
// out of DocCommand so FlowDocGenTests can drive it in-process without spawning
// the CLI.
//
// Pipeline: DocCollector.Collect → DocExampleRunner.RunAll → ContentHashCache
// (skip/regenerate accounting) → HtmlEmitter / MarkdownEmitter per format.
//
// Path safety (T-41-03-V12): NormalizeOutDir resolves `--out` to an absolute
// path under a base root and CONFINES all writes to it — a traversal-shaped arg
// is normalized so the emitters can only ever write inside the resolved dir.
public enum DocFormat { Html, Markdown, Both }

public sealed record DocGenResult(
    string OutDir,
    string? HtmlPath,
    string? MarkdownPath,
    int EntryCount,
    int ExampleFailureCount,
    CacheDecision Cache);

public sealed class DocGenerator
{
    public const string DefaultOutDir = "docs/reference";

    /// <summary>
    /// Resolve <paramref name="rawOut"/> to a normalized, traversal-safe absolute
    /// output directory (T-41-03-V12).
    ///
    /// Policy:
    ///   * An empty/whitespace arg falls back to <c>docs/reference</c> under the
    ///     base root.
    ///   * An <b>explicitly-rooted absolute path</b> the composer gives
    ///     (e.g. <c>/tmp/flowdoc</c> or <c>C:\docs</c>) is honored — a composer
    ///     deliberately choosing where to write is not an attack, and the verb
    ///     should write wherever the user has permission.
    ///   * A <b>relative path</b> is resolved under the base root and CONFINED:
    ///     any <c>..</c> traversal that would escape the base root
    ///     (e.g. <c>../../etc</c>) is rebased into a safe subdir under the
    ///     default output dir. This is the threat the V12 mitigation targets —
    ///     a relative <c>--out</c> sneaking out of the project tree.
    ///
    /// The result is always <c>Path.GetFullPath</c>-normalized (no surviving
    /// <c>..</c> segments).
    /// </summary>
    public static string NormalizeOutDir(string? rawOut, string? baseRoot = null)
    {
        baseRoot = Path.GetFullPath(baseRoot ?? Directory.GetCurrentDirectory());

        if (string.IsNullOrWhiteSpace(rawOut))
            rawOut = DefaultOutDir;

        // An explicit absolute path is the composer's deliberate choice — honor it.
        if (Path.IsPathRooted(rawOut))
            return Path.GetFullPath(rawOut);

        // Relative path: resolve under base root and normalize.
        var full = Path.GetFullPath(Path.Combine(baseRoot, rawOut));

        // Confinement check with boundary safety (not a glob prefix — a sibling
        // like `<root>-evil` must NOT count as inside). A relative arg that
        // resolves outside the base root is a traversal escape: rebase it.
        var rootWithSep = baseRoot.EndsWith(Path.DirectorySeparatorChar)
            ? baseRoot
            : baseRoot + Path.DirectorySeparatorChar;
        bool confined = string.Equals(full, baseRoot, StringComparison.Ordinal)
            || full.StartsWith(rootWithSep, StringComparison.Ordinal);

        if (!confined)
        {
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(leaf) || leaf == "..")
                leaf = "out";
            full = Path.GetFullPath(Path.Combine(baseRoot, DefaultOutDir, leaf));
        }

        return full;
    }

    /// <summary>
    /// Run the full pipeline. <paramref name="flowSourceDirs"/> scopes the `///`
    /// proc harvest (TopDirectoryOnly per dir); pass null for builtins-only.
    /// </summary>
    public DocGenResult Generate(
        string? rawOut,
        DocFormat format,
        IEnumerable<string>? flowSourceDirs = null,
        string? baseRoot = null,
        bool runExamples = true)
    {
        var outDir = NormalizeOutDir(rawOut, baseRoot);
        Directory.CreateDirectory(outDir);

        var collector = new DocCollector();
        var models = collector.Collect(flowSourceDirs);

        if (runExamples)
        {
            var exampleRunner = new DocExampleRunner();
            models = exampleRunner.RunAll(models);
        }

        var cache = ContentHashCache.Load(outDir);
        var decision = cache.Decide(models);

        string? htmlPath = null;
        string? mdPath = null;
        if (format is DocFormat.Html or DocFormat.Both)
            htmlPath = new HtmlEmitter().Write(outDir, models);
        if (format is DocFormat.Markdown or DocFormat.Both)
            mdPath = new MarkdownEmitter().Write(outDir, models);

        cache.Save(outDir, models);

        int failureCount = models.Sum(m => m.ExampleFailures.Count);
        return new DocGenResult(outDir, htmlPath, mdPath, models.Length, failureCount, decision);
    }

    public static DocFormat ParseFormat(string? raw) =>
        (raw?.Trim().ToLowerInvariant()) switch
        {
            "html" => DocFormat.Html,
            "md" or "markdown" => DocFormat.Markdown,
            _ => DocFormat.Both,
        };
}
