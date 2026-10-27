namespace Atc.Rest.Api.Client.Testing;

/// <summary>
/// The bodies of the requests a <see cref="FakeHttpMessageHandler"/> received, kept apart from the requests: a
/// generated client disposes the request - and its content - after sending it, so the content cannot be read later.
/// </summary>
internal static class RecordedBodies
{
    private static readonly ConditionalWeakTable<HttpRequestMessage, byte[]> Bodies = new();

    /// <summary>
    /// Keeps the body of <paramref name="request"/> as it was sent.
    /// </summary>
    public static void Record(
        HttpRequestMessage request,
        byte[] body)
        => Bodies.AddOrUpdate(request, body);

    /// <summary>
    /// Gets the body recorded for <paramref name="request"/>, if it passed through a <see cref="FakeHttpMessageHandler"/>.
    /// </summary>
    public static bool TryGet(
        HttpRequestMessage request,
        [NotNullWhen(true)] out byte[]? body)
        => Bodies.TryGetValue(request, out body);
}