using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using System.Numerics;

namespace FlowLang.Runtime;

/// <summary>
/// Wraps a CLR value with Flow type information.
/// </summary>
public class Value
{
    public object? Data { get; }
    public FlowType Type { get; }

    public Value(object? data, FlowType type)
    {
        Data = data;
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }

    // Factory methods for common types
    public static Value Void() => new(null, VoidType.Instance);
    public static Value Int(int value) => new(value, IntType.Instance);
    public static Value Float(double value) => new(value, FloatType.Instance);
    public static Value Long(long value) => new(value, LongType.Instance);
    public static Value Double(double value) => new(value, DoubleType.Instance);
    public static Value String(string value) => new(value, StringType.Instance);
    public static Value Bool(bool value) => new(value, BoolType.Instance);
    public static Value Number(BigInteger value) => new(value, NumberType.Instance);
    public static Value Buffer(object? value = null) => new(value, BufferType.Instance);
    public static Value Note(string value) => new(value, NoteType.Instance);
    public static Value Semitone(int value) => new(value, SemitoneType.Instance);
    public static Value Millisecond(double value) => new(value, MillisecondType.Instance);
    public static Value Second(double value) => new(value, SecondType.Instance);
    public static Value Decibel(double value) => new(value, DecibelType.Instance);

    public static Value Array(IReadOnlyList<Value> elements, FlowType elementType)
    {
        return new Value(elements, new ArrayType(elementType));
    }

    /// <summary>
    /// Converts this value to another type if possible.
    /// </summary>
    public Value ConvertTo(FlowType targetType)
    {
        if (Type.Equals(targetType))
            return this;

        // Numeric conversions
        if (Data is int intVal)
        {
            if (targetType is LongType) return Long(intVal);
            if (targetType is FloatType) return Float(intVal);
            if (targetType is DoubleType) return Double(intVal);
            if (targetType is NumberType) return Number(new BigInteger(intVal));
        }

        if (Data is long longVal)
        {
            if (targetType is DoubleType) return Double(longVal);
            if (targetType is NumberType) return Number(new BigInteger(longVal));
        }

        if (Data is double doubleVal)
        {
            if (targetType is NumberType) return Number(new BigInteger(doubleVal));
        }

        // Time conversions
        if (Type is MillisecondType && targetType is SecondType && Data is double msVal)
        {
            return Second(msVal / 1000.0);
        }

        if (Type is SecondType && targetType is MillisecondType && Data is double secVal)
        {
            return Millisecond(secVal * 1000.0);
        }

        // Array conversions - Void[] can convert to any array type (empty arrays)
        if (Type is ArrayType sourceArray && targetType is ArrayType targetArray)
        {
            if (sourceArray.ElementType is TypeSystem.PrimitiveTypes.VoidType)
            {
                // Convert Void[] to T[] by returning a new array with the target element type
                var arrayData = Data as IReadOnlyList<Value> ?? throw new InvalidCastException("Expected array data");
                return Array(arrayData, targetArray.ElementType);
            }
        }

        throw new InvalidCastException($"Cannot convert {Type} to {targetType}");
    }

    /// <summary>
    /// Gets the CLR value as a specific type.
    /// </summary>
    public T As<T>() => (T)Data!;

    /// <summary>
    /// Gets the CLR value as a specific type, or default if null or wrong type.
    /// </summary>
    public T? AsOrDefault<T>() => Data is T t ? t : default;

    public override string ToString()
    {
        if (Data is null) return "void";
        if (Data is string str) return $"\"{str}\"";
        if (Data is bool b) return b ? "true" : "false";
        if (Data is IReadOnlyList<Value> arr)
            return $"[{string.Join(", ", arr.Select(v => v.ToString()))}]";
        return Data.ToString() ?? "null";
    }
}
