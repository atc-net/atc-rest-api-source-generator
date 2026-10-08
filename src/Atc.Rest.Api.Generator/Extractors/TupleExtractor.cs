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
        => openApiDoc?.Components?.Schemas is null
            ? null
            : ExtractForSchemas(openApiDoc, projectName, [.. openApiDoc.Components.Schemas.Keys], pathSegment: null, registry, includeDeprecated);

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
        if (openApiDoc.Components?.Schemas is null || schemaNames.Count == 0)
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
            if (tupleInfo is null || tupleInfo.PrefixItems.Count == 0)
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
            HeaderContent: HeaderBuilder.WithUsings(
                NamespaceConstants.System,
                NamespaceConstants.SystemCodeDomCompiler,
                NamespaceConstants.SystemCollectionsGeneric,
                NamespaceConstants.SystemTextJson,
                NamespaceConstants.SystemTextJsonSerialization),
            Namespace: ns,
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.None,
            Parameters: [recordParameters]);

        var generator = new GenerateContentForRecords(
            new CodeDocumentationTagsGenerator(),
            recordsContainer);

        return generator.Generate() + Environment.NewLine + Environment.NewLine + GenerateConverter(recordParameters);
    }

    /// <summary>
    /// The converter of a tuple record: on the wire a tuple is a JSON array (<c>[55.5, 12.5]</c>), not an
    /// object with the record's member names. Each position is read and written with the serializer's own
    /// options, so converters and naming policies for the item types keep applying.
    /// </summary>
    private static string GenerateConverter(RecordParameters recordParameters)
    {
        var name = recordParameters.Name;
        var parameters = recordParameters.Parameters ?? [];
        var fixedItems = parameters.Where(p => !IsAdditionalItems(p)).ToList();
        var additionalItems = parameters.FirstOrDefault(IsAdditionalItems);
        var additionalItemType = additionalItems?.TypeName.Substring(0, additionalItems.TypeName.Length - 2);

        var sb = new StringBuilder();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Reads and writes a <see cref=\"{name}\"/> as the JSON array the specification describes.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public sealed class {name}JsonConverter : JsonConverter<{name}>");
        sb.AppendLine("{");
        sb.AppendLine(4, $"public override {name} Read(");
        sb.AppendLine(8, "ref Utf8JsonReader reader,");
        sb.AppendLine(8, "Type typeToConvert,");
        sb.AppendLine(8, "JsonSerializerOptions options)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "if (reader.TokenType != JsonTokenType.StartArray)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, $"throw new JsonException(\"A {name} is a JSON array.\");");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "reader.Read();");

        for (var i = 0; i < fixedItems.Count; i++)
        {
            var item = fixedItems[i];
            var typeName = ItemTypeName(item);
            sb.AppendLine();
            sb.AppendLine(8, "if (reader.TokenType == JsonTokenType.EndArray)");
            sb.AppendLine(8, "{");
            sb.AppendLine(12, $"throw new JsonException(\"A {name} has {fixedItems.Count} item(s); item {i + 1} ({item.Name}) is missing.\");");
            sb.AppendLine(8, "}");
            sb.AppendLine();
            sb.AppendLine(8, $"var item{i} = JsonSerializer.Deserialize(ref reader, Info<{typeName}>(options)){(typeName.EndsWith("?", StringComparison.Ordinal) ? string.Empty : "!")};");
            sb.AppendLine(8, "reader.Read();");
        }

        sb.AppendLine();
        if (additionalItems is not null)
        {
            sb.AppendLine(8, $"var additionalItems = new List<{additionalItemType}>();");
            sb.AppendLine(8, "while (reader.TokenType != JsonTokenType.EndArray)");
            sb.AppendLine(8, "{");
            sb.AppendLine(12, $"additionalItems.Add(JsonSerializer.Deserialize(ref reader, Info<{additionalItemType}>(options))!);");
            sb.AppendLine(12, "reader.Read();");
            sb.AppendLine(8, "}");
        }
        else
        {
            sb.AppendLine(8, "// Items beyond the ones the tuple names are not part of the model");
            sb.AppendLine(8, "while (reader.TokenType != JsonTokenType.EndArray)");
            sb.AppendLine(8, "{");
            sb.AppendLine(12, "reader.Skip();");
            sb.AppendLine(12, "reader.Read();");
            sb.AppendLine(8, "}");
        }

        var arguments = fixedItems.Select((_, i) => $"item{i}").ToList();
        if (additionalItems is not null)
        {
            arguments.Add("additionalItems.ToArray()");
        }

        sb.AppendLine();
        sb.AppendLine(8, $"return new {name}({string.Join(", ", arguments)});");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        sb.AppendLine(4, "public override void Write(");
        sb.AppendLine(8, "Utf8JsonWriter writer,");
        sb.AppendLine(8, $"{name} value,");
        sb.AppendLine(8, "JsonSerializerOptions options)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "writer.WriteStartArray();");
        foreach (var item in fixedItems)
        {
            sb.AppendLine(8, $"JsonSerializer.Serialize(writer, value.{item.Name}, Info<{ItemTypeName(item)}>(options));");
        }

        if (additionalItems is not null)
        {
            sb.AppendLine();
            sb.AppendLine(8, $"foreach (var item in value.{additionalItems.Name} ?? [])");
            sb.AppendLine(8, "{");
            sb.AppendLine(12, $"JsonSerializer.Serialize(writer, item, Info<{additionalItemType}>(options));");
            sb.AppendLine(8, "}");
            sb.AppendLine();
        }

        sb.AppendLine(8, "writer.WriteEndArray();");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        sb.AppendLine(4, "// The metadata the options resolve: no reflection-based overload, so the converter works in a Native AOT app");
        sb.AppendLine(4, "private static global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> Info<T>(JsonSerializerOptions options) => (global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));");
        sb.Append('}');

        return sb.ToString();
    }

    private static bool IsAdditionalItems(ParameterBaseParameters parameter)
        => parameter.Name == "AdditionalItems" && parameter.TypeName.EndsWith("[]", StringComparison.Ordinal);

    private static string ItemTypeName(ParameterBaseParameters parameter)
        => parameter.IsNullableType && !parameter.TypeName.EndsWith("?", StringComparison.Ordinal)
            ? parameter.TypeName + "?"
            : parameter.TypeName;

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
        if (!tupleInfo.IsStrictTuple && tupleInfo.AdditionalItemsType is not null)
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
            DeclarationModifier: DeclarationModifiers.PublicSealedRecord,
            Name: OpenApiSchemaExtensions.SanitizeSchemaName(schemaName),
            Parameters: parameters,
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
                new("JsonConverter", $"typeof({OpenApiSchemaExtensions.SanitizeSchemaName(schemaName)}JsonConverter)"),
            });
    }
}