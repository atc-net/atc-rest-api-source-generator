namespace Atc.Rest.Api.Client.Testing;

/// <summary>
/// Builds the canned <see cref="HttpResponseMessage"/> instances fed to
/// <see cref="FakeHttpMessageHandler"/>.
/// </summary>
public static class HttpResponseMessageFactory
{
    /// <summary>
    /// Creates a response carrying <paramref name="payload"/> serialized as JSON.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="payload">The payload to serialize.</param>
    /// <param name="statusCode">The status code to return.</param>
    /// <param name="jsonSerializerOptions">Optional serializer options.</param>
    /// <returns>A JSON response.</returns>
    public static HttpResponseMessage Json<T>(
        T payload,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        JsonSerializerOptions? jsonSerializerOptions = null)
        => new(statusCode)
        {
            Content = JsonContent.Create(payload, options: jsonSerializerOptions),
        };

    /// <summary>
    /// Creates a response from a raw JSON string, without going through a serializer.
    /// </summary>
    /// <param name="json">The raw JSON body.</param>
    /// <param name="statusCode">The status code to return.</param>
    /// <returns>A JSON response.</returns>
    /// <remarks>
    /// Use this to reproduce a payload exactly as the real API returns it - including
    /// shapes a strongly typed model could not express, such as an unexpected null or
    /// an extra property.
    /// </remarks>
    public static HttpResponseMessage RawJson(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    /// <summary>
    /// Creates a response with a status code and an optional plain-text body.
    /// </summary>
    /// <param name="statusCode">The status code to return.</param>
    /// <param name="body">The body to return.</param>
    /// <returns>A plain-text response.</returns>
    public static HttpResponseMessage StatusCode(
        HttpStatusCode statusCode,
        string body = "")
        => new(statusCode)
        {
            Content = new StringContent(body),
        };

    /// <summary>
    /// Creates an <c>application/problem+json</c> response as described by RFC 7807.
    /// </summary>
    /// <param name="statusCode">The status code to return.</param>
    /// <param name="title">The problem title. Defaults to the status code reason phrase.</param>
    /// <param name="detail">An optional human-readable explanation.</param>
    /// <param name="type">An optional problem type URI.</param>
    /// <param name="instance">An optional URI identifying the specific occurrence.</param>
    /// <returns>A problem-details response.</returns>
    /// <remarks>
    /// The body is written as raw JSON rather than serialized from a generated
    /// <c>ProblemDetails</c> model, so this package stays independent of any generated code.
    /// </remarks>
    public static HttpResponseMessage ProblemDetails(
        HttpStatusCode statusCode,
        string? title = null,
        string? detail = null,
        string? type = null,
        string? instance = null)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = title ?? statusCode.ToString(),
            ["status"] = (int)statusCode,
        };

        if (type is not null)
        {
            payload["type"] = type;
        }

        if (detail is not null)
        {
            payload["detail"] = detail;
        }

        if (instance is not null)
        {
            payload["instance"] = instance;
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/problem+json"),
        };
    }

    /// <summary>
    /// Creates a response with no body at all.
    /// </summary>
    /// <param name="statusCode">The status code to return. Defaults to <c>204 No Content</c>.</param>
    /// <returns>A response without content.</returns>
    public static HttpResponseMessage NoContent(
        HttpStatusCode statusCode = HttpStatusCode.NoContent)
        => new(statusCode);
}