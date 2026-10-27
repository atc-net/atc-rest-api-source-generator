namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// The result of applying an overlay: the changed document and what was reported.
/// </summary>
/// <param name="Document">The document after the overlay (unchanged when <see cref="HasErrors"/>).</param>
/// <param name="Problems">The errors and warnings, in action order.</param>
public sealed record OverlayApplication(
    JsonNode? Document,
    IReadOnlyList<OverlayProblem> Problems)
{
    /// <summary>
    /// Gets a value indicating whether the overlay could not be applied.
    /// </summary>
    public bool HasErrors => Problems.Any(p => p.IsError);
}