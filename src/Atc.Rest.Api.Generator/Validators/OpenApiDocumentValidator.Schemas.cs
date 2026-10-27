// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CommentTypo
// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The schema rules (ATC_API_SCH): types, references, constraints, discriminators and schema names.
/// </summary>
public static partial class OpenApiDocumentValidator
{
    /// <summary>
    /// Validates schemas (ATC_API_SCH001-012, ATC_API_SCH014).
    /// </summary>
    private static List<DiagnosticMessage> ValidateSchemas(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Components?.Schemas is null)
        {
            return diagnostics;
        }

        foreach (var schemaEntry in document.Components.Schemas)
        {
            var schemaName = schemaEntry.Key;
            var schema = schemaEntry.Value;
            var schemaStart = diagnostics.Count;

            // Get the actual schema (handle references)
            var actualSchema = schema;
            if (schema is OpenApiSchemaReference { Target: not null } schemaRef)
            {
                actualSchema = schemaRef.Target;
            }

            // ATC_API_SCH014: Multiple non-null types (OpenAPI 3.1 type arrays)
            ValidateMultipleNonNullTypes(diagnostics, sourceFilePath, schemaName, actualSchema);

            // ATC_API_SCH015: $ref with sibling properties (OpenAPI 3.1)
            ValidateRefWithSiblingProperties(diagnostics, sourceFilePath, schemaName, schema);

            // ATC_API_SCH016: const value (JSON Schema 2020-12)
            ValidateConstValue(diagnostics, sourceFilePath, schemaName, actualSchema);

            // ATC_API_SCH017: unevaluatedProperties (JSON Schema 2020-12)
            ValidateUnevaluatedProperties(diagnostics, sourceFilePath, schemaName, actualSchema);

            // ATC_API_SCH022: properties but no required list - every property optional, generated nullable
            if (actualSchema.Properties is { Count: > 0 } &&
                actualSchema.Required is not { Count: > 0 } &&
                actualSchema.GetSchemaType() == "object")
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.SchemaWithoutRequiredProperties,
                    $"Schema '{schemaName}' has properties but no 'required' list, so every property is optional and " +
                    "generated nullable (with a '= null' default). List in 'required' the properties that are always there: what the API always sends (a response), what it always needs (a request). " +
                    $"Location: #/components/schemas/{schemaName}",
                    DiagnosticSeverity.Info,
                    sourceFilePath));
            }

            var schemaType = actualSchema.GetSchemaType();

            // Validate based on schema type
            if (schemaType == "array")
            {
                ValidateArraySchema(diagnostics, sourceFilePath, schemaName, actualSchema);
            }
            else if (schemaType == "object")
            {
                ValidateObjectSchema(diagnostics, sourceFilePath, schemaName, schema, actualSchema);
            }

            // ATC_API_SCH008: Enum name not using correct casing style
            if (actualSchema.Enum is { Count: > 0 } &&
                !CasingHelper.IsPascalCase(schemaName))
            {
                var suggested = CasingHelper.SuggestPascalCase(schemaName);
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.EnumNameCasing,
                    $"Enum '{schemaName}' is not using PascalCase. " +
                    $"Suggestion: '{suggested}'. " +
                    $"Location: #/components/schemas/{schemaName}",
                    DiagnosticSeverity.Warning,
                    sourceFilePath));
            }

            InScope(diagnostics, schemaStart, SpecSuppressions.Schema(schemaName));
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates that schemas with multiple non-null types (OpenAPI 3.1 feature) report a warning.
    /// </summary>
    private static void ValidateMultipleNonNullTypes(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema)
    {
        // Check if schema has multiple non-null types
        if (schema.HasMultipleNonNullTypes())
        {
            var typeNames = schema.GetAllNonNullTypeNames();
            var primaryType = schema.GetPrimaryNonNullType();
            var primaryTypeName = primaryType?.ToString()?.ToLowerInvariant() ?? "unknown";

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.MultipleNonNullTypes,
                $"Schema '{schemaName}' has multiple non-null types [{string.Join(", ", typeNames)}]. " +
                $"Using primary type '{primaryTypeName}' for C# code generation. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath,
                LineNumber: null,
                ColumnNumber: null,
                Context: $"Schema: {schemaName}",
                Suggestions:
                [
                    "Consider using a single type instead of type array",
                    "Use oneOf/anyOf for polymorphic types"
                ]));
        }

        // Also check properties for multiple types
        if (schema is OpenApiSchema { Properties: not null } actualSchema)
        {
            foreach (var property in actualSchema.Properties)
            {
                var propName = property.Key;
                var propSchema = property.Value;

                // Resolve reference if needed
                var actualPropSchema = propSchema;
                if (propSchema is OpenApiSchemaReference { Target: not null } propRef)
                {
                    actualPropSchema = propRef.Target;
                }

                if (actualPropSchema.HasMultipleNonNullTypes())
                {
                    var typeNames = actualPropSchema.GetAllNonNullTypeNames();
                    var primaryType = actualPropSchema.GetPrimaryNonNullType();
                    var primaryTypeName = primaryType?.ToString()?.ToLowerInvariant() ?? "unknown";

                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.MultipleNonNullTypes,
                        $"Property '{propName}' in schema '{schemaName}' has multiple non-null types [{string.Join(", ", typeNames)}]. " +
                        $"Using primary type '{primaryTypeName}' for C# code generation. " +
                        $"Location: #/components/schemas/{schemaName}/properties/{propName}",
                        DiagnosticSeverity.Warning,
                        sourceFilePath,
                        LineNumber: null,
                        ColumnNumber: null,
                        Context: $"Property: {schemaName}.{propName}",
                        Suggestions:
                        [
                            "Consider using a single type instead of type array",
                            "Use oneOf/anyOf for polymorphic types"
                        ]));
                }
            }
        }
    }

    /// <summary>
    /// Validates that $ref schemas with sibling properties are detected (OpenAPI 3.1 info).
    /// </summary>
    private static void ValidateRefWithSiblingProperties(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema)
    {
        // Check if this is a reference with sibling properties
        if (schema.HasRefSiblingProperties())
        {
            var siblingProps = new List<string>();

            if (schema is OpenApiSchemaReference schemaRef)
            {
                if (!string.IsNullOrEmpty(schemaRef.Description))
                {
                    siblingProps.Add("description");
                }

                if (schemaRef.Deprecated)
                {
                    siblingProps.Add("deprecated");
                }

                if (schemaRef.Default is not null)
                {
                    siblingProps.Add("default");
                }
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RefWithSiblingProperties,
                $"Schema '{schemaName}' uses $ref with sibling properties [{string.Join(", ", siblingProps)}]. " +
                $"This is an OpenAPI 3.1 feature. The sibling properties will override the referenced schema's properties. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Info,
                sourceFilePath,
                LineNumber: null,
                ColumnNumber: null,
                Context: $"Schema: {schemaName}",
                Suggestions:
                [
                    "This is supported - sibling properties override referenced schema properties",
                    "For OpenAPI 3.0 compatibility, move overrides to a separate schema using allOf"
                ]));
        }

        // Also check properties for $ref with siblings
        if (schema is OpenApiSchema { Properties: not null } actualSchema)
        {
            foreach (var property in actualSchema.Properties)
            {
                var propName = property.Key;
                var propSchema = property.Value;

                if (propSchema.HasRefSiblingProperties())
                {
                    var siblingProps = new List<string>();

                    if (propSchema is OpenApiSchemaReference propRef)
                    {
                        if (!string.IsNullOrEmpty(propRef.Description))
                        {
                            siblingProps.Add("description");
                        }

                        if (propRef.Deprecated)
                        {
                            siblingProps.Add("deprecated");
                        }

                        if (propRef.Default is not null)
                        {
                            siblingProps.Add("default");
                        }
                    }

                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.RefWithSiblingProperties,
                        $"Property '{propName}' in schema '{schemaName}' uses $ref with sibling properties [{string.Join(", ", siblingProps)}]. " +
                        $"This is an OpenAPI 3.1 feature. The sibling properties will override the referenced schema's properties. " +
                        $"Location: #/components/schemas/{schemaName}/properties/{propName}",
                        DiagnosticSeverity.Info,
                        sourceFilePath,
                        LineNumber: null,
                        ColumnNumber: null,
                        Context: $"Property: {schemaName}.{propName}",
                        Suggestions:
                        [
                            "This is supported - sibling properties override referenced schema properties",
                            "For OpenAPI 3.0 compatibility, move overrides to a separate schema using allOf"
                        ]));
                }
            }
        }
    }

    /// <summary>
    /// Validates schemas that use const value (JSON Schema 2020-12 feature).
    /// </summary>
    private static void ValidateConstValue(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema)
    {
        if (schema.HasConstValue())
        {
            var constValue = schema.GetConstValue();
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.SchemaUsesConstValue,
                $"Schema '{schemaName}' uses const value '{constValue}' (JSON Schema 2020-12). " +
                $"This value will be used as the default and only valid value for this property. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Info,
                sourceFilePath,
                LineNumber: null,
                ColumnNumber: null,
                Context: $"Schema: {schemaName}",
                Suggestions:
                [
                    $"The const value '{constValue}' will be used as a fixed value",
                    "Consider using enum with a single value for better OpenAPI 3.0 compatibility"
                ]));
        }

        // Also check properties for const values
        if (schema is OpenApiSchema { Properties: not null } actualSchema)
        {
            foreach (var property in actualSchema.Properties)
            {
                if (property.Value.HasConstValue())
                {
                    var constValue = property.Value.GetConstValue();
                    diagnostics.Add(new DiagnosticMessage(
                        RuleIdentifiers.SchemaUsesConstValue,
                        $"Property '{property.Key}' in schema '{schemaName}' uses const value '{constValue}' (JSON Schema 2020-12). " +
                        $"This value will be used as the default and only valid value for this property. " +
                        $"Location: #/components/schemas/{schemaName}/properties/{property.Key}",
                        DiagnosticSeverity.Info,
                        sourceFilePath,
                        LineNumber: null,
                        ColumnNumber: null,
                        Context: $"Property: {schemaName}.{property.Key}",
                        Suggestions:
                        [
                            $"The const value '{constValue}' will be used as a fixed value",
                            "Consider using enum with a single value for better OpenAPI 3.0 compatibility"
                        ]));
                }
            }
        }
    }

    /// <summary>
    /// Validates schemas that use unevaluatedProperties (JSON Schema 2020-12 feature).
    /// </summary>
    private static void ValidateUnevaluatedProperties(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema)
    {
        if (schema.HasUnevaluatedPropertiesRestriction())
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.UnevaluatedPropertiesNotSupported,
                $"Schema '{schemaName}' uses unevaluatedProperties: false (JSON Schema 2020-12). " +
                $"This restricts additional properties in composition but is not fully supported in code generation. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath,
                LineNumber: null,
                ColumnNumber: null,
                Context: $"Schema: {schemaName}",
                Suggestions:
                [
                    "unevaluatedProperties affects allOf/oneOf/anyOf composition validation",
                    "For code generation, additionalProperties: false provides similar behavior",
                    "Manual validation may be needed for strict enforcement"
                ]));
        }
    }

    private static void ValidateArraySchema(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema actualSchema)
    {
        // ATC_API_SCH001: Missing title on array type
        if (string.IsNullOrEmpty(actualSchema.Title))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ArrayTitleMissing,
                $"Missing title on array type '#/components/schemas/{schemaName}'. " +
                $"Add a 'title' property to the schema.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
        else if (actualSchema.Title!.Length > 0 && char.IsLower(actualSchema.Title[0]))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ArrayTitleNotUppercase,
                $"Title on array type '{actualSchema.Title}' is not starting with uppercase. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // ATC_API_SCH006: Object name not using correct casing style (applies to arrays too)
        if (!CasingHelper.IsPascalCase(schemaName))
        {
            var suggested = CasingHelper.SuggestPascalCase(schemaName);
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ObjectNameCasing,
                $"Schema '{schemaName}' is not using PascalCase. " +
                $"Suggestion: '{suggested}'. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    private static void ValidateObjectSchema(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema,
        IOpenApiSchema actualSchema)
    {
        // ATC_API_SCH003: Missing title on object type
        if (string.IsNullOrEmpty(actualSchema.Title))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ObjectTitleMissing,
                $"Missing title on object type '#/components/schemas/{schemaName}'. " +
                $"Add a 'title' property to the schema.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
        else if (actualSchema.Title!.Length > 0 && char.IsLower(actualSchema.Title[0]))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ObjectTitleNotUppercase,
                $"Title on object type '{actualSchema.Title}' is not starting with uppercase. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // ATC_API_SCH006: Object name not using correct casing style
        if (!CasingHelper.IsPascalCase(schemaName))
        {
            var suggested = CasingHelper.SuggestPascalCase(schemaName);
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ObjectNameCasing,
                $"Schema '{schemaName}' is not using PascalCase. " +
                $"Suggestion: '{suggested}'. " +
                $"Location: #/components/schemas/{schemaName}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // Validate properties
        if (actualSchema.Properties is not null)
        {
            foreach (var propertyEntry in actualSchema.Properties)
            {
                var propertyKey = propertyEntry.Key;
                var propertySchema = propertyEntry.Value;

                ValidateObjectProperty(diagnostics, sourceFilePath, schemaName, schema, propertyKey, propertySchema);
            }
        }
    }

    private static void ValidateObjectProperty(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        IOpenApiSchema schema,
        string propertyKey,
        IOpenApiSchema propertySchema)
    {
        // ATC_API_SCH012: Missing key/name for object property
        if (string.IsNullOrEmpty(propertyKey))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.PropertyKeyMissing,
                $"Missing key/name for one or more properties on object type '#/components/schemas/{schemaName}'.",
                DiagnosticSeverity.Error,
                sourceFilePath));
            return;
        }

        // ATC_API_SCH007: Object property name not using correct casing style
        if (!CasingHelper.IsCamelCase(propertyKey))
        {
            var suggested = CasingHelper.SuggestCamelCase(propertyKey);
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.PropertyNameCasing,
                $"Property '{propertyKey}' in schema '{schemaName}' is not using camelCase. " +
                $"Suggestion: '{suggested}'. " +
                $"Location: #/components/schemas/{schemaName}/properties/{propertyKey}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        // Get actual property schema
        var actualPropertySchema = propertySchema;
        if (propertySchema is OpenApiSchemaReference { Target: not null } propRef)
        {
            actualPropertySchema = propRef.Target;
        }

        var propertyType = actualPropertySchema.GetSchemaType();

        // ATC_API_SCH010: Implicit object definition on property not supported
        if (propertyType == "object")
        {
            if (!propertySchema.IsSchemaReference() &&
                actualPropertySchema.AdditionalProperties is null)
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.ImplicitObjectNotSupported,
                    $"Implicit object definition on property '{propertyKey}' in type '#/components/schemas/{schemaName}' is not supported. " +
                    $"Use a $ref to a named schema instead.",
                    DiagnosticSeverity.Error,
                    sourceFilePath));
            }
        }
        else if (propertyType == "array")
        {
            ValidateArrayProperty(diagnostics, sourceFilePath, schemaName, propertyKey, actualPropertySchema);
        }
    }

    private static void ValidateArrayProperty(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string schemaName,
        string propertyKey,
        IOpenApiSchema actualPropertySchema)
    {
        var items = actualPropertySchema.Items;

        // A tuple (prefixItems) is an array whose positions are typed one by one: it needs no items
        var tupleCandidate = actualPropertySchema.Dereferenced;
        if (items is null && tupleCandidate?.HasPrefixItems == true)
        {
            return;
        }

        // ATC_API_SCH011: Array property missing items specification
        if (items is null)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ArrayPropertyMissingItems,
                $"Not specifying items for array property '{propertyKey}' in type '#/components/schemas/{schemaName}' is not supported. " +
                $"Add an 'items' specification.",
                DiagnosticSeverity.Error,
                sourceFilePath));
            return;
        }

        // Get actual items schema
        var actualItems = items;
        if (items is OpenApiSchemaReference { Target: not null } itemsRef)
        {
            actualItems = itemsRef.Target;
        }

        var itemsType = actualItems.GetSchemaType();

        // ATC_API_SCH009: Array property missing data type specification
        if (string.IsNullOrEmpty(itemsType) && !IsSpecialPropertyName(propertyKey))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ArrayPropertyMissingType,
                $"Not specifying a data type for array property '{propertyKey}' in type '#/components/schemas/{schemaName}' is not supported. " +
                $"Add a type or $ref to the items specification.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }

        // ATC_API_SCH005: Implicit object definition in array property not supported
        if (!string.IsNullOrEmpty(itemsType) &&
            itemsType == "object" &&
            !items.IsSchemaReference() &&
            !IsSimpleDataType(itemsType))
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.ImplicitArrayObjectNotSupported,
                $"Implicit object definition on property '{propertyKey}' in array type '#/components/schemas/{schemaName}' is not supported. " +
                $"Use a $ref to a named schema instead.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Checks if a property name is a special name that doesn't require strict type checking.
    /// </summary>
    private static bool IsSpecialPropertyName(string propertyName)
        => string.Equals(propertyName, "items", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(propertyName, "result", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(propertyName, "results", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if a schema is an array type.
    /// </summary>
    private static bool IsArraySchema(
        IOpenApiSchema schema,
        OpenApiDocument document)
    {
        var actualSchema = schema;

        // Resolve reference
        if (schema is OpenApiSchemaReference schemaRef)
        {
            actualSchema = schemaRef.Target ?? schema;
        }

        if (actualSchema is OpenApiSchema openApiSchema)
        {
            return openApiSchema.Type?.ToString()?.Equals("array", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        return false;
    }

    /// <summary>
    /// Checks if a schema represents a paginated response (object with items/results array).
    /// </summary>
    private static bool IsPaginatedSchema(
        IOpenApiSchema schema,
        OpenApiDocument document,
        int depth = 0)
    {
        // An allOf cycle is SCH023 before this runs; the depth bounds one that check does not see
        if (depth > MaxSchemaDepth)
        {
            return false;
        }

        // Check explicit x-pagination annotation first (authoritative when present)
        var annotation = schema.GetPaginationAnnotation();
        if (annotation.HasValue)
        {
            return annotation.Value;
        }

        var actualSchema = schema;

        // Resolve reference
        if (schema is OpenApiSchemaReference schemaRef)
        {
            actualSchema = schemaRef.Target ?? schema;

            // Also check annotation on the resolved target
            var targetAnnotation = actualSchema.GetPaginationAnnotation();
            if (targetAnnotation.HasValue)
            {
                return targetAnnotation.Value;
            }
        }

        if (actualSchema is not OpenApiSchema openApiSchema)
        {
            return false;
        }

        // Heuristic fallback: check direct properties if it's an object type
        if (openApiSchema.Type.HasValue &&
            openApiSchema.Type.Value.HasFlag(JsonSchemaType.Object) &&
            openApiSchema.Properties is not null)
        {
            foreach (var prop in openApiSchema.Properties)
            {
                if (PaginationConventions.IsArrayPropertyName(prop.Key) &&
                    IsArraySchema(prop.Value, document))
                {
                    return true;
                }
            }
        }

        // Check allOf compositions (common in pagination patterns like PaginatedResult<T>)
        if (openApiSchema.AllOf is { Count: > 0 })
        {
            foreach (var allOfSchema in openApiSchema.AllOf)
            {
                if (IsPaginatedSchema(allOfSchema, document, depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a schema is an object containing any array property.
    /// This handles wrapper response objects like ResendEventsResponse that contain arrays.
    /// </summary>
    private static bool IsObjectContainingArray(
        IOpenApiSchema schema,
        OpenApiDocument document,
        int depth = 0)
    {
        if (depth > MaxSchemaDepth)
        {
            return false;
        }

        var actualSchema = schema;

        if (schema is OpenApiSchemaReference schemaRef)
        {
            actualSchema = schemaRef.Target ?? schema;
        }

        if (actualSchema is not OpenApiSchema openApiSchema)
        {
            return false;
        }

        if (openApiSchema.Type.HasValue &&
            openApiSchema.Type.Value.HasFlag(JsonSchemaType.Object) &&
            openApiSchema.Properties is not null)
        {
            foreach (var prop in openApiSchema.Properties)
            {
                if (IsArraySchema(prop.Value, document))
                {
                    return true;
                }
            }
        }

        if (openApiSchema.AllOf is { Count: > 0 })
        {
            foreach (var allOfSchema in openApiSchema.AllOf)
            {
                if (IsObjectContainingArray(allOfSchema, document, depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that no component schema is typed only 'null' (ATC_API_SCH024): it has no values and no C# type,
    /// so nothing is generated for it while every reference to it names a type that does not exist.
    /// </summary>
    private static List<DiagnosticMessage> ValidateNullOnlyTypes(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();
        foreach (var schema in document.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            if (schema.Value is OpenApiSchema { Type: JsonSchemaType.Null })
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.SchemaTypeOnlyNull,
                    $"Schema '{schema.Key}' has the type 'null' and no other: it has no values and no C# type, so nothing is generated for it.",
                    DiagnosticSeverity.Error,
                    sourceFilePath,
                    Context: $"Schema: {schema.Key}",
                    Suggestions: ["Give the schema a type (type: [string, 'null'] for a nullable string)", "Or remove the schema and the references to it"]));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates that no two distinct schema names sanitize to the same C# identifier (ATC_API_SCH018).
    /// Such collisions would produce duplicate type definitions in the generated code.
    /// </summary>
    private static List<DiagnosticMessage> ValidateSchemaNameCollisions(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.Components?.Schemas is null)
        {
            return diagnostics;
        }

        // Group the original schema names by the C# identifier they sanitize to. Use the same
        // sanitization the generator applies so detection matches real generation behavior.
        var groupsByIdentifier = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var schemaName in document.Components.Schemas.Keys)
        {
            var sanitized = OpenApiSchemaExtensions.SanitizeSchemaName(schemaName);
            if (!groupsByIdentifier.TryGetValue(sanitized, out var names))
            {
                names = new List<string>();
                groupsByIdentifier[sanitized] = names;
            }

            names.Add(schemaName);
        }

        foreach (var group in groupsByIdentifier)
        {
            if (group.Value.Count <= 1)
            {
                continue;
            }

            var collidingNames = string.Join("', '", group.Value);
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.SchemaNameCollision,
                $"Schema names '{collidingNames}' all sanitize to the C# identifier '{group.Key}', which would produce duplicate type definitions. Rename all but one to avoid the collision.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates that components.mediaTypes entries do not wrap anonymous inline schemas (ATC_API_SCH019).
    /// An array whose items is a named $ref is acceptable and does NOT trigger the warning.
    /// </summary>
    private static void ValidateComponentsMediaTypes(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        if (document.Components?.MediaTypes is null)
        {
            return;
        }

        foreach (var mediaType in document.Components.MediaTypes)
        {
            var schema = mediaType.Value?.Schema;
            if (schema is null)
            {
                continue;
            }

            // Array with named-ref items is OK (e.g., type: array, items: {$ref: '#/components/schemas/Foo'}).
            // Only flag schemas that are truly anonymous (no $ref, no title).
            if (schema.GetSchemaType() == "array" && schema.Items is OpenApiSchemaReference)
            {
                continue;
            }

            if (schema is not OpenApiSchemaReference && string.IsNullOrEmpty(schema.Title))
            {
                diagnostics.Add(new DiagnosticMessage(
                    RuleId: RuleIdentifiers.AnonymousInlineMediaTypeSchema,
                    Message: $"Reusable media type '{mediaType.Key}' in components.mediaTypes wraps an anonymous inline schema. " +
                             "Reference a named components.schemas entry instead to ensure a stable type name in generated code.",
                    Severity: DiagnosticSeverity.Warning,
                    FilePath: sourceFilePath));
            }
        }
    }

    /// <summary>
    /// Warns when a polymorphic schema has a discriminator block without 'propertyName' and
    /// no common string property can be auto-detected across variants (ATC_API_SCH020).
    /// </summary>
    /// <summary>
    /// Returns true when the discriminator at the given JSON pointer path
    /// has no propertyName — used to suppress parser errors that are only
    /// valid for OAS 3.0/3.1 but not 3.2 (where propertyName is optional).
    /// </summary>
    private static bool IsDiscriminatorWithoutPropertyName(
        OpenApiDocument document,
        string pointer)
    {
        // pointer format: #/components/schemas/{Name}/discriminator
        var parts = pointer.TrimStart('#').TrimStart('/').Split('/');
        if (parts.Length < 3 ||
            parts[0] != "components" ||
            parts[1] != "schemas")
        {
            return false;
        }

        var schemaName = parts[2];
        if (document.Components?.Schemas is null ||
            !document.Components.Schemas.TryGetValue(schemaName, out var schema))
        {
            return false;
        }

        return string.IsNullOrEmpty(schema.DiscriminatorPropertyName);
    }

    private static void ValidateDiscriminatorPropertyNames(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
        {
            return;
        }

        foreach (var schema in document.Components.Schemas)
        {
            var schemaValue = schema.Value;

            // Skip references — they are validated at their target
            if (schemaValue is OpenApiSchemaReference)
            {
                continue;
            }

            // Only check schemas with polymorphic composition and a discriminator block
            if (!schemaValue.HasPolymorphicComposition || !schemaValue.HasDiscriminatorBlock())
            {
                continue;
            }

            // If propertyName is set, there is nothing to warn about
            if (!string.IsNullOrEmpty(schemaValue.DiscriminatorPropertyName))
            {
                continue;
            }

            // Try auto-detect — if it succeeds, generation will proceed normally
            if (!string.IsNullOrEmpty(schemaValue.DetectDiscriminatorProperty(document)))
            {
                continue;
            }

            diagnostics.Add(new DiagnosticMessage(
                RuleId: RuleIdentifiers.DiscriminatorMissingPropertyName,
                Message: $"Schema '{schema.Key}' has a discriminator block without 'propertyName', " +
                         "and no common string property could be auto-detected across all polymorphic variants. " +
                         "Add 'propertyName' to the discriminator block for reliable polymorphic code generation.",
                Severity: DiagnosticSeverity.Warning,
                FilePath: sourceFilePath));
        }
    }
}