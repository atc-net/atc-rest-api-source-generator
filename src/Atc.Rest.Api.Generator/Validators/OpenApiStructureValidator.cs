namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Finds the places where the specification holds a value of another kind where an object belongs - a path item, an
/// operation, a response, a media type, a schema written as a string. The OpenAPI reader leaves a <see langword="null"/>
/// there without an error, and every later step would trip over it, so each is an error of its own and the document is
/// not validated further or generated from.
/// </summary>
public static class OpenApiStructureValidator
{
    /// <summary>
    /// Validates the document's structure.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="sourceFilePath">The specification's path, for the diagnostics.</param>
    /// <returns>An error per missing object; empty when the structure is whole.</returns>
    public static List<DiagnosticMessage> Validate(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        if (document is null)
        {
            return diagnostics;
        }

        void Missing(string pointer)
            => diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OpenApiCoreError,
                $"'{pointer}' must be an object, but the specification has a value of another kind there",
                DiagnosticSeverity.Error,
                sourceFilePath));

        var visited = new HashSet<OpenApiSchema>();

        foreach (var path in document.Paths ?? [])
        {
            var pointer = $"#/paths/{path.Key}";
            if (path.Value is null)
            {
                Missing(pointer);
                continue;
            }

            Parameters(path.Value.Parameters, pointer + "/parameters", Missing, visited);
            foreach (var operation in path.Value.Operations ?? [])
            {
                var operationPointer = $"{pointer}/{operation.Key.Method.ToLowerInvariant()}";
                if (operation.Value is null)
                {
                    Missing(operationPointer);
                    continue;
                }

                Parameters(operation.Value.Parameters, operationPointer + "/parameters", Missing, visited);
                if (operation.Value.RequestBody is { } requestBody)
                {
                    Content(requestBody.Content, operationPointer + "/requestBody/content", Missing, visited);
                }

                foreach (var response in operation.Value.Responses ?? [])
                {
                    Response(response.Value, $"{operationPointer}/responses/{response.Key}", Missing, visited);
                }
            }
        }

        var components = document.Components;
        foreach (var schema in components?.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            Schema(schema.Value, $"#/components/schemas/{schema.Key}", Missing, visited);
        }

        foreach (var response in components?.Responses ?? new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal))
        {
            Response(response.Value, $"#/components/responses/{response.Key}", Missing, visited);
        }

        foreach (var parameter in components?.Parameters ?? new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal))
        {
            if (parameter.Value is null)
            {
                Missing($"#/components/parameters/{parameter.Key}");
            }
            else
            {
                Schema(parameter.Value.Schema, $"#/components/parameters/{parameter.Key}/schema", Missing, visited, optional: true);
            }
        }

        foreach (var requestBody in components?.RequestBodies ?? new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal))
        {
            if (requestBody.Value is null)
            {
                Missing($"#/components/requestBodies/{requestBody.Key}");
            }
            else
            {
                Content(requestBody.Value.Content, $"#/components/requestBodies/{requestBody.Key}/content", Missing, visited);
            }
        }

        foreach (var scheme in components?.SecuritySchemes ?? new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal))
        {
            if (scheme.Value is null)
            {
                Missing($"#/components/securitySchemes/{scheme.Key}");
            }
        }

        return diagnostics;
    }

    private static void Parameters(
        IList<IOpenApiParameter>? parameters,
        string pointer,
        Action<string> missing,
        HashSet<OpenApiSchema> visited)
    {
        for (var i = 0; i < (parameters?.Count ?? 0); i++)
        {
            if (parameters![i] is null)
            {
                missing($"{pointer}/{i}");
            }
            else
            {
                Schema(parameters[i].Schema, $"{pointer}/{i}/schema", missing, visited, optional: true);
            }
        }
    }

    private static void Response(
        IOpenApiResponse? response,
        string pointer,
        Action<string> missing,
        HashSet<OpenApiSchema> visited)
    {
        if (response is null)
        {
            missing(pointer);
            return;
        }

        Content(response.Content, pointer + "/content", missing, visited);
        foreach (var header in response.Headers ?? new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))
        {
            if (header.Value is null)
            {
                missing($"{pointer}/headers/{header.Key}");
            }
        }
    }

    private static void Content(
        IDictionary<string, IOpenApiMediaType>? content,
        string pointer,
        Action<string> missing,
        HashSet<OpenApiSchema> visited)
    {
        foreach (var mediaType in content ?? new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal))
        {
            if (mediaType.Value is null)
            {
                missing($"{pointer}/{mediaType.Key}");
            }
            else
            {
                Schema(mediaType.Value.Schema, $"{pointer}/{mediaType.Key}/schema", missing, visited, optional: true);
            }
        }
    }

    private static void Schema(
        IOpenApiSchema? schema,
        string pointer,
        Action<string> missing,
        HashSet<OpenApiSchema> visited,
        bool optional = false)
    {
        // A reference is checked where its target is declared; an absent optional schema is no error
        if (schema is null)
        {
            if (!optional)
            {
                missing(pointer);
            }

            return;
        }

        if (schema is not OpenApiSchema inline || !visited.Add(inline))
        {
            return;
        }

        foreach (var property in inline.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            Schema(property.Value, $"{pointer}/properties/{property.Key}", missing, visited);
        }

        foreach (var (name, list) in (ValueTuple<string, IList<IOpenApiSchema>?>[])[("allOf", inline.AllOf), ("anyOf", inline.AnyOf), ("oneOf", inline.OneOf)])
        {
            for (var i = 0; i < (list?.Count ?? 0); i++)
            {
                Schema(list![i], $"{pointer}/{name}/{i}", missing, visited);
            }
        }

        Schema(inline.Items, pointer + "/items", missing, visited, optional: true);
        Schema(inline.AdditionalProperties, pointer + "/additionalProperties", missing, visited, optional: true);
    }
}