namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A test double of an EndpointPerOperation result for a status the operation does not declare (a gateway's 502, a
/// proxy's 503) is one call: <c>WithStatus(status, problem)</c>, whose problem is what <c>Problem</c> reads - an
/// operation with a <c>default</c> response has its own factory for any status instead. The client's
/// <c>ProblemDetails</c> and <c>ValidationProblemDetails</c> take every member as an optional argument, so a test
/// names only the ones it needs.
/// </summary>
public sealed class EndpointResultAnyStatusTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: AnyStatus
          version: 1.0.0
        paths:
          /things/{id}:
            get:
              operationId: getThing
              parameters:
                - { name: id, in: path, required: true, schema: { type: string } }
              responses:
                '200':
                  description: The thing
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
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
          /others:
            get:
              operationId: listOthers
              responses:
                '200':
                  description: The others
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Thing'
                default:
                  description: Any error
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/Failure'
        components:
          schemas:
            Thing:
              type: object
              required: [id]
              properties:
                id: { type: string }
            Failure:
              type: object
              properties:
                message: { type: string }
        """;

    private const string ProbeSource = """
        using System.Net;
        using Atc.Rest.Client;

        namespace AnyStatus.RuntimeTest;

        public static class AnyStatusProbe
        {
            public static List<string> Run()
            {
                var validation = new ValidationProblemDetails(Errors: new Dictionary<string, string[]> { ["id"] = ["Too long."] });
                return
                [
                    Of(GetThingEndpointResult.WithStatus(HttpStatusCode.BadGateway, new ProblemDetails(Title: "Upstream down"))),
                    Of(GetThingEndpointResult.WithStatus(HttpStatusCode.ServiceUnavailable)),
                    Of(GetThingEndpointResult.WithStatus(HttpStatusCode.UnprocessableEntity, validation)),
                    Of(ListEventsEndpointResult.WithStatus(HttpStatusCode.BadGateway, new ProblemDetails(Title: "Feed down"))),
                    $"default response: {typeof(ListOthersEndpointResult).GetMethod("WithStatus") is null}",
                    $"empty problem: {new ProblemDetails().Title ?? "-"}",
                ];
            }

            private static string Of(EndpointResponse response)
                => response is IEndpointResult result
                    ? $"{(int)response.StatusCode} {response.IsSuccess} {result.Problem?.Title ?? "-"} {result.ValidationProblem?.Errors?.Count ?? 0}"
                    : "no IEndpointResult";
        }
        """;

    [Fact]
    public void AnUndeclaredStatus_IsOneFactoryCall()
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
        sources.Add(("AnyStatusProbe.cs", usings + Environment.NewLine + ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var lines = (List<string>)assembly.GetType("AnyStatus.RuntimeTest.AnyStatusProbe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, null)!;

        Assert.Equal(
            [
                "502 False Upstream down 0",
                "503 False Service Unavailable 0",
                "422 False - 1",
                "502 False Feed down 0",
                "default response: True",
                "empty problem: -",
            ],
            lines);
    }
}