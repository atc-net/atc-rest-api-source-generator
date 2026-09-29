namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A 400 that declares its own model (<c>ApiProblem</c>, no <c>errors</c>) while the generated ValidationFilter writes a
/// ValidationProblemDetails: the EndpointPerOperation client keeps <c>BadRequestContent</c> as the declared model and
/// gives the field errors through <c>BadRequestValidationErrors</c> - the data the TypeScript clients expose as
/// <c>ValidationError.errors</c>.
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    /// <summary>
    /// Three operations with a declared 400: <c>ApiProblem</c> under <c>application/json</c> and under
    /// <c>application/problem+json</c>, and <c>ProblemDetails</c> (already read as a ValidationProblemDetails).
    /// </summary>
    private const string DeclaredBadRequestYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /json-problem/{mode}:
            get:
              operationId: getJsonProblem
              parameters:
                - name: mode
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                '400':
                  description: Bad Request
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/ApiProblem'
          /problem-json/{mode}:
            get:
              operationId: getProblemJson
              parameters:
                - name: mode
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                '400':
                  description: Bad Request
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ApiProblem'
          /pd/{mode}:
            get:
              operationId: getPd
              parameters:
                - name: mode
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                '400':
                  description: Bad Request
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ProblemDetails'
        components:
          schemas:
            ProblemDetails:
              type: object
              properties:
                type:
                  type: string
                title:
                  type: string
                status:
                  type: integer
                detail:
                  type: string
                instance:
                  type: string
            ApiProblem:
              type: object
              properties:
                type:
                  type: string
                title:
                  type: string
                status:
                  type: integer
                detail:
                  type: string
        """;

    /// <summary>
    /// Calls each operation against a stub that answers 400 with the ValidationFilter's body (<c>errors</c>) or with a
    /// plain ApiProblem (<c>plain</c>); one row per call:
    /// <c>{operation}/{mode}: {status} {BadRequestContent type} | errors => {BadRequestValidationErrors as JSON}</c>
    /// (<c>(no property)</c> when the result class has none).
    /// </summary>
    private const string DeclaredBadRequestProbeSource = """"
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated;
        using ErrorBodies.Generated.JsonProblem.Client;
        using ErrorBodies.Generated.JsonProblem.Endpoints.Interfaces;
        using ErrorBodies.Generated.ProblemJson.Client;
        using ErrorBodies.Generated.ProblemJson.Endpoints.Interfaces;
        using ErrorBodies.Generated.Pd.Client;
        using ErrorBodies.Generated.Pd.Endpoints.Interfaces;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            public static async Task<List<string>> RunAsync()
            {
                var services = new ServiceCollection();
                services
                    .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                services.AddErrorBodiesEndpoints();
                using var provider = services.BuildServiceProvider();

                var rows = new List<string>();
                foreach (var mode in new[] { "errors", "plain" })
                {
                    Describe(rows, $"jsonProblem/{mode}", await provider.GetRequiredService<IGetJsonProblemEndpoint>().ExecuteAsync(new GetJsonProblemParameters(mode)));
                    Describe(rows, $"problemJson/{mode}", await provider.GetRequiredService<IGetProblemJsonEndpoint>().ExecuteAsync(new GetProblemJsonParameters(mode)));
                    Describe(rows, $"pd/{mode}", await provider.GetRequiredService<IGetPdEndpoint>().ExecuteAsync(new GetPdParameters(mode)));
                }

                return rows;
            }

            private static void Describe(List<string> rows, string name, EndpointResponse response)
            {
                var type = response.GetType();
                var content = type.GetProperty("BadRequestContent")!.GetValue(response);
                var errorsProperty = type.GetProperty("BadRequestValidationErrors");
                var errors = errorsProperty is null
                    ? "(no property)"
                    : System.Text.Json.JsonSerializer.Serialize(errorsProperty.GetValue(response));
                rows.Add($"{name}: {(int)response.StatusCode} {content?.GetType().Name} | errors => {errors}");
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var mode = request.RequestUri!.AbsolutePath.Trim('/').Split('/')[1];
                    var body = mode == "errors"
                        ? """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Name":["The Name field is required."],"Tag":["Too long.","Not allowed."]}}"""
                        : """{"type":"about:blank","title":"Bad input","status":400,"detail":"no field errors"}""";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/problem+json"),
                    });
                }
            }
        }
        """";

    [Theory]
    [InlineData("ProblemDetails")]
    [InlineData("PlainText")]
    [InlineData("PlainTextOnly")]
    [InlineData("Custom")]
    public async Task DeclaredBadRequestModel_KeepsTheModel_AndGivesTheValidationErrors(
        string errorResponseFormat)
    {
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker(errorResponseFormat), includeMinimalApi: false, DeclaredBadRequestYaml);
        sources.Add(("Probe.cs", DeclaredBadRequestProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var probe = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!;
        var rows = await (Task<List<string>>)probe.GetMethod("RunAsync")!.Invoke(null, [])!;

        const string errors = """{"Name":["The Name field is required."],"Tag":["Too long.","Not allowed."]}""";
        foreach (var operation in (string[])["jsonProblem", "problemJson"])
        {
            // The declared model stays the declared model; the field errors of a validation 400 are typed next to it.
            Assert.Contains($"{operation}/errors: 400 ApiProblem | errors => {errors}", rows, StringComparer.Ordinal);

            // A 400 body without errors: no errors, and nothing throws.
            Assert.Contains($"{operation}/plain: 400 ApiProblem | errors => null", rows, StringComparer.Ordinal);
        }

        // A declared ProblemDetails 400 is read as a ValidationProblemDetails, which holds the errors itself.
        Assert.Contains("pd/errors: 400 ValidationProblemDetails | errors => (no property)", rows, StringComparer.Ordinal);
    }
}