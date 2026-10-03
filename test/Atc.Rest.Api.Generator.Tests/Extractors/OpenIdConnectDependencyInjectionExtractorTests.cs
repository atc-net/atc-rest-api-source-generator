namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OpenIdConnectDependencyInjectionExtractorTests
{
    [Fact]
    public void Extract_NullConfig_ReturnsNull()
    {
        Assert.Null(OpenIdConnectDependencyInjectionExtractor.Extract(null, "TestApi"));
    }

    [Fact]
    public void Extract_ConfigWithDiscoveryUrlAndScopes_UsesUrlAsAuthorityFallbackAndAddsScopesOnce()
    {
        var config = new OpenIdConnectConfig
        {
            SchemeName = "oidc",
            OpenIdConnectUrl = "https://login.example.com/.well-known/openid-configuration",
        };
        config.DefaultScopes.Add("openid", string.Empty);
        config.DefaultScopes.Add("email", string.Empty);

        var result = OpenIdConnectDependencyInjectionExtractor.Extract(config, "Demo.PetStore");

        Assert.NotNull(result);
        Assert.Contains("namespace Demo.PetStore.Generated.Authentication;", result, StringComparison.Ordinal);
        Assert.Contains("public static class OpenIdConnectAuthenticationExtensions", result, StringComparison.Ordinal);
        Assert.Contains("public static IServiceCollection AddPetStoreOpenIdConnectAuthentication(", result, StringComparison.Ordinal);
        Assert.Contains("options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;", result, StringComparison.Ordinal);

        // Configuration overrides the spec, the spec URL is the fallback.
        Assert.Contains("options.Authority = configuration[\"Authentication:OpenIdConnect:Authority\"]", result, StringComparison.Ordinal);
        Assert.Contains("?? \"https://login.example.com/.well-known/openid-configuration\";", result, StringComparison.Ordinal);
        Assert.Contains("?? throw new InvalidOperationException(\"Authentication:OpenIdConnect:ClientId is required\");", result, StringComparison.Ordinal);

        // openid is always first and never duplicated; profile is only a default when no scopes are requested.
        Assert.Contains("options.Scope.Clear();", result, StringComparison.Ordinal);
        Assert.Equal(1, result.Split("options.Scope.Add(\"openid\");", StringSplitOptions.None).Length - 1);
        Assert.Contains("options.Scope.Add(\"email\");", result, StringComparison.Ordinal);
        Assert.DoesNotContain("options.Scope.Add(\"profile\");", result, StringComparison.Ordinal);

        Assert.Contains("using Microsoft.AspNetCore.Authentication.Cookies;", result, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.AspNetCore.Authentication.OpenIdConnect;", result, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.Extensions.Configuration;", result, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.Extensions.DependencyInjection;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ConfigWithoutDiscoveryUrlOrScopes_RequiresAuthorityAndDefaultsToProfileScope()
    {
        var config = new OpenIdConnectConfig();

        var result = OpenIdConnectDependencyInjectionExtractor.Extract(config, "TestApi");

        Assert.NotNull(result);
        Assert.Contains("public static IServiceCollection AddTestApiOpenIdConnectAuthentication(", result, StringComparison.Ordinal);
        Assert.Contains("?? throw new InvalidOperationException(\"Authentication:OpenIdConnect:Authority is required\");", result, StringComparison.Ordinal);
        Assert.Contains("options.Scope.Add(\"openid\");", result, StringComparison.Ordinal);
        Assert.Contains("options.Scope.Add(\"profile\");", result, StringComparison.Ordinal);
    }
}