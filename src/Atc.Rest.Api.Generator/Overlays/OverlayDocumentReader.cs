namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// Reads a YAML or JSON document - an OpenAPI description or an Overlay - into a <see cref="JsonNode"/>, with the
/// same YAML conversion the OpenAPI reader uses, so a document read here is read exactly as the generator reads it.
/// </summary>
public static class OverlayDocumentReader
{
    /// <summary>
    /// Reads a document.
    /// </summary>
    /// <param name="content">The YAML or JSON text.</param>
    /// <returns>The document; <see langword="null"/> for an empty one.</returns>
    /// <exception cref="FormatException">The text is no valid YAML or JSON.</exception>
    public static JsonNode? Read(string content)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        try
        {
            var stream = new SharpYaml.Serialization.YamlStream();
            using (var reader = new StringReader(content))
            {
                stream.Load(reader);
            }

            return stream.Documents.Count == 0
                ? null
                : Microsoft.OpenApi.YamlReader.YamlConverter.ToJsonNode(stream.Documents[0]);
        }
        catch (SharpYaml.YamlException ex)
        {
            throw new FormatException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Whether a document is an Overlay (it has an <c>overlay</c> version and no <c>openapi</c>), not an OpenAPI
    /// description - an overlay file next to a specification must not be taken for one of its parts.
    /// </summary>
    /// <param name="content">The YAML or JSON text.</param>
    /// <returns><see langword="true"/> for an Overlay.</returns>
    public static bool IsOverlay(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        // A cheap look at the top-level keys first: most files are specifications
        var hasOverlayKey = Regex.IsMatch(content, @"^\s*[""']?overlay[""']?\s*:", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!hasOverlayKey)
        {
            return false;
        }

        try
        {
            return Read(content!) is JsonObject root &&
                   root.ContainsKey("overlay") &&
                   !root.ContainsKey("openapi") &&
                   !root.ContainsKey("swagger");
        }
        catch (FormatException)
        {
            return false;
        }
    }
}