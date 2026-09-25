using FlowLang.Diagnostics;
using FlowLang.Runtime;

namespace FlowLang.Core;

/// <summary>Host configuration for one <see cref="FlowEngine"/>.</summary>
public sealed record EngineOptions
{
    /// <summary>Receives <c>print</c> output. Null follows <see cref="Console.Out"/> at write time.</summary>
    public TextWriter? Output { get; init; }

    /// <summary>Receives advisories and warnings. Null follows <see cref="Console.Error"/> at write time.</summary>
    public TextWriter? Diagnostics { get; init; }

    /// <summary>
    /// One-shot advisory log. Null gives the engine its own, so each engine shows an
    /// advisory once. Hosts that recreate engines for one logical session (watch mode
    /// reloads) share a log so advisories stay once per session.
    /// </summary>
    public AdvisoryLog? Advisories { get; init; }

    /// <summary>Configuration snapshot. Null takes <see cref="FlowConfig.Active"/> at construction.</summary>
    public FlowConfigPoco? Config { get; init; }

    /// <summary>Host ceiling for loop iterations; scripts cannot raise their limit above it.</summary>
    public int MaxIterationsCeiling { get; init; } = 1_000_000;

    /// <summary>Writes <c>[verbose]</c> tracing to the diagnostic sink.</summary>
    public bool Verbose { get; init; }
}

/// <summary>Per-evaluation limits for <see cref="FlowEngine.Evaluate"/>.</summary>
public sealed record EvaluationOptions
{
    /// <summary>Cooperative cancellation requested by the host.</summary>
    public CancellationToken Cancellation { get; init; }

    /// <summary>Wall-clock budget; exceeding it cancels the evaluation as <see cref="EvaluationOutcome.TimedOut"/>.</summary>
    public TimeSpan? TimeLimit { get; init; }
}

public enum EvaluationOutcome
{
    /// <summary>The program ran and reported no errors.</summary>
    Succeeded,
    /// <summary>The program ran (or failed to parse) and reported errors.</summary>
    Failed,
    /// <summary>The host cancelled the evaluation.</summary>
    Cancelled,
    /// <summary>The evaluation exceeded <see cref="EvaluationOptions.TimeLimit"/>.</summary>
    TimedOut,
    /// <summary>An unexpected host failure prevented evaluation.</summary>
    HostFailure,
}

/// <summary>What one evaluation produced.</summary>
public sealed record EvaluationResult(
    EvaluationOutcome Outcome,
    IReadOnlyList<FlowError> Errors,
    IReadOnlyList<FlowDiagnostic> Diagnostics,
    Value? LastValue,
    TimeSpan Elapsed)
{
    public bool Succeeded => Outcome == EvaluationOutcome.Succeeded;
    /// <summary>Stable category codes with the original diagnostic spans/details.</summary>
    public IReadOnlyList<FlowLang.Analysis.AnalysisDiagnostic> CodedDiagnostics { get; init; } = [];
    /// <summary>Host-only debugging detail; adapters must not expose stack traces to scripts.</summary>
    public Exception? HostException { get; init; }
}
