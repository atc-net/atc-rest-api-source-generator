namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// An event or a method of a hub.
/// </summary>
/// <param name="Name">Its name on the wire, as the specification writes it.</param>
/// <param name="Payload">What it carries, or <see langword="null"/> for nothing.</param>
/// <param name="Returns">What a method answers, or <see langword="null"/> for nothing (never set on an event).</param>
public sealed record SignalRHubMember(
    string Name,
    SignalRHubPayload? Payload,
    SignalRHubPayload? Returns);