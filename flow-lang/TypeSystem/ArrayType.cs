namespace FlowLang.TypeSystem;

/// <summary>
/// Represents an array type with an element type.
/// </summary>
public sealed class ArrayType : FlowType
{
    public FlowType ElementType { get; }

    public ArrayType(FlowType elementType)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
    }

    public override string Name => $"{ElementType.Name}[]";

    public override bool Equals(FlowType? other)
    {
        return other is ArrayType arrayType
            && ElementType.Equals(arrayType.ElementType);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GetType(), ElementType);
    }

    public override bool IsCompatibleWith(FlowType target)
    {
        // Arrays are compatible if element types are compatible
        if (target is not ArrayType targetArray)
            return false;

        // Special case: Void[] is compatible with any array type (empty arrays)
        if (ElementType is PrimitiveTypes.VoidType)
            return true;

        // Special case: Void[] target accepts any array type
        if (targetArray.ElementType is PrimitiveTypes.VoidType)
            return true;

        return ElementType.IsCompatibleWith(targetArray.ElementType);
    }

    public override bool CanConvertTo(FlowType target)
    {
        // Arrays can convert if element types can convert
        if (target is not ArrayType targetArray)
            return false;

        // Special case: Any array can convert to Void[]
        if (targetArray.ElementType is PrimitiveTypes.VoidType)
            return true;

        return ElementType.CanConvertTo(targetArray.ElementType);
    }

    public override int GetSpecificity()
    {
        // Array specificity is based on element type specificity
        return ElementType.GetSpecificity() + 50;
    }
}
