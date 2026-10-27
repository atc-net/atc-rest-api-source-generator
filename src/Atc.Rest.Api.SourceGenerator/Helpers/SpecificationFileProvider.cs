namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// The specification files a generator reads: every <c>.yaml</c>, <c>.yml</c> and <c>.json</c> AdditionalFile that can be
/// a specification or an overlay - one provider for every generator.
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
            .Select(static (file, cancellationToken) =>
                new YamlFileInfo(
                    file.Path,
                    file.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .Where(static file => OverlayFileHelper.IsCandidate(file))
            .Collect()
            .Select(static (array, _) => new EquatableArray<YamlFileInfo>(array));
}