namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Finds which component schemas a document's requests carry and which its responses carry - directly or through
/// properties, array items, dictionary values and compositions. One generated record serves both directions, so a
/// <c>readOnly</c> property listed in <c>required</c> stays required only when no request carries the schema, and a
/// <c>writeOnly</c> one only when no response carries it (<see cref="KeepsRequired"/>).
/// <para>
/// Requests: the request bodies of every media type (forms too) and the path-item and operation parameters.
/// Responses: the response bodies and the response headers. What a webhook or a callback references counts as both.
/// </para>
/// </summary>
public sealed class SchemaUsageReachability
{
    // Keyed by document identity, like SchemaExtractor's allOf cache: a document is only changed before its first
    // extraction (the generators normalize and flatten it first)
    private static readonly ConditionalWeakTable<OpenApiDocument, SchemaUsageReachability> Cache = new();

    private readonly IDictionary<string, IOpenApiSchema>? components;
    private readonly HashSet<string> requestSchemas;
    private readonly HashSet<string> responseSchemas;

    private SchemaUsageReachability(
        IDictionary<string, IOpenApiSchema>? components,
        HashSet<string> requestSchemas,
        HashSet<string> responseSchemas)
    {
        this.components = components;
        this.requestSchemas = requestSchemas;
        this.responseSchemas = responseSchemas;
    }

    /// <summary>
    /// Gets the names (as in <c>components.schemas</c>) of the schemas a request carries.
    /// </summary>
    public IReadOnlyCollection<string> RequestSchemas => requestSchemas;

    /// <summary>
    /// Gets the names (as in <c>components.schemas</c>) of the schemas a response carries.
    /// </summary>
    public IReadOnlyCollection<string> ResponseSchemas => responseSchemas;

