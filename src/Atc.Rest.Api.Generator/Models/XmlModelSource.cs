namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// A generated model record that an XML body can carry, with what its XML mapping is built from.
/// </summary>
/// <param name="SchemaKey">The schema name as in <c>components.schemas</c> (the default root element name).</param>
/// <param name="TypeName">The C# type name of the record.</param>
/// <param name="Schema">The schema as declared (its <c>xml</c> keyword names the root element).</param>
/// <param name="EffectiveSchema">The schema the record's properties come from (allOf merged).</param>
/// <param name="Record">The record as generated: its parameter names, types and defaults.</param>
public sealed record XmlModelSource(
    string SchemaKey,
    string TypeName,
    OpenApiSchema Schema,
    OpenApiSchema EffectiveSchema,
    RecordParameters Record);