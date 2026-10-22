namespace Atc.Rest.Api.Generator.Tests.Models;

/// <summary>
/// What a server's specification uses, found once for AddApi, MapApi and their options. Access declared with Atc's
/// extensions alone runs the authentication middleware but registers no security policies: those come from a scheme.
/// </summary>
public sealed class ServerFeaturesTests
{
    [Theory]
    [InlineData("components: { securitySchemes: { bearer: { type: http, scheme: bearer } } }\nsecurity: [ { bearer: [] } ]", true, true)]
    [InlineData("x-authentication-required: true", false, true)]
    [InlineData("", false, false)]
    public void Of_Security_IsTheSchemesForThePolicies_AndEveryDeclarationForTheMiddleware(
        string declaration,
        bool hasSecuritySchemes,
        bool requiresAuthentication)
    {
        var document = OpenApiDocumentHelper.ParseYaml($"""
            openapi: 3.0.3
            info: {"{"} title: Contoso, version: 1.0.0 {"}"}
            {declaration}
            paths:
              /pets:
                get:
                  operationId: listPets
                  responses:
                    '200': {"{"} description: OK {"}"}
            """);

        var features = ServerFeatures.Of(document, new ServerConfig());

        Assert.Equal(hasSecuritySchemes, features.HasSecuritySchemes);
        Assert.Equal(requiresAuthentication, features.RequiresAuthentication);
    }

    [Fact]
    public void Of_WithoutAConfiguration_HasNoValidationFilterAndNoVersioning()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths: {}
            """);

        var features = ServerFeatures.Of(document, config: null);

        Assert.False(features.HasValidation);
        Assert.False(features.HasVersioning);
        Assert.False(features.HasRateLimiting);
    }
}