namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The literal values a variant's discriminator property takes, per variant schema and property name,
/// collected from every component <c>oneOf</c> / <c>anyOf</c> that declares a <c>discriminator</c>.
/// </summary>
/// <remarks>
/// A union only narrows in TypeScript when each variant's discriminator is a literal
/// (<c>type: 'credit_card'</c>), and Zod only dispatches by it in a <c>z.discriminatedUnion</c>. The value
/// is the <c>mapping</c> key naming the variant, or the variant's schema name when the mapping leaves it
/// out (OpenAPI's implicit mapping) - the values the C# converter dispatches on. A union without a
/// <c>discriminator</c> block contributes nothing: nothing in the specification fixes the value there.
/// </remarks>
internal sealed class TypeScriptDiscriminatorLiterals
{
    private readonly Dictionary<string, Dictionary<string, List<string>>> literalsByVariant = new(StringComparer.Ordinal);

    private TypeScriptDiscriminatorLiterals()
    {
    }

    /// <summary>
    /// Collects the discriminator literals of every discriminated union in <paramref name="openApiDoc"/>.
    /// </summary>
    public static TypeScriptDiscriminatorLiterals Collect(
        OpenApiDocument openApiDoc)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var result = new TypeScriptDiscriminatorLiterals();
        if (openApiDoc.Components?.Schemas is null)
        {
            return result;
        }

        foreach (var (_, schema) in openApiDoc.Components.Schemas)
        {
            if (schema is not OpenApiSchema union ||
                union.GetDiscriminatorPropertyName() is not { Length: > 0 } propertyName)
            {
                continue;
            }

            var mapping = union.GetDiscriminatorMapping();
            foreach (var variant in union.GetPolymorphicVariantSchemaNames())
            {
                var values = mapping?
                    .Where(m => string.Equals(m.Value, variant, StringComparison.Ordinal))
                    .Select(m => m.Key)
                    .ToList() ?? [];
                if (values.Count == 0)
                {
                    values.Add(variant);
                }

                result.Add(variant, propertyName, values);
            }
        }

        return result;
    }

    /// <summary>
    /// The literal values of <paramref name="propertyName"/> on <paramref name="variantName"/>, in document
    /// order, or an empty list when the property is not a discriminator of that variant.
    /// </summary>
    public IReadOnlyList<string> ValuesOf(
        string variantName,
        string propertyName)
        => literalsByVariant.TryGetValue(variantName, out var byProperty) &&
           byProperty.TryGetValue(propertyName, out var values)
            ? values
            : [];

    /// <summary>
    /// The TypeScript literal union of <see cref="ValuesOf"/> (<c>'circle' | 'round'</c>), or
    /// <see langword="null"/> when the property is not a discriminator of that variant.
    /// </summary>
    public string? TypeOf(
        string variantName,
        string propertyName)
    {
        var values = ValuesOf(variantName, propertyName);
        return values.Count == 0
            ? null
            : string.Join(" | ", values.Select(Literal));
    }

    /// <summary>
    /// The Zod schema of <see cref="ValuesOf"/> (<c>z.literal('circle')</c>, or a <c>z.union</c> of literals),
    /// or <see langword="null"/> when the property is not a discriminator of that variant.
    /// </summary>
    public string? ZodOf(
        string variantName,
        string propertyName)
    {
        var values = ValuesOf(variantName, propertyName);
        return values.Count switch
        {
            0 => null,
            1 => "z.literal(" + Literal(values[0]) + ")",
            _ => "z.union([" + string.Join(", ", values.Select(v => "z.literal(" + Literal(v) + ")")) + "])",
        };
    }

    /// <summary>
    /// A single-quoted TypeScript string literal.
    /// </summary>
    private static string Literal(string value)
        => "'" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal) + "'";

    private void Add(
        string variantName,
        string propertyName,
        List<string> values)
    {
        if (!literalsByVariant.TryGetValue(variantName, out var byProperty))
        {
            byProperty = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            literalsByVariant[variantName] = byProperty;
        }

        if (!byProperty.TryGetValue(propertyName, out var existing))
        {
            existing = [];
            byProperty[propertyName] = existing;
        }

        foreach (var value in values.Where(v => !existing.Contains(v, StringComparer.Ordinal)))
        {
            existing.Add(value);
        }
    }
}