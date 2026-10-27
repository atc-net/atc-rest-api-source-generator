namespace Showcase.Api.Domain.ApiHandlers.Testing;

/// <summary>
/// Handler for testing a declared 400 model.
/// The specification declares <c>TestingProblem</c> (a problem without field errors) for 400. A request body that
/// breaks its rules never reaches this handler: the generated ValidationFilter rejects it with a
/// ValidationProblemDetails. The handler itself rejects the name "taken" with the declared model.
/// </summary>
public sealed class PostDeclaredBadRequestTestHandler : IPostDeclaredBadRequestTestHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Testings.PostDeclaredBadRequestTest");

    public Task<PostDeclaredBadRequestTestResult> ExecuteAsync(
        PostDeclaredBadRequestTestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PostDeclaredBadRequestTest");

        var result = string.Equals(parameters.Request.Name, "taken", StringComparison.OrdinalIgnoreCase)
            ? PostDeclaredBadRequestTestResult.BadRequest(
                new TestingProblem(
                    Type: "https://showcase.example/problems/name-taken",
                    Title: "The name is taken",
                    Status: 400,
                    Detail: "Choose another name - \"taken\" is already in use."))
            : PostDeclaredBadRequestTestResult.NoContent();

        return Task.FromResult(result);
    }
}