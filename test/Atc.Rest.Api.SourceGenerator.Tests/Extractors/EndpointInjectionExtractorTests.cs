namespace Atc.Rest.Api.SourceGenerator.Tests.Extractors;

/// <summary>
/// The endpoints a class marked for injection receives: one per operation, named after its operationId, grouped by
/// the path's first segment, and the partial class that takes them in its constructor.
/// </summary>
public class EndpointInjectionExtractorTests
{
    [Fact]
    public void Discover_DocumentWithoutPaths_FindsNothing()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths: {}
            """);

        Assert.Empty(EndpointInjectionExtractor.DiscoverEndpointsFromOpenApi(document, "Contoso"));
    }

    [Fact]
    public void Discover_WebhooksOnlyDocument_FindsNothing()
    {
        // OpenAPI 3.1 lets a document have no paths at all
        var document = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: 3.1.0
            info: { title: Contoso, version: 1.0.0 }
            webhooks:
              petAdded:
                post:
                  operationId: petAdded
                  responses: { '200': { description: OK } }
            """);

        Assert.Empty(EndpointInjectionExtractor.DiscoverEndpointsFromOpenApi(document, "Contoso"));
    }

    [Fact]
    public void Discover_DocumentMadeInCodeWithoutPaths_FindsNothing()
        => Assert.Empty(EndpointInjectionExtractor.DiscoverEndpointsFromOpenApi(new OpenApiDocument { Paths = null }, "Contoso"));

    [Fact]
    public void Discover_EveryOperation_IsAnEndpointNamedAfterItsOperationId()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /users:
                get:
                  operationId: listUsers
                  responses: { '200': { description: OK } }
              /pets/{petId}:
                get:
                  operationId: getPetById
                  responses: { '200': { description: OK } }
                delete:
                  operationId: deletePet
                  responses: { '204': { description: OK } }
              /pets:
                post:
                  operationId: createPet
                  responses: { '201': { description: OK } }
            """);

        var endpoints = EndpointInjectionExtractor.DiscoverEndpointsFromOpenApi(document, "Contoso");

        // Sorted by path segment, then by interface name
        Assert.Equal(
            [
                new EndpointInfo("ICreatePetEndpoint", "Pets", "createPetEndpoint", "Contoso.Generated.Pets.Endpoints.Interfaces"),
                new EndpointInfo("IDeletePetEndpoint", "Pets", "deletePetEndpoint", "Contoso.Generated.Pets.Endpoints.Interfaces"),
                new EndpointInfo("IGetPetByIdEndpoint", "Pets", "getPetByIdEndpoint", "Contoso.Generated.Pets.Endpoints.Interfaces"),
                new EndpointInfo("IListUsersEndpoint", "Users", "listUsersEndpoint", "Contoso.Generated.Users.Endpoints.Interfaces"),
            ],
            endpoints);
    }

    [Theory]
    [InlineData("list-pets", "IListPetsEndpoint", "listPetsEndpoint")]
    [InlineData("list_pets", "IListPetsEndpoint", "listPetsEndpoint")]
    [InlineData("ListPets", "IListPetsEndpoint", "listPetsEndpoint")]
    [InlineData("x", "IXEndpoint", "xEndpoint")]
    public void Discover_OperationIdOfAnyCasing_IsPascalCaseForTheInterface_AndCamelCaseForTheField(
        string operationId,
        string expectedInterface,
        string expectedField)
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            $$"""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: {{operationId}}
                  responses: { '200': { description: OK } }
            """);

        var endpoint = Assert.Single(EndpointInjectionExtractor.DiscoverEndpointsFromOpenApi(document, "Contoso"));

        Assert.Equal(expectedInterface, endpoint.InterfaceName);
        Assert.Equal(expectedField, endpoint.FieldName);
    }

    [Fact]
    public void Discover_OperationWithoutAnOperationId_IsNamedAfterItsMethodAndPath()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /pets:
                get:
                  responses: { '200': { description: OK } }
            """);
        var operation = document.Paths["/pets"].Operations![HttpMethod.Get];
        var expected = OperationIdHelper.Effective(operation, "GET", "/pets").ToPascalCaseForDotNet();

        var endpoint = Assert.Single(EndpointInjectionExtractor.DiscoverEndpointsFromOpenApi(document, "Contoso"));

        Assert.False(string.IsNullOrEmpty(expected));
        Assert.Equal($"I{expected}Endpoint", endpoint.InterfaceName);
    }

    [Theory]
    [InlineData("/api/v1/pets", "Pets")]
    [InlineData("/{tenant}/orders", "Orders")]
    [InlineData("/", "Default")]
    public void Discover_PathSegment_IsTheFirstMeaningfulSegment(
        string path,
        string expectedSegment)
    {
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                [path] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation { OperationId = "run" },
                    },
                },
            },
        };

        var endpoint = Assert.Single(EndpointInjectionExtractor.DiscoverEndpointsFromOpenApi(document, "Contoso"));

        // The one segment of a specification is no part of the namespace: the client declares its interfaces in
        // Contoso.Generated.Endpoints.Interfaces (RootNamespaceResolutionTests compares with the client's output)
        Assert.Equal(expectedSegment, endpoint.PathSegment);
        Assert.Equal("Contoso.Generated.Endpoints.Interfaces", endpoint.FullNamespace);
    }

    [Fact]
    public void Discover_PathItemOrOperationThatIsNull_IsSkipped()
    {
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/broken"] = null,
                ["/empty"] = new OpenApiPathItem { Operations = null },
                ["/pets"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = null,
                        [HttpMethod.Post] = new OpenApiOperation { OperationId = "createPet" },
                    },
                },
            },
        };

        var endpoint = Assert.Single(EndpointInjectionExtractor.DiscoverEndpointsFromOpenApi(document, "Contoso"));

        Assert.Equal("ICreatePetEndpoint", endpoint.InterfaceName);
    }

    [Fact]
    public void GeneratePartialClass_TakesEveryEndpointInItsConstructor_GroupedBySegment()
    {
        List<EndpointInfo> endpoints =
        [
            new("ICreatePetEndpoint", "Pets", "createPetEndpoint", "Contoso.Generated.Pets.Endpoints.Interfaces"),
            new("IGetPetEndpoint", "Pets", "getPetEndpoint", "Contoso.Generated.Pets.Endpoints.Interfaces"),
            new("IListUsersEndpoint", "Users", "listUsersEndpoint", "Contoso.Generated.Users.Endpoints.Interfaces"),
        ];

        var code = Normalize(EndpointInjectionExtractor.GeneratePartialClass("Contoso.Host", "Program", endpoints));

        Assert.Equal(
            Normalize("""
            // <auto-generated />
            #nullable enable

            using Contoso.Generated.Pets.Endpoints.Interfaces;
            using Contoso.Generated.Users.Endpoints.Interfaces;

            namespace Contoso.Host;

            public sealed partial class Program
            {
                // Pets endpoints
                private readonly ICreatePetEndpoint createPetEndpoint;
                private readonly IGetPetEndpoint getPetEndpoint;

                // Users endpoints
                private readonly IListUsersEndpoint listUsersEndpoint;

                public Program(
                    // Pets
                    ICreatePetEndpoint createPetEndpoint,
                    IGetPetEndpoint getPetEndpoint,
                    // Users
                    IListUsersEndpoint listUsersEndpoint)
                {
                    // Pets
                    this.createPetEndpoint = createPetEndpoint;
                    this.getPetEndpoint = getPetEndpoint;

                    // Users
                    this.listUsersEndpoint = listUsersEndpoint;
                }
            }

            """),
            code);
        Assert.Empty(CSharpSyntaxTree.ParseText(code, cancellationToken: TestContext.Current.CancellationToken).GetDiagnostics(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GeneratePartialClass_OneEndpoint_ClosesTheConstructorAfterIt()
    {
        List<EndpointInfo> endpoints = [new("IPingEndpoint", "Ping", "pingEndpoint", "Contoso.Generated.Ping.Endpoints.Interfaces")];

        var code = Normalize(EndpointInjectionExtractor.GeneratePartialClass("Contoso.Host", "Startup", endpoints, "internal partial"));

        Assert.Contains("internal partial class Startup\n", code, StringComparison.Ordinal);
        Assert.Contains("        IPingEndpoint pingEndpoint)\n    {\n", code, StringComparison.Ordinal);
        Assert.EndsWith("        this.pingEndpoint = pingEndpoint;\n    }\n}\n", code, StringComparison.Ordinal);
        Assert.Empty(CSharpSyntaxTree.ParseText(code, cancellationToken: TestContext.Current.CancellationToken).GetDiagnostics(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GeneratePartialClass_SegmentsOutOfOrder_AreGroupedInTheOrderTheyFirstAppear_WithOneUsingEach()
    {
        List<EndpointInfo> endpoints =
        [
            new("IListUsersEndpoint", "Users", "listUsersEndpoint", "Contoso.Generated.Users.Endpoints.Interfaces"),
            new("IGetPetEndpoint", "Pets", "getPetEndpoint", "Contoso.Generated.Pets.Endpoints.Interfaces"),
            new("IGetUserEndpoint", "Users", "getUserEndpoint", "Contoso.Generated.Users.Endpoints.Interfaces"),
        ];

        var code = Normalize(EndpointInjectionExtractor.GeneratePartialClass("Contoso.Host", "Program", endpoints));

        Assert.Single(code.Split('\n'), l => l == "using Contoso.Generated.Users.Endpoints.Interfaces;");
        Assert.True(
            code.IndexOf("// Users endpoints", StringComparison.Ordinal) < code.IndexOf("// Pets endpoints", StringComparison.Ordinal),
            code);

        // Every parameter but the last ends with a comma, wherever its group is
        Assert.Contains("        IGetUserEndpoint getUserEndpoint,\n", code, StringComparison.Ordinal);
        Assert.Contains("        IGetPetEndpoint getPetEndpoint)\n", code, StringComparison.Ordinal);
        Assert.Empty(CSharpSyntaxTree.ParseText(code, cancellationToken: TestContext.Current.CancellationToken).GetDiagnostics(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DiscoverThenGenerate_ParsesAsCSharp()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  responses: { '200': { description: OK } }
              /stores/{storeId}/orders:
                post:
                  operationId: placeOrder
                  responses: { '201': { description: OK } }
            """);

        var code = EndpointInjectionExtractor.GeneratePartialClass(
            "Contoso.Host",
            "Program",
            EndpointInjectionExtractor.DiscoverEndpointsFromOpenApi(document, "Contoso"));

        Assert.Contains("IPlaceOrderEndpoint placeOrderEndpoint)", code, StringComparison.Ordinal);
        Assert.Empty(CSharpSyntaxTree.ParseText(code, cancellationToken: TestContext.Current.CancellationToken).GetDiagnostics(TestContext.Current.CancellationToken));
    }

    private static string Normalize(string code)
        => code.Replace("\r\n", "\n", StringComparison.Ordinal);
}