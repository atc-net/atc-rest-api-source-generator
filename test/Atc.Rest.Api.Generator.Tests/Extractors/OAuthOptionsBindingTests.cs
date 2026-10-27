namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// The generated OAuth registration reads each option from its configuration section with generated code (see the
/// runtime test <c>OAuthOptionsBindingRuntimeTests</c>), including the refresh URL when a flow has one.
/// </summary>
public class OAuthOptionsBindingTests
{
    [Fact]
    public void Extract_WithRefreshTokens_AlsoReadsTheRefreshUrl()
    {
        var config = new OAuthConfig
        {
            HasAuthorizationCode = true,
            AuthorizationCodeTokenUrl = "https://auth.contoso.com/token",
            AuthorizationCodeRefreshUrl = "https://auth.contoso.com/refresh",
        };

        var result = OAuthDependencyInjectionExtractor.Extract(config, "Contoso.Orders");

        Assert.NotNull(result);
        Assert.DoesNotContain("BindConfiguration", result, StringComparison.Ordinal);
        Assert.Contains("section[\"RefreshUrl\"]", result, StringComparison.Ordinal);
    }
}