using FlowLang.Core;

namespace FlowLang.Ast.Statements;

/// <summary>
/// The type of musical context being set.
/// </summary>
public enum MusicalContextType { Timesig, Tempo, Swing, Key, Dynamics }

/// <summary>
/// A musical context block statement that sets tempo, time signature, swing, or key
/// for its body scope. e.g., tempo 120 { ... } or timesig 4/4 { ... }
/// </summary>
public record MusicalContextStatement(
    SourceLocation Location,
    MusicalContextType ContextType,
    Expression Value,                    // The value expression (e.g., 120 for tempo, 4 for timesig numerator)
    Expression? Value2,                  // Optional second value (denominator for timesig)
    IReadOnlyList<Statement> Body
) : Statement(Location);
