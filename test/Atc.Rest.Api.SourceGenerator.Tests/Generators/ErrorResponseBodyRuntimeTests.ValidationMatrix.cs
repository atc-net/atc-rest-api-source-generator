// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// End-to-end validation matrix: the generated server runs on Kestrel with handlers that answer 204, and a raw HTTP
/// client sends a valid and an invalid value for every constraint the generator turns into a validation attribute -
/// <c>pattern</c> (on a string, on a string component a property or parameter references, on the items of an array),
/// <c>format: email</c>, <c>minLength</c> / <c>maxLength</c>, <c>minimum</c> / <c>maximum</c> (and their exclusive
/// form), <c>minItems</c> / <c>maxItems</c> and <c>required</c> - in every place a value arrives: the path, the query, a
/// header, a cookie, and a JSON, URL-encoded, multipart and XML body. A valid value reaches the handler (204); an
/// invalid one is a 400 whose <c>errors</c> name the value by its wire name.
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    /// <summary>
    /// The servers the matrix runs against: the key in the report and the host. The <c>MapEndpoints()</c> host has no
    /// validation filter, so it validates nothing (pinned by <c>APlainHostWithAddValidation_DoesNotValidateTheGeneratedModels</c>).
    /// </summary>
    internal static readonly (string Key, string Title)[] ValidationHosts =
    [
        ("validation", "Atc.Rest.MinimalApi host"),
        ("validationRdg", "Request Delegate Generator"),
    ];

    /// <summary>
    /// The cells each host is expected to fail, with the reason: the ratchet of the validation matrix.
    /// </summary>
    internal static readonly Dictionary<string, Dictionary<string, string>> KnownValidationFailures = new(StringComparer.Ordinal);

    /// <summary>
    /// What the validation matrix does not exercise.
    /// </summary>
    internal static readonly string[] ValidationNotExercised =
    [
        "The MapEndpoints() host: it has no validation filter, so a generated model or parameter is not validated there (the wiki says so; a test pins it).",
        "multipleOf, uniqueItems, minProperties / maxProperties and const: the generator emits no attribute for them.",
        "An array in the path, a header or a cookie: the matrix sends arrays in the query and the bodies.",
        "An empty string or an empty array where the wire cannot tell it from no value (the query, a header, a cookie, a form field).",
        "A format: uri path parameter: ASP.NET Core keeps %2F encoded in a route value, so a URL in one path segment does not arrive as it was sent.",
        "Compare, Remote, ValidateNever, CreditCard and Phone: MVC and DataAnnotations attributes with no OpenAPI keyword to generate them from (a phone number is a pattern here).",
        "A request body that is a bare primitive, a dictionary or untyped JSON: it stays [FromBody], so a value that does not convert is the framework's 400 that names nothing (a model, a oneOf, a list and a webhook payload are read by the generated reader).",
    ];

    // The places a value arrives: the column id, its group and its label
    private static readonly (string Id, string Group, string Label)[] ValidationLocations =
    [
        ("path", "Parameter", "path"),
        ("query", "Parameter", "query"),
        ("header", "Parameter", "header"),
        ("cookie", "Parameter", "cookie"),
        ("json", "Body", "JSON"),
        ("json-list", "Body", "JSON list"),
        ("json-oneof", "Body", "JSON oneOf"),
        ("webhook", "Body", "webhook payload"),
        ("form", "Body", "URL-encoded form"),
        ("multipart", "Body", "multipart"),
        ("xml", "Body", "XML"),
    ];

    // The fields: the wire name, its schema, whether it is an array, a valid value for the requests that test another field
    private static readonly (string Key, string Schema, bool IsArray, string[] Default)[] ValidationFields =
    [
        ("code", "{ type: string, pattern: '^[A-Z]{2}\\d{4}$' }", false, ["DK9876"]),
        ("productCode", "{ $ref: '#/components/schemas/ProductCode' }", false, ["DK9876"]),
        ("tags", "{ type: array, maxItems: 3, items: { type: string, pattern: '^[^:]+:.+$' } }", true, ["color:red"]),
        ("codes", "{ type: array, items: { $ref: '#/components/schemas/ProductCode' } }", true, ["DK9876"]),
        ("email", "{ type: string, format: email }", false, ["ada@contoso.com"]),
        ("contact", "{ type: string, format: email, pattern: '^.+@contoso\\.com$' }", false, ["ada@contoso.com"]),
        ("name", "{ type: string, minLength: 2, maxLength: 5 }", false, ["Ada"]),
        ("quantity", "{ type: integer, minimum: 1, maximum: 10 }", false, ["5"]),
        ("price", "{ type: number, minimum: 0.5, exclusiveMaximum: 100 }", false, ["9.5"]),
        ("labels", "{ type: array, minItems: 1, maxItems: 3, items: { type: string } }", true, ["a"]),

        // Common form-field patterns
        ("phone", "{ type: string, pattern: '^\\+?[1-9]\\d{6,14}$' }", false, ["+4512345678"]),
        ("postalCode", "{ type: string, pattern: '^\\d{4}$' }", false, ["8000"]),
        ("countryCode", "{ type: string, pattern: '^[A-Z]{2}$' }", false, ["DK"]),
        ("color", "{ type: string, pattern: '^#[0-9A-Fa-f]{6}$' }", false, ["#1E90FF"]),
        ("slug", "{ type: string, pattern: '^[a-z0-9]+(?:-[a-z0-9]+)*$' }", false, ["summer-sale-2026"]),
        ("time", "{ type: string, pattern: '^([01]\\d|2[0-3]):[0-5]\\d$' }", false, ["08:30"]),
        ("iban", "{ type: string, pattern: '^[A-Z]{2}\\d{2}[A-Z0-9]{11,30}$' }", false, ["DK5000400440116243"]),
        ("version", "{ type: string, pattern: '^\\d+\\.\\d+\\.\\d+$' }", false, ["3.0.0"]),

        // Formats that bind to a typed value: a value that does not parse is the binder's 400
        ("id", "{ type: string, format: uuid }", false, ["3f2504e0-4f89-11d3-9a0c-0305e82c3301"]),
        ("day", "{ type: string, format: date }", false, ["2026-10-21"]),
        ("at", "{ type: string, format: date-time }", false, ["2026-10-21T08:30:00Z"]),
        ("website", "{ type: string, format: uri }", false, ["https://contoso.com/shop"]),
    ];

    // The rows: the group (the constraint), the field, the values sent (null: the field is left out), valid or not
    private static readonly (string Group, string Key, string[]? Sent, bool Valid)[] ValidationRows =
    [
        ("pattern", "code", ["DK9876"], true),
        ("pattern", "code", ["dk9876"], false),
        ("pattern", "code", ["DK98765"], false),
        ("pattern", "code", ["xDK9876"], false),
        ("pattern", "code", [string.Empty], false),
        ("pattern on a referenced string component", "productCode", ["DK9876"], true),
        ("pattern on a referenced string component", "productCode", ["dk9876"], false),
        ("pattern on a referenced string component", "productCode", ["DK98765"], false),
        ("pattern on the items of an array", "tags", ["color:red", "size:xl"], true),
        ("pattern on the items of an array", "tags", ["red"], false),
        ("pattern on the items of an array", "tags", ["color:red", ":xl"], false),
        ("pattern on the items of an array", "tags", ["color:red", string.Empty], false),
        ("maxItems", "tags", ["a:1", "b:2", "c:3", "d:4"], false),
        ("pattern on items that reference a string component", "codes", ["DK9876", "AB1234"], true),
        ("pattern on items that reference a string component", "codes", ["DK9876", "dk1234"], false),
        ("format: email", "email", ["ada@contoso.com"], true),
        ("format: email", "email", ["not-an-email"], false),
        ("format: email and pattern", "contact", ["ada@contoso.com"], true),
        ("format: email and pattern", "contact", ["ada@fabrikam.com"], false),
        ("format: email and pattern", "contact", ["contoso.com"], false),
        ("minLength / maxLength", "name", ["Ab"], true),
        ("minLength / maxLength", "name", ["Abcde"], true),
        ("minLength / maxLength", "name", ["A"], false),
        ("minLength / maxLength", "name", ["Abcdef"], false),
        ("minimum / maximum", "quantity", ["1"], true),
        ("minimum / maximum", "quantity", ["10"], true),
        ("minimum / maximum", "quantity", ["0"], false),
        ("minimum / maximum", "quantity", ["11"], false),
        ("minimum / exclusiveMaximum", "price", ["0.5"], true),
        ("minimum / exclusiveMaximum", "price", ["99.9"], true),
        ("minimum / exclusiveMaximum", "price", ["0.4"], false),
        ("minimum / exclusiveMaximum", "price", ["100"], false),
        ("minItems / maxItems", "labels", ["a", "b", "c"], true),
        ("minItems / maxItems", "labels", [], false),
        ("minItems / maxItems", "labels", ["a", "b", "c", "d"], false),
        ("required", "requiredName", ["Ada"], true),
        ("required", "requiredName", null, false),
        ("pattern: phone number (E.164)", "phone", ["+4512345678"], true),
        ("pattern: phone number (E.164)", "phone", ["12-34-56-78"], false),
        ("pattern: phone number (E.164)", "phone", ["+045123456"], false),
        ("pattern: postal code (four digits)", "postalCode", ["8000"], true),
        ("pattern: postal code (four digits)", "postalCode", ["800"], false),
        ("pattern: postal code (four digits)", "postalCode", ["DK-8000"], false),
        ("pattern: country code (ISO 3166-1 alpha-2)", "countryCode", ["DK"], true),
        ("pattern: country code (ISO 3166-1 alpha-2)", "countryCode", ["dk"], false),
        ("pattern: country code (ISO 3166-1 alpha-2)", "countryCode", ["DNK"], false),
        ("pattern: hex colour", "color", ["#1E90FF"], true),
        ("pattern: hex colour", "color", ["1E90FF"], false),
        ("pattern: hex colour", "color", ["#1E90FG"], false),
        ("pattern: URL slug", "slug", ["summer-sale-2026"], true),
        ("pattern: URL slug", "slug", ["Summer Sale"], false),
        ("pattern: URL slug", "slug", ["sale--2026"], false),
        ("pattern: time of day (24-hour)", "time", ["23:59"], true),
        ("pattern: time of day (24-hour)", "time", ["24:00"], false),
        ("pattern: time of day (24-hour)", "time", ["9:30"], false),
        ("pattern: IBAN shape", "iban", ["DK5000400440116243"], true),
        ("pattern: IBAN shape", "iban", ["DK50 0040 0440 1162 43"], false),
        ("pattern: version (major.minor.patch)", "version", ["3.0.0"], true),
        ("pattern: version (major.minor.patch)", "version", ["v3.0.0"], false),
        ("pattern: version (major.minor.patch)", "version", ["3.0"], false),
        ("format: uuid", "id", ["3f2504e0-4f89-11d3-9a0c-0305e82c3301"], true),
        ("format: uuid", "id", ["not-a-uuid"], false),
        ("format: date", "day", ["2026-10-21"], true),
        ("format: date", "day", ["2026-13-40"], false),
        ("format: date", "day", ["yesterday"], false),
        ("format: date-time", "at", ["2026-10-21T08:30:00Z"], true),
        ("format: date-time", "at", ["2026-10-21T25:30:00Z"], false),
        ("format: date-time", "at", ["soon"], false),
        ("format: uri", "website", ["https://contoso.com/shop"], true),
        ("format: uri", "website", ["not a url"], false),
        ("format: uri", "website", ["contoso.com/shop"], false),
    ];

    // Every scalar is a path parameter too, but a URI: a route value keeps its %2F encoded, so a URL in one path segment
    // does not arrive as it was sent
    private static readonly string[] ValidationPathFields = [.. ValidationFields.Where(f => !f.IsArray && f.Key != "website").Select(f => f.Key)];

    [Theory]
    [InlineData("validation")]
    [InlineData("validationRdg")]
    public async Task EndToEnd_EveryConstraintIsValidated(string hostKey)
    {
        var matrix = await CollectValidationMatrixAsync(hostKey);
        if (Environment.GetEnvironmentVariable("VALIDATION_E2E_DIR") is { Length: > 0 } dumpDirectory)
        {
            await File.WriteAllTextAsync(Path.Combine(dumpDirectory, hostKey + ".json"), matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }

        var known = KnownValidationFailures.GetValueOrDefault(hostKey) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var failures = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in matrix["cases"]!.AsArray())
        {
            foreach (var cell in item!["clients"]!.AsObject())
            {
                if ((string?)cell.Value!["verdict"] is not (VerdictPass or VerdictNotApplicable))
                {
                    failures[$"{(string)item["id"]!} [{cell.Key}]"] = (string)cell.Value["why"]!;
                }
            }
        }

        var unexpected = failures.Where(f => !known.ContainsKey(f.Key)).ToList();
        var fixedCells = known.Keys.Where(k => !failures.ContainsKey(k)).Order(StringComparer.Ordinal).ToList();
        Assert.True(unexpected.Count == 0, $"{unexpected.Count} failing cells:\n" + string.Join("\n", unexpected.Take(150).Select(f => $"{f.Key}: {f.Value}")));
        Assert.True(fixedCells.Count == 0, $"{fixedCells.Count} cells pass now. Remove them from {nameof(KnownValidationFailures)}:\n" + string.Join("\n", fixedCells));
    }

    /// <summary>
    /// The verdict is not blind: a valid value that is rejected, an invalid one that reaches the handler, and a 400 that
    /// names another field each fail.
    /// </summary>
    [Fact]
    public void ValidationVerdicts_FailAMissedOrMisnamedError()
    {
        Assert.Equal(VerdictPass, (string?)ValidationVerdict("code", valid: true, 204, string.Empty)["verdict"]);
        Assert.Equal(VerdictFail, (string?)ValidationVerdict("code", valid: true, 400, """{"errors":{"code":["x"]}}""")["verdict"]);
        Assert.Equal(VerdictPass, (string?)ValidationVerdict("code", valid: false, 400, """{"errors":{"code":["x"]}}""")["verdict"]);
        Assert.Equal(VerdictPass, (string?)ValidationVerdict("tags", valid: false, 400, """{"errors":{"tags[1]":["x"]}}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)ValidationVerdict("code", valid: false, 204, string.Empty)["verdict"]);
        Assert.Equal(VerdictFail, (string?)ValidationVerdict("code", valid: false, 400, """{"errors":{"name":["x"]}}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)ValidationVerdict("code", valid: false, 400, """{"title":"Bad Request"}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)ValidationVerdict("code", valid: false, 500, "boom")["verdict"]);
    }

    internal static string ValidationYaml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("openapi: 3.1.0");
        sb.AppendLine("info:");
        sb.AppendLine("  title: ErrorBodies");
        sb.AppendLine("  version: 1.0.0");
        sb.AppendLine("paths:");

        void Operation(
            string path,
            string method,
            string operationId,
            IEnumerable<string> parameters,
            string? bodyMediaType,
            string bodySchema = "{ $ref: '#/components/schemas/Order' }")
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {path}:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {method}:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"      operationId: {operationId}");
            var list = parameters.ToList();
            if (list.Count > 0)
            {
                sb.AppendLine("      parameters:");
                foreach (var p in list)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"        - {p}");
                }
            }

            if (bodyMediaType is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"      requestBody: {{ required: true, content: {{ {bodyMediaType}: {{ schema: {bodySchema} }} }} }}");
            }

            sb.AppendLine("      responses:");
            sb.AppendLine("        '204': { description: Valid }");
        }

        string Field(string key)
            => ValidationFields.Single(f => f.Key == key).Schema;
        var scalars = ValidationFields.Where(f => !f.IsArray).ToList();
        const string requiredName = "{ type: string }";

        Operation("/path/" + string.Join('/', ValidationPathFields.Select(k => "{" + k + "}")), "get", "byPath", ValidationPathFields.Select(k => $"{{ name: {k}, in: path, required: true, schema: {Field(k)} }}"), null);
        Operation(
            "/query",
            "get",
            "byQuery",
            [.. ValidationFields.Select(f => $"{{ name: {f.Key}, in: query, schema: {f.Schema} }}"), $"{{ name: requiredName, in: query, required: true, schema: {requiredName} }}"],
            null);
        Operation(
            "/header",
            "get",
            "byHeader",
            [.. scalars.Select(f => $"{{ name: {ValidationHeaderName(f.Key)}, in: header, schema: {f.Schema} }}"), $"{{ name: {ValidationHeaderName("requiredName")}, in: header, required: true, schema: {requiredName} }}"],
            null);
        Operation(
            "/cookie",
            "get",
            "byCookie",
            [.. scalars.Select(f => $"{{ name: {f.Key}, in: cookie, schema: {f.Schema} }}"), $"{{ name: requiredName, in: cookie, required: true, schema: {requiredName} }}"],
            null);
        Operation("/json", "post", "byJson", [], "application/json");
        Operation("/json-list", "post", "byJsonList", [], "application/json", "{ type: array, items: { $ref: '#/components/schemas/Order' } }");
        Operation("/json-oneof", "post", "byJsonOneOf", [], "application/json", "{ $ref: '#/components/schemas/AnyOrder' }");
        Operation("/form", "post", "byForm", [], "application/x-www-form-urlencoded");
        Operation("/multipart", "post", "byMultipart", [], "multipart/form-data");
        Operation("/xml", "post", "byXml", [], "application/xml");

        // A webhook the server receives, its payload the same model
        sb.AppendLine("webhooks:");
        sb.AppendLine("  byWebhook:");
        sb.AppendLine("    post:");
        sb.AppendLine("      operationId: byWebhook");
        sb.AppendLine("      requestBody: { required: true, content: { application/json: { schema: { $ref: '#/components/schemas/Order' } } } }");
        sb.AppendLine("      responses:");
        sb.AppendLine("        '204': { description: Valid }");

        sb.AppendLine("components:");
        sb.AppendLine("  schemas:");
        sb.AppendLine("    AnyOrder:");
        sb.AppendLine("      oneOf:");
        sb.AppendLine("        - $ref: '#/components/schemas/Order'");
        sb.AppendLine("        - $ref: '#/components/schemas/GiftCard'");
        sb.AppendLine("      discriminator:");
        sb.AppendLine("        propertyName: kind");
        sb.AppendLine("        mapping:");
        sb.AppendLine("          order: '#/components/schemas/Order'");
        sb.AppendLine("          gift: '#/components/schemas/GiftCard'");
        sb.AppendLine("    GiftCard:");
        sb.AppendLine("      type: object");
        sb.AppendLine("      properties:");
        sb.AppendLine("        kind: { type: string }");
        sb.AppendLine("        amount: { type: integer }");
        sb.AppendLine("    ProductCode:");
        sb.AppendLine("      type: string");
        sb.AppendLine("      description: A product code - two capital letters and four digits.");
        sb.AppendLine("      pattern: '^[A-Z]{2}\\d{4}$'");
        sb.AppendLine("      example: DK9876");
        sb.AppendLine("    Order:");
        sb.AppendLine("      type: object");
        sb.AppendLine("      required: [requiredName]");
        sb.AppendLine("      properties:");
        foreach (var f in ValidationFields)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {f.Key}: {f.Schema}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"        requiredName: {requiredName}");

        // The discriminator of AnyOrder, which a variant declares
        sb.AppendLine("        kind: { type: string }");
        return sb.ToString();
    }

    internal static string ValidationHandlers()
    {
        string[] operations = ["ByPath", "ByQuery", "ByHeader", "ByCookie", "ByJson", "ByJsonList", "ByJsonOneOf", "ByForm", "ByMultipart", "ByXml"];
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("namespace ErrorBodies.RuntimeTest;");
        sb.AppendLine();
        foreach (var operation in operations)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"public sealed class {operation}HandlerImpl : I{operation}Handler");
            sb.AppendLine("{");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    public System.Threading.Tasks.Task<{operation}Result> ExecuteAsync({operation}Parameters p, System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        => System.Threading.Tasks.Task.FromResult({operation}Result.NoContent());");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        const string webhooks = "global::ErrorBodies.Generated.Webhooks";
        sb.AppendLine(CultureInfo.InvariantCulture, $"public sealed class ByWebhookHandlerImpl : {webhooks}.Handlers.IByWebhookWebhookHandler");
        sb.AppendLine("{");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public System.Threading.Tasks.Task<{webhooks}.Results.ByWebhookWebhookResult> ExecuteAsync({webhooks}.Parameters.ByWebhookWebhookParameters p, System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        => System.Threading.Tasks.Task.FromResult({webhooks}.Results.ByWebhookWebhookResult.NoContent());");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class ValidationRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void Register(IServiceCollection services)");
        sb.AppendLine("    {");
        foreach (var operation in operations)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        services.AddScoped<I{operation}Handler, {operation}HandlerImpl>();");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"        services.AddScoped<{webhooks}.Handlers.IByWebhookWebhookHandler, ByWebhookHandlerImpl>();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Each request is disposed by its using declaration.")]
    internal static async Task<JsonObject> CollectValidationMatrixAsync(
        string hostKey)
    {
        var started = DateTimeOffset.Now;
        await using var host = await XmlTestHost.StartAsync(
            ValidationYaml(),
            ValidationHandlers(),
            "ErrorBodies.RuntimeTest.ValidationRegistration",
            configureWithAssembly: (app, assembly) => assembly
                .GetType("ErrorBodies.Generated.Webhooks.Endpoints.WebhookEndpointExtensions", throwOnError: true)!
                .GetMethod("MapErrorBodiesWebhooks")!
                .Invoke(null, [app]),
            useRequestDelegateGenerator: hostKey == "validationRdg");

        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        // What the generator writes for each field - its type and validation attributes - from the same specification
        Assert.True(OpenApiDocumentHelper.TryParseYaml(ValidationYaml(), "validation.yaml", out var document), "The validation specification does not parse.");
        var orderProperties = document!.Components!.Schemas!["Order"].Properties!;

        var cases = new JsonArray();
        foreach (var row in ValidationRows)
        {
            var isRequired = row.Key == "requiredName";
            var generated = orderProperties[row.Key];
            var attributes = generated.GetValidationAttributes(isRequired);
            var written = $"{generated.ToCSharpTypeForModel(isRequired)}{(attributes.Count == 0 ? " (bound by its type)" : " [" + string.Join(", ", attributes) + "]")}";
            var cells = new JsonObject();
            foreach (var location in ValidationLocations)
            {
                if (!ValidationApplies(row.Key, row.Sent, location.Id))
                {
                    cells[location.Id] = new JsonObject
                    {
                        ["verdict"] = VerdictNotApplicable,
                        ["why"] = ValidationNotApplicableReason(row.Key, row.Sent, location.Id),
                        ["summary"] = "n/a",
                    };
                    continue;
                }

                using var request = ValidationRequest(row.Key, row.Sent, location.Id);
                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var wireName = location.Id switch
                {
                    "header" => ValidationHeaderName(row.Key),
                    "json-list" => "[0]." + row.Key,
                    _ => row.Key,
                };
                cells[location.Id] = ValidationVerdict(wireName, row.Valid, (int)response.StatusCode, body);
            }

            var shown = row.Sent is null
                ? "(left out)"
                : row.Sent.Length == 0
                    ? "[]"
                    : string.Join(", ", row.Sent.Select(s => s.Length == 0 ? "\"\"" : s));
            cases.Add(new JsonObject
            {
                ["id"] = $"{row.Group} · {row.Key} = {shown}",
                ["group"] = $"{row.Group}: {(isRequired ? "required: [requiredName]" : ValidationFields.Single(f => f.Key == row.Key).Schema)} → {written}",
                ["request"] = $"{row.Key} = {shown}",
                ["expects"] = row.Valid ? "204: the handler is called" : $"400 naming {row.Key}",
                ["clients"] = cells,
            });
        }

        return new JsonObject
        {
            ["generatedAt"] = started.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            ["durationSeconds"] = System.Math.Round((DateTimeOffset.Now - started).TotalSeconds, 1),
            ["clients"] = new JsonArray([.. ValidationLocations.Select(l => (JsonNode)new JsonObject { ["id"] = l.Id, ["group"] = l.Group, ["label"] = l.Label })]),
            ["cases"] = cases,
        };
    }

    internal static string ValidationHeaderName(string key)
        => "X-" + char.ToUpperInvariant(key[0]) + key[1..];

    /// <summary>
    /// Whether a row can be sent in a place: the path takes the scalars it declares and is always there; an array goes in
    /// the query and the bodies; an empty string or array only where the wire tells it from no value (JSON, XML).
    /// </summary>
    internal static bool ValidationApplies(
        string key,
        string[]? sent,
        string location)
        => ValidationNotApplicableReason(key, sent, location) is null;

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The request owns its content.")]
    internal static HttpRequestMessage ValidationRequest(
        string key,
        string[]? sent,
        string location)
    {
        // The field under test with what the row sends; the required one always valid unless the row leaves it out
        var values = new List<(string Key, string[] Values)>();
        if (sent is not null)
        {
            values.Add((key, sent));
        }

        if (key != "requiredName")
        {
            values.Add(("requiredName", ["Ada"]));
        }
        else if (sent is null)
        {
            // Another field, so the form or part list is not empty
            values.Add(("name", ["Ada"]));
        }

        var pairs = values.SelectMany(v => v.Values.Select(x => new KeyValuePair<string, string>(v.Key, x))).ToList();
        switch (location)
        {
            case "path":
                var segments = ValidationPathFields.Select(k => Uri.EscapeDataString(k == key ? sent![0] : ValidationFields.Single(f => f.Key == k).Default[0]));
                return new HttpRequestMessage(HttpMethod.Get, "/path/" + string.Join('/', segments));
            case "query":
                return new HttpRequestMessage(HttpMethod.Get, "/query?" + string.Join('&', pairs.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}")));
            case "header":
                var header = new HttpRequestMessage(HttpMethod.Get, "/header");
                foreach (var p in pairs)
                {
                    header.Headers.TryAddWithoutValidation(ValidationHeaderName(p.Key), p.Value);
                }

                return header;
            case "cookie":
                var cookie = new HttpRequestMessage(HttpMethod.Get, "/cookie");
                cookie.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", pairs.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}")));
                return cookie;
            case "json" or "json-list" or "json-oneof" or "webhook":
                var json = new JsonObject();
                foreach (var (k, v) in values)
                {
                    json[k] = ValidationFields.FirstOrDefault(f => f.Key == k).IsArray
                        ? new JsonArray([.. v.Select(x => (JsonNode?)JsonValue.Create(x))])
                        : ValidationJsonScalar(k, v[0]);
                }

                // The model alone, as the one item of a list, as the variant of a oneOf (the discriminator first), or as a
                // webhook's payload
                var (url, payload) = location switch
                {
                    "json-list" => ("/json-list", (JsonNode)new JsonArray(json)),
                    "json-oneof" => ("/json-oneof", new JsonObject([new KeyValuePair<string, JsonNode?>("kind", "order"), .. json.Select(p => new KeyValuePair<string, JsonNode?>(p.Key, p.Value?.DeepClone()))])),
                    "webhook" => ("/webhooks/by-webhook", json),
                    _ => ("/json", json),
                };
                return new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
                };
            case "form":
                return new HttpRequestMessage(HttpMethod.Post, "/form") { Content = new FormUrlEncodedContent(pairs) };
            case "multipart":
                var multipart = new MultipartFormDataContent();
                foreach (var p in pairs)
                {
                    multipart.Add(new StringContent(p.Value), p.Key);
                }

                return new HttpRequestMessage(HttpMethod.Post, "/multipart") { Content = multipart };
            default:
                var xml = new XElement("Order", pairs.Select(p => new XElement(p.Key, p.Value))).ToString(SaveOptions.DisableFormatting);
                return new HttpRequestMessage(HttpMethod.Post, "/xml")
                {
                    Content = new StringContent(xml, Encoding.UTF8, "application/xml"),
                };
        }
    }

    /// <summary>
    /// The cell: a valid value reaches the handler (204); an invalid one is a 400 whose <c>errors</c> name it by its wire
    /// name - the name itself, or an item of it (<c>tags[1]</c>).
    /// </summary>
    internal static JsonObject ValidationVerdict(
        string wireName,
        bool valid,
        int status,
        string body)
    {
        var keys = new List<string>();
        string? detail = null;
        if (status == 400)
        {
            try
            {
                var problem = JsonNode.Parse(body);
                if (problem?["errors"] is JsonObject errors)
                {
                    keys.AddRange(errors.Select(e => e.Key));
                }

                detail = problem?["detail"]?.GetValue<string>();
            }
            catch (JsonException)
            {
                // Not a problem body: the verdict says what came back
            }
        }

        var summary = status == 400 && keys.Count > 0
            ? $"400 errors: {string.Join(", ", keys)}"
            : $"{status} {body[..System.Math.Min(body.Length, 200)]}";

        // Named by an errors key, or - a body the reader rejects before validation (an XML element that is required) -
        // by the problem's detail
        var named = keys.Any(k =>
                        string.Equals(k, wireName, StringComparison.OrdinalIgnoreCase) ||
                        k.StartsWith(wireName + "[", StringComparison.OrdinalIgnoreCase)) ||
                    (keys.Count == 0 && detail?.Contains($"'{wireName}'", StringComparison.Ordinal) == true);
        var (verdict, why) = valid
            ? status == 204
                ? (VerdictPass, "valid: the handler was called")
                : (VerdictFail, $"valid, but got {summary}")
            : status == 400 && named
                ? (VerdictPass, $"rejected: {summary}")
                : (VerdictFail, $"expected a 400 naming {wireName}, got {summary}");

        return new JsonObject
        {
            ["verdict"] = verdict,
            ["why"] = why,
            ["summary"] = summary,
        };
    }

    private static string? ValidationNotApplicableReason(
        string key,
        string[]? sent,
        string location)
    {
        var isArray = ValidationFields.FirstOrDefault(f => f.Key == key).IsArray;
        var isEmpty = sent is { Length: 0 } || sent?.Any(s => s.Length == 0) == true;
        if (location == "path")
        {
            return !ValidationPathFields.Contains(key, StringComparer.Ordinal)
                ? "the path declares only scalars, and a path parameter is always required"
                : isEmpty ? "an empty path segment is another route" : null;
        }

        if (isArray && location is "header" or "cookie")
        {
            return "the matrix sends arrays in the query and the bodies";
        }

        if (isEmpty && location is not ("json" or "json-list" or "json-oneof" or "webhook" or "xml"))
        {
            return "the wire cannot tell an empty value from no value here";
        }

        if (isEmpty && isArray && location == "xml")
        {
            return "an empty XML array is no element at all, the same as no value";
        }

        return null;
    }

    private static JsonNode? ValidationJsonScalar(
        string key,
        string value)
        => key switch
        {
            "quantity" => JsonValue.Create(long.Parse(value, CultureInfo.InvariantCulture)),
            "price" => JsonValue.Create(decimal.Parse(value, CultureInfo.InvariantCulture)),
            _ => JsonValue.Create(value),
        };
}