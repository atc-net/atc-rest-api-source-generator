namespace Atc.Rest.Api.Client.Testing;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records every outgoing request and
/// replays canned responses, so a generated client can be exercised end-to-end
/// without a real server.
/// </summary>
/// <remarks>
/// Responses are consumed in order. Once the queue is exhausted the last entry is
/// reused, so a test that only cares about a single call does not have to enumerate
/// every retry a resilience policy might perform.
/// </remarks>
/// <example>
/// <code>
/// using var handler = new FakeHttpMessageHandler(
///     () => HttpResponseMessageFactory.Json(new { id = 1 }));
///
/// using var httpClient = handler.CreateClient("https://api.example.com");
/// var client = new MyApiClient(httpClient);
///
/// await client.GetItemAsync(new GetItemParameters(Id: 1), CancellationToken.None);
///
/// handler.Requests.Should().ContainSingle();
/// </code>
/// </example>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage>[] responses;
    private readonly List<KeyedResponse> keyedResponses = [];
    private readonly List<HttpRequestMessage> requests = [];
    private readonly Lock gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeHttpMessageHandler"/> class
    /// that replays the supplied responses in order.
    /// </summary>
    /// <param name="responses">The responses to replay. The last one is reused once exhausted.</param>
    public FakeHttpMessageHandler(params Func<HttpResponseMessage>[] responses)
    {
        ArgumentNullException.ThrowIfNull(responses);

        this.responses = responses
            .Select(Func<HttpRequestMessage, HttpResponseMessage> (response) => _ => response())
            .ToArray();
    }

    private FakeHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    {
        this.responses = responses;
    }

    /// <summary>
    /// Creates a handler whose responses can inspect the incoming request.
    /// </summary>
    /// <param name="responses">The responses to replay. The last one is reused once exhausted.</param>
    /// <returns>A new <see cref="FakeHttpMessageHandler"/>.</returns>
    public static FakeHttpMessageHandler FromRequests(
        params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    {
        ArgumentNullException.ThrowIfNull(responses);

        return new FakeHttpMessageHandler(responses);
    }

    /// <summary>
    /// Gets every request that passed through this handler, in order (a snapshot; requests sent concurrently are all
    /// recorded). A request's body can be read with <see cref="HttpRequestMessageExtensions.ReadBodyAsync"/> even after
    /// the client disposed the request, as generated clients do.
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    /// <summary>
    /// Gets or sets a task that is awaited before a response is produced.
    /// </summary>
    /// <remarks>
    /// Assign a pending task to hold a request open - useful for observing
    /// cancellation, timeout or concurrency behaviour.
    /// </remarks>
    public Task ResponseGate { get; set; } = Task.CompletedTask;

    /// <summary>
    /// Gets the request recorded at the supplied index.
    /// </summary>
    /// <param name="index">The zero-based index of the request.</param>
    /// <returns>The recorded request.</returns>
    public HttpRequestMessage Request(int index)
    {
        lock (gate)
        {
            return requests[index];
        }
    }

    /// <summary>
    /// Registers a response for every request whose absolute path ends with
    /// <paramref name="pathSuffix"/>, whatever its method, regardless of call order.
    /// </summary>
    /// <param name="pathSuffix">The path suffix to match, for example <c>/api/items</c>.</param>
    /// <param name="response">The response factory to invoke on a match.</param>
    /// <returns>The same handler, so calls can be chained.</returns>
    public FakeHttpMessageHandler RespondTo(
        string pathSuffix,
        Func<HttpResponseMessage> response)
        => Register(method: null, pathSuffix, response);

    /// <summary>
    /// Registers a response for every request with <paramref name="method"/> whose absolute path ends with
    /// <paramref name="pathSuffix"/>, regardless of call order. A method-specific registration wins over one for
    /// every method.
    /// </summary>
    /// <param name="method">The HTTP method to match, for example <see cref="HttpMethod.Post"/>.</param>
    /// <param name="pathSuffix">The path suffix to match, for example <c>/api/items</c>.</param>
    /// <param name="response">The response factory to invoke on a match.</param>
    /// <returns>The same handler, so calls can be chained.</returns>
    public FakeHttpMessageHandler RespondTo(
        HttpMethod method,
        string pathSuffix,
        Func<HttpResponseMessage> response)
    {
        ArgumentNullException.ThrowIfNull(method);
        return Register(method, pathSuffix, response);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> wired to this handler.
    /// </summary>
    /// <param name="baseAddress">The base address the client should use.</param>
    /// <returns>An <see cref="HttpClient"/> that routes through this handler.</returns>
    /// <remarks>
    /// The handler is not owned by the returned client, so the same handler can back
    /// several clients and stays inspectable after the client is disposed.
    /// </remarks>
    public HttpClient CreateClient(string baseAddress = "https://localhost")
        => new(this, disposeHandler: false) { BaseAddress = new Uri(baseAddress) };

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The body is kept apart from the request: generated clients dispose the request (and its content) after sending
        if (request.Content is not null)
        {
            RecordedBodies.Record(request, await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
        }

        int position;
        KeyedResponse? keyed;
        lock (gate)
        {
            requests.Add(request);
            position = requests.Count - 1;
            keyed = FindKeyedResponse(request);
        }

        await ResponseGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (keyed is not null)
        {
            return keyed.Response(request);
        }

        if (responses.Length == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(FakeHttpMessageHandler)} received a request for '{request.RequestUri}' but no response was configured.");
        }

        return responses[Math.Min(position, responses.Length - 1)](request);
    }

    private FakeHttpMessageHandler Register(
        HttpMethod? method,
        string pathSuffix,
        Func<HttpResponseMessage> response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathSuffix);
        ArgumentNullException.ThrowIfNull(response);

        lock (gate)
        {
            keyedResponses.RemoveAll(k => k.Method == method && string.Equals(k.PathSuffix, pathSuffix, StringComparison.OrdinalIgnoreCase));
            keyedResponses.Add(new KeyedResponse(method, pathSuffix, _ => response()));
        }

        return this;
    }

    private KeyedResponse? FindKeyedResponse(HttpRequestMessage request)
    {
        if (keyedResponses.Count == 0 ||
            request.RequestUri is null)
        {
            return null;
        }

        var path = request.RequestUri.IsAbsoluteUri
            ? request.RequestUri.AbsolutePath
            : request.RequestUri.OriginalString;
        var matches = keyedResponses
            .Where(k => (k.Method is null || k.Method == request.Method) &&
                        path.EndsWith(k.PathSuffix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Find(k => k.Method is not null) ?? matches.FirstOrDefault();
    }

    private sealed record KeyedResponse(
        HttpMethod? Method,
        string PathSuffix,
        Func<HttpRequestMessage, HttpResponseMessage> Response);
}