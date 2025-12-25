namespace Atc.Rest.Api.Generator.Services;

/// <summary>
/// Collects statistics from code generation for reporting.
/// </summary>
public static class StatisticsCollector
{
    /// <summary>
    /// Collects statistics from generated types and OpenAPI document.
    /// </summary>
    /// <param name="types">The generated types.</param>
    /// <param name="document">The OpenAPI document.</param>
    /// <param name="specificationName">The name of the specification file.</param>
    /// <param name="generatorType">The type of generator ("Server" or "Client").</param>
    /// <param name="diagnostics">The diagnostics from validation.</param>
    /// <param name="duration">The duration of the generation process.</param>
    /// <returns>The collected statistics.</returns>
    public static GenerationStatistics CollectFromGeneratedTypes(
        IReadOnlyList<GeneratedType> types,
        OpenApiDocument document,
        string specificationName,
        string generatorType,
        IReadOnlyList<DiagnosticMessage> diagnostics,
        TimeSpan duration)
    {
        var grouped = types
            .GroupBy(t => t.Category, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        var warnings = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        return new GenerationStatistics
        {
            SpecificationName = specificationName,
            SpecificationVersion = document.Info?.Version ?? string.Empty,
            OpenApiVersion = GetOpenApiVersion(document),
            ApiTitle = document.Info?.Title ?? string.Empty,
            GeneratorVersion = GetGeneratorVersion(),
            GeneratorType = generatorType,
            GeneratedAt = DateTimeOffset.UtcNow,
            Duration = duration,
            ModelsCount = GetDictionaryValue(grouped, "Models"),
            EnumsCount = GetDictionaryValue(grouped, "Enums"),
            ParametersCount = GetDictionaryValue(grouped, "Parameters"),
            ResultsCount = GetDictionaryValue(grouped, "Results"),
            HandlersCount = GetDictionaryValue(grouped, "Handlers"),
            EndpointsCount = GetDictionaryValue(grouped, "Endpoints"),
            OperationsCount = CountOperations(document),
            ClientMethodsCount = GetDictionaryValue(grouped, "Client"),
            EndpointClassesCount = GetDictionaryValue(grouped, "EndpointClasses"),
            WebhooksCount = document.GetWebhooksCount(),
            WebhookHandlersCount = GetDictionaryValue(grouped, "WebhookHandlers"),
            ErrorCount = errors.Count,
            WarningCount = warnings.Count,
            ErrorRuleIds = errors
                .Select(e => e.RuleId).Distinct(StringComparer.Ordinal)
                .ToList(),
            WarningRuleIds = warnings
                .Select(w => w.RuleId).Distinct(StringComparer.Ordinal)
                .ToList(),
        };
    }

    /// <summary>
    /// Collects statistics directly from an OpenAPI document (before generation).
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <param name="specificationName">The name of the specification file.</param>
    /// <param name="generatorType">The type of generator ("Server" or "Client").</param>
    /// <param name="diagnostics">The diagnostics from validation.</param>
    /// <returns>The collected statistics.</returns>
    public static GenerationStatistics CollectFromOpenApiDocument(
        OpenApiDocument document,
        string specificationName,
        string generatorType,
        IReadOnlyList<DiagnosticMessage> diagnostics)
    {
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        var warnings = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        var operationsCount = CountOperations(document);
        var schemasCount = document.Components?.Schemas?.Count ?? 0;
        var enumsCount = CountEnums(document);

        return new GenerationStatistics
        {
            SpecificationName = specificationName,
            SpecificationVersion = document.Info?.Version ?? string.Empty,
            OpenApiVersion = GetOpenApiVersion(document),
            ApiTitle = document.Info?.Title ?? string.Empty,
            GeneratorVersion = GetGeneratorVersion(),
            GeneratorType = generatorType,
            GeneratedAt = DateTimeOffset.UtcNow,

            // Estimates based on OpenAPI document
            ModelsCount = schemasCount - enumsCount,
            EnumsCount = enumsCount,
            ParametersCount = operationsCount, // One parameters class per operation (approximately)
            ResultsCount = operationsCount, // One result class per operation
            HandlersCount = operationsCount, // One handler per operation
            EndpointsCount = CountPathSegments(document), // One endpoint file per path segment
            OperationsCount = operationsCount,
            ClientMethodsCount = generatorType == "Client" ? operationsCount : 0,
            EndpointClassesCount = 0, // Depends on generation mode
            WebhooksCount = document.GetWebhooksCount(),
            WebhookHandlersCount = CountWebhookOperations(document), // One handler per webhook operation
            ErrorCount = errors.Count,
            WarningCount = warnings.Count,
            ErrorRuleIds = errors
                .Select(e => e.RuleId).Distinct(StringComparer.Ordinal)
                .ToList(),
            WarningRuleIds = warnings
                .Select(w => w.RuleId).Distinct(StringComparer.Ordinal)
                .ToList(),
        };
    }

    private static int GetDictionaryValue(
        Dictionary<string, int> dictionary,
        string key)
        => dictionary.TryGetValue(key, out var value) ? value : 0;

    private static int CountOperations(OpenApiDocument document)
    {
        if (document.Paths == null)
        {
            return 0;
        }

        return document.Paths.Sum(path =>
            path.Value.Operations?.Count ?? 0);
    }

    private static int CountWebhookOperations(OpenApiDocument document)
    {
        if (document.Webhooks == null)
        {
            return 0;
        }

        return document.Webhooks.Sum(webhook =>
            webhook.Value?.Operations?.Count ?? 0);
    }

    private static int CountEnums(OpenApiDocument document)
    {
        if (document.Components?.Schemas == null)
        {
            return 0;
        }

        return document.Components.Schemas.Count(kvp =>
            kvp.Value.Enum?.Count > 0 ||
            (kvp.Value is OpenApiSchema schema && schema.Type?.ToString() == "string" && schema.Enum?.Count > 0));
    }

    private static int CountPathSegments(OpenApiDocument document)
    {
        if (document.Paths == null)
        {
            return 0;
        }

        var segments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in document.Paths.Keys)
        {
            var segment = GetFirstPathSegment(path);
            if (!string.IsNullOrEmpty(segment))
            {
                segments.Add(segment);
            }
        }

        return System.Math.Max(1, segments.Count);
    }

