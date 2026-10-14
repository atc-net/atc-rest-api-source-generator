namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Reads the <c>x-signalr-hubs</c> document extension: the SignalR hubs an API has beside its HTTP operations.
/// </summary>
/// <remarks>
/// <code>
/// x-signalr-hubs:
///   notifications:
///     url: /hubs/notifications
///     events:                      # server to client
///       - name: SystemNotification
///         payload: '#/components/schemas/SystemNotification'
///       - name: user-activity
///         payload: '#/components/schemas/UserActivityEvent'
///     methods:                     # client to server
///       - name: Subscribe
///         payload: string[]
///       - name: GetStatus
///         returns: '#/components/schemas/HubStatus'
///       - name: GetHistory
///         payload: { type: integer, format: int32 }
///         returns: { type: array, items: { $ref: '#/components/schemas/SystemNotification' } }
/// </code>
/// A payload is written short - a component schema (by its <c>#/components/schemas/</c> reference or its name), a
/// primitive (<c>string</c>, <c>integer</c>, <c>number</c>, <c>boolean</c>), either followed by <c>[]</c> for an
/// array - or as a schema: <c>$ref</c>, <c>type</c> with <c>format</c>, or <c>type: array</c> with <c>items</c>.
/// Without one the event or method carries nothing. An event or a method declared twice is read once.
/// </remarks>
public static class SignalRHubHelper
{
    /// <summary>
    /// The name of the document extension.
    /// </summary>
    public const string ExtensionName = "x-signalr-hubs";

    /// <summary>
    /// The hubs the document declares, in the order they are written; empty without the extension.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The hubs.</returns>
    public static List<SignalRHub> Parse(OpenApiDocument? openApiDoc)
    {
        var hubs = new List<SignalRHub>();
        if (openApiDoc?.Extensions is null ||
            !openApiDoc.Extensions.TryGetValue(ExtensionName, out var extension) ||
            extension is not JsonNodeExtension { Node: JsonObject hubsObject })
        {
            return hubs;
        }

        foreach (var hub in hubsObject)
        {
            if (hub.Value is not JsonObject hubObject || string.IsNullOrWhiteSpace(hub.Key))
            {
                continue;
            }

            var url = hubObject["url"] is JsonValue urlValue && urlValue.TryGetValue<string>(out var text) && text.Length > 0
                ? text
                : "/hubs/" + hub.Key;

            hubs.Add(new SignalRHub(
                hub.Key,
                url,
                Members(openApiDoc, hubObject["events"], withResult: false),
                Members(openApiDoc, hubObject["methods"], withResult: true)));
        }

        return hubs;
    }

    /// <summary>
    /// The component schemas the hubs' events and methods carry.
    /// </summary>
    /// <param name="hubs">The hubs.</param>
    /// <returns>The schema names, each once.</returns>
    public static List<string> PayloadSchemas(IEnumerable<SignalRHub> hubs)
        => hubs
            .SelectMany(h => h.Events.Concat(h.Methods))
            .SelectMany(m => new[] { m.Payload, m.Returns })
            .SelectMany(p => p?.Schemas ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The C# type of each payload schema, fully qualified (<c>global::{Project}.Generated[.{Segment}].Models.{Name}</c>):
    /// the model is in the shared Models namespace or in that of the single path segment that uses it, under its
    /// sanitized name - and, written in full, no type the hub file imports (<c>Task</c>) can stand in for it.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The root namespace of the generated code.</param>
    /// <param name="hubs">The hubs.</param>
    /// <param name="singleModelsNamespace">Whether every model is in one namespace (a single typed client).</param>
    /// <returns>Per schema name, its C# type.</returns>
    public static Dictionary<string, string> ModelTypeNames(
        OpenApiDocument openApiDoc,
        string projectName,
        IEnumerable<SignalRHub> hubs,
        bool singleModelsNamespace = false)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var sharedSchemas = PathSegmentHelper.GetSharedSchemas(openApiDoc);
        var schemaSegments = PathSegmentHelper.GetSchemaSegmentMap(openApiDoc);
        foreach (var schema in PayloadSchemas(hubs))
        {
            var segment = !singleModelsNamespace &&
                          !sharedSchemas.Contains(schema) &&
                          schemaSegments.TryGetValue(schema, out var segments) &&
                          segments.Count == 1
                ? PathSegmentHelper.ResolveEffectivePathSegment(openApiDoc, projectName, segments.First())
                : null;
            names[schema] = $"global::{NamespaceBuilder.ForModels(projectName, segment ?? string.Empty)}.{OpenApiSchemaExtensions.SanitizeSchemaName(schema)}";
        }

        return names;
    }

