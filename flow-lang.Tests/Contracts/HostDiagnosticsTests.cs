using System.Text.Json;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.TestFramework;
using FlowLang.Tests.Helpers;
using FlowLang.Tests.Integration.Phase48;
using Xunit;

namespace FlowLang.Tests.Contracts;

/// <summary>
/// Hosts see every diagnostic: the legacy FlowError list and the rich
/// FlowDiagnostic list are counted and formatted together, styling follows the
/// terminal policy, and a pure-Flow test that reports an error fails.
/// </summary>
[Collection("FlowScripts")]
public class HostDiagnosticsTests
{
    private const string UnknownIdentifier = "use \"@std\"\nInt length = 3\nInt n = lenght\n(nope 1)";

    [Fact]
    public void ErrorCountAndFormatAllCoverBothDiagnosticLists()
    {
        using var engine = new FlowEngine();
        engine.Execute(UnknownIdentifier, "<host>");

        var reporter = engine.ErrorReporter;
        Assert.Single(reporter.Diagnostics);          // rich: unknown identifier
        Assert.Single(reporter.Errors);               // legacy: function not found
        Assert.Equal(2, reporter.ErrorCount);

        var text = reporter.FormatAll(engine.SourceMap, useColor: false);
        Assert.Contains("unknown identifier 'lenght'", text);
        Assert.Contains("Function 'nope' not found", text);
        Assert.DoesNotContain("\u001b[", text);
    }

    [Fact]
    public void NoColorDisablesDiagnosticStyling()
    {
        using var scope = new TerminalEnvironmentScope();
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        Assert.False(ErrorReporter.ShouldUseColor());
        Assert.False(ErrorReporter.ShouldUseColor(toStdout: true));

        Environment.SetEnvironmentVariable("NO_COLOR", null);
        Environment.SetEnvironmentVariable("TERM", "dumb");
        Assert.False(ErrorReporter.ShouldUseColor());
    }

    [Fact]
    public void TestBodyThatReportsAnErrorFails()
    {
        using var engine = new FlowEngine();
        var originalOut = Console.Out;
        using var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            Assert.True(engine.Execute(
                "use \"@test\"\n(test \"broken\" lazy((missingFunction 1)))\n(test \"fine\" lazy((print \"ok\")))"));
            var (passed, failed) = new TestRunner().Run(engine, "<inline>");
            Assert.Equal(1, passed);
            Assert.Equal(1, failed);
            Assert.Contains("FAIL  <inline>::broken: body reported 1 error(s)", captured.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}

/// <summary>The browser runtime reports rich diagnostics in RunResult.errors.</summary>
[Collection(WasmEntryConsoleCollection.Name)]
public class WasmDiagnosticsTests
{
    [Fact]
    public void RunFromJsReportsUnknownIdentifiers()
    {
#pragma warning disable CA1416 // browser-only marshalling boundary; Execute path is platform-agnostic
        var json = WasmEntry.RunFromJs("use \"@std\"\nInt length = 3\nInt n = lenght");
#pragma warning restore CA1416
        var errors = JsonDocument.Parse(json).RootElement.GetProperty("errors");
        var error = Assert.Single(errors.EnumerateArray());
        Assert.Equal("unknown identifier 'lenght' (did you mean 'length'?)", error.GetProperty("message").GetString());
        Assert.Equal(3, error.GetProperty("line").GetInt32());
    }
}
