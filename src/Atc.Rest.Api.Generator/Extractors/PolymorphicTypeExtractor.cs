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
    /// Generates code for a polymorphic base type (abstract record with JsonPolymorphic attributes).
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

        // Header
        sb.Append(HeaderBuilder.WithUsings(
            NamespaceConstants.SystemCodeDomCompiler,
            NamespaceConstants.SystemTextJsonSerialization));

        // Namespace
        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();

        // XML documentation
        var compositionType = config.IsOneOf ? "oneOf" : "anyOf";
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Polymorphic base type ({compositionType}) with discriminator property '{config.DiscriminatorPropertyName}'.");
        if (!config.IsDiscriminatorExplicit)
        {
            sb.AppendLine("/// Note: Discriminator was auto-detected from common properties.");
        }

        sb.AppendLine("/// </summary>");

        // GeneratedCode attribute
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");

        // JsonPolymorphic attribute
        sb.AppendLine($"[JsonPolymorphic(TypeDiscriminatorPropertyName = \"{config.DiscriminatorPropertyName}\")]");

        // JsonDerivedType attributes for each variant
        foreach (var variant in config.Variants)
        {
            sb.AppendLine($"[JsonDerivedType(typeof({variant.TypeName}), \"{variant.DiscriminatorValue}\")]");
        }

        // Abstract record declaration
        sb.AppendLine($"public abstract record {config.BaseTypeName};");

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

        // If we still don't have a discriminator, we can't generate proper polymorphic code
        if (string.IsNullOrEmpty(discriminatorPropertyName))
        {
            return null;
        }

        var config = new PolymorphicConfig
        {
            BaseTypeName = schemaName,
            DiscriminatorPropertyName = discriminatorPropertyName!,
            IsOneOf = isOneOf,
            IsDiscriminatorExplicit = isExplicit,
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

        // Auto-generate discriminator value from schema name (convert to snake_case)
        return ToSnakeCase(variantSchemaName);
    }

    /// <summary>
    /// Converts a PascalCase string to snake_case.
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}