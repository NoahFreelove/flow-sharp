using FlowCli.Doc;
using Xunit;

namespace FlowLang.Tests.Integration.Phase41;

/// <summary>
/// Phase 41 DOC-01 — the <c>flow doc</c> generator emits browsable static HTML at
/// <c>docs/reference/index.html</c> PLUS a Markdown sibling (D-09), sourcing
/// <c>///</c> doc-comments + parsed proc signatures + the existing
/// <c>BuiltInDocs.cs</c> entries (D-08).
///
/// <c>FlowDoc_OutPath_NormalizedNoTraversal</c> pins the T-41-03-V12 path-traversal
/// mitigation: a <c>--out</c> argument is normalized and must not escape the
/// confining base root (no <c>../../etc</c> writes).
///
/// 41-03: live — drives <see cref="DocGenerator"/> + <see cref="ContentHashCache"/>
/// in-process (the same pipeline DocCommand wires for the CLI verb).
/// </summary>
[Trait("Category", "Phase41")]
public class FlowDocGenTests
{
    [Fact]
    public void FlowDoc_EmitsHtmlAndMarkdown()
    {
        var dir = NewTempDir();
        try
        {
            var gen = new DocGenerator();
            // Builtins-only (no flow source dirs) so the test is hermetic and
            // does not depend on a repo .flow corpus. runExamples:false keeps it
            // fast — example execution is covered by DocExampleExecTests.
            var result = gen.Generate(
                rawOut: dir, format: DocFormat.Both,
                flowSourceDirs: null, baseRoot: dir, runExamples: false);

            var indexPath = System.IO.Path.Combine(result.OutDir, HtmlEmitter.FileName);
            var mdPath = System.IO.Path.Combine(result.OutDir, MarkdownEmitter.FileName);
            Assert.True(System.IO.File.Exists(indexPath), "index.html should exist");
            Assert.True(System.IO.File.Exists(mdPath), "reference.md should exist");

            var html = System.IO.File.ReadAllText(indexPath);
            // At least one well-known BuiltInDocs entry name.
            Assert.Contains("transpose", html);
            Assert.Contains("reverb", html);
            // Category headings from the CLAUDE.md grouping.
            Assert.Contains("Audio effects", html);
            Assert.Contains("Collections", html);
            // Browsable + no-JS contract.
            Assert.Contains("prefers-color-scheme", html);
            Assert.DoesNotContain("<script", html);

            var md = System.IO.File.ReadAllText(mdPath);
            Assert.Contains("# Flow Language Reference", md);
            Assert.Contains("### transpose", md);

            Assert.True(result.EntryCount > 50, "should collect the full builtin surface");
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void FlowDoc_OutPath_NormalizedNoTraversal()
    {
        var baseRoot = NewTempDir();
        try
        {
            // A traversal-shaped --out must be confined under baseRoot.
            var resolved = DocGenerator.NormalizeOutDir("../../etc", baseRoot);
            var fullBase = System.IO.Path.GetFullPath(baseRoot);
            var rootWithSep = fullBase.EndsWith(System.IO.Path.DirectorySeparatorChar)
                ? fullBase : fullBase + System.IO.Path.DirectorySeparatorChar;

            Assert.True(
                resolved.StartsWith(rootWithSep, System.StringComparison.Ordinal),
                $"normalized out '{resolved}' must stay under base root '{fullBase}'");
            Assert.DoesNotContain("..", resolved);

            // And generating with that traversal arg writes ONLY inside baseRoot.
            var gen = new DocGenerator();
            var result = gen.Generate(
                rawOut: "../../escape", format: DocFormat.Both,
                flowSourceDirs: null, baseRoot: baseRoot, runExamples: false);
            Assert.True(
                result.OutDir.StartsWith(rootWithSep, System.StringComparison.Ordinal),
                "output dir must be confined under base root");
            Assert.True(System.IO.File.Exists(
                System.IO.Path.Combine(result.OutDir, HtmlEmitter.FileName)));
        }
        finally { Cleanup(baseRoot); }
    }

    [Fact]
    public void FlowDoc_DefaultOutDir_IsDocsReference()
    {
        var baseRoot = NewTempDir();
        try
        {
            var resolved = DocGenerator.NormalizeOutDir(null, baseRoot);
            var expected = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(baseRoot, "docs", "reference"));
            Assert.Equal(expected, resolved);
        }
        finally { Cleanup(baseRoot); }
    }

    private static string NewTempDir()
    {
        var dir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "flowdoc-gen-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try { System.IO.Directory.Delete(dir, recursive: true); } catch { }
    }
}
