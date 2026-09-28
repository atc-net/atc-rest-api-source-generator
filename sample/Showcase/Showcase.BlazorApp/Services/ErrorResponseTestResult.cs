namespace Showcase.BlazorApp.Services;

/// <summary>
/// What the Blazor app read from one error response.
/// </summary>
/// <param name="Status">The <c>status</c> of the ProblemDetails (or the HTTP status when there is no body).</param>
/// <param name="Title">The ProblemDetails <c>title</c>.</param>
/// <param name="Detail">The ProblemDetails <c>detail</c> - the handler's message.</param>
/// <param name="Errors">The validation errors (400), keyed by the wire name the client sent.</param>
/// <param name="RetryAfter">The <c>Retry-After</c> header (429).</param>
public sealed record ErrorResponseTestResult(
    int Status,
    string? Title,
    string? Detail,
    string? Errors,
    string? RetryAfter);