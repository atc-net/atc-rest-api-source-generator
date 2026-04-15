namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates Mock Service Worker (MSW) handlers from OpenAPI operations.
/// Each path segment produces a handler file with mock responses.
/// </summary>
public static class TypeScriptMswHandlerExtractor
{
    /// <summary>
    /// Generates MSW handler files for all operations in the OpenAPI document.
    /// </summary>
    [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Base URL is a path prefix, not a full URI.")]
    public static List<(string SegmentName, string Content)> Extract(
        OpenApiDocument openApiDoc,
        string? headerContent,
        string baseUrl,
        TypeScriptNamingStrategy namingStrategy)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var results = new List<(string SegmentName, string Content)>();
        var pathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        foreach (var segment in pathSegments)
        {
            var operations = PathSegmentHelper.GetOperationsForSegment(openApiDoc, segment);
            if (operations.Count == 0)
            {
                continue;
            }

            var content = GenerateHandlerFile(
                segment,
                operations,
                openApiDoc,
                headerContent,
                baseUrl,
                namingStrategy);

            results.Add((segment, content));
        }

        // Generate combined handlers file
        if (results.Count > 0)
        {
            var indexContent = GenerateHandlersIndex(results, headerContent);
            results.Add(("handlers", indexContent));
        }

        return results;
    }

    private static string GenerateHandlerFile(
        string segment,
        List<(string Path, string Method, OpenApiOperation Operation)> operations,
        OpenApiDocument openApiDoc,
        string? headerContent,
        string baseUrl,
        TypeScriptNamingStrategy namingStrategy)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("import { http, HttpResponse } from 'msw';");
        sb.AppendLine();

        var segmentLower = segment.Length > 0
            ? char.ToLowerInvariant(segment[0]) + segment.Substring(1)
            : segment;

        sb.Append("export const ").Append(segmentLower).AppendLine("Handlers = [");

        for (var i = 0; i < operations.Count; i++)
        {
            var (path, method, operation) = operations[i];
            var httpMethod = method.ToLowerInvariant();

            // Convert OpenAPI path params to MSW format: {petId} -> :petId
            var mswPath = ConvertPathToMswFormat(path);
            var fullPath = string.IsNullOrEmpty(baseUrl) ? mswPath : $"{baseUrl}{mswPath}";

            // Determine response status and body
            var (statusCode, hasBody) = GetPrimaryResponse(operation);
            var mockBody = hasBody ? GenerateMockBody(operation, openApiDoc, namingStrategy) : null;

            sb.Append("  http.").Append(httpMethod).Append("('").Append(fullPath).AppendLine("', () => {");

            if (mockBody != null)
            {
                if (statusCode != 200)
                {
                    sb.Append("    return HttpResponse.json(").Append(mockBody).Append(", { status: ").Append(statusCode).AppendLine(" });");
                }
                else
                {
                    sb.Append("    return HttpResponse.json(").Append(mockBody).AppendLine(");");
                }
            }
            else
            {
                sb.Append("    return new HttpResponse(null, { status: ").Append(statusCode).AppendLine(" });");
            }

            sb.Append("  })");

            if (i < operations.Count - 1)
            {
                sb.AppendLine(",");
            }
            else
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("];");

        return sb.ToString();
    }

    private static string GenerateHandlersIndex(
        List<(string SegmentName, string Content)> segments,
        string? headerContent)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        // Import each segment handler
        foreach (var (segmentName, _) in segments)
        {
            var segmentLower = segmentName.Length > 0
                ? char.ToLowerInvariant(segmentName[0]) + segmentName.Substring(1)
                : segmentName;
            var segmentKebab = segmentName.EnsureFirstCharacterToLower();
            sb.Append("import { ").Append(segmentLower).Append("Handlers } from './").Append(segmentKebab).AppendLine("';");
        }

        sb.AppendLine();
        sb.AppendLine("export const handlers = [");

        for (var i = 0; i < segments.Count; i++)
        {
            var segmentLower = segments[i].SegmentName.Length > 0
                ? char.ToLowerInvariant(segments[i].SegmentName[0]) + segments[i].SegmentName.Substring(1)
                : segments[i].SegmentName;
            sb.Append("  ...").Append(segmentLower).Append("Handlers");
            sb.AppendLine(i < segments.Count - 1 ? "," : string.Empty);
        }

        sb.AppendLine("];");

