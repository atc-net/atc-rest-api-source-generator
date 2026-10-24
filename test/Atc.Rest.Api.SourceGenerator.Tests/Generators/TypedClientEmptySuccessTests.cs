namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The TypedClient's Throw style returns the operation's model: an empty success where the operation declares only the
/// model is no model - a <c>JsonException</c> that says so, not a null through the non-nullable return type - while an
/// empty success where it also declares one without content (a 204; several successes are a Strict error, OPR018) is
/// null.
/// </summary>
public sealed class TypedClientEmptySuccessTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Things
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
          /things/{id}/maybe:
            get:
              operationId: findThing
              parameters:
                - { name: id, in: path, required: true, schema: { type: string } }
              responses:
                '200':
                  description: The thing
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
                '204':
                  description: No such thing
        components:
          schemas:
            Thing:
              type: object
              required: [name]
              properties:
                name: { type: string }
        """;

    private const string ProbeSource = """"
        using System.Net;
        using System.Text.Json;

        namespace Things.RuntimeTest;

        public static class EmptyProbe
        {
            public static async Task<string> RunAsync(string method, HttpStatusCode status)
            {
                var assembly = typeof(EmptyProbe).Assembly;
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(method) is not null);
                var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler(status)) { BaseAddress = new Uri("http://stub/") })!;
                var call = clientType.GetMethod(method)!;
                var parameters = Activator.CreateInstance(call.GetParameters()[0].ParameterType, "1");
                try
                {
                    var task = (Task)call.Invoke(client, [parameters, CancellationToken.None])!;
                    await task;
                    var result = task.GetType().GetProperty("Result")!.GetValue(task);
                    return result is null ? "null" : "a model";
                }
                catch (JsonException exception)
                {
                    return "JsonException: " + exception.Message;
                }
            }

            private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                    => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(string.Empty) });
            }
        }
        """";

    [Theory]
    [InlineData("GetThingAsync", HttpStatusCode.OK, "JsonException: The 200 answer has no body, but the operation declares a Thing for it.")]
    [InlineData("FindThingAsync", HttpStatusCode.OK, "null")]
    [InlineData("FindThingAsync", HttpStatusCode.NoContent, "null")]
    public async Task AnEmptySuccess_IsNullOnlyWhereTheOperationDeclaresOneWithoutContent(
        string method,
        HttpStatusCode status,
        string expected)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient", "validateSpecificationStrategy": "Standard" }""", includeMinimalApi: false, Yaml);
        sources.Add(("EmptyProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var actual = await (Task<string>)assembly.GetType("Things.RuntimeTest.EmptyProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [method, status])!;

        Assert.Equal(expected, actual);
    }
}