namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A typed client registered with its generated <c>Add…Client</c> extension takes its serializer options from DI: the
/// generated <c>ApiClientJsonOptions</c>, which starts from the client's own defaults (camelCase, enums as their wire
/// values), like ASP.NET Core's <c>JsonOptions</c> on the server. A caller adjusts them with the extension's callback or
/// with <c>services.Configure&lt;ApiClientJsonOptions&gt;</c> - before or after the registration. The extension used to
/// build every client with the fixed defaults, so a consumer whose server writes snake_case could not use it.
/// </summary>
public sealed class TypedClientJsonOptionsTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things:
            post:
              operationId: createThing
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Thing'
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
        components:
          schemas:
            Kind:
              type: string
              enum: [small, large]
            Thing:
              type: object
              properties:
                firstName: { type: string }
                kind:
                  $ref: '#/components/schemas/Kind'
        """;

    private const string ProbeSource = """"
        using System.Net;
        using System.Text.Json;
        using ErrorBodies.Generated;
        using ErrorBodies.Generated.Models;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class JsonOptionsProbe
        {
            public static async Task<string> RunAsync(string how)
            {
                var handler = new StubHandler();
                var services = new ServiceCollection();
                if (how == "configure-before")
                {
                    services.Configure<ApiClientJsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
                }

                var builder = how == "callback"
                    ? services.AddErrorBodiesClient(
                        c => c.BaseAddress = new Uri("http://stub/"),
                        o => o.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower)
                    : services.AddErrorBodiesClient(c => c.BaseAddress = new Uri("http://stub/"));
                builder.ConfigurePrimaryHttpMessageHandler(() => handler);

                var client = services.BuildServiceProvider().GetRequiredService<IErrorBodiesClient>();
                var answer = await client.CreateThingAsync(new CreateThingParameters(new Thing("Ann", Kind.Large)));
                return handler.Body + " -> " + answer.FirstName + "/" + answer.Kind;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                public string? Body { get; private set; }

                protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    Body = await request.Content!.ReadAsStringAsync(cancellationToken);
                    var content = new StringContent(Body);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                }
            }
        }
        """";

    [Theory]
    [InlineData("defaults", """{"firstName":"Ann","kind":"large"} -> Ann/large""")]
    [InlineData("callback", """{"first_name":"Ann","kind":"large"} -> Ann/large""")]
    [InlineData("configure-before", """{"first_name":"Ann","kind":"large"} -> Ann/large""")]
    public async Task TheClientsSerializerOptions_ComeFromDi(
        string how,
        string expected)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml);
        sources.Add(("JsonOptionsProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var outcome = await (Task<string>)assembly.GetType("ErrorBodies.RuntimeTest.JsonOptionsProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [how])!;

        Assert.Equal(expected, outcome);
    }
}