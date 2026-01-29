// ReSharper disable InvertIf
// ReSharper disable PossibleUnintendedLinearSearchInSet
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI schema definitions and converts them to RecordsParameters for code generation.
/// </summary>
public static class SchemaExtractor
{
    /// <summary>
    /// Common property names for pagination result arrays.
    /// These are used to detect generic pagination base schemas.
    /// </summary>
    private static readonly string[] PaginationArrayPropertyNames
        = ["results", "items", "data", "values", "content"];

    /// <summary>
    /// Extracts model records from OpenAPI document components.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    /// <returns>RecordsParameters containing all model record definitions, or null if no schemas exist.</returns>
    public static RecordsParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false,
        bool generatePartialModels = false)
    {
        var recordParametersList = ExtractIndividual(openApiDoc, includeDeprecated: includeDeprecated, generatePartialModels: generatePartialModels);

        if (recordParametersList == null || recordParametersList.Count == 0)
        {
            return null;
        }

        var headerContent = BuildHeaderContent(recordParametersList);

        return new RecordsParameters(
            HeaderContent: headerContent,
            Namespace: NamespaceBuilder.ForModels(projectName),
            DocumentationTags: null,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters: recordParametersList);
    }

    /// <summary>
    /// Extracts model records from OpenAPI document components filtered by path segment.
    /// Only includes schemas that are used by operations in the specified path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets").</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    /// <returns>RecordsParameters containing filtered model record definitions, or null if no schemas exist.</returns>
    public static RecordsParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        bool generatePartialModels = false)
    {
        var recordParametersList = ExtractIndividual(openApiDoc, pathSegment, registry, includeDeprecated, generatePartialModels: generatePartialModels);

        if (recordParametersList == null || recordParametersList.Count == 0)
        {
            return null;
        }

        var headerContent = BuildHeaderContent(recordParametersList);

        return new RecordsParameters(
            HeaderContent: headerContent,
            Namespace: NamespaceBuilder.ForModels(projectName, pathSegment),
            DocumentationTags: null,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters: recordParametersList);
    }

    /// <summary>
    /// Extracts model records from OpenAPI document components for specific schemas.
    /// Used for generating shared types (used by multiple segments) or segment-specific types.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="schemaNames">The specific schema names to extract.</param>
    /// <param name="pathSegment">The path segment for namespace (null = shared namespace without segment).</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    /// <param name="includeSharedModelsUsing">Whether to include a using directive for the shared Models namespace (for segment-specific files that reference shared types).</param>
    /// <returns>RecordsParameters containing model record definitions, or null if no matching schemas.</returns>
    public static RecordsParameters? ExtractForSchemas(
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        bool generatePartialModels = false,
        bool includeSharedModelsUsing = false)
    {
        if (schemaNames == null || schemaNames.Count == 0)
        {
            return null;
        }

        var recordParametersList = ExtractIndividual(openApiDoc, schemaNames, registry, includeDeprecated, generatePartialModels: generatePartialModels);

        if (recordParametersList == null || recordParametersList.Count == 0)
        {
            return null;
        }

        // Build header with optional shared models using
        var sharedModelsNamespace = includeSharedModelsUsing && !string.IsNullOrEmpty(pathSegment)
            ? NamespaceBuilder.ForModels(projectName)
            : null;
        var headerContent = BuildHeaderContent(recordParametersList, sharedModelsNamespace);

        // Use shared namespace if no pathSegment, otherwise segment-specific namespace
        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);

        return new RecordsParameters(
            HeaderContent: headerContent,
            Namespace: ns,
            DocumentationTags: null,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters: recordParametersList);
    }

    /// <summary>
    /// Extracts model records from OpenAPI document components for specific schemas,
    /// also extracting inline enum definitions from properties.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="schemaNames">The specific schema names to extract.</param>
    /// <param name="pathSegment">The path segment for namespace (null = shared namespace without segment).</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    /// <param name="includeSharedModelsUsing">Whether to include a using directive for the shared Models namespace.</param>
    /// <returns>A tuple containing RecordsParameters and a list of inline enums discovered.</returns>
    public static (RecordsParameters? Records, List<InlineEnumInfo> InlineEnums) ExtractForSchemasWithInlineEnums(
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        bool generatePartialModels = false,
        bool includeSharedModelsUsing = false)
    {
        var inlineEnums = new List<InlineEnumInfo>();

        if (schemaNames == null || schemaNames.Count == 0)
        {
            return (null, inlineEnums);
        }

        // Use shared namespace if no pathSegment, otherwise segment-specific namespace
        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);
        var effectivePathSegment = pathSegment ?? "Shared";

        var recordParametersList = ExtractIndividualWithInlineEnums(
            openApiDoc,
            schemaNames,
            ns,
            effectivePathSegment,
            inlineEnums,
            registry,
            includeDeprecated,
            generatePartialModels: generatePartialModels);

        if (recordParametersList == null || recordParametersList.Count == 0)
        {
            return (null, inlineEnums);
        }

        // Build header with optional shared models using
        var sharedModelsNamespace = includeSharedModelsUsing && !string.IsNullOrEmpty(pathSegment)
            ? NamespaceBuilder.ForModels(projectName)
            : null;
        var headerContent = BuildHeaderContent(recordParametersList, sharedModelsNamespace);

        var records = new RecordsParameters(
            HeaderContent: headerContent,
            Namespace: ns,
            DocumentationTags: null,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters: recordParametersList);

        return (records, inlineEnums);
    }

    /// <summary>
    /// Extracts individual record parameters from OpenAPI document components.
    /// Returns a list of individual records without combined header/namespace.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    /// <returns>List of RecordParameters for each model, or null if no schemas exist.</returns>
    public static List<RecordParameters>? ExtractIndividual(
        OpenApiDocument openApiDoc,
        bool includeDeprecated = false,
        bool generatePartialModels = false)
        => ExtractIndividual(openApiDoc, schemaFilter: null, includeDeprecated: includeDeprecated, generatePartialModels: generatePartialModels);

    /// <summary>
    /// Extracts individual record parameters from OpenAPI document components filtered by path segment.
    /// Returns a list of individual records without combined header/namespace.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets").</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    /// <returns>List of RecordParameters for each model used by operations in the path segment, or null if no schemas exist.</returns>
    public static List<RecordParameters>? ExtractIndividual(
        OpenApiDocument openApiDoc,
        string pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        bool generatePartialModels = false)
    {
        if (string.IsNullOrEmpty(pathSegment))
        {
            return ExtractIndividual(openApiDoc, schemaFilter: null, registry: registry, includeDeprecated: includeDeprecated, generatePartialModels: generatePartialModels);
        }

        // Get schemas used by operations in this path segment
        var usedSchemas = PathSegmentHelper.GetSchemasUsedBySegment(openApiDoc, pathSegment);
        return ExtractIndividual(openApiDoc, usedSchemas, registry, includeDeprecated, generatePartialModels: generatePartialModels);
    }

    /// <summary>
    /// Extracts individual record parameters from OpenAPI document components with polymorphic support.
    /// Returns a list of individual records without combined header/namespace.
    /// Polymorphic base schemas are skipped; variant records include inheritance.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="polymorphicConfigs">Polymorphic configurations for inheritance tracking.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    /// <returns>List of RecordParameters for each model, or null if no schemas exist.</returns>
    public static List<RecordParameters>? ExtractIndividualWithPolymorphism(
        OpenApiDocument openApiDoc,
        Dictionary<string, PolymorphicConfig>? polymorphicConfigs,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        bool generatePartialModels = false)
        => ExtractIndividual(openApiDoc, schemaFilter: null, registry, includeDeprecated, polymorphicConfigs, generatePartialModels);

    /// <summary>
    /// Extracts individual record parameters from OpenAPI document components filtered by path segment with polymorphic support.
    /// Returns a list of individual records without combined header/namespace.
    /// Polymorphic base schemas are skipped; variant records include inheritance.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets").</param>
    /// <param name="polymorphicConfigs">Polymorphic configurations for inheritance tracking.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    /// <returns>List of RecordParameters for each model used by operations in the path segment, or null if no schemas exist.</returns>
    public static List<RecordParameters>? ExtractIndividualWithPolymorphism(
        OpenApiDocument openApiDoc,
        string pathSegment,
        Dictionary<string, PolymorphicConfig>? polymorphicConfigs,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        bool generatePartialModels = false)
    {
        if (string.IsNullOrEmpty(pathSegment))
        {
            return ExtractIndividual(openApiDoc, schemaFilter: null, registry: registry, includeDeprecated: includeDeprecated, polymorphicConfigs: polymorphicConfigs, generatePartialModels: generatePartialModels);
        }

        // Get schemas used by operations in this path segment
        var usedSchemas = PathSegmentHelper.GetSchemasUsedBySegment(openApiDoc, pathSegment);
        return ExtractIndividual(openApiDoc, usedSchemas, registry, includeDeprecated, polymorphicConfigs, generatePartialModels);
    }

    /// <summary>
    /// Extracts individual record parameters from OpenAPI document components.
    /// Optionally filters to only include schemas in the provided set.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="schemaFilter">Optional set of schema names to include. If null, all schemas are included.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated schemas.</param>
    /// <param name="polymorphicConfigs">Optional polymorphic configurations for adding inheritance to variant types.</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    /// <returns>List of RecordParameters for each model, or null if no schemas exist.</returns>
    private static List<RecordParameters>? ExtractIndividual(
        OpenApiDocument openApiDoc,
        HashSet<string>? schemaFilter,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        Dictionary<string, PolymorphicConfig>? polymorphicConfigs = null,
        bool generatePartialModels = false)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Components?.Schemas == null || openApiDoc.Components.Schemas.Count == 0)
        {
            return null;
        }

        var recordParametersList = new List<RecordParameters>();

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var originalSchemaName = schema.Key;
            var schemaValue = schema.Value;

            // Apply schema filter if provided (uses original name from OpenAPI spec)
            if (schemaFilter != null && !schemaFilter.Contains(originalSchemaName))
            {
                continue;
            }

            // Skip schema references as they point to other schemas
            if (schemaValue is OpenApiSchemaReference)
            {
                continue;
            }

            // Skip deprecated schemas if not including them
            if (!includeDeprecated && schemaValue is OpenApiSchema { Deprecated: true })
            {
                continue;
            }

            // Skip polymorphic base schemas (oneOf/anyOf) - they're handled by PolymorphicTypeExtractor
            if (schemaValue.HasPolymorphicComposition())
            {
                continue;
            }

            if (schemaValue is OpenApiSchema actualSchema)
            {
                // Skip array schemas (like "Pets"), they're handled inline
                if (actualSchema.Type == JsonSchemaType.Array)
                {
                    continue;
                }

                // Skip enum schemas - handled by EnumExtractor
                if (actualSchema is { Type: JsonSchemaType.String, Enum.Count: > 0 })
                {
                    continue;
                }

                // Skip tuple schemas - handled by TupleExtractor
                if (actualSchema.HasPrefixItems())
                {
                    continue;
                }

                if (actualSchema.Type == JsonSchemaType.Object)
                {
                    // Sanitize schema name - replace dots with underscores for valid C# identifiers
                    var schemaName = OpenApiSchemaExtensions.SanitizeSchemaName(originalSchemaName);

                    // Check if this schema is a polymorphic variant and get its base type (uses original name)
                    var baseTypeName = PolymorphicTypeExtractor.GetBaseTypeForVariant(originalSchemaName, polymorphicConfigs);

                    // Check if this is a pagination base schema (has results: array with empty items)
                    if (IsPaginationBaseSchema(actualSchema))
                    {
                        var genericRecordParams = ExtractGenericPaginatedRecord(schemaName, actualSchema, registry, generatePartialModels);
                        if (genericRecordParams != null)
                        {
                            recordParametersList.Add(genericRecordParams);
                        }
                    }
                    else
                    {
                        var recordParams = ExtractRecordFromSchema(schemaName, actualSchema, registry, baseTypeName, generatePartialModels);
                        if (recordParams != null)
                        {
                            recordParametersList.Add(recordParams);
                        }
                    }
                }
            }
        }

        return recordParametersList.Count > 0 ? recordParametersList : null;
    }

    /// <summary>
    /// Extracts individual record parameters with inline enum support.
    /// </summary>
    private static List<RecordParameters>? ExtractIndividualWithInlineEnums(
        OpenApiDocument openApiDoc,
        HashSet<string>? schemaFilter,
        string ns,
        string pathSegment,
        List<InlineEnumInfo> inlineEnums,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        bool generatePartialModels = false)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Components?.Schemas == null || openApiDoc.Components.Schemas.Count == 0)
        {
            return null;
        }

        // Dictionary to track inline enums by their values key for deduplication
        var inlineEnumsByValuesKey = new Dictionary<string, InlineEnumInfo>(StringComparer.Ordinal);

        var recordParametersList = new List<RecordParameters>();

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var originalSchemaName = schema.Key;
            var schemaValue = schema.Value;

            // Apply schema filter if provided (uses original name from OpenAPI spec)
            if (schemaFilter != null && !schemaFilter.Contains(originalSchemaName))
            {
                continue;
            }

            // Skip schema references as they point to other schemas
            if (schemaValue is OpenApiSchemaReference)
            {
                continue;
            }

            // Skip deprecated schemas if not including them
            if (!includeDeprecated && schemaValue is OpenApiSchema { Deprecated: true })
            {
                continue;
            }

            // Skip polymorphic base schemas (oneOf/anyOf) - they're handled by PolymorphicTypeExtractor
            if (schemaValue.HasPolymorphicComposition())
            {
                continue;
            }

            if (schemaValue is OpenApiSchema actualSchema)
            {
                // Skip array schemas (like "Pets"), they're handled inline
                if (actualSchema.Type == JsonSchemaType.Array)
                {
                    continue;
                }

                // Skip enum schemas - handled by EnumExtractor
                if (actualSchema is { Type: JsonSchemaType.String, Enum.Count: > 0 })
                {
                    continue;
                }

                // Skip tuple schemas - handled by TupleExtractor
                if (actualSchema.HasPrefixItems())
                {
                    continue;
                }

                if (actualSchema.Type == JsonSchemaType.Object)
                {
                    // Sanitize schema name - replace dots with underscores for valid C# identifiers
                    var schemaName = OpenApiSchemaExtensions.SanitizeSchemaName(originalSchemaName);

                    // Check if this is a pagination base schema (has results: array with empty items)
                    if (IsPaginationBaseSchema(actualSchema))
                    {
                        var genericRecordParams = ExtractGenericPaginatedRecord(schemaName, actualSchema, registry, generatePartialModels);
                        if (genericRecordParams != null)
                        {
                            recordParametersList.Add(genericRecordParams);
                        }
                    }
                    else
                    {
                        var recordParams = ExtractRecordFromSchemaWithInlineEnums(
                            schemaName,
                            actualSchema,
                            ns,
                            pathSegment,
                            inlineEnumsByValuesKey,
                            registry,
                            generatePartialModels);

                        if (recordParams != null)
                        {
                            recordParametersList.Add(recordParams);
                        }
                    }
                }
            }
        }

        // Collect all unique inline enums
        inlineEnums.AddRange(inlineEnumsByValuesKey.Values);

        return recordParametersList.Count > 0 ? recordParametersList : null;
    }

    /// <summary>
    /// Checks if a schema represents a pagination base (has results/items/data/values/content: array with empty/untyped items).
    /// </summary>
    private static bool IsPaginationBaseSchema(OpenApiSchema schema)
    {
        if (schema.Properties == null)
        {
            return false;
        }

        // Look for common pagination array property names
        IOpenApiSchema? resultsProp = null;
        foreach (var propName in PaginationArrayPropertyNames)
        {
            if (schema.Properties.TryGetValue(propName, out var propValue))
            {
                resultsProp = propValue;
                break;
            }
        }

        if (resultsProp is not OpenApiSchema resultsSchema)
        {
            return false;
        }

        // Check if it's an array type
        if (resultsSchema.Type != JsonSchemaType.Array)
        {
            return false;
        }

        return resultsSchema.Items switch
        {
            // Check if items are empty/untyped (items: {} or no items)
            null => true,

            // Check if items schema has no type defined (empty schema)
            OpenApiSchema itemSchema => itemSchema.Type == null ||
                                        itemSchema.Type == JsonSchemaType.Null,
            _ => false,
        };
    }

    /// <summary>
    /// Extracts a generic record definition from a pagination base schema.
    /// Generates PaginatedResult&lt;T&gt; with T[] Results property.
    /// </summary>
    private static RecordParameters? ExtractGenericPaginatedRecord(
        string schemaName,
        OpenApiSchema schema,
        TypeConflictRegistry? registry = null,
        bool generatePartialModels = false)
    {
        var properties = schema.Properties?.ToList() ?? [];
        var required = schema.Required ?? new HashSet<string>(StringComparer.Ordinal);

        if (properties.Count == 0)
        {
            return null;
        }

        var parametersList = new List<ParameterBaseParameters>();

        foreach (var prop in properties)
        {
            var propName = prop.Key.ToPascalCaseForDotNet();
            var isRequired = required.Contains(prop.Key, StringComparer.Ordinal);

            string csharpType;
            string? genericTypeName = null;
            var isGenericListType = false;

            // Special handling for pagination array properties - make it generic List<T>
            if (PaginationArrayPropertyNames.Any(name =>
                prop.Key.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                csharpType = "List<T>";
                genericTypeName = null;
                isGenericListType = false;
            }
            else
            {
                csharpType = prop.Value.ToCSharpTypeForModel(isRequired, registry);
            }

            // Extract nullability from the type name - the code generation library handles adding "?"
            var isNullableType = csharpType.EndsWith("?", StringComparison.Ordinal);
            var cleanTypeName = isNullableType
                ? csharpType.Substring(0, csharpType.Length - 1)
                : csharpType;

            // Get validation attributes from OpenAPI schema constraints
            var validationAttributes = prop.Value.GetValidationAttributes(isRequired);

            // Use TypeHelpers extension methods for type classification
            var isReferenceType = cleanTypeName.IsReferenceType();

            // Convert validation attributes to AttributeParameters
            IList<AttributeParameters>? attributes = null;
            if (validationAttributes.Count > 0)
            {
                attributes = validationAttributes
                    .Select(attr => new AttributeParameters(attr, null))
                    .ToList();
            }

            parametersList.Add(new ParameterBaseParameters(
                Attributes: attributes,
                GenericTypeName: genericTypeName,
                IsGenericListType: isGenericListType,
                TypeName: cleanTypeName,
                IsNullableType: isNullableType,
                IsReferenceType: isReferenceType,
                Name: propName,
                DefaultValue: null));
        }

        // Return record with generic type parameter <T>
        var declarationModifier = generatePartialModels
            ? DeclarationModifiers.PublicPartialRecord
            : DeclarationModifiers.PublicSealedRecord;

        return new RecordParameters(
            DocumentationTags: null,
            DeclarationModifier: declarationModifier,
            Name: $"{schemaName}<T>",
            Parameters: parametersList);
    }

    /// <summary>
    /// Extracts a single record definition from an OpenAPI schema.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="schema">The OpenAPI schema.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="baseTypeName">Optional base type name for polymorphic inheritance.</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    private static RecordParameters? ExtractRecordFromSchema(
        string schemaName,
        OpenApiSchema schema,
        TypeConflictRegistry? registry = null,
        string? baseTypeName = null,
        bool generatePartialModels = false)
    {
        var properties = schema.Properties?.ToList() ?? [];
        var required = schema.Required ?? new HashSet<string>(StringComparer.Ordinal);

        // Generate empty records for schemas with no properties (e.g., Unit type for 204 No Content responses)
        // These are valid types in OpenAPI that need to be generated
        var parametersList = new List<ParameterBaseParameters>();
        var seenPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in properties)
        {
            var propName = prop.Key.ToPascalCaseForDotNet();

            // Skip duplicate property names (OpenAPI schemas can have duplicates via allOf composition)
            if (!seenPropertyNames.Add(propName))
            {
                continue;
            }

            // Rename property if it matches the enclosing type name (C# doesn't allow this)
            if (propName.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
            {
                propName = propName + "Value";
            }

            var isRequired = required.Contains(prop.Key, StringComparer.Ordinal);

            // Check for additionalProperties (Dictionary types) first
            // Use extension method for model type mapping
            // Note: ToCSharpTypeForModel() handles nullability based on schema's nullable property,
            // not based on required (only value types are nullable when not required)
            var csharpType = prop.Value.HasAdditionalProperties()
                ? prop.Value.GetDictionaryTypeString(isRequired, registry) ?? prop.Value.ToCSharpTypeForModel(isRequired, registry)
                : prop.Value.ToCSharpTypeForModel(isRequired, registry);

            // Extract nullability from the type name - the code generation library handles adding "?"
            var isNullableType = csharpType.EndsWith("?", StringComparison.Ordinal);
            var cleanTypeName = isNullableType
                ? csharpType.Substring(0, csharpType.Length - 1)
                : csharpType;

            // Get validation attributes from OpenAPI schema constraints
            var validationAttributes = prop.Value.GetValidationAttributes(isRequired);

            // Use TypeHelpers extension methods for type classification
            var isReferenceType = cleanTypeName.IsReferenceType();

            // Convert validation attributes to AttributeParameters
            // Note: The code generation library handles the [property: ...] syntax for records automatically
            IList<AttributeParameters>? attributes = null;
            if (validationAttributes.Count > 0)
            {
                attributes = validationAttributes
                    .Select(attr => new AttributeParameters(attr, null))
                    .ToList();
            }

            // Extract default value from schema
            var defaultValue = ExtractSchemaDefault(prop.Value, cleanTypeName);

            parametersList.Add(new ParameterBaseParameters(
                Attributes: attributes,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: cleanTypeName,
                IsNullableType: isNullableType,
                IsReferenceType: isReferenceType,
                Name: propName,
                DefaultValue: defaultValue));
        }

        // C# records require parameters with default values to come after all required parameters
        // Sort: required without defaults first, then non-required without defaults, then parameters with defaults
        var sortedParameters = parametersList
            .OrderBy(p => p.DefaultValue != null ? 1 : 0)
            .ToList();

        // Build record name with inheritance if this is a polymorphic variant
        var recordName = string.IsNullOrEmpty(baseTypeName)
            ? schemaName
            : $"{schemaName} : {baseTypeName}";

        var declarationModifier = generatePartialModels
            ? DeclarationModifiers.PublicPartialRecord
            : DeclarationModifiers.PublicSealedRecord;

        return new RecordParameters(
            DocumentationTags: null,
            DeclarationModifier: declarationModifier,
            Name: recordName,
            Parameters: sortedParameters);
    }

    /// <summary>
    /// Extracts the default value from an OpenAPI schema and formats it as a C# literal.
    /// </summary>
    private static string? ExtractSchemaDefault(
        IOpenApiSchema? schemaInterface,
        string typeName)
        => DefaultValueHelper.ExtractSchemaDefault(schemaInterface, typeName);

    /// <summary>
    /// Extracts a single record definition from an OpenAPI schema, with inline enum support.
    /// Inline enum properties are detected and tracked in the inlineEnums dictionary.
    /// </summary>
    private static RecordParameters? ExtractRecordFromSchemaWithInlineEnums(
        string schemaName,
        OpenApiSchema schema,
        string ns,
        string pathSegment,
        Dictionary<string, InlineEnumInfo> inlineEnumsByValuesKey,
        TypeConflictRegistry? registry = null,
        bool generatePartialModels = false)
    {
        var properties = schema.Properties?.ToList() ?? [];
        var required = schema.Required ?? new HashSet<string>(StringComparer.Ordinal);

        // Generate empty records for schemas with no properties (e.g., Unit type for 204 No Content responses)
        var parametersList = new List<ParameterBaseParameters>();
        var seenPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in properties)
        {
            var propName = prop.Key.ToPascalCaseForDotNet();

            // Skip duplicate property names (OpenAPI schemas can have duplicates via allOf composition)
            if (!seenPropertyNames.Add(propName))
            {
                continue;
            }

            // Rename property if it matches the enclosing type name (C# doesn't allow this)
            if (propName.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
            {
                propName = propName + "Value";
            }

            var isRequired = required.Contains(prop.Key, StringComparer.Ordinal);
            string csharpType;

            // Check for inline enum before standard type resolution
            if (InlineEnumExtractor.IsInlineEnumSchema(prop.Value))
            {
                var actualSchema = (OpenApiSchema)prop.Value;
                var enumTypeName = InlineEnumExtractor.GenerateInlineEnumTypeName(schemaName, propName);
                var valuesKey = InlineEnumExtractor.GetEnumValuesKey(actualSchema);

                // Check if we already have an enum with the same values (deduplication)
                if (inlineEnumsByValuesKey.TryGetValue(valuesKey, out var existingEnum))
                {
                    // Reuse existing enum type name
                    csharpType = existingEnum.TypeName;
                }
                else
                {
                    // Create new inline enum
                    var enumParams = InlineEnumExtractor.ExtractEnumFromInlineSchema(actualSchema, enumTypeName, ns);
                    if (enumParams != null)
                    {
                        var inlineEnumInfo = new InlineEnumInfo(enumTypeName, pathSegment, enumParams, valuesKey);
                        inlineEnumsByValuesKey[valuesKey] = inlineEnumInfo;
                        csharpType = enumTypeName;
                    }
                    else
                    {
                        // Fallback to string if extraction fails
                        csharpType = "string";
                    }
                }

                // Handle nullable for inline enums
                if (!isRequired || actualSchema.IsNullable())
                {
                    csharpType += "?";
                }
            }
            else
            {
                // Standard type resolution
                csharpType = prop.Value.HasAdditionalProperties()
                    ? prop.Value.GetDictionaryTypeString(isRequired, registry) ?? prop.Value.ToCSharpTypeForModel(isRequired, registry)
                    : prop.Value.ToCSharpTypeForModel(isRequired, registry);
            }

            // Extract nullability from the type name - the code generation library handles adding "?"
            var isNullableType = csharpType.EndsWith("?", StringComparison.Ordinal);
            var cleanTypeName = isNullableType
                ? csharpType.Substring(0, csharpType.Length - 1)
                : csharpType;

            // Get validation attributes from OpenAPI schema constraints
            var validationAttributes = prop.Value.GetValidationAttributes(isRequired);

            // Use TypeHelpers extension methods for type classification
            var isReferenceType = cleanTypeName.IsReferenceType();

            // Convert validation attributes to AttributeParameters
            IList<AttributeParameters>? attributes = null;
            if (validationAttributes.Count > 0)
            {
                attributes = validationAttributes
                    .Select(attr => new AttributeParameters(attr, null))
                    .ToList();
            }

            // Extract default value from schema
            var defaultValue = ExtractSchemaDefault(prop.Value, cleanTypeName);

            parametersList.Add(new ParameterBaseParameters(
                Attributes: attributes,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: cleanTypeName,
                IsNullableType: isNullableType,
                IsReferenceType: isReferenceType,
                Name: propName,
                DefaultValue: defaultValue));
        }

        // C# records require parameters with default values to come after all required parameters
        var sortedParameters = parametersList
            .OrderBy(p => p.DefaultValue != null ? 1 : 0)
            .ToList();

        var declarationModifier = generatePartialModels
            ? DeclarationModifiers.PublicPartialRecord
            : DeclarationModifiers.PublicSealedRecord;

        return new RecordParameters(
            DocumentationTags: null,
            DeclarationModifier: declarationModifier,
            Name: schemaName,
            Parameters: sortedParameters);
    }

    /// <summary>
    /// Builds the header content for generated models file, including required using directives.
    /// Adds Microsoft.AspNetCore.Http if any record uses IFormFile types.
    /// Adds System.Collections.Generic if any record uses Dictionary types.
    /// </summary>
    /// <param name="records">The record parameters to analyze for required usings.</param>
    /// <param name="sharedModelsNamespace">Optional shared models namespace to include for segment-specific files.</param>
    private static string BuildHeaderContent(
        List<RecordParameters> records,
        string? sharedModelsNamespace = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Check if any record uses IFormFile or IFormFileCollection
        var usesFormFile = records.Any(r =>
            r.Parameters?.Any(p =>
                p.TypeName.IndexOf("IFormFile", StringComparison.Ordinal) >= 0) ?? false);

        // Check if any record uses Dictionary types
        var usesDictionary = records.Any(r =>
            r.Parameters?.Any(p =>
                p.TypeName.StartsWith("Dictionary<", StringComparison.Ordinal)) ?? false);

        if (usesFormFile)
        {
            sb.AppendLine("using Microsoft.AspNetCore.Http;");
        }

        sb.AppendLine("using System.CodeDom.Compiler;");

        if (usesDictionary)
        {
            sb.AppendLine("using System.Collections.Generic;");
        }

        sb.AppendLine("using System.ComponentModel.DataAnnotations;");

        // Add shared models namespace for segment-specific files that may reference shared types
        if (!string.IsNullOrEmpty(sharedModelsNamespace))
        {
            sb.AppendLine($"using {sharedModelsNamespace};");
        }

        sb.AppendLine();

        return sb.ToString();
    }
}