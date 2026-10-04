namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Finds out whether a specification marks anything deprecated: a schema (also one nested in a property, array items,
/// a dictionary value or a composition), a property, an operation or a parameter. The generated code marks those
/// <c>[Obsolete]</c> and uses them itself, so its files then suppress the obsolete warning.
/// </summary>
public static class DeprecationDetector
{
    /// <summary>
    /// Whether the specification marks any schema, property, operation or parameter deprecated.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns><see langword="true"/> when something is deprecated.</returns>
    public static bool HasDeprecatedMembers(OpenApiDocument openApiDoc)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        var components = openApiDoc.Components;
        if ((components?.Schemas?.Values.Any(IsDeprecated) ?? false) ||
            (components?.Parameters?.Values.Any(IsDeprecated) ?? false))
        {
            return true;
        }

        foreach (var pathItem in openApiDoc.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
        {
            if (pathItem.Parameters?.Any(IsDeprecated) ?? false)
            {
                return true;
            }

            foreach (var operation in pathItem.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                if (operation.Deprecated ||
                    (operation.Parameters?.Any(IsDeprecated) ?? false) ||
                    HasDeprecatedSchema(operation.RequestBody?.Content) ||
                    (operation.Responses?.Values.Any(response => HasDeprecatedSchema(response.Content)) ?? false))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The document without its deprecated operations (and without a path left with no operation) - what a client
    /// generated without <c>includeDeprecated</c> covers, in step with the deprecated schemas it leaves out.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The document itself when no operation is deprecated, else a copy.</returns>
    public static OpenApiDocument WithoutDeprecatedOperations(
        OpenApiDocument openApiDoc)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        var paths = openApiDoc.Paths ?? new OpenApiPaths();
        if (!paths.Values.Any(p => p is OpenApiPathItem { Operations: { } operations } && operations.Values.Any(o => o.Deprecated)))
        {
            return openApiDoc;
        }

        var filtered = new OpenApiPaths();
        foreach (var path in paths)
        {
            if (path.Value is not OpenApiPathItem { Operations: { } operations } pathItem ||
                !operations.Values.Any(o => o.Deprecated))
            {
                filtered.Add(path.Key, path.Value);
                continue;
            }

            var current = operations
                .Where(o => !o.Value.Deprecated)
                .ToDictionary(o => o.Key, o => o.Value);
            if (current.Count > 0)
            {
                filtered.Add(
                    path.Key,
                    new OpenApiPathItem
                    {
                        Summary = pathItem.Summary,
                        Description = pathItem.Description,
                        Servers = pathItem.Servers,
                        Parameters = pathItem.Parameters,
                        Extensions = pathItem.Extensions,
                        Operations = current,
                    });
            }
        }

        return new OpenApiDocument(openApiDoc)
        {
            Paths = filtered,
        };
    }

    private static bool IsDeprecated(IOpenApiParameter parameter)
        => parameter is not OpenApiParameterReference &&
           (parameter.Deprecated || IsDeprecated(parameter.Schema) || HasDeprecatedSchema(parameter.Content));

    // A reference is skipped: the schema it points to is a component schema, checked on its own (and a cycle ends).
    private static bool IsDeprecated(IOpenApiSchema? schema)
        => schema is not null and not OpenApiSchemaReference &&
           (schema.Deprecated ||
            (schema.Properties?.Values.Any(IsDeprecated) ?? false) ||
            IsDeprecated(schema.Items) ||
            IsDeprecated(schema.AdditionalProperties) ||
            (schema.AllOf?.Any(IsDeprecated) ?? false) ||
            (schema.AnyOf?.Any(IsDeprecated) ?? false) ||
            (schema.OneOf?.Any(IsDeprecated) ?? false));

    private static bool HasDeprecatedSchema(
        IDictionary<string, IOpenApiMediaType>? content)
        => content?.Values.Any(mediaType => IsDeprecated(mediaType.Schema)) ?? false;
}