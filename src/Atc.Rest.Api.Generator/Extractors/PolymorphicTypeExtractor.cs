namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts polymorphic (oneOf/anyOf) schema definitions and generates code for them.
/// </summary>
public static class PolymorphicTypeExtractor
{
    /// <summary>
    /// Extracts polymorphic configurations from all schemas in the OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <returns>Dictionary mapping schema name to PolymorphicConfig, or null if no polymorphic schemas found.</returns>
    public static Dictionary<string, PolymorphicConfig>? ExtractPolymorphicConfigs(
        OpenApiDocument openApiDoc)
    {
        if (openApiDoc.Components?.Schemas == null ||
            openApiDoc.Components.Schemas.Count == 0)
        {
            return null;
        }

        var configs = new Dictionary<string, PolymorphicConfig>(StringComparer.Ordinal);

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var schemaName = schema.Key;
            var schemaValue = schema.Value;

            // Skip schema references
            if (schemaValue is OpenApiSchemaReference)
            {
                continue;
            }

            // Check if this is a polymorphic schema
            if (!schemaValue.HasPolymorphicComposition())
            {
                continue;
            }

            var config = GetPolymorphicConfig(schemaName, schemaValue, openApiDoc);
            if (config != null)
            {
                configs[schemaName] = config;
            }
        }

