namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// What an event or a method of a hub carries: a component schema, a primitive, or an array of one of them.
/// </summary>
/// <param name="SchemaName">The component schema's name, or <see langword="null"/> for a primitive or an array.</param>
/// <param name="Type">The primitive's JSON type (<c>string</c>, <c>integer</c>, <c>number</c>, <c>boolean</c>), or <see langword="null"/>.</param>
/// <param name="Format">The primitive's format (<c>int64</c>, <c>uuid</c>, <c>date-time</c>), or <see langword="null"/>.</param>
/// <param name="Items">What an array holds, or <see langword="null"/> for no array.</param>
public sealed record SignalRHubPayload(
    string? SchemaName,
    string? Type,
    string? Format,
    SignalRHubPayload? Items)
{
    /// <summary>
    /// The component schemas this payload refers to (an array's items included).
    /// </summary>
    public IEnumerable<string> Schemas
        => Items?.Schemas ?? (SchemaName is null ? [] : [SchemaName]);
}