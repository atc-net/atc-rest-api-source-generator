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

        // No "JSON options" item: this specification has neither an enum nor a polymorphic schema
        foreach (var item in new[] { "ProblemDetails", "application/problem+xml" })
        {
            Assert.Contains(lines, line => line.Contains("/// <para>• ", StringComparison.Ordinal) && line.Contains(item, StringComparison.Ordinal) && line.TrimEnd().EndsWith("</para>", StringComparison.Ordinal));
        }

        Assert.Contains("AddApiHandlersFrom", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>AllowOutOfOrderMetadataProperties</c> lets a polymorphic body's discriminator stand anywhere in the object:
    /// it is set only when the specification has a polymorphic schema. The enum wire converter has a block of its own;
    /// with neither, Add{Api}Api() configures no JSON options at all.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Extract_JsonOptions_FollowWhatTheSpecificationHas(
        bool polymorphic,
        bool enums)
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(JsonOptionsYaml(polymorphic, enums));

        // Act
        var result = UnifiedServiceCollectionExtractor.Extract(document, "TestApi", new ServerConfig());

        // Assert
        Assert.Equal(polymorphic, result.Contains("AllowOutOfOrderMetadataProperties = true", StringComparison.Ordinal));
        Assert.Equal(polymorphic, result.Contains("discriminator", StringComparison.Ordinal));
        Assert.Equal(enums, result.Contains("EnumWireConverterFactory", StringComparison.Ordinal));
        Assert.Equal(polymorphic || enums, result.Contains("services.ConfigureHttpJsonOptions(", StringComparison.Ordinal));
        Assert.Equal(polymorphic || enums, result.Contains("<para>• JSON options", StringComparison.Ordinal));
    }

    // A spec with or without a polymorphic schema (a oneOf with a discriminator) and with or without an enum
    private static string JsonOptionsYaml(
        bool polymorphic,
        bool enums)
        => $$"""
            openapi: 3.0.3
            info: { title: T, version: 1.0.0 }
            paths:
              /things:
                post:
                  operationId: createThing
                  requestBody:
                    content:
                      application/json:
                        schema: { $ref: '#/components/schemas/{{(polymorphic ? "Shape" : "Thing")}}' }
                  responses:
                    '204': { description: No Content }
            components:
              schemas:
                Thing:
                  type: object
                  required: [name]
                  properties:
                    name: { type: string{{(enums ? ", enum: [Red, Green]" : string.Empty)}} }
            {{(polymorphic ? ShapeSchemas : string.Empty)}}
            """;

    // A polymorphic schema is generated whether an operation uses it or not, so the non-polymorphic spec has none
    private const string ShapeSchemas = """
            Circle:
              type: object
              required: [kind, radius]
              properties:
                kind: { type: string }
                radius: { type: number }
            Square:
              type: object
              required: [kind, side]
              properties:
                kind: { type: string }
                side: { type: number }
            Shape:
              oneOf:
                - $ref: '#/components/schemas/Circle'
                - $ref: '#/components/schemas/Square'
              discriminator:
                propertyName: kind
                mapping:
                  circle: '#/components/schemas/Circle'
                  square: '#/components/schemas/Square'
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