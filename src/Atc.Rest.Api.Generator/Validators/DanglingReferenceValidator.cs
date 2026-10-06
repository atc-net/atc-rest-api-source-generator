namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Every <c>$ref</c> in the document resolves: a reference to a schema, parameter, response, request body, header or
/// path item that <c>components</c> does not hold is an error (<c>ATC_API_SCH013</c>) naming the reference and the
/// place it sits, under every strategy - the generators would otherwise emit code naming a type that is never
/// generated. Looks everywhere a reference can sit: path items and webhooks with their parameters, the operations'
/// parameters, request bodies and responses (headers, content, the schema and the item schema of a media type), and
/// the component schemas with their properties, items, <c>allOf</c>/<c>oneOf</c>/<c>anyOf</c> members,
/// <c>additionalProperties</c> and <c>not</c>.
/// </summary>
internal static class DanglingReferenceValidator
{
    public static List<DiagnosticMessage> Validate(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var walk = new Walk(sourceFilePath);
        foreach (var path in document.Paths ?? [])
        {
            walk.PathItem(path.Value, $"paths/{path.Key}");
        }

        foreach (var webhook in document.Webhooks ?? new Dictionary<string, IOpenApiPathItem>(StringComparer.Ordinal))
        {
            walk.PathItem(webhook.Value, $"webhooks/{webhook.Key}");
        }

        var components = document.Components;
        if (components is null)
        {
            return walk.Diagnostics;
        }

        foreach (var schema in components.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            walk.Schema(schema.Value, $"components/schemas/{schema.Key}");
        }

        foreach (var parameter in components.Parameters ?? new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal))
        {
            walk.Parameter(parameter.Value, $"components/parameters/{parameter.Key}");
        }

        foreach (var response in components.Responses ?? new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal))
        {
            walk.Response(response.Value, $"components/responses/{response.Key}");
        }

        foreach (var body in components.RequestBodies ?? new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal))
        {
            walk.RequestBody(body.Value, $"components/requestBodies/{body.Key}");
        }

        foreach (var header in components.Headers ?? new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))
        {
            walk.Header(header.Value, $"components/headers/{header.Key}");
        }

        return walk.Diagnostics;
    }

    private sealed class Walk(string sourceFilePath)
    {
        // A component schema is walked once, from components; a schema tree that refers to itself is a tree
        private readonly HashSet<OpenApiSchema> visited = new(ByReference.Instance);

        public List<DiagnosticMessage> Diagnostics { get; } = [];

        public void PathItem(
            IOpenApiPathItem? item,
            string path)
        {
            if (item is OpenApiPathItemReference { Target: null } reference)
            {
                Report("Path item", "pathItems", reference.Reference.Id, path);
                return;
            }

            if (item is null)
            {
                return;
            }

            Parameters(item.Parameters, path);
            foreach (var operation in item.Operations ?? [])
            {
                Operation(operation.Value, $"{path}/{operation.Key.Method.ToLowerInvariant()}");
            }
        }

        public void Schema(
            IOpenApiSchema? schema,
            string path)
        {
            switch (schema)
            {
                case OpenApiSchemaReference { Target: null } reference:
                    Diagnostics.Add(DiagnosticBuilder.SchemaReferenceError(reference.Reference.Id ?? "unknown", path, sourceFilePath));
                    return;
                case OpenApiSchema concrete when visited.Add(concrete):
                    Schema(concrete.Items, $"{path}/items");
                    Schema(concrete.AdditionalProperties, $"{path}/additionalProperties");
                    Schema(concrete.Not, $"{path}/not");
                    Schemas(concrete.AllOf, $"{path}/allOf");
                    Schemas(concrete.OneOf, $"{path}/oneOf");
                    Schemas(concrete.AnyOf, $"{path}/anyOf");
                    foreach (var property in concrete.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
                    {
                        Schema(property.Value, $"{path}/properties/{property.Key}");
                    }

                    return;
                default:
                    // Null, a resolved reference (its component is walked from components), or a schema seen before
                    return;
            }
        }

        public void Parameter(
            IOpenApiParameter? parameter,
            string path)
        {
            if (parameter is OpenApiParameterReference { Target: null } reference)
            {
                Report("Parameter", "parameters", reference.Reference.Id, path);
                return;
            }

            if (parameter is not OpenApiParameter concrete)
            {
                return;
            }

            Schema(concrete.Schema, $"{path}/schema");
            Content(concrete.Content, path);
        }

        public void Response(
            IOpenApiResponse? response,
            string path)
        {
            if (response is OpenApiResponseReference { Target: null } reference)
            {
                Report("Response", "responses", reference.Reference.Id, path);
                return;
            }

            if (response is not OpenApiResponse concrete)
            {
                return;
            }

            foreach (var header in concrete.Headers ?? new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))
            {
                Header(header.Value, $"{path}/headers/{header.Key}");
            }

            Content(concrete.Content, path);
        }

        public void RequestBody(
            IOpenApiRequestBody? body,
            string path)
        {
            if (body is OpenApiRequestBodyReference { Target: null } reference)
            {
                Report("Request body", "requestBodies", reference.Reference.Id, path);
                return;
            }

            if (body is OpenApiRequestBody concrete)
            {
                Content(concrete.Content, path);
            }
        }

        public void Header(
            IOpenApiHeader? header,
            string path)
        {
            if (header is OpenApiHeaderReference { Target: null } reference)
            {
                Report("Header", "headers", reference.Reference.Id, path);
                return;
            }

            if (header is not OpenApiHeader concrete)
            {
                return;
            }

            Schema(concrete.Schema, $"{path}/schema");
            Content(concrete.Content, path);
        }

        private void Operation(
            OpenApiOperation? operation,
            string path)
        {
            if (operation is null)
            {
                return;
            }

            Parameters(operation.Parameters, path);
            RequestBody(operation.RequestBody, $"{path}/requestBody");
            foreach (var response in operation.Responses ?? [])
            {
                Response(response.Value, $"{path}/responses/{response.Key}");
            }
        }

        private void Parameters(
            IList<IOpenApiParameter>? parameters,
            string path)
        {
            for (var index = 0; index < (parameters?.Count ?? 0); index++)
            {
                Parameter(parameters![index], $"{path}/parameters/{index}");
            }
        }

        private void Content(
            IDictionary<string, IOpenApiMediaType>? content,
            string path)
        {
            foreach (var mediaType in content ?? new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal))
            {
                Schema(mediaType.Value?.Schema, $"{path}/content/{mediaType.Key}/schema");
                Schema(mediaType.Value?.ItemSchema, $"{path}/content/{mediaType.Key}/itemSchema");
            }
        }

        private void Schemas(
            IList<IOpenApiSchema>? schemas,
            string path)
        {
            for (var index = 0; index < (schemas?.Count ?? 0); index++)
            {
                Schema(schemas![index], $"{path}/{index}");
            }
        }

        private void Report(
            string kind,
            string section,
            string? referenceId,
            string path)
            => Diagnostics.Add(DiagnosticBuilder.ComponentReferenceError(kind, section, referenceId ?? "unknown", path, sourceFilePath));

        private sealed class ByReference : IEqualityComparer<OpenApiSchema>
        {
            public static ByReference Instance { get; } = new();

            public bool Equals(
                OpenApiSchema x,
                OpenApiSchema y)
                => ReferenceEquals(x, y);

            public int GetHashCode(OpenApiSchema obj)
                => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}