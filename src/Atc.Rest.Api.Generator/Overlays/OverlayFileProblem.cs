namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// A problem an overlay file reported.
/// </summary>
/// <param name="OverlayPath">The overlay file.</param>
/// <param name="Problem">The problem.</param>
public sealed record OverlayFileProblem(
    string OverlayPath,
    OverlayProblem Problem)
{
    /// <summary>
    /// Gets where in the overlay the problem is: <c>" (actions[2])"</c>, or empty for the overlay as a whole.
    /// </summary>
    public string Where
        => Problem.ActionIndex is { } index
            ? " (actions[" + index.ToString(CultureInfo.InvariantCulture) + "])"
            : string.Empty;
}