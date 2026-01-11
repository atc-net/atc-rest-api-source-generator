// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable InvertIf
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable StringLiteralTypo
// ReSharper disable PossibleUnintendedLinearSearchInSet
namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helper methods for working with API path segments.
/// </summary>
public static class PathSegmentHelper
{
    /// <summary>
    /// Common API prefixes to skip when extracting path segments.
    /// </summary>
    private static readonly HashSet<string> SkipSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "apis",
    };

    /// <summary>
    /// Extracts the first meaningful path segment from an API path.
    /// Skips common prefixes like "api" and version segments like "v1".
    /// </summary>
    /// <param name="path">The API path (e.g., "/api/v1/pets/{petId}").</param>
    /// <returns>The first meaningful path segment in PascalCase and pluralized (e.g., "Pets"), or "Default" if empty.</returns>
    public static string GetFirstPathSegment(string path)
    {
        // Remove leading slash and split
        var trimmedPath = path.TrimStart('/');
        var segments = trimmedPath.Split('/');

        // Find first meaningful segment (skip common prefixes and versions)
        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment))
            {
                continue;
            }

            // Skip path parameters like {id}
            if (segment.StartsWith("{", StringComparison.Ordinal) && segment.EndsWith("}", StringComparison.Ordinal))
            {
                continue;
            }

            // Skip common API prefixes
            if (SkipSegments.Contains(segment))
            {
                continue;
            }

            // Skip version segments (v1, v2, v3, etc.)
            if (IsVersionSegment(segment))
            {
                continue;
            }

            // Found a meaningful segment - return in PascalCase and pluralized
            var pascalCase = segment.ToPascalCaseForDotNet();
            return EnsurePluralized(pascalCase);
        }

        return "Default";
    }

    /// <summary>
    /// Checks if a segment is a version pattern (v1, v2, v3, etc.).
    /// </summary>
    private static bool IsVersionSegment(string segment)
    {
        if (segment.Length < 2)
        {
            return false;
        }

        if (segment[0] != 'v' && segment[0] != 'V')
        {
            return false;
        }

        for (var i = 1; i < segment.Length; i++)
        {
            if (!char.IsDigit(segment[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Ensures a string is pluralized. Simple pluralization rules for common cases.
    /// </summary>
    private static string EnsurePluralized(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return word;
        }

        // Already plural (ends with 's')
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            return word;
        }

        // Simple pluralization rules
        if (word.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            !IsVowel(word[word.Length - 2]))
        {
            // e.g., "Category" -> "Categories"
            return word.Substring(0, word.Length - 1) + "ies";
        }

        if (word.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
        {
            // e.g., "Box" -> "Boxes"
            return word + "es";
        }

        // Default: just add 's'
        return word + "s";
    }

    /// <summary>
    /// Checks if a character is a vowel.
    /// </summary>
    private static bool IsVowel(char c)
        => "aeiouAEIOU".IndexOf(c) >= 0;

    /// <summary>
    /// Gets all unique first path segments from an OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>A list of unique path segments in PascalCase.</returns>
    public static List<string> GetUniquePathSegments(OpenApiDocument openApiDoc)
    {
        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return [];
        }

        var segments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in openApiDoc.Paths)
        {
            var segment = GetFirstPathSegment(path.Key);
            segments.Add(segment);
        }

        return segments
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Gets all operations that belong to a specific path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="pathSegment">The path segment to filter by (case-insensitive).</param>
    /// <returns>A list of tuples containing path, HTTP method, and operation.</returns>
    public static List<(string Path, string Method, OpenApiOperation Operation)> GetOperationsForSegment(
        OpenApiDocument openApiDoc,
        string pathSegment)
    {
        var operations = new List<(string Path, string Method, OpenApiOperation Operation)>();

        if (openApiDoc.Paths == null)
        {
            return operations;
        }

        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;
            var segment = GetFirstPathSegment(pathKey);

            if (!segment.Equals(pathSegment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (path.Value is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations)
            {
                var httpMethod = operation
                    .Key
                    .ToString()
                    .ToUpperInvariant();

                operations.Add((pathKey, httpMethod, operation.Value));
            }
        }

        return operations;
    }

    /// <summary>
    /// Gets all schema names referenced by operations in a specific path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="pathSegment">The path segment to filter by.</param>
    /// <returns>A set of schema names used by operations in the segment.</returns>
    public static HashSet<string> GetSchemasUsedBySegment(
        OpenApiDocument openApiDoc,
        string pathSegment)
    {
        var schemaNames = new HashSet<string>(StringComparer.Ordinal);
        var operations = GetOperationsForSegment(openApiDoc, pathSegment);

        foreach (var (_, _, operation) in operations)
        {
            // Collect schemas from parameters
            if (operation.Parameters != null)
            {
                foreach (var parameter in operation.Parameters)
                {
                    CollectSchemaNames(parameter.Schema, schemaNames);
                }
            }

            // Collect schemas from request body
            if (operation.RequestBody?.Content != null)
            {
                foreach (var content in operation.RequestBody.Content)
                {
                    CollectSchemaNames(content.Value.Schema, schemaNames);
                }
            }

            // Collect schemas from responses
            if (operation.Responses != null)
            {
                foreach (var response in operation.Responses)
                {
                    if (response.Value is OpenApiResponse { Content: not null } openApiResponse)
                    {
                        foreach (var content in openApiResponse.Content)
                        {
                            CollectSchemaNames(content.Value.Schema, schemaNames);
                        }
                    }
                }
            }
        }

        // Recursively add schemas referenced by collected schemas
        if (openApiDoc.Components?.Schemas != null)
        {
            var processedSchemas = new HashSet<string>(StringComparer.Ordinal);
            var schemasToProcess = new Queue<string>(schemaNames);

            while (schemasToProcess.Count > 0)
            {
                var schemaName = schemasToProcess.Dequeue();
                if (processedSchemas.Contains(schemaName, StringComparer.Ordinal))
                {
                    continue;
                }

                processedSchemas.Add(schemaName);

                if (openApiDoc.Components.Schemas.TryGetValue(schemaName, out var schema))
                {
                    var referencedSchemas = new HashSet<string>(StringComparer.Ordinal);
                    CollectSchemaNames(schema, referencedSchemas);

                    foreach (var referencedSchema in referencedSchemas)
                    {
                        if (!processedSchemas.Contains(referencedSchema))
                        {
                            schemaNames.Add(referencedSchema);
                            schemasToProcess.Enqueue(referencedSchema);
                        }
                    }
                }
            }
        }

        return schemaNames;
    }

    /// <summary>
    /// Maps each schema to the set of path segments that use it.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>A dictionary mapping schema names to the set of segments that use them.</returns>
    public static Dictionary<string, HashSet<string>> GetSchemaSegmentMap(
        OpenApiDocument openApiDoc)
    {
        var schemaSegmentMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var allSegments = GetUniquePathSegments(openApiDoc);

        foreach (var segment in allSegments)
        {
            var usedSchemas = GetSchemasUsedBySegment(openApiDoc, segment);
            foreach (var schema in usedSchemas)
            {
                if (!schemaSegmentMap.TryGetValue(schema, out var segments))
                {
                    segments = new HashSet<string>(StringComparer.Ordinal);
                    schemaSegmentMap[schema] = segments;
                }

                segments.Add(segment);
            }
        }

        return schemaSegmentMap;
    }

    /// <summary>
    /// Gets schema names that are used by multiple path segments (shared types).
    /// These should be generated under a common namespace without a segment suffix.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>A set of schema names used by 2 or more segments.</returns>
    public static HashSet<string> GetSharedSchemas(OpenApiDocument openApiDoc)
    {
        var schemaSegmentMap = GetSchemaSegmentMap(openApiDoc);
        var sharedSchemas = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kvp in schemaSegmentMap)
        {
            if (kvp.Value.Count > 1)
            {
                sharedSchemas.Add(kvp.Key);
            }
        }

        // Include schemas used only by webhooks (they don't belong to any path segment)
        var webhookSchemas = GetSchemasUsedByWebhooks(openApiDoc);
        foreach (var webhookSchema in webhookSchemas)
        {
            // Only add if not already used by a path segment
            if (!schemaSegmentMap.ContainsKey(webhookSchema))
            {
                sharedSchemas.Add(webhookSchema);
            }
        }

        return sharedSchemas;
    }

    /// <summary>
    /// Gets schema names that are used by webhooks.
    /// These schemas should be generated in the shared namespace since webhooks don't have path segments.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>A set of schema names used by webhooks.</returns>
    public static HashSet<string> GetSchemasUsedByWebhooks(
        OpenApiDocument openApiDoc)
    {
        var schemas = new HashSet<string>(StringComparer.Ordinal);

        if (!openApiDoc.HasWebhooks())
        {
            return schemas;
        }

        foreach (var (_, _, operation) in openApiDoc.GetAllWebhookOperations())
        {
            // Collect schemas from request body
            if (operation.RequestBody?.Content != null)
            {
                foreach (var content in operation.RequestBody.Content.Values)
                {
                    if (content?.Schema != null)
                    {
                        CollectSchemaNames(content.Schema, schemas);
                    }
                }
            }

            // Collect schemas from responses
            if (operation.Responses != null)
            {
                foreach (var response in operation.Responses.Values)
                {
                    if (response?.Content != null)
                    {
                        foreach (var content in response.Content.Values)
                        {
                            if (content?.Schema != null)
                            {
                                CollectSchemaNames(content.Schema, schemas);
                            }
                        }
                    }
                }
            }
        }

        return schemas;
    }

    /// <summary>
    /// Gets schema names that are used by only one path segment (segment-specific types).
    /// These should be generated under the segment-specific namespace.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="pathSegment">The path segment to get specific schemas for.</param>
    /// <returns>A set of schema names used only by the specified segment.</returns>
    public static HashSet<string> GetSegmentSpecificSchemas(
        OpenApiDocument openApiDoc,
        string pathSegment)
    {
        var allForSegment = GetSchemasUsedBySegment(openApiDoc, pathSegment);
        var sharedSchemas = GetSharedSchemas(openApiDoc);

        // Return only schemas NOT in the shared set
        allForSegment.ExceptWith(sharedSchemas);
        return allForSegment;
    }

    /// <summary>
    /// Gets the segment that a schema belongs to, or null if it's a shared schema.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="schemaName">The schema name to find.</param>
    /// <returns>The path segment the schema belongs to, or null if shared (including webhook-only schemas).</returns>
    public static string? GetSchemaSegment(
        OpenApiDocument openApiDoc,
        string schemaName)
    {
        var sharedSchemas = GetSharedSchemas(openApiDoc);
        if (sharedSchemas.Contains(schemaName))
        {
            return null; // Shared schema - no segment
        }

        // Find which segment uses this schema
        var schemaSegmentMap = GetSchemaSegmentMap(openApiDoc);
        if (schemaSegmentMap.TryGetValue(schemaName, out var segments) && segments.Count == 1)
        {
            return segments.First();
        }

        // Default to null (shared) if not found or ambiguous
        return null;
    }

    /// <summary>
    /// Gets all unique model using directives needed for a webhook operation.
    /// Includes both shared and segment-specific namespaces.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="webhookOperation">The webhook operation.</param>
    /// <returns>A list of using directives for model namespaces.</returns>
    public static List<string> GetWebhookModelUsings(
        OpenApiDocument openApiDoc,
        string projectName,
        OpenApiOperation webhookOperation)
    {
        var usings = new HashSet<string>(StringComparer.Ordinal);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal);

        // Collect schemas from request body
        if (webhookOperation.RequestBody?.Content != null)
        {
            foreach (var content in webhookOperation.RequestBody.Content.Values)
            {
                if (content?.Schema != null)
                {
                    CollectSchemaNames(content.Schema, schemaNames);
                }
            }
        }

        // Collect schemas from responses
        if (webhookOperation.Responses != null)
        {
            foreach (var response in webhookOperation.Responses.Values)
            {
                if (response?.Content != null)
                {
                    foreach (var content in response.Content.Values)
                    {
                        if (content?.Schema != null)
                        {
                            CollectSchemaNames(content.Schema, schemaNames);
                        }
                    }
                }
            }
        }

        // For each schema, determine its namespace
        var sharedSchemas = GetSharedSchemas(openApiDoc);
        var schemaSegmentMap = GetSchemaSegmentMap(openApiDoc);

        foreach (var schemaName in schemaNames)
        {
            if (sharedSchemas.Contains(schemaName))
            {
                // Shared schema - use root Models namespace
                usings.Add($"{projectName}.Generated.Models");
            }
            else if (schemaSegmentMap.TryGetValue(schemaName, out var segments) && segments.Count == 1)
            {
                // Segment-specific schema
                var segment = segments.First();
                usings.Add($"{projectName}.Generated.{segment}.Models");
            }
            else
            {
                // Default to shared namespace if ambiguous
                usings.Add($"{projectName}.Generated.Models");
            }
        }

        return usings
            .OrderBy(u => u, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Collects schema names from an OpenAPI schema recursively.
    /// </summary>
    private static void CollectSchemaNames(
        IOpenApiSchema? schema,
        HashSet<string> schemaNames)
    {
        if (schema == null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refId = schemaRef.Reference.Id ?? schemaRef.Id;
            if (!string.IsNullOrEmpty(refId))
            {
                schemaNames.Add(refId!);
            }

            return;
        }

        if (schema is OpenApiSchema actualSchema)
        {
            // Handle array items (use HasFlag since JsonSchemaType is a flags enum in OpenAPI 3.1.x)
            if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true && actualSchema.Items != null)
            {
                CollectSchemaNames(actualSchema.Items, schemaNames);
            }

            // Handle object properties
            if (actualSchema.Properties != null)
            {
                foreach (var property in actualSchema.Properties)
                {
                    CollectSchemaNames(property.Value, schemaNames);
                }
            }

            // Handle additionalProperties
            if (actualSchema.AdditionalProperties != null)
            {
                CollectSchemaNames(actualSchema.AdditionalProperties, schemaNames);
            }

            // Handle allOf
            if (actualSchema.AllOf != null)
            {
                foreach (var allOfSchema in actualSchema.AllOf)
                {
                    CollectSchemaNames(allOfSchema, schemaNames);
                }
            }

            // Handle oneOf
            if (actualSchema.OneOf != null)
            {
                foreach (var oneOfSchema in actualSchema.OneOf)
                {
                    CollectSchemaNames(oneOfSchema, schemaNames);
                }
            }

            // Handle anyOf
            if (actualSchema.AnyOf != null)
            {
                foreach (var anyOfSchema in actualSchema.AnyOf)
                {
                    CollectSchemaNames(anyOfSchema, schemaNames);
                }
            }
        }
    }

    /// <summary>
    /// Checks if a path segment has any operations (and thus will generate Handlers and Results).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="pathSegment">The path segment to check.</param>
    /// <returns>True if the segment has at least one operation.</returns>
    public static bool PathSegmentHasOperations(
        OpenApiDocument openApiDoc,
        string pathSegment)
    {
        if (openApiDoc.Paths == null)
        {
            return false;
        }

        foreach (var path in openApiDoc.Paths)
        {
            if (path.Key.ShouldSkipForPathSegment(pathSegment))
            {
                continue;
            }

            if (path.Value is OpenApiPathItem { Operations.Count: > 0 })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a path segment has any operations with parameters or request body.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="pathSegment">The path segment to check.</param>
    /// <returns>True if any operation has parameters or request body.</returns>
    public static bool PathSegmentHasParameters(
        OpenApiDocument openApiDoc,
        string pathSegment)
    {
        if (openApiDoc.Paths == null)
        {
            return false;
        }

        foreach (var path in openApiDoc.Paths)
        {
            if (path.Key.ShouldSkipForPathSegment(pathSegment))
            {
                continue;
            }

            if (path.Value is not OpenApiPathItem pathItem)
            {
                continue;
            }

            // Check path-level parameters
            if (pathItem.Parameters is { Count: > 0 })
            {
                return true;
            }

            // Check each operation
            if (pathItem.Operations != null)
            {
                foreach (var operation in pathItem.Operations)
                {
                    if (operation.Value == null)
                    {
                        continue;
                    }

                    // Check operation-level parameters
                    if (operation.Value.Parameters is { Count: > 0 })
                    {
                        return true;
                    }

                    // Check request body
                    if (operation.Value.RequestBody?.Content is { Count: > 0 })
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a path segment has segment-specific models (not shared models).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="pathSegment">The path segment to check.</param>
    /// <returns>True if the segment has any segment-specific schemas.</returns>
    public static bool PathSegmentHasModels(
        OpenApiDocument openApiDoc,
        string pathSegment)
    {
        var segmentSchemas = GetSegmentSpecificSchemas(openApiDoc, pathSegment);
        return segmentSchemas.Count > 0;
    }

    /// <summary>
    /// Gets comprehensive namespace availability information for a path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="pathSegment">The path segment to check.</param>
    /// <returns>A record containing flags for each namespace type availability.</returns>
    public static PathSegmentNamespaces GetPathSegmentNamespaces(
        OpenApiDocument openApiDoc,
        string pathSegment)
    {
        var hasOperations = PathSegmentHasOperations(openApiDoc, pathSegment);

        return new PathSegmentNamespaces(
            HasHandlers: hasOperations,
            HasResults: hasOperations,
            HasParameters: PathSegmentHasParameters(openApiDoc, pathSegment),
            HasModels: PathSegmentHasModels(openApiDoc, pathSegment));
    }

    /// <summary>
    /// Gets conditional using directives for path segment namespaces.
    /// Only includes namespace usings for types that actually exist based on the PathSegmentNamespaces flags.
    /// </summary>
    /// <param name="projectName">The project/root namespace name.</param>
    /// <param name="pathSegment">The path segment (e.g., "Pets"). Null or empty for root namespace.</param>
    /// <param name="namespaces">The namespace availability flags.</param>
    /// <param name="includeHandlers">Whether to include Handlers namespace (default: true).</param>
    /// <param name="includeModels">Whether to include Models namespace (default: true).</param>
    /// <param name="isGlobalUsing">Whether to use "global using" syntax (default: false).</param>
    /// <returns>An enumerable of using directive strings.</returns>
    public static IEnumerable<string> GetSegmentUsings(
        string projectName,
        string? pathSegment,
        PathSegmentNamespaces namespaces,
        bool includeHandlers = true,
        bool includeModels = true,
        bool isGlobalUsing = false)
    {
        var prefix = isGlobalUsing ? "global using " : "using ";
        var segmentPart = string.IsNullOrEmpty(pathSegment) ? string.Empty : $".{pathSegment}";

        if (includeHandlers && namespaces.HasHandlers)
        {
            yield return $"{prefix}{projectName}.Generated{segmentPart}.Handlers;";
        }

        if (includeModels && namespaces.HasModels)
        {
            yield return $"{prefix}{projectName}.Generated{segmentPart}.Models;";
        }

        if (namespaces.HasParameters)
        {
            yield return $"{prefix}{projectName}.Generated{segmentPart}.Parameters;";
        }

        if (namespaces.HasResults)
        {
            yield return $"{prefix}{projectName}.Generated{segmentPart}.Results;";
        }
    }
}