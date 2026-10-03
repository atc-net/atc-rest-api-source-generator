namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class PathSegmentHelperTests
{
    // Address is used by three segments (shared); Pet and User by one each (segment-specific).
    private const string TwoSegmentYaml = """
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
          /users:
            get:
              operationId: listUsers
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/User'
          /addresses/{addressId}:
            get:
              operationId: getAddress
              parameters:
                - name: addressId
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
                        $ref: '#/components/schemas/Address'
        components:
          schemas:
            Pet:
              type: object
              properties:
                id:
                  type: integer
                address:
                  $ref: '#/components/schemas/Address'
            User:
              type: object
              properties:
                name:
                  type: string
                address:
                  $ref: '#/components/schemas/Address'
            Address:
              type: object
              properties:
                street:
                  type: string
        """;

    private const string WebhookYaml = """
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
          /users:
            get:
              operationId: listUsers
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/User'
        webhooks:
          petShipped:
            post:
              operationId: onPetShipped
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      type: object
                      properties:
                        pet:
                          $ref: '#/components/schemas/Pet'
                        event:
                          $ref: '#/components/schemas/ShipmentEvent'
              responses:
                '200':
                  description: Acknowledged
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Address'
                '500':
                  description: Error
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ProblemDetails'
        components:
          schemas:
            Pet:
              type: object
              properties:
                id:
                  type: integer
                address:
                  $ref: '#/components/schemas/Address'
            User:
              type: object
              properties:
                name:
                  type: string
                address:
                  $ref: '#/components/schemas/Address'
            Address:
              type: object
              properties:
                street:
                  type: string
            ShipmentEvent:
              type: object
              properties:
                trackingNumber:
                  type: string
            ProblemDetails:
              type: object
              properties:
                detail:
                  type: string
        """;

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

    // ========== GetFirstPathSegment "api"/"apis" suffix word-boundary Tests ==========
    // Concatenated lowercase segments have no natural word-boundary signal for PascalCasing.
    // As a narrow, low-risk heuristic we split a trailing "api"/"apis" suffix from an otherwise
    // concatenated lowercase segment so common REST-style paths produce a readable segment.
    [Theory]
    [InlineData("/thirdpartyapi/authorization", "ThirdpartyApi")]
    [InlineData("/customerapi/meteringpoints", "CustomerApi")]
    [InlineData("/myapi", "MyApi")]
    [InlineData("/mediaapis", "MediaApis")]
    public void GetFirstPathSegment_ConcatenatedApiSuffix_InsertsWordBoundary(
        string path,
        string expected)
    {
        var result = PathSegmentHelper.GetFirstPathSegment(path);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/api", "Default")] // "api" alone is a skipped prefix, not a suffix case
    [InlineData("/apis", "Default")] // "apis" alone is a skipped prefix, not a suffix case
    [InlineData("/recipes", "Recipes")] // does not end with "api"/"apis" — unaffected
    [InlineData("/rapid", "Rapid")] // does not end with "api"/"apis" — unaffected
    public void GetFirstPathSegment_ConcatenatedApiSuffix_DoesNotAffectUnrelatedSegments(
        string path,
        string expected)
    {
        var result = PathSegmentHelper.GetFirstPathSegment(path);
        Assert.Equal(expected, result);
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
        Assert.Contains("Pets", result, StringComparer.Ordinal);
        Assert.Contains("Users", result, StringComparer.Ordinal);
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
        Assert.Equal("Accounts", result[0], StringComparer.Ordinal);
        Assert.Equal("Pets", result[1], StringComparer.Ordinal);
        Assert.Equal("Zebras", result[2], StringComparer.Ordinal);
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
    public void GetSchemasUsedBySegment_IncludesItemSchemaReferences()
    {
        // OpenAPI 3.2 itemSchema (streaming): the per-element type must be collected so the
        // model gets generated, even when referenced only via itemSchema. Regression test
        // for an itemSchema-only type producing a client/server that references an
        // undefined type (caught by CompilationVerificationTests).
        var yaml = @"
openapi: 3.2.0
info:
  title: Test API
  version: 1.0.0
paths:
  /events:
    get:
      operationId: streamEvents
      responses:
        '200':
          description: OK
          content:
            application/jsonl:
              itemSchema:
                $ref: '#/components/schemas/Event'
components:
  schemas:
    Event:
      type: object
      properties:
        id:
          type: string
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = PathSegmentHelper.GetSchemasUsedBySegment(doc, "Events");

        Assert.Contains("Event", result);
    }

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
        Assert.Contains("using MyProject.Generated.Pets.Handlers;", result, StringComparer.Ordinal);
        Assert.Contains("using MyProject.Generated.Pets.Models;", result, StringComparer.Ordinal);
        Assert.Contains("using MyProject.Generated.Pets.Parameters;", result, StringComparer.Ordinal);
        Assert.Contains("using MyProject.Generated.Pets.Results;", result, StringComparer.Ordinal);
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
        Assert.Contains("global using MyProject.Generated.Pets.Handlers;", result, StringComparer.Ordinal);
        Assert.Contains("global using MyProject.Generated.Pets.Results;", result, StringComparer.Ordinal);
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
        Assert.DoesNotContain("using MyProject.Generated.Pets.Handlers;", result, StringComparer.Ordinal);
        Assert.Contains("using MyProject.Generated.Pets.Models;", result, StringComparer.Ordinal);
        Assert.Contains("using MyProject.Generated.Pets.Parameters;", result, StringComparer.Ordinal);
        Assert.Contains("using MyProject.Generated.Pets.Results;", result, StringComparer.Ordinal);
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
        Assert.Contains("using MyProject.Generated.Pets.Handlers;", result, StringComparer.Ordinal);
        Assert.DoesNotContain("using MyProject.Generated.Pets.Models;", result, StringComparer.Ordinal);
        Assert.Contains("using MyProject.Generated.Pets.Parameters;", result, StringComparer.Ordinal);
        Assert.Contains("using MyProject.Generated.Pets.Results;", result, StringComparer.Ordinal);
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
        Assert.Contains("using MyProject.Generated.Handlers;", result, StringComparer.Ordinal);
        Assert.Contains("using MyProject.Generated.Results;", result, StringComparer.Ordinal);
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
        Assert.Contains("using MyProject.Generated.Handlers;", result, StringComparer.Ordinal);
        Assert.Contains("using MyProject.Generated.Results;", result, StringComparer.Ordinal);
    }

    // ========== ResolveEffectivePathSegment Tests (A1 + A2) ==========
    // A1: when a document has only one unique path segment, that segment adds no
    // organizational value and must be dropped (null) so generated types land directly
    // under "{root}.Generated.{Category}".
    [Fact]
    public void ResolveEffectivePathSegment_SingleUniqueSegment_ReturnsNull()
    {
        var doc = CreateOpenApiDocument(
            "/thirdpartyapi/api/authorization/authorizations",
            "/thirdpartyapi/api/isalive",
            "/thirdpartyapi/api/token");

        var result = PathSegmentHelper.ResolveEffectivePathSegment(doc, "Eloverblik.Api.ThirdPartyApi", "ThirdpartyApi");

        Assert.Null(result);
    }

    [Fact]
    public void ResolveEffectivePathSegment_MultipleUniqueSegments_KeepsSegment()
    {
        var doc = CreateOpenApiDocument(
            "/pets",
            "/stores",
            "/users");

        var result = PathSegmentHelper.ResolveEffectivePathSegment(doc, "PetStoreFull", "Pets");

        Assert.Equal("Pets", result, StringComparer.Ordinal);
    }

    // A2: when the segment merely echoes the last dot-part of the configured root
    // namespace it adds no value and is dropped — but only when dropping stays collision-free.
    [Fact]
    public void ResolveEffectivePathSegment_SegmentDuplicatesRootNamespaceTail_ReturnsNull()
    {
        var doc = CreateOpenApiDocument(
            "/thirdpartyapi/api/token",
            "/customerapi/api/token");

        var result = PathSegmentHelper.ResolveEffectivePathSegment(doc, "Eloverblik.Api.ThirdPartyApi", "ThirdpartyApi");

        Assert.Null(result);
    }

    [Fact]
    public void ResolveEffectivePathSegment_SegmentDoesNotDuplicateRootNamespaceTail_KeepsSegment()
    {
        var doc = CreateOpenApiDocument(
            "/thirdpartyapi/api/token",
            "/customerapi/api/token");

        var result = PathSegmentHelper.ResolveEffectivePathSegment(doc, "Eloverblik.Api.ThirdPartyApi", "CustomerApi");

        Assert.Equal("CustomerApi", result, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveEffectivePathSegment_NullOrEmptySegment_ReturnsNull()
    {
        var doc = CreateOpenApiDocument("/pets", "/stores");

        Assert.Null(PathSegmentHelper.ResolveEffectivePathSegment(doc, "MyProject", null));
        Assert.Null(PathSegmentHelper.ResolveEffectivePathSegment(doc, "MyProject", string.Empty));
    }

    // ========== Circular Schema Reference Tests ==========
    [Fact]
    public void GetSchemasUsedBySegment_CircularInlineSchema_DoesNotStackOverflow()
    {
        // Arrange — create a self-referencing inline schema (TreeNode with children: TreeNode[])
        // Build via YAML with $ref cycle, which the library resolves eagerly
        var yaml = @"
openapi: 3.1.1
info:
  title: Test API
  version: 1.0.0
paths:
  /trees:
    get:
      operationId: listTrees
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/TreeNode'
components:
  schemas:
    TreeNode:
      type: object
      properties:
        name:
          type: string
        children:
          type: array
          items:
            $ref: '#/components/schemas/TreeNode'
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act — should complete without StackOverflowException
        var result = PathSegmentHelper.GetSchemasUsedBySegment(doc, "Trees");

        // Assert — should find the TreeNode $ref
        Assert.Contains("TreeNode", result);
    }

    [Fact]
    public void GetSchemasUsedBySegment_DeeplyNestedSchema_CollectsAllRefNames()
    {
        // Arrange — a non-circular deep chain: response -> array -> object -> nested ref
        var yaml = @"
openapi: 3.1.1
info:
  title: Test API
  version: 1.0.0
paths:
  /orders:
    get:
      operationId: listOrders
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/Order'
components:
  schemas:
    Order:
      type: object
      properties:
        id:
          type: string
        items:
          type: array
          items:
            $ref: '#/components/schemas/OrderItem'
    OrderItem:
      type: object
      properties:
        name:
          type: string
";
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = PathSegmentHelper.GetSchemasUsedBySegment(doc, "Orders");

        // Assert — should find the $ref to Order
        Assert.Contains("Order", result);
    }

    // ========== GetSchemaSegmentMap / GetSharedSchemas / GetSegmentSpecificSchemas Tests ==========
    [Fact]
    public void GetSchemaSegmentMap_SchemaUsedByTwoSegments_MapsToBoth()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(TwoSegmentYaml);

        var result = PathSegmentHelper.GetSchemaSegmentMap(doc);

        Assert.Equal(3, result.Count);
        Assert.Equal(["Pets"], result["Pet"].OrderBy(s => s, StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["Users"], result["User"].OrderBy(s => s, StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["Addresses", "Pets", "Users"], result["Address"].OrderBy(s => s, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void GetSharedSchemas_ReturnsOnlySchemasUsedByMoreThanOneSegment()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(TwoSegmentYaml);

        var result = PathSegmentHelper.GetSharedSchemas(doc);

        Assert.Equal(["Address"], result);
    }

    [Fact]
    public void GetSharedSchemas_WebhookOnlySchema_IsShared()
    {
        // A webhook has no path segment, so the model it carries lands in the shared namespace
        var doc = OpenApiDocumentHelper.ParseYaml(WebhookYaml);

        var result = PathSegmentHelper.GetSharedSchemas(doc);

        Assert.Contains("ShipmentEvent", result);
        Assert.DoesNotContain("Pet", result);
    }

    [Fact]
    public void GetSegmentSpecificSchemas_WithSharedSet_ExcludesSharedSchemas()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(TwoSegmentYaml);
        var shared = new HashSet<string>(StringComparer.Ordinal) { "Address" };

        var result = PathSegmentHelper.GetSegmentSpecificSchemas(doc, "Pets", shared);

        Assert.Equal(["Pet"], result);
    }

    [Fact]
    public void GetSegmentSpecificSchemas_ArrayAliasComponent_IsNotAModel()
    {
        // "Pets: array of Pet" is replaced by List<Pet> wherever it is used - it is no generated type
        var doc = OpenApiDocumentHelper.ParseYaml("""
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
                            $ref: '#/components/schemas/Pets'
            components:
              schemas:
                Pets:
                  type: array
                  items:
                    $ref: '#/components/schemas/Pet'
                Pet:
                  type: object
                  properties:
                    id:
                      type: integer
            """);

        var result = PathSegmentHelper.GetSegmentSpecificSchemas(doc, "Pets", new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal(["Pet"], result);
    }

    // ========== GetSchemasUsedByWebhooks Tests ==========
    [Fact]
    public void GetSchemasUsedByWebhooks_CollectsRequestAndResponseSchemas()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(WebhookYaml);

        var result = PathSegmentHelper.GetSchemasUsedByWebhooks(doc);

        Assert.Equal(["Address", "Pet", "ProblemDetails", "ShipmentEvent"], result.OrderBy(s => s, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void GetSchemasUsedByWebhooks_NoWebhooks_ReturnsEmpty()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(TwoSegmentYaml);

        var result = PathSegmentHelper.GetSchemasUsedByWebhooks(doc);

        Assert.Empty(result);
    }

    // ========== GetWebhookModelUsings Tests ==========
    [Fact]
    public void GetWebhookModelUsings_MixesSharedAndSegmentNamespaces()
    {
        // Pet is only used by /pets (segment namespace); ShipmentEvent is webhook-only and Address is used by
        // two segments (both shared). ProblemDetails binds to the framework type and gets no using.
        var doc = OpenApiDocumentHelper.ParseYaml(WebhookYaml);
        var webhook = doc.Webhooks!["petShipped"].Operations![HttpMethod.Post];

        var result = PathSegmentHelper.GetWebhookModelUsings(doc, "TestApi", webhook);

        Assert.Equal(
            ["TestApi.Generated.Models", "TestApi.Generated.Pets.Models"],
            result);
    }

    // ========== PathSegmentHasModels (pre-computed shared set) Tests ==========
    [Fact]
    public void PathSegmentHasModels_WithSharedSet_SegmentWithOwnSchema_ReturnsTrue()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(TwoSegmentYaml);
        var shared = PathSegmentHelper.GetSharedSchemas(doc);

        Assert.True(PathSegmentHelper.PathSegmentHasModels(doc, "Pets", shared));
    }

    [Fact]
    public void PathSegmentHasModels_WithSharedSet_SegmentUsingOnlySharedSchema_ReturnsFalse()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(TwoSegmentYaml);
        var shared = PathSegmentHelper.GetSharedSchemas(doc);

        Assert.False(PathSegmentHelper.PathSegmentHasModels(doc, "Addresses", shared));
    }

    [Fact]
    public void PathSegmentHasModels_WithSharedSet_OnlyBuiltInErrorContract_ReturnsFalse()
    {
        var doc = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.1.1
            info:
              title: Test API
              version: 1.0.0
            paths:
              /health:
                get:
                  operationId: getHealth
                  responses:
                    '500':
                      description: Error
                      content:
                        application/problem+json:
                          schema:
                            $ref: '#/components/schemas/ProblemDetails'
            components:
              schemas:
                ProblemDetails:
                  type: object
                  properties:
                    detail:
                      type: string
            """);

        Assert.False(PathSegmentHelper.PathSegmentHasModels(doc, "Health", new HashSet<string>(StringComparer.Ordinal)));
    }

    // ========== PathSegmentHasInlineModels Tests ==========
    [Fact]
    public void PathSegmentHasInlineModels_InlineObjectResponse_ReturnsTrue()
    {
        var doc = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.1.1
            info:
              title: Test API
              version: 1.0.0
            paths:
              /analytics/summary:
                get:
                  operationId: getAnalyticsSummary
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: object
                            properties:
                              total:
                                type: integer
            """);

        Assert.True(PathSegmentHelper.PathSegmentHasInlineModels(doc, "Analytics"));
    }

    [Fact]
    public void PathSegmentHasInlineModels_ArrayOfInlineObjectsInRequestBody_ReturnsTrue()
    {
        var doc = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.1.1
            info:
              title: Test API
              version: 1.0.0
            paths:
              /reports:
                post:
                  operationId: createReports
                  requestBody:
                    content:
                      application/json:
                        schema:
                          type: array
                          items:
                            type: object
                            properties:
                              name:
                                type: string
                  responses:
                    '201':
                      description: Created
            """);

        Assert.True(PathSegmentHelper.PathSegmentHasInlineModels(doc, "Reports"));
    }

    [Fact]
    public void PathSegmentHasInlineModels_OnlyReferencedSchemas_ReturnsFalse()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(TwoSegmentYaml);

        Assert.False(PathSegmentHelper.PathSegmentHasInlineModels(doc, "Pets"));
        Assert.False(PathSegmentHelper.PathSegmentHasInlineModels(doc, "Unknown"));
    }

    // ========== GetPathSegmentNamespaces (pre-computed shared set) Tests ==========
    [Fact]
    public void GetPathSegmentNamespaces_WithSharedSet_SegmentUsingOnlySharedSchema_HasNoModels()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(TwoSegmentYaml);
        var shared = PathSegmentHelper.GetSharedSchemas(doc);

        var result = PathSegmentHelper.GetPathSegmentNamespaces(doc, "Addresses", shared);

        Assert.True(result.HasHandlers);
        Assert.True(result.HasResults);
        Assert.True(result.HasParameters);
        Assert.False(result.HasModels);
    }

    [Fact]
    public void GetPathSegmentNamespaces_WithSharedSet_SegmentWithOwnSchema_HasModels()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(TwoSegmentYaml);
        var shared = PathSegmentHelper.GetSharedSchemas(doc);

        var result = PathSegmentHelper.GetPathSegmentNamespaces(doc, "Users", shared);

        Assert.True(result.HasHandlers);
        Assert.True(result.HasResults);
        Assert.False(result.HasParameters);
        Assert.True(result.HasModels);
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