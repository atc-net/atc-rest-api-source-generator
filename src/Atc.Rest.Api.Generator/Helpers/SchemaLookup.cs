namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Finds a component schema by the schema object Microsoft.OpenApi resolved a <c>$ref</c> to - the typed client and the
/// result extractor both need the name back from the resolved object.
/// </summary>
public static class SchemaLookup
{
    /// <summary>
    /// Finds the schema name in <c>Components.Schemas</c> whose schema is <paramref name="itemSchema"/> (the same object),
    /// else one whose <c>title</c> is the same (specifications often title a schema with its name).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="itemSchema">The resolved schema.</param>
    /// <returns>The schema name, or <see langword="null"/> when no component matches.</returns>
    public static string? FindSchemaNameByReference(
        OpenApiDocument openApiDoc,
        OpenApiSchema itemSchema)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (itemSchema is null)
        {
            throw new ArgumentNullException(nameof(itemSchema));
        }

        if (openApiDoc.Components?.Schemas is null)
        {
            return null;
        }

        foreach (var kvp in openApiDoc.Components.Schemas)
        {
            // First try: compare by reference (same object instance)
            if (ReferenceEquals(kvp.Value, itemSchema))
            {
                return kvp.Key;
            }

            // Second try: match by Title property if set
            if (kvp.Value is OpenApiSchema componentSchema &&
                !string.IsNullOrEmpty(componentSchema.Title) &&
                !string.IsNullOrEmpty(itemSchema.Title) &&
                string.Equals(componentSchema.Title, itemSchema.Title, StringComparison.Ordinal))
            {
                return kvp.Key;
            }
        }

        return null;
    }
}