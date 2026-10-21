namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Emits the base type of an <c>oneOf</c> / <c>anyOf</c> schema, and the JSON converter it needs.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SchemaExtractor"/> deliberately skips polymorphic base schemas, and marks each variant
/// as deriving from the base. Nothing emitted the base itself, so the generated variants, handlers
/// and results referenced a type no file declared - a consumer-facing <c>CS0246</c>. The only
/// implementation lived in the test fixture, which is why the snapshots showed the base being
/// generated while a real build failed.
/// </para>
/// <para>
/// Shared by the server and client generators rather than duplicated in each, because a second copy
/// of generation logic is precisely what produced that divergence.
/// </para>
/// </remarks>
internal static class PolymorphicTypeEmitter
{
    /// <summary>
    /// Emits the polymorphic base types whose schema name appears in <paramref name="schemaNames"/>.
    /// </summary>
    /// <remarks>
    /// The caller passes the same schema set and <paramref name="pathSegment"/> it passes when
    /// emitting models, so a base lands in the same namespace as its variants. That matters beyond
    /// tidiness: the emitted base names its variants unqualified, in
    /// <c>[JsonDerivedType(typeof(Circle), …)]</c> and in the converter body, and its header imports
    /// nothing but <c>System.CodeDom.Compiler</c> and <c>System.Text.Json.Serialization</c>. Split
    /// the two across namespaces and the emitted file no longer compiles.
    /// </remarks>
    /// <param name="context">The source-production context to emit into.</param>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The root namespace.</param>
    /// <param name="schemaNames">The schema names owned by this namespace.</param>
    /// <param name="pathSegment">The path segment, or <see langword="null"/> for shared models.</param>
    /// <param name="boundRequestModels">The server's models bound by their own reader: a base among them (a JSON-only request
    /// body) is partial and gets the generated <c>BindAsync</c>. Null for a client.</param>
    public static void Emit(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment,
        Dictionary<string, ServerBoundModel>? boundRequestModels = null)
    {
        var polymorphicConfigs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(openApiDoc);

        if (polymorphicConfigs is null ||
            polymorphicConfigs.Count == 0)
        {
            return;
        }

        var modelsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);

        foreach (var polymorphicConfig in polymorphicConfigs)
        {
            if (!schemaNames.Contains(polymorphicConfig.Key))
            {
                continue;
            }

            var config = polymorphicConfig.Value;
            var additionalUsings = GetVariantNamespaces(openApiDoc, projectName, config, schemaNames);

            // A union has no discriminator to switch on, so the base carries a hand-written
            // converter instead of [JsonPolymorphic]/[JsonDerivedType].
            var baseContent = config.UsesCustomConverter
                ? PolymorphicTypeExtractor.GenerateUnionBaseType(config, projectName, pathSegment, additionalUsings)
                : PolymorphicTypeExtractor.GeneratePolymorphicBaseType(config, projectName, pathSegment, additionalUsings);

            // A base a JSON-only request body is: partial, with the generated reader that names a value it cannot read
            if (boundRequestModels?.TryGetValue(polymorphicConfig.Key, out var bound) == true)
            {
                baseContent = baseContent
                    .Replace($"public abstract record {config.BaseTypeName};", $"public abstract partial record {config.BaseTypeName};")
                    .Replace($"public sealed record {config.BaseTypeName}(", $"public sealed partial record {config.BaseTypeName}(");
                context.AddGeneratedSource(
                    NamespaceBuilder.ToFileName(modelsNamespace, config.BaseTypeName + "Binding"),
                    SourceText.From(XmlModelMapperExtractor.GenerateBinding(config.BaseTypeName, modelsNamespace, projectName, bound).NormalizeForSourceOutput(), Encoding.UTF8));
            }

            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(modelsNamespace, config.BaseTypeName),
                SourceText.From(baseContent.NormalizeForSourceOutput(), Encoding.UTF8));

            var converterContent = GetConverterContent(config, projectName, pathSegment, additionalUsings);
            if (converterContent is null)
            {
                continue;
            }

            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(modelsNamespace, $"{config.BaseTypeName}JsonConverter"),
                SourceText.From(converterContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Returns the converter source for a base type, or <see langword="null"/> when the built-in
    /// <c>System.Text.Json</c> polymorphism attributes are sufficient.
    /// </summary>
    private static string? GetConverterContent(
        PolymorphicConfig config,
        string projectName,
        string? pathSegment,
        IReadOnlyCollection<string>? additionalUsings)
    {
        if (config.UsesCustomConverter)
        {
            return PolymorphicTypeExtractor.GenerateUnionConverter(config, projectName, pathSegment, additionalUsings);
        }

        // A discriminated type needs a converter to honour a defaultMapping fallback, or when a variant declares
        // the discriminator property ([JsonPolymorphic] refuses that); otherwise the attributes do the dispatch.
        return config.UsesDiscriminatorConverter
            ? PolymorphicTypeExtractor.GenerateDiscriminatorFallbackConverter(config, projectName, pathSegment, additionalUsings)
            : null;
    }

    /// <summary>
    /// Returns the Models namespaces that have to be imported so the base can name its variants.
    /// </summary>
    /// <remarks>
    /// A variant that is part of <paramref name="schemaNames"/> is emitted into the very namespace
    /// the base is going into, so it needs no import. Anything else lives in the shared Models
    /// namespace or in another segment's, and a real specification does split them: in the Monta
    /// partner API the <c>TeamOrOperator</c> union is scoped to one segment while its variants are
    /// not, which is a compile error without this.
    /// </remarks>
    private static List<string> GetVariantNamespaces(
        OpenApiDocument openApiDoc,
        string projectName,
        PolymorphicConfig config,
        HashSet<string> schemaNames)
    {
        var namespaces = new List<string>();

        var outsideVariants = config.Variants
            .Select(v => v.SchemaRefId)
            .Where(refId => !schemaNames.Contains(refId))
            .ToList();

        if (outsideVariants.Count == 0)
        {
            return namespaces;
        }

        var sharedSchemas = PathSegmentHelper.GetSharedSchemas(openApiDoc);

        foreach (var refId in outsideVariants)
        {
            var variantNamespace = sharedSchemas.Contains(refId)
                ? NamespaceBuilder.ForModels(projectName)
                : FindSegmentNamespace(openApiDoc, projectName, sharedSchemas, refId);

            if (variantNamespace is not null &&
                !namespaces.Contains(variantNamespace, StringComparer.Ordinal))
            {
                namespaces.Add(variantNamespace);
            }
        }

        return namespaces;
    }

    /// <summary>
    /// Finds the Models namespace of the path segment that owns <paramref name="schemaName"/>.
    /// </summary>
    private static string? FindSegmentNamespace(
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> sharedSchemas,
        string schemaName)
    {
        foreach (var pathSegment in PathSegmentHelper.GetUniquePathSegments(openApiDoc))
        {
            if (!PathSegmentHelper.GetSegmentSpecificSchemas(openApiDoc, pathSegment, sharedSchemas).Contains(schemaName))
            {
                continue;
            }

            var effectiveSegment = PathSegmentHelper.ResolveEffectivePathSegment(openApiDoc, projectName, pathSegment) ?? string.Empty;

            return NamespaceBuilder.ForModels(projectName, effectiveSegment);
        }

        return null;
    }
}