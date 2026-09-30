namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Resolves an XML model body - a request or response body whose only model media type is XML (no JSON one) - and
/// the generated <c>{Model}Xml</c> calls that write and read it. Shared by the server and both C# clients, so every
/// side reads what the others write.
/// </summary>
public static class XmlBodyHelper
{
    /// <summary>
    /// Finds the XML body of a content map that declares no JSON media type: a component model, or an array of one.
    /// </summary>
    /// <param name="content">The request or response content.</param>
    /// <returns>The XML body, or <see langword="null"/> when there is none (or a JSON media type is declared too).</returns>
    public static XmlBody? FindXmlOnlyBody(
        IDictionary<string, IOpenApiMediaType>? content)
    {
        if (content is null || content.Count == 0)
        {
            return null;
        }

        if (content.Any(entry => ResponseBodyClassifier.Classify(entry.Key, entry.Value.Schema) == ResponseBodyKind.Json))
        {
            return null;
        }

        foreach (var entry in content)
        {
            if (!XmlBodyReachability.IsXmlModelBody(entry.Key, entry.Value.Schema))
            {
                continue;
            }

            var schema = entry.Value.Schema;
            if (IsMappedModel(schema))
            {
                return new XmlBody(entry.Key, IsList: false, ListRootName: null);
            }

            if (schema is OpenApiSchema { Type: { } type } array &&
                (type & JsonSchemaType.Array) == JsonSchemaType.Array &&
                IsMappedModel(array.Items))
            {
                var rootName = array.Xml?.Name is { Length: > 0 } name && name != "##default" ? name : null;
                return new XmlBody(entry.Key, IsList: true, rootName);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a schema is a reference to a model record that gets a generated <c>{Model}Xml</c>: an object (or
    /// allOf) schema, not polymorphic, not an enum, array or paginated generic.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <returns><see langword="true"/> for a mapped model.</returns>
    public static bool IsMappedModel(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference { Target: OpenApiSchema target } &&
           !target.HasPolymorphicComposition() &&
           target.Enum is not { Count: > 0 } &&
           (target.Type is { } type ? (type & JsonSchemaType.Object) == JsonSchemaType.Object : target.AllOf is { Count: > 0 }) &&
           !SchemaExtractor.IsPaginationBaseSchema(target);

    /// <summary>
    /// The expression that writes a body as UTF-8 XML bytes.
    /// </summary>
    /// <param name="typeName">The body's C# type (<c>Pet</c>, <c>List&lt;Pet&gt;</c>).</param>
    /// <param name="body">The XML body.</param>
    /// <param name="valueExpression">The value to write.</param>
    /// <returns>The expression.</returns>
    public static string ToUtf8BytesExpression(
        string typeName,
        XmlBody body,
        string valueExpression)
        => body.IsList
            ? $"{CompanionOf(typeName, body)}.ListToUtf8Bytes({valueExpression}{(body.ListRootName is null ? string.Empty : $", \"{body.ListRootName}\"")})"
            : $"{CompanionOf(typeName, body)}.ToUtf8Bytes({valueExpression})";

    /// <summary>
    /// The method that reads a body from an XML document string (<c>PetXml.FromXml</c>, <c>PetXml.ListFromXml</c>).
    /// </summary>
    /// <param name="typeName">The body's C# type.</param>
    /// <param name="body">The XML body.</param>
    /// <returns>The method group.</returns>
    public static string FromXmlMethod(
        string typeName,
        XmlBody body)
        => $"{CompanionOf(typeName, body)}.{(body.IsList ? "ListFromXml" : "FromXml")}";

    /// <summary>
    /// The method that reads a body from its root element (<c>PetXml.Read</c>, <c>PetXml.ReadList</c>).
    /// </summary>
    /// <param name="typeName">The body's C# type.</param>
    /// <param name="body">The XML body.</param>
    /// <returns>The method group.</returns>
    public static string ReadMethod(
        string typeName,
        XmlBody body)
        => $"{CompanionOf(typeName, body)}.{(body.IsList ? "ReadList" : "Read")}";

    private static string CompanionOf(
        string typeName,
        XmlBody body)
    {
        var model = typeName.TrimEnd('?');
        if (body.IsList)
        {
            var open = model.IndexOf('<');
            model = open >= 0 && model.EndsWith(">", StringComparison.Ordinal)
                ? model.Substring(open + 1, model.Length - open - 2).TrimEnd('?')
                : model.TrimEnd(']').TrimEnd('[');
        }

        return model + "Xml";
    }
}