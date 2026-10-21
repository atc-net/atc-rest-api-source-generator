namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A webhook whose payload is JSON Lines is received like an operation's streamed body: the handler reads
/// <c>parameters.Payload</c> record by record, and a record that is not valid is a 400 naming it. The payload may be
/// declared with a <c>schema</c> (3.0 / 3.1) or an OpenAPI 3.2 <c>itemSchema</c>.
/// </summary>
public sealed class JsonLinesWebhookTests
{
    private const string Webhooks = "global::ErrorBodies.Generated.Webhooks";

    private const string HandlerSource = $$""""
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class PingHandlerImpl : IPingHandler
        {
            public System.Threading.Tasks.Task<PingResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(PingResult.NoContent());
        }

        public sealed class ReadingsArrivedHandlerImpl : {{Webhooks}}.Handlers.IOnReadingsArrivedWebhookHandler
        {
            public static int LastCount = -1;

            public async System.Threading.Tasks.Task<{{Webhooks}}.Results.OnReadingsArrivedWebhookResult> ExecuteAsync({{Webhooks}}.Parameters.OnReadingsArrivedWebhookParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                var count = 0;
                await foreach (var reading in parameters.Payload.WithCancellation(cancellationToken))
                {
                    count += reading.DeviceId.Length > 0 ? 1 : 0;
                }

                LastCount = count;
                return {{Webhooks}}.Results.OnReadingsArrivedWebhookResult.Ok();
            }
        }

        public static class WebhookRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IPingHandler, PingHandlerImpl>();
                services.AddScoped<{{Webhooks}}.Handlers.IOnReadingsArrivedWebhookHandler, ReadingsArrivedHandlerImpl>();
            }
        }
        """";

    public static TheoryData<string> Payloads()
        => new()
        {
            // OpenAPI 3.1: a schema under the JSON Lines media type is one record
            "openapi: 3.1.1|schema",

            // OpenAPI 3.2: an itemSchema (a payload declared only that way had no parameters class)
            "openapi: 3.2.0|itemSchema",
        };

    [Theory]
    [MemberData(nameof(Payloads))]
    public void Server_TheWebhookPayloadIsAStreamedRequestBody_AndCompilesWithoutWarnings(string payload)
    {
        var sources = Generate(payload);
        var parameters = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("WebhookParameters", StringComparison.Ordinal)).Select(s => s.Source));

        Assert.Contains("public StreamedRequestBody<Reading> Payload", parameters, StringComparison.Ordinal);
        Assert.DoesNotContain("[FromBody]", parameters, StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSourcesForWarnings(sources, includeMinimalApi: true));
    }

    public static TheoryData<string, bool> PayloadsOnEveryHost()
        => new()
        {
            { "openapi: 3.1.1|schema", false },
            { "openapi: 3.1.1|schema", true },
            { "openapi: 3.2.0|itemSchema", false },
            { "openapi: 3.2.0|itemSchema", true },
        };

    [Theory]
    [MemberData(nameof(PayloadsOnEveryHost))]
    public async Task LiveHost_TheHandlerReadsTheRecords_AndAnInvalidOneIsA400(
        string payload,
        bool rdg)
    {
        await using var host = await XmlTestHost.StartAsync(
            Yaml(payload),
            HandlerSource,
            "ErrorBodies.RuntimeTest.WebhookRegistration",
            configureWithAssembly: (app, assembly) => assembly
                .GetType("ErrorBodies.Generated.Webhooks.Endpoints.WebhookEndpointExtensions", throwOnError: true)!
                .GetMethod("MapErrorBodiesWebhooks")!
                .Invoke(null, [app]),
            useRequestDelegateGenerator: rdg);

        var received = await host.SendRawAsync(HttpMethod.Post, "/webhooks/readings-arrived", accept: null, "application/jsonl", "{\"deviceId\":\"a\",\"value\":1}\n{\"deviceId\":\"b\",\"value\":2}\n");
        Assert.Equal(200, received.Status);
        Assert.Equal(2, (int)host.Assembly.GetType("ErrorBodies.RuntimeTest.ReadingsArrivedHandlerImpl", throwOnError: true)!.GetField("LastCount")!.GetValue(null)!);
        Assert.Equal(
            "400 application/problem+json errors=[1].value",
            await host.PostAsync("/webhooks/readings-arrived", "application/jsonl", "{\"deviceId\":\"a\",\"value\":1}\n{\"deviceId\":\"b\",\"value\":500}\n", accept: null));
        Assert.StartsWith(
            "415 ",
            await host.PostAsync("/webhooks/readings-arrived", "application/json", "[]", accept: null),
            StringComparison.Ordinal);
    }

    private static List<(string HintName, string Source)> Generate(string payload)
        => ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml(payload));

    private static string Yaml(string payload)
    {
        var parts = payload.Split('|');
        return $$"""
            {{parts[0]}}
            info:
              title: ErrorBodies
              version: 1.0.0
            paths:
              /ping:
                get:
                  operationId: ping
                  responses:
                    '204':
                      description: Alive
            webhooks:
              readingsArrived:
                post:
                  operationId: onReadingsArrived
                  requestBody:
                    required: true
                    content:
                      application/jsonl:
                        {{parts[1]}}:
                          $ref: '#/components/schemas/Reading'
                  responses:
                    '200':
                      description: Received
            components:
              schemas:
                Reading:
                  title: Reading
                  type: object
                  required: [deviceId, value]
                  properties:
                    deviceId:
                      type: string
                      minLength: 1
                    value:
                      type: number
                      format: double
                      maximum: 100
            """;
    }
}
