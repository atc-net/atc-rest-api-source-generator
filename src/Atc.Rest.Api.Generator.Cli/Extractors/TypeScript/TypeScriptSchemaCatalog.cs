namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Which component schemas the TypeScript client emits, and as what - one rule for the plain models and the Zod
/// schemas, so a model always has its schema: an object (also an <c>allOf</c> without <c>type</c>) is a model, an
/// array, a dictionary or a named primitive is an alias, a string with <c>enum</c> values is an enum. A deprecated
/// schema is left out unless the configuration includes it.
/// </summary>
public static class TypeScriptSchemaCatalog
{
    /// <summary>
    /// The object schemas, each emitted as an interface (and a Zod object).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="includeDeprecated">Whether a deprecated schema is emitted.</param>
    /// <returns>The schemas by component name, in document order.</returns>
    public static IEnumerable<(string Name, OpenApiSchema Schema)> ObjectModels(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
        => Components(openApiDoc, includeDeprecated)
            .Where(c => !c.Schema.IsOfType(JsonSchemaType.Array) &&
                        !TypeScriptModelExtractor.IsRecordAliasSchema(c.Schema) &&
                        !IsStringEnum(c.Schema) &&
                        TypeScriptModelExtractor.IsObjectSchema(c.Schema));

    /// <summary>
    /// The alias schemas: an array, a dictionary (<c>additionalProperties</c> only) or a component that only names a
    /// primitive, each emitted as a type alias (and its Zod schema).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="includeDeprecated">Whether a deprecated schema is emitted.</param>
    /// <returns>The schemas by component name with their kind, in document order.</returns>
    public static IEnumerable<(string Name, OpenApiSchema Schema, TypeScriptAliasKind Kind)> Aliases(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        foreach (var (name, schema) in Components(openApiDoc, includeDeprecated))
        {
            if (TypeScriptModelExtractor.IsScalarAliasSchema(schema))
            {
                yield return (name, schema, TypeScriptAliasKind.Scalar);
            }
            else if (TypeScriptModelExtractor.IsRecordAliasSchema(schema))
            {
                yield return (name, schema, TypeScriptAliasKind.Record);
            }
            else if (schema.IsOfType(JsonSchemaType.Array))
            {
                yield return (name, schema, TypeScriptAliasKind.Array);
            }
        }
    }

    /// <summary>
    /// The string enums with their values (each value once, <c>null</c> left out); an enum without a value is left
    /// out.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="includeDeprecated">Whether a deprecated schema is emitted.</param>
    /// <returns>The schemas by component name with their values, in document order.</returns>
    public static IEnumerable<(string Name, OpenApiSchema Schema, List<string> Values)> StringEnums(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        foreach (var (name, schema) in Components(openApiDoc, includeDeprecated))
        {
            if (IsStringEnum(schema) && EnumValues(schema.Enum) is { } values)
            {
                yield return (name, schema, values);
            }
        }
    }

    /// <summary>
    /// The values of an <c>enum</c> list as strings: <c>null</c> (the OpenAPI 3.1 nullable enum) is no member, and a
    /// value listed twice is one member.
    /// </summary>
    /// <param name="enumValues">The enum nodes.</param>
    /// <returns>The values, or <see langword="null"/> when there is none.</returns>
    public static List<string>? EnumValues(IList<JsonNode>? enumValues)
    {
        if (enumValues is null || enumValues.Count == 0)
        {
            return null;
        }

        var result = new List<string>();
        foreach (var enumValue in enumValues)
        {
            if (enumValue is null || JsonNullSentinel.IsJsonNullSentinel(enumValue))
            {
                continue;
            }

            var value = enumValue.ToString().Trim('"');
            if (value.Length > 0 && !result.Contains(value, StringComparer.Ordinal))
            {
                result.Add(value);
            }
        }

        return result.Count > 0 ? result : null;
    }

    private static bool IsStringEnum(OpenApiSchema schema)
        => schema.IsOfType(JsonSchemaType.String) && schema.Enum is { Count: > 0 };

    private static IEnumerable<(string Name, OpenApiSchema Schema)> Components(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        foreach (var component in openApiDoc.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            if (component.Value is OpenApiSchema schema &&
                (includeDeprecated || !schema.Deprecated))
            {
                yield return (component.Key, schema);
            }
        }
    }
}