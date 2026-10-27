namespace Showcase.BlazorApp.Models;

/// <summary>
/// An opened price stream, whichever operation opened it: the response (it owns the connection), and what the
/// operation's own result class read from it - the declared headers and the typed error.
/// </summary>
public sealed record PriceStreamCall(
    StreamingEndpointResponse<PriceTick> Response,
    string? Format,
    ProblemDetails? Problem,
    int? RetryAfterSeconds) : IDisposable
{
    public void Dispose()
        => Response.Dispose();
}