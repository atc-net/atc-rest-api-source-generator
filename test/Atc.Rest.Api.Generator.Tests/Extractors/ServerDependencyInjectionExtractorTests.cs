namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class ServerDependencyInjectionExtractorTests
{
    private const string PetsAndUsersYaml = """
                                            openapi: 3.0.0
                                            info:
                                              title: Test
                                              version: 1.0.0
                                            paths:
                                              /pets:
                                                get:
                                                  operationId: listPets
                                                  responses:
                                                    '200':
                                                      description: OK
                                                post:
                                                  operationId: createPet
                                                  responses:
                                                    '201':
                                                      description: Created
                                              /users:
                                                get:
                                                  operationId: listUsers
                                                  deprecated: true
                                                  responses:
                                                    '200':
                                                      description: OK
                                            """;

    [Fact]
    public void Extract_WithOperations_ListsHandlerRegistrationsSortedByInterface()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PetsAndUsersYaml);

        var result = ServerDependencyInjectionExtractor.Extract(document, "Contoso.PetApi", pathSegments: null);

        Assert.NotNull(result);
        Assert.Equal("Contoso.PetApi.Generated.DependencyInjection", result.Namespace);
        Assert.Equal("ServiceCollectionExtensions", result.ClassTypeName);
        Assert.Equal(DeclarationModifiers.PublicStaticClass, result.DeclarationModifier);
        Assert.Contains("using Microsoft.Extensions.DependencyInjection;", result.HeaderContent, StringComparison.Ordinal);
        Assert.Contains("using Contoso.PetApi.Generated.Handlers;", result.HeaderContent, StringComparison.Ordinal);

        var method = Assert.Single(result.Methods!);
        Assert.Equal("AddPetApiHandlers", method.Name);
        Assert.Equal("IServiceCollection", method.ReturnTypeName);
        var parameter = Assert.Single(method.Parameters!);
        Assert.Equal("this IServiceCollection", parameter.TypeName);
        Assert.Equal("services", parameter.Name);

        var content = method.Content!;
        Assert.Contains("// services.AddScoped<ICreatePetHandler, YourImplementation>();", content, StringComparison.Ordinal);
        Assert.Contains("// services.AddScoped<IListPetsHandler, YourImplementation>();", content, StringComparison.Ordinal);
        Assert.True(
            content.IndexOf("ICreatePetHandler", StringComparison.Ordinal) < content.IndexOf("IListPetsHandler", StringComparison.Ordinal),
            "Handler registrations are sorted by interface name");
        Assert.EndsWith("return services;", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_DeprecatedOperation_SkippedUnlessIncluded()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PetsAndUsersYaml);

        var excluded = ServerDependencyInjectionExtractor.Extract(document, "Contoso.PetApi", pathSegments: null);
        var included = ServerDependencyInjectionExtractor.Extract(document, "Contoso.PetApi", pathSegments: null, includeDeprecated: true);

        Assert.DoesNotContain("IListUsersHandler", excluded!.Methods![0].Content, StringComparison.Ordinal);
        Assert.Contains("// services.AddScoped<IListUsersHandler, YourImplementation>();", included!.Methods![0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_NoPaths_ReturnsNull()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            """);

        Assert.Null(ServerDependencyInjectionExtractor.Extract(document, "Contoso.PetApi", pathSegments: null));
        Assert.Null(ServerDependencyInjectionExtractor.Extract(document, "Contoso.PetApi", ["Pets"]));
    }

    [Fact]
    public void Extract_WithPathSegments_EmitsOneHandlerUsingPerDistinctSegment()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PetsAndUsersYaml);

        var result = ServerDependencyInjectionExtractor.Extract(
            document,
            "Contoso.PetApi",
            ["Users", "Pets", "Pets"],
            includeDeprecated: true);

        Assert.NotNull(result);
        var header = result.HeaderContent!;
        Assert.DoesNotContain("using Contoso.PetApi.Generated.Handlers;", header, StringComparison.Ordinal);
        Assert.Contains("using Contoso.PetApi.Generated.Pets.Handlers;", header, StringComparison.Ordinal);
        Assert.Contains("using Contoso.PetApi.Generated.Users.Handlers;", header, StringComparison.Ordinal);
        Assert.Equal(1, header.Split("using Contoso.PetApi.Generated.Pets.Handlers;").Length - 1);
        Assert.True(
            header.IndexOf("Pets.Handlers", StringComparison.Ordinal) < header.IndexOf("Users.Handlers", StringComparison.Ordinal),
            "Handler usings are sorted");
    }

    [Fact]
    public void Extract_EmptyPathSegments_FallsBackToRootHandlersUsing()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PetsAndUsersYaml);

        var result = ServerDependencyInjectionExtractor.Extract(document, "Contoso.PetApi", []);

        Assert.Contains("using Contoso.PetApi.Generated.Handlers;", result!.HeaderContent, StringComparison.Ordinal);
    }
}