    private static List<SignalRHubMember> Members(
        OpenApiDocument openApiDoc,
        JsonNode? node,
        bool withResult)
    {
        var members = new List<SignalRHubMember>();
        if (node is not JsonArray array)
        {
            return members;
        }

        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array)
        {
            if (item is not JsonObject member ||
                member["name"] is not JsonValue nameValue ||
                !nameValue.TryGetValue<string>(out var name) ||
                string.IsNullOrWhiteSpace(name) ||
                !taken.Add(name))
            {
                continue;
            }

            members.Add(new SignalRHubMember(
                name,
                Resolve(openApiDoc, Payload(member["payload"])),
                withResult ? Resolve(openApiDoc, Payload(member["returns"])) : null));
        }

        return members;
    }

    private static SignalRHubPayload? Payload(JsonNode? node)
        => node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
                => Short(text.Trim()),
            JsonObject schema => Schema(schema),
            _ => null,
        };

    // "Notice", "#/components/schemas/Notice", "string", "Notice[]"
    private static SignalRHubPayload Short(string text)
    {
        if (text.EndsWith("[]", StringComparison.Ordinal) && text.Length > 2)
        {
            return new SignalRHubPayload(null, "array", null, Short(text.Substring(0, text.Length - 2)));
        }

        if (IsPrimitive(text))
        {
            return new SignalRHubPayload(null, text, null, null);
        }

        var lastSlash = text.LastIndexOf('/');
        return new SignalRHubPayload(
            lastSlash >= 0 && lastSlash < text.Length - 1 ? text.Substring(lastSlash + 1) : text,
            null,
            null,
            null);
    }

    // { $ref: ... }, { type: integer, format: int64 }, { type: array, items: ... }
    private static SignalRHubPayload? Schema(JsonObject schema)
    {
        if (Text(schema["$ref"]) is { } reference)
        {
            return Short(reference);
        }

        var type = Text(schema["type"]);
        if (type == "array")
        {
            return Payload(schema["items"]) is { } items
                ? new SignalRHubPayload(null, "array", null, items)
                : null;
        }

        return type is not null && IsPrimitive(type)
            ? new SignalRHubPayload(null, type, Text(schema["format"]), null)
            : null;
    }

    // A component schema that is generated as no type - an array or primitive alias (Tasks: array of Task) - is the
    // type it names, as everywhere else it is used
    private static SignalRHubPayload? Resolve(
        OpenApiDocument openApiDoc,
        SignalRHubPayload? payload,
        int depth = 0)
    {
        if (payload is null || depth > 16)
        {
            return payload;
        }

        if (payload.Items is not null)
        {
            return payload with { Items = Resolve(openApiDoc, payload.Items, depth + 1) };
        }

        if (payload.SchemaName is null ||
            openApiDoc.Components?.Schemas is not { } schemas ||
            !schemas.TryGetValue(payload.SchemaName, out var schema) ||
            TypeConflictRegistry.IsGeneratedType(schema))
        {
            return payload;
        }

        return FromSchema(openApiDoc, schema, depth + 1) ?? payload;
    }

    private static SignalRHubPayload? FromSchema(
        OpenApiDocument openApiDoc,
        IOpenApiSchema schema,
        int depth)
    {
        if (schema is OpenApiSchemaReference reference)
        {
            return reference.Reference.Id is { } id
                ? Resolve(openApiDoc, new SignalRHubPayload(id, null, null, null), depth)
                : null;
        }

        if (schema is not OpenApiSchema { Type: { } type } inline)
        {
            return null;
        }

        var withoutNull = type & ~JsonSchemaType.Null;
        if (withoutNull == JsonSchemaType.Array)
        {
            return inline.Items is not null && FromSchema(openApiDoc, inline.Items, depth + 1) is { } items
                ? new SignalRHubPayload(null, "array", null, items)
                : null;
        }

        var primitive = withoutNull switch
        {
            JsonSchemaType.String => "string",
            JsonSchemaType.Integer => "integer",
            JsonSchemaType.Number => "number",
            JsonSchemaType.Boolean => "boolean",
            _ => null,
        };
        return primitive is null ? null : new SignalRHubPayload(null, primitive, inline.Format, null);
    }

    private static bool IsPrimitive(string type)
        => type is "string" or "integer" or "number" or "boolean";

    private static string? Text(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}