// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helper methods for extracting information from OpenAPI server URLs.
/// </summary>
public static class ServerUrlHelper
{
    /// <summary>
    /// Extracts the base path from the first server URL in an OpenAPI document.
    /// Handles both relative paths (/api/v1) and absolute URLs (http://example.com/api/v1).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The base path (e.g., "/api/v1"), or null if no servers or no path.</returns>
    public static string? GetServersBasePath(OpenApiDocument openApiDoc)
    {
        if (openApiDoc.Servers is null || openApiDoc.Servers.Count == 0)
        {
            return null;
        }

        var serverUrl = openApiDoc.Servers[0].Url;
        if (serverUrl is null)
        {
            return null;
        }

        return ExtractPathFromServerUrl(serverUrl);
    }

    /// <summary>
    /// Extracts the path portion from a server URL string.
    /// </summary>
    /// <param name="serverUrl">The server URL string (relative or absolute).</param>
    /// <returns>The path portion, or null if empty/root.</returns>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "OpenAPI server URLs can contain variables like {protocol} which are not valid URIs.")]
    [SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "We are returning a path portion, not a full URI.")]
    public static string? ExtractPathFromServerUrl(string serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            return null;
        }

        // Handle relative URLs (e.g., /api/v1)
        if (serverUrl.StartsWith("/", StringComparison.Ordinal))
        {
            var path = serverUrl.TrimEnd('/');
            return string.IsNullOrEmpty(path) || path == "/" ? null : path;
        }

        // Handle absolute URLs (e.g., http://example.com/api/v1)
        if (Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.TrimEnd('/');
            return string.IsNullOrEmpty(path) || path == "/" ? null : path;
        }

        // Handle URLs with variable placeholders (e.g., {protocol}://api.example.com/v1)
        // Try to extract path after the host portion
        var protocolIndex = serverUrl.IndexOf("://", StringComparison.Ordinal);
        if (protocolIndex >= 0)
        {
            var afterProtocol = serverUrl.Substring(protocolIndex + 3);
            var pathStart = afterProtocol.IndexOf('/');
            if (pathStart >= 0)
            {
                var path = afterProtocol
                    .Substring(pathStart)
                    .TrimEnd('/');
                return string.IsNullOrEmpty(path) || path == "/" ? null : path;
            }
        }

        return null;
    }
}