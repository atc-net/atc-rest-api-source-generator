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
    [InlineData("/account", "Accounts")] // "account" -> "Accounts" (pluralized)
    [InlineData("/category", "Categories")] // "category" -> "Categories" (y -> ies)
    [InlineData("/box", "Boxes")] // "box" -> "Boxes" (x -> xes)
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
        Assert.Equal("PetStores", result);
    }

    [Fact]
    public void GetFirstPathSegment_SnakeCase_ConvertsToPascalCase()
    {
        var result = PathSegmentHelper.GetFirstPathSegment("/pet_store");
        Assert.Equal("PetStores", result);
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
openapi: 3.0.3
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
openapi: 3.0.3
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
openapi: 3.0.3
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
openapi: 3.0.3
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