namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// Something wrong with an overlay, or an action that changed nothing.
/// </summary>
/// <param name="IsError">Whether the overlay cannot be applied (otherwise a warning: the action did nothing).</param>
/// <param name="ActionIndex">The index of the action in <c>actions</c>, or <see langword="null"/> for the overlay as a whole.</param>
/// <param name="Message">What is wrong.</param>
public sealed record OverlayProblem(
    bool IsError,
    int? ActionIndex,
    string Message);