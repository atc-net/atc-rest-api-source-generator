// This file intentionally throws exceptions for testing GlobalErrorHandlingMiddleware
#pragma warning disable S112 // General exceptions should not be thrown
#pragma warning disable CA2201 // Do not raise reserved exception types
#pragma warning disable S3928 // Parameter names used in ArgumentException
#pragma warning disable MA0015 // Specify the parameter name
#pragma warning disable MA0012 // Do not raise reserved exception type
#pragma warning disable CA1848 // Use LoggerMessage delegates

namespace Showcase.Api.Domain.ApiHandlers.Testings;

/// <summary>
/// Handler for testing exception handling.
/// Throws different exceptions based on the code parameter to test GlobalErrorHandlingMiddleware.
/// </summary>
public sealed class GetExceptionTestHandler : IGetExceptionTestHandler
{
    private readonly ILogger<GetExceptionTestHandler> logger;

    public GetExceptionTestHandler(ILogger<GetExceptionTestHandler> logger)
        => this.logger = logger;

    public Task<GetExceptionTestResult> ExecuteAsync(
        GetExceptionTestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Testing exception handling with code: {Code}", parameters.Code);

        return parameters.Code switch
        {
            0 => SuccessResponse(parameters.Code),
            1 => ThrowGeneralException(),
            2 => ThrowArgumentException(),
            3 => ThrowArgumentNullException(),
            4 => ThrowInvalidOperationException(),
            5 => ThrowUnauthorizedAccessException(),
            6 => ThrowNotImplementedException(),
            7 => ThrowTimeoutException(),
            8 => ThrowValidationException(),
            9 => ThrowKeyNotFoundException(),
            10 => ThrowNullReferenceException(),
            _ => Task.FromResult(GetExceptionTestResult.NotFound()),
        };
    }

    private static Task<GetExceptionTestResult> SuccessResponse(int code)
    {
        var response = new ExceptionTestResponse(
            Code: code,
            Message: "No exception thrown - success!",
            Timestamp: DateTimeOffset.UtcNow);

        return Task.FromResult<GetExceptionTestResult>(response);
    }

    private static Task<GetExceptionTestResult> ThrowGeneralException()
        => throw new Exception("General Exception (code 1) - This maps to 500 Internal Server Error");

    private static Task<GetExceptionTestResult> ThrowArgumentException()
        => throw new ArgumentException("Argument Exception (code 2) - This maps to 400 Bad Request", "code");

    private static Task<GetExceptionTestResult> ThrowArgumentNullException()
        => throw new ArgumentNullException("code", "Argument Null Exception (code 3) - This maps to 400 Bad Request");

    private static Task<GetExceptionTestResult> ThrowInvalidOperationException()
        => throw new InvalidOperationException("Invalid Operation Exception (code 4) - This maps to 409 Conflict");

    private static Task<GetExceptionTestResult> ThrowUnauthorizedAccessException()
        => throw new UnauthorizedAccessException("Unauthorized Access Exception (code 5) - This maps to 401 Unauthorized");

    private static Task<GetExceptionTestResult> ThrowNotImplementedException()
        => throw new NotImplementedException("Not Implemented Exception (code 6) - This maps to 501 Not Implemented");

    private static Task<GetExceptionTestResult> ThrowTimeoutException()
        => throw new TimeoutException("Timeout Exception (code 7) - This maps to 504 Gateway Timeout");

    private static Task<GetExceptionTestResult> ThrowValidationException()
        => throw new System.ComponentModel.DataAnnotations.ValidationException("Validation Exception (code 8) - This maps to 400 Bad Request");

    private static Task<GetExceptionTestResult> ThrowKeyNotFoundException()
        => throw new KeyNotFoundException("Key Not Found Exception (code 9) - This maps to 500 Internal Server Error (default)");

    private static Task<GetExceptionTestResult> ThrowNullReferenceException()
        => throw new NullReferenceException("Null Reference Exception (code 10) - This maps to 500 Internal Server Error");
}