namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Renames component schemas whose names are no identifier (<c>Note-Size</c>, <c>3DModel</c>,
/// <c>Company.Api.Thing</c>) to the sanitized name, in the document itself: the schema keys and every
/// reference to them. A generator that names types, files and imports after the schemas can then use the
/// names as they are. Schema names are not on the wire, so the API is unchanged.
/// </summary>
public static class SchemaNameNormalizer
{
    private const string SchemaReferencePrefix = "#/components/schemas/";

    /// <summary>
    /// Returns a document whose component schema names are all valid identifiers.
    /// </summary>
    /// <param name="document">The parsed OpenAPI document.</param>
    /// <returns>The document itself when no schema needs a new name, otherwise a renamed copy.</returns>
    public static OpenApiDocument WithIdentifierSchemaNames(
        OpenApiDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var schemas = document.Components?.Schemas;
        if (schemas is null || schemas.Count == 0)
        {
            return document;
        }

        var usedNames = new HashSet<string>(schemas.Keys, StringComparer.Ordinal);
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in schemas.Keys)
        {
            var sanitized = OpenApiSchemaExtensions.SanitizeSchemaName(name);
            if (string.Equals(sanitized, name, StringComparison.Ordinal))
            {
                continue;
            }

            var uniqueName = sanitized;
            var suffix = 1;
            while (!usedNames.Add(uniqueName))
            {
                suffix++;
                uniqueName = sanitized + suffix.ToString(CultureInfo.InvariantCulture);
            }

            renames[name] = uniqueName;
        }

        return Renamed(document, renames);
    }

    /// <summary>
    /// Returns a document in which no component schema has one of the given names: such a schema is renamed with
    /// the suffix (<c>Response</c> to <c>ResponseModel</c>), uniquely, and every reference to it follows. A
    /// generator whose output declares or uses those names itself (its own helpers, the platform's globals) can
    /// then name the models without a clash.
    /// </summary>
    /// <param name="document">The parsed OpenAPI document.</param>
    /// <param name="reservedNames">The names a schema cannot have.</param>
    /// <param name="suffix">The suffix a renamed schema gets.</param>
    /// <returns>The document itself when no schema needs a new name, otherwise a renamed copy.</returns>
    public static OpenApiDocument WithSchemaNamesOutside(
        OpenApiDocument document,
        IReadOnlyCollection<string> reservedNames,
        string suffix)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var schemas = document.Components?.Schemas;
        if (schemas is null || schemas.Count == 0 || reservedNames is null || reservedNames.Count == 0)
        {
            return document;
        }

        var reserved = new HashSet<string>(reservedNames, StringComparer.Ordinal);
        var usedNames = new HashSet<string>(schemas.Keys, StringComparer.Ordinal);
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in schemas.Keys.Where(reserved.Contains))
        {
            var uniqueName = name + suffix;
            var counter = 1;
            while (!usedNames.Add(uniqueName) || reserved.Contains(uniqueName))
            {
                counter++;
                uniqueName = name + suffix + counter.ToString(CultureInfo.InvariantCulture);
            }

            renames[name] = uniqueName;
        }

        return Renamed(document, renames);
    }

    private static OpenApiDocument Renamed(
        OpenApiDocument document,
        Dictionary<string, string> renames)
    {
        if (renames.Count == 0)
        {
            return document;
        }

        var root = JsonNode.Parse(Services.SpecificationService.SerializeToJson(document));
        if (root?["components"]?["schemas"] is not JsonObject schemasNode)
        {
            return document;
        }

        // The schema keys, in their original order
        var entries = schemasNode.ToList();
        schemasNode.Clear();
        foreach (var entry in entries)
        {
            schemasNode[renames.TryGetValue(entry.Key, out var renamed) ? renamed : entry.Key] = entry.Value;
        }

        RenameReferences(root, renames);

        var (normalized, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(
            root.ToJsonString(),
            "schema-names-normalized.json");

        return normalized ?? document;
    }

    private static void RenameReferences(
        JsonNode? node,
        Dictionary<string, string> renames)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (property.Value is JsonValue value &&
                        value.GetValueKind() == JsonValueKind.String &&
                        TryRename(value.GetValue<string>(), renames, out var renamed) &&
                        (property.Key == "$ref" || IsDiscriminatorMapping(obj)))
                    {
                        obj[property.Key] = renamed;
                    }
                    else
                    {
                        RenameReferences(property.Value, renames);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    RenameReferences(item, renames);
                }

                break;
        }
    }

    /// <summary>
    /// A discriminator's <c>mapping</c> object: its values name schemas, as a reference or as a bare name.
    /// </summary>
    private static bool IsDiscriminatorMapping(JsonObject obj)
        => obj.Parent is JsonObject parent &&
           parent.ContainsKey("propertyName") &&
           ReferenceEquals(parent["mapping"], obj);

    private static bool TryRename(
        string value,
        Dictionary<string, string> renames,
        out string renamed)
    {
        var isReference = value.StartsWith(SchemaReferencePrefix, StringComparison.Ordinal);
        var name = isReference ? value.Substring(SchemaReferencePrefix.Length) : value;

        // A JSON pointer escapes "~" and "/", and a reference may be percent-encoded
        if (isReference)
        {
            name = Uri.UnescapeDataString(name).Replace("~1", "/").Replace("~0", "~");
        }

        if (renames.TryGetValue(name, out var newName))
        {
            renamed = isReference ? SchemaReferencePrefix + newName : newName;
            return true;
        }

        renamed = value;
        return false;
    }
}