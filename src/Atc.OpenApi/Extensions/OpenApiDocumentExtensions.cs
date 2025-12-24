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
    /// <param name="document">The OpenAPI document.</param>
    extension(OpenApiDocument document)
    {
        /// <summary>
        /// Gets all operations from all paths in the document.
        /// </summary>
        /// <returns>Enumerable of tuples containing path, HTTP method, and operation.</returns>
        public IEnumerable<(string Path, string Method, OpenApiOperation Operation)> GetAllOperations()
            => document is null
                ? throw new ArgumentNullException(nameof(document))
                : GetAllOperationsIterator(document);

        // ========== Webhooks Support (OpenAPI 3.1) ==========

        /// <summary>
        /// Checks if the document has any webhooks defined (OpenAPI 3.1 feature).
        /// </summary>
        /// <returns>True if the document has webhooks.</returns>
        public bool HasWebhooks()
            => document?.Webhooks?.Count > 0;

        /// <summary>
        /// Gets all webhooks from the document (OpenAPI 3.1 feature).
        /// Webhooks are callback-style operations where the API sends data to a consumer's endpoint.
        /// </summary>
        /// <returns>Enumerable of tuples containing webhook name and path item.</returns>
        public IEnumerable<(string Name, IOpenApiPathItem PathItem)> GetAllWebhooks()
            => document is null
                ? throw new ArgumentNullException(nameof(document))
                : GetAllWebhooksIterator(document);

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
#pragma warning disable CA1024 // Use properties where appropriate - extension method cannot be a property
        public int GetWebhooksCount()
#pragma warning restore CA1024
            => document?.Webhooks?.Count ?? 0;
    }

    private static IEnumerable<(string Path, string Method, OpenApiOperation Operation)> GetAllOperationsIterator(
        OpenApiDocument document)
    {
        if (document.Paths == null)
        {
            yield break;
        }

        foreach (var path in document.Paths)
        {
            if (path.Value?.Operations == null)
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

    private static IEnumerable<(string Name, IOpenApiPathItem PathItem)> GetAllWebhooksIterator(
        OpenApiDocument document)
    {
        if (document.Webhooks == null)
        {
            yield break;
        }

        foreach (var webhook in document.Webhooks)
        {
            if (webhook.Value != null)
            {
                yield return (webhook.Key, webhook.Value);
            }
        }
    }

    private static IEnumerable<(string WebhookName, string Method, OpenApiOperation Operation)> GetAllWebhookOperationsIterator(
        OpenApiDocument document)
    {
        if (document.Webhooks == null)
        {
            yield break;
        }

        foreach (var webhook in document.Webhooks)
        {
            if (webhook.Value?.Operations == null)
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