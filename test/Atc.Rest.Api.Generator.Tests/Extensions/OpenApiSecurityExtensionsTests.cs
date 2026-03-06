namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiSecurityExtensionsTests
{
    // ========== HasSecuritySchemes Tests ==========
    [Fact]
    public void HasSecuritySchemes_NoComponents_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasSecuritySchemes());
    }

    [Fact]
    public void HasSecuritySchemes_WithBearerScheme_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithJwtBearer);

        Assert.NotNull(doc);
        Assert.True(doc!.HasSecuritySchemes());
    }

    // ========== HasDocumentSecurity Tests ==========
    [Fact]
    public void HasDocumentSecurity_NoSecurity_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasDocumentSecurity());
    }

    [Fact]
    public void HasDocumentSecurity_WithDocumentSecurity_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithDocumentLevelSecurity);

        Assert.NotNull(doc);
        Assert.True(doc!.HasDocumentSecurity());
    }

    // ========== HasJwtBearerSecurity Tests ==========
    [Fact]
    public void HasJwtBearerSecurity_NoSchemes_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasJwtBearerSecurity());
    }

    [Fact]
    public void HasJwtBearerSecurity_WithBearerScheme_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithJwtBearer);

        Assert.NotNull(doc);
        Assert.True(doc!.HasJwtBearerSecurity());
    }

    [Fact]
    public void HasJwtBearerSecurity_WithApiKeyOnly_ReturnsFalse()
    {
        var doc = ParseYaml(YamlWithApiKey);

        Assert.NotNull(doc);
        Assert.False(doc!.HasJwtBearerSecurity());
    }

    // ========== HasOAuth2ClientCredentials Tests ==========
    [Fact]
    public void HasOAuth2ClientCredentials_NoSchemes_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasOAuth2ClientCredentials());
    }

    [Fact]
    public void HasOAuth2ClientCredentials_WithClientCredentials_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOAuth2ClientCredentials);

        Assert.NotNull(doc);
        Assert.True(doc!.HasOAuth2ClientCredentials());
    }

    // ========== HasOAuth2AuthorizationCode Tests ==========
    [Fact]
    public void HasOAuth2AuthorizationCode_NoSchemes_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasOAuth2AuthorizationCode());
    }

    [Fact]
    public void HasOAuth2AuthorizationCode_WithAuthCode_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOAuth2AuthorizationCode);

        Assert.NotNull(doc);
        Assert.True(doc!.HasOAuth2AuthorizationCode());
    }

    // ========== HasOAuth2TokenManagement Tests ==========
    [Fact]
    public void HasOAuth2TokenManagement_NoSchemes_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasOAuth2TokenManagement());
    }

    [Fact]
    public void HasOAuth2TokenManagement_WithClientCredentials_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOAuth2ClientCredentials);

        Assert.NotNull(doc);
        Assert.True(doc!.HasOAuth2TokenManagement());
    }

    // ========== HasOpenIdConnectSecurity Tests ==========
    [Fact]
    public void HasOpenIdConnectSecurity_NoSchemes_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasOpenIdConnectSecurity());
    }

    [Fact]
    public void HasOpenIdConnectSecurity_WithOpenIdConnect_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOpenIdConnect);

        Assert.NotNull(doc);
        Assert.True(doc!.HasOpenIdConnectSecurity());
    }

    // ========== ExtractSecuritySchemes Tests ==========
    [Fact]
    public void ExtractSecuritySchemes_NoComponents_ReturnsEmpty()
    {
        var doc = new OpenApiDocument();

        var result = doc.ExtractSecuritySchemes();

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractSecuritySchemes_WithBearerScheme_ExtractsCorrectly()
    {
        var doc = ParseYaml(YamlWithJwtBearer);
        Assert.NotNull(doc);

        var result = doc!.ExtractSecuritySchemes();

        Assert.Single(result);
        Assert.True(result.ContainsKey("bearerAuth"));
        Assert.Equal("bearerAuth", result["bearerAuth"].Name);
        Assert.Equal(
            Atc.OpenApi.Models.SecuritySchemeType.Http,
            result["bearerAuth"].Type);
        Assert.Equal("bearer", result["bearerAuth"].Scheme);
    }

    [Fact]
    public void ExtractSecuritySchemes_WithApiKey_ExtractsCorrectly()
    {
        var doc = ParseYaml(YamlWithApiKey);
        Assert.NotNull(doc);

        var result = doc!.ExtractSecuritySchemes();

        Assert.Single(result);
        Assert.True(result.ContainsKey("apiKeyAuth"));
        Assert.Equal(
            Atc.OpenApi.Models.SecuritySchemeType.ApiKey,
            result["apiKeyAuth"].Type);
        Assert.Equal("X-API-Key", result["apiKeyAuth"].ParameterName);
    }

    [Fact]
    public void ExtractSecuritySchemes_WithOAuth2_ExtractsFlows()
    {
        var doc = ParseYaml(YamlWithOAuth2ClientCredentials);
        Assert.NotNull(doc);

        var result = doc!.ExtractSecuritySchemes();

        Assert.Single(result);
        Assert.True(result.ContainsKey("oauth2"));
        Assert.Equal(
            Atc.OpenApi.Models.SecuritySchemeType.OAuth2,
            result["oauth2"].Type);
        Assert.NotNull(result["oauth2"].Flows);
        Assert.NotNull(result["oauth2"].Flows!.ClientCredentials);
    }

    // ========== ExtractSecurityRequirements Tests ==========
    [Fact]
    public void ExtractSecurityRequirements_NoSecurity_ReturnsNull()
    {
        var doc = ParseYaml(YamlWithNoSecurity);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractSecurityRequirements(doc);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractSecurityRequirements_WithDocumentSecurity_ReturnsRequirements()
    {
        var doc = ParseYaml(YamlWithDocumentLevelSecurity);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractSecurityRequirements(doc);

        Assert.NotNull(result);
        Assert.NotEmpty(result!);
    }

    // ========== GetOAuth2SchemeName Tests ==========
    [Fact]
    public void GetOAuth2SchemeName_NoSchemes_ReturnsNull()
    {
        var doc = new OpenApiDocument();

        Assert.Null(doc.GetOAuth2SchemeName());
    }

    [Fact]
    public void GetOAuth2SchemeName_WithOAuth2_ReturnsName()
    {
        var doc = ParseYaml(YamlWithOAuth2ClientCredentials);
        Assert.NotNull(doc);

        var result = doc!.GetOAuth2SchemeName();

        Assert.Equal("oauth2", result);
    }

    // ========== GetOpenIdConnectSchemeName Tests ==========
    [Fact]
    public void GetOpenIdConnectSchemeName_NoSchemes_ReturnsNull()
    {
        var doc = new OpenApiDocument();

        Assert.Null(doc.GetOpenIdConnectSchemeName());
    }

    [Fact]
    public void GetOpenIdConnectSchemeName_WithOpenIdConnect_ReturnsName()
    {
        var doc = ParseYaml(YamlWithOpenIdConnect);
        Assert.NotNull(doc);

        var result = doc!.GetOpenIdConnectSchemeName();

        Assert.Equal("oidc", result);
    }

    // ========== GetOpenIdConnectUrl Tests ==========
    [Fact]
    public void GetOpenIdConnectUrl_NoSchemes_ReturnsNull()
    {
        var doc = new OpenApiDocument();

        Assert.Null(doc.GetOpenIdConnectUrl());
    }

    [Fact]
    public void GetOpenIdConnectUrl_WithOpenIdConnect_ReturnsUrl()
    {
        var doc = ParseYaml(YamlWithOpenIdConnect);
        Assert.NotNull(doc);

        var result = doc!.GetOpenIdConnectUrl();

        Assert.NotNull(result);
        Assert.Contains(
            ".well-known/openid-configuration",
            result,
            StringComparison.Ordinal);
    }

    // ========== GetOAuth2ClientCredentialsFlow Tests ==========
    [Fact]
    public void GetOAuth2ClientCredentialsFlow_NoSchemes_ReturnsNull()
    {
        var doc = new OpenApiDocument();

        Assert.Null(doc.GetOAuth2ClientCredentialsFlow());
    }

    [Fact]
    public void GetOAuth2ClientCredentialsFlow_WithFlow_ReturnsFlowInfo()
    {
        var doc = ParseYaml(YamlWithOAuth2ClientCredentials);
        Assert.NotNull(doc);

        var result = doc!.GetOAuth2ClientCredentialsFlow();

        Assert.NotNull(result);
        Assert.NotNull(result!.TokenUrl);
        Assert.Contains(
            "token",
            result.TokenUrl!,
            StringComparison.OrdinalIgnoreCase);
    }

    // ========== GetOAuth2AuthorizationCodeFlow Tests ==========
    [Fact]
    public void GetOAuth2AuthorizationCodeFlow_NoSchemes_ReturnsNull()
    {
        var doc = new OpenApiDocument();

        Assert.Null(doc.GetOAuth2AuthorizationCodeFlow());
    }

    [Fact]
    public void GetOAuth2AuthorizationCodeFlow_WithFlow_ReturnsFlowInfo()
    {
        var doc = ParseYaml(YamlWithOAuth2AuthorizationCode);
        Assert.NotNull(doc);

        var result = doc!.GetOAuth2AuthorizationCodeFlow();

        Assert.NotNull(result);
        Assert.NotNull(result!.AuthorizationUrl);
        Assert.NotNull(result.TokenUrl);
    }

    // ========== GetAllOAuth2Scopes Tests ==========
    [Fact]
    public void GetAllOAuth2Scopes_NoSchemes_ReturnsEmpty()
    {
        var doc = new OpenApiDocument();

        var result = doc.GetAllOAuth2Scopes();

        Assert.Empty(result);
    }

    [Fact]
    public void GetAllOAuth2Scopes_WithScopes_ReturnsScopesDictionary()
    {
        var doc = ParseYaml(YamlWithOAuth2ClientCredentials);
        Assert.NotNull(doc);

        var result = doc!.GetAllOAuth2Scopes();

        Assert.NotEmpty(result);
        Assert.True(result.ContainsKey("read:pets"));
    }

    // ========== HasOperationsRequiringOAuth2 Tests ==========
    [Fact]
    public void HasOperationsRequiringOAuth2_NoSecurity_ReturnsFalse()
    {
        var doc = ParseYaml(YamlWithNoSecurity);
        Assert.NotNull(doc);

        Assert.False(doc!.HasOperationsRequiringOAuth2());
    }

    [Fact]
    public void HasOperationsRequiringOAuth2_WithOAuth2DocSecurity_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOAuth2DocumentSecurity);
        Assert.NotNull(doc);

        Assert.True(doc!.HasOperationsRequiringOAuth2());
    }

    // ========== ExtractUnifiedSecurityConfiguration Tests ==========
    [Fact]
    public void ExtractUnifiedSecurityConfiguration_NoSecurity_ReturnsNoneSource()
    {
        var doc = ParseYaml(YamlWithNoSecurity);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractUnifiedSecurityConfiguration(
            pathItem,
            doc);

        Assert.Equal(SecuritySource.None, result.Source);
        Assert.False(result.AuthenticationRequired);
    }

    [Fact]
    public void ExtractUnifiedSecurityConfiguration_WithDocSecurity_ReturnsOpenApiSource()
    {
        var doc = ParseYaml(YamlWithDocumentLevelSecurity);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractUnifiedSecurityConfiguration(
            pathItem,
            doc);

        Assert.Equal(SecuritySource.OpenApiSecuritySchemes, result.Source);
        Assert.True(result.AuthenticationRequired);
    }

    // ========== ATC Extension Extraction Tests ==========
    [Fact]
    public void ExtractAuthenticationRequired_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractAuthenticationRequired());
    }

    [Fact]
    public void ExtractAuthorizeRoles_NullExtensions_ReturnsEmpty()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Empty(extensions.ExtractAuthorizeRoles());
    }

    [Fact]
    public void ExtractAuthenticationSchemes_NullExtensions_ReturnsEmpty()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Empty(extensions.ExtractAuthenticationSchemes());
    }

    // ========== ATC Extension YAML-Based Tests ==========
    [Fact]
    public void ExtractSecurityConfiguration_WithAtcAuthRequired_ReturnsConfig()
    {
        var doc = ParseYaml(YamlWithAtcAuthRequired);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractSecurityConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.True(result!.Value.AuthRequired);
    }

    [Fact]
    public void ExtractSecurityConfiguration_NoSecurity_ReturnsNull()
    {
        var doc = ParseYaml(YamlWithNoSecurity);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractSecurityConfiguration(
            pathItem,
            doc);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractSecurityConfiguration_WithAllowAnonymous_ReturnsAnonymousFlag()
    {
        var doc = ParseYaml(YamlWithAtcAllowAnonymous);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractSecurityConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.True(result!.Value.AllowAnonymous);
        Assert.False(result.Value.AuthRequired);
    }

    [Fact]
    public void ExtractSecurityConfiguration_InheritsFromDocument()
    {
        var doc = ParseYaml(YamlWithAtcDocLevelAuth);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractSecurityConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.True(result!.Value.AuthRequired);
    }

    // ========== Helper Methods ==========
    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(
            yaml,
            "test.yaml",
            out var document)
            ? document
            : null;

    private static OpenApiPathItem GetFirstPathItem(OpenApiDocument doc)
        => (OpenApiPathItem)doc.Paths.First().Value;

    private static OpenApiOperation GetFirstOperation(OpenApiPathItem pathItem)
        => pathItem.Operations.First().Value;

    // ========== YAML Test Data ==========
    private const string YamlWithNoSecurity = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithJwtBearer = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        components:
          securitySchemes:
            bearerAuth:
              type: http
              scheme: bearer
              bearerFormat: JWT
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithApiKey = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        components:
          securitySchemes:
            apiKeyAuth:
              type: apiKey
              in: header
              name: X-API-Key
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithOAuth2ClientCredentials = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        components:
          securitySchemes:
            oauth2:
              type: oauth2
              flows:
                clientCredentials:
                  tokenUrl: https://auth.example.com/token
                  scopes:
                    read:pets: Read pets
                    write:pets: Write pets
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithOAuth2AuthorizationCode = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        components:
          securitySchemes:
            oauth2:
              type: oauth2
              flows:
                authorizationCode:
                  authorizationUrl: https://auth.example.com/authorize
                  tokenUrl: https://auth.example.com/token
                  scopes:
                    read:pets: Read pets
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithOpenIdConnect = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        components:
          securitySchemes:
            oidc:
              type: openIdConnect
              openIdConnectUrl: https://auth.example.com/.well-known/openid-configuration
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithDocumentLevelSecurity = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        components:
          securitySchemes:
            bearerAuth:
              type: http
              scheme: bearer
              bearerFormat: JWT
        security:
          - bearerAuth: []
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithOAuth2DocumentSecurity = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        components:
          securitySchemes:
            oauth2:
              type: oauth2
              flows:
                clientCredentials:
                  tokenUrl: https://auth.example.com/token
                  scopes:
                    read:pets: Read pets
        security:
          - oauth2:
            - read:pets
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithAtcAuthRequired = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              x-authentication-required: true
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithAtcAllowAnonymous = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-authentication-required: true
        paths:
          /pets:
            get:
              operationId: getPets
              x-authentication-required: false
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithAtcDocLevelAuth = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-authentication-required: true
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;
}