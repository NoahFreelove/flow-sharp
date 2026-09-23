using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Patterns;
using FlowLang.Ast.Statements;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using RuntimeContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Interpreter;

/// <summary>
/// Resolves a member of a domain value. Returns true when the resolver owns the
/// value's type; <paramref name="value"/> is then the member, or null when the type
/// has no such member.
/// </summary>
public delegate bool MemberResolver(Value target, string member, out Value? value);

/// <summary>
/// How the interpreter evaluates grammar constructs whose meaning a domain library
/// supplies. Flow has one grammar (docs/decisions/2026-09-22-grammar-policy.md): note
/// streams, chords, songs, sections and context blocks always parse, and a domain
/// library (the music layer) binds what they do. A context without a binding reports
/// a located error for the construct instead of evaluating it.
/// </summary>
public sealed class DomainBindings
{
    private readonly Dictionary<Type, Func<Expression, ExpressionEvaluator, Value>> _expressions = new();
    private readonly Dictionary<Type, Action<Statement, Interpreter>> _statements = new();
    private readonly List<Func<string, Value?>> _literals = new();
    private readonly List<Func<string, Value?>> _constants = new();
    private readonly List<MemberResolver> _members = new();
    private readonly List<Func<FlowType, Value?>> _defaults = new();
    private readonly List<Action<RuntimeContext, string, Value, FlowType>> _declarationObservers = new();
    private readonly List<Func<object, Value, bool?>> _literalPatterns = new();
    private readonly List<Func<ConstructorPattern, Value, RuntimeContext, bool?>> _constructorPatterns = new();

    /// <summary>Binds the evaluation of expressions of type <typeparamref name="T"/>.</summary>
    public DomainBindings Expression<T>(Func<T, ExpressionEvaluator, Value> evaluate) where T : Expression
    {
        _expressions[typeof(T)] = (e, ev) => evaluate((T)e, ev);
        return this;
    }

    /// <summary>Binds the execution of statements of type <typeparamref name="T"/>.</summary>
    public DomainBindings Statement<T>(Action<T, Interpreter> execute) where T : Statement
    {
        _statements[typeof(T)] = (s, i) => execute((T)s, i);
        return this;
    }

    /// <summary>
    /// Parses a unit or pitch literal token (<c>C4</c>, <c>100ms</c>, <c>-6dB</c>).
    /// Tokens no parser accepts evaluate to their text as a String.
    /// </summary>
    public DomainBindings LiteralParser(Func<string, Value?> parse) { _literals.Add(parse); return this; }

    /// <summary>
    /// Resolves a bare identifier that is neither a variable nor a function
    /// (for example the duration names <c>q</c>, <c>e</c>, <c>h</c>).
    /// </summary>
    public DomainBindings Constant(Func<string, Value?> resolve) { _constants.Add(resolve); return this; }

    /// <summary>Resolves <c>value.Member</c> on domain values.</summary>
    public DomainBindings Members(MemberResolver resolve) { _members.Add(resolve); return this; }

    /// <summary>The value an uninitialized declaration of a domain type holds.</summary>
    public DomainBindings DefaultValue(Func<FlowType, Value?> create) { _defaults.Add(create); return this; }

    /// <summary>Observes every typed variable declaration (name, bound value, declared type).</summary>
    public DomainBindings DeclarationObserver(Action<RuntimeContext, string, Value, FlowType> observe)
    {
        _declarationObservers.Add(observe);
        return this;
    }

    /// <summary>
    /// Matches a literal pattern payload against a domain scrutinee. Returns null to
    /// leave the comparison to the language.
    /// </summary>
    public DomainBindings LiteralPattern(Func<object, Value, bool?> match) { _literalPatterns.Add(match); return this; }

    /// <summary>
    /// Matches a domain constructor pattern (chord, roman numeral, articulation).
    /// Returns null when the pattern is not the resolver's.
    /// </summary>
    public DomainBindings ConstructorPattern(Func<ConstructorPattern, Value, RuntimeContext, bool?> match)
    {
        _constructorPatterns.Add(match);
        return this;
    }

    internal bool TryEvaluate(Expression expr, ExpressionEvaluator evaluator, out Value value)
    {
        if (_expressions.TryGetValue(expr.GetType(), out var evaluate))
        {
            value = evaluate(expr, evaluator);
            return true;
        }
        value = Value.Void();
        return false;
    }

    internal bool TryExecute(Statement stmt, Interpreter interpreter)
    {
        if (!_statements.TryGetValue(stmt.GetType(), out var execute))
            return false;
        execute(stmt, interpreter);
        return true;
    }

    internal Value? ParseLiteral(string text) => FirstNonNull(_literals, text);

    internal Value? ResolveConstant(string name) => FirstNonNull(_constants, name);

    internal bool TryResolveMember(Value target, string member, out Value? value)
    {
        foreach (var resolve in _members)
            if (resolve(target, member, out value))
                return true;
        value = null;
        return false;
    }

    internal Value? CreateDefault(FlowType type)
    {
        foreach (var create in _defaults)
            if (create(type) is { } value)
                return value;
        return null;
    }

    internal void OnDeclared(RuntimeContext context, string name, Value value, FlowType declaredType)
    {
        foreach (var observe in _declarationObservers)
            observe(context, name, value, declaredType);
    }

    internal bool? MatchLiteralPattern(object payload, Value scrutinee)
    {
        foreach (var match in _literalPatterns)
            if (match(payload, scrutinee) is { } result)
                return result;
        return null;
    }

    internal bool? MatchConstructorPattern(ConstructorPattern pattern, Value scrutinee, RuntimeContext context)
    {
        foreach (var match in _constructorPatterns)
            if (match(pattern, scrutinee, context) is { } result)
                return result;
        return null;
    }

    private static Value? FirstNonNull(List<Func<string, Value?>> functions, string text)
    {
        foreach (var f in functions)
            if (f(text) is { } value)
                return value;
        return null;
    }

    /// <summary>
    /// A readable name for an unbound construct: <c>NoteStreamExpression</c> →
    /// "note stream".
    /// </summary>
    internal static string DescribeConstruct(Type nodeType)
    {
        var name = nodeType.Name;
        foreach (var suffix in new[] { "Expression", "Statement", "Declaration" })
            if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
            {
                name = name[..^suffix.Length];
                break;
            }
        var sb = new System.Text.StringBuilder();
        foreach (var c in name)
        {
            if (char.IsUpper(c) && sb.Length > 0) sb.Append(' ');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