    /// <summary>
    /// The reachability of a document, computed once per document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The reachability.</returns>
    public static SchemaUsageReachability For(OpenApiDocument openApiDoc)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        return Cache.GetValue(openApiDoc, Compute);
    }

    /// <summary>
    /// Computes the reachability of a document, without caching it (the validator sees the document before the
    /// generators flatten it).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The reachability.</returns>
    public static SchemaUsageReachability Compute(OpenApiDocument openApiDoc)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        var requests = new HashSet<string>(StringComparer.Ordinal);
        var responses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pathItem in openApiDoc.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
        {
            AddPathItem(pathItem, requests, responses);
        }

        // A webhook or a callback: the API sends the request and reads the response, so what they reference is used
        // both ways
        foreach (var pathItem in (openApiDoc.Webhooks?.Values ?? Enumerable.Empty<IOpenApiPathItem>()).Concat(CallbackPathItems(openApiDoc)))
        {
            AddPathItem(pathItem, requests, requests);
            AddPathItem(pathItem, responses, responses);
        }

        return new SchemaUsageReachability(openApiDoc.Components?.Schemas, requests, responses);
    }

    /// <summary>
    /// Whether a property is <c>readOnly</c> or <c>writeOnly</c> - itself, or the schema its <c>$ref</c> points at.
    /// </summary>
    /// <param name="property">The property's schema.</param>
    /// <returns><see langword="true"/> for a readOnly or writeOnly property.</returns>
    public static bool IsReadOrWriteOnly(IOpenApiSchema? property)
        => IsReadOnly(property) || IsWriteOnly(property);

    /// <summary>
    /// Whether a <c>readOnly</c> or <c>writeOnly</c> property listed in <c>required</c> stays required in the record of
    /// <paramref name="schemaName"/>: a readOnly one when only responses carry the schema, a writeOnly one when only
    /// requests carry it. Otherwise (used both ways, used nowhere, or both readOnly and writeOnly) it is generated
    /// optional - nullable, with <c>= null</c> - so it can be left out where it does not apply.
    /// <para>
    /// A schema is used where it or any allOf base it derives from is used, so a derived record and its base decide an
    /// inherited property alike (the derived record forwards it to the base's constructor).
    /// </para>
    /// </summary>
    /// <param name="usage">The reachability of the document; <see langword="null"/> keeps nothing required.</param>
    /// <param name="schemaName">The component schema the record is generated from (as in <c>components.schemas</c>).</param>
    /// <param name="property">The property's schema.</param>
    /// <returns><see langword="true"/> when the property stays required.</returns>
    public static bool KeepsRequired(
        SchemaUsageReachability? usage,
        string? schemaName,
        IOpenApiSchema? property)
    {
        var isReadOnly = IsReadOnly(property);
        var isWriteOnly = IsWriteOnly(property);
        if (!isReadOnly && !isWriteOnly)
        {
            return true;
        }

        if (usage is null || string.IsNullOrEmpty(schemaName) || (isReadOnly && isWriteOnly))
        {
            return false;
        }

        var (inRequests, inResponses) = usage.DirectionsOf(schemaName!);
        return isReadOnly
            ? inResponses && !inRequests
            : inRequests && !inResponses;
    }

    /// <summary>
    /// Whether requests and responses carry a schema, or an allOf base it derives from.
    /// </summary>
    /// <param name="schemaName">The component schema (as in <c>components.schemas</c>).</param>
    /// <returns>Whether requests carry it, and whether responses do.</returns>
    public (bool InRequests, bool InResponses) DirectionsOf(string schemaName)
    {
        var inRequests = false;
        var inResponses = false;
        foreach (var name in SelfAndBases(schemaName))
        {
            inRequests |= requestSchemas.Contains(name);
            inResponses |= responseSchemas.Contains(name);
        }

        return (inRequests, inResponses);
    }

    /// <summary>
    /// The schema, then every allOf base it derives from (a <c>$ref</c> in its <c>allOf</c>, and theirs), each once.
    /// </summary>
    /// <param name="schemaName">The component schema (as in <c>components.schemas</c>).</param>
    /// <returns>The schema names.</returns>
    public IEnumerable<string> SelfAndBases(string schemaName)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(schemaName);
        while (pending.Count > 0)
        {
            var name = pending.Dequeue();
            if (!seen.Add(name))
            {
                continue;
            }

            yield return name;

            if (components is not null &&
                components.TryGetValue(name, out var schema) &&
                schema.Dereferenced?.AllOf is { Count: > 0 } allOf)
            {
                foreach (var part in allOf)
                {
                    if (part is OpenApiSchemaReference { ComponentName: { } baseName })
                    {
                        pending.Enqueue(baseName);
                    }
                }
            }
        }
    }

    private static bool IsReadOnly(IOpenApiSchema? property)
        => property is not null && (property.ReadOnly || property.Dereferenced?.ReadOnly == true);

    private static bool IsWriteOnly(IOpenApiSchema? property)
        => property is not null && (property.WriteOnly || property.Dereferenced?.WriteOnly == true);

    private static IEnumerable<IOpenApiPathItem> CallbackPathItems(
        OpenApiDocument openApiDoc)
    {
        var pathItems = (openApiDoc.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
            .Concat(openApiDoc.Webhooks?.Values ?? Enumerable.Empty<IOpenApiPathItem>());
        foreach (var pathItem in pathItems)
        {
            foreach (var operation in pathItem?.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                foreach (var callback in operation?.Callbacks?.Values ?? Enumerable.Empty<IOpenApiCallback>())
                {
                    foreach (var callbackPathItem in callback?.PathItems?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
                    {
                        yield return callbackPathItem;
                    }
                }
            }
        }
    }

    private static void AddPathItem(
        IOpenApiPathItem? pathItem,
        HashSet<string> requests,
        HashSet<string> responses)
    {
        if (pathItem is null)
        {
            return;
        }

        foreach (var parameter in pathItem.Parameters ?? Enumerable.Empty<IOpenApiParameter>())
        {
            AddParameter(parameter, requests);
        }

        foreach (var operation in pathItem.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
        {
            if (operation is null)
            {
                continue;
            }

            foreach (var parameter in operation.Parameters ?? Enumerable.Empty<IOpenApiParameter>())
            {
                AddParameter(parameter, requests);
            }

            AddContent(operation.RequestBody?.Content, requests);

            foreach (var response in operation.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
            {
                AddContent(response?.Content, responses);
                foreach (var header in response?.Headers?.Values ?? Enumerable.Empty<IOpenApiHeader>())
                {
                    Walk(header?.Schema, responses);
                    AddContent(header?.Content, responses);
                }
            }
        }
    }

    private static void AddParameter(
        IOpenApiParameter? parameter,
        HashSet<string> reachable)
    {
        Walk(parameter?.Schema, reachable);
        AddContent(parameter?.Content, reachable);
    }

    private static void AddContent(
        IDictionary<string, IOpenApiMediaType>? content,
        HashSet<string> reachable)
    {
        foreach (var mediaType in content?.Values ?? Enumerable.Empty<IOpenApiMediaType>())
        {
            Walk(mediaType?.Schema, reachable);
        }
    }

    private static void Walk(
        IOpenApiSchema? schema,
        HashSet<string> reachable)
    {
        if (schema is null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference reference)
        {
            if (reference.ComponentName is not { } name || !reachable.Add(name))
            {
                return;
            }

            schema = reference.Target;
            if (schema is null)
            {
                return;
            }
        }

        foreach (var property in schema.Properties?.Values ?? Enumerable.Empty<IOpenApiSchema>())
        {
            Walk(property, reachable);
        }

        Walk(schema.Items, reachable);
        Walk(schema.AdditionalProperties, reachable);

        foreach (var part in (schema.AllOf ?? Enumerable.Empty<IOpenApiSchema>())
                     .Concat(schema.OneOf ?? Enumerable.Empty<IOpenApiSchema>())
                     .Concat(schema.AnyOf ?? Enumerable.Empty<IOpenApiSchema>()))
        {
            Walk(part, reachable);
        }
    }
}