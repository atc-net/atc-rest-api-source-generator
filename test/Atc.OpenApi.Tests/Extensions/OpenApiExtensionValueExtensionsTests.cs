namespace Atc.OpenApi.Tests.Extensions;

/// <summary>
/// One reader for the value of every <c>x-</c> extension, from YAML (numbers arrive as <see cref="decimal"/>) and JSON.
/// </summary>
public sealed class OpenApiExtensionValueExtensionsTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Values
          version: 1.0.0
        x-text: hello
        x-flag: true
        x-count: 3
        x-huge: 99999999999
        x-ratio: 0.25
        x-names: [a, b, 7]
        x-empty-list: []
        paths: {}
        """;

    private const string Json = """
        { "openapi": "3.0.3", "info": { "title": "Values", "version": "1.0.0" }, "x-count": 3, "x-ratio": 0.25, "paths": {} }
        """;

    [Fact]
    public void Read_FromYaml_EachKindOfValue()
    {
        var extensions = OpenApiDocumentHelper.ParseYaml(Yaml).Extensions;

        Assert.Equal("hello", extensions.ReadString("x-text"));
        Assert.True(extensions.ReadBool("x-flag"));
        Assert.Equal(3, extensions.ReadInt("x-count"));
        Assert.Equal(int.MaxValue, extensions.ReadInt("x-huge"));
        Assert.Equal(0.25, extensions.ReadDouble("x-ratio"));
        Assert.Equal(3.0, extensions.ReadDouble("x-count"));
        Assert.Equal(["a", "b"], extensions.ReadStringArray("x-names")!, StringComparer.Ordinal);
    }

    [Fact]
    public void Read_FromJson_Numbers()
    {
        var extensions = OpenApiDocumentHelper.ParseYaml(Json).Extensions;

        Assert.Equal(3, extensions.ReadInt("x-count"));
        Assert.Equal(0.25, extensions.ReadDouble("x-ratio"));
    }

    [Fact]
    public void Read_AnAbsentOrMismatchedValue_IsNull()
    {
        var extensions = OpenApiDocumentHelper.ParseYaml(Yaml).Extensions;

        Assert.Null(extensions.NodeOf("x-missing"));
        Assert.Null(extensions.ReadString("x-missing"));
        Assert.Null(extensions.ReadBool("x-text"));
        Assert.Null(extensions.ReadInt("x-text"));
        Assert.Null(extensions.ReadStringArray("x-text"));
        Assert.Null(extensions.ReadStringArray("x-empty-list"));
        Assert.Null(((IDictionary<string, IOpenApiExtension>?)null).ReadString("x-text"));
    }
}