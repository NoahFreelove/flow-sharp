using Mono.Cecil;
using Xunit;

namespace FlowLang.Tests.Contracts;

/// <summary>
/// The transitive type closure of the language (every type a language type uses,
/// and everything those use, inside flow-lang.dll) must not reach music, audio or
/// platform code. This is the in-assembly form of the Phase 3 gate "the language
/// artifact's dependency closure excludes music": <see cref="DependencyDirectionTests"/>
/// checks direct references, this checks what they drag in. The non-language types
/// the closure needs are listed exactly, so the future language-only assembly's
/// contents change only on purpose.
/// </summary>
public class LanguageClosureTests
{
    // Types outside the language namespaces that the language legitimately needs.
    private static readonly string[] AllowedExtras =
    [
        "FlowLang.Core.SourceLocation",
        "FlowLang.Core.SourceMap",
        "FlowLang.Core.Span",
        "FlowLang.StandardLibrary.InternalFunctionRegistry",
        "FlowLang.StandardLibrary.TestFramework.AssertionException",
        "FlowLang.StandardLibrary.TestFramework.TestRecord",
        "FlowLang.StandardLibrary.TestFramework.TestSnapshot",
        "FlowLang.StandardLibrary.Utils",
    ];

    [Fact]
    public void LanguageClosureExcludesMusicAndPlatformCode()
    {
        var closure = Closure(out var paths);
        var forbidden = closure.Where(t => IsIn(Namespace(t), DependencyDirectionTests.ForbiddenNamespaces)).ToList();
        Assert.True(forbidden.Count == 0,
            "The language closure reaches music/platform code:\n  " + string.Join("\n  ", forbidden.Select(t => paths[t])));

        var extras = closure
            .Where(t => !IsIn(Namespace(t), DependencyDirectionTests.LanguageNamespaces) && !t.Contains('<'))
            .Order(StringComparer.Ordinal).ToList();
        var unexpected = extras.Except(AllowedExtras).ToList();
        var gone = AllowedExtras.Except(extras).ToList();
        Assert.True(unexpected.Count == 0 && gone.Count == 0,
            "The language closure's non-language types changed. Add a justified entry to AllowedExtras, or remove a stale one.\n"
            + string.Join("\n", unexpected.Select(t => "  new:  " + paths[t]).Concat(gone.Select(t => "  gone: " + t))));
    }

    private static string Namespace(string fullName) =>
        fullName.LastIndexOf('.') is var i and > 0 ? fullName[..i] : "";

    private static bool IsIn(string ns, string[] roots) =>
        roots.Any(r => ns == r || ns.StartsWith(r + ".", StringComparison.Ordinal));

    // Breadth-first closure from every language type; paths[t] explains why t is in it.
    private static HashSet<string> Closure(out Dictionary<string, string> paths)
    {
        using var module = ModuleDefinition.ReadModule(typeof(FlowLang.Core.FlowEngine).Assembly.Location);
        var types = module.Types.ToDictionary(t => t.FullName);
        var parent = new Dictionary<string, string?>();
        var queue = new Queue<string>();
        foreach (var t in module.Types)
        {
            if (!IsIn(t.Namespace, DependencyDirectionTests.LanguageNamespaces)
                || IsIn(t.Namespace, ["FlowLang.TypeSystem.SpecialTypes"])
                || t.FullName == "FlowLang.Runtime.WasmEntry")
                continue;
            parent[t.FullName] = null;
            queue.Enqueue(t.FullName);
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var referenced in References(types[current], types))
                if (parent.TryAdd(referenced, current))
                    queue.Enqueue(referenced);
        }
        paths = parent.Keys.ToDictionary(t => t, t =>
        {
            var chain = new List<string> { t };
            for (var p = parent[t]; p != null; p = parent[p]) chain.Add(p);
            return string.Join(" <- ", chain);
        });
        return parent.Keys.ToHashSet();
    }

    private static HashSet<string> References(TypeDefinition type, Dictionary<string, TypeDefinition> ownTypes)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        void Add(TypeReference? r)
        {
            switch (r)
            {
                case null or GenericParameter: return;
                case GenericInstanceType g:
                    foreach (var a in g.GenericArguments) Add(a);
                    Add(g.ElementType);
                    return;
                case TypeSpecification s: Add(s.ElementType); return;
            }
            var root = r;
            while (root.DeclaringType != null) root = root.DeclaringType;
            if (ownTypes.ContainsKey(root.FullName)) result.Add(root.FullName);
        }
        void Visit(TypeDefinition t)
        {
            Add(t.BaseType);
            foreach (var i in t.Interfaces) Add(i.InterfaceType);
            foreach (var a in t.CustomAttributes) Add(a.AttributeType);
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
            foreach (var n in t.NestedTypes) Visit(n);
        }
        Visit(type);
        return result;
    }
}
