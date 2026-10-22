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

    /// <summary>
    /// An object in the query, as OpenAPI serializes it: each property by its own key - <c>filter[name]</c>
    /// (<c>style: deepObject</c>) or <c>name</c> (<c>style: form</c>, <c>explode: true</c>, the default).
    /// </summary>
    /// <param name="ParameterName">The parameter's name.</param>
    /// <param name="DeepObject">Whether a property's key is <c>{parameter}[{property}]</c> (otherwise the property's name).</param>
    /// <param name="SchemaName">The component schema of the model.</param>
    /// <param name="Properties">The model's properties: wire name, schema, whether the model requires it.</param>
    public sealed record QueryObject(
        string ParameterName,
        bool DeepObject,
        string SchemaName,
        IReadOnlyList<QueryObjectProperty> Properties)
    {
        /// <summary>
        /// The query key a property is sent with.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns><c>filter[name]</c> or <c>name</c>.</returns>
        public string KeyOf(QueryObjectProperty property)
            => DeepObject ? $"{ParameterName}[{property?.WireName}]" : property?.WireName ?? string.Empty;
    }

    /// <summary>
    /// A property of a <see cref="QueryObject"/>.
    /// </summary>
    /// <param name="WireName">The property's name in the specification.</param>
    /// <param name="Schema">Its schema.</param>
    /// <param name="Required">Whether the model requires it.</param>
    public sealed record QueryObjectProperty(
        string WireName,
        IOpenApiSchema Schema,
        bool Required);

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
            if (valueKind == ParameterValueKind.Object && parameter.GetQueryObject() is not null)
            {
                return new ParameterSerialization(style, parameter.Explode, parameter.AllowReserved, valueKind, IsSupported: true);
            }

            // OpenApiParameter.Explode is a computed getter that derives the OpenAPI style-based default
            // (true for Form, false otherwise), applied at parse time too. No null-coalescing fallback
            // is needed; reading it directly is correct even when explode was not declared on the
            // parameter. (Verified against the YAML reader parse path.)
            var explode = parameter.Explode;
            var allowReserved = parameter.AllowReserved;

            var isSupported = style switch
            {
                ParameterStyle.Form => valueKind is ParameterValueKind.Primitive or ParameterValueKind.Array,

                // A query array whose items are one value with a pipe or a space between them (or repeated keys
                // with explode: true)
                ParameterStyle.PipeDelimited or ParameterStyle.SpaceDelimited => valueKind == ParameterValueKind.Array &&
                                                                                   parameter.In == ParameterLocation.Query,

                // A primitive, or a path or header array as its items joined by commas
                ParameterStyle.Simple => valueKind == ParameterValueKind.Primitive ||
                                         (valueKind == ParameterValueKind.Array && parameter.In is ParameterLocation.Path or ParameterLocation.Header),

                // OAS 3.2: style:cookie (RFC 6265) applies to in:cookie params; no percent-encoding,
                // semicolon-separated pairs. Supported for primitive and array values.
                ParameterStyle.Cookie => valueKind == ParameterValueKind.Primitive
                    || valueKind == ParameterValueKind.Array,
                _ => false,
            };

            return new ParameterSerialization(style, explode, allowReserved, valueKind, isSupported);
        }

        /// <summary>
        /// The parameter as an object in the query the generated code binds and sends - a model (a component schema)
        /// whose properties are each one value (a string, a number, a boolean, an enum), serialized as
        /// <c>style: deepObject</c> or as <c>style: form</c> with <c>explode: true</c> - or null. Nested objects and
        /// arrays have no serialization the specification defines there, and <c>form</c> without explode loses the
        /// property names to a reader that does not know them.
        /// </summary>
        /// <returns>The object, or null.</returns>
        public QueryObject? GetQueryObject()
        {
            if (parameter.In != ParameterLocation.Query ||
                string.IsNullOrEmpty(parameter.Name) ||
                parameter.Schema is not OpenApiSchemaReference { Target: OpenApiSchema model } reference ||
                reference.ComponentName is not { Length: > 0 } schemaName ||
                model.Properties is not { Count: > 0 } properties ||
                model.AllOf is { Count: > 0 } ||
                model.OneOf is { Count: > 0 } ||
                model.AnyOf is { Count: > 0 } ||
                (model.Type is { } type && !type.HasFlag(JsonSchemaType.Object)))
            {
                return null;
            }

            var style = parameter.Style ?? ParameterStyle.Form;
            var deepObject = style == ParameterStyle.DeepObject;
            if (!deepObject && !(style == ParameterStyle.Form && parameter.Explode))
            {
                return null;
            }

            var list = new List<QueryObjectProperty>();
            foreach (var property in properties)
            {
                if (!IsSingleValue(property.Value))
                {
                    return null;
                }

                list.Add(new QueryObjectProperty(property.Key, property.Value, model.Required?.Contains(property.Key) == true));
            }

            return new QueryObject(parameter.Name!, deepObject, schemaName, list);
        }

        /// <summary>
        /// Whether the generated code cannot bind or send the parameter's value: an object that is no flat model in the
        /// query sent as <c>deepObject</c> or <c>form</c> with <c>explode</c> (<see cref="GetQueryObject"/>), or a
        /// <c>oneOf</c>/<c>anyOf</c>/<c>allOf</c> without a type - or an array of either. A reference wrapped in an
        /// <c>allOf</c> is the reference. A parameter with a <c>content</c> map (no schema) is not judged here.
        /// </summary>
        /// <returns>True when the value cannot be bound or sent.</returns>
        public bool IsUnbindable()
        {
            if (parameter.In == ParameterLocation.QueryString ||
                parameter.Schema is null ||
                parameter.GetQueryObject() is not null)
            {
                return false;
            }

            // A label (.5) or matrix (;id=5) value: the generated code neither binds nor sends the prefix
            if (parameter.Style is ParameterStyle.Label or ParameterStyle.Matrix)
            {
                return true;
            }

            var schema = (IOpenApiSchema?)parameter.Schema.SingleWrappedReference() ?? parameter.Schema;
            if (schema is OpenApiSchemaReference && parameter.In == ParameterLocation.Query)
            {
                // A wrapped model in the query: judged as the model
                var unwrapped = new OpenApiParameter
                {
                    Name = parameter.Name,
                    In = parameter.In,
                    Style = parameter.Style,
                    Explode = parameter.Explode,
                    Schema = schema,
                };
                if (unwrapped.GetQueryObject() is not null)
                {
                    return false;
                }
            }

            return IsUnbindableValue(schema);
        }

        /// <summary>
        /// The character between the items of an array that is one value: <c>,</c> for a query array in <c>form</c>
        /// with <c>explode: false</c> and for a path or header array (<c>style: simple</c>, explode or not), <c>|</c>
        /// for <c>pipeDelimited</c> and a space for <c>spaceDelimited</c> (both without explode).
        /// <see langword="null"/> for a query array sent as repeated keys (<c>explode: true</c>) and for anything that
        /// is no array.
        /// </summary>
        /// <returns>The delimiter, or null.</returns>
        public char? GetArrayDelimiter()
        {
            if (GetValueKind(parameter.Schema) != ParameterValueKind.Array)
            {
                return null;
            }

            if (parameter.In is ParameterLocation.Path or ParameterLocation.Header)
            {
                return (parameter.Style ?? ParameterStyle.Simple) == ParameterStyle.Simple ? ',' : null;
            }

            if (parameter.In != ParameterLocation.Query ||
                parameter.Explode)
            {
                return null;
            }

            return (parameter.Style ?? ParameterStyle.Form) switch
            {
                ParameterStyle.Form => ',',
                ParameterStyle.PipeDelimited => '|',
                ParameterStyle.SpaceDelimited => ' ',
                _ => null,
            };
        }
    }

    // One value in a query: a primitive, an enum, a reference to either, or an enum wrapped in allOf - no object,
    // no array, no composition
    private static bool IsSingleValue(IOpenApiSchema? schema)
    {
        if (schema is null)
        {
            return false;
        }

        if (schema.WrappedEnum() is not null)
        {
            return true;
        }

        if (schema.Type is { } type &&
            (type.HasFlag(JsonSchemaType.Object) || type.HasFlag(JsonSchemaType.Array)))
        {
            return false;
        }

        return schema.Format is not ("byte" or "binary") &&
               schema.Properties is null or { Count: 0 } &&
               schema.Items is null &&
               schema.AllOf is null or { Count: 0 } &&
               schema.OneOf is null or { Count: 0 } &&
               schema.AnyOf is null or { Count: 0 };
    }

    // An object, or a composition without a type, or an array of either
    private static bool IsUnbindableValue(IOpenApiSchema? schema)
        => schema?.HasNoTextForm() == true;

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