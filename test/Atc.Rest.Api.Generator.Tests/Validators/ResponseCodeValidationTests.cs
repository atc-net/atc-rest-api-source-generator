namespace Atc.Rest.Api.Generator.Tests.Validators;

public class ResponseCodeValidationTests
{
    private const string TestFilePath = "test.yaml";

    // ========== 401 Unauthorized Without Security Tests (ATC_API_OPR021) ==========
    [Fact]
    public void StrictMode_Warns_401WithoutSecurity()
    {
        // Arrange: Operation with 401 response but no security requirements
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /health:
                                get:
                                  operationId: healthCheck
                                  responses:
                                    200:
                                      description: OK
                                    401:
                                      description: Unauthorized
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should have ATC_API_OPR021 warning
        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.UnauthorizedWithoutSecurity);
    }

    [Fact]
    public void StandardMode_NoWarning_401WithoutSecurity()
    {
        // Arrange: Operation with 401 response but no security requirements
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /health:
                                get:
                                  operationId: healthCheck
                                  responses:
                                    200:
                                      description: OK
                                    401:
                                      description: Unauthorized
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Standard mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Standard, doc, [], TestFilePath);

        // Assert: Should NOT have ATC_API_OPR021 warning (only in strict mode)
        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.UnauthorizedWithoutSecurity);
    }

    // ========== 403 Forbidden Without Authorization Tests (ATC_API_OPR022) ==========
    [Fact]
    public void StrictMode_Warns_403WithoutAuthorization()
    {
        // Arrange: Operation with 403 response but no roles/policies/scopes
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /admin:
                                get:
                                  operationId: getAdmin
                                  responses:
                                    200:
                                      description: OK
                                    403:
                                      description: Forbidden
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should have ATC_API_OPR022 warning
        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.ForbiddenWithoutAuthorization);
    }

    // ========== 404 NotFound on POST Tests (ATC_API_OPR023) ==========
    [Fact]
    public void StrictMode_Warns_404OnPostOperation()
    {
        // Arrange: POST operation with 404 response
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /pets:
                                post:
                                  operationId: createPet
                                  responses:
                                    201:
                                      description: Created
                                    404:
                                      description: Not Found
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should have ATC_API_OPR023 warning
        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.NotFoundOnPostOperation);
    }

    [Fact]
    public void StrictMode_NoWarning_404OnGetOperation()
    {
        // Arrange: GET operation with 404 response (this is expected)
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
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
                                    200:
                                      description: OK
                                    404:
                                      description: Not Found
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should NOT have ATC_API_OPR023 warning (404 is expected on GET)
        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.NotFoundOnPostOperation);
    }

    // ========== 409 Conflict on Non-Mutating Operation Tests (ATC_API_OPR024) ==========
    [Fact]
    public void StrictMode_Warns_409OnGetOperation()
    {
        // Arrange: GET operation with 409 response
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    200:
                                      description: OK
                                    409:
                                      description: Conflict
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should have ATC_API_OPR024 warning
        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.ConflictOnNonMutatingOperation);
    }

    [Fact]
    public void StrictMode_Warns_409OnDeleteOperation()
    {
        // Arrange: DELETE operation with 409 response
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /pets/{petId}:
                                delete:
                                  operationId: deletePet
                                  parameters:
                                    - name: petId
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    204:
                                      description: No Content
                                    409:
                                      description: Conflict
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should have ATC_API_OPR024 warning
        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.ConflictOnNonMutatingOperation);
    }

    [Fact]
    public void StrictMode_NoWarning_409OnPostOperation()
    {
        // Arrange: POST operation with 409 response (this is expected)
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /pets:
                                post:
                                  operationId: createPet
                                  responses:
                                    201:
                                      description: Created
                                    409:
                                      description: Conflict
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should NOT have ATC_API_OPR024 warning (409 is expected on POST)
        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ConflictOnNonMutatingOperation);
    }

    [Fact]
    public void StrictMode_NoWarning_409OnPutOperation()
    {
        // Arrange: PUT operation with 409 response (this is expected)
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /pets/{petId}:
                                put:
                                  operationId: updatePet
                                  parameters:
                                    - name: petId
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    200:
                                      description: OK
                                    409:
                                      description: Conflict
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should NOT have ATC_API_OPR024 warning (409 is expected on PUT)
        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ConflictOnNonMutatingOperation);
    }

    // ========== 429 TooManyRequests Without Rate Limiting Tests (ATC_API_OPR025) ==========
    [Fact]
    public void StrictMode_Warns_429WithoutRateLimiting()
    {
        // Arrange: Operation with 429 response but no rate limiting configured
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    200:
                                      description: OK
                                    429:
                                      description: Too Many Requests
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should have ATC_API_OPR025 warning
        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.TooManyRequestsWithoutRateLimiting);
    }

    // ========== 400 BadRequest Without Parameters Tests (ATC_API_OPR010) ==========
    [Fact]
    public void StrictMode_Warns_400WithoutParameters()
    {
        // Arrange: Operation with 400 response but no parameters
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /health:
                                get:
                                  operationId: healthCheck
                                  responses:
                                    200:
                                      description: OK
                                    400:
                                      description: Bad Request
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should have ATC_API_OPR010 warning
        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.BadRequestWithoutParameters);
    }

    [Fact]
    public void StrictMode_NoWarning_400WithParameters()
    {
        // Arrange: Operation with 400 response and parameters (this is expected)
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  parameters:
                                    - name: limit
                                      in: query
                                      schema:
                                        type: integer
                                  responses:
                                    200:
                                      description: OK
                                    400:
                                      description: Bad Request
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act: Validate with Strict mode
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        // Assert: Should NOT have ATC_API_OPR010 warning (400 with params is expected)
        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.BadRequestWithoutParameters);
    }
}