        return sb.ToString();
    }

    private static string ConvertPathToMswFormat(string path)
    {
        var sb = new StringBuilder(path.Length);
        foreach (var c in path)
        {
            switch (c)
            {
                case '{':
                    sb.Append(':');
                    break;
                case '}':
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private static (int StatusCode, bool HasBody) GetPrimaryResponse(
        OpenApiOperation operation)
    {
        if (operation.Responses == null)
        {
            return (200, false);
        }

        // Try 200, then 201, then 204
        if (operation.Responses.TryGetValue("200", out var resp200))
        {
            return (200, resp200?.Content?.ContainsKey("application/json") == true);
        }

        if (operation.Responses.TryGetValue("201", out var resp201))
        {
            return (201, resp201?.Content?.ContainsKey("application/json") == true);
        }

        if (operation.Responses.ContainsKey("204"))
        {
            return (204, false);
        }

        return (200, false);
    }

    private static string? GenerateMockBody(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        TypeScriptNamingStrategy namingStrategy)
    {
        var schema = operation.GetResponseSchema("200") ?? operation.GetResponseSchema("201");
        if (schema == null)
        {
            return null;
        }

        // For array responses, wrap in []
        if (schema is OpenApiSchema { Type: JsonSchemaType.Array } arraySchema)
        {
            var itemMock = GenerateMockObject(arraySchema.Items, openApiDoc, namingStrategy);
            return $"[{itemMock}]";
        }

        // For $ref, resolve and generate
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var resolved = schemaRef.Target;
            if (resolved != null)
            {
                return GenerateMockObject(resolved, openApiDoc, namingStrategy);
            }
        }

        return GenerateMockObject(schema, openApiDoc, namingStrategy);
    }

    private static string GenerateMockObject(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeScriptNamingStrategy namingStrategy,
        int depth = 0)
    {
        if (schema == null || depth > 3)
        {
            return "{}";
        }

        // Resolve references
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var target = schemaRef.Target;
            return target != null
                ? GenerateMockObject(target, openApiDoc, namingStrategy, depth)
                : "{}";
        }

        if (schema is not OpenApiSchema actualSchema)
        {
            return "{}";
        }

        // Handle allOf composition
        if (actualSchema.AllOf is { Count: > 0 })
        {
            var mergedProps = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
            foreach (var sub in actualSchema.AllOf)
            {
                var resolved = sub is OpenApiSchemaReference r ? r.Target : sub;
                if (resolved is OpenApiSchema s && s.Properties != null)
                {
                    foreach (var p in s.Properties)
                    {
                        mergedProps.TryAdd(p.Key, p.Value);
                    }
                }
            }

            return GenerateMockFromProperties(mergedProps, openApiDoc, namingStrategy, depth);
        }

        if (actualSchema.Properties == null || actualSchema.Properties.Count == 0)
        {
            return "{}";
        }

        return GenerateMockFromProperties(actualSchema.Properties, openApiDoc, namingStrategy, depth);
    }

    private static string GenerateMockFromProperties(
        IDictionary<string, IOpenApiSchema> properties,
        OpenApiDocument openApiDoc,
        TypeScriptNamingStrategy namingStrategy,
        int depth)
    {
        var sb = new StringBuilder();
        sb.Append("{ ");

        var first = true;
        foreach (var prop in properties)
        {
            if (!first)
            {
                sb.Append(", ");
            }

            first = false;
            var propName = prop.Key.ApplyNamingStrategy(namingStrategy);
            var value = GenerateMockValue(prop.Value, prop.Key, openApiDoc, namingStrategy, depth + 1);
            sb.Append(propName).Append(": ").Append(value);
        }

        sb.Append(" }");
        return sb.ToString();
    }

    [SuppressMessage("", "MA0051:Method is too long", Justification = "Switch over schema types.")]
    private static string GenerateMockValue(
        IOpenApiSchema schema,
        string propertyName,
        OpenApiDocument openApiDoc,
        TypeScriptNamingStrategy namingStrategy,
        int depth)
    {
        // Resolve references
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var target = schemaRef.Target;
            if (target is OpenApiSchema { Type: JsonSchemaType.String, Enum.Count: > 0 } enumSchema)
            {
                // Return first enum value
                var firstValue = enumSchema.Enum.FirstOrDefault();
                return firstValue != null ? $"'{firstValue}'" : "'unknown'";
            }

            return target != null
                ? GenerateMockObject(target, openApiDoc, namingStrategy, depth)
                : "{}";
        }

        if (schema is not OpenApiSchema actualSchema)
        {
            return "null";
        }

        // Handle enums
        if (actualSchema is { Type: JsonSchemaType.String, Enum.Count: > 0 })
        {
            var firstValue = actualSchema.Enum.FirstOrDefault();
            return firstValue != null ? $"'{firstValue}'" : "'unknown'";
        }

        // Handle by type
        if (actualSchema.Type?.HasFlag(JsonSchemaType.String) == true)
        {
            return actualSchema.Format switch
            {
                "uuid" => "'00000000-0000-0000-0000-000000000001'",
                "date" => "'2024-01-15'",
                "date-time" => "'2024-01-15T10:30:00Z'",
                "uri" or "url" => $"'https://example.com/{propertyName}'",
                "email" => "'user@example.com'",
                "binary" => "'data'",
                _ => $"'sample-{propertyName}'",
            };
        }

        if (actualSchema.Type?.HasFlag(JsonSchemaType.Integer) == true)
        {
            return propertyName.Contains("id", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
        }

        if (actualSchema.Type?.HasFlag(JsonSchemaType.Number) == true)
        {
            return "0.0";
        }

        if (actualSchema.Type?.HasFlag(JsonSchemaType.Boolean) == true)
        {
            return "true";
        }

        if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
        {
            if (depth > 2)
            {
                return "[]";
            }

            var itemMock = actualSchema.Items != null
                ? GenerateMockValue(actualSchema.Items, propertyName, openApiDoc, namingStrategy, depth + 1)
                : "null";
            return $"[{itemMock}]";
        }

        if (actualSchema.Type?.HasFlag(JsonSchemaType.Object) == true)
        {
            return depth > 2 ? "{}" : GenerateMockObject(actualSchema, openApiDoc, namingStrategy, depth);
        }

        return "null";
    }
}