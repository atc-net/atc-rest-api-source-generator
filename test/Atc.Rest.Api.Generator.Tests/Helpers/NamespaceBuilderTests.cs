namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class NamespaceBuilderTests
{
    // ========== Build Tests ==========
    [Theory]
    [InlineData("MyApi", "Models", null, "MyApi.Generated.Models")]
    [InlineData("MyApi", "Models", "Pets", "MyApi.Generated.Pets.Models")]
    [InlineData("MyApi", "Handlers", null, "MyApi.Generated.Handlers")]
    [InlineData("MyApi", "Handlers", "Users", "MyApi.Generated.Users.Handlers")]
    [InlineData("MyApi", "Results", "", "MyApi.Generated.Results")] // empty pathSegment treated as null
    public void Build_ReturnsExpectedNamespace(
        string projectName,
        string category,
        string? pathSegment,
        string expected)
    {
        var result = NamespaceBuilder.Build(projectName, category, pathSegment);
        Assert.Equal(expected, result);
    }

    // ========== BuildNested Tests ==========
    [Theory]
    [InlineData("MyApi", "Endpoints", "Interfaces", null, "MyApi.Generated.Endpoints.Interfaces")]
    [InlineData("MyApi", "Endpoints", "Interfaces", "Pets", "MyApi.Generated.Pets.Endpoints.Interfaces")]
    public void BuildNested_ReturnsExpectedNamespace(
        string projectName,
        string category,
        string subCategory,
        string? pathSegment,
        string expected)
    {
        var result = NamespaceBuilder.BuildNested(projectName, category, subCategory, pathSegment);
        Assert.Equal(expected, result);
    }

    // ========== BuildBase Tests ==========
    [Fact]
    public void BuildBase_ReturnsGeneratedNamespace()
    {
        var result = NamespaceBuilder.BuildBase("MyApi");
        Assert.Equal("MyApi.Generated", result);
    }

    // ========== Convenience Method Tests ==========
    [Fact]
    public void ForModels_WithoutSegment_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Models", NamespaceBuilder.ForModels("MyApi"));
    }

    [Fact]
    public void ForModels_WithSegment_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Pets.Models", NamespaceBuilder.ForModels("MyApi", "Pets"));
    }

    [Fact]
    public void ForParameters_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Parameters", NamespaceBuilder.ForParameters("MyApi"));
    }

    [Fact]
    public void ForResults_WithSegment_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Users.Results", NamespaceBuilder.ForResults("MyApi", "Users"));
    }

    [Fact]
    public void ForHandlers_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Handlers", NamespaceBuilder.ForHandlers("MyApi"));
    }

    [Fact]
    public void ForEndpoints_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Endpoints", NamespaceBuilder.ForEndpoints("MyApi"));
    }

    [Fact]
    public void ForClient_WithSegment_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Pets.Client", NamespaceBuilder.ForClient("MyApi", "Pets"));
    }

    [Fact]
    public void ForEndpointInterfaces_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Endpoints.Interfaces", NamespaceBuilder.ForEndpointInterfaces("MyApi"));
    }

    [Fact]
    public void ForEndpointInterfaces_WithSegment_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Pets.Endpoints.Interfaces", NamespaceBuilder.ForEndpointInterfaces("MyApi", "Pets"));
    }

    [Fact]
    public void ForEndpointResults_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Endpoints.Results", NamespaceBuilder.ForEndpointResults("MyApi"));
    }

    // ========== Webhook Namespace Tests ==========
    [Fact]
    public void ForWebhooks_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Webhooks", NamespaceBuilder.ForWebhooks("MyApi"));
    }

    [Fact]
    public void ForWebhookHandlers_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Webhooks.Handlers", NamespaceBuilder.ForWebhookHandlers("MyApi"));
    }

    [Fact]
    public void ForWebhookParameters_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Webhooks.Parameters", NamespaceBuilder.ForWebhookParameters("MyApi"));
    }

    [Fact]
    public void ForWebhookResults_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Webhooks.Results", NamespaceBuilder.ForWebhookResults("MyApi"));
    }

    [Fact]
    public void ForWebhookEndpoints_ReturnsCorrectNamespace()
    {
        Assert.Equal("MyApi.Generated.Webhooks.Endpoints", NamespaceBuilder.ForWebhookEndpoints("MyApi"));
    }
}