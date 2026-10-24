namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The report's OpenAPI constructs tab: which constructs of OpenAPI (and which of Atc's <c>x-</c> extensions) each
/// scenario specification under <c>test/Scenarios</c> uses - read from the specifications themselves at report time, so
/// it is never stale - and which construct no scenario uses.
/// </summary>
public sealed partial class ErrorResponseBodyRuntimeTests
{
    private static readonly (string Group, string Key, string Label)[] OpenApiConstructs =
    [
        ("Version", "v3.0", "OpenAPI 3.0"),
        ("Version", "v3.1", "OpenAPI 3.1"),
        ("Version", "v3.2", "OpenAPI 3.2"),
        ("Schemas", "allOf", "allOf (composition, inheritance)"),
        ("Schemas", "oneOf", "oneOf (a choice)"),
        ("Schemas", "anyOf", "anyOf"),
        ("Schemas", "discriminator", "discriminator (polymorphism)"),
        ("Schemas", "not", "not"),
        ("Schemas", "enum-string", "string enum"),
        ("Schemas", "enum-integer", "integer enum"),
        ("Schemas", "const", "const"),
        ("Schemas", "type-array", "type as an array (3.1: [string, null])"),
        ("Schemas", "nullable", "nullable (3.0) or null in the type"),
        ("Schemas", "prefixItems", "prefixItems (a tuple)"),
        ("Schemas", "dictionary", "additionalProperties (a dictionary)"),
        ("Schemas", "readOnly", "readOnly"),
        ("Schemas", "writeOnly", "writeOnly"),
        ("Schemas", "default", "default value"),
        ("Schemas", "pattern", "pattern"),
        ("Schemas", "length", "minLength / maxLength"),
        ("Schemas", "range", "minimum / maximum"),
        ("Schemas", "items-limit", "minItems / maxItems"),
        ("Schemas", "format-date", "format: date / date-time"),
        ("Schemas", "format-uuid", "format: uuid"),
        ("Schemas", "format-binary", "format: binary / byte"),
        ("Schemas", "format-uri", "format: uri / email"),
        ("Schemas", "deprecated-schema", "deprecated schema"),
        ("Parameters", "in-path", "path parameter"),
        ("Parameters", "in-query", "query parameter"),
        ("Parameters", "in-header", "header parameter"),
        ("Parameters", "in-cookie", "cookie parameter"),
        ("Parameters", "in-querystring", "querystring parameter (3.2)"),
        ("Parameters", "array-param", "array parameter"),
        ("Parameters", "object-param", "object parameter"),
        ("Parameters", "style-form-noexplode", "style: form, explode: false"),
        ("Parameters", "style-deepObject", "style: deepObject"),
        ("Parameters", "style-delimited", "style: pipeDelimited / spaceDelimited"),
        ("Parameters", "style-label-matrix", "style: label / matrix"),
        ("Parameters", "path-level", "path-level parameters"),
        ("Parameters", "param-ref", "$ref to a component parameter"),
        ("Request bodies", "body-json", "application/json"),
        ("Request bodies", "body-json-other", "another JSON media type (+json)"),
        ("Request bodies", "body-multipart", "multipart/form-data"),
        ("Request bodies", "body-urlencoded", "application/x-www-form-urlencoded"),
        ("Request bodies", "body-binary", "a raw file (octet-stream, an image, a PDF)"),
        ("Request bodies", "body-xml", "XML"),
        ("Request bodies", "body-text", "text/plain"),
        ("Request bodies", "body-stream", "a stream of records (JSON Lines, JSON Text Sequences)"),
        ("Responses", "status-range", "a status range (2XX)"),
        ("Responses", "status-default", "default response"),
        ("Responses", "status-204", "204 No Content"),
        ("Responses", "multiple-2xx", "several success statuses"),
        ("Responses", "response-headers", "response headers"),
        ("Responses", "response-sse", "Server-Sent Events (text/event-stream)"),
        ("Responses", "response-file", "a file download"),
        ("Responses", "response-xml", "an XML response"),
        ("Responses", "response-text", "a text response"),
        ("Responses", "response-json-other", "another JSON media type (+json)"),
        ("Responses", "response-links", "links"),
        ("Operations", "callbacks", "callbacks"),
        ("Operations", "webhooks", "webhooks"),
        ("Operations", "op-deprecated", "deprecated operation"),
        ("Operations", "op-no-id", "an operation without operationId"),
        ("Operations", "method-query", "the QUERY method (3.2)"),
        ("Operations", "method-other", "PATCH / HEAD / OPTIONS / TRACE"),
        ("Operations", "tags", "tags"),
        ("Servers & security", "servers-multiple", "several servers"),
        ("Servers & security", "server-variables", "server variables"),
        ("Servers & security", "sec-apikey", "security: apiKey"),
        ("Servers & security", "sec-bearer", "security: http bearer"),
        ("Servers & security", "sec-basic", "security: http basic"),
        ("Servers & security", "sec-oauth2", "security: oauth2"),
        ("Servers & security", "sec-oidc", "security: openIdConnect"),
        ("Servers & security", "sec-mtls", "security: mutualTLS"),
        ("Servers & security", "sec-anonymous", "an operation open to anyone (security: [])"),
    ];

