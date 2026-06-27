using FlowCli.Doc;
using Xunit;

namespace FlowLang.Tests.Integration.Phase41;

/// <summary>
/// Phase 41 DOC-01 — the <c>flow doc</c> content-hash incremental cache (D-09):
/// an unchanged entry is SKIPPED on re-gen; an edited entry REGENERATES. The key
/// covers signature + doc-comment text + example bodies + the BuiltInDocs entry +
/// a GeneratorVersion constant (RESEARCH Pitfall 6).
///
/// 41-03: live — exercises <see cref="ContentHashCache"/> directly.
/// </summary>
[Trait("Category", "Phase41")]
public class DocCacheTests
{
    private static DocModel Model(string summary = "Doubles its input.", string example = "(dbl 21)") =>
        new(
            Name: "dbl",
            Signature: "(dbl Int: x)",
            Summary: summary,
            Params: new[] { new DocParam("x", "the input") },
            Examples: new[] { example },
            ExampleFailures: System.Array.Empty<string>(),
            Category: "Arithmetic",
            Source: DocSource.Proc);

    [Fact]
    public void ContentHashCache_SkipsUnchanged()
    {
        var dir = NewTempDir();
        try
        {
            var models = new[] { Model() };

            // First gen — nothing cached, so everything regenerates.
            var first = ContentHashCache.Load(dir);
            var d1 = first.Decide(models);
            Assert.Equal(0, d1.Skipped);
            Assert.Equal(1, d1.Regenerated);
            first.Save(dir, models);

            // Second gen, no change — the entry is skipped.
            var second = ContentHashCache.Load(dir);
            var d2 = second.Decide(models);
            Assert.Equal(1, d2.Skipped);
            Assert.Equal(0, d2.Regenerated);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void EditedEntry_Regenerates()
    {
        var dir = NewTempDir();
        try
        {
            var original = new[] { Model(summary: "Doubles its input.") };
            var cache = ContentHashCache.Load(dir);
            cache.Save(dir, original);

            // Edit the /// doc-comment text → entry must regenerate.
            var edited = new[] { Model(summary: "Returns twice the input.") };
            var reloaded = ContentHashCache.Load(dir);
            var decision = reloaded.Decide(edited);
            Assert.Equal(0, decision.Skipped);
            Assert.Equal(1, decision.Regenerated);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void EditedExampleBody_Regenerates()
    {
        var dir = NewTempDir();
        try
        {
            var cache = ContentHashCache.Load(dir);
            cache.Save(dir, new[] { Model(example: "(dbl 21)") });

            // Changing only the example body still invalidates the entry.
            var reloaded = ContentHashCache.Load(dir);
            var decision = reloaded.Decide(new[] { Model(example: "(dbl 50)") });
            Assert.Equal(1, decision.Regenerated);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void GeneratorVersionMismatch_InvalidatesWholeCache()
    {
        var dir = NewTempDir();
        try
        {
            var models = new[] { Model() };
            ContentHashCache.Load(dir).Save(dir, models);

            // Rewrite the sidecar with a stale GeneratorVersion — Load must
            // treat it as an empty cache, forcing a full regen.
            var cachePath = System.IO.Path.Combine(dir, ContentHashCache.CacheFileName);
            var stale = System.IO.File.ReadAllText(cachePath)
                .Replace(ContentHashCache.GeneratorVersion, "OLD-VERSION");
            System.IO.File.WriteAllText(cachePath, stale);

            var reloaded = ContentHashCache.Load(dir);
            var decision = reloaded.Decide(models);
            Assert.Equal(0, decision.Skipped);
            Assert.Equal(1, decision.Regenerated);
        }
        finally { Cleanup(dir); }
    }

    private static string NewTempDir()
    {
        var dir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "flowdoc-cache-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try { System.IO.Directory.Delete(dir, recursive: true); } catch { }
    }
}
