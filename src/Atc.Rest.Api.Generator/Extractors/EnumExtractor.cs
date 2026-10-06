namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI enum schema definitions and converts them to EnumParameters for code generation.
/// </summary>
public static class EnumExtractor
{
    /// <summary>
    /// Extracts all enum definitions from OpenAPI document components for a specific path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Users").</param>
    /// <returns>List of EnumParameters for each enum schema, or null if no enums exist.</returns>
    public static List<EnumParameters>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment)
    {
        if (openApiDoc.Components?.Schemas is null ||
            openApiDoc.Components.Schemas.Count == 0)
        {
            return null;
        }

        // Get schemas used by operations in this path segment
        var usedSchemas = string.IsNullOrEmpty(pathSegment)
            ? null
            : PathSegmentHelper.GetSchemasUsedBySegment(openApiDoc, pathSegment);

        return ExtractCore(openApiDoc, projectName, usedSchemas, pathSegment);
    }

    /// <summary>
    /// Extracts enum definitions from OpenAPI document components for specific schemas.
    /// Used for generating shared enums (used by multiple segments) or segment-specific enums.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="schemaNames">The specific schema names to extract.</param>
    /// <param name="pathSegment">The path segment for namespace (null = shared namespace without segment).</param>
    /// <returns>List of EnumParameters for each enum schema, or null if no enums exist.</returns>
    public static List<EnumParameters>? ExtractForSchemas(
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment)
    {
        if (openApiDoc.Components?.Schemas is null ||
            openApiDoc.Components.Schemas.Count == 0 ||
            schemaNames is null ||
            schemaNames.Count == 0)
        {
            return null;
        }

        return ExtractCore(openApiDoc, projectName, schemaNames, pathSegment);
    }

    /// <summary>
    /// Generates the content for a single enum using GenerateContentForEnum - or, for a string enum of a generated
    /// client, as an extensible enum (<see cref="ExtensibleEnumContentGenerator"/>).
    /// </summary>
    /// <param name="parameters">The enum.</param>
    /// <param name="extensible">Whether a string enum is written as an extensible enum (a generated client).</param>
    /// <param name="stringOnTheWire">Whether a string enum carries its string converter even when no value needs
    /// <c>[EnumMember]</c> (the server: a host without a <c>JsonStringEnumConverter</c> would write the member's
    /// number; a converter the host registers still decides).</param>
    public static string GenerateEnumContent(
        EnumParameters parameters,
        bool extensible = false,
        bool stringOnTheWire = false)
    {
        if (extensible && ExtensibleEnumContentGenerator.AppliesTo(parameters))
        {
            return ExtensibleEnumContentGenerator.Generate(parameters);
        }

        // An integer enum is its number on the wire: its own converter says so (a JsonStringEnumConverter the host or a
        // client registers would write the member's name; the generated EnumWireConverterFactory lets this one decide)
        if (!parameters.UseFlags &&
            parameters.Values.Count > 0 &&
            parameters.Values.All(v => v.Value is not null && v.EnumMemberValue is null))
        {
            parameters = parameters with
            {
                HeaderContent = HeaderBuilder.WithUsings(
                    NamespaceConstants.SystemCodeDomCompiler,
                    NamespaceConstants.SystemTextJsonSerialization),
                Attributes = [.. parameters.Attributes ?? [], new AttributeParameters("JsonConverter", $"typeof(JsonNumberEnumConverter<{parameters.EnumTypeName}>)")],
            };
        }

        if (stringOnTheWire &&
            ExtensibleEnumContentGenerator.AppliesTo(parameters) &&
            parameters.Values.All(v => v.EnumMemberValue is null))
        {
            parameters = parameters with
            {
                HeaderContent = HeaderBuilder.WithUsings(
                    NamespaceConstants.SystemCodeDomCompiler,
                    NamespaceConstants.SystemTextJsonSerialization),
                Attributes = [.. parameters.Attributes ?? [], new AttributeParameters("JsonConverter", "typeof(JsonStringEnumConverter)")],
            };
        }

        var generator = new GenerateContentForEnum(
            new CodeDocumentationTagsGenerator(),
            parameters);

        return generator.Generate();
    }

    /// <summary>
    /// Determines if an enum value needs an EnumMember attribute.
    /// This is required when the value contains characters that are not valid in C# identifiers
    /// or when the casing doesn't match PascalCase.
    /// </summary>
    internal static bool NeedsEnumMemberAttribute(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Check for special characters that require transformation
        if (value.Contains("-") ||
            value.Contains(":") ||
            value.Contains("_") ||
            value.Contains(" ") ||
            value.Equals("*", StringComparison.Ordinal))
        {
            return true;
        }

        // Check if first character is lowercase (camelCase)
        return char.IsLower(value[0]);
    }

    /// <summary>
    /// Builds the header content for generated enum files.
    /// </summary>
    /// <param name="hasEnumMemberValues">Whether the enum has values requiring EnumMember attributes.</param>
    internal static string BuildEnumHeaderContent(bool hasEnumMemberValues)
        => hasEnumMemberValues
            ? HeaderBuilder.WithUsings(
                NamespaceConstants.SystemCodeDomCompiler,
                NamespaceConstants.SystemRuntimeSerialization,
                NamespaceConstants.SystemTextJsonSerialization)
            : HeaderBuilder.WithUsings(NamespaceConstants.SystemCodeDomCompiler);

    /// <summary>
    /// Extracts enum value parameters from a list of OpenAPI enum values (JsonNode).
    /// </summary>
    /// <param name="enumValues">The list of enum values from an OpenAPI schema.</param>
    /// <returns>List of EnumValueParameters, or null if no valid values could be extracted.</returns>
    internal static List<EnumValueParameters>? ExtractEnumValues(
        IList<JsonNode>? enumValues,
        IList<string>? memberNames = null)
    {
        if (enumValues is null || enumValues.Count == 0)
        {
            return null;
        }

        var result = new List<EnumValueParameters>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var usedIntegers = new HashSet<long>();
        var usedStrings = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < enumValues.Count; index++)
        {
            var enumValue = enumValues[index];

            // `null` in the enum list (the OpenAPI 3.1 nullable enum) is no member: null is the absence of a value.
            if (enumValue is null || JsonNullSentinel.IsJsonNullSentinel(enumValue))
            {
                continue;
            }

            // x-enum-varnames gives the member its name, by position
            var givenName = memberNames is not null && index < memberNames.Count && !string.IsNullOrWhiteSpace(memberNames[index])
                ? memberNames[index]
                : null;

            // Integer enum values: synthesise a `Value{n}` identifier (or `ValueNegN`
            // for negatives) and carry the explicit numeric value so the generated
            // member reads `Value1 = 1`. JsonValue's underlying number may be parsed
            // as long / int / decimal depending on YAML loader settings, so try the
            // raw JsonElement kind first.
            if (enumValue is JsonValue jsonValue &&
                jsonValue.GetValueKind() == JsonValueKind.Number &&
                long.TryParse(
                    jsonValue.ToJsonString(),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var intValue))
            {
                // A value listed twice is one member (two members with one value would not compile)
                if (!usedIntegers.Add(intValue))
                {
                    continue;
                }

                var identifier = givenName is not null
                    ? CSharpLiteralHelper.ToEnumMemberName(givenName.ToPascalCase(separators: [' ', '-', '_', ':'], removeSeparators: true))
                    : intValue >= 0
                        ? $"Value{intValue}"
                        : $"ValueNeg{-intValue}";
                result.Add(new EnumValueParameters(
                    DocumentationTags: null,
                    DescriptionAttribute: null,
                    Name: UniqueName(identifier),
                    EnumMemberValue: null,
                    Value: intValue));
                continue;
            }

            // OpenAPI string enum values are stored as JsonNode
            var valueStr = enumValue.ToString();

            // A string value is taken as it is (it may itself end in a quote); anything else by its text
            var originalValue = enumValue is JsonValue stringValue && stringValue.GetValueKind() == JsonValueKind.String
                ? stringValue.GetValue<string>()
                : (valueStr ?? string.Empty).Trim('"');

            // The empty string is a value a specification may list ("", "a", "b"): the member Empty carries it
            if (string.IsNullOrEmpty(originalValue))
            {
                if (enumValue is JsonValue emptyValue && emptyValue.GetValueKind() == JsonValueKind.String)
                {
                    result.Add(new EnumValueParameters(
                        DocumentationTags: null,
                        DescriptionAttribute: null,
                        Name: UniqueName(givenName ?? "Empty"),
                        EnumMemberValue: string.Empty,
                        Value: null));
                }

                continue;
            }

            // Check if transformation is needed
            var needsTransformation = NeedsEnumMemberAttribute(originalValue);

            string name;
            string? enumMemberValue;

            if (givenName is not null)
            {
                // x-enum-varnames names the member; the value stays on the wire
                name = givenName.ToPascalCase(separators: [' ', '-', '_', ':'], removeSeparators: true);
                enumMemberValue = originalValue;
            }
            else if (needsTransformation)
            {
                // Transform to valid C# identifier
                name = originalValue.ToPascalCase(separators: [' ', '-', '_', ':'], removeSeparators: true);
                enumMemberValue = originalValue;
            }
            else
            {
                // Use original value as-is
                name = originalValue;
                enumMemberValue = null;
            }

            // A value listed twice is one member (two members with one wire value cannot both be read back)
            if (!usedStrings.Add(originalValue))
            {
                continue;
            }

            // Whatever the value holds (quotes, slashes, a leading digit, a keyword), the member name
            // is a valid identifier and the value itself goes on the wire through [EnumMember].
            if (!CSharpLiteralHelper.IsValidIdentifier(name))
            {
                name = CSharpLiteralHelper.ToEnumMemberName(name);
                enumMemberValue = originalValue;
            }

            // A member renamed to stay unique ("active" and "Active": Active and Active2) keeps its value on the wire
            var uniqueName = UniqueName(name);
            if (!string.Equals(uniqueName, name, StringComparison.Ordinal))
            {
                enumMemberValue = originalValue;
            }

            result.Add(new EnumValueParameters(
                DocumentationTags: null,
                DescriptionAttribute: null,
                Name: uniqueName,
                EnumMemberValue: enumMemberValue,
                Value: null));
        }

        return result.Count > 0 ? result : null;

        // Two values that give the same name ("in-progress", "in_progress") stay two members
        string UniqueName(string name)
        {
            var uniqueName = name;
            var suffix = 1;
            while (!usedNames.Add(uniqueName))
            {
                suffix++;
                uniqueName = name + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return uniqueName;
        }
    }

    /// <summary>
    /// The member names an <c>x-enum-varnames</c> extension gives, by position; <see langword="null"/> without one.
    /// </summary>
    internal static List<string>? MemberNamesOf(IOpenApiSchema schema)
        => schema.Extensions is { } extensions &&
           extensions.TryGetValue("x-enum-varnames", out var extension) &&
           extension is JsonNodeExtension { Node: JsonArray names }
            ? names.Select(n => n is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : string.Empty).ToList()
            : null;

    /// <summary>
    /// Core extraction logic for enum definitions.
    /// </summary>
    private static List<EnumParameters>? ExtractCore(
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string>? schemaFilter,
        string? pathSegment)
    {
        var enumParametersList = new List<EnumParameters>();

        // Use shared namespace if no pathSegment, otherwise segment-specific namespace
        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);

        foreach (var schema in openApiDoc.Components!.Schemas!)
        {
            var originalSchemaName = schema.Key;
            var schemaValue = schema.Value;

            // Apply schema filter if provided (uses original name from OpenAPI spec)
            if (schemaFilter is not null && !schemaFilter.Contains(originalSchemaName))
            {
                continue;
            }

            // Skip schema references
            if (schemaValue is OpenApiSchemaReference)
            {
                continue;
            }

            // Check if this is a string enum schema
            if (schemaValue is OpenApiSchema actualSchema && IsEnumSchema(actualSchema))
            {
                // Sanitize schema name - replace dots with underscores for valid C# identifiers
                var schemaName = OpenApiSchemaExtensions.SanitizeSchemaName(originalSchemaName);
                var enumParams = ExtractEnumFromSchema(schemaName, actualSchema, ns);
                if (enumParams is not null)
                {
                    enumParametersList.Add(enumParams);
                }
            }
        }

        return enumParametersList.Count > 0 ? enumParametersList : null;
    }

    /// <summary>
    /// Checks if a schema represents an enum the generator writes as a C# enum: string or integer members - the
    /// type as written, or as the members say when the schema has no <c>type</c>. A number or boolean enum is its
    /// primitive (no C# enum holds a double).
    /// </summary>
    private static bool IsEnumSchema(OpenApiSchema schema)
    {
        if (schema.Enum is not { Count: > 0 })
        {
            return false;
        }

        // String and integer enums are both supported. For integers, ExtractEnumValues
        // synthesises identifiers (`Value{n}`) and carries the explicit numeric value so
        // the generated C# enum reads `Value1 = 1, Value5 = 5` instead of relying on
        // positional ordering.
        return schema.IsOfType(JsonSchemaType.String) || schema.IsOfType(JsonSchemaType.Integer);
    }

    /// <summary>
    /// Extracts enum parameters from an OpenAPI schema.
    /// </summary>
    private static EnumParameters? ExtractEnumFromSchema(
        string schemaName,
        OpenApiSchema schema,
        string ns)
    {
        var enumValues = ExtractEnumValues(schema.Enum, MemberNamesOf(schema));
        if (enumValues is null)
        {
            return null;
        }

        // Check if any value needs EnumMember attribute
        var hasEnumMemberValues = enumValues.Any(v => v.EnumMemberValue is not null);

        // Build header content with appropriate usings
        var headerContent = BuildEnumHeaderContent(hasEnumMemberValues);

        // Get description from schema if available
        CodeDocumentationTags? docTags = null;
        if (!string.IsNullOrEmpty(schema.Description))
        {
            docTags = new CodeDocumentationTags(schema.Description!);
        }

        // Build attributes list - JsonConverter is added by GenerateContentForEnum when EnumMember values are present
        var attributes = new List<AttributeParameters>
        {
            new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
        };

        return new EnumParameters(
            HeaderContent: headerContent,
            Namespace: ns,
            DocumentationTags: docTags,
            Attributes: attributes,
            DeclarationModifier: DeclarationModifiers.Public,
            EnumTypeName: schemaName,
            UseFlags: false,
            Values: enumValues);
    }
}