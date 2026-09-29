namespace Showcase.Api.Domain.ApiHandlers.Testing;

/// <summary>
/// Handler for testing error responses.
/// Returns the requested error status through the generated result factories: because the specification
/// declares no body for these statuses, every one of them is written as an RFC 9457 ProblemDetails
/// (<c>application/problem+json</c>), with the message as <c>detail</c>.
/// </summary>
public sealed class GetErrorResponseTestHandler : IGetErrorResponseTestHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Testings.GetErrorResponseTest");

    public Task<GetErrorResponseTestResult> ExecuteAsync(
        GetErrorResponseTestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetErrorResponseTest");

        var result = parameters.Status switch
        {
            400 => GetErrorResponseTestResult.BadRequest(
                new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["name"] = ["The name must be at most 20 characters."],
                    })),
            404 => GetErrorResponseTestResult.NotFound("No item with that id exists."),
            409 => GetErrorResponseTestResult.Conflict("The item was changed by someone else."),
            422 => GetErrorResponseTestResult.UnprocessableEntity("The item cannot be enabled while it is archived."),
            429 => GetErrorResponseTestResult.TooManyRequests(retryAfterSeconds: 30, message: "Slow down - try again in 30 seconds."),
            _ => GetErrorResponseTestResult.NoContent(),
        };

        return Task.FromResult(result);
    }
}