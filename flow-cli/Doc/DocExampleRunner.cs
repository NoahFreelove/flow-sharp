using FlowLang.Diagnostics;
using FlowLang.Core;

namespace FlowCli.Doc;

// Phase 41 Plan 41-03 DOC-02 — executes each `///` code example in-process and
// annotates failures.
//
// Per CONTEXT D-10 the doc-example default form is a BARE EXPRESSION that simply
// "runs without error" (RESEARCH Open Q3 recommended default). We reuse the
// proven in-process pattern from flow-cli/Commands/TestCommand.cs:79-107:
//
//   using var engine = new FlowEngine(verbose: false);
//   bool ok = engine.Execute(source, "<doc-example>");
//   if (!ok || engine.ErrorReporter.HasErrors) { /* [example failed] */ }
//
// A fresh FlowEngine per example IS the hermetic isolation — every TestRegistry,
// musical-context stack, voice pool, PRNG, and binding table is engine-scoped,
// so two examples cannot leak into each other (the same isolation guarantee the
// Phase 35 TestRunner provides per (test ...) block, here at engine granularity).
// We do NOT build a second isolation framework (D-10).
//
// Per D-10, audio/MIDI examples are judged by SUCCESSFUL RENDER (no error), NOT
// byte output — `(play ...)` / `(writeWav ...)` / `(writeMidi ...)` in an example
// is a pass if the engine executed it without accumulating an error. We never
// compare bytes (platform-portable).
//
// stdout from an example's own `(print ...)` is suppressed during generation so
// `flow doc` output stays clean; stderr advisories likewise. Only the pass/fail
// signal flows back into the DocModel.
//
// T-41-03-DOS mitigation: each example runs under a wall-clock budget
// (DefaultExampleTimeoutMs) on a worker thread; a runaway example is annotated
// `[example failed] timed out` rather than hanging the whole `flow doc` run.
public sealed class DocExampleRunner
{
    public const int DefaultExampleTimeoutMs = 30_000;

    private readonly int _timeoutMs;

    public DocExampleRunner(int timeoutMs = DefaultExampleTimeoutMs)
    {
        _timeoutMs = timeoutMs <= 0 ? DefaultExampleTimeoutMs : timeoutMs;
    }

    /// <summary>
    /// Returns the same models with each model's ExampleFailures populated: a
    /// per-example nullable list of the same length as Examples (null = pass,
    /// non-null = the [example failed] annotation text). Both emitters index
    /// ExampleFailures[i] directly under Examples[i] so a failure is always
    /// rendered beneath the example that caused it, regardless of how many
    /// other examples pass.
    /// </summary>
    public DocModel[] RunAll(IReadOnlyList<DocModel> models)
    {
        var result = new DocModel[models.Count];
        for (int i = 0; i < models.Count; i++)
        {
            var model = models[i];
            if (model.Examples.Count == 0)
            {
                result[i] = model;
                continue;
            }

            // Build a per-example nullable list: same length as Examples,
            // null at index j = example j passed, non-null = failure text.
            var perExample = new string?[model.Examples.Count];
            bool anyFailed = false;
            for (int j = 0; j < model.Examples.Count; j++)
            {
                var failure = RunOne(model.Examples[j]);
                perExample[j] = failure;
                if (failure is not null)
                    anyFailed = true;
            }

            if (!anyFailed)
            {
                result[i] = model;
                continue;
            }

            // Convert to IReadOnlyList<string> using empty string for passes
            // so emitters can use the index directly without a null check.
            // The emitters already guard `!string.IsNullOrEmpty` / present
            // the annotation only when non-empty.
            var failures = new string[model.Examples.Count];
            for (int j = 0; j < model.Examples.Count; j++)
                failures[j] = perExample[j] ?? string.Empty;

            result[i] = model.WithFailures(failures);
        }
        return result;
    }

    /// <summary>
    /// Execute a single example. Returns null on success, or the
    /// `[example failed]` annotation text (the formatted errors / timeout note)
    /// on failure.
    /// </summary>
    public string? RunOne(string exampleSource)
    {
        // The example's print/advisory output goes to discarded engine sinks, and the
        // time limit is enforced cooperatively, so a slow example stops rather than
        // being abandoned on a background thread.
        try
        {
            using var engine = new FlowEngine(new EngineOptions
            {
                Output = TextWriter.Null,
                Diagnostics = TextWriter.Null,
            });
            // Doc examples are snippets: like `flow -e`, they run with @std imported.
            engine.ImportInteractiveDefaults();
            var result = engine.Evaluate(exampleSource, "<doc-example>", new EvaluationOptions
            {
                TimeLimit = TimeSpan.FromMilliseconds(_timeoutMs),
            });
            if (result.Outcome == EvaluationOutcome.TimedOut)
                return $"[example failed] timed out after {_timeoutMs} ms";
            if (!result.Succeeded)
            {
                var formatted = engine.ErrorReporter.FormatAll(engine.SourceMap, useColor: false);
                return string.IsNullOrWhiteSpace(formatted)
                    ? "[example failed]"
                    : "[example failed] " + Flatten(formatted);
            }
            return null;
        }
        catch (Exception ex)
        {
            return "[example failed] " + Flatten(ex.Message);
        }
    }

    private static string Flatten(string s) =>
        s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
}
