namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Emits the enums and tuple records of a set of schemas into a models namespace (shared, or a path segment's) - one
/// emitter for the server and the client generators, which differ only in how an enum is written on the wire.
/// </summary>
internal static class SchemaTypeEmitter
{
    /// <summary>
    /// Emits an enum file per enum schema of <paramref name="schemaNames"/>.
    /// </summary>
    /// <param name="context">The generated sources.</param>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The root namespace.</param>
    /// <param name="schemaNames">The schemas to emit.</param>
    /// <param name="pathSegment">The path segment, or <see langword="null"/> for the shared models.</param>
    /// <param name="extensibleEnums">Whether the enums are extensible.</param>
    /// <param name="stringOnTheWire">Whether a string enum carries its string converter even when no value needs one (the server).</param>
    public static void EmitEnums(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment,
        bool extensibleEnums,
        bool stringOnTheWire)
    {
        var enumParametersList = EnumExtractor.ExtractForSchemas(openApiDoc, projectName, schemaNames, pathSegment);
        if (enumParametersList is null || enumParametersList.Count == 0)
        {
            return;
        }

        var enumsNamespace = NamespaceBuilder.ForModels(projectName, pathSegment);
        foreach (var enumParams in enumParametersList)
        {
            var generatedContent = EnumExtractor.GenerateEnumContent(enumParams, extensibleEnums, stringOnTheWire: stringOnTheWire, enumWireConverter: context.EnumWireConverter, readMemberNames: context.ReadEnumMemberNames);

            // The file name mirrors the namespace the enum is emitted into
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(enumsNamespace, enumParams.EnumTypeName),
                SourceText.From(generatedContent.NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Emits a tuple record file per tuple schema (<c>prefixItems</c>) of <paramref name="schemaNames"/>.
    /// </summary>
    /// <param name="context">The generated sources.</param>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The root namespace.</param>
    /// <param name="schemaNames">The schemas to emit.</param>
    /// <param name="pathSegment">The path segment, or <see langword="null"/> for the shared models.</param>
    public static void EmitTuples(
        GeneratedSourceContext context,
        OpenApiDocument openApiDoc,
        string projectName,
        HashSet<string> schemaNames,
        string? pathSegment)
    {
        var tupleParametersList = TupleExtractor.ExtractForSchemas(openApiDoc, projectName, schemaNames, pathSegment);
        if (tupleParametersList is null || tupleParametersList.Count == 0)
        {
            return;
        }

        var ns = NamespaceBuilder.ForModels(projectName, pathSegment);
        foreach (var tupleParams in tupleParametersList)
        {
            // The file name mirrors the namespace the tuple is emitted into
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(ns, tupleParams.Name),
                SourceText.From(TupleExtractor.GenerateTupleContent(tupleParams, ns).NormalizeForSourceOutput(), Encoding.UTF8));
        }
    }
}