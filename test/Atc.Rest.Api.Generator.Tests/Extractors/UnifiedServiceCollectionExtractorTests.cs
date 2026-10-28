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

    /// <summary>
    /// The XML docs are what Visual Studio shows on mouse-over: the registrations as real bullets (a <c>/// - </c> line
    /// is joined into one long line), the API by its name, and every registration the method makes - the XML problem
    /// writer and the JSON options too.
    /// </summary>
    [Fact]
    public void Extract_TheToolTip_ListsEveryRegistrationAsABullet()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(XmlYaml);

        // Act
        var result = UnifiedServiceCollectionExtractor.Extract(document, "TestApi", new ServerConfig());

        // Assert
        var lines = result.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.DoesNotContain(lines, line => line.TrimStart().StartsWith("/// - ", StringComparison.Ordinal));
        Assert.DoesNotContain("TestApi API", result, StringComparison.Ordinal);
        Assert.Contains("/// Adds the services the Test API needs.", result, StringComparison.Ordinal);

        // One paragraph per item with a typed bullet: Visual Studio's tooltip renders a <list> as one run-on line
        Assert.DoesNotContain("<list", result, StringComparison.Ordinal);
        foreach (var item in new[] { "ProblemDetails", "application/problem+xml", "JSON" })
        {
            Assert.Contains(lines, line => line.Contains("/// <para>• ", StringComparison.Ordinal) && line.Contains(item, StringComparison.Ordinal) && line.TrimEnd().EndsWith("</para>", StringComparison.Ordinal));
        }

        Assert.Contains("AddApiHandlersFrom", result, StringComparison.Ordinal);
    }

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