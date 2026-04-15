namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class OperationFeaturesHelperTests
{
    [Fact]
    public void DetectFeatures_GetWithPathParam_DetectsParameters()
    {
        var yaml = """
                   openapi: 3.0.0
                   info:
                     title: Test
                     version: 1.0.0
                   paths:
                     /pets/{petId}:
                       get:
                         operationId: getPetById
                         parameters:
                           - name: petId
                             in: path
                             required: true
                             schema:
                               type: integer
                         responses:
                           '200':
                             description: OK
                   """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/pets/{petId}"];
        var operation = pathItem.Operations.Values.First();

        var features = OperationFeaturesHelper.DetectOperationFeatures(
            operation,
            (OpenApiPathItem)pathItem,
            document,
            "GET");

        Assert.True(features.HasParameters);
        Assert.True(features.HasPathParameters);
        Assert.Equal("GET", features.HttpMethod);
    }

    [Fact]
    public void DetectFeatures_PostWithBody_DetectsParameters()
    {
        var yaml = """
                   openapi: 3.0.0
                   info:
                     title: Test
                     version: 1.0.0
                   paths:
                     /pets:
                       post:
                         operationId: createPet
                         requestBody:
                           required: true
                           content:
                             application/json:
                               schema:
                                 type: object
                                 properties:
                                   name:
                                     type: string
                         responses:
                           '201':
                             description: Created
                   """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/pets"];
        var operation = pathItem.Operations.Values.First();

        var features = OperationFeaturesHelper.DetectOperationFeatures(
            operation,
            (OpenApiPathItem)pathItem,
            document,
            "POST");

        Assert.True(features.HasParameters);
        Assert.False(features.HasPathParameters);
    }

    [Fact]
    public void DetectFeatures_WithSecurity_DetectsAuth()
    {
        var yaml = """
                   openapi: 3.0.0
                   info:
                     title: Test
                     version: 1.0.0
                   components:
                     securitySchemes:
                       BearerAuth:
                         type: http
                         scheme: bearer
                   security:
                     - BearerAuth: []
                   paths:
                     /protected:
                       get:
                         operationId: getProtected
                         responses:
                           '200':
                             description: OK
                   """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/protected"];
        var operation = pathItem.Operations.Values.First();

        var features = OperationFeaturesHelper.DetectOperationFeatures(
            operation,
            (OpenApiPathItem)pathItem,
            document,
            "GET");

        Assert.True(features.HasSecurity);
    }

    [Fact]
    public void DetectFeatures_NoParamsNoSecurity_AllFalse()
    {
        var yaml = """
                   openapi: 3.0.0
                   info:
                     title: Test
                     version: 1.0.0
                   paths:
                     /health:
                       get:
                         operationId: getHealth
                         responses:
                           '200':
                             description: OK
                   """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/health"];
        var operation = pathItem.Operations.Values.First();

        var features = OperationFeaturesHelper.DetectOperationFeatures(
            operation,
            (OpenApiPathItem)pathItem,
            document,
            "GET");

        Assert.False(features.HasParameters);
        Assert.False(features.HasPathParameters);
        Assert.False(features.HasSecurity);
        Assert.False(features.HasRolesOrPolicies);
        Assert.False(features.HasRateLimiting);
    }
}