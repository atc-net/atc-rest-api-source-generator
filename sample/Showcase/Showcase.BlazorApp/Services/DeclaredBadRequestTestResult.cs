namespace Showcase.BlazorApp.Services;

/// <summary>
/// What the Blazor app read from an operation that declares its own 400 model.
/// </summary>
/// <param name="Status">The <c>status</c> of the declared model (or the HTTP status when it is no 400).</param>
/// <param name="Title">The declared model's <c>title</c>.</param>
/// <param name="Detail">The declared model's <c>detail</c>.</param>
/// <param name="ValidationErrors">The ValidationFilter's field errors (<c>BadRequestValidationErrors</c>), if any.</param>
public sealed record DeclaredBadRequestTestResult(
    int Status,
    string? Title,
    string? Detail,
    string? ValidationErrors);