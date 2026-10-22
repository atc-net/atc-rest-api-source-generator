namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The constants of the <c>x-cache-tags</c> tags (<c>OutputCacheTags</c>, <c>CacheTags</c>): what a host evicts by
/// (<c>IOutputCacheStore.EvictByTagAsync</c>, <c>HybridCache.RemoveByTagAsync</c>), written once beside the policies.
/// </summary>
public static class CacheTagConstants
{
    /// <summary>
    /// Every tag of the policies, once each in ordinal order, with its constant's name. Two tags that give one
    /// identifier (<c>usb-devices</c>, <c>usb_devices</c>) are told apart by a number (<c>UsbDevices2</c>).
    /// </summary>
    /// <param name="policies">The cache policies.</param>
    /// <returns>The constant name per tag.</returns>
    public static IReadOnlyDictionary<string, string> Assign(
        IEnumerable<CacheConfiguration> policies)
    {
        if (policies is null)
        {
            throw new ArgumentNullException(nameof(policies));
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in policies
                     .SelectMany(p => p.Tags)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(t => t, StringComparer.Ordinal))
        {
            names[tag] = taken.Claim(PolicyNamingHelper.ToConstantName(tag));
        }

        return names;
    }

    /// <summary>
    /// Appends the class of tag constants to a policies file, or nothing when no policy has a tag.
    /// </summary>
    /// <param name="fileContent">The policies file.</param>
    /// <param name="className">The class name, e.g. <c>OutputCacheTags</c>.</param>
    /// <param name="classSummary">The class's summary line.</param>
    /// <param name="tags">The constant name per tag (see <see cref="Assign"/>).</param>
    /// <returns>The file with the class appended.</returns>
    public static string AppendClass(
        string fileContent,
        string className,
        string classSummary,
        IReadOnlyDictionary<string, string> tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return fileContent;
        }

        var builder = new StringBuilder(fileContent);
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// {classSummary}");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine($"public static class {className}");
        builder.AppendLine("{");

        var first = true;
        foreach (var tag in tags.OrderBy(t => t.Key, StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.AppendLine();
            }

            first = false;
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, $"/// The cache tag \"{tag.Key}\".");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, $"public const string {tag.Value} = \"{tag.Key}\";");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }
}