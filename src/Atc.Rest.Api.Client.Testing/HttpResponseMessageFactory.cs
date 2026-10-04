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
    /// Creates an <c>application/problem+json</c> validation problem (RFC 9457 with an <c>errors</c> member), as
    /// Atc.Rest.MinimalApi's validation filter answers a request that fails validation.
    /// </summary>
    /// <param name="errors">The messages per field, keyed by the field's wire name.</param>
    /// <param name="statusCode">The status code to return. Defaults to <c>400 Bad Request</c>.</param>
    /// <param name="title">The problem title.</param>
    /// <returns>A validation-problem response.</returns>
    public static HttpResponseMessage ValidationProblem(
        IDictionary<string, string[]> errors,
        HttpStatusCode statusCode = HttpStatusCode.BadRequest,
        string title = "One or more validation errors occurred.")
    {
        ArgumentNullException.ThrowIfNull(errors);

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = title,
            ["status"] = (int)statusCode,
            ["errors"] = errors,
        };

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/problem+json"),
        };
    }

    /// <summary>
    /// Creates an <c>application/xml</c> response from raw XML.
    /// </summary>
    /// <param name="xml">The XML body.</param>
    /// <param name="statusCode">The status code to return.</param>
    /// <returns>An XML response.</returns>
    public static HttpResponseMessage Xml(
        string xml,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml"),
        };

    /// <summary>
    /// Creates an <c>application/problem+xml</c> response as described by RFC 9457 (the
    /// <c>urn:ietf:rfc:7807</c> namespace).
    /// </summary>
    /// <param name="statusCode">The status code to return.</param>
    /// <param name="title">The problem title. Defaults to the status code reason phrase.</param>
    /// <param name="detail">An optional human-readable explanation.</param>
    /// <returns>An XML problem-details response.</returns>
    public static HttpResponseMessage ProblemXml(
        HttpStatusCode statusCode,
        string? title = null,
        string? detail = null)
    {
        var problem = new System.Xml.Linq.XElement(
            System.Xml.Linq.XName.Get("problem", "urn:ietf:rfc:7807"),
            new System.Xml.Linq.XElement(System.Xml.Linq.XName.Get("title", "urn:ietf:rfc:7807"), title ?? statusCode.ToString()),
            new System.Xml.Linq.XElement(System.Xml.Linq.XName.Get("status", "urn:ietf:rfc:7807"), (int)statusCode));

        if (detail is not null)
        {
            problem.Add(new System.Xml.Linq.XElement(System.Xml.Linq.XName.Get("detail", "urn:ietf:rfc:7807"), detail));
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                problem.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),
                Encoding.UTF8,
                "application/problem+xml"),
        };
    }

    /// <summary>
    /// Creates a file response: the bytes with their media type and, optionally, a file name
    /// (<c>Content-Disposition: attachment</c>).
    /// </summary>
    /// <param name="content">The file's bytes.</param>
    /// <param name="contentType">The file's media type, for example <c>application/pdf</c>.</param>
    /// <param name="fileName">The file name the response names, or <see langword="null"/> for none.</param>
    /// <param name="statusCode">The status code to return.</param>
    /// <returns>A file response.</returns>
    public static HttpResponseMessage File(
        byte[] content,
        string contentType,
        string? fileName = null,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        if (fileName is not null)
        {
            body.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = fileName };
        }

        return new HttpResponseMessage(statusCode) { Content = body };
    }

    /// <summary>
    /// Creates a <c>text/event-stream</c> response with one <c>data:</c> event per item, each serialized as JSON - a
    /// streamed (<c>IAsyncEnumerable</c>) response the generated clients read item by item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to stream.</param>
    /// <param name="jsonSerializerOptions">Optional serializer options.</param>
    /// <returns>A server-sent-events response.</returns>
    public static HttpResponseMessage ServerSentEvents<T>(
        IEnumerable<T> items,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        var events = new StringBuilder();
        foreach (var item in items)
        {
            events.Append("data: ").Append(JsonSerializer.Serialize(item, jsonSerializerOptions)).Append("\n\n");
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(events.ToString(), Encoding.UTF8, "text/event-stream"),
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