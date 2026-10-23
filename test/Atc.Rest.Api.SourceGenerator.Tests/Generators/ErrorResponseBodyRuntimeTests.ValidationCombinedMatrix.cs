// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// End-to-end matrix of the two kinds of validation together: the DataAnnotations the generator writes from the
/// specification, and FluentValidation validators written by hand in the domain (a synchronous rule, an asynchronous
/// one, and a validator for the whole request - route and body). The generated server
/// runs on Kestrel; each row is a request, each column the way it arrives. A cell passes when the answer is what the
/// row expects: 204 for a valid request, else a 400 with exactly the expected keys, and on each key the expected
/// messages in order - the specification's first.
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    internal const string CombinedFirstNameMessage = "FirstName must start with an uppercase letter.";
    internal const string CombinedTakenMessage = "That last name is taken.";
    internal const string CombinedFrozenMessage = "A member cannot be renamed Frozen.";

    // A message from the specification's DataAnnotations: any text, but not one of the validators'
    internal const string AnyDataAnnotationsMessage = "*";

    /// <summary>
    /// The servers the matrix runs against: the key in the report and the host.
    /// </summary>
    internal static readonly (string Key, string Title)[] ValidationCombinedHosts =
    [
        ("validationCombined", "Atc.Rest.MinimalApi host"),
        ("validationCombinedRdg", "Request Delegate Generator"),
    ];

    /// <summary>
    /// The cells each host is expected to fail, with the reason: the ratchet of the matrix.
    /// </summary>
    internal static readonly Dictionary<string, Dictionary<string, string>> KnownValidationCombinedFailures = new(StringComparer.Ordinal)
    {
        ["validationCombined"] = ListBodyFluentValidationGaps(),
        ["validationCombinedRdg"] = ListBodyFluentValidationGaps(),
    };

    /// <summary>
    /// What the matrix does not exercise.
    /// </summary>
    internal static readonly string[] ValidationCombinedNotExercised =
    [
        "An XML body, a query, a header or a cookie: FluentValidation sees the bound model or parameters record however it arrived, and the Validation matrix covers the DataAnnotations of every place a value arrives.",
        "A validator with a scoped dependency (a DbContext): the generated AddApiValidatorsFromDomain() registers validators as singletons; the wiki shows how to resolve a scoped service per call.",
        "FluentValidation's own options (cascade modes, rule sets, severity): the validator decides; the server only collects its errors.",
        "A validator registered for a list body's own type (the generated JsonRequestBodyList<T>): it runs, but its errors keep the validator's C# path (x[0].FirstName) - the matrix registers what a domain writes, the model's validator.",
    ];

    /// <summary>
    /// A JSON body that is a list of the model: its items get the specification's DataAnnotations (keys <c>[0].email</c>),
    /// but not the model's FluentValidation validator - Atc.Rest.MinimalApi 3.1 looks up a validator for the body's own
    /// type (the generated <c>JsonRequestBodyList&lt;Member&gt;</c>), not for its items; and a validator registered for
    /// that list type answers with its C# path (<c>x[0].FirstName</c>), not the wire name. Reported to Atc.Rest.MinimalApi.
    /// </summary>
    private static Dictionary<string, string> ListBodyFluentValidationGaps()
    {
        const string reason = "Atc.Rest.MinimalApi 3.1: the model's FluentValidation validator does not run for the items of a list body (only DataAnnotations do)";
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["one kind fails · FluentValidation only, a synchronous rule [json-list]"] = reason,
            ["one kind fails · FluentValidation only, an asynchronous rule [json-list]"] = reason,
            ["both fail · on different fields [json-list]"] = reason,
            ["both fail · on the same field: both messages, the specification's first [json-list]"] = reason,
            ["both fail · the asynchronous rule runs although DataAnnotations failed [json-list]"] = reason,
        };
    }

    // The way a request arrives: the column id, its group and its label
    private static readonly (string Id, string Group, string Label)[] ValidationCombinedLocations =
    [
        ("json", "Body", "JSON"),
        ("form", "Body", "URL-encoded form"),
        ("multipart", "Body", "multipart"),
        ("json-list", "Body", "JSON list"),
        ("parameters", "Route and body", "parameters record"),
    ];

    // The rows: the group, the member sent, the columns it applies to (null: every column but the parameters record),
    // and the answer expected - null for 204, else the keys and their messages in order
    private static readonly (string Group, string Id, (string FirstName, string LastName, string Email) Member, string[]? Columns, (string Key, string[] Messages)[]? Errors)[] ValidationCombinedRows =
    [
        ("valid", "every rule passes", ("Ada", "Lovelace", "ada@contoso.com"), null, null),
        ("one kind fails", "DataAnnotations only (format: email)", ("Ada", "Lovelace", "not-an-email"), null, [("email", [AnyDataAnnotationsMessage])]),
        ("one kind fails", "FluentValidation only, a synchronous rule", ("ada", "Lovelace", "ada@contoso.com"), null, [("firstName", [CombinedFirstNameMessage])]),
        ("one kind fails", "FluentValidation only, an asynchronous rule", ("Ada", "Taken", "ada@contoso.com"), null, [("lastName", [CombinedTakenMessage])]),
        ("both fail", "on different fields", ("ada", "Lovelace", "not-an-email"), null, [("email", [AnyDataAnnotationsMessage]), ("firstName", [CombinedFirstNameMessage])]),
        ("both fail", "on the same field: both messages, the specification's first", ("a", "Lovelace", "ada@contoso.com"), null, [("firstName", [AnyDataAnnotationsMessage, CombinedFirstNameMessage])]),
        ("both fail", "the asynchronous rule runs although DataAnnotations failed", ("Ada", "Taken", "not-an-email"), null, [("email", [AnyDataAnnotationsMessage]), ("lastName", [CombinedTakenMessage])]),
        ("the whole request", "a validator of the parameters record (route and body), keyed by the body's field", ("Ada", "Frozen", "ada@contoso.com"), ["parameters"], [("lastName", [CombinedFrozenMessage])]),
    ];

    [Theory]
    [InlineData("validationCombined")]
    [InlineData("validationCombinedRdg")]
    public async Task EndToEnd_DataAnnotationsAndFluentValidationAnswerTogether(
        string hostKey)
    {
        var matrix = await CollectValidationCombinedMatrixAsync(hostKey);

        var known = KnownValidationCombinedFailures.GetValueOrDefault(hostKey) ?? new Dictionary<string, string>(StringComparer.Ordinal);
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
        Assert.True(unexpected.Count == 0, $"{unexpected.Count} failing cells:\n" + string.Join("\n", unexpected.Select(f => $"{f.Key}: {f.Value}")));
        Assert.True(fixedCells.Count == 0, $"{fixedCells.Count} cells pass now. Remove them from {nameof(KnownValidationCombinedFailures)}:\n" + string.Join("\n", fixedCells));
    }

    /// <summary>
    /// The verdict is not blind: a missing key, an extra key, a message out of order, a validator's message where the
    /// specification's was expected, and a 204 where a 400 was expected each fail.
    /// </summary>
    [Fact]
    public void ValidationCombinedVerdicts_FailAWrongAnswer()
    {
        (string Key, string[] Messages)[] sameField = [("firstName", [AnyDataAnnotationsMessage, CombinedFirstNameMessage])];
        Assert.Equal(VerdictPass, (string?)ValidationCombinedVerdict(sameField, 400, """{"errors":{"firstName":["too short", "FirstName must start with an uppercase letter."]}}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)ValidationCombinedVerdict(sameField, 400, """{"errors":{"firstName":["FirstName must start with an uppercase letter.", "too short"]}}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)ValidationCombinedVerdict(sameField, 400, """{"errors":{"firstName":["FirstName must start with an uppercase letter."]}}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)ValidationCombinedVerdict(sameField, 400, """{"errors":{"firstName":["too short", "FirstName must start with an uppercase letter."], "email":["x"]}}""")["verdict"]);
        Assert.Equal(VerdictFail, (string?)ValidationCombinedVerdict(sameField, 204, string.Empty)["verdict"]);
        Assert.Equal(VerdictPass, (string?)ValidationCombinedVerdict(null, 204, string.Empty)["verdict"]);
        Assert.Equal(VerdictFail, (string?)ValidationCombinedVerdict(null, 400, """{"errors":{"email":["x"]}}""")["verdict"]);
    }

    internal static string ValidationCombinedYaml()
        => """
            openapi: 3.1.0
            info:
              title: ErrorBodies
              version: 1.0.0
            paths:
              /members/json:
                post:
                  operationId: createMemberFromJson
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema: { $ref: '#/components/schemas/Member' }
                  responses:
                    '204': { description: Created }
                    '400': { description: Bad request }
              /members/form:
                post:
                  operationId: createMemberFromForm
                  requestBody:
                    required: true
                    content:
                      application/x-www-form-urlencoded:
                        schema: { $ref: '#/components/schemas/Member' }
                  responses:
                    '204': { description: Created }
                    '400': { description: Bad request }
              /members/multipart:
                post:
                  operationId: createMemberFromMultipart
                  requestBody:
                    required: true
                    content:
                      multipart/form-data:
                        schema: { $ref: '#/components/schemas/Member' }
                  responses:
                    '204': { description: Created }
                    '400': { description: Bad request }
              /members/list:
                post:
                  operationId: importMembers
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          type: array
                          items: { $ref: '#/components/schemas/Member' }
                  responses:
                    '204': { description: Imported }
                    '400': { description: Bad request }
              /members/{memberId}:
                put:
                  operationId: updateMember
                  parameters:
                    - name: memberId
                      in: path
                      required: true
                      schema: { type: string, format: uuid }
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema: { $ref: '#/components/schemas/Member' }
                  responses:
                    '204': { description: Updated }
                    '400': { description: Bad request }
            components:
              schemas:
                Member:
                  type: object
                  required: [firstName, lastName, email]
                  properties:
                    firstName: { type: string, minLength: 2, maxLength: 20 }
                    lastName: { type: string, minLength: 2, maxLength: 20 }
                    email: { type: string, format: email }
            """;

    /// <summary>
    /// Stand-ins for the domain project: handlers that answer 204, and the validators - registered as the generated
    /// <c>AddApiValidatorsFromDomain()</c> registers them, as singletons.
    /// </summary>
    internal static string ValidationCombinedDomain()
        => """
            using FluentValidation;
            using Microsoft.Extensions.DependencyInjection;

            namespace ErrorBodies.RuntimeTest;

            public sealed class CreateMemberFromJsonHandlerImpl : ICreateMemberFromJsonHandler
            {
                public System.Threading.Tasks.Task<CreateMemberFromJsonResult> ExecuteAsync(CreateMemberFromJsonParameters p, System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(CreateMemberFromJsonResult.NoContent());
            }

            public sealed class CreateMemberFromFormHandlerImpl : ICreateMemberFromFormHandler
            {
                public System.Threading.Tasks.Task<CreateMemberFromFormResult> ExecuteAsync(CreateMemberFromFormParameters p, System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(CreateMemberFromFormResult.NoContent());
            }

            public sealed class CreateMemberFromMultipartHandlerImpl : ICreateMemberFromMultipartHandler
            {
                public System.Threading.Tasks.Task<CreateMemberFromMultipartResult> ExecuteAsync(CreateMemberFromMultipartParameters p, System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(CreateMemberFromMultipartResult.NoContent());
            }

            public sealed class ImportMembersHandlerImpl : IImportMembersHandler
            {
                public System.Threading.Tasks.Task<ImportMembersResult> ExecuteAsync(ImportMembersParameters p, System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(ImportMembersResult.NoContent());
            }

            public sealed class UpdateMemberHandlerImpl : IUpdateMemberHandler
            {
                public System.Threading.Tasks.Task<UpdateMemberResult> ExecuteAsync(UpdateMemberParameters p, System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(UpdateMemberResult.NoContent());
            }

            // The domain's rules for a member: a synchronous one, and an asynchronous one (a lookup)
            public sealed class MemberValidator : AbstractValidator<Member>
            {
                public MemberValidator()
                {
                    RuleFor(x => x.FirstName)
                        .Must(v => v is null || (v.Length > 0 && char.IsUpper(v[0])))
                        .WithMessage("FirstName must start with an uppercase letter.");

                    RuleFor(x => x.LastName)
                        .MustAsync(async (lastName, cancellationToken) =>
                        {
                            await System.Threading.Tasks.Task.Delay(10, cancellationToken);
                            return lastName != "Taken";
                        })
                        .WithMessage("That last name is taken.");
                }
            }

            // The whole request: the route's memberId and the body together
            public sealed class UpdateMemberParametersValidator : AbstractValidator<UpdateMemberParameters>
            {
                public UpdateMemberParametersValidator()
                    => RuleFor(x => x.Request.LastName)
                        .NotEqual("Frozen")
                        .When(x => x.MemberId != System.Guid.Empty)
                        .WithMessage("A member cannot be renamed Frozen.");
            }

            public static class ValidationCombinedRegistration
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddScoped<ICreateMemberFromJsonHandler, CreateMemberFromJsonHandlerImpl>();
                    services.AddScoped<ICreateMemberFromFormHandler, CreateMemberFromFormHandlerImpl>();
                    services.AddScoped<ICreateMemberFromMultipartHandler, CreateMemberFromMultipartHandlerImpl>();
                    services.AddScoped<IImportMembersHandler, ImportMembersHandlerImpl>();
                    services.AddScoped<IUpdateMemberHandler, UpdateMemberHandlerImpl>();
                    services.AddSingleton<IValidator<Member>, MemberValidator>();
                    services.AddSingleton<IValidator<UpdateMemberParameters>, UpdateMemberParametersValidator>();
                }
            }
            """;

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Each request is disposed by its using declaration.")]
    internal static async Task<JsonObject> CollectValidationCombinedMatrixAsync(
        string hostKey)
    {
        var started = DateTimeOffset.Now;
        await using var host = await XmlTestHost.StartAsync(
            ValidationCombinedYaml(),
            ValidationCombinedDomain(),
            "ErrorBodies.RuntimeTest.ValidationCombinedRegistration",
            useRequestDelegateGenerator: hostKey == "validationCombinedRdg");

        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        var cases = new JsonArray();
        foreach (var row in ValidationCombinedRows)
        {
            var cells = new JsonObject();
            foreach (var location in ValidationCombinedLocations)
            {
                var applies = row.Columns is null
                    ? location.Id != "parameters"
                    : row.Columns.Contains(location.Id, StringComparer.Ordinal);
                if (!applies)
                {
                    cells[location.Id] = new JsonObject
                    {
                        ["verdict"] = VerdictNotApplicable,
                        ["why"] = row.Columns is null
                            ? "The parameters record's own validator has a row of its own; here it has nothing to refuse."
                            : "A validator of the update request's parameters record: only that operation has one.",
                        ["summary"] = "n/a",
                    };
                    continue;
                }

                using var request = ValidationCombinedRequest(location.Id, row.Member);
                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var expected = row.Errors?
                    .Select(e => (location.Id == "json-list" ? "[0]." + e.Key : e.Key, e.Messages))
                    .ToArray();
                cells[location.Id] = ValidationCombinedVerdict(expected, (int)response.StatusCode, body);
            }

            cases.Add(new JsonObject
            {
                ["id"] = $"{row.Group} · {row.Id}",
                ["group"] = row.Group,
                ["request"] = $"firstName = {row.Member.FirstName}, lastName = {row.Member.LastName}, email = {row.Member.Email}",
                ["expects"] = row.Errors is null
                    ? "204: the handler is called"
                    : "400: " + string.Join("; ", row.Errors.Select(e => $"{e.Key} = [{string.Join(", ", e.Messages.Select(m => m == AnyDataAnnotationsMessage ? "the specification's message" : m))}]")),
                ["clients"] = cells,
            });
        }

        return new JsonObject
        {
            ["generatedAt"] = started.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            ["durationSeconds"] = System.Math.Round((DateTimeOffset.Now - started).TotalSeconds, 1),
            ["clients"] = new JsonArray([.. ValidationCombinedLocations.Select(l => (JsonNode)new JsonObject { ["id"] = l.Id, ["group"] = l.Group, ["label"] = l.Label })]),
            ["cases"] = cases,
        };
    }

    /// <summary>
    /// The verdict of one cell: a valid request reaches the handler (204); an invalid one is a 400 with exactly the
    /// expected keys, and on each key the expected messages in order (<c>*</c>: a message of the specification's, any
    /// text but none of the validators').
    /// </summary>
    internal static JsonObject ValidationCombinedVerdict(
        (string Key, string[] Messages)[]? expected,
        int status,
        string body)
    {
        var errors = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        if (status == 400)
        {
            try
            {
                if (JsonNode.Parse(body)?["errors"] is JsonObject problemErrors)
                {
                    foreach (var (key, value) in problemErrors)
                    {
                        errors[key] = value is JsonArray messages ? [.. messages.Select(m => m?.GetValue<string>() ?? string.Empty)] : [];
                    }
                }
            }
            catch (JsonException)
            {
                // Not a problem body: the verdict says what came back
            }
        }

        var summary = status == 400 && errors.Count > 0
            ? "400 " + string.Join("; ", errors.Select(e => $"{e.Key} = [{string.Join(" | ", e.Value)}]"))
            : $"{status} {body[..System.Math.Min(body.Length, 200)]}";

        if (expected is null)
        {
            return status == 204
                ? new JsonObject { ["verdict"] = VerdictPass, ["why"] = "valid: the handler was called", ["summary"] = summary }
                : new JsonObject { ["verdict"] = VerdictFail, ["why"] = $"valid, but got {summary}", ["summary"] = summary };
        }

        string[] validatorMessages = [CombinedFirstNameMessage, CombinedTakenMessage, CombinedFrozenMessage];
        var matches = status == 400 &&
                      errors.Count == expected.Length &&
                      expected.All(e =>
                          errors.TryGetValue(e.Key, out var actual) &&
                          actual.Length == e.Messages.Length &&
                          e.Messages.Zip(actual).All(p => p.First == AnyDataAnnotationsMessage
                              ? !validatorMessages.Contains(p.Second, StringComparer.Ordinal)
                              : string.Equals(p.First, p.Second, StringComparison.Ordinal)));

        var expects = string.Join("; ", expected.Select(e => $"{e.Key} = [{string.Join(" | ", e.Messages)}]"));
        return new JsonObject
        {
            ["verdict"] = matches ? VerdictPass : VerdictFail,
            ["why"] = matches ? $"as expected: {summary}" : $"expected 400 {expects}, got {summary}",
            ["summary"] = summary,
        };
    }

    private static HttpRequestMessage ValidationCombinedRequest(
        string locationId,
        (string FirstName, string LastName, string Email) member)
    {
        string Json()
            => new JsonObject
            {
                ["firstName"] = member.FirstName,
                ["lastName"] = member.LastName,
                ["email"] = member.Email,
            }.ToJsonString();

        var fields = new[] { ("firstName", member.FirstName), ("lastName", member.LastName), ("email", member.Email) };
        return locationId switch
        {
            "json" => new HttpRequestMessage(HttpMethod.Post, "/members/json") { Content = new StringContent(Json(), Encoding.UTF8, "application/json") },
            "json-list" => new HttpRequestMessage(HttpMethod.Post, "/members/list") { Content = new StringContent($"[{Json()}]", Encoding.UTF8, "application/json") },
            "parameters" => new HttpRequestMessage(HttpMethod.Put, "/members/3f2504e0-4f89-11d3-9a0c-0305e82c3301") { Content = new StringContent(Json(), Encoding.UTF8, "application/json") },
            "form" => new HttpRequestMessage(HttpMethod.Post, "/members/form") { Content = new FormUrlEncodedContent(fields.Select(f => KeyValuePair.Create(f.Item1, f.Item2))) },
            "multipart" => new HttpRequestMessage(HttpMethod.Post, "/members/multipart") { Content = Multipart(fields) },
            _ => throw new ArgumentOutOfRangeException(nameof(locationId), locationId, message: null),
        };

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The parts are disposed with the multipart content, which the request disposes.")]
        static MultipartFormDataContent Multipart(
            (string Name, string Value)[] fields)
        {
            var content = new MultipartFormDataContent();
            foreach (var (name, value) in fields)
            {
                content.Add(new StringContent(value), name);
            }

            return content;
        }
    }
}