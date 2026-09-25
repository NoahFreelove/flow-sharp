using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Patterns;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.Analysis;

/// <summary>
/// Conservative source checks, not execution or whole-program type inference.
/// Declarations are collected before use: availability/order and dynamic exports
/// remain runtime questions. Only source-defined procedures have complete call
/// contracts; internal declarations can have additional host overloads/defaults.
/// </summary>
internal sealed class StaticBindings
{
    private readonly AnalysisResult _analysis;
    private readonly CancellationToken _cancellation;
    private readonly List<AnalysisDiagnostic> _diagnostics = new();
    private readonly Scope _global = new(null);
    private readonly Dictionary<string, List<ProcedureDescriptor>> _procedures;
    private bool _openImports;
    private bool _strict;

    private sealed class Scope(Scope? parent)
    {
        public HashSet<string> Names { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Values { get; } = new(StringComparer.Ordinal);
        public bool ShadowsProcedure(string name) => Values.Contains(name)
            || (parent is not null && Names.Contains(name)) || (parent?.ShadowsProcedure(name) ?? false);
        public bool Contains(string name) => Names.Contains(name) || (parent?.Contains(name) ?? false);
    }

    private StaticBindings(AnalysisResult analysis, CancellationToken cancellation)
    {
        _analysis = analysis;
        _strict = analysis.Root.Program.Pragmas.Has("strict");
        _cancellation = cancellation;
        _procedures = analysis.Modules.SelectMany(m => m.Procedures)
            .GroupBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        foreach (var module in analysis.Modules)
        {
            Collect(module.Syntax.Program.Statements, _global);
            if (module.Name is { } name) _global.Names.Add(name);
        }
        _openImports = analysis.Diagnostics.Any(d => d.Code.StartsWith("flow.module.", StringComparison.Ordinal));
    }

    public static IReadOnlyList<AnalysisDiagnostic> Analyze(AnalysisResult analysis, CancellationToken cancellation)
    {
        var binder = new StaticBindings(analysis, cancellation);
        // Imported code runs in the caller's scope. Do not check it independently
        // against a fictitious closed module scope; its syntax is still checked.
        binder.Statements(analysis.Root.Program.Statements, binder._global);
        return binder._diagnostics;
    }

    private void Collect(IEnumerable<Statement> statements, Scope scope)
    {
        foreach (var statement in statements)
            switch (statement)
            {
                case ProcDeclaration p: scope.Names.Add(p.Name); break;
                case VariableDeclaration v: scope.Names.Add(v.Name); scope.Values.Add(v.Name); break;
                case SectionDeclaration s: scope.Names.Add(s.Name); break;
                case TupleDestructureStatement t:
                    foreach (var p in t.Patterns) { scope.Names.Add(p.Name); scope.Values.Add(p.Name); }
                    break;
            }
    }

    private Scope Child(IEnumerable<Statement> body, Scope parent, IEnumerable<string>? names = null)
    {
        var scope = new Scope(parent);
        Collect(body, scope);
        if (names is not null) { scope.Names.UnionWith(names); scope.Values.UnionWith(names); }
        return scope;
    }

    private void Report(string code, string message, AstNode node)
    {
        // Warnings describe source concerns, including unreachable/deferred code;
        // they do not claim that an execution necessarily encounters the problem.
        var span = _analysis.Root.Tokens.FirstOrDefault(t => t.Location == node.Location)?.EffectiveSpan
            ?? Span.At(node.Location);
        _diagnostics.Add(new(code, FlowDiagnostic.Warning(message, span)));
    }

    private void Name(string name, AstNode node, Scope scope)
    {
        if (!_openImports && !scope.Contains(name))
            Report("flow.binding.unknown", $"Name '{name}' has no visible declaration; runtime-dependent bindings are not inferred", node);
    }

    private void Statements(IEnumerable<Statement> statements, Scope scope)
    {
        // A nested import may introduce arbitrary names into this scope. Fail open
        // on unresolved names; retain syntax and explicit call-contract checks.
        var previousOpen = _openImports;
        var body = statements.ToArray();
        if (body.Any(s => s is ImportStatement) && scope != _global) _openImports = true;
        foreach (var statement in body)
        {
            _cancellation.ThrowIfCancellationRequested();
            switch (statement)
            {
                case ProcDeclaration p when !p.IsInternal:
                    var previousStrict = _strict;
                    _strict = p.IsStrict;
                    Statements(p.Body, Child(p.Body, _global, p.Parameters.Select(x => x.Name)));
                    _strict = previousStrict;
                    break;
                case VariableDeclaration v: Expression(v.Value, scope); break;
                case AssignmentStatement a: Name(a.Name, a, scope); Expression(a.Value, scope); break;
                case TupleDestructureStatement t: Expression(t.Value, scope); break;
                case ExpressionStatement e: Expression(e.Expression, scope); break;
                case ReturnStatement r: Expression(r.Value, scope); break;
                case ForStatement f:
                    Expression(f.Collection, scope);
                    Statements(f.Body, Child(f.Body, scope, new[] { f.VariableName })); break;
                case WhileStatement w:
                    Expression(w.Condition, scope); Statements(w.Body, Child(w.Body, scope)); break;
                case MusicalContextStatement m:
                    Expression(m.Value, scope); Expression(m.Value2, scope);
                    Statements(m.Body, Child(m.Body, scope)); break;
                case TuningContextStatement t:
                    Expression(t.TuningExpr, scope); Statements(t.Body, Child(t.Body, scope)); break;
                case LiveBlockStatement l:
                    Expression(l.QuantizeValue, scope); Statements(l.Body, Child(l.Body, scope)); break;
                case SectionDeclaration s:
                    var section = Child(s.Body, scope);
                    foreach (var p in s.Parameters ?? []) Pattern(p, section);
                    foreach (var e in s.DefaultValues ?? []) Expression(e, scope);
                    Statements(s.Body, section); break;
            }
        }
        _openImports = previousOpen;
    }

