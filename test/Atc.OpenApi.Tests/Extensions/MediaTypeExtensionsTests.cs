namespace Atc.OpenApi.Tests.Extensions;

public sealed class MediaTypeExtensionsTests
{
    [Theory]
    [InlineData("application/json", "application/json", true, false)]
    [InlineData("application/json; charset=utf-8", "application/json", true, false)]
    [InlineData("Application/JSON", "Application/JSON", true, false)]
    [InlineData("application/hal+json", "application/hal+json", true, false)]
    [InlineData("application/problem+json; charset=utf-8", "application/problem+json", true, false)]
    [InlineData("application/xml", "application/xml", false, true)]
    [InlineData("text/xml; charset=utf-8", "text/xml", false, true)]
    [InlineData("application/atom+xml", "application/atom+xml", false, true)]
    [InlineData("application/json-seq", "application/json-seq", false, false)]
    [InlineData("text/plain", "text/plain", false, false)]
    [InlineData(null, "", false, false)]
    public void BaseType_IsJson_IsXml(
        string? mediaType,
        string baseType,
        bool isJson,
        bool isXml)
    {
        Assert.Equal(baseType, mediaType.BaseType);
        Assert.Equal(isJson, mediaType.IsJson);
        Assert.Equal(isXml, mediaType.IsXml);
    }

    [Fact]
    public void JsonBody_PrefersApplicationJson_ElseTheFirstJsonMediaType()
    {
        var plain = new OpenApiMediaType();
        var hal = new OpenApiMediaType();
        var xml = new OpenApiMediaType();

        Assert.Same(plain, new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal) { ["application/hal+json"] = hal, ["application/json"] = plain }.JsonBody);
        Assert.Same(hal, new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal) { ["application/xml"] = xml, ["application/hal+json"] = hal }.JsonBody);
        Assert.Null(new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal) { ["application/xml"] = xml }.JsonBody);
        Assert.Null(((IDictionary<string, IOpenApiMediaType>?)null).JsonBody);
    }
}