namespace Atc.Rest.Api.Generator.Extensions;

/// <summary>
/// Extension members for C# type checking and classification.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class StringExtensions
{
    /// <param name="csharpType">The C# type string (e.g., "int", "long?", "string").</param>
    extension(string csharpType)
    {
        /// <summary>
        /// Determines if a C# type string represents a value type.
        /// </summary>
        /// <returns>True if the type is a value type (including nullable value types), false otherwise.</returns>
        public bool IsValueType()
            => CSharpTypeHelper.IsBasicValueType(csharpType);

        /// <summary>
        /// Determines if a C# type string represents a nullable value type.
        /// </summary>
        /// <returns>True if the type is a nullable value type, false otherwise.</returns>
        public bool IsNullableValueType()
            => CSharpTypeHelper.IsBasicValueType(csharpType) &&
               CSharpTypeHelper.IsNullable(csharpType);

        /// <summary>
        /// Determines if a C# type string represents an array type.
        /// </summary>
        /// <returns>True if the type is an array, false otherwise.</returns>
        public bool IsArrayType()
            => csharpType.EndsWith("[]", StringComparison.Ordinal);

        /// <summary>
        /// Determines if a C# type string represents a reference type.
        /// </summary>
        /// <returns>True if the type is a reference type (including arrays), false otherwise.</returns>
        public bool IsReferenceType()
            => !csharpType.IsValueType() ||
               csharpType.IsArrayType();
    }
}