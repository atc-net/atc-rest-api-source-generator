namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The casing of a document's enum values (<c>ATC_API_NAM005</c>). PascalCase and UPPER_SNAKE_CASE are always fine (they
/// are C# names as they are). Any other casing is fine when the document uses it throughout - the values are a vocabulary
/// the server, the clients and a hub share, v3 writes a value as the specification has it, and its extensible enums read
/// any casing. A document that mixes other casings has the values out of the majority's reported.
/// </summary>
internal sealed record EnumValueCasing(
    string Name,
    Func<string, bool> Matches,
    Func<string, string> Suggest)
{
    /// <summary>
    /// The other casings an enum value may be written in. A value can match several (<c>done</c> is camelCase,
    /// snake_case and kebab-case at once).
    /// </summary>
    private static readonly EnumValueCasing[] Casings =
    [
        new("camelCase", CasingHelper.IsCamelCase, CasingHelper.SuggestCamelCase),
        new("snake_case", CasingHelper.IsSnakeCase, v => CasingHelper.SuggestKebabCase(v).Replace('-', '_')),
        new("kebab-case", CasingHelper.IsKebabCase, CasingHelper.SuggestKebabCase),
    ];

    /// <summary>
    /// Whether a value is a C# name as it is: PascalCase or UPPER_SNAKE_CASE, fine in any document.
    /// </summary>
    /// <param name="value">The enum value.</param>
    /// <returns><see langword="true"/> for PascalCase or UPPER_SNAKE_CASE.</returns>
    public static bool IsCSharpStyle(string value)
        => CasingHelper.IsPascalCase(value) || CasingHelper.IsUpperSnakeCase(value);

    /// <summary>
    /// The casing the document's other enum values (neither PascalCase nor UPPER_SNAKE_CASE) are written in: the one
    /// most of them match (ties in the order camelCase, snake_case, kebab-case), or null when there is no such value.
    /// </summary>
    /// <param name="schemas">The document's component schemas.</param>
    /// <returns>The document's casing for the other values, or null.</returns>
    public static EnumValueCasing? OfDocument(
        IDictionary<string, IOpenApiSchema> schemas)
    {
        var values = schemas.Values
            .Select(s => s is OpenApiSchemaReference { Target: not null } reference ? reference.Target : s)
            .Where(s => s.Enum is { Count: > 0 })
            .SelectMany(s => s.Enum!)
            .Select(v => v?.ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v) && !IsCSharpStyle(v!))
            .Select(v => v!)
            .ToList();
        if (values.Count == 0)
        {
            return null;
        }

        return Casings
            .Select((casing, order) => (Casing: casing, Order: order, Count: values.Count(casing.Matches)))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Order)
            .First()
            .Casing;
    }
}