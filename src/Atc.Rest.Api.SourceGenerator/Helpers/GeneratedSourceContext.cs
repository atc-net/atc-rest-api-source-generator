namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Thin proxy over <see cref="SourceProductionContext"/> that applies
/// <see cref="GeneratedCodeAttributeHelper.ApplyExcludeFromCodeCoverage"/> to every emitted source
/// file, and <see cref="GeneratedCodeAttributeHelper.SuppressObsolete"/> for a specification with deprecated
/// members. Generator methods that only ever call <c>AddGeneratedSource</c> can take this type instead of
/// <see cref="SourceProductionContext"/> without any other change to their body.
/// </summary>
internal readonly struct GeneratedSourceContext(
    SourceProductionContext context,
    bool excludeFromCodeCoverage,
    bool suppressObsolete)
{
    public void AddGeneratedSource(
        string hintName,
        SourceText sourceText)
    {
        var content = GeneratedCodeAttributeHelper.ApplyExcludeFromCodeCoverage(sourceText.ToString(), excludeFromCodeCoverage);
        if (suppressObsolete)
        {
            content = GeneratedCodeAttributeHelper.SuppressObsolete(content);
        }

        context.AddGeneratedSource(hintName, SourceText.From(content, sourceText.Encoding ?? Encoding.UTF8));
    }
}