namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Every EndpointPerOperation result has the server's reason for a refusal, whatever the status: <c>Problem</c> (the
/// <c>ProblemDetails</c> of any status that is not a success) and <c>ValidationProblem</c> (when it carries field
/// errors) - code that shows "why" needs no <c>switch</c> over <c>ContentObject</c> and no list of declared statuses.
/// </summary>
public class EndpointResultProblemTests
{
    private const string Yaml = """
        openapi: 3.2.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things/{id}:
            get:
              operationId: getThing
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: The thing
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
                '400':
                  description: Invalid
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ValidationProblemDetails'
                '404':
                  description: No such thing
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ProblemDetails'
          /events:
            get:
              operationId: listEvents
              responses:
                '200':
                  description: Events
                  content:
                    application/jsonl:
                      itemSchema:
                        $ref: '#/components/schemas/Thing'
                '404':
                  description: No feed
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ProblemDetails'
        components:
          schemas:
            Thing:
              type: object
              required: [id]
              properties:
                id:
                  type: string
            ProblemDetails:
              type: object
              properties:
                type: { type: string }
                title: { type: string }
                status: { type: integer }
                detail: { type: string }
                instance: { type: string }
            ValidationProblemDetails:
              type: object
              properties:
                type: { type: string }
                title: { type: string }
                status: { type: integer }
                detail: { type: string }
                instance: { type: string }
                errors:
                  type: object
                  additionalProperties:
                    type: array
                    items:
                      type: string
        """;

    private const string Probe = """
        using System.Net;
        using Atc.Rest.Client;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            public static List<string> Describe()
            {
                return
                [
                    Of("ok", GetThingEndpointResult.Ok(new Thing("1"))),
                    Of("declared 404", GetThingEndpointResult.NotFound(ProblemDetailsFactory.Create(HttpStatusCode.NotFound, "No such thing."))),
                    Of("400 with field errors", Raw(HttpStatusCode.BadRequest, "{\"title\":\"Invalid\",\"status\":400,\"errors\":{\"name\":[\"Too long.\"]}}")),
                    Of("undeclared 409, a problem", Raw(HttpStatusCode.Conflict, "{\"title\":\"Conflict\",\"detail\":\"The name is taken.\"}")),
                    Of("undeclared 503, text", Raw(HttpStatusCode.ServiceUnavailable, "Down for maintenance")),
                    Of("undeclared 502, empty", Raw(HttpStatusCode.BadGateway, string.Empty)),
                    ];
            }

            private static GetThingEndpointResult Raw(HttpStatusCode status, string body)
                => new(new EndpointResponse(
                    isSuccess: false,
                    statusCode: status,
                    content: body,
                    contentObject: null,
                    headers: new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)));

            private static string Of(string name, IGetThingEndpointResult result)
                => Text(name, result.Problem, result.ValidationProblem);

                private static string Text(string name, ProblemDetails? problem, ValidationProblemDetails? validation)
                => $"{name}: {(problem is null ? "no problem" : $"{problem.Status} {problem.Title ?? "-"} / {problem.Detail ?? "-"}")}" +
                   (validation?.Errors is { Count: > 0 } errors ? $" / errors {string.Join(",", errors.Keys)}" : string.Empty);
        }
        """;

    [Fact]
    public void EveryResult_GivesTheProblemOfARefusal()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            """{ "generationMode": "EndpointPerOperation", "validateSpecificationStrategy": "None" }""",
            includeMinimalApi: false,
            Yaml);
        var usings = string.Join(
            Environment.NewLine,
            sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"using {ns};"));
        sources.Add(("Probe.cs", usings + Environment.NewLine + Probe));

        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var described = (List<string>)assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("Describe")!.Invoke(null, [])!;

        Assert.Equal(
            [
                "ok: no problem",
                "declared 404: 404 NotFound / No such thing.",
                "400 with field errors: 400 Invalid / - / errors name",
                "undeclared 409, a problem: 409 Conflict / The name is taken.",
                "undeclared 503, text: 503 ServiceUnavailable / Down for maintenance",
                "undeclared 502, empty: 502 BadGateway / -",
            ],
            described);
    }
}