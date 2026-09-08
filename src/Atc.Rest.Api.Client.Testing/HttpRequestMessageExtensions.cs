namespace Atc.Rest.Api.Client.Testing;

/// <summary>
/// Extension methods that make <see cref="HttpRequestMessage"/> instances recorded by
/// <see cref="FakeHttpMessageHandler"/> easy to assert against.
/// </summary>
/// <remarks>
/// These helpers deliberately return values rather than asserting, so they work with any
/// assertion library - AwesomeAssertions, FluentAssertions, xUnit's <c>Assert</c> or plain
/// comparisons.
/// </remarks>
public static class HttpRequestMessageExtensions
{
    /// <summary>
    /// Gets the absolute path of the request URI, without the query string.
    /// </summary>
    /// <param name="request">The recorded request.</param>
    /// <returns>The absolute path, or an empty string when there is no URI.</returns>
    public static string GetPath(this HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.RequestUri?.AbsolutePath ?? string.Empty;
    }

    /// <summary>
    /// Gets the query string of the request URI, including the leading <c>?</c>.
    /// </summary>
    /// <param name="request">The recorded request.</param>
    /// <returns>The query string, or an empty string when there is none.</returns>
    public static string GetQuery(this HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.RequestUri?.Query ?? string.Empty;
    }

    /// <summary>
    /// Reads a single query-string parameter.
    /// </summary>
    /// <param name="request">The recorded request.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns>The decoded value, or <see langword="null"/> when the parameter is absent.</returns>
    public static string? GetQueryParameter(
        this HttpRequestMessage request,
        string name)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var query = request.GetQuery();
        if (query.Length <= 1)
        {
            return null;
        }

        foreach (var pair in query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=', StringComparison.Ordinal);

            var key = separatorIndex < 0
                ? pair
                : pair[..separatorIndex];

            if (!string.Equals(Uri.UnescapeDataString(key), name, StringComparison.Ordinal))
            {
                continue;
            }

            return separatorIndex < 0
                ? string.Empty
                : Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
        }

        return null;
    }

    /// <summary>
    /// Reads a request header value.
    /// </summary>
    /// <param name="request">The recorded request.</param>
    /// <param name="name">The header name.</param>
    /// <returns>The first value, or <see langword="null"/> when the header is absent.</returns>
    /// <remarks>
    /// Content headers such as <c>Content-Type</c> live on the body, not the request, so
    /// they are looked up there as a fallback.
    /// </remarks>
    public static string? GetHeader(
        this HttpRequestMessage request,
        string name)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (request.Headers.TryGetValues(name, out var values))
        {
            return values.FirstOrDefault();
        }

        if (request.Content is not null &&
            request.Content.Headers.TryGetValues(name, out var contentValues))
        {
            return contentValues.FirstOrDefault();
        }

        return null;
    }

    /// <summary>
    /// Gets the bearer token from the <c>Authorization</c> header.
    /// </summary>
    /// <param name="request">The recorded request.</param>
    /// <returns>The token, or <see langword="null"/> when the scheme is not <c>Bearer</c>.</returns>
    public static string? GetBearerToken(this HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authorization = request.Headers.Authorization;

        return authorization is not null &&
               string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            ? authorization.Parameter
            : null;
    }

    /// <summary>
    /// Reads the request body as a string.
    /// </summary>
    /// <param name="request">The recorded request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The body, or an empty string when there is none.</returns>
    public static async Task<string> ReadBodyAsync(
        this HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Content is null)
        {
            return string.Empty;
        }

        return await request.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads and deserializes the request body.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="request">The recorded request.</param>
    /// <param name="jsonSerializerOptions">Optional serializer options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deserialized body, or <see langword="default"/> when there is no body.</returns>
    public static async Task<T?> ReadBodyAsAsync<T>(
        this HttpRequestMessage request,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Content is null)
        {
            return default;
        }

        var json = await request.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, jsonSerializerOptions);
    }
}