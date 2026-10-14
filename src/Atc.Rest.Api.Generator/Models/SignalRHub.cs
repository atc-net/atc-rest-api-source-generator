namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// One hub of the <c>x-signalr-hubs</c> extension.
/// </summary>
/// <param name="Key">The hub's key in the extension (<c>notifications</c>).</param>
/// <param name="Path">The path the hub is mapped at.</param>
/// <param name="Events">What the server sends its clients.</param>
/// <param name="Methods">What a client invokes on the server.</param>
public sealed record SignalRHub(
    string Key,
    string Path,
    IReadOnlyList<SignalRHubMember> Events,
    IReadOnlyList<SignalRHubMember> Methods);