    /// <summary>
    /// Constructs the generator rejects with an error under every strategy, so no scenario can use them: the report
    /// names the rule instead of a gap, and the rule's own tests cover them.
    /// </summary>
    private static readonly Dictionary<string, string> RejectedOpenApiConstructs = new(StringComparer.Ordinal)
    {
        ["style-label-matrix"] = RuleIdentifiers.ParameterValueNotBindable,
    };

    /// <summary>
    /// Every OpenAPI construct the report lists, and every Atc extension the generator reads, is used by a scenario
    /// specification - so each is generated, compiled and compared with its snapshot - unless the generator rejects it.
    /// </summary>
    [Fact]
    public void EveryOpenApiConstruct_IsUsedByAScenario_OrRejected()
    {
        var data = CollectOpenApiConstructs(FindRepositoryRoot());
        var used = data["scenarios"]!.AsArray()
            .SelectMany(s => s!["constructs"]!.AsArray().Select(c => (string)c!))
            .ToHashSet(StringComparer.Ordinal);
        var gaps = data["constructs"]!.AsArray()
            .Select(c => c!.AsObject())
            .Where(c => c["rejected"] is null && !used.Contains((string)c["key"]!))
            .Select(c => (string)c["key"]!)
            .ToList();

        Assert.True(gaps.Count == 0, $"Not used by any scenario: {string.Join(", ", gaps)}");
    }

