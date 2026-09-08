namespace Eloverblik.ThirdPartyApi.Client.Tests.TestDoubles;

/// <summary>
/// Builds the canned <see cref="HttpResponseMessage"/> instances fed to
/// <see cref="FakeHttpMessageHandler"/>.
/// </summary>
public static class HttpResponseMessageFactory
{
    public static HttpResponseMessage Json<T>(
        T payload,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = JsonContent.Create(payload),
        };

    public static HttpResponseMessage StatusCode(
        HttpStatusCode statusCode,
        string body = "")
        => new(statusCode)
        {
            Content = new StringContent(body),
        };
}
