namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Emits the XML mapping of the models an XML body can carry (<c>{Model}Xml</c> next to each record) and the shared
/// <c>XmlModelSupport</c> helpers. Shared by the server and client generators, so both sides read and write the same
/// XML.
/// </summary>
internal static class XmlModelEmitter
{
    /// <summary>
    /// Emits the <c>{Model}Xml</c> class of each model, into the models' namespace with the records' header.
    /// </summary>
    /// <param name="context">The source-production context to emit into.</param>
    /// <param name="sources">The records that get an XML mapping.</param>
    /// <param name="headerContent">The records' header (usings).</param>
    /// <param name="modelsNamespace">The models' namespace.</param>
    /// <param name="projectName">The root namespace.</param>
    /// <param name="xmlModels">The schemas that get an XML mapping.</param>
    /// <param name="extensibleEnums">Whether the string enums are extensible enums (a generated client).</param>
    public static void EmitCompanions(
        GeneratedSourceContext context,
        IReadOnlyList<XmlModelSource> sources,
        string? headerContent,
        string modelsNamespace,
        string projectName,
        ISet<string> xmlModels,
        bool extensibleEnums = false)
    {
        foreach (var source in sources)
        {
            var content = XmlModelMapperExtractor.GenerateCompanion(source, headerContent, modelsNamespace, projectName, xmlModels, extensibleEnums);
            context.AddGeneratedSource(
                NamespaceBuilder.ToFileName(modelsNamespace, source.TypeName + "Xml"),
                content);
        }
    }

    /// <summary>
    /// Emits the server's <c>application/problem+xml</c> writer when the specification has an XML body.
    /// </summary>
    /// <param name="context">The source-production context to emit into.</param>
    /// <param name="projectName">The root namespace.</param>
    /// <param name="xmlModels">The schemas that get an XML mapping.</param>
    public static void EmitProblemDetailsWriter(
        GeneratedSourceContext context,
        string projectName,
        ISet<string> xmlModels)
    {
        if (xmlModels.Count == 0)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.XmlProblemDetailsWriter.g.cs",
            XmlModelMapperExtractor.GenerateProblemDetailsWriter(projectName));
    }

    /// <summary>
    /// Emits the shared <c>XmlModelSupport</c> helpers when any model gets an XML mapping.
    /// </summary>
    /// <param name="context">The source-production context to emit into.</param>
    /// <param name="projectName">The root namespace.</param>
    /// <param name="xmlModels">The schemas that get an XML mapping.</param>
    public static void EmitSupport(
        GeneratedSourceContext context,
        string projectName,
        ISet<string> xmlModels)
    {
        if (xmlModels.Count == 0)
        {
            return;
        }

        context.AddGeneratedSource(
            $"{projectName}.{XmlModelMapperExtractor.SupportTypeName}.g.cs",
            XmlModelMapperExtractor.GenerateSupport(projectName));
    }
}