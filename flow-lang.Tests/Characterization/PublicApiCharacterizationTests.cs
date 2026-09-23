using System.Reflection;
using System.Text;
using FlowLang.Core;
using Xunit;

namespace FlowLang.Tests.Characterization;

/// <summary>
/// Existing C# consumers keep working during the extraction (roadmap 5.3: "keep a
/// delegating facade where practical"). Every public type and member recorded in
/// the snapshot must still exist; a deliberate removal is listed in
/// <c>Snapshots/api/allowed-removals.txt</c> with a reason. Additions are free.
/// </summary>
public class PublicApiCharacterizationTests
{
    [Fact]
    public void NoPublicApiIsRemovedWithoutAnAllowance()
    {
        var current = PublicSurface(typeof(FlowEngine).Assembly);
        var snapshotPath = Path.Combine(Snapshot.RepoRoot, "flow-lang.Tests", "Characterization", "Snapshots", "api", "flow-lang.txt");
        if (Environment.GetEnvironmentVariable("FLOW_UPDATE_SNAPSHOTS") == "1")
        {
            Snapshot.Verify("api/flow-lang.txt", string.Join("\n", current));
            return;
        }

        var recorded = File.ReadAllLines(snapshotPath).Where(l => l.Length > 0);
        var allowancePath = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "allowed-removals.txt");
        var allowed = File.Exists(allowancePath)
            ? File.ReadAllLines(allowancePath).Where(l => l.Length > 0 && !l.StartsWith('#'))
                .Select(l => l.Split("  #")[0].Trim()).ToHashSet(StringComparer.Ordinal)
            : [];
        var missing = recorded.Where(r => !current.Contains(r) && !allowed.Contains(r)).ToList();
        Assert.True(missing.Count == 0,
            $"{missing.Count} public API entries were removed. Restore them (a delegating facade), or list each in api/allowed-removals.txt with a reason:\n  "
            + string.Join("\n  ", missing.Take(40)));
    }

    internal static SortedSet<string> PublicSurface(Assembly assembly)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in assembly.GetExportedTypes())
        {
            var name = type.FullName ?? type.Name;
            set.Add("type " + name);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var member in type.GetMembers(flags))
            {
                // Compiler-generated record/closure members carry no API meaning.
                if (member.Name.StartsWith('<') || member.Name == "EqualityContract") continue;
                set.Add($"{name} :: {Describe(member)}");
            }
        }
        return set;
    }

    private static string Describe(MemberInfo member) => member switch
    {
        MethodInfo m => $"{(m.IsStatic ? "static " : "")}{Short(m.ReturnType)} {m.Name}({Params(m.GetParameters())})",
        ConstructorInfo c => $"ctor({Params(c.GetParameters())})",
        PropertyInfo p => $"{(p.GetMethod?.IsStatic == true ? "static " : "")}property {Short(p.PropertyType)} {p.Name}",
        FieldInfo f => $"{(f.IsStatic ? "static " : "")}field {Short(f.FieldType)} {f.Name}",
        EventInfo e => $"event {e.Name}",
        Type t => $"nested {t.Name}",
        _ => member.ToString() ?? member.Name,
    };

    private static string Params(ParameterInfo[] ps) => string.Join(", ", ps.Select(p => Short(p.ParameterType)));

    private static string Short(Type t) => t.IsGenericType
        ? $"{t.Name.Split('`')[0]}<{string.Join(",", t.GetGenericArguments().Select(Short))}>"
        : t.Name;
}
