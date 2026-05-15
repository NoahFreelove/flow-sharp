using System.Linq;
using FlowLang.StandardLibrary;
using FlowLang.Tests.Unit.Phase17;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLsp;
using FlowLsp.Handlers;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase31;

/// <summary>
/// Phase 31 Plan 31-05 (SPEC-3): pins the LSP-side varargs rendering layer.
///
/// CONTEXT D-01: Variadic parameters render with the Unicode horizontal ellipsis
/// `…` (U+2026, UTF-8 E2 80 A6), NOT three ASCII dots. Modern LSP clients
/// (VSCode, JetBrains) render the glyph cleanly; the single code point keeps
/// tooltips compact next to other variadic-friendly tokens.
///
/// CONTEXT D-02: Ellipsis trails the parameter TYPE, not the parameter name.
/// Format: <c>name(Type…)</c>. Matches the strongest single convention from the
/// Java / TypeScript / C# variadic-rendering family.
///
/// Phase 24 D-04: flow-lang stays untouched — <c>FunctionSignature.ToString()</c>
/// continues to emit ASCII <c>"..."</c> for runtime use. The LSP-side
/// <c>LspMappings.FormatSignature</c> is the new layer that renders U+2026.
///
/// Pitfall 3 (RESEARCH.md §Pitfalls): explicit
/// <c>SignatureInformation.Parameters</c> via <c>LspMappings.BuildParameters</c>
/// sidesteps the UTF-8-vs-grapheme offset problem when the active-parameter
/// highlight crosses the varargs glyph position.
/// </summary>
public class VarargsRenderingFacts
{
    private static (BuiltInIndex bi, UserSymbolIndex ui, StdlibSymbolIndex si) Indices()
    {
        var reg = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(reg);
        return (new BuiltInIndex(reg), new UserSymbolIndex(), new StdlibSymbolIndex(new ParseSession()));
    }

    // ===== Unit-level (FormatSignature / BuildParameters) =====

    [Fact]
    public void FormatSignature_VarargsParam_UsesU2026()
    {
        var sig = new FunctionSignature(
            "concat",
            new FlowType[] { StringType.Instance },
            IsVarArgs: true);
        var rendered = LspMappings.FormatSignature(sig);
        // U+2026 horizontal ellipsis, NOT three ASCII dots.
        Assert.Equal("concat(String…)", rendered);
        Assert.DoesNotContain("...", rendered);
    }

    [Fact]
    public void FormatSignature_NonVarargs_NoEllipsis()
    {
        var sig = new FunctionSignature(
            "add",
            new FlowType[] { IntType.Instance, IntType.Instance },
            IsVarArgs: false);
        var rendered = LspMappings.FormatSignature(sig);
        Assert.Equal("add(Int, Int)", rendered);
        Assert.DoesNotContain("…", rendered);
    }

    [Fact]
    public void FormatSignature_MultiParam_OnlyLastGetsEllipsis()
    {
        // D-02 — ellipsis ONLY on the last param; preceding params render bare.
        var sig = new FunctionSignature(
            "dict",
            new FlowType[] { SymbolType.Instance, IntType.Instance },
            IsVarArgs: true);
        var rendered = LspMappings.FormatSignature(sig);
        Assert.Equal("dict(Symbol, Int…)", rendered);
    }

    [Fact]
    public void BuildParameters_VarargsParam_LastLabelHasEllipsis()
    {
        var sig = new FunctionSignature(
            "dict",
            new FlowType[] { SymbolType.Instance, IntType.Instance },
            IsVarArgs: true);
        var parameters = LspMappings.BuildParameters(sig).ToList();
        Assert.Equal(2, parameters.Count);
        // First param: bare type
        var firstLabel = parameters[0].Label?.Label ?? string.Empty;
        Assert.Equal("Symbol", firstLabel);
        Assert.DoesNotContain("…", firstLabel);
        // Last param: type + U+2026
        var lastLabel = parameters[1].Label?.Label ?? string.Empty;
        Assert.Equal("Int…", lastLabel);
        Assert.Contains("…", lastLabel);
    }

    [Fact]
    public void BuildParameters_NonVarargs_NoEllipsisAnywhere()
    {
        var sig = new FunctionSignature(
            "add",
            new FlowType[] { IntType.Instance, IntType.Instance },
            IsVarArgs: false);
        var parameters = LspMappings.BuildParameters(sig).ToList();
        Assert.Equal(2, parameters.Count);
        foreach (var p in parameters)
        {
            var lbl = p.Label?.Label ?? string.Empty;
            Assert.DoesNotContain("…", lbl);
        }
    }

    // ===== Handler-level (HoverHandler / SignatureHelpHandler integration) =====

    [Fact]
    public void Hover_VarargsBuiltin_RendersEllipsis()
    {
        // `list` is a real registered varargs builtin per BuiltInFunctions.cs:492-496
        // (signature: ("list", [VoidType.Instance], IsVarArgs: true)).
        var (bi, ui, si) = Indices();
        var hover = HoverHandler.BuildHover("list", bi, ui, si, DocumentUri.File("/t.flow"));
        Assert.NotNull(hover);
        var md = hover!.Contents.MarkupContent!.Value;
        Assert.Contains("…", md);      // U+2026 horizontal ellipsis
        Assert.DoesNotContain("...", md);   // NOT three ASCII dots
    }

    [Fact]
    public void Hover_NonVarargsBuiltin_NoEllipsisRegression()
    {
        // `print` is NOT varargs — must not introduce U+2026 (Phase 17 regression).
        var (bi, ui, si) = Indices();
        var hover = HoverHandler.BuildHover("print", bi, ui, si, DocumentUri.File("/t.flow"));
        Assert.NotNull(hover);
        var md = hover!.Contents.MarkupContent!.Value;
        Assert.DoesNotContain("…", md);
    }

    [Fact]
    public void SignatureHelp_VarargsBuiltin_LabelHasEllipsis_ParametersNonEmpty()
    {
        // Construct a SignatureInformation the same way SignatureHelpHandler does
        // so the test exercises the same FormatSignature + BuildParameters wiring.
        var (bi, _, _) = Indices();
        var entry = bi.Find("list");
        Assert.NotNull(entry);
        Assert.True(entry!.Signatures.Count > 0);

        var label = LspMappings.FormatSignature(entry.Signatures[0]);
        var parameters = LspMappings.BuildParameters(entry.Signatures[0]);

        Assert.Contains("…", label);
        Assert.NotEmpty(parameters);
        // Last parameter must carry the ellipsis (D-02 — trails the type).
        var paramList = parameters.ToList();
        var lastLabel = paramList[^1].Label?.Label ?? string.Empty;
        Assert.Contains("…", lastLabel);
    }
}