    private static string GetFirstPathSegment(string path)
    {
        var trimmed = path.TrimStart('/');
        var slashIndex = trimmed.IndexOf('/');
        return slashIndex > 0 ? trimmed.Substring(0, slashIndex) : trimmed;
    }

    private static string GetOpenApiVersion(OpenApiDocument document)
    {
        // Detect OpenAPI 3.1 features
        if (HasOpenApi31Features(document))
        {
            return "3.1.x";
        }

        return "3.0.x";
    }

    /// <summary>
    /// Detects if the OpenAPI document uses any OpenAPI 3.1 specific features.
    /// </summary>
    private static bool HasOpenApi31Features(OpenApiDocument document)
    {
        // Check for webhooks (OpenAPI 3.1 feature)
        if (document.Webhooks?.Count > 0)
        {
            return true;
        }

        // Check schemas for 3.1 features
        if (document.Components?.Schemas != null)
        {
            foreach (var schemaEntry in document.Components.Schemas)
            {
                if (HasOpenApi31SchemaFeatures(schemaEntry.Value))
                {
                    return true;
                }
            }
        }

        // Check path/operation schemas for 3.1 features
        if (document.Paths != null)
        {
            foreach (var pathItem in document.Paths.Values)
            {
                if (pathItem.Operations != null)
                {
                    foreach (var operation in pathItem.Operations.Values)
                    {
                        if (HasOperationOpenApi31Features(operation))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a schema uses OpenAPI 3.1 specific features.
    /// </summary>
    private static bool HasOpenApi31SchemaFeatures(IOpenApiSchema schema)
    {
        // Check for multiple non-null types (type arrays)
        if (schema.HasMultipleNonNullTypes())
        {
            return true;
        }

        // Check for const value (JSON Schema 2020-12)
        if (schema is OpenApiSchema actualSchema)
        {
            // Check for const value
            if (actualSchema.Const != null)
            {
                return true;
            }

            // Recursively check properties
            if (actualSchema.Properties != null)
            {
                foreach (var propSchema in actualSchema.Properties.Values)
                {
                    if (HasOpenApi31SchemaFeatures(propSchema))
                    {
                        return true;
                    }
                }
            }

            // Check array items
            if (actualSchema.Items != null &&
                HasOpenApi31SchemaFeatures(actualSchema.Items))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if an operation uses OpenAPI 3.1 specific features.
    /// </summary>
    private static bool HasOperationOpenApi31Features(
        OpenApiOperation operation)
    {
        // Check request body schemas
        if (operation.RequestBody?.Content != null)
        {
            foreach (var content in operation.RequestBody.Content.Values)
            {
                if (content.Schema != null && HasOpenApi31SchemaFeatures(content.Schema))
                {
                    return true;
                }
            }
        }

        // Check response schemas
        if (operation.Responses != null)
        {
            foreach (var response in operation.Responses.Values)
            {
                if (response.Content != null)
                {
                    foreach (var content in response.Content.Values)
                    {
                        if (content.Schema != null && HasOpenApi31SchemaFeatures(content.Schema))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        // Check parameter schemas
        if (operation.Parameters != null)
        {
            foreach (var parameter in operation.Parameters)
            {
                if (parameter.Schema != null && HasOpenApi31SchemaFeatures(parameter.Schema))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetGeneratorVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "1.0.0";
    }
}