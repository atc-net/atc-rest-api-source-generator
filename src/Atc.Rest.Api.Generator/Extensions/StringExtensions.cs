namespace Atc.Rest.Api.Generator.Extensions;

/// <summary>
/// Extension members for C# type checking and classification.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "CA1708:Names should differ by more than case", Justification = "OK - CLang14 - multiple extension(...) blocks")]
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

    /// <param name="assemblyName">An assembly name, e.g. <c>Contoso.Api.Domain</c>.</param>
    extension(string assemblyName)
    {
        /// <summary>
        /// The last dot-separated term of an assembly name (<c>Domain</c>): the suffix of the generated
        /// <c>AddApiHandlersFrom…</c> and <c>AddApiValidatorsFrom…</c> registration methods.
        /// </summary>
        public string LastAssemblyNameTerm
        {
            get
            {
                var parts = assemblyName.Split('.');
                return parts[parts.Length - 1];
            }
        }
    }
}