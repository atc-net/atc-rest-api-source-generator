namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A component schema no operation uses - the payload of an event, a message, a callback - is part of the contract
/// all the same: every generator emits it, so a server and its clients share one type for what travels beside the
/// HTTP operations (a SignalR hub, a queue).
/// </summary>
public class UnusedComponentSchemaTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Cameras
          version: 1.0.0
        paths:
          /cameras:
            get:
              operationId: listCameras
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Camera'
        components:
          schemas:
            Camera:
              type: object
              required: [id]
              properties:
                id: { type: string }
            MotionDetected:
              type: object
              description: Sent over the hub when a camera sees motion; no operation returns it
              required: [cameraId, level]
              properties:
                cameraId: { type: string }
                level: { $ref: '#/components/schemas/MotionLevel' }
            MotionLevel:
              type: string
              enum: [low, high]
        """;

    [Theory]
    [InlineData("server", ".atc-rest-api-server", "{}")]
    [InlineData("client", ".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation" }""")]
    [InlineData("client", ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""")]
    public void Generator_EmitsASchemaNoOperationUses_AndWhatItRefersTo(
        string kind,
        string markerFile,
        string marker)
    {
        IIncrementalGenerator generator = kind == "server" ? new ApiServerGenerator() : new ApiClientGenerator();
        var compilation = CSharpCompilation.Create(
            "Cameras.Host",
            [],
            CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: true),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(
            [
                new CompilationVerificationHarness.InMemoryAdditionalText($"Cameras-{kind}-{marker.Length}.yaml", Yaml),
                new CompilationVerificationHarness.InMemoryAdditionalText(markerFile, marker),
            ])
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, TestContext.Current.CancellationToken);

        var sources = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()));

        Assert.Contains("record MotionDetected(", sources, StringComparison.Ordinal);
        Assert.Contains("MotionLevel Level", sources, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"(enum|struct|record struct|class) MotionLevel\b", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)), sources);
        Assert.Empty(output.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));
    }
}