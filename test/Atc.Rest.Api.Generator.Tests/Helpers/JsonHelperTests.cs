namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class JsonHelperTests
{
    [Fact]
    public void ConfigOptions_PropertyNameCaseInsensitive_IsTrue()
    {
        Assert.True(JsonHelper.ConfigOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void ConfigOptions_CanDeserializeWithCamelCase()
    {
        var json = """{"generate": true, "namespace": "Test"}""";
        var result = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(result);
        Assert.True(result!.Generate);
        Assert.Equal("Test", result.Namespace);
    }

    [Fact]
    public void ConfigOptions_CanDeserializeWithPascalCase()
    {
        var json = """{"Generate": true, "Namespace": "Test"}""";
        var result = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(result);
        Assert.True(result!.Generate);
        Assert.Equal("Test", result.Namespace);
    }

    [Fact]
    public void ConfigOptions_CanDeserializeWithMixedCase()
    {
        var json = """{"GENERATE": true, "namespace": "Test"}""";
        var result = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(result);
        Assert.True(result!.Generate);
        Assert.Equal("Test", result.Namespace);
    }
}