namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class HttpClientInterfaceExtractorTests
{
    private const string PetsYaml = """
                                    openapi: 3.0.0
                                    info:
                                      title: Test API
                                      version: 1.0.0
                                    paths:
                                      /pets:
                                        get:
                                          operationId: listPets
                                          responses:
                                            '200':
                                              description: OK
                                              content:
                                                application/json:
                                                  schema:
                                                    type: array
                                                    items:
                                                      $ref: '#/components/schemas/Pet'
                                      /pets/{petId}:
                                        get:
                                          operationId: getPet
                                          parameters:
                                            - name: petId
                                              in: path
                                              required: true
                                              schema:
                                                type: string
                                          responses:
                                            '200':
                                              description: OK
                                              content:
                                                application/json:
                                                  schema:
                                                    $ref: '#/components/schemas/Pet'
                                    components:
                                      schemas:
                                        Pet:
                                          type: object
                                          properties:
                                            id:
                                              type: string
                                            name:
                                              type: string
                                    """;

    [Theory]
    [InlineData("PetsClient", "IPetsClient")]
    [InlineData("TestApiClient", "ITestApiClient")]
    public void BuildInterfaceName_ClientTypeName_IsPrefixedWithI(
        string clientTypeName,
        string expected)
    {
        Assert.Equal(expected, HttpClientInterfaceExtractor.BuildInterfaceName(clientTypeName));
    }

    [Fact]
    public void Extract_NullClientClass_ReturnsNull()
    {
        Assert.Null(HttpClientInterfaceExtractor.Extract(null));
    }

    [Fact]
    public void Extract_ClientClass_ProjectsOnlyPublicOperationMethodsWithoutBodies()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PetsYaml);
        var clientClass = HttpClientExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);
        Assert.NotNull(clientClass);

        var result = HttpClientInterfaceExtractor.Extract(clientClass);

        Assert.NotNull(result);
        Assert.Equal("ITestApiClient", result.InterfaceTypeName);
        Assert.Equal(clientClass.Namespace, result.Namespace);
        Assert.Equal(DeclarationModifiers.PublicInterface, result.DeclarationModifier);
        Assert.NotNull(result.Attributes);
        Assert.Contains(result.Attributes, a => a.Name == "GeneratedCode");

        // The private EnsureSuccessAsync helper on the class is an implementation detail and stays off the interface.
        Assert.Contains(clientClass.Methods!, m => m.Name == "EnsureSuccessAsync");
        Assert.NotNull(result.Methods);
        Assert.Equal(2, result.Methods.Count);
        Assert.DoesNotContain(result.Methods, m => m.Name == "EnsureSuccessAsync");

        var getPet = result.Methods.Single(m => m.Name == "GetPetAsync");
        var classGetPet = clientClass.Methods!.Single(m => m.Name == "GetPetAsync");
        Assert.Equal(DeclarationModifiers.None, getPet.DeclarationModifier);
        Assert.Null(getPet.Content);
        Assert.Null(getPet.Attributes);
        Assert.False(getPet.UseExpressionBody);
        Assert.Equal(classGetPet.ReturnTypeName, getPet.ReturnTypeName);
        Assert.Equal(classGetPet.ReturnGenericTypeName, getPet.ReturnGenericTypeName);

        // Parameters are kept (same count and names) but carry no attributes on an interface member.
        Assert.NotNull(getPet.Parameters);
        Assert.Equal(classGetPet.Parameters!.Count, getPet.Parameters.Count);
        Assert.Equal(classGetPet.Parameters.Select(p => p.Name), getPet.Parameters.Select(p => p.Name), StringComparer.Ordinal);
        Assert.All(getPet.Parameters, p => Assert.Null(p.Attributes));
    }
}