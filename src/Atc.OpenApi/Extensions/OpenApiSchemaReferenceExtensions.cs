namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension members for a <c>$ref</c> to a schema.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
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
}