namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for the SecurityPoliciesExtractor helpers; the Extract method itself is covered by PolicyExtractorTests.
/// </summary>
public class SecurityPoliciesExtractorTests
{
    private const string OAuth2Scheme = """
                                        components:
                                          securitySchemes:
                                            oauth2:
                                              type: oauth2
                                              flows:
                                                clientCredentials:
                                                  tokenUrl: https://auth.example.com/token
                                                  scopes:
                                                    "pets.read": Read access to pets
                                                    "pets.write": Write access to pets
                                            api_key:
                                              type: apiKey
                                              in: header
                                              name: X-Api-Key
                                        """;

    [Fact]
    public void HasPolicies_OperationRequiresScope_ReturnsTrue()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            $"""
             openapi: 3.0.0
             info:
               title: Test
               version: 1.0.0
             paths:
               /pets:
                 get:
                   operationId: listPets
                   security:
                     - oauth2:
                         - "pets.read"
                   responses:
                     '200':
                       description: OK
             {OAuth2Scheme}
             """);

        Assert.True(SecurityPoliciesExtractor.HasPolicies(document));
    }

    [Fact]
    public void HasPolicies_AuthenticatedWithoutScopes_ReturnsFalse()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            $"""
             openapi: 3.0.0
             info:
               title: Test
               version: 1.0.0
             paths:
               /pets:
                 get:
                   operationId: listPets
                   security:
                     - api_key: []
                   responses:
                     '200':
                       description: OK
             {OAuth2Scheme}
             """);

        Assert.False(SecurityPoliciesExtractor.HasPolicies(document));
    }

    [Fact]
    public void HasPolicies_OnlyDeprecatedOperationRequiresScope_HonoursIncludeDeprecated()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            $"""
             openapi: 3.0.0
             info:
               title: Test
               version: 1.0.0
             paths:
               /pets:
                 get:
                   operationId: listPets
                   deprecated: true
                   security:
                     - oauth2:
                         - "pets.read"
                   responses:
                     '200':
                       description: OK
             {OAuth2Scheme}
             """);

        Assert.False(SecurityPoliciesExtractor.HasPolicies(document));
        Assert.True(SecurityPoliciesExtractor.HasPolicies(document, includeDeprecated: true));
    }

    [Fact]
    public void OperationPolicyName_NoRequirementHasScopes_ReturnsNull()
    {
        var requirements = new List<SecurityRequirement>
        {
            new() { SchemeName = "api_key", AuthenticationScheme = "api_key" },
            new() { SchemeName = "bearer", AuthenticationScheme = "Bearer" },
        };

        Assert.Null(SecurityPoliciesExtractor.OperationPolicyName(requirements));
    }

    [Fact]
    public void OperationPolicyName_SingleRequirementWithScopes_ReturnsScopePolicy()
    {
        var requirements = new List<SecurityRequirement>
        {
            new() { SchemeName = "oauth2", AuthenticationScheme = "oauth2", Scopes = ["pets.read"] },
        };

        Assert.Equal("oauth2:pets.read", SecurityPoliciesExtractor.OperationPolicyName(requirements));
    }

    [Fact]
    public void OperationPolicyName_DifferentAlternatives_JoinsSortedNamesWithPipe()
    {
        var requirements = new List<SecurityRequirement>
        {
            new() { SchemeName = "oauth2", AuthenticationScheme = "oauth2", Scopes = ["pets.write", "pets.read"] },
            new() { SchemeName = "api_key", AuthenticationScheme = "api_key" },
        };

        Assert.Equal("api_key|oauth2:pets.read+pets.write", SecurityPoliciesExtractor.OperationPolicyName(requirements));
    }

    [Fact]
    public void OperationPolicyName_SameAlternativeTwice_ReturnsSingleName()
    {
        var requirements = new List<SecurityRequirement>
        {
            new() { SchemeName = "oauth2", AuthenticationScheme = "oauth2", Scopes = ["pets.read"] },
            new() { SchemeName = "oauth2", AuthenticationScheme = "oauth2", Scopes = ["pets.read"] },
        };

        Assert.Equal("oauth2:pets.read", SecurityPoliciesExtractor.OperationPolicyName(requirements));
    }

    [Theory]
    [InlineData("oauth2", new string[0], "oauth2")]
    [InlineData("oauth2", new[] { "pets.read" }, "oauth2:pets.read")]
    [InlineData("oauth2", new[] { "pets.write", "pets.read" }, "oauth2:pets.read+pets.write")]
    public void GeneratePolicyName_ReturnsSchemeWithSortedScopes(
        string schemeName,
        string[] scopes,
        string expected)
        => Assert.Equal(expected, SecurityPoliciesExtractor.GeneratePolicyName(schemeName, scopes));

    [Theory]
    [InlineData("oauth2", "Oauth2")]
    [InlineData("oauth2:pets:read", "Oauth2PetsRead")]
    [InlineData("api_key|oauth2:pets:read", "ApiKeyOrOauth2PetsRead")]
    [InlineData("oauth2:admin+orders:write", "Oauth2AdminAndOrdersWrite")]
    public void GenerateConstantName_ReadsSeparatorsAsPascalCaseJoiners(
        string policyName,
        string expected)
        => Assert.Equal(expected, SecurityPoliciesExtractor.GenerateConstantName(policyName));

    [Fact]
    public void GenerateConstantName_Null_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() => SecurityPoliciesExtractor.GenerateConstantName(null!));
}