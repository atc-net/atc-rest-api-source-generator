namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The endpoint-per-operation client registers through Atc.Rest.Client's one JSON serializer of the app. Its options
/// are composed: every area and every client library adds its configure step, and each step is applied once, in
/// registration order - no registration overrides another's converters or JsonSerializerContext.
/// </summary>
public sealed class ClientSerializerRegistrationTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
          /owners:
            get:
              operationId: listOwners
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
        components:
          schemas:
            Pet:
              type: object
              properties:
                kind:
                  $ref: '#/components/schemas/PetKind'
            PetKind:
              type: string
              enum: [cat, dog]
        """;

    private const string ProbeSource = """"
        using System.Text.Json;
        using Atc.Rest.Client.Options;
        using Atc.Rest.Client.Serialization;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class RegistrationProbe
        {
            // Returns how often each configure step ran and how many of the generated enum converters the options hold
            public static string Run(string scenario)
            {
                var assembly = typeof(RegistrationProbe).Assembly;
                var methods = assembly.GetTypes().SelectMany(t => t.GetMethods()).ToList();
                var services = new ServiceCollection();
                var first = 0;
                var second = 0;
                Action<JsonSerializerOptions> firstStep = _ => first++;
                Action<JsonSerializerOptions> secondStep = _ => second++;

                switch (scenario)
                {
                    case "aggregate":
                        methods.Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, firstStep]);
                        break;
                    case "areas":
                        methods.Single(m => m.Name == "AddPetsEndpoints").Invoke(null, [services, firstStep]);
                        methods.Single(m => m.Name == "AddOwnersEndpoints").Invoke(null, [services, secondStep]);
                        break;
                    case "another library first":
                        services.ConfigureAtcRestClientJsonOptions(secondStep);
                        methods.Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, firstStep]);
                        break;
                }

                var converters = 0;
                services.ConfigureAtcRestClientJsonOptions(o => converters = o.Converters.Count(c => c.GetType().Name == "EnumWireConverterFactory"));
                _ = services.BuildServiceProvider().GetRequiredService<IContractSerializer>();

                return $"first={first} second={second} enumConverters={converters}";
            }
        }
        """";

    [Theory]
    [InlineData("aggregate", "first=1 second=0 enumConverters=1")]
    [InlineData("areas", "first=1 second=1 enumConverters=1")]
    [InlineData("another library first", "first=1 second=1 enumConverters=1")]
    public void EveryConfigureStep_ReachesTheOneSerializer_Once(
        string scenario,
        string expected)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);
        sources.Add(("RegistrationProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var actual = (string)assembly.GetType("ErrorBodies.RuntimeTest.RegistrationProbe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [scenario])!;

        Assert.Equal(expected, actual);
    }
}