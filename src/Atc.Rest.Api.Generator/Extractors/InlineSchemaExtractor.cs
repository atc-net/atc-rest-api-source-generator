namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts inline object schemas from OpenAPI operations and generates record types for them.
/// Inline schemas are those defined directly in request bodies or responses rather than as $ref to components/schemas.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class InlineSchemaExtractor
{
    /// <param name="schema">The schema.</param>
    extension(IOpenApiSchema? schema)
    {
        /// <summary>
        /// Gets a value indicating whether the schema is an inline object with properties, not a <c>$ref</c> (<see cref="IsInlineObjectSchema"/>).
        /// </summary>
        public bool IsInlineObject
            => IsInlineObjectSchema(schema);
    }

    /// <summary>
    /// Checks if a schema is an inline object with properties (not a $ref to components/schemas).
    /// </summary>
    /// <param name="schema">The schema to check.</param>
    /// <returns>True if the schema is an inline object with properties.</returns>
    public static bool IsInlineObjectSchema(IOpenApiSchema? schema)
    {
        // Must not be a reference (those are handled by the normal schema extraction)
        if (schema is OpenApiSchemaReference)
        {
            return false;
        }

        // Must be an actual schema with object type and properties
        if (schema is not OpenApiSchema actualSchema)
        {
            return false;
        }

        return actualSchema.Type?.HasFlag(JsonSchemaType.Object) == true &&
               actualSchema.Properties is not null &&
               actualSchema.Properties.Count > 0;
    }

    /// <summary>
    /// Generates a unique type name for an inline schema based on operation context.
    /// </summary>
    /// <param name="operationId">The operation ID (e.g., "listReports").</param>
    /// <param name="context">The context: "Response", "Request", or "ResponseItem".</param>
    /// <param name="statusCode">Optional status code for response types (e.g., "200").</param>
    /// <returns>The generated type name (e.g., "ListReportsResponseItem").</returns>
    public static string GenerateInlineTypeName(
        string operationId,
        string context,
        string? statusCode = null)
    {
        var baseName = operationId.ToPascalCaseForDotNet();

        // For non-200 responses, include the status code to avoid conflicts
        if (!string.IsNullOrEmpty(statusCode) && statusCode != "200" && statusCode != "default")
        {
            return $"{baseName}{context}{statusCode}";
        }

        return $"{baseName}{context}";
    }

    /// <summary>
    /// Extracts a RecordParameters from an inline schema.
    /// </summary>
    /// <param name="schema">The inline schema to extract.</param>
    /// <param name="typeName">The generated type name for the record.</param>
    /// <param name="registry">Optional type conflict registry.</param>
    /// <returns>The record parameters for code generation.</returns>
    public static RecordParameters ExtractRecordFromInlineSchema(
        OpenApiSchema schema,
        string typeName,
        TypeConflictRegistry? registry = null)
    {
        var properties = schema.Properties?.ToList() ?? [];
        var required = schema.Required ?? new HashSet<string>(StringComparer.Ordinal);

        var parametersList = new List<ParameterBaseParameters>();
        var propertyNames = new PropertyNameRegistry();

        foreach (var prop in properties)
        {
            // A valid, unique name for every property; the same key listed twice (allOf composition) is skipped
            var propName = propertyNames.Resolve(prop.Key, prop.Key.ToPascalCaseForDotNet());
            if (propName is null)
            {
                continue;
            }

            // Rename property if it matches the enclosing type name
            if (propName.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            {
                propName = propName + "Value";
            }

            var isRequired = required.Contains(prop.Key, StringComparer.Ordinal);

            // Get C# type using model type mapping
            var csharpType = prop.Value.HasAdditionalProperties()
                ? prop.Value.GetDictionaryTypeString(isRequired, registry) ?? prop.Value.ToCSharpTypeForModel(isRequired, registry)
                : prop.Value.ToCSharpTypeForModel(isRequired, registry);

            // Extract nullability
            var isNullableType = csharpType.EndsWith("?", StringComparison.Ordinal);
            var cleanTypeName = isNullableType
                ? csharpType.Substring(0, csharpType.Length - 1)
                : csharpType;

            // Get validation attributes
            var validationAttributes = prop.Value.GetValidationAttributes(isRequired);

            var isReferenceType = cleanTypeName.IsReferenceType();

            IList<AttributeParameters>? attributes = BuildAttributes(prop.Key, propName, validationAttributes);

            // Extract default value (null for an optional property: it may be absent)
            var defaultValue = DefaultValueHelper.ForRecordParameter(DefaultValueHelper.ExtractSchemaDefault(prop.Value, cleanTypeName, registry), isRequired, isNullableType);

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

        // Sort: required without defaults first, then parameters with defaults
        var sortedParameters = parametersList
            .OrderBy(p => p.DefaultValue is not null ? 1 : 0)
            .ToList();

        return new RecordParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.PublicSealedRecord,
            Name: typeName,
            Parameters: sortedParameters,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            });
    }

    /// <summary>
    /// Inline-enum-aware variant of <see cref="ExtractRecordFromInlineSchema"/>.
    /// For each property whose schema is an inline enum (scalar or array-of), generates
    /// a named C# enum type (<c>{TypeName}{PropertyName}</c>), uses that name as the
    /// property's C# type, and records the enum in the dedup map for the caller to
    /// emit as a separate file. All other properties fall through to the existing
    /// <c>ToCSharpTypeForModel</c> mapping.
    /// </summary>
    public static RecordParameters ExtractRecordFromInlineSchemaWithInlineEnums(
        OpenApiSchema schema,
        string typeName,
        string ns,
        string pathSegment,
        Dictionary<string, InlineEnumInfo> inlineEnumsByValuesKey,
        TypeConflictRegistry? registry = null)
    {
        var properties = schema.Properties?.ToList() ?? [];
        var required = schema.Required ?? new HashSet<string>(StringComparer.Ordinal);

        var parametersList = new List<ParameterBaseParameters>();
        var propertyNames = new PropertyNameRegistry();

        foreach (var prop in properties)
        {
            var propName = propertyNames.Resolve(prop.Key, prop.Key.ToPascalCaseForDotNet());
            if (propName is null)
            {
                continue;
            }

            if (propName.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            {
                propName = propName + "Value";
            }

            var isRequired = required.Contains(prop.Key, StringComparer.Ordinal);

            string csharpType;
            if (prop.Value.IsInlineEnum)
            {
                var actualSchema = (OpenApiSchema)prop.Value;
                var enumTypeName = ResolveOrRegisterInlineEnum(actualSchema, typeName, propName, ns, pathSegment, inlineEnumsByValuesKey, registry?.ComponentTypeNames);
                csharpType = enumTypeName ?? "string";
                if (!isRequired || actualSchema.IsNullable)
                {
                    csharpType += "?";
                }
            }
            else if (InlineEnumExtractor.TryGetInlineEnumArrayItems(prop.Value, out var arrayItemSchema))
            {
                var enumTypeName = ResolveOrRegisterInlineEnum(arrayItemSchema!, typeName, propName, ns, pathSegment, inlineEnumsByValuesKey, registry?.ComponentTypeNames);
                csharpType = enumTypeName is not null
                    ? $"List<{enumTypeName}>"
                    : prop.Value.ToCSharpTypeForModel(isRequired, registry);

                if (!isRequired || ((OpenApiSchema)prop.Value).IsNullable)
                {
                    csharpType += "?";
                }
            }
            else
            {
                csharpType = prop.Value.HasAdditionalProperties()
                    ? prop.Value.GetDictionaryTypeString(isRequired, registry) ?? prop.Value.ToCSharpTypeForModel(isRequired, registry)
                    : prop.Value.ToCSharpTypeForModel(isRequired, registry);
            }

            var isNullableType = csharpType.EndsWith("?", StringComparison.Ordinal);
            var cleanTypeName = isNullableType
                ? csharpType.Substring(0, csharpType.Length - 1)
                : csharpType;

            var validationAttributes = prop.Value.GetValidationAttributes(isRequired);
            var isReferenceType = cleanTypeName.IsReferenceType();

            IList<AttributeParameters>? attributes = BuildAttributes(prop.Key, propName, validationAttributes);

            var defaultValue = DefaultValueHelper.ForRecordParameter(DefaultValueHelper.ExtractSchemaDefault(prop.Value, cleanTypeName, registry), isRequired, isNullableType);

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

        var sortedParameters = parametersList
            .OrderBy(p => p.DefaultValue is not null ? 1 : 0)
            .ToList();

        return new RecordParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.PublicSealedRecord,
            Name: typeName,
            Parameters: sortedParameters,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            });
    }

    /// <summary>
    /// Builds the attribute list for a record parameter, combining an optional
    /// JsonPropertyName attribute with the schema validation attributes.
    /// </summary>
    private static IList<AttributeParameters>? BuildAttributes(
        string jsonKey,
        string propertyName,
        IList<string> validationAttributes)
    {
        var attributesList = new List<AttributeParameters>();

        var jsonPropertyNameAttribute = JsonPropertyNameHelper.CreateJsonPropertyNameAttribute(jsonKey, propertyName);
        if (jsonPropertyNameAttribute is not null)
        {
            attributesList.Add(jsonPropertyNameAttribute);
        }

        if (validationAttributes.Count > 0)
        {
            attributesList.AddRange(validationAttributes.Select(attr => new AttributeParameters(attr, null)));
        }

        return attributesList.Count > 0
            ? attributesList
            : null;
    }

    /// <summary>
    /// Dedup-aware resolver mirroring the one in <c>SchemaExtractor</c>. Returns an
    /// existing inline enum's type name when an identical value set has already been
    /// seen, otherwise constructs and registers a fresh <see cref="InlineEnumInfo"/>.
    /// </summary>
    private static string? ResolveOrRegisterInlineEnum(
        OpenApiSchema enumSchema,
        string parentTypeName,
        string propertyName,
        string ns,
        string pathSegment,
        Dictionary<string, InlineEnumInfo> inlineEnumsByValuesKey,
        ISet<string>? componentTypeNames)
    {
        var valuesKey = InlineEnumExtractor.GetEnumValuesKey(enumSchema);
        if (inlineEnumsByValuesKey.TryGetValue(valuesKey, out var existing))
        {
            return existing.TypeName;
        }

        var enumTypeName = InlineEnumExtractor.GenerateInlineEnumTypeName(parentTypeName, propertyName, componentTypeNames);
        var enumParams = InlineEnumExtractor.ExtractEnumFromInlineSchema(enumSchema, enumTypeName, ns);
        if (enumParams is null)
        {
            return null;
        }

        inlineEnumsByValuesKey[valuesKey] = new InlineEnumInfo(enumTypeName, pathSegment, enumParams, valuesKey);
        return enumTypeName;
    }
}