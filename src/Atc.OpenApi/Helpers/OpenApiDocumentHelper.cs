namespace Atc.OpenApi.Helpers;

/// <summary>
/// Parses OpenAPI YAML into an <see cref="OpenApiDocument"/>, caching the result per path so the
/// source generator and the CLI do not re-parse an unchanged specification on every pass.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
[SuppressMessage("Design", "S1075:Refactor your code not to use hardcoded absolute paths or URIs", Justification = "OK.")]
public static class OpenApiDocumentHelper
{
    // A stand-in absolute URI for an xml.namespace the reader cannot take, followed by the value's index
    private const string PlaceholderNamespace = "urn:atc-invalid-xml-namespace:";

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

    // A "namespace" key and its one-token value, in YAML (block or flow) or JSON; a value that starts an object or an
    // array (a property named namespace) is no match
    private static readonly System.Text.RegularExpressions.Regex XmlNamespaceValue = new(
        @"(?<key>[""']?namespace[""']?[ \t]*:[ \t]*)(?<quote>[""']?)(?<value>[^""'\s,{}\[\]]+)\k<quote>",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

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
        if (yamlContent is null)
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
        if (yamlContent is null)
        {
            throw new ArgumentNullException(nameof(yamlContent));
        }

        if (yamlPath is null)
        {
            throw new ArgumentNullException(nameof(yamlPath));
        }

        try
        {
            var (document, _) = TryParseYamlWithDiagnostic(yamlContent, yamlPath);
            openApiDocument = document;
            return openApiDocument is not null;
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
        if (yamlContent is null)
        {
            throw new ArgumentNullException(nameof(yamlContent));
        }

        if (yamlPath is null)
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

        var settings = new OpenApiReaderSettings();
        using var memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yamlContent));

        // $self declares the document's canonical URI — use it as baseUri so that
        // relative $ref resolution works even when the file is not at its canonical location.
        var selfUri = ExtractSelfUri(yamlContent);
        var baseUri = selfUri ?? new Uri("file://" + yamlPath.Replace("\\", "/"));

        OpenApiDocument? document;
        OpenApiDiagnostic? diagnostic;
        try
        {
            // JSON text (a specification after its overlays, or written as JSON) is read as JSON: SharpYaml, under
            // the YAML reader, throws on an escaped surrogate pair - an emoji as JSON writes it
            var readResult = IsJsonText(yamlContent)
                ? new OpenApiJsonReader().Read(memoryStream, baseUri, settings)
                : new OpenApiYamlReader().Read(memoryStream, baseUri, settings);
            document = readResult.Document;
            diagnostic = readResult.Diagnostic;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            // The parser throws on some documents (an unsupported openapi version, an empty $ref): an error to
            // report, as for any document it cannot read. An xml.namespace that is no absolute URI is named with its
            // schema - the reader's own message ("Invalid URI") says neither.
            var reason = ex is UriFormatException && DescribeInvalidXmlNamespaces(yamlContent, baseUri) is { } namespaces
                ? namespaces
                : ex.Message;
            document = null;
            diagnostic = new OpenApiDiagnostic();
            diagnostic.Errors.Add(new OpenApiError(string.Empty, "The specification cannot be read: " + reason));
        }

        StashSpecVersion(document, diagnostic);

        ParseCache[yamlPath] = (contentHash, contentLength, document, diagnostic);

        return (document, diagnostic);
    }

    /// <summary>
    /// Names the schemas whose <c>xml.namespace</c> is no absolute URI: each such value is replaced by a placeholder URI
    /// in a copy of the text, the copy is read, and the schemas that hold a placeholder are reported with their value.
    /// Null when that finds none.
    /// </summary>
    private static string? DescribeInvalidXmlNamespaces(
        string content,
        Uri baseUri)
    {
        var originals = new List<string>();
        var copy = XmlNamespaceValue.Replace(content, match =>
        {
            var value = match.Groups["value"].Value;
            if (Uri.TryCreate(value, UriKind.Absolute, out _))
            {
                return match.Value;
            }

            originals.Add(value);
            return match.Groups["key"].Value + match.Groups["quote"].Value + PlaceholderNamespace + (originals.Count - 1).ToString(CultureInfo.InvariantCulture) + match.Groups["quote"].Value;
        });
        if (originals.Count == 0)
        {
            return null;
        }

        OpenApiDocument? document;
        try
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(copy));
            document = (IsJsonText(copy)
                ? new OpenApiJsonReader().Read(stream, baseUri, new OpenApiReaderSettings())
                : new OpenApiYamlReader().Read(stream, baseUri, new OpenApiReaderSettings())).Document;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            return null;
        }

        var found = new List<string>();
        foreach (var pair in document?.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            CollectPlaceholders(pair.Key, pair.Value, depth: 0);
        }

        return found.Count == 0
            ? null
            : string.Join("; ", found) + " - an xml.namespace must be an absolute URI (http://..., urn:...).";

        void CollectPlaceholders(
            string path,
            IOpenApiSchema? schema,
            int depth)
        {
            if (schema is null or OpenApiSchemaReference || depth > 32)
            {
                return;
            }

            if (schema.Xml?.Namespace?.OriginalString is { } ns &&
                ns.StartsWith(PlaceholderNamespace, StringComparison.Ordinal) &&
                int.TryParse(ns.Substring(PlaceholderNamespace.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                index < originals.Count)
            {
                found.Add($"schema '{path}' has xml.namespace '{originals[index]}'");
            }

            foreach (var pair in schema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
            {
                CollectPlaceholders($"{path}.{pair.Key}", pair.Value, depth + 1);
            }

            CollectPlaceholders($"{path}[]", schema.Items, depth + 1);
        }
    }

    private static bool IsJsonText(string content)
    {
        foreach (var c in content)
        {
            if (!char.IsWhiteSpace(c) && c != (char)0xFEFF)
            {
                return c == '{';
            }
        }

        return false;
    }

    /// <summary>
    /// Stashes the parsed OpenAPI specification version onto the document's
    /// (non-serialized) metadata bag, so that downstream code holding only the
    /// document can recover it via <c>OpenApiDocument.SpecVersion</c>.
    /// The spec version is only exposed on the parser diagnostic, not the document.
    /// </summary>
    private static void StashSpecVersion(
        OpenApiDocument? document,
        OpenApiDiagnostic? diagnostic)
    {
        if (document is null || diagnostic is null)
        {
            return;
        }

        document.Metadata ??= new Dictionary<string, object>(StringComparer.Ordinal);
        document.Metadata[OpenApiDocumentExtensions.SpecVersionMetadataKey] = diagnostic.SpecificationVersion;
    }

    /// <summary>
    /// Scans the raw YAML for a top-level <c>$self:</c> field and returns its value as a URI.
    /// Returns null when the field is absent or its value is not a valid absolute URI.
    /// </summary>
    private static Uri? ExtractSelfUri(string yamlContent)
    {
        const string prefix = "$self:";

        foreach (var rawLine in yamlContent.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            // Only match top-level keys (must start at column 0, no indentation)
            if (line.Length == 0 || line[0] == ' ' || line[0] == '\t' || line[0] == '#')
            {
                continue;
            }

            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var value = line.Substring(prefix.Length).Trim();

            // Strip optional surrounding quotes (single or double)
            if (value.Length >= 2)
            {
                var last = value[value.Length - 1];
                if ((value[0] == '"' && last == '"') ||
                    (value[0] == '\'' && last == '\''))
                {
                    value = value.Substring(1, value.Length - 2);
                }
            }

            return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
        }

        return null;
    }
}