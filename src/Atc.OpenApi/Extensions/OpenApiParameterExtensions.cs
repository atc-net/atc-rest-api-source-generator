// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable ConvertIfStatementToReturnStatement
namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for OpenApiParameter to handle parameter type mapping and metadata.
/// </summary>
[SuppressMessage("", "CA1024:Use properties where appropriate", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "CA1708:Names of 'Members'", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S1144:Remove the unused private method", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3928:The parameter name 'schema'", Justification = "OK - CLang14 - extension")]
public static class OpenApiParameterExtensions
{
    /// <summary>
    /// Result of resolving an OpenAPI parameter reference.
    /// </summary>
    /// <param name="Parameter">The resolved OpenAPI parameter, or null if resolution failed.</param>
    /// <param name="ReferenceId">The reference ID if this was a parameter reference, null otherwise.</param>
    public sealed record ResolvedParameter(OpenApiParameter? Parameter, string? ReferenceId);

    /// <param name="parameterInterface">The OpenAPI parameter interface (can be a reference or direct parameter).</param>
    extension(IOpenApiParameter parameterInterface)
    {
        /// <summary>
        /// Resolves an OpenAPI parameter interface to an actual parameter and optional reference ID.
        /// Handles both direct parameters and parameter references.
        /// </summary>
        /// <returns>A ResolvedParameter containing the resolved parameter and its reference ID (if applicable).</returns>
        public ResolvedParameter Resolve()
        {
            if (parameterInterface is OpenApiParameterReference paramRef)
            {
                return new ResolvedParameter(paramRef.Target as OpenApiParameter, paramRef.Reference.Id);
            }

            if (parameterInterface is OpenApiParameter directParam)
            {
                return new ResolvedParameter(directParam, null);
            }

            return new ResolvedParameter(null, null);
        }

        /// <summary>
        /// Gets the name of the parameter, resolving references if necessary.
        /// </summary>
        /// <returns>The parameter name, or null if resolution failed.</returns>
        public string? GetName()
        {
            if (parameterInterface is OpenApiParameter param)
            {
                return param.Name;
            }

            if (parameterInterface is OpenApiParameterReference paramRef)
            {
                var target = paramRef.Target as OpenApiParameter;
                return target?.Name;
            }

            return null;
        }
    }

    /// <param name="parameter">The OpenAPI parameter.</param>
    extension(OpenApiParameter parameter)
    {
        /// <summary>
        /// Maps an OpenAPI parameter to a C# type string.
        /// </summary>
        /// <returns>A C# type string representation.</returns>
        public string ToCSharpType()
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            if (parameter.Schema == null)
            {
                return "string";
            }

            return parameter.Schema.ToCSharpType(parameter.Required);
        }

        /// <summary>
        /// Gets the binding attribute name for the parameter based on its location.
        /// </summary>
        /// <returns>The attribute name (FromQuery, FromRoute, FromHeader, etc.).</returns>
        public string GetBindingAttributeName()
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return parameter.In switch
            {
                ParameterLocation.Query => "FromQuery",
                ParameterLocation.Path => "FromRoute",
                ParameterLocation.Header => "FromHeader",
                ParameterLocation.Cookie => "FromCookie",
                _ => "FromQuery",
            };
        }

        /// <summary>
        /// Determines if the parameter type is a value type.
        /// </summary>
        /// <returns>True if the parameter is a value type.</returns>
        public bool IsValueType()
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            if (parameter.Schema is not OpenApiSchema schema)
            {
                return false;
            }

            return schema.Type is JsonSchemaType.Integer
                or JsonSchemaType.Number
                or JsonSchemaType.Boolean;
        }

        /// <summary>
        /// Gets the default value for a parameter based on its type, required status, and schema default.
        /// </summary>
        /// <returns>The default value string or null.</returns>
        public string? GetDefaultValue()
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            var csharpType = parameter.ToCSharpType();

            // Nullable types don't need defaults
            if (csharpType.EndsWith("?", StringComparison.Ordinal))
            {
                return null;
            }

            // Check if schema has a default value
            var schemaDefault = ExtractSchemaDefault(parameter.Schema, csharpType);
            if (schemaDefault != null)
            {
                return schemaDefault;
            }

            // String handling - use string.Empty as fallback
            if (csharpType == "string")
            {
                return parameter.Required ? "string.Empty" : null;
            }

            // Value types that aren't nullable need default!
            if (parameter.IsValueType() && parameter.Required)
            {
                return "default!";
            }

            return null;
        }

        /// <summary>
        /// Extracts the default value from an OpenAPI schema and formats it as a C# literal.
        /// </summary>
        private static string? ExtractSchemaDefault(
            IOpenApiSchema? schemaInterface,
            string paramType)
        {
            if (schemaInterface is not OpenApiSchema schema)
            {
                return null;
            }

            var defaultValue = schema.Default;
            if (defaultValue == null)
            {
                return null;
            }

            // Format the default value based on the C# type
            var rawValue = defaultValue.ToString();
            if (string.IsNullOrEmpty(rawValue))
            {
                return null;
            }

            // String types - the code generation library will add quotes, so return unquoted value
            if (paramType == "string")
            {
                // The raw value from JSON may already be quoted - strip existing quotes
                if (rawValue.StartsWith("\"", StringComparison.Ordinal) &&
                    rawValue.EndsWith("\"", StringComparison.Ordinal) &&
                    rawValue.Length >= 2)
                {
                    return rawValue.Substring(1, rawValue.Length - 2);
                }

                return rawValue;
            }

            // Boolean values need lowercase in C#
            if (paramType == "bool")
            {
                return rawValue.ToLowerInvariant();
            }

            // Numeric types can use the raw value directly
            if (CSharpTypeHelper.IsNumericType(paramType))
            {
                return rawValue;
            }

            return null;
        }
    }
}