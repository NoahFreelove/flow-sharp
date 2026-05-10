using FlowLang.Core;
using FlowLang.TypeSystem;

namespace FlowLang.Ast;

/// <summary>
/// Base class for all AST nodes.
/// </summary>
public abstract record AstNode(SourceLocation Location)
{
    /// <summary>
    /// The type of this node (determined during type checking).
    /// </summary>
    public FlowType? ResolvedType { get; init; }
}

/// <summary>
/// Base class for all expression nodes.
/// </summary>
public abstract record Expression(SourceLocation Location) : AstNode(Location);

/// <summary>
/// Base class for all statement nodes.
/// </summary>
public abstract record Statement(SourceLocation Location) : AstNode(Location);
