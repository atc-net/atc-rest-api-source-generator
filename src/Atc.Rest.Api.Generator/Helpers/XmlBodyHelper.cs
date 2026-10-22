namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Resolves an XML model body - a request or response body whose only model media type is XML (no JSON one) - and
/// the generated <c>{Model}Xml</c> calls that write and read it. Shared by the server and both C# clients, so every
/// side reads what the others write.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class XmlBodyHelper
{
    /// <param name="content">The request or response content.</param>
    extension(IDictionary<string, IOpenApiMediaType>? content)
    {
        /// <summary>
        /// Gets the XML body of the content when it declares no JSON media type (<see cref="FindXmlOnlyBody"/>).
        /// </summary>
        public XmlBody? XmlOnlyBody
            => FindXmlOnlyBody(content);

        /// <summary>
        /// Gets the XML media type offered beside a JSON one (<see cref="FindXmlAlternative"/>).
        /// </summary>
        public XmlBody? XmlAlternative
            => FindXmlAlternative(content);
    }

    /// <summary>
    /// The generated list type a JSON-only array body is read into.
    /// </summary>
    public const string JsonRequestBodyListTypeName = "JsonRequestBodyList";

    // The polymorphic bases of each document, found once per document
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<OpenApiDocument, HashSet<string>> PolymorphicBases = new();

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
    /// Finds the XML alternative of a JSON model body: the XML media types that carry the same model (or array of it)
    /// as the JSON one, negotiated by <c>Accept</c>.
    /// </summary>
    /// <param name="content">The request or response content.</param>
    /// <returns>The XML body with its media types, or <see langword="null"/> when the body is not offered as XML too.</returns>
    public static XmlBody? FindXmlAlternative(
        IDictionary<string, IOpenApiMediaType>? content)
    {
        if (content is null ||
            content.FirstOrDefault(entry => ResponseBodyClassifier.Classify(entry.Key, entry.Value.Schema) == ResponseBodyKind.Json) is not { Key: not null } json)
        {
            return null;
        }

        var jsonIdentity = BodyIdentity(json.Value.Schema);
        if (jsonIdentity is null)
        {
            return null;
        }

        var xmlMediaTypes = content
            .Where(entry => XmlBodyReachability.IsXmlModelBody(entry.Key, entry.Value.Schema) &&
                            string.Equals(BodyIdentity(entry.Value.Schema), jsonIdentity, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToList();
        if (xmlMediaTypes.Count == 0)
        {
            return null;
        }

        var only = FindXmlOnlyBody(content.Where(entry => string.Equals(entry.Key, xmlMediaTypes[0], StringComparison.Ordinal)).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));
        return only is null ? null : only with { AlternativeMediaTypes = xmlMediaTypes };
    }

    /// <summary>
    /// The XML request body a generated client sends: an XML-only body, or - with <c>preferXml</c> - the XML
    /// alternative of a JSON body.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="preferXml">Whether the client prefers XML for a body offered as JSON and XML.</param>
    /// <returns>The XML body, or <see langword="null"/> when the client sends JSON (or no model body).</returns>
    public static XmlBody? ClientRequestBody(
        OpenApiOperation operation,
        bool preferXml)
    {
        var content = operation?.RequestBody?.Content;
        return FindXmlOnlyBody(content) ?? (preferXml ? FindXmlAlternative(content) : null);
    }

    /// <summary>
    /// The request body a generated server binds by its <c>Content-Type</c>: a model offered as XML - only, or as an
    /// alternative to JSON - is bound by a generated <c>BindAsync</c> on the record, a list of one by a generated
    /// <c>RequestBodyList&lt;T&gt;</c>; a model offered as JSON only by the record's <c>BindAsync</c> too, so a value
    /// that does not parse is a 400 naming the member (<c>[FromBody]</c> answers it with a 400 that names nothing).
    /// </summary>
    /// <param name="content">The request body content.</param>
    /// <returns>The bound body, or <see langword="null"/> when the body is bound otherwise (<c>[FromBody]</c>, a form).</returns>
    public static XmlBody? ServerBoundRequestBody(
        IDictionary<string, IOpenApiMediaType>? content)
        => FindXmlOnlyBody(content) ?? FindXmlAlternative(content) ?? FindJsonOnlyModelBody(content);

    /// <summary>
    /// Finds a request body offered as JSON only (every media type is JSON: <c>application/json</c>, <c>*+json</c>) that
    /// the generated server reads with its own reader: one component model or polymorphic base (its generated
    /// <c>BindAsync</c>), or an array (the generated <c>JsonRequestBodyList&lt;T&gt;</c>).
    /// </summary>
    /// <param name="content">The request body content.</param>
    /// <returns>The body (<see cref="XmlBody.IsList"/> for an array), or <see langword="null"/> when it is none of these.</returns>
    public static XmlBody? FindJsonOnlyModelBody(
        IDictionary<string, IOpenApiMediaType>? content)
    {
        if (content is null || content.Count == 0 ||
            !content.All(entry => ResponseBodyClassifier.Classify(entry.Key, entry.Value.Schema) == ResponseBodyKind.Json))
        {
            return null;
        }

        var identities = content.Values
            .Select(media => media.Schema is OpenApiSchemaReference reference && (IsMappedModel(reference) || IsPolymorphicBase(reference))
                ? reference.Reference?.Id ?? reference.Id
                : null)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (identities.Count == 1 && identities[0] is not null)
        {
            return new XmlBody(content.Keys.First(), IsList: false, ListRootName: null) { JsonOnlyMediaTypes = [.. content.Keys] };
        }

        // An array of anything JSON carries, the same under every media type
        var lists = content.Values
            .Select(media => media.Schema is { } schema && schema.IsOfType(JsonSchemaType.Array) && !schema.HasPrefixItems && schema.Items is not null
                ? schema.ToCSharpType(isRequired: true)
                : null)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return lists.Count == 1 && lists[0] is { } list && list.StartsWith("List<", StringComparison.Ordinal)
            ? new XmlBody(content.Keys.First(), IsList: true, ListRootName: null) { JsonOnlyMediaTypes = [.. content.Keys] }
            : null;
    }

    /// <summary>
    /// The request body of a webhook the generated server reads with its own reader: a JSON-only body
    /// (<see cref="FindJsonOnlyModelBody"/>). A webhook payload offered as XML stays <c>[FromBody]</c>.
    /// </summary>
    /// <param name="operation">The webhook operation.</param>
    /// <returns>The body, or <see langword="null"/>.</returns>
    public static XmlBody? ServerBoundWebhookBody(OpenApiOperation? operation)
        => FindJsonOnlyModelBody(operation?.RequestBody?.Content);

    /// <summary>
    /// The C# type a JSON-only list body is read into: <c>JsonRequestBodyList&lt;T&gt;</c> for the body's
    /// <c>List&lt;T&gt;</c>, nullable as it is.
    /// </summary>
    /// <param name="listType">The body's type (<c>List&lt;T&gt;</c> or <c>List&lt;T&gt;?</c>).</param>
    /// <returns>The type the server binds.</returns>
    public static string JsonListBodyTypeName(string listType)
        => listType is not null && listType.StartsWith("List<", StringComparison.Ordinal)
            ? JsonRequestBodyListTypeName + listType.Substring("List".Length)
            : listType!;

    /// <summary>
    /// Whether any operation or webhook of the document has a JSON-only array body (the server then declares
    /// <c>JsonRequestBodyList&lt;T&gt;</c>).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>True when one does.</returns>
    public static bool HasJsonOnlyListBody(OpenApiDocument openApiDoc)
        => openApiDoc.GetAllOperationsAndWebhooks().Any(o => FindJsonOnlyModelBody(o.RequestBody?.Content) is { IsList: true, IsJsonOnly: true });

    /// <summary>
    /// Whether any operation or webhook of the document has a body the generated server reads itself (so it declares
    /// <c>RequestBodyProblems</c>).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>True when one does.</returns>
    public static bool HasServerBoundBody(OpenApiDocument openApiDoc)
        => (openApiDoc?.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
               .SelectMany(p => p?.Operations?.Values.Where(o => o is not null) ?? Enumerable.Empty<OpenApiOperation>())
               .Any(o => ServerBoundRequestBody(o.RequestBody?.Content) is not null) ||
           (openApiDoc?.Webhooks?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
               .SelectMany(p => p?.Operations?.Values.Where(o => o is not null) ?? Enumerable.Empty<OpenApiOperation>())
               .Any(o => ServerBoundWebhookBody(o) is not null);

    /// <summary>
    /// The models a generated server binds by <c>Content-Type</c>: alone (with whether any operation also accepts them
    /// as JSON), and as the items of a list body.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The schema names and how they are bound.</returns>
    public static Dictionary<string, ServerBoundModel> ServerBoundRequestModels(
        OpenApiDocument openApiDoc)
    {
        var models = new Dictionary<string, ServerBoundModel>(StringComparer.Ordinal);
        var operations = (openApiDoc?.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
            .SelectMany(p => p?.Operations?.Values.Where(o => o is not null) ?? Enumerable.Empty<OpenApiOperation>())
            .Select(o => (Operation: o, Body: ServerBoundRequestBody(o.RequestBody?.Content)))
            .Concat((openApiDoc?.Webhooks?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
                .SelectMany(p => p?.Operations?.Values.Where(o => o is not null) ?? Enumerable.Empty<OpenApiOperation>())
                .Select(o => (Operation: o, Body: ServerBoundWebhookBody(o))));
        foreach (var (operation, bound) in operations)
        {
            var content = operation.RequestBody?.Content;

            // A JSON-only array is read by the generated JsonRequestBodyList<T>: its items need no reader of their own
            if (bound is not { } body || body is { IsList: true, IsJsonOnly: true })
            {
                continue;
            }

            var schema = content![body.MediaType].Schema;
            var reference = body.IsList ? (schema as OpenApiSchema)?.Items as OpenApiSchemaReference : schema as OpenApiSchemaReference;
            if ((reference?.Reference?.Id ?? reference?.Id) is not { } name)
            {
                continue;
            }

            var known = models.TryGetValue(name, out var existing) ? existing : new ServerBoundModel(AcceptsJson: false, BoundAlone: false, BoundInList: false, AcceptsXml: false);
            models[name] = body.IsList
                ? known with { BoundInList = true, AcceptsXml = true }
                : known with
                {
                    BoundAlone = true,
                    AcceptsJson = known.AcceptsJson || body.IsAlternative || body.IsJsonOnly,
                    AcceptsXml = known.AcceptsXml || !body.IsJsonOnly,
                };
        }

        return models;
    }

    /// <summary>
    /// The C# type of a model body: the record's name (the sanitized schema name), or <c>List&lt;T&gt;</c> of it.
    /// </summary>
    /// <param name="content">The body content.</param>
    /// <param name="body">The XML body.</param>
    /// <returns>The type name.</returns>
    public static string ModelTypeName(
        IDictionary<string, IOpenApiMediaType> content,
        XmlBody body,
        TypeConflictRegistry? registry = null)
    {
        // A JSON-only array is the list of what its items are
        if (body is { IsList: true, IsJsonOnly: true })
        {
            return content[body.MediaType].Schema!.ToCSharpType(isRequired: true, registry);
        }

        // A model named like a namespace in scope (Pet under ...Generated.Pet) is written fully qualified
        var name = ItemTypeName(content, body);
        name = registry?.ResolveTypeName(name) ?? name;
        return body.IsList ? $"List<{name}>" : name;
    }

    /// <summary>
    /// The record's name of a model body: the model, or the item model of a list.
    /// </summary>
    /// <param name="content">The body content.</param>
    /// <param name="body">The XML body.</param>
    /// <returns>The type name.</returns>
    public static string ItemTypeName(
        IDictionary<string, IOpenApiMediaType> content,
        XmlBody body)
    {
        var schema = content[body.MediaType].Schema;
        var reference = body.IsList ? (schema as OpenApiSchema)?.Items as OpenApiSchemaReference : schema as OpenApiSchemaReference;
        return OpenApiSchemaExtensions.SanitizeSchemaName(reference?.Reference?.Id ?? reference?.Id ?? "object");
    }

    /// <summary>
    /// The media types of an operation's declared error responses (4xx, 5xx, <c>default</c>).
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The media types, in declaration order.</returns>
    public static IEnumerable<string> ErrorMediaTypes(
        OpenApiOperation operation)
        => (operation?.Responses ?? new OpenApiResponses())
            .Where(entry => entry.Key.Equals("default", StringComparison.OrdinalIgnoreCase) || entry.Key.StartsWith("4", StringComparison.Ordinal) || entry.Key.StartsWith("5", StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Content?.Keys ?? Enumerable.Empty<string>());

    /// <summary>
    /// The <c>Accept</c> header a generated client sends for an operation with an XML answer: for a body offered as
    /// JSON and XML, JSON first (<c>preferXml</c>: XML first); for an XML-only body, its media types. Errors are asked
    /// for as they are read: the declared error media types, else a ProblemDetails (an <c>Accept</c> without it lets a
    /// host decline to write the problem).
    /// </summary>
    /// <param name="bodies">The XML bodies of the operation's success responses.</param>
    /// <param name="errorMediaTypes">The declared error media types.</param>
    /// <param name="preferXml">Whether the client prefers XML for a body offered as JSON and XML.</param>
    /// <returns>The header value, or <see langword="null"/> when no success response has an XML body.</returns>
    public static string? AcceptFor(
        IReadOnlyCollection<XmlBody> bodies,
        IEnumerable<string> errorMediaTypes,
        bool preferXml,
        IReadOnlyList<string>? fileMediaTypes = null)
    {
        var files = fileMediaTypes ?? [];
        if ((bodies is null || bodies.Count == 0) && files.Count == 0)
        {
            return null;
        }

        // A JSON model or a file
        if (bodies is null || bodies.Count == 0)
        {
            return string.Join(", ", new[] { "application/json" }.Concat(files).Concat(errorMediaTypes).Append("application/problem+json").Distinct(StringComparer.OrdinalIgnoreCase));
        }

        var xmlMediaTypes = bodies.SelectMany(b => b.AlternativeMediaTypes ?? [b.MediaType]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (bodies.Any(b => b.IsAlternative))
        {
            return preferXml
                ? string.Join(", ", xmlMediaTypes.Concat(files).Append("application/problem+xml;q=0.9").Append("application/problem+json;q=0.8"))
                : string.Join(", ", new[] { "application/json" }.Concat(files).Append("application/problem+json"));
        }

        return string.Join(", ", xmlMediaTypes.Concat(files).Concat(errorMediaTypes).Append("application/problem+json").Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether a schema is a reference to a model record that gets a generated <c>{Model}Xml</c>: an object (or
    /// allOf) schema, not polymorphic, not an enum, array or paginated generic.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <returns><see langword="true"/> for a mapped model.</returns>
    public static bool IsMappedModel(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference { Target: OpenApiSchema target } &&
           !target.HasPolymorphicComposition &&
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
    /// The method that reads a body from its root element (<c>PetXml.Read</c>, <c>PetXml.ReadList</c>).
    /// </summary>
    /// <param name="typeName">The body's C# type.</param>
    /// <param name="body">The XML body.</param>
    /// <returns>The method group.</returns>
    public static string ReadMethod(
        string typeName,
        XmlBody body)
        => $"{CompanionOf(typeName, body)}.{(body.IsList ? "ReadList" : "Read")}";

    private static string? BodyIdentity(IOpenApiSchema? schema)
    {
        if (schema is OpenApiSchemaReference reference && IsMappedModel(reference))
        {
            return reference.Reference?.Id ?? reference.Id;
        }

        if (schema is OpenApiSchema { Type: { } type } array &&
            (type & JsonSchemaType.Array) == JsonSchemaType.Array &&
            array.Items is OpenApiSchemaReference item &&
            IsMappedModel(item))
        {
            return "[]" + (item.Reference?.Id ?? item.Id);
        }

        return null;
    }

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

    // A component with oneOf/anyOf the generator emits a polymorphic base for (an abstract record)
    private static bool IsPolymorphicBase(OpenApiSchemaReference reference)
        => reference.Target is OpenApiSchema target &&
           target.HasPolymorphicComposition &&
           reference.Reference?.HostDocument is { } document &&
           reference.ComponentName is { } id &&
           PolymorphicBases.GetValue(document, d => new HashSet<string>(PolymorphicTypeExtractor.ExtractPolymorphicConfigs(d)?.Keys ?? Enumerable.Empty<string>(), StringComparer.Ordinal)).Contains(id);
}