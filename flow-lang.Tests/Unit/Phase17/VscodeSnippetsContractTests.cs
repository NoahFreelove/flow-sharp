using System.IO;
using System.Text.Json;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 contract test for the VSCode snippets file
/// (<c>vscode-extension/snippets/flow.code-snippets</c>).
///
/// CR-01 regression guard: the packaged `proc` snippet MUST use Flow's
/// canonical `end proc` block terminator. A prior revision shipped a
/// brace-style `proc name () { ... }` snippet which produces uncompilable
/// Flow (SimpleLexer maps `end` → TokenType.EndProc; Parser.cs:255 requires
/// `end proc` after the procedure body).
///
/// This test walks up from the test assembly to locate the snippets file in
/// the repo tree. If the file cannot be found (e.g., packaged test harness),
/// the test is skipped rather than failed — we only enforce the contract when
/// the source tree is co-located with the test binary.
/// </summary>
public class VscodeSnippetsContractTests
{
    private static string? FindSnippetsFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "vscode-extension", "snippets", "flow.code-snippets");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void Snippets_JsonIsValid()
    {
        var path = FindSnippetsFile();
        Assert.SkipWhen(path is null, "snippets file not found in repo tree");
        var json = File.ReadAllText(path!);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void ProcSnippet_UsesEndProcTerminator()
    {
        var path = FindSnippetsFile();
        Assert.SkipWhen(path is null, "snippets file not found in repo tree");
        var json = File.ReadAllText(path!);
        using var doc = JsonDocument.Parse(json);

        var proc = doc.RootElement.GetProperty("Proc declaration");
        var body = proc.GetProperty("body");
        Assert.Equal(JsonValueKind.Array, body.ValueKind);

        // Flatten the body array into a single string and assert the terminator
        // form matches canonical Flow syntax (see flow-lang/Parsing/Parser.cs:255).
        var joined = string.Join("\n",
            body.EnumerateArray().Select(e => e.GetString() ?? string.Empty));
        Assert.Contains("end proc", joined);
        // Reject the old brace-style terminator that would produce invalid syntax.
        Assert.DoesNotContain("() {", joined);
    }
}
