namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class UnifiedServiceCollectionExtractorTests
{
    private const string XmlYaml = """
                                   openapi: 3.0.3
                                   info:
                                     title: Test
                                     version: 1.0.0
                                   paths:
                                     /things:
                                       get:
                                         operationId: listThings
                                         responses:
                                           '200':
                                             description: OK
                                             content:
                                               application/xml:
                                                 schema:
                                                   $ref: '#/components/schemas/Thing'
                                   components:
                                     schemas:
                                       Thing:
                                         type: object
                                         properties:
                                           name:
                                             type: string
                                   """;

    [Fact]
    public void Extract_XmlProblemDetailsWriterGuard_SkipsKeyedDescriptorsBeforeReadingTheImplementationType()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(XmlYaml);

        // Act
        var result = UnifiedServiceCollectionExtractor.Extract(document, "TestApi", new ServerConfig());

        // Assert
        // ImplementationType throws on a keyed descriptor (DI Abstractions 8.0.0/8.0.1): read only on an unkeyed IProblemDetailsWriter
        Assert.Contains(
            "if (!services.Any(d => d.ServiceType == typeof(global::Microsoft.AspNetCore.Http.IProblemDetailsWriter) && !d.IsKeyedService && d.ImplementationType == typeof(global::TestApi.Generated.XmlProblemDetailsWriter)))",
            result,
            StringComparison.Ordinal);
        Assert.DoesNotContain("services.Any(d => d.ImplementationType", result, StringComparison.Ordinal);
    }
}