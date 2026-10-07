namespace Atc.OpenApi.Tests.Extensions;

public class OpenApiSpecVersionExtensionsTests
{
    [Theory]
    [InlineData(OpenApiSpecVersion.OpenApi2_0, "2.0")]
    [InlineData(OpenApiSpecVersion.OpenApi3_0, "3.0")]
    [InlineData(OpenApiSpecVersion.OpenApi3_1, "3.1")]
    [InlineData(OpenApiSpecVersion.OpenApi3_2, "3.2")]
    public void ToDisplayString_KnownVersion_ReturnsShortForm(
        OpenApiSpecVersion version,
        string expected)
    {
        OpenApiSpecVersion? specVersion = version;

        Assert.Equal(expected, specVersion.ToDisplayString());
    }

    [Fact]
    public void ToDisplayString_Null_ReturnsGeneric3x()
    {
        OpenApiSpecVersion? specVersion = null;

        Assert.Equal("3.x", specVersion.ToDisplayString());
    }

    [Fact]
    public void ToDisplayString_ParsedDocumentVersion_RoundTrips()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: 3.1.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            """);

        Assert.Equal("3.1", doc.GetOpenApiSpecVersion().ToDisplayString());
    }
}