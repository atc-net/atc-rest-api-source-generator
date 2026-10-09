#pragma warning disable IDE0001
namespace Showcase.BlazorApp.Services;

/// <summary>
/// Gateway service - Testings operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// Test exception handling by code.
    /// Returns success response or throws with HTTP status info.
    /// </summary>
    public async Task<(bool IsSuccess, int StatusCode, string Message, Showcase.Generated.Testing.Models.ExceptionTestResponse? Response)> TestExceptionAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Showcase.Generated.Testing.Client.GetExceptionTestParameters(Code: code);

        // The spec marks /testing with x-retry-enabled: false, so the resilience handler sends it once
        // and the actual error response comes back without retries.
        var result = await getExceptionTestEndpoint
            .ExecuteAsync(
                parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsOk)
        {
            return (true, (int)result.StatusCode, result.OkContent.Message, result.OkContent);
        }

        if (result.IsNotFound)
        {
            return (false, (int)result.StatusCode, "Not Found - Invalid code", null);
        }

        // For other status codes (400, 429, 500, 501, 504), read the error message
        var statusCode = (int)result.StatusCode;
        var message = $"Error: {result.StatusCode}";

        switch (result.ContentObject)
        {
            // ContentObject is the registered error type (ValidationProblemDetails for 400, ProblemDetails for the
            // others the client knows about), or the raw content for a status the spec does not list (501, 504)
            // A validation problem carries its messages in Errors (a ValidationException the handler throws: a message
            // without a field name is under the "" key) and has no Detail
            case Showcase.Generated.ValidationProblemDetails validationProblem:
                message = validationProblem.Detail
                    ?? (validationProblem.Errors is { Count: > 0 } errors ? string.Join("; ", errors.SelectMany(e => e.Value)) : null)
                    ?? validationProblem.Title
                    ?? message;
                break;
            case Showcase.Generated.ProblemDetails problemDetails:
                message = problemDetails.Detail ?? problemDetails.Title ?? message;
                break;
            case string jsonString when !string.IsNullOrEmpty(jsonString):
                // Try to deserialize raw JSON string to ProblemDetails
                try
                {
                    var pd = JsonSerializer.Deserialize<Showcase.Generated.ProblemDetails>(jsonString, jsonOptions);
                    if (pd is not null)
                    {
                        message = pd.Detail ?? pd.Title ?? message;
                    }
                }
                catch
                {
                    // If deserialization fails, use the raw string
                    message = jsonString;
                }

                break;
            default:
            {
                if (result.ContentObject is not null)
                {
                    message = result.ContentObject.ToString() ?? message;
                }

                break;
            }
        }

        return (false, statusCode, message, null);
    }

    /// <summary>
    /// Asks the API for an error status and reads it through the generated typed accessors.
    /// Every undeclared error body is an RFC 9457 ProblemDetails, so each accessor returns a parsed
    /// ProblemDetails (a ValidationProblemDetails with its <c>errors</c> for 400) - no string parsing needed.
    /// </summary>
    public async Task<ErrorResponseTestResult> TestErrorResponseAsync(
        int status,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Showcase.Generated.Testing.Client.GetErrorResponseTestParameters(Status: status);

        var result = await getErrorResponseTestEndpoint
            .ExecuteAsync(
                parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        result.Headers.TryGetValue("Retry-After", out var retryAfter);

        return result switch
        {
            { IsBadRequest: true } => FromValidationProblem(result.BadRequestContent),
            { IsNotFound: true } => FromProblem(result.NotFoundContent),
            { IsConflict: true } => FromProblem(result.ConflictContent),
            { IsUnprocessableEntity: true } => FromProblem(result.UnprocessableEntityContent),
            { IsTooManyRequests: true } => FromProblem(result.TooManyRequestsContent) with { RetryAfter = retryAfter?.FirstOrDefault() },
            _ => new ErrorResponseTestResult((int)result.StatusCode, Title: null, Detail: null, Errors: null, RetryAfter: null),
        };

        static ErrorResponseTestResult FromProblem(
            Showcase.Generated.ProblemDetails problem)
            => new(problem.Status ?? 0, problem.Title, problem.Detail, Errors: null, RetryAfter: null);

        static ErrorResponseTestResult FromValidationProblem(
            Showcase.Generated.ValidationProblemDetails problem)
            => new(
                problem.Status ?? 0,
                problem.Title,
                problem.Detail,
                problem.Errors is null
                    ? null
                    : string.Join("; ", problem.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")),
                RetryAfter: null);
    }

    /// <summary>
    /// Posts to an operation that declares its own 400 model (<c>TestingProblem</c>, without field errors) and reads
    /// the answer through the generated accessors: <c>BadRequestContent</c> is always the declared model, and
    /// <c>BadRequestValidationErrors</c> gives the field errors when the ValidationFilter rejected the request body.
    /// </summary>
    public async Task<DeclaredBadRequestTestResult> TestDeclaredBadRequestAsync(
        string name,
        int age,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Showcase.Generated.Testing.Client.PostDeclaredBadRequestTestParameters(
            Request: new Showcase.Generated.Testing.Models.DeclaredBadRequestTestRequest(Name: name, Age: age));

        var result = await postDeclaredBadRequestTestEndpoint
            .ExecuteAsync(
                parameters,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsBadRequest)
        {
            return new DeclaredBadRequestTestResult((int)result.StatusCode, Title: null, Detail: null, ValidationErrors: null);
        }

        var problem = result.BadRequestContent;
        var errors = result.BadRequestValidationErrors;
        return new DeclaredBadRequestTestResult(
            problem.Status ?? 0,
            problem.Title,
            problem.Detail,
            errors is null
                ? null
                : string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")));
    }
}