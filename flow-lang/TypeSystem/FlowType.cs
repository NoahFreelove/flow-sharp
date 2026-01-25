namespace FlowLang.TypeSystem;

/// <summary>
/// Base class for all types in the Flow language.
/// </summary>
public abstract class FlowType : IEquatable<FlowType>
{
    /// <summary>
    /// The name of this type as it appears in source code.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Determines if a value of this type can be assigned to the target type.
    /// </summary>
    public virtual bool IsCompatibleWith(FlowType target)
    {
        return Equals(target);
    }

    /// <summary>
    /// Determines if this type can be implicitly converted to the target type.
    /// </summary>
    public virtual bool CanConvertTo(FlowType target)
    {
        return IsCompatibleWith(target);
    }

    /// <summary>
    /// Gets the specificity score for overload resolution.
    /// Higher scores indicate more specific types.
    /// </summary>
    public virtual int GetSpecificity()
    {
        return 100; // Base specificity
    }

    public virtual bool Equals(FlowType? other)
    {
        if (other is null) return false;
        return GetType() == other.GetType();
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as FlowType);
    }

    public override int GetHashCode()
    {
        return GetType().GetHashCode();
    }

    public override string ToString() => Name;

    public static bool operator ==(FlowType? left, FlowType? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(FlowType? left, FlowType? right)
    {
        return !(left == right);
    }
}
