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
///       - name: SwapCameras          # several arguments, in their order on the wire
///         arguments:
///           - name: cameraId
///             payload: { type: string, format: uuid }
///           - name: direction
///             payload: SwapDirection
/// </code>
/// A payload is written short - a component schema (by its <c>#/components/schemas/</c> reference or its name), a
/// primitive (<c>string</c>, <c>integer</c>, <c>number</c>, <c>boolean</c>), either followed by <c>[]</c> for an
/// array - or as a schema: <c>$ref</c>, <c>type</c> with <c>format</c>, or <c>type: array</c> with <c>items</c>.
/// Without one the event or method carries nothing; <c>payload</c> is the short form of one argument named
/// <c>payload</c>, <c>arguments</c> lists several (at most <see cref="MaxArguments"/>, what SignalR's typed handlers
/// take). An event or a method declared twice is read once. What cannot be read is reported by <see cref="FindProblems"/>.
/// </remarks>
public static class SignalRHubHelper
{
    /// <summary>
    /// The name of the document extension.
    /// </summary>
    public const string ExtensionName = "x-signalr-hubs";

    /// <summary>
    /// The most arguments an event or a method takes: SignalR's typed handlers (<c>HubConnection.On</c>) take eight.
    /// </summary>
    public const int MaxArguments = 8;

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
            .SelectMany(m => m.Arguments.Select(a => a.Payload).Append(m.Returns))
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

    /// <summary>
    /// What the generator cannot read in the document's hubs, each as the place (<c>x-signalr-hubs/{hub}/{kind}/{member}/{field}</c>)
    /// and what is wrong: a member it would otherwise generate without the value it carries.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The problems, in the order they are written.</returns>
    public static List<(string Path, string Message)> FindProblems(
        OpenApiDocument? openApiDoc)
    {
        var problems = new List<(string Path, string Message)>();
        if (openApiDoc?.Extensions is null ||
            !openApiDoc.Extensions.TryGetValue(ExtensionName, out var extension) ||
            extension is not JsonNodeExtension { Node: JsonObject hubsObject })
        {
            return problems;
        }

        const string valueForm = "write a component schema (its #/components/schemas/ reference or its name), a primitive " +
                                 "(string, integer, number, boolean, or { type, format }), or an array of one ([] or { type: array, items }); " +
                                 "an inline object or enum belongs in components/schemas";
        foreach (var hubEntry in hubsObject)
        {
            var hubKey = hubEntry.Key;
            if (hubEntry.Value is not JsonObject hub)
            {
                continue;
            }

            foreach (var kind in new[] { "events", "methods" })
            {
                if (hub[kind] is not JsonArray list)
                {
                    continue;
                }

                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is not JsonObject member)
                    {
                        continue;
                    }

                    var name = Text(member["name"]);
                    var path = $"{ExtensionName}/{hubKey}/{kind}/{name ?? "[" + i.ToString(CultureInfo.InvariantCulture) + "]"}";
                    if (name is null)
                    {
                        problems.Add((path + "/name", $"A member of the {hubKey} hub's {kind} has no name."));
                        continue;
                    }

                    if (member["payload"] is { } payload && Payload(payload) is null)
                    {
                        problems.Add((path + "/payload", $"The payload of {name} (hub {hubKey}) cannot be read: {valueForm}."));
                    }

                    if (member["returns"] is { } returns)
                    {
                        if (kind == "events")
                        {
                            problems.Add((path + "/returns", $"The event {name} (hub {hubKey}) has returns: an event answers nothing - only a method does."));
                        }
                        else if (Payload(returns) is null)
                        {
                            problems.Add((path + "/returns", $"What {name} (hub {hubKey}) returns cannot be read: {valueForm}."));
                        }
                    }

                    if (member["arguments"] is not { } arguments)
                    {
                        continue;
                    }

                    if (member["payload"] is not null)
                    {
                        problems.Add((path + "/arguments", $"{name} (hub {hubKey}) has both payload and arguments: payload is the short form of one argument, use one of them."));
                    }

                    if (arguments is not JsonArray argumentList)
                    {
                        problems.Add((path + "/arguments", $"The arguments of {name} (hub {hubKey}) are no list: write each as {{ name, payload }}, in their order on the wire."));
                        continue;
                    }

                    if (argumentList.Count > MaxArguments)
                    {
                        problems.Add((path + "/arguments", $"{name} (hub {hubKey}) has {argumentList.Count} arguments: SignalR's typed handlers take at most {MaxArguments}; put some of them in a model."));
                    }

                    var names = new HashSet<string>(StringComparer.Ordinal);
                    for (var a = 0; a < argumentList.Count; a++)
                    {
                        var argument = argumentList[a] as JsonObject;
                        var argumentName = argument is null ? null : Text(argument["name"]);
                        var argumentPath = $"{path}/arguments/{argumentName ?? "[" + a.ToString(CultureInfo.InvariantCulture) + "]"}";
                        if (argumentName is null)
                        {
                            problems.Add((argumentPath, $"Argument {a + 1} of {name} (hub {hubKey}) has no name: write each argument as {{ name, payload }}."));
                            continue;
                        }

                        if (!names.Add(argumentName))
                        {
                            problems.Add((argumentPath, $"{name} (hub {hubKey}) has two arguments named {argumentName}."));
                        }

                        if (argument!["payload"] is not { } argumentPayload)
                        {
                            problems.Add((argumentPath + "/payload", $"The argument {argumentName} of {name} (hub {hubKey}) has no payload."));
                        }
                        else if (Payload(argumentPayload) is null)
                        {
                            problems.Add((argumentPath + "/payload", $"The argument {argumentName} of {name} (hub {hubKey}) cannot be read: {valueForm}."));
                        }
                    }
                }
            }
        }

        return problems;
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
                Arguments(openApiDoc, member),
                withResult ? Resolve(openApiDoc, Payload(member["returns"])) : null));
        }

        return members;
    }

    // `arguments`, else the one `payload`, else nothing
    private static List<SignalRHubArgument> Arguments(
        OpenApiDocument openApiDoc,
        JsonObject member)
    {
        if (member["arguments"] is JsonArray list)
        {
            return list
                .OfType<JsonObject>()
                .Select(a => (Name: Text(a["name"]), Payload: Resolve(openApiDoc, Payload(a["payload"]))))
                .Where(a => a.Name is not null && a.Payload is not null)
                .Select(a => new SignalRHubArgument(a.Name!, a.Payload!))
                .ToList();
        }

        return Resolve(openApiDoc, Payload(member["payload"])) is { } payload
            ? [new SignalRHubArgument("payload", payload)]
            : [];
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

        // An inline enum is no plain string: its values would be lost - it belongs in components/schemas
        return type is not null && IsPrimitive(type) && schema["enum"] is null
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