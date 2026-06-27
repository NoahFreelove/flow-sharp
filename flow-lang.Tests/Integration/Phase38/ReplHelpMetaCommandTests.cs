using System;
using System.IO;
using FlowInterpreter;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-04 — `:help &lt;name&gt;` meta-command per D-38-09 (overrides
/// REQUIREMENTS.md REPL-02 `?fn` wording per D-v1.5-01). Extends the existing
/// `:quit/:help/:clear/:stop` switch at Repl.cs:210-220. Format per UI-SPEC
/// lines 259-291: bold+green header, dim signature, default body, dim Example label.
/// Unknown identifier emits the locked yellow advisory line.
/// </summary>
[Collection("FlowScripts")]
public class ReplHelpMetaCommandTests : IDisposable
{
    public ReplHelpMetaCommandTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// `:help transpose` MUST print a layout containing the proc name as a header,
    /// a non-empty signature line, the doc body from BuiltInDocs.TryGet("transpose"),
    /// and an Example: block label. Asserts each section is present in raw text
    /// (ANSI escape stripping the easy way: substring-check on plain text content).
    /// </summary>
    [Fact]
    public void HelpWithName_PrintsHeaderSignatureBodyExample()
    {
        var repl = new Repl();

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            // Bool return of HandleCommand: true = continue REPL (do not exit).
            var result = repl.HandleCommandForTesting(":help transpose");
            Assert.True(result, ":help transpose should not exit the REPL");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var rendered = sw.ToString();

        // Header: the proc name appears verbatim somewhere
        Assert.Contains("transpose", rendered);
        // Body: BuiltInDocs entry for transpose mentions the word "semitone" in its summary
        Assert.Contains("semitone", rendered, StringComparison.OrdinalIgnoreCase);
        // Example: locked label per UI-SPEC line 276
        Assert.Contains("Example:", rendered);
    }

    /// <summary>
    /// Quick 260610-gl4 Finding 4 regression — `:help createSineTone` MUST resolve a
    /// doc entry (the tone constructors were absent from BuiltInDocs, so the composer's
    /// real-terminal smoke saw "[help] no documentation entry for 'createSineTone'").
    /// Asserts the header, the canonical Hertz-first runnable Example, and the ABSENCE
    /// of the no-documentation advisory. Same assertion for the other three tone
    /// constructors so a regression that drops any of them is caught.
    /// </summary>
    [Theory]
    [InlineData("createSineTone", "(play (createSineTone 440Hz 1.0 0.5))")]
    [InlineData("createSawTone", "(play (createSawTone 220Hz 1.0 0.5))")]
    [InlineData("createSquareTone", "(play (createSquareTone 330Hz 1.0 0.5))")]
    [InlineData("createTriangleTone", "(play (createTriangleTone 262Hz 1.0 0.5))")]
    public void HelpWithToneConstructor_ResolvesEntryAndRunnableExample(string name, string expectedExample)
    {
        var repl = new Repl();

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            var result = repl.HandleCommandForTesting($":help {name}");
            Assert.True(result, $":help {name} should not exit the REPL");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var rendered = sw.ToString();
        Assert.Contains(name, rendered);
        Assert.Contains("Example:", rendered);
        Assert.Contains(expectedExample, rendered);
        Assert.DoesNotContain($"[help] no documentation entry for '{name}'", rendered);
    }

    /// <summary>
    /// `:help fooBar` MUST emit the locked yellow advisory wording per UI-SPEC line 289
    /// — exact text is part of the composer-facing contract.
    /// </summary>
    [Fact]
    public void HelpWithUnknownName_PrintsAdvisory()
    {
        var repl = new Repl();

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            repl.HandleCommandForTesting(":help fooBar");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var rendered = sw.ToString();
        Assert.Contains("[help] no documentation entry for 'fooBar'", rendered);
        Assert.Contains(":help", rendered); // The CTA to ":help" meta-command list survives ANSI stripping.
    }
}
