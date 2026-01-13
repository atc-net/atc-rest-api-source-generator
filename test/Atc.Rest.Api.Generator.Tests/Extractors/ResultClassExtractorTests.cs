namespace Atc.Rest.Api.Generator.Tests.Extractors;

[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public class ResultClassExtractorTests
{
    // ========== Ok Method Parameter Naming Tests ==========

    [Fact]
    public void Extract_WithStringResponse_UsesMessageParameterName()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /login:
                post:
                  operationId: loginUser
                  responses:
                    '200':
                      description: successful operation
                      content:
                        application/json:
                          schema:
                            type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        Assert.Single(resultClasses);

        var resultClass = resultClasses[0];
        var okMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);

        // Verify parameter name is "message" for string type
        var param = okMethod.Parameters[0];
        Assert.Equal("string", param.TypeName);
        Assert.Equal("message", param.Name);

        // Verify Content uses "message"
        Assert.Contains("message", okMethod.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithComplexTypeResponse_UsesResponseParameterName()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /users/{id}:
                get:
                  operationId: getUserById
                  parameters:
                    - name: id
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
                            $ref: '#/components/schemas/User'
            components:
              schemas:
                User:
                  type: object
                  properties:
                    id:
                      type: string
                    name:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        Assert.Single(resultClasses);

        var resultClass = resultClasses[0];
        var okMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);

        // Verify parameter name is "response" for complex type
        var param = okMethod.Parameters[0];
        Assert.Equal("User", param.TypeName);
        Assert.Equal("response", param.Name);

        // Verify Content uses "response"
        Assert.Contains("response", okMethod.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithStringResponse_ImplicitOperatorUsesMessageParameterName()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /message:
                get:
                  operationId: getMessage
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];

        // Find the implicit operator method
        var implicitOperator = resultClass.Methods?.FirstOrDefault(m =>
            m.DeclarationModifier == DeclarationModifiers.PublicStaticImplicitOperator);
        Assert.NotNull(implicitOperator);
        Assert.NotNull(implicitOperator.Parameters);
        Assert.Single(implicitOperator.Parameters);

        // Verify parameter name is "message" for string type
        var param = implicitOperator.Parameters[0];
        Assert.Equal("string", param.TypeName);
        Assert.Equal("message", param.Name);

        // Verify Content uses "message"
        Assert.Contains("message", implicitOperator.Content, StringComparison.Ordinal);
    }

    // ========== NotFound Method Tests ==========

    [Fact]
    public void Extract_With404Response_GeneratesNotFoundWithStringMessageParameter()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /users/{id}:
                get:
                  operationId: getUserById
                  parameters:
                    - name: id
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
                            $ref: '#/components/schemas/User'
                    '404':
                      description: Not Found
            components:
              schemas:
                User:
                  type: object
                  properties:
                    id:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];
        var notFoundMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "NotFound");
        Assert.NotNull(notFoundMethod);
        Assert.NotNull(notFoundMethod.Parameters);
        Assert.Single(notFoundMethod.Parameters);

        // Verify parameter is string? message = null
        var param = notFoundMethod.Parameters[0];
        Assert.Equal("string", param.TypeName);
        Assert.Equal("message", param.Name);
        Assert.True(param.IsNullableType);
        Assert.Equal("null", param.DefaultValue);

        // Verify Content uses TypedResults.NotFound(message)
        Assert.Contains("TypedResults.NotFound(message)", notFoundMethod.Content, StringComparison.Ordinal);
    }

    // ========== Conflict Method Tests ==========

    [Fact]
    public void Extract_With409Response_GeneratesConflictWithStringMessageParameter()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /users:
                post:
                  operationId: createUser
                  requestBody:
                    content:
                      application/json:
                        schema:
                          $ref: '#/components/schemas/User'
                  responses:
                    '201':
                      description: Created
                    '409':
                      description: Conflict
            components:
              schemas:
                User:
                  type: object
                  properties:
                    id:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];
        var conflictMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "Conflict");
        Assert.NotNull(conflictMethod);
        Assert.NotNull(conflictMethod.Parameters);
        Assert.Single(conflictMethod.Parameters);

        // Verify parameter is string? message = null (not ProblemDetails)
        var param = conflictMethod.Parameters[0];
        Assert.Equal("string", param.TypeName);
        Assert.Equal("message", param.Name);
        Assert.True(param.IsNullableType);
        Assert.Equal("null", param.DefaultValue);

        // Verify Content uses TypedResults.Conflict(message)
        Assert.Contains("TypedResults.Conflict(message)", conflictMethod.Content, StringComparison.Ordinal);
    }

    // ========== PreconditionFailed (412) Tests ==========

    [Fact]
    public void Extract_With412Response_GeneratesPreconditionFailedMethod()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /resources/{id}:
                put:
                  operationId: updateResource
                  parameters:
                    - name: id
                      in: path
                      required: true
                      schema:
                        type: string
                  requestBody:
                    content:
                      application/json:
                        schema:
                          $ref: '#/components/schemas/Resource'
                  responses:
                    '200':
                      description: OK
                    '412':
                      description: Precondition Failed
            components:
              schemas:
                Resource:
                  type: object
                  properties:
                    id:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];
        var preconditionFailedMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "PreconditionFailed");
        Assert.NotNull(preconditionFailedMethod);

        // Verify no parameters
        Assert.Null(preconditionFailedMethod.Parameters);

        // Verify Content uses TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed)
        Assert.Contains("TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed)", preconditionFailedMethod.Content, StringComparison.Ordinal);

        // Verify documentation
        Assert.NotNull(preconditionFailedMethod.DocumentationTags);
        Assert.Contains("412", preconditionFailedMethod.DocumentationTags.Summary, StringComparison.Ordinal);
        Assert.Contains("Precondition Failed", preconditionFailedMethod.DocumentationTags.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_Without412Response_DoesNotGeneratePreconditionFailedMethod()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /users/{id}:
                get:
                  operationId: getUserById
                  parameters:
                    - name: id
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
                            $ref: '#/components/schemas/User'
                    '404':
                      description: Not Found
            components:
              schemas:
                User:
                  type: object
                  properties:
                    id:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];
        var preconditionFailedMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "PreconditionFailed");

        // PreconditionFailed should NOT exist since 412 is not in the spec
        Assert.Null(preconditionFailedMethod);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}