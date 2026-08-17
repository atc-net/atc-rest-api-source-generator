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
    // Cache for GetSchemasUsedBySegment results per document+segment.
    // Uses ConditionalWeakTable so entries are GC'd when the document is collected.
    private static readonly ConditionalWeakTable<OpenApiDocument, Dictionary<string, HashSet<string>>> SchemasPerSegmentCache = new();

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
    /// Preserves the original singular/plural form from the path.
    /// </summary>
    /// <param name="path">The API path (e.g., "/api/v1/pets/{petId}").</param>
    /// <returns>The first meaningful path segment in PascalCase (e.g., "Pets" or "Admin"), or "Default" if empty.</returns>
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

            // Found a meaningful segment - return in PascalCase, preserving singular/plural form
            return NormalizeSegmentCasing(segment);
        }

        return "Default";
    }

    /// <summary>
    /// Normalizes the casing of a path segment for use in generated namespaces/file names.
    /// Concatenated lowercase segments (e.g., "thirdpartyapi") have no natural word-boundary
    /// signal for PascalCasing, so as a narrow, low-risk heuristic a trailing "api"/"apis"
    /// suffix is split off before PascalCasing, producing "ThirdpartyApi" instead of
    /// "Thirdpartyapi". This does not attempt general-purpose word splitting.
    /// </summary>
    private static string NormalizeSegmentCasing(string segment)
    {
        const string apisSuffix = "apis";
        const string apiSuffix = "api";

        if (segment.Length > apisSuffix.Length &&
            segment.EndsWith(apisSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var head = segment.Substring(0, segment.Length - apisSuffix.Length);
            return $"{head.ToPascalCaseForDotNet()}Apis";
        }

        if (segment.Length > apiSuffix.Length &&
            segment.EndsWith(apiSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var head = segment.Substring(0, segment.Length - apiSuffix.Length);
            return $"{head.ToPascalCaseForDotNet()}Api";
        }

        return segment.ToPascalCaseForDotNet();
    }

    /// <summary>
    /// Resolves the effective path segment to use when building generated namespaces and file names.
    /// Returns <see langword="null" /> when the segment is redundant, which makes consumers fall back
    /// to the segment-less namespace shape (<c>{root}.Generated.{Category}</c>).
    /// </summary>
    /// <remarks>
    /// A segment is considered redundant when either:
    /// <list type="number">
    /// <item>
    /// <description>
    /// (A1) The document exposes only one unique path segment. Grouping by segment then adds no
    /// organizational value because every generated type would share the same segment anyway.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// (A2) The segment merely echoes the last dot-part of the configured root namespace
    /// (e.g. segment <c>ThirdpartyApi</c> for root namespace <c>Eloverblik.Api.ThirdPartyApi</c>).
    /// </description>
    /// </item>
    /// </list>
    /// A2 is only applied when dropping the segment stays collision-free, i.e. no other unique
    /// segment would end up mapping onto the same segment-less namespace.
    /// </remarks>
    /// <param name="openApiDoc">The OpenAPI document being generated from.</param>
    /// <param name="rootNamespace">The configured root namespace (project name).</param>
    /// <param name="pathSegment">The PascalCased path segment to evaluate.</param>
    /// <returns>The segment to use, or <see langword="null" /> when it is redundant.</returns>
    public static string? ResolveEffectivePathSegment(
        OpenApiDocument openApiDoc,
        string rootNamespace,
        string? pathSegment)
    {
        if (string.IsNullOrEmpty(pathSegment))
        {
            return null;
        }

        var uniqueSegments = GetUniquePathSegments(openApiDoc);

        // A1: a single unique segment carries no disambiguation value.
        if (uniqueSegments.Count <= 1)
        {
            return null;
        }

        // A2: the segment only echoes the root namespace tail.
        if (!DuplicatesRootNamespaceTail(rootNamespace, pathSegment!))
        {
            return pathSegment;
        }

        // Collision guard: only drop when exactly one segment duplicates the root namespace tail,
        // otherwise two distinct segments would collapse onto the same segment-less namespace.
        var duplicateCount = uniqueSegments
            .Count(segment => DuplicatesRootNamespaceTail(rootNamespace, segment));

        return duplicateCount == 1
            ? null
            : pathSegment;
    }

    /// <summary>
    /// Determines whether a path segment duplicates the last dot-part of the root namespace,
    /// ignoring case (e.g. "ThirdpartyApi" vs. "Eloverblik.Api.ThirdPartyApi").
    /// </summary>
    private static bool DuplicatesRootNamespaceTail(
        string rootNamespace,
        string pathSegment)
    {
        if (string.IsNullOrEmpty(rootNamespace))
        {
            return false;
        }

        var lastSeparatorIndex = rootNamespace.LastIndexOf('.');
        var tail = lastSeparatorIndex >= 0
            ? rootNamespace.Substring(lastSeparatorIndex + 1)
            : rootNamespace;

        return string.Equals(tail, pathSegment, StringComparison.OrdinalIgnoreCase);
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
    /// Gets all unique first path segments from an OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>A list of unique path segments in PascalCase.</returns>
    public static List<string> GetUniquePathSegments(OpenApiDocument openApiDoc)
    {
        if (openApiDoc.Paths is null || openApiDoc.Paths.Count == 0)
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

        if (openApiDoc.Paths is null)
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

            if (path.Value is not IOpenApiPathItem pathItem || pathItem.Operations is null)
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
        // Return cached result if available (avoids redundant traversal across multiple extractors)
        if (!SchemasPerSegmentCache.TryGetValue(openApiDoc, out var segmentCache))
        {
            segmentCache = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            SchemasPerSegmentCache.Add(openApiDoc, segmentCache);
        }

        if (segmentCache.TryGetValue(pathSegment, out var cached))
        {
            // Return a copy so callers can modify without corrupting the cache
            return new HashSet<string>(cached, StringComparer.Ordinal);
        }

        var schemaNames = new HashSet<string>(StringComparer.Ordinal);
        var operations = GetOperationsForSegment(openApiDoc, pathSegment);

        foreach (var (_, _, operation) in operations)
        {
            // Collect schemas from parameters
            if (operation.Parameters is not null)
            {
                foreach (var parameter in operation.Parameters)
                {
                    CollectSchemaNames(parameter.Schema, schemaNames);
                }
            }

            // Collect schemas from request body (Schema and OpenAPI 3.2 ItemSchema)
            if (operation.RequestBody?.Content is not null)
            {
                foreach (var content in operation.RequestBody.Content)
                {
                    CollectSchemaNames(content.Value.Schema, schemaNames);
                    CollectSchemaNames(content.Value.ItemSchema, schemaNames);
                }
            }

            // Collect schemas from responses (Schema and OpenAPI 3.2 ItemSchema)
            if (operation.Responses is not null)
            {
                foreach (var response in operation.Responses)
                {
                    if (response.Value is OpenApiResponse { Content: not null } openApiResponse)
                    {
                        foreach (var content in openApiResponse.Content)
                        {
                            CollectSchemaNames(content.Value.Schema, schemaNames);
                            CollectSchemaNames(content.Value.ItemSchema, schemaNames);
                        }
                    }
                }
            }
        }

        // Recursively add schemas referenced by collected schemas
        if (openApiDoc.Components?.Schemas is not null)
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

        // Cache the computed result for subsequent calls from other extractors
        segmentCache[pathSegment] = schemaNames;

        // Return a copy so callers can modify without corrupting the cache
        return new HashSet<string>(schemaNames, StringComparer.Ordinal);
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
            if (operation.RequestBody?.Content is not null)
            {
                foreach (var content in operation.RequestBody.Content.Values)
                {
                    if (content?.Schema is not null)
                    {
                        CollectSchemaNames(content.Schema, schemas);
                    }
                }
            }

            // Collect schemas from responses
            if (operation.Responses is not null)
            {
                foreach (var response in operation.Responses.Values)
                {
                    if (response?.Content is not null)
                    {
                        foreach (var content in response.Content.Values)
                        {
                            if (content?.Schema is not null)
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
        => GetSegmentSpecificSchemas(openApiDoc, pathSegment, GetSharedSchemas(openApiDoc));

    /// <summary>
    /// Gets schema names that are used by only one path segment (segment-specific types),
    /// using a pre-computed shared schemas set to avoid recomputation in loops.
    /// </summary>
    public static HashSet<string> GetSegmentSpecificSchemas(
        OpenApiDocument openApiDoc,
        string pathSegment,
        HashSet<string> sharedSchemas)
    {
        var allForSegment = GetSchemasUsedBySegment(openApiDoc, pathSegment);

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
        if (webhookOperation.RequestBody?.Content is not null)
        {
            foreach (var content in webhookOperation.RequestBody.Content.Values)
            {
                if (content?.Schema is not null)
                {
                    CollectSchemaNames(content.Schema, schemaNames);
                }
            }
        }

        // Collect schemas from responses
        if (webhookOperation.Responses is not null)
        {
            foreach (var response in webhookOperation.Responses.Values)
            {
                if (response?.Content is not null)
                {
                    foreach (var content in response.Content.Values)
                    {
                        if (content?.Schema is not null)
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
    /// Uses a visited set to prevent infinite recursion on circular inline schemas.
    /// </summary>
    private static void CollectSchemaNames(
        IOpenApiSchema? schema,
        HashSet<string> schemaNames)
        => CollectSchemaNames(schema, schemaNames, []);

    private static void CollectSchemaNames(
        IOpenApiSchema? schema,
        HashSet<string> schemaNames,
        HashSet<object> visited)
    {
        if (schema is null)
        {
            return;
        }

        // Guard against circular references in inline schemas
        if (!visited.Add(schema))
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
            if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true && actualSchema.Items is not null)
            {
                CollectSchemaNames(actualSchema.Items, schemaNames, visited);
            }

            // Handle object properties
            if (actualSchema.Properties is not null)
            {
                foreach (var property in actualSchema.Properties)
                {
                    CollectSchemaNames(property.Value, schemaNames, visited);
                }
            }

            // Handle additionalProperties
            if (actualSchema.AdditionalProperties is not null)
            {
                CollectSchemaNames(actualSchema.AdditionalProperties, schemaNames, visited);
            }

            // Handle allOf
            if (actualSchema.AllOf is not null)
            {
                foreach (var allOfSchema in actualSchema.AllOf)
                {
                    CollectSchemaNames(allOfSchema, schemaNames, visited);
                }
            }

            // Handle oneOf
            if (actualSchema.OneOf is not null)
            {
                foreach (var oneOfSchema in actualSchema.OneOf)
                {
                    CollectSchemaNames(oneOfSchema, schemaNames, visited);
                }
            }

            // Handle anyOf
            if (actualSchema.AnyOf is not null)
            {
                foreach (var anyOfSchema in actualSchema.AnyOf)
                {
                    CollectSchemaNames(anyOfSchema, schemaNames, visited);
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
        if (openApiDoc.Paths is null)
        {
            return false;
        }

        foreach (var path in openApiDoc.Paths)
        {
            if (path.Key.ShouldSkipForPathSegment(pathSegment))
            {
                continue;
            }

            if (path.Value is IOpenApiPathItem { Operations.Count: > 0 })
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
        if (openApiDoc.Paths is null)
        {
            return false;
        }

        foreach (var path in openApiDoc.Paths)
        {
            if (path.Key.ShouldSkipForPathSegment(pathSegment))
            {
                continue;
            }

            if (path.Value is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            // Check path-level parameters
            if (pathItem.Parameters is { Count: > 0 })
            {
                return true;
            }

            // Check each operation
            if (pathItem.Operations is not null)
            {
                foreach (var operation in pathItem.Operations)
                {
                    if (operation.Value is null)
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
        => PathSegmentHasModels(openApiDoc, pathSegment, GetSharedSchemas(openApiDoc));

    /// <summary>
    /// Checks if a path segment has segment-specific models (not shared models),
    /// using a pre-computed shared schemas set to avoid recomputation in loops.
    /// </summary>
    public static bool PathSegmentHasModels(
        OpenApiDocument openApiDoc,
        string pathSegment,
        HashSet<string> sharedSchemas)
    {
        var segmentSchemas = GetSegmentSpecificSchemas(openApiDoc, pathSegment, sharedSchemas);
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
        => GetPathSegmentNamespaces(openApiDoc, pathSegment, GetSharedSchemas(openApiDoc));

    /// <summary>
    /// Gets the namespace availability flags for a path segment,
    /// using a pre-computed shared schemas set to avoid recomputation in loops.
    /// </summary>
    public static PathSegmentNamespaces GetPathSegmentNamespaces(
        OpenApiDocument openApiDoc,
        string pathSegment,
        HashSet<string> sharedSchemas)
    {
        var hasOperations = PathSegmentHasOperations(openApiDoc, pathSegment);

        return new PathSegmentNamespaces(
            HasHandlers: hasOperations,
            HasResults: hasOperations,
            HasParameters: PathSegmentHasParameters(openApiDoc, pathSegment),
            HasModels: PathSegmentHasModels(openApiDoc, pathSegment, sharedSchemas));
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