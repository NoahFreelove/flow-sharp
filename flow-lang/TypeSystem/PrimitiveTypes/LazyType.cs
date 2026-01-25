namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents a lazy-evaluated value of any type.
/// </summary>
public class LazyType : FlowType
{
    public FlowType InnerType { get; }

    public LazyType(FlowType innerType)
    {
        InnerType = innerType ?? throw new ArgumentNullException(nameof(innerType));
    }

    public override string Name => $"Lazy<{InnerType.Name}>";

    public override bool IsCompatibleWith(FlowType other)
    {
        if (other is not LazyType lazyOther)
            return false;

        // Allow any Lazy<T> to match Lazy<Void> (wildcard for function overloading)
        if (lazyOther.InnerType is VoidType)
            return true;

        return InnerType.IsCompatibleWith(lazyOther.InnerType);
    }

    public override bool Equals(object? obj)
    {
        return obj is LazyType other && InnerType.Equals(other.InnerType);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(nameof(LazyType), InnerType);
    }
}
