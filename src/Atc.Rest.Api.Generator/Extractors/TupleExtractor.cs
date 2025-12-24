namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI schema definitions with prefixItems and converts them to RecordsParameters for tuple code generation.
/// </summary>
public static class TupleExtractor
{
    /// <summary>
    /// Extracts tuple records from OpenAPI document components (schemas with prefixItems).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <returns>List of RecordParameters for tuple types, or null if no tuple schemas exist.</returns>
    public static List<RecordParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false)
    {
        if (openApiDoc.Components?.Schemas == null)
        {
            return null;
        }

        var result = new List<RecordParameters>();

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var schemaName = schema.Key;
            var schemaValue = schema.Value;

            if (schemaValue is not OpenApiSchema actualSchema)
            {
                continue;
            }

            // Skip deprecated if configured
            if (!includeDeprecated && actualSchema.Deprecated)
            {
                continue;
            }

            // Only process schemas with prefixItems
            if (!actualSchema.HasPrefixItems())
            {
                continue;
            }

            var tupleInfo = actualSchema.GetTupleInfo(openApiDoc, registry);
            if (tupleInfo == null || tupleInfo.PrefixItems.Count == 0)
            {
                continue;
            }

            var recordParams = CreateTupleRecord(
                schemaName,
                tupleInfo,
                actualSchema.Description);
            result.Add(recordParams);
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Extracts tuple records filtered by path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <returns>List of RecordParameters for tuple types in the segment, or null if none exist.</returns>
    public static List<RecordParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false)
    {
        if (openApiDoc.Components?.Schemas == null)
        {
            return null;
        }

        // Get schemas used by the path segment
        var schemasInSegment = PathSegmentHelper.GetSchemasUsedBySegment(openApiDoc, pathSegment);

        var result = new List<RecordParameters>();

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var schemaName = schema.Key;
            var schemaValue = schema.Value;

            // Skip if not in this segment
            if (!schemasInSegment.Contains(schemaName))
            {
                continue;
            }

            if (schemaValue is not OpenApiSchema actualSchema)
            {
                continue;
            }

            // Skip deprecated if configured
            if (!includeDeprecated && actualSchema.Deprecated)
            {
                continue;
            }

            // Only process schemas with prefixItems
            if (!actualSchema.HasPrefixItems())
            {
                continue;
            }

            var tupleInfo = actualSchema.GetTupleInfo(openApiDoc, registry);
            if (tupleInfo == null || tupleInfo.PrefixItems.Count == 0)
            {
                continue;
            }

            var recordParams = CreateTupleRecord(
                schemaName,
                tupleInfo,
                actualSchema.Description);
            result.Add(recordParams);
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Checks if a schema is a tuple type (has prefixItems).
    /// Used by SchemaExtractor to skip tuple schemas.
    /// </summary>
    /// <param name="schema">The schema to check.</param>
    /// <returns>True if the schema is a tuple type.</returns>
    public static bool IsTupleSchema(IOpenApiSchema schema)
        => schema.HasPrefixItems();

    /// <summary>
    /// Extracts tuple records for a specific set of schema names.
    /// Used when generating tuples for only schemas used by a path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="schemaNames">Set of schema names to include.</param>
    /// <param name="pathSegment">Optional path segment for namespace.</param>
    /// <param name="registry">Optional conflict registry.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <returns>List of RecordParameters for matching tuple types.</returns>
    public static List<RecordParameters>? ExtractForSchemas(
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false)
    {
        if (openApiDoc.Components?.Schemas == null || schemaNames.Count == 0)
        {
            return null;
        }

        var result = new List<RecordParameters>();

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var schemaName = schema.Key;
            var schemaValue = schema.Value;

            // Skip if not in the requested set
            if (!schemaNames.Contains(schemaName))
            {
                continue;
            }

            if (schemaValue is not OpenApiSchema actualSchema)
            {
                continue;
            }

            // Skip deprecated if configured
            if (!includeDeprecated && actualSchema.Deprecated)
            {
                continue;
            }

            // Only process schemas with prefixItems
            if (!actualSchema.HasPrefixItems())
            {
                continue;
            }

            var tupleInfo = actualSchema.GetTupleInfo(openApiDoc, registry);
            if (tupleInfo == null || tupleInfo.PrefixItems.Count == 0)
            {
                continue;
            }

            var recordParams = CreateTupleRecord(
                schemaName,
                tupleInfo,
                actualSchema.Description);
            result.Add(recordParams);
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Generates the content for a single tuple record using GenerateContentForRecords.
    /// </summary>
    /// <param name="recordParameters">The record parameters for the tuple.</param>
    /// <param name="ns">The namespace to use.</param>
    /// <returns>The generated C# code.</returns>
    public static string GenerateTupleContent(
        RecordParameters recordParameters,
        string ns)
    {
        var recordsContainer = new RecordsParameters(
            HeaderContent: HeaderBuilder.WithUsings("System.CodeDom.Compiler"),
            Namespace: ns,
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.None,
            Parameters: [recordParameters]);

        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            recordsContainer);

        return generator.Generate();
    }

    /// <summary>
    /// Creates a RecordParameters for a tuple schema.
    /// </summary>
    private static RecordParameters CreateTupleRecord(
        string schemaName,
        TupleInfo tupleInfo,
        string? description)
    {
        var parameters = new List<ParameterBaseParameters>();

        // Add prefix items as record parameters
        foreach (var item in tupleInfo.PrefixItems)
        {
            var typeName = item.IsNullable ? $"{item.CSharpType}?" : item.CSharpType;

            parameters.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: typeName,
                IsNullableType: item.IsNullable,
                IsReferenceType: item.CSharpType.IsReferenceType(),
                Name: item.Name,
                DefaultValue: null));
        }

        // Add trailing array for mixed mode (prefixItems + items)
        if (!tupleInfo.IsStrictTuple && tupleInfo.AdditionalItemsType != null)
        {
            parameters.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: $"{tupleInfo.AdditionalItemsType}[]",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "AdditionalItems",
                DefaultValue: null));
        }

        // Build documentation
        var summary = description ?? $"Tuple type with {tupleInfo.PrefixItems.Count} elements.";

        return new RecordParameters(
            DocumentationTags: new CodeDocumentationTags(summary),
            DeclarationModifier: DeclarationModifiers.PublicRecord,
            Name: OpenApiSchemaExtensions.SanitizeSchemaName(schemaName),
            Parameters: parameters);
    }
}