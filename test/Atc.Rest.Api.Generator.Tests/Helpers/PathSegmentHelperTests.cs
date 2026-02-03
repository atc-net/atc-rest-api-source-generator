namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class PathSegmentHelperTests
{
    // ========== GetFirstPathSegment Tests ==========
    [Theory]
    [InlineData("/pets", "Pets")]
    [InlineData("/pets/{petId}", "Pets")]
    [InlineData("/users", "Users")]
    [InlineData("/users/{userId}/orders", "Users")]
    [InlineData("/api/users", "Users")] // skip "api" prefix
    [InlineData("/api/v1/users", "Users")] // skip "api" and "v1"
    [InlineData("/api/v2/charge-points", "ChargePoints")] // skip "api" and "v2"
    [InlineData("/apis/accounts", "Accounts")] // skip "apis" prefix
    [InlineData("/v1/pets", "Pets")] // skip version segment only
    [InlineData("/v2/orders/{orderId}", "Orders")] // skip version segment
    [InlineData("/account", "Account")] // preserve singular form
    [InlineData("/category", "Category")] // preserve singular form
    [InlineData("/box", "Box")] // preserve singular form
    [InlineData("/admin", "Admin")] // preserve singular form (bug fix case)
    [InlineData("/admin/resend-events", "Admin")] // preserve singular form with nested path
    [InlineData("/{id}", "Default")] // Path parameter only
    [InlineData("/", "Default")] // Empty path
    [InlineData("", "Default")] // Empty string
    [InlineData("/api", "Default")] // Only "api" prefix
    [InlineData("/api/v1", "Default")] // Only prefix and version
    [InlineData("/api/v1/{id}", "Default")] // Prefix, version, and path parameter only
    public void GetFirstPathSegment_ReturnsExpectedResult(
        string path,
        string expected)
    {
        var result = PathSegmentHelper.GetFirstPathSegment(path);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetFirstPathSegment_KebabCase_ConvertsToPascalCase()
    {
        var result = PathSegmentHelper.GetFirstPathSegment("/pet-store");
        Assert.Equal("PetStore", result);
    }

    [Fact]
    public void GetFirstPathSegment_SnakeCase_ConvertsToPascalCase()
    {
        var result = PathSegmentHelper.GetFirstPathSegment("/pet_store");
        Assert.Equal("PetStore", result);
    }

    // ========== GetUniquePathSegments Tests ==========
    [Fact]
    public void GetUniquePathSegments_EmptyPaths_ReturnsEmptyList()
    {
        var doc = CreateOpenApiDocument();
        var result = PathSegmentHelper.GetUniquePathSegments(doc);
        Assert.Empty(result);
    }

    [Fact]
    public void GetUniquePathSegments_MultiplePaths_ReturnsUniqueSegments()
    {
        var doc = CreateOpenApiDocument(
            "/pets",
            "/pets/{petId}",
            "/users",
            "/users/{userId}");

        var result = PathSegmentHelper.GetUniquePathSegments(doc);

        Assert.Equal(2, result.Count);
        Assert.Contains("Pets", result);
        Assert.Contains("Users", result);
    }

    [Fact]
    public void GetUniquePathSegments_ReturnsAlphabeticallySorted()
    {
        var doc = CreateOpenApiDocument(
            "/zebras",
            "/accounts",
            "/pets");

        var result = PathSegmentHelper.GetUniquePathSegments(doc);

        Assert.Equal(3, result.Count);
        Assert.Equal("Accounts", result[0]);
        Assert.Equal("Pets", result[1]);
        Assert.Equal("Zebras", result[2]);
    }

    // ========== GetOperationsForSegment Tests ==========
    [Fact]
    public void GetOperationsForSegment_ReturnsOperationsForMatchingSegment()
    {
        var yaml = @"
openapi: 3.1.1
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
    post:
      operationId: createPet
      responses:
        '201':
          description: Created
  /users:
    get:
      operationId: listUsers
      responses:
        '200':
          description: OK
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.GetOperationsForSegment(doc, "Pets");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, op => op.Method == "GET");
        Assert.Contains(result, op => op.Method == "POST");
    }

    [Fact]
    public void GetOperationsForSegment_NoMatchingSegment_ReturnsEmptyList()
    {
        var yaml = @"
openapi: 3.1.1
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
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.GetOperationsForSegment(doc, "Users");

        Assert.Empty(result);
    }

    // ========== GetSchemasUsedBySegment Tests ==========
    [Fact]
    public void GetSchemasUsedBySegment_ReturnsReferencedSchemas()
    {
        var yaml = @"
openapi: 3.1.1
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
components:
  schemas:
    Pet:
      type: object
      properties:
        id:
          type: integer
        name:
          type: string
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.GetSchemasUsedBySegment(doc, "Pets");

        Assert.Contains("Pet", result);
    }

    [Fact]
    public void GetSchemasUsedBySegment_IncludesNestedSchemas()
    {
        var yaml = @"
openapi: 3.1.1
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
                $ref: '#/components/schemas/Pet'
components:
  schemas:
    Pet:
      type: object
      properties:
        id:
          type: integer
        owner:
          $ref: '#/components/schemas/Owner'
    Owner:
      type: object
      properties:
        name:
          type: string
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.GetSchemasUsedBySegment(doc, "Pets");

        Assert.Contains("Pet", result);
        Assert.Contains("Owner", result);
    }

    // ========== PathSegmentHasOperations Tests ==========
    [Fact]
    public void PathSegmentHasOperations_WithOperations_ReturnsTrue()
    {
        var yaml = @"
openapi: 3.1.1
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
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.PathSegmentHasOperations(doc, "Pets");

        Assert.True(result);
    }

    [Fact]
    public void PathSegmentHasOperations_NoOperationsForSegment_ReturnsFalse()
    {
        var yaml = @"
openapi: 3.1.1
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
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.PathSegmentHasOperations(doc, "Users");

        Assert.False(result);
    }

    [Fact]
    public void PathSegmentHasOperations_EmptyPaths_ReturnsFalse()
    {
        var doc = CreateOpenApiDocument();

        var result = PathSegmentHelper.PathSegmentHasOperations(doc, "Pets");

        Assert.False(result);
    }

    // ========== PathSegmentHasParameters Tests ==========
    [Fact]
    public void PathSegmentHasParameters_WithPathParameter_ReturnsTrue()
    {
        var yaml = @"
openapi: 3.1.1
info:
  title: Test API
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
            type: string
      responses:
        '200':
          description: OK
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.PathSegmentHasParameters(doc, "Pets");

        Assert.True(result);
    }

    [Fact]
    public void PathSegmentHasParameters_WithRequestBody_ReturnsTrue()
    {
        var yaml = @"
openapi: 3.1.1
info:
  title: Test API
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
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.PathSegmentHasParameters(doc, "Pets");

        Assert.True(result);
    }

    [Fact]
    public void PathSegmentHasParameters_NoParametersNoBody_ReturnsFalse()
    {
        // This is the key test - an endpoint with no parameters should not generate a Parameters namespace
        var yaml = @"
openapi: 3.1.1
info:
  title: Test API
  version: 1.0.0
paths:
  /systems/info:
    get:
      operationId: getSystemInfo
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                type: object
                properties:
                  uptime:
                    type: string
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.PathSegmentHasParameters(doc, "Systems");

        Assert.False(result);
    }

    [Fact]
    public void PathSegmentHasParameters_NoMatchingSegment_ReturnsFalse()
    {
        var yaml = @"
openapi: 3.1.1
info:
  title: Test API
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
            type: string
      responses:
        '200':
          description: OK
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.PathSegmentHasParameters(doc, "Users");

        Assert.False(result);
    }

    // ========== PathSegmentHasModels Tests ==========
    [Fact]
    public void PathSegmentHasModels_WithSegmentSpecificSchema_ReturnsTrue()
    {
        var yaml = @"
openapi: 3.1.1
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
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/Pet'
components:
  schemas:
    Pet:
      type: object
      properties:
        id:
          type: integer
        name:
          type: string
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.PathSegmentHasModels(doc, "Pets");

        Assert.True(result);
    }

    [Fact]
    public void PathSegmentHasModels_NoModels_ReturnsFalse()
    {
        var yaml = @"
openapi: 3.1.1
info:
  title: Test API
  version: 1.0.0
paths:
  /health:
    get:
      operationId: getHealth
      responses:
        '200':
          description: OK
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.PathSegmentHasModels(doc, "Health");

        Assert.False(result);
    }

    // ========== GetPathSegmentNamespaces Tests ==========
    [Fact]
    public void GetPathSegmentNamespaces_FullyPopulatedSegment_AllTrue()
    {
        var yaml = @"
openapi: 3.1.1
info:
  title: Test API
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
          type: integer
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.GetPathSegmentNamespaces(doc, "Pets");

        Assert.True(result.HasHandlers);
        Assert.True(result.HasResults);
        Assert.True(result.HasParameters);
        Assert.True(result.HasModels);
    }

    [Fact]
    public void GetPathSegmentNamespaces_OperationsWithoutParameters_NoParameters()
    {
        // This tests the exact scenario that caused the original bug -
        // a GET endpoint with no parameters should not have a Parameters namespace
        var yaml = @"
openapi: 3.1.1
info:
  title: Test API
  version: 1.0.0
paths:
  /systems/info:
    get:
      operationId: getSystemInfo
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/SystemInfo'
components:
  schemas:
    SystemInfo:
      type: object
      properties:
        uptime:
          type: string
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.GetPathSegmentNamespaces(doc, "Systems");

        Assert.True(result.HasHandlers, "Should have handlers for the GET operation");
        Assert.True(result.HasResults, "Should have results for the GET operation");
        Assert.False(result.HasParameters, "Should NOT have parameters since GET has no params");
        Assert.True(result.HasModels, "Should have models for SystemInfo");
    }

    [Fact]
    public void GetPathSegmentNamespaces_NonExistentSegment_AllFalse()
    {
        var yaml = @"
openapi: 3.1.1
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
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.GetPathSegmentNamespaces(doc, "Users");

        Assert.False(result.HasHandlers);
        Assert.False(result.HasResults);
        Assert.False(result.HasParameters);
        Assert.False(result.HasModels);
    }

    // ========== GetSegmentUsings Tests ==========
    [Fact]
    public void GetSegmentUsings_AllFlagsTrue_ReturnsAllUsings()
    {
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: true,
            HasParameters: true,
            HasModels: true);

        var result = PathSegmentHelper.GetSegmentUsings("MyProject", "Pets", namespaces).ToList();

        Assert.Equal(4, result.Count);
        Assert.Contains("using MyProject.Generated.Pets.Handlers;", result);
        Assert.Contains("using MyProject.Generated.Pets.Models;", result);
        Assert.Contains("using MyProject.Generated.Pets.Parameters;", result);
        Assert.Contains("using MyProject.Generated.Pets.Results;", result);
    }

    [Fact]
    public void GetSegmentUsings_AllFlagsFalse_ReturnsEmpty()
    {
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: false,
            HasResults: false,
            HasParameters: false,
            HasModels: false);

        var result = PathSegmentHelper.GetSegmentUsings("MyProject", "Pets", namespaces).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void GetSegmentUsings_WithGlobalUsing_UsesGlobalPrefix()
    {
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: true,
            HasParameters: false,
            HasModels: false);

        var result = PathSegmentHelper.GetSegmentUsings(
            "MyProject", "Pets", namespaces, isGlobalUsing: true).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains("global using MyProject.Generated.Pets.Handlers;", result);
        Assert.Contains("global using MyProject.Generated.Pets.Results;", result);
    }

    [Fact]
    public void GetSegmentUsings_ExcludeHandlers_OmitsHandlersUsing()
    {
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: true,
            HasParameters: true,
            HasModels: true);

        var result = PathSegmentHelper.GetSegmentUsings(
            "MyProject", "Pets", namespaces, includeHandlers: false).ToList();

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain("using MyProject.Generated.Pets.Handlers;", result);
        Assert.Contains("using MyProject.Generated.Pets.Models;", result);
        Assert.Contains("using MyProject.Generated.Pets.Parameters;", result);
        Assert.Contains("using MyProject.Generated.Pets.Results;", result);
    }

    [Fact]
    public void GetSegmentUsings_ExcludeModels_OmitsModelsUsing()
    {
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: true,
            HasParameters: true,
            HasModels: true);

        var result = PathSegmentHelper.GetSegmentUsings(
            "MyProject", "Pets", namespaces, includeModels: false).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains("using MyProject.Generated.Pets.Handlers;", result);
        Assert.DoesNotContain("using MyProject.Generated.Pets.Models;", result);
        Assert.Contains("using MyProject.Generated.Pets.Parameters;", result);
        Assert.Contains("using MyProject.Generated.Pets.Results;", result);
    }

    [Fact]
    public void GetSegmentUsings_NullPathSegment_UsesRootNamespace()
    {
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: true,
            HasParameters: false,
            HasModels: false);

        var result = PathSegmentHelper.GetSegmentUsings("MyProject", null, namespaces).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains("using MyProject.Generated.Handlers;", result);
        Assert.Contains("using MyProject.Generated.Results;", result);
    }

    [Fact]
    public void GetSegmentUsings_EmptyPathSegment_UsesRootNamespace()
    {
        var namespaces = new PathSegmentNamespaces(
            HasHandlers: true,
            HasResults: true,
            HasParameters: false,
            HasModels: false);

        var result = PathSegmentHelper.GetSegmentUsings("MyProject", "", namespaces).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains("using MyProject.Generated.Handlers;", result);
        Assert.Contains("using MyProject.Generated.Results;", result);
    }

    // ========== Helper Methods ==========
    private static OpenApiDocument CreateOpenApiDocument(params string[] paths)
    {
        var doc = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
            Paths = new OpenApiPaths(),
        };

        foreach (var path in paths)
        {
            doc.Paths.Add(path, new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = new OpenApiOperation
                    {
                        OperationId = "test",
                        Responses = new OpenApiResponses
                        {
                            ["200"] = new OpenApiResponse { Description = "OK" },
                        },
                    },
                },
            });
        }

        return doc;
    }
}