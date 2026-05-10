using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Interpreter;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using Xunit;

namespace FlowLang.Tests.Unit;

/// <summary>
/// FIX-06 regression tests: Thunk caches both successful values and exceptions.
/// After the Lazy&lt;Value&gt; refactor, failed evaluators must re-throw the SAME
/// exception with the original stack trace preserved (ExceptionDispatchInfo
/// semantics), and the evaluator must be invoked exactly once regardless of
/// how many times Force() is called.
/// </summary>
public class ThunkTests
{
    /// <summary>
    /// Minimal test double. Overrides the (newly-virtual) Evaluate hook so
    /// the test can inject success-/failure-producing lambdas and count
    /// evaluator invocations — verifying the "invoked exactly once" contract.
    /// </summary>
    private sealed class CountingEvaluator : ExpressionEvaluator
    {
        private readonly Func<Value> _thunk;
        public int CallCount { get; private set; }

        public CountingEvaluator(Func<Value> thunk)
            : base(
                new FlowLang.Runtime.ExecutionContext(new ErrorReporter(), new InternalFunctionRegistry()),
                new ErrorReporter(),
                new NoopInvoker())
        {
            _thunk = thunk;
        }

        public override Value Evaluate(Expression expression)
        {
            CallCount++;
            return _thunk();
        }
    }

    private sealed class NoopInvoker : IFunctionInvoker
    {
        public Value ExecuteUserFunction(ProcDeclaration proc, IReadOnlyList<Value> args)
            => throw new NotSupportedException("NoopInvoker: test double");

        public Value ExecuteUserFunctionWithCaptures(
            ProcDeclaration proc,
            IReadOnlyList<Value> args,
            IReadOnlyDictionary<string, Value>? capturedVariables)
            => throw new NotSupportedException("NoopInvoker: test double");
    }

    // Placeholder expression — CountingEvaluator.Evaluate ignores its argument,
    // so any concrete Expression subtype will do. LiteralExpression is the
    // simplest and requires no evaluation context to construct.
    private static readonly Expression FakeExpression =
        new LiteralExpression(SourceLocation.Unknown, 0);

    [Fact]
    public void Force_CachesSuccessValue()
    {
        var evaluator = new CountingEvaluator(() => Value.Int(42));
        var thunk = new Thunk(FakeExpression, evaluator);

        Assert.Equal(42, thunk.Force().As<int>());
        Assert.Equal(42, thunk.Force().As<int>());
        Assert.Equal(1, evaluator.CallCount); // evaluator invoked ONCE across two Force calls
        Assert.True(thunk.IsEvaluated);
    }

    [Fact]
    public void Force_CachesExceptionAndRethrows()
    {
        var evaluator = new CountingEvaluator(() =>
            throw new InvalidOperationException("boom"));
        var thunk = new Thunk(FakeExpression, evaluator);

        var first = Assert.Throws<InvalidOperationException>(() => thunk.Force());
        var second = Assert.Throws<InvalidOperationException>(() => thunk.Force());

        Assert.Equal("boom", first.Message);
        Assert.Equal("boom", second.Message);
        // Per Lazy<T> with ExecutionAndPublication: same exception instance is cached
        // and rethrown via ExceptionDispatchInfo on every subsequent .Value access.
        Assert.Same(first, second);
        // Evaluator called ONCE — failure is cached, never retried.
        Assert.Equal(1, evaluator.CallCount);
        // NOTE: IsEvaluated (Lazy.IsValueCreated) returns false after a factory-thrown
        // exception — per Microsoft docs, IsValueCreated only flips true on successful
        // materialization. Failure caching is orthogonal, and is verified by the
        // Assert.Same(first, second) + CallCount == 1 checks above. This mirrors the
        // pre-refactor behavior, where _isEvaluated was also only set on the success path.
        Assert.False(thunk.IsEvaluated);
    }

    [Fact]
    public void Force_RethrowPreservesStackTrace()
    {
        var evaluator = new CountingEvaluator(() =>
            throw new InvalidOperationException("boom"));
        var thunk = new Thunk(FakeExpression, evaluator);

        Exception? captured = null;
        try { thunk.Force(); } catch (Exception ex) { captured = ex; }

        Assert.NotNull(captured);
        Assert.NotNull(captured!.StackTrace);
        // Original frame — the CountingEvaluator lambda that threw — must appear
        // in the stack trace. If Lazy<T> were swallowing and re-wrapping the
        // exception instead of using ExceptionDispatchInfo, the frame would be lost.
        Assert.Contains("CountingEvaluator", captured.StackTrace!);
    }

    [Fact]
    public void Force_EvaluatorInvokedExactlyOnce_EvenWhenThrowing()
    {
        var evaluator = new CountingEvaluator(() =>
            throw new InvalidOperationException("boom"));
        var thunk = new Thunk(FakeExpression, evaluator);

        // Call Force multiple times to confirm failure caching is durable.
        for (int i = 0; i < 5; i++)
        {
            Assert.Throws<InvalidOperationException>(() => thunk.Force());
        }
        Assert.Equal(1, evaluator.CallCount);
    }
}
