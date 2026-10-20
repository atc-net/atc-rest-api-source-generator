namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Where a value is an extensible enum: a string enum when the generation's string enums are extensible (a client, or a
/// server whose marker sets <c>extensibleEnums</c>). Such a type has a <c>TryParse</c> of its own (it is
/// <c>IParsable</c>) and takes any text, so a server binds it as any parsable value - not as a C# enum, whose wire
/// value is rewritten to its member name and checked against the members.
/// </summary>
public static class ExtensibleEnumHelper
{
    /// <summary>
    /// Whether the value - or a list's item - is a string enum the registry generates as an extensible enum.
    /// </summary>
    /// <param name="schema">The value's schema.</param>
    /// <param name="registry">The registry of the generation (its <see cref="TypeConflictRegistry.ExtensibleEnums"/>).</param>
    /// <returns><see langword="true"/> for an extensible enum, or a list of them.</returns>
    public static bool IsExtensibleEnum(
        IOpenApiSchema? schema,
        TypeConflictRegistry? registry)
    {
        if (registry?.ExtensibleEnums != true || schema is null)
        {
            return false;
        }

        var value = Resolve(schema);
        if (value is OpenApiSchema { Items: { } items } && value.IsOfType(JsonSchemaType.Array))
        {
            value = Resolve(items);
        }

        return value is { Enum.Count: > 0 } && value.IsOfType(JsonSchemaType.String);
    }

    /// <summary>
    /// The parameters of every operation whose value is an extensible enum (see <see cref="IsExtensibleEnum"/>), by the
    /// name of its parameters record and the parameter's wire name, with the default the specification gives it as its
    /// text (an extensible enum's member is no constant, so the wire-binding step sends the default as if it was sent).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="registry">The registry of the generation.</param>
    /// <returns>The parameters' names and defaults, by record; empty unless the string enums are extensible.</returns>
    public static Dictionary<string, Dictionary<string, string?>> ParametersByRecord(
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry)
    {
        var result = new Dictionary<string, Dictionary<string, string?>>(StringComparer.Ordinal);
        if (registry?.ExtensibleEnums != true)
        {
            return result;
        }

        foreach (var path in openApiDoc?.Paths ?? [])
        {
            foreach (var operation in path.Value?.Operations ?? [])
            {
                if (operation.Value is null)
                {
                    continue;
                }

                var names = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var parameter in (path.Value!.Parameters ?? []).Concat(operation.Value.Parameters ?? []))
                {
                    if (parameter.Resolve().Parameter is { Name: { } name } resolved &&
                        IsExtensibleEnum(resolved.Schema, registry))
                    {
                        // A cookie is bound through a header of its own name (see WireBindingHelper.CookieHeaderPrefix)
                        names[resolved.In == ParameterLocation.Cookie ? WireBindingHelper.CookieHeaderPrefix + name : name] = DefaultTextOf(resolved.Schema);
                    }
                }

                if (names.Count > 0)
                {
                    var operationId = OperationIdHelper.Effective(operation.Value, operation.Key.ToString(), path.Key);
                    result[$"{operationId!.ToPascalCaseForDotNet()}Parameters"] = names;
                }
            }
        }

        return result;
    }

    // The parameter's own default as its wire text: next to the reference (OpenAPI 3.1, or an allOf wrapper's), or the
    // wrapper's - not the referenced enum's default
    private static string? DefaultTextOf(IOpenApiSchema? schema)
    {
        var value = schema switch
        {
            OpenApiSchemaReference { Target: { } target } reference when !ReferenceEquals(reference.Default, target.Default) => reference.Default,
            OpenApiSchema own => own.Default,
            _ => null,
        };

        return value is System.Text.Json.Nodes.JsonValue text && text.TryGetValue<string>(out var wire) ? wire : null;
    }

    // A reference, or a reference wrapped in a single allOf, as its target
    private static IOpenApiSchema? Resolve(IOpenApiSchema? schema)
    {
        var value = schema is OpenApiSchemaReference { Target: { } target } ? target : schema;
        if (value is OpenApiSchema { AllOf: { Count: 1 } allOf } && value.Enum is not { Count: > 0 })
        {
            value = allOf[0] is OpenApiSchemaReference { Target: { } wrapped } ? wrapped : allOf[0];
        }

        return value;
    }
}