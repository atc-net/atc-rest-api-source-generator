namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension members for a <c>$ref</c> to a schema.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "CA1708:Names should differ by more than case", Justification = "OK - CLang14 - multiple extension(...) blocks")]
public static class OpenApiSchemaReferenceExtensions
{
    /// <param name="schemaRef">The schema reference.</param>
    extension(OpenApiSchemaReference schemaRef)
    {
        /// <summary>
        /// Gets the name of the component the reference points at (<c>#/components/schemas/{name}</c>), or the schema's
        /// own id when the reference carries none.
        /// </summary>
        public string? ComponentName
            => schemaRef.Reference.Id ?? schemaRef.Id;
    }

    /// <param name="schema">The schema, or a <c>$ref</c> to one.</param>
    extension(IOpenApiSchema? schema)
    {
        /// <summary>
        /// Gets the schema a <c>$ref</c> points at, or the schema itself when it is no reference; <see langword="null"/>
        /// for a reference that resolves to nothing.
        /// </summary>
        public IOpenApiSchema? Dereferenced
            => schema is OpenApiSchemaReference reference
                ? reference.Target
                : schema;
    }
}