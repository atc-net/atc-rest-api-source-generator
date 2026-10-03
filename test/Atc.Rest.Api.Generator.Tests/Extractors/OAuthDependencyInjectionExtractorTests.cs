namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OAuthDependencyInjectionExtractorTests
{
    [Fact]
    public void Extract_NullConfig_ReturnsNull()
    {
        Assert.Null(OAuthDependencyInjectionExtractor.Extract(null, "TestApi"));
    }

    [Fact]
    public void Extract_WithConfig_GeneratesServiceCollectionAndHttpClientBuilderExtensions()
    {
        var config = new OAuthConfig
        {
            HasClientCredentials = true,
            ClientCredentialsTokenUrl = "https://auth.example.com/token",
        };

        var result = OAuthDependencyInjectionExtractor.Extract(config, "Demo.PetStore");

        Assert.NotNull(result);
        Assert.Contains("namespace Demo.PetStore.Generated.OAuth;", result, StringComparison.Ordinal);
        Assert.Contains("public static class OAuthServiceCollectionExtensions", result, StringComparison.Ordinal);

        // The registration method is named after the last project-name segment and binds "OAuth:{projectName}".
        Assert.Contains("public static IServiceCollection AddPetStoreOAuthAuthentication(", result, StringComparison.Ordinal);
        Assert.Contains(".BindConfiguration(\"OAuth:Demo.PetStore\");", result, StringComparison.Ordinal);
        Assert.Contains("services.AddHttpClient<IOAuthTokenProvider, OAuthTokenProvider>(\"OAuthTokenProvider\");", result, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<OAuthAuthenticationHandler>();", result, StringComparison.Ordinal);

        Assert.Contains("public static IHttpClientBuilder AddOAuthAuthentication(", result, StringComparison.Ordinal);
        Assert.Contains("return builder.AddHttpMessageHandler<OAuthAuthenticationHandler>();", result, StringComparison.Ordinal);

        Assert.Contains("using System;", result, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.Extensions.DependencyInjection;", result, StringComparison.Ordinal);
    }
}