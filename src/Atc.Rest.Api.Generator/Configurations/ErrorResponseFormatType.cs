namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines the expected format of error responses from the API.
/// Only applies to EndpointPerOperation client generation mode.
/// </summary>
public enum ErrorResponseFormatType
{
    /// <summary>
    /// RFC 7807 ProblemDetails format (default).
    /// Uses ProblemDetails for errors and ValidationProblemDetails for 400 Bad Request.
    /// </summary>
    ProblemDetails = 0,

    /// <summary>
    /// Plain text error messages.
    /// Uses string for errors, but ValidationProblemDetails for 400 Bad Request.
    /// </summary>
    PlainText = 1,

    /// <summary>
    /// Plain text error messages for all responses including 400.
    /// Uses string for all error responses.
    /// </summary>
    PlainTextOnly = 2,

    /// <summary>
    /// Custom error response model defined in customErrorResponseModel config.
    /// Requires customErrorResponseModel to be configured.
    /// </summary>
    Custom = 3,
}