    private static JsonObject CollectOpenApiConstructs(string root)
    {
        var scenariosRoot = Path.Combine(root, "test", "Scenarios");

        // The Atc extensions the generator reads: every "x-..." literal in the libraries
        var extensions = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains($"{Path.DirectorySeparatorChar}Atc.OpenApi{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                        f.Contains($"{Path.DirectorySeparatorChar}Atc.Rest.Api.Generator{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), "\"(?<x>x-[a-z0-9]+(?:-[a-z0-9]+)*)\"", System.Text.RegularExpressions.RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Select(m => m.Groups["x"].Value))
            .Where(x => !x.StartsWith("x-correlation", StringComparison.Ordinal) && !x.StartsWith("x-swagger", StringComparison.Ordinal))

            // the parser's own note on a document (its OpenAPI version), never written in a specification
            .Where(x => !string.Equals(x, OpenApiDocumentExtensions.SpecVersionMetadataKey, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var scenarios = new JsonArray();
        foreach (var directory in Directory.GetDirectories(scenariosRoot).OrderBy(d => d, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(directory);
            var specPath = Path.Combine(directory, name + ".yaml");
            if (!File.Exists(specPath))
            {
                continue;
            }

            var text = File.ReadAllText(specPath);
            var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(text, $"constructs/{name}.yaml");
            var found = document is null ? new HashSet<string>(StringComparer.Ordinal) : ConstructsOf(document, text);
            foreach (var extension in extensions.Where(x => System.Text.RegularExpressions.Regex.IsMatch(text, @"(?m)^\s*-?\s*" + System.Text.RegularExpressions.Regex.Escape(x) + @"\s*:", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1))))
            {
                found.Add(extension);
            }

            scenarios.Add(new JsonObject
            {
                ["name"] = name,
                ["constructs"] = new JsonArray([.. found.OrderBy(c => c, StringComparer.Ordinal).Select(c => (JsonNode)c)]),
            });
        }

        return new JsonObject
        {
            ["constructs"] = new JsonArray([.. OpenApiConstructs
                .Select(c => (JsonNode)new JsonObject
                {
                    ["group"] = c.Group,
                    ["key"] = c.Key,
                    ["label"] = c.Label,
                    ["rejected"] = RejectedOpenApiConstructs.TryGetValue(c.Key, out var rule) ? rule : null,
                })
                .Concat(extensions.Select(x => (JsonNode)new JsonObject { ["group"] = "Atc extensions", ["key"] = x, ["label"] = x }))]),
            ["scenarios"] = scenarios,
        };
    }

    private static HashSet<string> ConstructsOf(
        OpenApiDocument document,
        string text)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var version = System.Text.RegularExpressions.Regex.Match(text, @"(?m)^openapi:\s*['""]?(?<v>3\.\d)", System.Text.RegularExpressions.RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        if (version.Success)
        {
            found.Add("v" + version.Groups["v"].Value);
        }

        void AddIf(
            bool condition,
            string key)
        {
            if (condition)
            {
                found.Add(key);
            }
        }

        var visited = new HashSet<OpenApiSchema>(ReferenceEqualityComparer.Instance);
        void Schema(IOpenApiSchema? schema)
        {
            if (schema is not OpenApiSchema s || !visited.Add(s))
            {
                return;
            }

            AddIf(s.AllOf is { Count: > 0 }, "allOf");
            AddIf(s.OneOf is { Count: > 0 }, "oneOf");
            AddIf(s.AnyOf is { Count: > 0 }, "anyOf");
            AddIf(s.Discriminator is not null, "discriminator");
            AddIf(s.Not is not null, "not");
            AddIf(s.Enum is { Count: > 0 }, s.Type?.HasFlag(JsonSchemaType.Integer) == true ? "enum-integer" : "enum-string");
            AddIf(s.Const is not null, "const");
            AddIf(s.Type?.HasFlag(JsonSchemaType.Null) == true, "nullable");
            AddIf(s.HasPrefixItems, "prefixItems");
            AddIf(s.AdditionalProperties is not null, "dictionary");
            AddIf(s.ReadOnly, "readOnly");
            AddIf(s.WriteOnly, "writeOnly");
            AddIf(s.Default is not null, "default");
            AddIf(!string.IsNullOrEmpty(s.Pattern), "pattern");
            AddIf(s.MinLength is not null || s.MaxLength is not null, "length");
            AddIf(!string.IsNullOrEmpty(s.Minimum) || !string.IsNullOrEmpty(s.Maximum) || !string.IsNullOrEmpty(s.ExclusiveMinimum) || !string.IsNullOrEmpty(s.ExclusiveMaximum), "range");
            AddIf(s.MinItems is not null || s.MaxItems is not null, "items-limit");
            AddIf(s.Deprecated, "deprecated-schema");
            AddIf(s.Format is "date" or "date-time", "format-date");
            AddIf(s.Format is "uuid", "format-uuid");
            AddIf(s.Format is "binary" or "byte", "format-binary");
            AddIf(s.Format is "uri" or "email", "format-uri");

            foreach (var child in (s.AllOf ?? []).Concat(s.OneOf ?? []).Concat(s.AnyOf ?? []).Concat((s.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)).Values))
            {
                Schema(child);
            }

            Schema(s.Items);
            Schema(s.AdditionalProperties);
            Schema(s.Not);
        }

        void Content(
            IDictionary<string, IOpenApiMediaType>? content,
            bool request)
        {
            foreach (var (mediaType, media) in content ?? new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal))
            {
                var type = mediaType.Split(';')[0].Trim().ToLowerInvariant();
                var kind = type switch
                {
                    "application/json" => "json",
                    "multipart/form-data" => "multipart",
                    "application/x-www-form-urlencoded" => "urlencoded",
                    "text/event-stream" => "sse",
                    "text/plain" => "text",
                    "application/jsonl" or "application/x-ndjson" or "application/json-seq" => "stream",
                    _ when type.EndsWith("+json", StringComparison.Ordinal) => "json-other",
                    _ when type.EndsWith("/xml", StringComparison.Ordinal) || type.EndsWith("+xml", StringComparison.Ordinal) => "xml",
                    _ when type.StartsWith("text/", StringComparison.Ordinal) => "text",
                    _ => "binary",
                };
                if (request)
                {
                    found.Add("body-" + kind);
                }
                else
                {
                    found.Add(kind switch { "sse" => "response-sse", "xml" => "response-xml", "text" => "response-text", "binary" => "response-file", "json-other" => "response-json-other", _ => "response-json" });
                }

                Schema(media.Schema);
            }
        }

        void Parameters(IEnumerable<IOpenApiParameter>? parameters)
        {
            foreach (var reference in parameters ?? [])
            {
                if (reference is OpenApiParameterReference)
                {
                    found.Add("param-ref");
                }

                if (reference.Resolve().Parameter is not { } p)
                {
                    continue;
                }

                found.Add(p.In switch
                {
                    ParameterLocation.Path => "in-path",
                    ParameterLocation.Header => "in-header",
                    ParameterLocation.Cookie => "in-cookie",
                    ParameterLocation.QueryString => "in-querystring",
                    _ => "in-query",
                });
                var schema = p.Schema.Dereferenced;
                AddIf(schema?.Type?.HasFlag(JsonSchemaType.Array) == true, "array-param");
                AddIf(schema?.Type?.HasFlag(JsonSchemaType.Object) == true || schema?.Properties is { Count: > 0 }, "object-param");
                AddIf(p.Style == ParameterStyle.Form && !p.Explode, "style-form-noexplode");
                AddIf(p.Style == ParameterStyle.DeepObject, "style-deepObject");
                AddIf(p.Style is ParameterStyle.PipeDelimited or ParameterStyle.SpaceDelimited, "style-delimited");
                AddIf(p.Style is ParameterStyle.Label or ParameterStyle.Matrix, "style-label-matrix");

                Schema(p.Schema);
                Content(p.Content, request: true);
            }
        }

        void Operations(IOpenApiPathItem? item)
        {
            if (item?.Parameters is { Count: > 0 })
            {
                found.Add("path-level");
            }

            Parameters(item?.Parameters);
            foreach (var (method, operation) in item?.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
            {
                var verb = method.Method.ToUpperInvariant();
                AddIf(verb == "QUERY", "method-query");
                AddIf(verb is "PATCH" or "HEAD" or "OPTIONS" or "TRACE", "method-other");
                AddIf(operation.Deprecated, "op-deprecated");
                AddIf(string.IsNullOrEmpty(operation.OperationId), "op-no-id");
                AddIf(operation.Tags is { Count: > 0 }, "tags");
                AddIf(operation.Callbacks is { Count: > 0 }, "callbacks");
                AddIf(operation.Security is { Count: 0 }, "sec-anonymous");
                Parameters(operation.Parameters);
                if (operation.RequestBody is { } body)
                {
                    Content(body.Content, request: true);
                }

                var successes = 0;
                foreach (var (status, response) in operation.Responses ?? new OpenApiResponses())
                {
                    AddIf(status.Length == 3 && status.EndsWith("XX", StringComparison.OrdinalIgnoreCase), "status-range");
                    AddIf(status == "default", "status-default");
                    AddIf(status == "204", "status-204");
                    if (status.StartsWith('2'))
                    {
                        successes++;
                    }

                    AddIf(response.Headers is { Count: > 0 }, "response-headers");
                    AddIf(response.Links is { Count: > 0 }, "response-links");
                    Content(response.Content, request: false);
                }

                AddIf(successes > 1, "multiple-2xx");
            }
        }

        foreach (var item in (document.Paths ?? new OpenApiPaths()).Values)
        {
            Operations(item);
        }

        if (document.Webhooks is { Count: > 0 } webhooks)
        {
            found.Add("webhooks");
            foreach (var item in webhooks.Values)
            {
                Operations(item);
            }
        }

        foreach (var schema in (document.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)).Values)
        {
            Schema(schema);
        }

        // the parsed model reads type: [string, null] as the same flags as 3.0's nullable: true, so the text tells them apart
        AddIf(Regex.IsMatch(text, @"(?m)^\s*type:\s*\[", RegexOptions.None, TimeSpan.FromSeconds(1)), "type-array");
        AddIf(document.Servers is { Count: > 1 }, "servers-multiple");
        AddIf(document.Servers?.Any(s => s.Variables is { Count: > 0 }) == true, "server-variables");
        foreach (var scheme in (document.Components?.SecuritySchemes ?? new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal)).Values)
        {
            found.Add(scheme.Type switch
            {
                SecuritySchemeType.ApiKey => "sec-apikey",
                SecuritySchemeType.OAuth2 => "sec-oauth2",
                SecuritySchemeType.OpenIdConnect => "sec-oidc",
                SecuritySchemeType.MutualTLS => "sec-mtls",
                _ => string.Equals(scheme.Scheme, "basic", StringComparison.OrdinalIgnoreCase) ? "sec-basic" : "sec-bearer",
            });
        }

        found.Remove("response-json");
        return found;
    }
}