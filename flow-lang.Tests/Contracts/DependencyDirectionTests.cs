using System.Text.Json;
using FlowLang.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace FlowLang.Tests.Contracts;

/// <summary>
/// Dependency-direction ratchet for docs/decisions/2026-09-22-dependency-direction.md.
///
/// Language-core types must not reference music, audio, IO or platform types. Today
/// they do; every existing edge (top-level source type -> forbidden target
/// namespace) is pinned in docs/baselines/phase1/language-dependency-edges.json.
/// A new edge fails. A removed edge also fails until the baseline is regenerated
/// with FLOW_UPDATE_DEPENDENCY_BASELINE=1, so the list only shrinks and stays exact.
/// </summary>
public class DependencyDirectionTests
{
    // Logical Flow.Language (roadmap 4.1) as it exists inside flow-lang.dll.
    public static readonly string[] LanguageNamespaces =
    [
        "FlowLang.Lexing", "FlowLang.Parsing", "FlowLang.Ast", "FlowLang.Interpreter",
        "FlowLang.Diagnostics", "FlowLang.Runtime", "FlowLang.TypeSystem",
        "FlowLang.StandardLibrary.Dict", "FlowLang.Syntax",
    ];

    // Namespaces a language-core type may not reference (music model/language,
    // audio, music IO, platform, network).
    public static readonly string[] ForbiddenNamespaces =
    [
        "FlowLang.Audio", "FlowLang.StandardLibrary.Audio", "FlowLang.StandardLibrary.Composition",
        "FlowLang.StandardLibrary.Harmony", "FlowLang.StandardLibrary.Improv", "FlowLang.StandardLibrary.Midi",
        "FlowLang.StandardLibrary.Network", "FlowLang.StandardLibrary.Notation", "FlowLang.StandardLibrary.Patterns",
        "FlowLang.StandardLibrary.Transforms", "FlowLang.StandardLibrary.Generative",
        "FlowLang.TypeSystem.SpecialTypes", "Melanchall", "NAudio", "Rug.Osc",
    ];

    // Special (music) types live under TypeSystem today; they are targets, not sources.
    private static readonly string[] SourceExclusions = ["FlowLang.TypeSystem.SpecialTypes"];

    private static readonly string BaselinePath = Path.Combine(
        Path.GetDirectoryName(FlowScriptData.FindTestsRoot())!,
        "docs", "baselines", "phase1", "language-dependency-edges.json");

    [Fact]
    public void LanguageCoreGainsNoNewMusicOrPlatformDependencies()
    {
        var current = ComputeEdges();

        if (Environment.GetEnvironmentVariable("FLOW_UPDATE_DEPENDENCY_BASELINE") == "1")
        {
            var json = JsonSerializer.Serialize(new
            {
                description = "Language-core type -> forbidden namespace edges in flow-lang.dll. Shrink-only; see docs/decisions/2026-09-22-dependency-direction.md.",
                languageNamespaces = LanguageNamespaces,
                forbiddenNamespaces = ForbiddenNamespaces,
                edges = current,
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BaselinePath, json + "\n");
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(BaselinePath));
        var baseline = doc.RootElement.GetProperty("edges").EnumerateArray().Select(e => e.GetString()!).ToHashSet();

        var added = current.Where(e => !baseline.Contains(e)).ToList();
        var removed = baseline.Where(e => !current.Contains(e)).Order().ToList();
        Assert.True(added.Count == 0,
            "New language -> music/platform dependencies:\n  " + string.Join("\n  ", added));
        Assert.True(removed.Count == 0,
            "Edges no longer present; regenerate the baseline with FLOW_UPDATE_DEPENDENCY_BASELINE=1:\n  "
            + string.Join("\n  ", removed));
    }

    public static List<string> ComputeEdges()
    {
        using var module = ModuleDefinition.ReadModule(typeof(FlowEngine).Assembly.Location);
        var edges = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in module.Types)
        {
            if (!IsIn(type.Namespace, LanguageNamespaces) || IsIn(type.Namespace, SourceExclusions)) continue;
            foreach (var ns in ReferencedNamespaces(type))
            {
                var target = ForbiddenNamespaces.FirstOrDefault(f => IsIn(ns, [f]));
                if (target is not null) edges.Add($"{type.FullName} -> {ns}");
            }
        }
        return edges.ToList();
    }

    private static bool IsIn(string ns, string[] roots) =>
        roots.Any(r => ns == r || ns.StartsWith(r + ".", StringComparison.Ordinal));

    // Every namespace a type (including its nested compiler-generated types) refers to.
    private static IEnumerable<string> ReferencedNamespaces(TypeDefinition type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(TypeReference? t)
        {
            while (t is not null)
            {
                switch (t)
                {
                    case GenericInstanceType g:
                        foreach (var a in g.GenericArguments) Add(a);
                        t = g.ElementType;
                        continue;
                    case TypeSpecification s:
                        t = s.ElementType;
                        continue;
                    case GenericParameter:
                        return;
                }
                var root = t;
                while (root.DeclaringType is not null) root = root.DeclaringType;
                if (!string.IsNullOrEmpty(root.Namespace)) seen.Add(root.Namespace);
                return;
            }
        }

        void Visit(TypeDefinition t)
        {
            Add(t.BaseType);
            foreach (var i in t.Interfaces) Add(i.InterfaceType);
            foreach (var f in t.Fields) Add(f.FieldType);
            foreach (var p in t.Properties) Add(p.PropertyType);
            foreach (var m in t.Methods)
            {
                Add(m.ReturnType);
                foreach (var p in m.Parameters) Add(p.ParameterType);
                if (!m.HasBody) continue;
                foreach (var v in m.Body.Variables) Add(v.VariableType);
                foreach (var ins in m.Body.Instructions)
                {
                    switch (ins.Operand)
                    {
                        case TypeReference tr: Add(tr); break;
                        case MethodReference mr:
                            Add(mr.DeclaringType);
                            Add(mr.ReturnType);
                            foreach (var p in mr.Parameters) Add(p.ParameterType);
                            if (mr is GenericInstanceMethod gm) foreach (var a in gm.GenericArguments) Add(a);
                            break;
                        case FieldReference fr: Add(fr.DeclaringType); Add(fr.FieldType); break;
                    }
                }
            }
            foreach (var nested in t.NestedTypes) Visit(nested);
        }

        Visit(type);
        return seen;
    }
}
