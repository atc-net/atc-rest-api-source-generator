namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// The specification files a generator reads: every <c>.yaml</c>, <c>.yml</c> and <c>.json</c> AdditionalFile that can be
/// a specification or an overlay, with the severities the project's <c>.editorconfig</c> gives the rules for that file
/// (the compiler applies <c>.editorconfig</c> to C# files only) - one provider for every generator.
/// </summary>
internal static class SpecificationFileProvider
{
    /// <summary>
    /// The specification and overlay files, as one equatable array.
    /// </summary>
    /// <param name="context">The generator's initialization context.</param>
    /// <returns>The provider.</returns>
    public static IncrementalValueProvider<EquatableArray<YamlFileInfo>> Create(
        IncrementalGeneratorInitializationContext context)
        => context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                 file.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                                 file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, cancellationToken) =>
                new YamlFileInfo(
                    pair.Left.Path,
                    pair.Left.GetText(cancellationToken)?.ToString() ?? string.Empty,
                    RuleSeverityOverrides.ReadSeverities(pair.Right.GetOptions(pair.Left).TryGetValue)))
            .Where(static file => OverlayFileHelper.IsCandidate(file))
            .Collect()
            .Select(static (array, _) => new EquatableArray<YamlFileInfo>(array));
}