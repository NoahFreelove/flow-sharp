using FlowLang.Ast;
using FlowLang.Ast.Patterns;
using FlowLang.Ast.Statements;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Runtime data for a section: named group of sequences with a musical context snapshot.
///
/// <para>
/// Phase 36 Plan 36-10 (D-36-17 SECT-01) extends SectionData with optional
/// parameter metadata so OverloadResolver.ResolveSection can dispatch over
/// multiple same-name registrations:
/// <list type="bullet">
///   <item><description><see cref="Parameters"/> — the Phase 35 pattern AST
///   list captured at section-declaration parse time. <c>null</c> for the
///   legacy zero-arg form; the dispatcher treats it as matching zero args.</description></item>
///   <item><description><see cref="DefaultValues"/> — defaulted expressions
///   for each parameter slot (D-36-15). Parallel-indexed with
///   <see cref="Parameters"/>; <c>null</c> when the parameter has no
///   default; the field is <c>null</c> at the wrapper level when the section
///   takes no parameters at all.</description></item>
///   <item><description><see cref="Body"/> — captured statements so the
///   parameterized re-entry can re-execute the body with bound parameter
///   values pushed into a synthetic frame.</description></item>
/// </list>
/// </para>
/// </summary>
public class SectionData
{
    public string Name { get; }
    public Dictionary<string, SequenceData> Sequences { get; }
    public Runtime.MusicalContext? Context { get; }
    public Core.SourceLocation? SourceLocation { get; }

    /// <summary>
    /// Phase 36 Plan 36-10 (D-36-17) — Phase 35 pattern AST list captured
    /// from the section declaration. Null for legacy zero-arg sections.
    /// </summary>
    public IReadOnlyList<Pattern>? Parameters { get; }

    /// <summary>
    /// Phase 36 Plan 36-10 (D-36-15) — per-slot default expressions
    /// parallel-indexed with <see cref="Parameters"/>. Null when the
    /// section has no parameters; individual slots may be null when a
    /// parameter has no default value.
    /// </summary>
    public IReadOnlyList<Expression?>? DefaultValues { get; }

    /// <summary>
    /// Phase 36 Plan 36-10 — captured section body statements. Used by the
    /// ExpressionEvaluator.EvaluateSectionCall re-entry path; null for
    /// legacy zero-arg sections (the Interpreter executes them once at
    /// declaration time and the cached Sequences are reused).
    /// </summary>
    public IReadOnlyList<Statement>? Body { get; }

    public SectionData(string name, Dictionary<string, SequenceData> sequences, Runtime.MusicalContext? context, Core.SourceLocation? sourceLocation = null,
        IReadOnlyList<Pattern>? parameters = null,
        IReadOnlyList<Expression?>? defaultValues = null,
        IReadOnlyList<Statement>? body = null)
    {
        Name = name;
        Sequences = sequences;
        Context = context;
        SourceLocation = sourceLocation;
        Parameters = parameters;
        DefaultValues = defaultValues;
        Body = body;
    }

    public override string ToString()
    {
        return $"Section[{Name}, {Sequences.Count} sequences]";
    }
}

/// <summary>
/// Represents a section type in the Flow type system.
/// </summary>
public sealed class SectionType : FlowType
{
    private SectionType() { }

    public static SectionType Instance { get; } = new();

    public override string Name => "Section";

    public override int GetSpecificity() => 138;
}
