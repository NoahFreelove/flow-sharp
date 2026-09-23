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

    /// <summary>
    /// Whether values of this type are usable as Dict keys (Phase 26.1).
    /// Default <c>false</c> — covers Buffer/Voice/Lazy/Function/Sequence/Track/Section/Song/Envelope/OscillatorState/Bar/etc.
    /// without per-class edits. Hashable types (Int, Long, Float, String, Symbol, Note, Chord — and recursively Tuple-of-hashables)
    /// override to <c>true</c>. Wave 4 (Dict) consumes this predicate at the type-annotation site.
    /// </summary>
    public virtual bool IsHashable() => false;

    /// <summary>
    /// True for unit-carrying quantities (dB, ms, s, cents, semitones, Hz, beats). The
    /// overload resolver scores a unit value landing in a bare numeric slot below a
    /// unit-preserving conversion. Domain types opt in; the language defines none.
    /// </summary>
    public virtual bool IsUnitQuantity => false;

    /// <summary>
    /// Lets a target type accept an implicit conversion from <paramref name="source"/>
    /// that the source type does not know about (for example a music enum backed by
    /// Int accepting Int). Consulted by <see cref="IntType"/>-style primitives.
    /// </summary>
    public virtual bool AcceptsConversionFrom(FlowType source) => false;

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
