namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A <c>security</c> requirement names a scheme under <c>components/securitySchemes</c>; one that names a scheme the
/// document does not declare (a typo: <c>petsstore_auth</c> for <c>petstore_auth</c>) makes the generated endpoint
/// require an authentication scheme no host registers, so every request on it fails at run time. The validator
/// reports it (<c>ATC_API_SEC012</c>) under every strategy, naming the requirement and the declared schemes.
/// </summary>
[RuleTest("ATC_API_SEC012")]
public class SecuritySchemeReferenceValidatorTests
{
    private const string TestFilePath = "security-refs.yaml";

    private const string Head = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        components:
          securitySchemes:
            petstore_auth:
              type: oauth2
              flows:
                implicit:
                  authorizationUrl: https://contoso.example/oauth/authorize
                  scopes:
                    read:pets: Read the pets
            api_key:
              type: apiKey
              in: header
              name: api_key

        """;

    [Theory]
    [InlineData(ValidateSpecificationStrategy.None)]
    [InlineData(ValidateSpecificationStrategy.Standard)]
    [InlineData(ValidateSpecificationStrategy.Strict)]
    public void Validate_AnOperationRequirementNamingAnUndeclaredScheme_IsAnErrorUnderEveryStrategy(
        ValidateSpecificationStrategy strategy)
    {
        var document = Parse(
            """
            paths:
              /pets:
                get:
                  operationId: listPets
                  security:
                    - petsstore_auth: ['read:pets']
                  responses:
                    '200': { description: OK }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

        var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.SecuritySchemeNotDeclared);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("petsstore_auth", error.Message, StringComparison.Ordinal);
        Assert.Contains("listPets", error.Message, StringComparison.Ordinal);
        Assert.Contains("petstore_auth", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ARootRequirementNamingAnUndeclaredScheme_IsAnError()
    {
        var document = Parse(
            """
            security:
              - bearer: []
            paths:
              /pets:
                get:
                  operationId: listPets
                  responses:
                    '200': { description: OK }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.None, document, [], TestFilePath);

        var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.SecuritySchemeNotDeclared);
        Assert.Contains("'bearer'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RequirementsNamingDeclaredSchemes_PassUnderStrict()
    {
        var document = Parse(
            """
            security:
              - api_key: []
            paths:
              /pets:
                get:
                  operationId: listPets
                  security:
                    - petstore_auth: ['read:pets']
                    - api_key: []
                  responses:
                    '200': { description: OK }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.SecuritySchemeNotDeclared);
    }

    private static OpenApiDocument Parse(string body)
    {
        var (document, diagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(Head + body, TestFilePath + body.Length);
        Assert.True(document is not null, string.Join("; ", (diagnostic?.Errors ?? []).Select(e => e.Message)));
        return document!;
    }
}