        return configs.Count > 0 ? configs : null;
    }

    /// <summary>
    /// Generates code for a polymorphic base type. Uses <c>[JsonPolymorphic]</c> + <c>[JsonDerivedType]</c>
    /// attributes for the standard explicit-discriminator path, or <c>[JsonConverter]</c> when
    /// <see cref="PolymorphicConfig.DefaultVariantTypeName"/> is set (auto-detect or <c>defaultMapping</c>).
    /// </summary>
    /// <param name="config">The polymorphic configuration.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="pathSegment">Optional path segment for sub-namespace.</param>
    /// <returns>The generated C# code for the polymorphic base type.</returns>
    public static string GeneratePolymorphicBaseType(
        PolymorphicConfig config,
        string projectName,
        string? pathSegment = null)
    {
        var sb = new StringBuilder();

        sb.Append(HeaderBuilder.WithUsings(
            NamespaceConstants.SystemCodeDomCompiler,
            NamespaceConstants.SystemTextJsonSerialization));

        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();

        var compositionType = config.IsOneOf ? "oneOf" : "anyOf";
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Polymorphic base type ({compositionType}) with discriminator property '{config.DiscriminatorPropertyName}'.");
        if (!config.IsDiscriminatorExplicit)
        {
            sb.AppendLine("/// Note: Discriminator was auto-detected from common properties.");
        }

        if (config.DefaultVariantTypeName != null)
        {
            var reason = config.IsDiscriminatorExplicit
                ? "'discriminator.defaultMapping' is set"
                : "discriminator was auto-detected";
            sb.AppendLine($"/// Note: Uses a custom JsonConverter because {reason} (fallback: {config.DefaultVariantTypeName}).");
        }

        sb.AppendLine("/// </summary>");

        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");

        if (config.DefaultVariantTypeName != null)
        {
            sb.AppendLine($"[JsonConverter(typeof({config.BaseTypeName}JsonConverter))]");
        }
        else
        {
            sb.AppendLine($"[JsonPolymorphic(TypeDiscriminatorPropertyName = \"{config.DiscriminatorPropertyName}\")]");
            foreach (var variant in config.Variants)
            {
                sb.AppendLine($"[JsonDerivedType(typeof({variant.TypeName}), \"{variant.DiscriminatorValue}\")]");
            }
        }

        sb.AppendLine($"public abstract record {config.BaseTypeName};");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a custom JSON converter for a discriminated polymorphic type with a
    /// <c>defaultMapping</c> fallback (OpenAPI 3.2). The converter reads the discriminator
    /// property, dispatches to the correct variant, and falls back to
    /// <see cref="PolymorphicConfig.DefaultVariantTypeName"/> for unrecognized values.
    /// </summary>
    /// <param name="config">The polymorphic configuration (must have <see cref="PolymorphicConfig.DefaultVariantTypeName"/> set).</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="pathSegment">Optional path segment for sub-namespace.</param>
    /// <returns>The generated C# code for the discriminator converter class.</returns>
    public static string GenerateDiscriminatorFallbackConverter(
        PolymorphicConfig config,
        string projectName,
        string? pathSegment = null)
    {
        var sb = new StringBuilder();

        sb.Append(HeaderBuilder.WithUsings(
            NamespaceConstants.System,
            NamespaceConstants.SystemCodeDomCompiler,
            NamespaceConstants.SystemTextJson,
            NamespaceConstants.SystemTextJsonSerialization));

        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Discriminator-based JSON converter for <see cref=\"{config.BaseTypeName}\"/>.");
        sb.AppendLine($"/// Dispatches on property '{config.DiscriminatorPropertyName}' and falls back to");
        sb.AppendLine($"/// <see cref=\"{config.DefaultVariantTypeName}\"/> for unrecognized discriminator values.");
        sb.AppendLine("/// </summary>");

        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public sealed class {config.BaseTypeName}JsonConverter : JsonConverter<{config.BaseTypeName}>");
        sb.AppendLine("{");

        sb.AppendLine($"    public override {config.BaseTypeName}? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        using var document = JsonDocument.ParseValue(ref reader);");
        sb.AppendLine("        var rawText = document.RootElement.GetRawText();");
        sb.AppendLine();
        sb.AppendLine("        string? discriminatorValue = null;");
        sb.AppendLine($"        if (document.RootElement.TryGetProperty(\"{config.DiscriminatorPropertyName}\", out var discriminatorElement))");
        sb.AppendLine("        {");
        sb.AppendLine("            discriminatorValue = discriminatorElement.GetString();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return discriminatorValue switch");
        sb.AppendLine("        {");

        foreach (var variant in config.Variants)
        {
            sb.AppendLine($"            \"{variant.DiscriminatorValue}\" => JsonSerializer.Deserialize<{variant.TypeName}>(rawText, options),");
        }

        sb.AppendLine($"            _ => JsonSerializer.Deserialize<{config.DefaultVariantTypeName}>(rawText, options),");
        sb.AppendLine("        };");
        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine($"    public override void Write(Utf8JsonWriter writer, {config.BaseTypeName} value, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        JsonSerializer.Serialize(writer, value, value.GetType(), options);");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Gets the polymorphic configuration for a schema.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <param name="schema">The OpenAPI schema.</param>
    /// <param name="document">The OpenAPI document for resolving references.</param>
    /// <returns>The polymorphic configuration, or null if not a valid polymorphic schema.</returns>
    public static PolymorphicConfig? GetPolymorphicConfig(
        string schemaName,
        IOpenApiSchema schema,
        OpenApiDocument document)
    {
        if (!schema.HasPolymorphicComposition())
        {
            return null;
        }

        var isOneOf = schema.HasOneOfComposition();
        var variantNames = schema.GetPolymorphicVariantSchemaNames();

        if (variantNames.Count == 0)
        {
            return null;
        }

        // Get discriminator - explicit or auto-detect
        var discriminatorPropertyName = schema.GetDiscriminatorPropertyName();
        var isExplicit = !string.IsNullOrEmpty(discriminatorPropertyName);

        if (!isExplicit)
        {
            discriminatorPropertyName = schema.DetectDiscriminatorProperty(document);
        }

        // If we still don't have a discriminator, generate a union type with try-parse JsonConverter
        if (string.IsNullOrEmpty(discriminatorPropertyName))
        {
            var unionConfig = new PolymorphicConfig
            {
                BaseTypeName = schemaName,
                IsOneOf = isOneOf,
                IsDiscriminatorExplicit = false,
                UsesCustomConverter = true,
            };

            foreach (var variantName in variantNames)
            {
                unionConfig.Variants.Add(new PolymorphicVariant
                {
                    TypeName = variantName,
                    SchemaRefId = variantName,
                });
            }

            return unionConfig;
        }

        var defaultVariantTypeName = schema.GetDiscriminatorDefaultMappingSchemaName();

        // When the discriminator was auto-detected the detected property is a real CLR property
        // on every variant. STJ [JsonPolymorphic] throws InvalidOperationException when
        // TypeDiscriminatorPropertyName matches an actual property on a derived type (.NET 10+).
        // Route through the custom converter instead, using the first variant as fallback.
        if (!isExplicit && defaultVariantTypeName == null)
        {
            defaultVariantTypeName = variantNames[0];
        }

        var config = new PolymorphicConfig
        {
            BaseTypeName = schemaName,
            DiscriminatorPropertyName = discriminatorPropertyName!,
            IsOneOf = isOneOf,
            IsDiscriminatorExplicit = isExplicit,
            DefaultVariantTypeName = defaultVariantTypeName,
        };

        // Get discriminator mapping (explicit or generate from schema names)
        var explicitMapping = schema.GetDiscriminatorMapping();

        foreach (var variantName in variantNames)
        {
            var discriminatorValue = GetDiscriminatorValueForVariant(variantName, explicitMapping);

            config.Variants.Add(new PolymorphicVariant
            {
                TypeName = variantName,
                DiscriminatorValue = discriminatorValue,
                SchemaRefId = variantName,
            });
        }

        return config;
    }

    /// <summary>
    /// Gets all schema names that are variants of polymorphic types.
    /// </summary>
    /// <param name="configs">The polymorphic configurations.</param>
    /// <returns>A set of schema names that are polymorphic variants.</returns>
    public static HashSet<string> GetPolymorphicVariantSchemaNames(
        Dictionary<string, PolymorphicConfig>? configs)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        if (configs == null)
        {
            return result;
        }

        foreach (var config in configs.Values)
        {
            foreach (var variant in config.Variants)
            {
                result.Add(variant.SchemaRefId);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the base type name for a variant schema, if it's part of a polymorphic type.
    /// </summary>
    /// <param name="schemaName">The schema name to check.</param>
    /// <param name="configs">The polymorphic configurations.</param>
    /// <returns>The base type name, or null if not a variant.</returns>
    public static string? GetBaseTypeForVariant(
        string schemaName,
        Dictionary<string, PolymorphicConfig>? configs)
    {
        if (configs == null)
        {
            return null;
        }

        foreach (var config in configs.Values)
        {
            // Skip union types — variants don't inherit from the wrapper
            if (config.UsesCustomConverter)
            {
                continue;
            }

            foreach (var variant in config.Variants)
            {
                if (variant.SchemaRefId.Equals(schemaName, StringComparison.Ordinal))
                {
                    return config.BaseTypeName;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Generates code for a union type wrapper (sealed record with [JsonConverter] attribute and implicit operators).
    /// Used for oneOf/anyOf schemas without a discriminator property.
    /// </summary>
    /// <param name="config">The polymorphic configuration (must have UsesCustomConverter = true).</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="pathSegment">Optional path segment for sub-namespace.</param>
    /// <returns>The generated C# code for the union wrapper type.</returns>
    public static string GenerateUnionBaseType(
        PolymorphicConfig config,
        string projectName,
        string? pathSegment = null)
    {
        var sb = new StringBuilder();

        sb.Append(HeaderBuilder.WithUsings(
            NamespaceConstants.SystemCodeDomCompiler,
            NamespaceConstants.SystemTextJsonSerialization));

        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();

        var compositionType = config.IsOneOf ? "oneOf" : "anyOf";
        var variantList = string.Join(", ", config.Variants.Select(v => v.TypeName));
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Union type ({compositionType}) without discriminator — uses try-parse deserialization.");
        sb.AppendLine($"/// Variants: {variantList}");
        sb.AppendLine("/// </summary>");

        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"[JsonConverter(typeof({config.BaseTypeName}JsonConverter))]");
        sb.AppendLine($"public sealed record {config.BaseTypeName}(object Value)");
        sb.AppendLine("{");

        foreach (var variant in config.Variants)
        {
            sb.AppendLine($"    public static implicit operator {config.BaseTypeName}({variant.TypeName} value) => new(value);");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates code for a union type JSON converter (try-parse deserialization).
    /// Used for oneOf/anyOf schemas without a discriminator property.
    /// </summary>
    /// <param name="config">The polymorphic configuration (must have UsesCustomConverter = true).</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="pathSegment">Optional path segment for sub-namespace.</param>
    /// <returns>The generated C# code for the JSON converter class.</returns>
    public static string GenerateUnionConverter(
        PolymorphicConfig config,
        string projectName,
        string? pathSegment = null)
    {
        var sb = new StringBuilder();

        sb.Append(HeaderBuilder.WithUsings(
            NamespaceConstants.System,
            NamespaceConstants.SystemCodeDomCompiler,
            NamespaceConstants.SystemTextJson,
            NamespaceConstants.SystemTextJsonSerialization));

        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Try-parse JSON converter for the <see cref=\"{config.BaseTypeName}\"/> union type.");
        sb.AppendLine("/// Attempts deserialization of each variant in order until one succeeds.");
        sb.AppendLine("/// </summary>");

        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public sealed class {config.BaseTypeName}JsonConverter : JsonConverter<{config.BaseTypeName}>");
        sb.AppendLine("{");

        sb.AppendLine($"    public override {config.BaseTypeName}? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        using var document = JsonDocument.ParseValue(ref reader);");
        sb.AppendLine("        var rawText = document.RootElement.GetRawText();");
        sb.AppendLine();
        sb.AppendLine("        JsonException? lastException = null;");

        foreach (var variant in config.Variants)
        {
            sb.AppendLine();
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine($"            var result = JsonSerializer.Deserialize<{variant.TypeName}>(rawText, options);");
            sb.AppendLine("            if (result is not null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                return new {config.BaseTypeName}(result);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        catch (JsonException ex)");
            sb.AppendLine("        {");
            sb.AppendLine("            lastException = ex;");
            sb.AppendLine("        }");
        }

        sb.AppendLine();
        sb.AppendLine("        throw new JsonException(");
        sb.AppendLine($"            $\"Unable to deserialize {{nameof({config.BaseTypeName})}}: no matching variant found.\",");
        sb.AppendLine("            lastException);");
        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine($"    public override void Write(Utf8JsonWriter writer, {config.BaseTypeName} value, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        JsonSerializer.Serialize(writer, value.Value, value.Value.GetType(), options);");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Gets the discriminator value for a variant schema from the explicit mapping,
    /// or generates one from the schema name.
    /// </summary>
    private static string GetDiscriminatorValueForVariant(
        string variantSchemaName,
        IDictionary<string, string>? explicitMapping)
    {
        // Check explicit mapping (value is schema name, key is discriminator value)
        if (explicitMapping != null)
        {
            foreach (var kvp in explicitMapping)
            {
                if (kvp.Value.Equals(variantSchemaName, StringComparison.Ordinal))
                {
                    return kvp.Key;
                }
            }
        }

        // No explicit mapping: use the schema name verbatim. This matches the OpenAPI
        // implicit-mapping convention (the discriminator value is the schema name as-is)
        // and System.Text.Json's default, rather than guessing a snake_case transformation.
        return variantSchemaName;
    }
}