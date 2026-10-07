namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// The one way a generator adds a file to the compilation, so every generated file gets the same finishing:
/// <see cref="GeneratedCodeAttributeHelper.SuppressMissingXmlComment"/> and <see cref="ImplicitUsingsHelper.AddImplicitUsings"/>.
/// </summary>
internal static class SourceProductionContextExtensions
{
    /// <param name="context">The source-production context of the generator step.</param>
    extension(SourceProductionContext context)
    {
        /// <summary>
        /// Adds a generated file to the compilation, with the missing-XML-comment warning suppressed in it and the implicit usings of its own.
        /// </summary>
        /// <param name="hintName">The file's hint name.</param>
        /// <param name="sourceText">The file's source.</param>
        public void AddGeneratedSource(
            string hintName,
            SourceText sourceText)
            => context.AddSource(
                hintName,
                SourceText.From(
                    Finish(sourceText),
                    sourceText.Encoding ?? Encoding.UTF8));
    }

    /// <param name="context">The post-initialization context of the generator.</param>
    extension(IncrementalGeneratorPostInitializationContext context)
    {
        /// <summary>
        /// Adds a generated file to the compilation, with the missing-XML-comment warning suppressed in it and the implicit usings of its own.
        /// </summary>
        /// <param name="hintName">The file's hint name.</param>
        /// <param name="sourceText">The file's source.</param>
        public void AddGeneratedSource(
            string hintName,
            SourceText sourceText)
            => context.AddSource(
                hintName,
                SourceText.From(
                    Finish(sourceText),
                    sourceText.Encoding ?? Encoding.UTF8));
    }

    private static string Finish(SourceText sourceText)
        => ImplicitUsingsHelper.AddImplicitUsings(
            GeneratedCodeAttributeHelper.SuppressMissingXmlComment(sourceText.ToString()));
}