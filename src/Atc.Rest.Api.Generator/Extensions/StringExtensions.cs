namespace Atc.Rest.Api.Generator.Extensions;

/// <summary>
/// Extension methods for C# type checking and classification.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Determines if a C# type string represents a value type.
    /// </summary>
    /// <param name="csharpType">The C# type string (e.g., "int", "long?", "string").</param>
    /// <returns>True if the type is a value type (including nullable value types), false otherwise.</returns>
    public static bool IsValueType(this string csharpType)
        => CSharpTypeHelper.IsBasicValueType(csharpType);

    /// <summary>
    /// Determines if a C# type string represents a nullable value type.
    /// </summary>
    /// <param name="csharpType">The C# type string (e.g., "int?", "long?").</param>
    /// <returns>True if the type is a nullable value type, false otherwise.</returns>
    public static bool IsNullableValueType(this string csharpType)
        => CSharpTypeHelper.IsBasicValueType(csharpType) &&
           CSharpTypeHelper.IsNullable(csharpType);

    /// <summary>
    /// Determines if a C# type string represents an array type.
    /// </summary>
    /// <param name="csharpType">The C# type string (e.g., "string[]", "int[]").</param>
    /// <returns>True if the type is an array, false otherwise.</returns>
    public static bool IsArrayType(this string csharpType)
        => csharpType.EndsWith("[]", StringComparison.Ordinal);

    /// <summary>
    /// Determines if a C# type string represents a reference type.
    /// </summary>
    /// <param name="csharpType">The C# type string.</param>
    /// <returns>True if the type is a reference type (including arrays), false otherwise.</returns>
    public static bool IsReferenceType(this string csharpType)
        => !csharpType.IsValueType() ||
           csharpType.IsArrayType();
}