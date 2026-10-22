namespace Atc.OpenApi.Helpers;

/// <summary>
/// The names of the array property that holds a page's items (<c>items</c>, <c>results</c>, <c>data</c>,
/// <c>records</c>, <c>values</c>, <c>content</c>) - one list for the validator that recognizes a paginated response,
/// the model that makes a pagination base generic and the clients that type a page as <c>Base&lt;Item&gt;</c>.
/// </summary>
public static class PaginationConventions
{
    /// <summary>
    /// The page array names, in the order a base with several of them is read.
    /// </summary>
    public static IReadOnlyList<string> ArrayPropertyNames { get; } = new[] { "items", "results", "data", "records", "values", "content" };

    /// <summary>
    /// Whether a property name is one of the page array names, in any case.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns><see langword="true"/> for a page array name.</returns>
    public static bool IsArrayPropertyName(string? propertyName)
        => propertyName is not null &&
           ArrayPropertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase);
}