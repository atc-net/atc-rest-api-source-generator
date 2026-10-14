namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// One argument of a hub event or method, in its place on the wire.
/// </summary>
/// <param name="Name">Its name in the specification (<c>payload</c> for the one argument of the short form).</param>
/// <param name="Payload">What it carries.</param>
public sealed record SignalRHubArgument(
    string Name,
    SignalRHubPayload Payload);