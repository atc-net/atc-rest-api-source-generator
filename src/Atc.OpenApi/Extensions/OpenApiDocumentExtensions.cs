namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for OpenApiDocument to handle parsing, queries, and project name extraction.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3928:The parameter name 'schema'", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3398:Move this method inside", Justification = "OK - CLang14 - extension")]
public static class OpenApiDocumentExtensions
{
    /// <summary>
    /// Key used to stash the detected <see cref="OpenApiSpecVersion"/> in
    /// <see cref="OpenApiDocument.Metadata"/> at parse time. The metadata bag is
    /// not part of the OpenAPI specification and is never serialized, so it is a
    /// safe side-channel for carrying the spec version to code that only holds the
    /// document (validators, extractors) — the version itself lives on the parser's
    /// diagnostic, not on the document.
    /// </summary>
    public const string SpecVersionMetadataKey = "x-atc-openapi-spec-version";

    /// <param name="document">The OpenAPI document.</param>
    extension(OpenApiDocument document)
    {
        /// <summary>
        /// Gets the OpenAPI specification version (3.0 / 3.1 / 3.2) that the document
        /// was parsed as, when available.
        /// </summary>
        /// <returns>
        /// The detected <see cref="OpenApiSpecVersion"/>, or <c>null</c> when the
        /// document was not parsed through <c>OpenApiDocumentHelper</c> (e.g. constructed
        /// in-memory) and therefore carries no version metadata.
        /// </returns>
        public OpenApiSpecVersion? SpecVersion
            => document?.Metadata is not null &&
               document.Metadata.TryGetValue(SpecVersionMetadataKey, out var value) &&
               value is OpenApiSpecVersion specVersion
                ? specVersion
                : null;

        /// <summary>
        /// Gets all operations from all paths in the document.
        /// </summary>
        /// <returns>Enumerable of tuples containing path, HTTP method, and operation.</returns>
        public IEnumerable<(string Path, string Method, OpenApiOperation Operation)> GetAllOperations()
            => document is null
                ? throw new ArgumentNullException(nameof(document))
                : GetAllOperationsIterator(document);

        /// <summary>
        /// Gets the operations of every path and every webhook - each operation a request can reach the generated code
        /// through.
        /// </summary>
        /// <returns>The operations, paths first.</returns>
        public IEnumerable<OpenApiOperation> GetAllOperationsAndWebhooks()
            => (document?.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
                .Concat(document?.Webhooks?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
                .SelectMany(p => p?.Operations?.Values.Where(o => o is not null) ?? Enumerable.Empty<OpenApiOperation>());

        // ========== Webhooks Support (OpenAPI 3.1) ==========

        /// <summary>
        /// Checks if the document has any webhooks defined (OpenAPI 3.1 feature).
        /// </summary>
        /// <returns>True if the document has webhooks.</returns>
        public bool HasWebhooks
            => document?.Webhooks?.Count > 0;

        /// <summary>
        /// Gets all webhook operations from the document (OpenAPI 3.1 feature).
        /// </summary>
        /// <returns>Enumerable of tuples containing webhook name, HTTP method, and operation.</returns>
        public IEnumerable<(string WebhookName, string Method, OpenApiOperation Operation)> GetAllWebhookOperations()
            => document is null
                ? throw new ArgumentNullException(nameof(document))
                : GetAllWebhookOperationsIterator(document);

        /// <summary>
        /// Gets the count of webhooks in the document.
        /// </summary>
        /// <returns>The number of webhooks.</returns>
        public int WebhooksCount
            => document?.Webhooks?.Count ?? 0;
    }

    private static IEnumerable<(string Path, string Method, OpenApiOperation Operation)> GetAllOperationsIterator(
        OpenApiDocument document)
    {
        if (document.Paths is null)
        {
            yield break;
        }

        foreach (var path in document.Paths)
        {
            if (path.Value?.Operations is null)
            {
                continue;
            }

            foreach (var operation in path.Value.Operations)
            {
                var method = operation.Key.ToString();
                method = method.ToUpperInvariant();

                yield return (path.Key, method, operation.Value);
            }
        }
    }

    private static IEnumerable<(string WebhookName, string Method, OpenApiOperation Operation)> GetAllWebhookOperationsIterator(
        OpenApiDocument document)
    {
        if (document.Webhooks is null)
        {
            yield break;
        }

        foreach (var webhook in document.Webhooks)
        {
            if (webhook.Value?.Operations is null)
            {
                continue;
            }

            foreach (var operation in webhook.Value.Operations)
            {
                var method = operation.Key.ToString();
                method = method.ToUpperInvariant();

                yield return (webhook.Key, method, operation.Value);
            }
        }
    }
}