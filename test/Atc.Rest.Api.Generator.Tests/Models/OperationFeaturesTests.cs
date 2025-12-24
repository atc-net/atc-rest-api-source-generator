namespace Atc.Rest.Api.Generator.Tests.Models;

public class OperationFeaturesTests
{
    [Fact]
    public void DefaultInstance_HasAllPropertiesFalse()
    {
        var features = new OperationFeatures();

        Assert.False(features.HasParameters);
        Assert.False(features.HasPathParameters);
        Assert.False(features.HasSecurity);
        Assert.False(features.HasRolesOrPolicies);
        Assert.False(features.HasRateLimiting);
        Assert.Equal(string.Empty, features.HttpMethod);
    }

    [Fact]
    public void WithAllFeatures_HasAllPropertiesSet()
    {
        var features = new OperationFeatures
        {
            HasParameters = true,
            HasPathParameters = true,
            HasSecurity = true,
            HasRolesOrPolicies = true,
            HasRateLimiting = true,
            HttpMethod = "GET",
        };

        Assert.True(features.HasParameters);
        Assert.True(features.HasPathParameters);
        Assert.True(features.HasSecurity);
        Assert.True(features.HasRolesOrPolicies);
        Assert.True(features.HasRateLimiting);
        Assert.Equal("GET", features.HttpMethod);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public void HttpMethod_AcceptsAllHttpMethods(string httpMethod)
    {
        var features = new OperationFeatures { HttpMethod = httpMethod };
        Assert.Equal(httpMethod, features.HttpMethod);
    }

    [Fact]
    public void HasParametersOnly_OtherPropertiesAreFalse()
    {
        var features = new OperationFeatures { HasParameters = true };

        Assert.True(features.HasParameters);
        Assert.False(features.HasPathParameters);
        Assert.False(features.HasSecurity);
        Assert.False(features.HasRolesOrPolicies);
        Assert.False(features.HasRateLimiting);
    }

    [Fact]
    public void HasSecurityOnly_OtherPropertiesAreFalse()
    {
        var features = new OperationFeatures { HasSecurity = true };

        Assert.False(features.HasParameters);
        Assert.False(features.HasPathParameters);
        Assert.True(features.HasSecurity);
        Assert.False(features.HasRolesOrPolicies);
        Assert.False(features.HasRateLimiting);
    }

    [Fact]
    public void HasRolesOrPoliciesOnly_OtherPropertiesAreFalse()
    {
        var features = new OperationFeatures { HasRolesOrPolicies = true };

        Assert.False(features.HasParameters);
        Assert.False(features.HasPathParameters);
        Assert.False(features.HasSecurity);
        Assert.True(features.HasRolesOrPolicies);
        Assert.False(features.HasRateLimiting);
    }

    [Fact]
    public void HasRateLimitingOnly_OtherPropertiesAreFalse()
    {
        var features = new OperationFeatures { HasRateLimiting = true };

        Assert.False(features.HasParameters);
        Assert.False(features.HasPathParameters);
        Assert.False(features.HasSecurity);
        Assert.False(features.HasRolesOrPolicies);
        Assert.True(features.HasRateLimiting);
    }
}