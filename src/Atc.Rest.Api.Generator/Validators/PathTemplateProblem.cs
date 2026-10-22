namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// A problem of a path template (<c>ATC_API_PTH001</c>).
/// </summary>
/// <param name="Message">The diagnostic's message.</param>
/// <param name="Suggestions">How to fix it.</param>
/// <param name="EndsChecks">Whether the path is checked no further.</param>
internal sealed record PathTemplateProblem(
    string Message,
    IReadOnlyList<string> Suggestions,
    bool EndsChecks);