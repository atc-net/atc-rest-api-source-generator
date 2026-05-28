namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Selects which property subset of a schema should be emitted. OpenAPI's
/// <c>readOnly: true</c> properties belong to responses only, and <c>writeOnly: true</c>
/// properties belong to requests only; when a schema has either marker the model
/// extractor emits a dedicated <c>&lt;Name&gt;Writable</c> sibling alongside the
/// canonical <c>&lt;Name&gt;</c> so client method signatures can pick the right
/// variant per position (request body vs. response body).
/// </summary>
internal enum SchemaVariant
{
    /// <summary>No filtering — emit every declared property. Used when the schema has
    /// neither readOnly nor writeOnly markers anywhere.</summary>
    Combined,

    /// <summary>Response shape — omit properties marked <c>writeOnly: true</c>.</summary>
    Response,

    /// <summary>Request shape — omit properties marked <c>readOnly: true</c>.</summary>
    Request,
}