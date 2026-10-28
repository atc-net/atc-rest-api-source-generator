namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Finds the properties listed in <c>required</c> that are generated optional anyway (<c>ATC_API_SCH029</c>): a
/// <c>readOnly</c> one in a schema requests carry, a <c>writeOnly</c> one in a schema responses carry. One record serves
/// requests and responses, so the property must be omittable where it does not apply (see
/// <see cref="SchemaUsageReachability.KeepsRequired"/>). A schema no operation uses reports nothing - which direction it
/// will be used in is unknown. Each property is reported on the schema that declares it (an inherited one on its base).
/// The directions are those of the document the models are generated from: with the marker's <c>includePaths</c> /
/// <c>excludePaths</c>, the path-filtered one.
/// </summary>
internal static class ReadOrWriteOnlyRequiredValidator
{
    public static List<DiagnosticMessage> Validate(
        OpenApiDocument document,
        string sourceFilePath,
        IList<string>? includePaths = null,
        IList<string>? excludePaths = null)
    {
        var diagnostics = new List<DiagnosticMessage>();
        if (document.Components?.Schemas is not { Count: > 0 } schemas)
        {
            return diagnostics;
        }

        var usage = SchemaUsageReachability.Compute(PathFilterHelper.WithFilteredPaths(document, includePaths, excludePaths));
        foreach (var entry in schemas)
        {
            var schemaName = entry.Key;
            if (entry.Value is not OpenApiSchema schema)
            {
                continue;
            }

            var (inRequests, inResponses) = usage.DirectionsOf(schemaName);
            if (!inRequests && !inResponses)
            {
                continue;
            }

            var (properties, required) = DeclaredProperties(schema);
            foreach (var property in properties)
            {
                if (!required.Contains(property.Key) ||
                    !SchemaUsageReachability.IsReadOrWriteOnly(property.Value) ||
                    SchemaUsageReachability.KeepsRequired(usage, schemaName, property.Value))
                {
                    continue;
                }

                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.RequiredReadOrWriteOnlyPropertyGeneratedOptional,
                    Message(usage, schemaName, property.Key, property.Value),
                    DiagnosticSeverity.Warning,
                    sourceFilePath,
                    Context: $"Schema: {schemaName}",
                    Suggestions: ["Split the request and response schemas to keep it required"],
                    Scope: SpecSuppressions.Schema(schemaName)));
            }
        }

        return diagnostics;
    }

    private static string Message(
        SchemaUsageReachability usage,
        string schemaName,
        string propertyName,
        IOpenApiSchema property)
    {
        var isReadOnly = property.ReadOnly || property.Dereferenced?.ReadOnly == true;
        var isWriteOnly = property.WriteOnly || property.Dereferenced?.WriteOnly == true;
        var name = $"'{schemaName}.{propertyName}'";
        if (isReadOnly && isWriteOnly)
        {
            return $"{name} is listed in 'required' but is both readOnly and writeOnly, so it is generated optional (nullable, = null). Mark it readOnly or writeOnly, not both.";
        }

        var flag = isReadOnly ? "readOnly" : "writeOnly";
        var direction = isReadOnly ? "requests" : "responses";
        var (inRequests, inResponses) = usage.DirectionsOf(schemaName);
        var alsoTheOtherWay = isReadOnly ? inResponses : inRequests;

        // Used in the other direction itself, or through a base it derives from
        var users = new HashSet<string>(isReadOnly ? usage.RequestSchemas : usage.ResponseSchemas, StringComparer.Ordinal);
        var user = usage.SelfAndBases(schemaName).FirstOrDefault(users.Contains)
                   ?? schemaName;
        var where = string.Equals(user, schemaName, StringComparison.Ordinal)
            ? $"'{schemaName}' is used in {direction}{(alsoTheOtherWay ? " too" : string.Empty)}"
            : $"'{schemaName}' derives from '{user}', which is used in {direction}";

        return $"{name} is listed in 'required' but is {flag} and {where}, so it is generated optional (nullable, = null). Split the request and response schemas to keep it required.";
    }

    // The properties a schema declares itself - its own, and those of its inline allOf parts - with their required list;
    // not those of a base it references, which are reported on the base
    private static (List<KeyValuePair<string, IOpenApiSchema>> Properties, HashSet<string> Required) DeclaredProperties(
        OpenApiSchema schema)
    {
        var properties = new List<KeyValuePair<string, IOpenApiSchema>>();
        var required = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Collect(schema, properties, required, seen);
        return (properties, required);
    }

    private static void Collect(
        IOpenApiSchema schema,
        List<KeyValuePair<string, IOpenApiSchema>> properties,
        HashSet<string> required,
        HashSet<string> seen)
    {
        foreach (var property in schema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            if (seen.Add(property.Key))
            {
                properties.Add(property);
            }
        }

        foreach (var name in schema.Required ?? new HashSet<string>(StringComparer.Ordinal))
        {
            required.Add(name);
        }

        foreach (var part in schema.AllOf ?? [])
        {
            if (part is not OpenApiSchemaReference)
            {
                Collect(part, properties, required, seen);
            }
        }
    }
}