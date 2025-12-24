namespace Atc.OpenApi.Models;

/// <summary>
/// Information about a prefixItems tuple schema (JSON Schema 2020-12 / OpenAPI 3.1).
/// </summary>
public sealed record TupleInfo
{
    /// <summary>
    /// Gets the list of prefix item schemas with types and optional names.
    /// </summary>
    public IReadOnlyList<TupleItemInfo> PrefixItems { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether this is a strict tuple (items: false).
    /// When true, no additional items beyond prefixItems are allowed.
    /// </summary>
    public bool IsStrictTuple { get; init; }

    /// <summary>
    /// Gets the C# type for additional items when not a strict tuple.
    /// Null when IsStrictTuple is true or items is not specified.
    /// </summary>
    public string? AdditionalItemsType { get; init; }

    /// <summary>
    /// Gets the minimum number of items constraint, or null if not specified.
    /// </summary>
    public int? MinItems { get; init; }

    /// <summary>
    /// Gets the maximum number of items constraint, or null if not specified.
    /// </summary>
    public int? MaxItems { get; init; }
}

/// <summary>
/// Information about a single item in a prefixItems tuple.
/// </summary>
public sealed record TupleItemInfo
{
    /// <summary>
    /// Gets the C# type for this tuple element.
    /// </summary>
    public string CSharpType { get; init; } = "object";

    /// <summary>
    /// Gets the name for this element (from description or positional like "Item1").
    /// </summary>
    public string Name { get; init; } = "Item1";

    /// <summary>
    /// Gets the original description from OpenAPI, or null if not specified.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether this element is nullable.
    /// </summary>
    public bool IsNullable { get; init; }
}
