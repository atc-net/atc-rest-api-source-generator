namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// The one way a generator adds a file to the compilation, so every generated file gets the same finishing:
/// <see cref="GeneratedCodeAttributeHelper.SuppressMissingXmlComment"/>.
/// </summary>
internal static class SourceProductionContextExtensions
{
    public static void AddGeneratedSource(
        this SourceProductionContext context,
        string hintName,
        SourceText sourceText)
        => context.AddSource(
            hintName,
            SourceText.From(
                GeneratedCodeAttributeHelper.SuppressMissingXmlComment(sourceText.ToString()),
                sourceText.Encoding ?? Encoding.UTF8));

    public static void AddGeneratedSource(
        this SourceProductionContext context,
        string hintName,
        string source)
        => context.AddSource(
            hintName,
            SourceText.From(GeneratedCodeAttributeHelper.SuppressMissingXmlComment(source), Encoding.UTF8));

    public static void AddGeneratedSource(
        this IncrementalGeneratorPostInitializationContext context,
        string hintName,
        SourceText sourceText)
        => context.AddSource(
            hintName,
            SourceText.From(
                GeneratedCodeAttributeHelper.SuppressMissingXmlComment(sourceText.ToString()),
                sourceText.Encoding ?? Encoding.UTF8));
}