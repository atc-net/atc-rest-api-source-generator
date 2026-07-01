namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Thin proxy over <see cref="SourceProductionContext"/> that applies
/// <see cref="GeneratedCodeAttributeHelper.ApplyExcludeFromCodeCoverage"/> to every emitted source
/// file. Generator methods that only ever call <c>AddSource</c> can take this type instead of
/// <see cref="SourceProductionContext"/> without any other change to their body.
/// </summary>
internal readonly struct GeneratedSourceContext(
    SourceProductionContext context,
    bool excludeFromCodeCoverage)
{
    public void AddSource(
        string hintName,
        SourceText sourceText)
        => context.AddSource(
            hintName,
            SourceText.From(
                GeneratedCodeAttributeHelper.ApplyExcludeFromCodeCoverage(sourceText.ToString(), excludeFromCodeCoverage),
                sourceText.Encoding ?? Encoding.UTF8));
}