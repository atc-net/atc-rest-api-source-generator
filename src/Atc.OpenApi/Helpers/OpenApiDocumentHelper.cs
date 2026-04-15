namespace Atc.OpenApi.Helpers;

[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
[SuppressMessage("Design", "S1075:Refactor your code not to use hardcoded absolute paths or URIs", Justification = "OK.")]
public static class OpenApiDocumentHelper
{
    /// <summary>
    /// In-process cache for parsed OpenAPI documents, keyed by yamlPath.
    /// Each entry stores a content hash to detect when the file has changed,
    /// so stale entries are automatically replaced (one slot per path).
    /// This prevents unbounded memory growth in long-running processes
    /// (e.g., IDE source generators) where each edit would otherwise add a new entry.
    /// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// for thread safety.
    /// </summary>
    private static readonly ConcurrentDictionary<string, (int ContentHash, int ContentLength, OpenApiDocument? Document, OpenApiDiagnostic? Diagnostic)> ParseCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Clears the in-process parse cache.
    /// Call this in long-running processes (e.g., CLI watch mode) to release
    /// memory held by previously parsed OpenAPI documents.
    /// </summary>
    public static void ClearCache()
        => ParseCache.Clear();

    /// <summary>
    /// Parses a YAML string into an OpenAPI document.
    /// </summary>
    /// <param name="yamlContent">The YAML content as a string.</param>
    /// <returns>The parsed OpenApiDocument.</returns>
    /// <exception cref="ArgumentNullException">Thrown when yamlContent is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when parsing fails.</exception>
    public static OpenApiDocument ParseYaml(
        string yamlContent)
    {
        if (yamlContent == null)
        {
            throw new ArgumentNullException(nameof(yamlContent));
        }

        var (document, _) = TryParseYamlWithDiagnostic(yamlContent, "test.yaml");
        return document ?? throw new InvalidOperationException("Failed to parse YAML");
    }

    /// <summary>
    /// Tries to parse a YAML string into an OpenAPI document.
    /// </summary>
    /// <param name="yamlContent">The YAML content as a string.</param>
    /// <param name="yamlPath">The path to the YAML file (used for error reporting).</param>
    /// <param name="openApiDocument">The parsed OpenApiDocument, or null if parsing failed.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when yamlContent or yamlPath is null.</exception>
    public static bool TryParseYaml(
        string yamlContent,
        string yamlPath,
        out OpenApiDocument? openApiDocument)
    {
        if (yamlContent == null)
        {
            throw new ArgumentNullException(nameof(yamlContent));
        }

        if (yamlPath == null)
        {
            throw new ArgumentNullException(nameof(yamlPath));
        }

        try
        {
            var (document, _) = TryParseYamlWithDiagnostic(yamlContent, yamlPath);
            openApiDocument = document;
            return openApiDocument != null;
        }
        catch (Exception)
        {
            openApiDocument = null;
            return false;
        }
    }

    /// <summary>
    /// Parses a YAML string into an OpenAPI document and returns diagnostic information.
    /// </summary>
    /// <param name="yamlContent">The YAML content as a string.</param>
    /// <param name="yamlPath">The path to the YAML file (used for base URI and error reporting).</param>
    /// <returns>A tuple containing the parsed document (or null if parsing failed) and the diagnostic information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when yamlContent or yamlPath is null.</exception>
    public static (OpenApiDocument? Document, OpenApiDiagnostic? Diagnostic) TryParseYamlWithDiagnostic(
        string yamlContent,
        string yamlPath)
    {
        if (yamlContent == null)
        {
            throw new ArgumentNullException(nameof(yamlContent));
        }

        if (yamlPath == null)
        {
            throw new ArgumentNullException(nameof(yamlPath));
        }

        // Return cached result if the same path+content was already parsed.
        // Uses content hash + length (one cache slot per path) to prevent unbounded growth.
        // Combining hash with length virtually eliminates collision risk vs hash alone.
        var contentHash = StringComparer.Ordinal.GetHashCode(yamlContent);
        var contentLength = yamlContent.Length;
        if (ParseCache.TryGetValue(yamlPath, out var cached) &&
            cached.ContentHash == contentHash &&
            cached.ContentLength == contentLength)
        {
            return (cached.Document, cached.Diagnostic);
        }

        var reader = new OpenApiYamlReader();
        var settings = new OpenApiReaderSettings();
        using var memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yamlContent));
        var baseUri = new Uri("file://" + yamlPath.Replace("\\", "/"));
        var readResult = reader.Read(memoryStream, baseUri, settings);

        ParseCache[yamlPath] = (contentHash, contentLength, readResult.Document, readResult.Diagnostic);

        return (readResult.Document, readResult.Diagnostic);
    }
}