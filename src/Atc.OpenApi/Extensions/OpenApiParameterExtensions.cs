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

        /// <summary>
        /// Where the parameter is sent (a reference by its target). A parameter is identified by its name and its
        /// location together: <c>id</c> in the path and <c>id</c> in the query are two parameters.
        /// </summary>
        /// <returns>The location, or <see langword="null"/> when the parameter cannot be resolved.</returns>
        public ParameterLocation? GetLocation()
            => parameterInterface switch
            {
                OpenApiParameter param => param.In,
                OpenApiParameterReference { Target: OpenApiParameter target } => target.In,
                _ => null,
            };
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
            if (parameter is null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            if (parameter.Schema is null)
            {
                return "string";
            }

            return parameter.Schema.ToCSharpType(parameter.Required);
        }

        /// <summary>
        /// Gets the binding attribute name for the parameter based on its location.
        /// Returns null for Cookie parameters — ASP.NET Core has no [FromCookie] attribute.
        /// </summary>
        /// <returns>The attribute name (FromQuery, FromRoute, FromHeader), or null for Cookie.</returns>
        public string? GetBindingAttributeName()
        {
            if (parameter is null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return parameter.In switch
            {
                ParameterLocation.Query => "FromQuery",
                ParameterLocation.Path => "FromRoute",
                ParameterLocation.Header => "FromHeader",
                ParameterLocation.Cookie => null,
                _ => "FromQuery",
            };
        }

        /// <summary>
        /// Determines if the parameter type is a value type.
        /// </summary>
        /// <returns>True if the parameter is a value type.</returns>
        public bool IsValueType()
        {
            if (parameter is null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            if (parameter.Schema is not OpenApiSchema schema || schema.Type is null)
            {
                return false;
            }

            // Strip the Null flag before checking (OpenAPI 3.1 uses combined flags for nullable types)
            var typeValue = schema.Type.Value;
            if (typeValue.HasFlag(JsonSchemaType.Null))
            {
                typeValue &= ~JsonSchemaType.Null;
            }

            return typeValue is JsonSchemaType.Integer
                or JsonSchemaType.Number
                or JsonSchemaType.Boolean;
        }

        /// <summary>
        /// Computes the effective style/explode/allowReserved and value-kind for the parameter,
        /// and whether this generator serializes that combination correctly (form primitive any
        /// explode; form array with explode:true; simple primitive). Unsupported combinations
        /// return <c>IsSupported = false</c> so callers can warn and fall back to default form
        /// serialization.
        /// </summary>
        public ParameterSerialization GetParameterSerialization()
        {
            var valueKind = GetValueKind(parameter.Schema);
            var style = parameter.Style ?? DefaultStyleFor(parameter.In);

            // OpenApiParameter.Explode is a computed getter that derives the OpenAPI style-based default
            // (true for Form, false otherwise), applied at parse time too. No null-coalescing fallback
            // is needed; reading it directly is correct even when explode was not declared on the
            // parameter. (Verified against the YAML reader parse path.)
            var explode = parameter.Explode;
            var allowReserved = parameter.AllowReserved;

            var isSupported = style switch
            {
                ParameterStyle.Form => valueKind == ParameterValueKind.Primitive
                    || (valueKind == ParameterValueKind.Array && explode),
                ParameterStyle.Simple => valueKind == ParameterValueKind.Primitive,

                // OAS 3.2: style:cookie (RFC 6265) applies to in:cookie params; no percent-encoding,
                // semicolon-separated pairs. Supported for primitive and array values.
                ParameterStyle.Cookie => valueKind == ParameterValueKind.Primitive
                    || valueKind == ParameterValueKind.Array,
                _ => false,
            };

            return new ParameterSerialization(style, explode, allowReserved, valueKind, isSupported);
        }
    }

    private static ParameterStyle DefaultStyleFor(ParameterLocation? location)
        => location is ParameterLocation.Query or ParameterLocation.Cookie
            ? ParameterStyle.Form
            : ParameterStyle.Simple;

    private static ParameterValueKind GetValueKind(IOpenApiSchema? schema)
    {
        if (schema is null)
        {
            return ParameterValueKind.Primitive;
        }

        if (schema.Type?.HasFlag(JsonSchemaType.Array) == true)
        {
            return ParameterValueKind.Array;
        }

        if (schema.Type?.HasFlag(JsonSchemaType.Object) == true || schema.Properties is { Count: > 0 })
        {
            return ParameterValueKind.Object;
        }

        // $ref schemas are NOT a problem here: the Microsoft.OpenApi OpenApiSchemaReference proxy
        // transparently forwards Type/Properties to its target, so a $ref-to-array classifies as
        // Array and a $ref-to-object as Object via the branches above (locked by unit tests).
        // What still falls through to Primitive are genuine composition schemas — oneOf/anyOf/allOf
        // with Type==null and no Properties; classifying those is deferred (deepObject/object-query).
        return ParameterValueKind.Primitive;
    }
}