    private void Pattern(Pattern pattern, Scope scope)
    {
        switch (pattern)
        {
            case BindingPattern b: scope.Names.Add(b.Name); scope.Values.Add(b.Name); break;
            case ConstructorPattern c:
                foreach (var p in c.SubPatterns) Pattern(p, scope); break;
            case GuardPattern g: Pattern(g.Inner, scope); Expression(g.GuardExpression, scope); break;
        }
    }

    private void Expression(Expression? expression, Scope scope)
    {
        _cancellation.ThrowIfCancellationRequested();
        switch (expression)
        {
            case VariableExpression v: Name(v.Name, v, scope); break;
            case FunctionCallExpression call: Call(call, scope); break;
            case LambdaExpression l:
                Statements(l.Body, Child(l.Body, scope, l.Parameters.Select(p => p.Name))); break;
            case LazyExpression l: Expression(l.InnerExpression, scope); break;
            case FlowExpression f:
                Expression(f.Left, scope);
                if (f.IntermediateName is { } name) { scope.Names.Add(name); scope.Values.Add(name); }
                if (f.Right is FunctionCallExpression flowCall) Call(flowCall, scope, new[] { f.Left });
                else if (f.Right is VariableExpression v)
                    Call(new(v.Location, v.Name, Array.Empty<Expression>()), scope, new[] { f.Left });
                else Expression(f.Right, scope);
                break;
            case TupleUnpackFlowExpression t:
                Expression(t.Left, scope);
                // Spread arity is dynamic; only inspect explicit arguments/head.
                if (t.Right is FunctionCallExpression unpackCall) Call(unpackCall, scope, checkSignature: false);
                else Expression(t.Right, scope);
                break;
            case ArrayLiteralExpression a: foreach (var e in a.Elements) Expression(e, scope); break;
            case TupleLiteralExpression t: foreach (var e in t.Elements) Expression(e, scope); break;
            case ArrayIndexExpression a: Expression(a.Array, scope); Expression(a.Index, scope); break;
            case MemberAccessExpression m: Expression(m.Object, scope); break;
            case InterpolatedStringExpression i: foreach (var e in i.Parts) Expression(e, scope); break;
            case MatchExpression m:
                Expression(m.Scrutinee, scope);
                foreach (var arm in m.Arms)
                {
                    var armScope = new Scope(scope);
                    Pattern(arm.Pattern, armScope); Expression(arm.Body, armScope);
                }
                break;
            // Domain expressions and section expansion require domain/runtime facts.
        }
    }

    private void Call(FunctionCallExpression call, Scope scope, IReadOnlyList<Expression>? prefix = null,
        bool checkSignature = true)
    {
        foreach (var arg in call.Arguments) Expression(arg, scope);
        foreach (var arg in call.NamedArgs?.Values ?? []) Expression(arg, scope);
        // Qualified calls and higher-order values have runtime dispatch semantics.
        if (call.Name.Contains('.')) return;
        Name(call.Name, call, scope);
        if (!checkSignature || !_procedures.TryGetValue(call.Name, out var candidates)
            || candidates.Any(p => p.IsInternal) || scope.ShadowsProcedure(call.Name)) return;
        // A variable can shadow a procedure with a function value.
        if (_analysis.Modules.Any(m => m.Syntax.Program.Statements.OfType<VariableDeclaration>()
                .Any(v => v.Name == call.Name))) return;
        var args = (prefix ?? []).Concat(call.Arguments).ToArray();
        var shaped = candidates.Where(p => Fits(p.Signature, args.Length, call.NamedArgs)).ToArray();
        if (shaped.Length == 0)
        {
            Report("flow.call.arguments", $"No declared overload of '{call.Name}' accepts these argument counts/names", call);
            return;
        }
        if (call.NamedArgs is { Count: > 0 }) return; // only positional literal types in this pass
        var types = args.Select(LiteralType).ToArray();
        if (types.Any(t => t is null) || shaped.Any(p => p.Signature.InputTypes.Any(t => !IsPrimitive(t)))) return;
        if (!shaped.Any(p => p.Signature.Matches(types.Select(t => t!).ToArray(), _strict)))
            Report("flow.call.types", $"No declared overload of '{call.Name}' accepts these literal argument types", call);
    }

    private static bool Fits(FunctionSignature signature, int count, IReadOnlyDictionary<string, Expression>? named)
    {
        if (named is null || named.Count == 0)
            return signature.IsVarArgs ? count >= signature.InputTypes.Count - 1 : count == signature.InputTypes.Count;
        // Runtime named-varargs binding has additional rules; leave it unknown.
        if (signature.IsVarArgs) return true;
        if (count + named.Count != signature.InputTypes.Count || signature.ParameterNames is null) return false;
        return named.Keys.All(n => signature.ParameterNames.Skip(count).Contains(n, StringComparer.Ordinal));
    }

    private static bool IsPrimitive(FlowType type) => type is IntType or LongType or FloatType or DoubleType
        or NumberType or BoolType or StringType or VoidType;

    private static FlowType? LiteralType(Expression expression) => expression is LiteralExpression { IsMusicLiteral: false } literal
        ? literal.Value switch
        {
            int => IntType.Instance, long => LongType.Instance, float => FloatType.Instance,
            double => DoubleType.Instance, bool => BoolType.Instance, string => StringType.Instance,
            System.Numerics.BigInteger => NumberType.Instance, _ => null,
        } : null;
}
