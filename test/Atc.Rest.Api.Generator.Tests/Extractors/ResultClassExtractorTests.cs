namespace Atc.Rest.Api.Generator.Tests.Extractors;

[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public class ResultClassExtractorTests
{
    // ========== 200 OK Tests ==========

    [Fact]
    public void Extract_WithStringResponse_UsesMessageParameterName()
    {
        // Arrange
        const string yaml = """
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
            document,
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
        const string yaml = """
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
            document,
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
        const string yaml = """
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
            document,
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

    // ========== 404 Not Found Tests ==========

    [Fact]
    public void Extract_With404Response_GeneratesNotFoundWithStringMessageParameter()
    {
        // Arrange
        const string yaml = """
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
            document,
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

        // Verify Content writes an RFC 9457 ProblemDetails with the message as detail
        Assert.Contains("TypedResults.Problem(detail: message, statusCode: 404)", notFoundMethod.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A status with a declared model takes the model optionally: called without it, the response has no body of its
    /// own and the host writes its ProblemDetails - not the declared model, so a client reading that model sees its
    /// fields empty. The factory says so where a handler author reads it.
    /// </summary>
    [Fact]
    public void Extract_With404DeclaringAModel_DocumentsWhatTheEmptyCallWrites()
    {
        const string yaml = """
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
                                    '404':
                                      description: Not Found
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Missing'
                                    '409':
                                      description: Conflict
                            components:
                              schemas:
                                Missing:
                                  type: object
                                  properties:
                                    code:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var resultClass = ResultClassExtractor.Extract(document, "TestApi", registry: null, systemTypeResolver: new SystemTypeConflictResolver([]), includeDeprecated: false)![0];

        var notFound = resultClass.Methods!.Single(m => m.Name == "NotFound");
        Assert.Equal("Missing", notFound.Parameters![0].TypeName);
        var remark = notFound.DocumentationTags?.Remark;
        Assert.NotNull(remark);
        Assert.Contains("ProblemDetails", remark, StringComparison.Ordinal);
        Assert.Contains("Missing", remark, StringComparison.Ordinal);

        // No declared body: the message becomes the ProblemDetails' detail, nothing to point out
        Assert.Null(resultClass.Methods!.Single(m => m.Name == "Conflict").DocumentationTags?.Remark);
    }

    // ========== 409 Conflict Tests ==========

    [Fact]
    public void Extract_With409Response_GeneratesConflictWithStringMessageParameter()
    {
        // Arrange
        const string yaml = """
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
            document,
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

        // Verify Content writes an RFC 9457 ProblemDetails with the message as detail
        Assert.Contains("TypedResults.Problem(detail: message, statusCode: 409)", conflictMethod.Content, StringComparison.Ordinal);
    }

    // ========== 410 Gone Tests (Generic Fallback) ==========

    [Fact]
    public void Extract_With410Response_GeneratesGoneMethod()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /legacy/{id}:
                                get:
                                  operationId: getLegacyResource
                                  parameters:
                                    - name: id
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                                    '410':
                                      description: Resource permanently removed
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];
        var method = resultClass.Methods?.FirstOrDefault(m => m.Name == "Gone");
        Assert.NotNull(method);

        Assert.Contains("TypedResults.Problem(detail: message, statusCode: 410)", method.Content, StringComparison.Ordinal);
        Assert.NotNull(method.DocumentationTags);
        Assert.Contains("410", method.DocumentationTags.Summary, StringComparison.Ordinal);
    }

    // ========== 412 Precondition Failed Tests ==========

    [Fact]
    public void Extract_With412Response_GeneratesPreconditionFailedMethod()
    {
        // Arrange
        const string yaml = """
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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];
        var preconditionFailedMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "PreconditionFailed");
        Assert.NotNull(preconditionFailedMethod);

        // Verify the optional message (2.x "PreconditionFailed()" still compiles)
        Assert.NotNull(preconditionFailedMethod.Parameters);
        Assert.Equal("message", Assert.Single(preconditionFailedMethod.Parameters).Name);

        // Verify Content writes an RFC 9457 ProblemDetails
        Assert.Contains("TypedResults.Problem(detail: message, statusCode: 412)", preconditionFailedMethod.Content, StringComparison.Ordinal);

        // Verify documentation
        Assert.NotNull(preconditionFailedMethod.DocumentationTags);
        Assert.Contains("412", preconditionFailedMethod.DocumentationTags.Summary, StringComparison.Ordinal);
        Assert.Contains("Precondition Failed", preconditionFailedMethod.DocumentationTags.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_Without412Response_DoesNotGeneratePreconditionFailedMethod()
    {
        // Arrange
        const string yaml = """
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
            document,
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

    // ========== 413 Payload Too Large Tests (Generic Fallback) ==========

    [Fact]
    public void Extract_With413Response_GeneratesPayloadTooLargeMethod()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /files:
                                post:
                                  operationId: uploadFile
                                  requestBody:
                                    content:
                                      application/octet-stream:
                                        schema:
                                          type: string
                                          format: binary
                                  responses:
                                    '200':
                                      description: OK
                                    '413':
                                      description: File too large
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];
        var method = resultClass.Methods?.FirstOrDefault(m => m.Name == "RequestEntityTooLarge");
        Assert.NotNull(method);
        Assert.NotNull(method.Parameters);
        Assert.Single(method.Parameters);

        var param = method.Parameters[0];
        Assert.Equal("string", param.TypeName);
        Assert.Equal("message", param.Name);
        Assert.True(param.IsNullableType);
        Assert.Equal("null", param.DefaultValue);

        Assert.Contains("TypedResults.Problem(detail: message, statusCode: 413)", method.Content, StringComparison.Ordinal);
        Assert.NotNull(method.DocumentationTags);
        Assert.Contains("413", method.DocumentationTags.Summary, StringComparison.Ordinal);
    }

    // ========== 422 Unprocessable Entity Tests ==========

    [Fact]
    public void Extract_With422Response_GeneratesUnprocessableEntityMethod()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /resources:
                                post:
                                  operationId: createResource
                                  requestBody:
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/Resource'
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: string
                                            format: uuid
                                    '422':
                                      description: Unprocessable Entity
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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];
        var unprocessableEntityMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "UnprocessableEntity");
        Assert.NotNull(unprocessableEntityMethod);
        Assert.NotNull(unprocessableEntityMethod.Parameters);
        Assert.Single(unprocessableEntityMethod.Parameters);

        // Verify parameter is string? message = null
        var param = unprocessableEntityMethod.Parameters[0];
        Assert.Equal("string", param.TypeName);
        Assert.Equal("message", param.Name);
        Assert.True(param.IsNullableType);
        Assert.Equal("null", param.DefaultValue);

        // Verify Content writes an RFC 9457 ProblemDetails with the message as detail
        Assert.Contains("TypedResults.Problem(detail: message, statusCode: 422)", unprocessableEntityMethod.Content, StringComparison.Ordinal);

        // Verify documentation
        Assert.NotNull(unprocessableEntityMethod.DocumentationTags);
        Assert.Contains("422", unprocessableEntityMethod.DocumentationTags.Summary, StringComparison.Ordinal);
        Assert.Contains("Unprocessable Entity", unprocessableEntityMethod.DocumentationTags.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_Without422Response_DoesNotGenerateUnprocessableEntityMethod()
    {
        // Arrange
        const string yaml = """
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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];
        var unprocessableEntityMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "UnprocessableEntity");

        // UnprocessableEntity should NOT exist since 422 is not in the spec
        Assert.Null(unprocessableEntityMethod);
    }

    // ========== 503 Service Unavailable Tests (Generic Fallback) ==========

    [Fact]
    public void Extract_With503Response_GeneratesServiceUnavailableMethod()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /payments:
                                post:
                                  operationId: processPayment
                                  requestBody:
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/Payment'
                                  responses:
                                    '200':
                                      description: OK
                                    '503':
                                      description: Service Unavailable
                            components:
                              schemas:
                                Payment:
                                  type: object
                                  properties:
                                    amount:
                                      type: number
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses[0];
        var method = resultClass.Methods?.FirstOrDefault(m => m.Name == "ServiceUnavailable");
        Assert.NotNull(method);
        Assert.NotNull(method.Parameters);
        Assert.Single(method.Parameters);

        var param = method.Parameters[0];
        Assert.Equal("string", param.TypeName);
        Assert.Equal("message", param.Name);
        Assert.True(param.IsNullableType);
        Assert.Equal("null", param.DefaultValue);

        Assert.Contains("TypedResults.Problem(detail: message, statusCode: 503)", method.Content, StringComparison.Ordinal);
        Assert.NotNull(method.DocumentationTags);
        Assert.Contains("503", method.DocumentationTags.Summary, StringComparison.Ordinal);
        Assert.Contains("Service Unavailable", method.DocumentationTags.Summary, StringComparison.Ordinal);
    }

    // ========== Namespace Segment Conflict Tests ==========

    [Fact]
    public void Extract_SchemaNameMatchingNamespaceSegment_UsesFullyQualifiedTypeName()
    {
        // Arrange: Schema "Device" conflicts with namespace segment "Device" in "Contoso.Data.Device.Management"
        const string yaml = """
                            openapi: "3.1.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /devices/{id}:
                                get:
                                  operationId: getDeviceById
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
                                            $ref: '#/components/schemas/Device'
                                    '404':
                                      description: Not Found
                            components:
                              schemas:
                                Device:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var registry = TypeConflictRegistry.Build(document, "Contoso.Data.Device.Management", "Devices");

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document,
            "Contoso.Data.Device.Management",
            registry: registry,
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

        // The parameter type should be fully qualified to avoid CS0118
        var param = okMethod.Parameters[0];
        Assert.Equal("Contoso.Data.Device.Management.Generated.Devices.Models.Device", param.TypeName);
    }

    [Fact]
    public void Extract_ItemSchemaStreamingResponse_OkAcceptsAsyncEnumerableOfItem()
    {
        // Arrange - OpenAPI 3.2 itemSchema streaming, no x-* annotation.
        const string yaml = """
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
                                        text/event-stream:
                                          itemSchema:
                                            $ref: '#/components/schemas/Event'
                            components:
                              schemas:
                                Event:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = resultClasses.Single();
        var okMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        var param = okMethod.Parameters[0];
        Assert.Equal("IAsyncEnumerable<Event>", param.TypeName);

        // text/event-stream framing emits the first-party SSE writer, not TypedResults.Ok.
        Assert.Contains("TypedResults.ServerSentEvents(response)", okMethod.Content, StringComparison.Ordinal);
    }

    // ========== 202 Accepted Tests ==========

    [Fact]
    public void Extract_WithNoContentAcceptedResponse_UsesStatusCodeNotParameterlessAccepted()
    {
        // Arrange - a 202 response with no response body content. TypedResults.Accepted()
        // has no parameterless overload (every overload requires a location or a value),
        // so the generated factory must fall back to TypedResults.StatusCode(202).
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /notifications:
                                post:
                                  operationId: sendNotification
                                  responses:
                                    '202':
                                      description: Accepted
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var acceptedMethod = resultClasses[0].Methods?.FirstOrDefault(m => m.Name == "Accepted");
        Assert.NotNull(acceptedMethod);
        Assert.Null(acceptedMethod.Parameters);
        Assert.Contains("TypedResults.StatusCode(StatusCodes.Status202Accepted)", acceptedMethod.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("TypedResults.Accepted()", acceptedMethod.Content, StringComparison.Ordinal);
    }

    // ========== Path segment overload / inline schemas ==========

    [Fact]
    public void Extract_WithPathSegment_KeepsOnlyThatSegmentsOperationsInItsNamespace()
    {
        // Arrange
        const string yaml = """
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
                              /users:
                                get:
                                  operationId: listUsers
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document,
            "TestApi",
            pathSegment: "Pets",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = Assert.Single(resultClasses);
        Assert.Equal("ListPetsResult", resultClass.ClassTypeName);
        Assert.Equal("TestApi.Generated.Pets.Results", resultClass.Namespace);
    }

    [Fact]
    public void Extract_WithUnknownPathSegment_ReturnsNull()
    {
        // Arrange
        const string yaml = """
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
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var resultClasses = ResultClassExtractor.Extract(
            document,
            "TestApi",
            pathSegment: "Orders",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.Null(resultClasses);
    }

    [Fact]
    public void ExtractWithInlineSchemas_InlineResponseObject_RecordsTheInlineTypeUnderThePathSegment()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /status:
                                get:
                                  operationId: getStatus
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: object
                                            properties:
                                              healthy:
                                                type: boolean
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (resultClasses, inlineSchemas) = ResultClassExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            pathSegment: "Status",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var resultClass = Assert.Single(resultClasses);
        Assert.Equal("TestApi.Generated.Status.Results", resultClass.Namespace);
        var okMethod = resultClass.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.Equal("GetStatusResponse", okMethod.Parameters![0].TypeName);

        var inline = Assert.Single(inlineSchemas);
        Assert.Equal("GetStatusResponse", inline.Key);
        Assert.Equal("GetStatusResponse", inline.Value.TypeName);
        Assert.Equal("Status", inline.Value.PathSegment);
        Assert.Equal("GetStatusResponse", inline.Value.RecordParameters.Name);
        var property = Assert.Single(inline.Value.RecordParameters.Parameters!);
        Assert.Equal("Healthy", property.Name);
        Assert.Equal("bool", property.TypeName);
    }

    [Fact]
    public void ExtractWithInlineSchemas_WithNamespaceSegment_DrivesNamespaceAndInlineSegmentWithoutChangingTheFilter()
    {
        // Arrange - the raw path segment filters operations, the namespace segment is where the files are emitted
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /status:
                                get:
                                  operationId: getStatus
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: object
                                            properties:
                                              healthy:
                                                type: boolean
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (resultClasses, inlineSchemas) = ResultClassExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            pathSegment: "Status",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false,
            namespaceSegment: "Shared");

        // Assert
        Assert.NotNull(resultClasses);
        Assert.Equal("TestApi.Generated.Shared.Results", Assert.Single(resultClasses).Namespace);
        Assert.Equal("Shared", Assert.Single(inlineSchemas).Value.PathSegment);
    }

    [Fact]
    public void ExtractWithInlineSchemas_NoInlineSchemas_ReturnsEmptyDictionary()
    {
        // Arrange
        const string yaml = """
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
                                            $ref: '#/components/schemas/Pet'
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (resultClasses, inlineSchemas) = ResultClassExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            pathSegment: null,
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]));

        // Assert
        Assert.NotNull(resultClasses);
        Assert.Equal("Pet", Assert.Single(resultClasses).Methods!.First(m => m.Name == "Ok").Parameters![0].TypeName);
        Assert.Empty(inlineSchemas);
    }

    // ========== Shared IResult sources ==========

    [Fact]
    public void LocationHttpResultSource_WrapsAResultWithALocationHeader()
    {
        var source = ResultClassExtractor.LocationHttpResultSource();

        Assert.Contains("internal sealed class LocationHttpResult : IResult", source, StringComparison.Ordinal);
        Assert.Contains($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]", source, StringComparison.Ordinal);
        Assert.Contains("httpContext.Response.Headers.Location = location;", source, StringComparison.Ordinal);
        Assert.Contains("return inner.ExecuteAsync(httpContext);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void XmlHttpResultSource_WritesTheBytesWithStatusAndUtf8MediaType()
    {
        var source = ResultClassExtractor.XmlHttpResultSource();

        Assert.Contains("internal sealed class XmlHttpResult : IResult, IStatusCodeHttpResult, IContentTypeHttpResult", source, StringComparison.Ordinal);
        Assert.Contains("ContentType = mediaType + \"; charset=utf-8\";", source, StringComparison.Ordinal);
        Assert.Contains("httpContext.Response.StatusCode = StatusCode;", source, StringComparison.Ordinal);
        Assert.Contains("httpContext.Response.ContentLength = body.Length;", source, StringComparison.Ordinal);
        Assert.Contains("return httpContext.Response.Body.WriteAsync(body, httpContext.RequestAborted).AsTask();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NegotiatedHttpResultSource_PicksXmlOnlyWhenAcceptPrefersIt()
    {
        var source = ResultClassExtractor.NegotiatedHttpResultSource();

        Assert.Contains("internal sealed class NegotiatedHttpResult : IResult, IStatusCodeHttpResult", source, StringComparison.Ordinal);
        Assert.Contains("params string[] xmlMediaTypes)", source, StringComparison.Ordinal);
        Assert.Contains("=> (json as IStatusCodeHttpResult)?.StatusCode;", source, StringComparison.Ordinal);
        Assert.Contains("? xml(mediaType).ExecuteAsync(httpContext)", source, StringComparison.Ordinal);
        Assert.Contains(": json.ExecuteAsync(httpContext);", source, StringComparison.Ordinal);

        // */* and a tie are JSON, never a 406
        Assert.Contains("if (type is \"*/*\" or \"application/*\" or \"application/json\" ||", source, StringComparison.Ordinal);
        Assert.Contains("return xmlQuality > jsonQuality ? chosen : null;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RetryAfterHttpResultSource_SetsRetryAfterInSecondsOnlyWhenGiven()
    {
        var source = ResultClassExtractor.RetryAfterHttpResultSource();

        Assert.Contains("internal sealed class RetryAfterHttpResult : IResult", source, StringComparison.Ordinal);
        Assert.Contains("private readonly int? retryAfterSeconds;", source, StringComparison.Ordinal);
        Assert.Contains("if (retryAfterSeconds is not null)", source, StringComparison.Ordinal);
        Assert.Contains("httpContext.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture);", source, StringComparison.Ordinal);
        Assert.Contains("return inner.ExecuteAsync(httpContext);", source, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}