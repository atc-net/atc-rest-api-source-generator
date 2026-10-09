namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class ErrorResponseShapeResolverTests
{
    private const string ErrorsYaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        paths:
          /items:
            get:
              operationId: getItems
              responses:
                '200':
                  description: OK
                '400':
                  description: Bad request
                  content:
                    application/problem+json:
                      schema: { $ref: '#/components/schemas/ValidationProblemDetails' }
                '401':
                  description: Unauthorized
                  content:
                    application/json:
                      schema: { type: string }
                '403':
                  description: Forbidden
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/ErrorMessage' }
                '404':
                  description: Not found
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/ProblemDetails' }
                '409':
                  description: Conflict
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/ErrorCode' }
                '410':
                  description: Gone
                  content:
                    text/plain:
                      schema: { type: string }
                '422':
                  description: Unprocessable
                  content:
                    application/xml:
                      schema: { $ref: '#/components/schemas/ApiError' }
                '429':
                  description: Too many requests
                  content:
                    text/plain:
                      schema: { type: string }
                    application/vnd.api+json:
                      schema: { $ref: '#/components/schemas/ApiError' }
                '500':
                  description: Error
                default:
                  description: Other
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/ApiError' }
        components:
          schemas:
            ProblemDetails:
              type: object
              properties:
                title: { type: string }
            ValidationProblemDetails:
              type: object
              properties:
                errors: { type: object }
            ApiError:
              type: object
              properties:
                code: { type: string }
            ErrorMessage:
              type: string
            ErrorCode:
              type: string
              enum: [a, b]
        """;

    [Theory]
    [InlineData("default", true)]
    [InlineData("DEFAULT", true)]
    [InlineData("400", true)]
    [InlineData("404", true)]
    [InlineData("500", true)]
    [InlineData("599", true)]
    [InlineData("399", false)]
    [InlineData("200", false)]
    [InlineData("4XX", false)]
    [InlineData("", false)]
    public void IsErrorStatusCode_ReturnsExpectedResult(
        string statusCode,
        bool expected)
    {
        Assert.Equal(expected, ErrorResponseShapeResolver.IsErrorStatusCode(statusCode));
    }

    [Theory]
    [InlineData("application/json", true)]
    [InlineData("application/problem+json", true)]
    [InlineData("application/vnd.api+json", true)]
    [InlineData("APPLICATION/JSON; charset=utf-8", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/xml", false)]
    [InlineData("application/jsonx", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsJsonMediaType_ReturnsExpectedResult(
        string? mediaType,
        bool expected)
    {
        Assert.Equal(expected, ErrorResponseShapeResolver.IsJsonMediaType(mediaType));
    }

    /// <summary>
    /// Whether a declared error model can hold the ProblemDetails a host writes for an empty error response: it has a
    /// <c>title</c>, <c>status</c> or <c>detail</c> (case-insensitive, also through <c>allOf</c>). Only then may the
    /// handler's factory be called without the model.
    /// </summary>
    [Theory]
    [InlineData("{ $ref: '#/components/schemas/ApiError' }", false)]
    [InlineData("{ type: object, properties: { message: { type: string } } }", false)]
    [InlineData("{ $ref: '#/components/schemas/ApiProblem' }", true)]
    [InlineData("{ type: object, properties: { status: { type: integer } } }", true)]
    [InlineData("{ type: object, properties: { Detail: { type: string } } }", true)]
    [InlineData("{ allOf: [ { $ref: '#/components/schemas/ApiProblem' }, { type: object, properties: { code: { type: string } } } ] }", true)]
    [InlineData("{ $ref: '#/components/schemas/Derived' }", true)]
    [InlineData("{ type: string }", false)]
    [InlineData("{ $ref: '#/components/schemas/ProblemDetails' }", true)]
    public void CanHoldProblemDetails_IsTrue_ForAModelWithATitleStatusOrDetail(
        string schema,
        bool expected)
    {
        var yaml = $$"""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths:
              /items:
                get:
                  operationId: getItems
                  responses:
                    '404':
                      description: Not found
                      content:
                        application/json:
                          schema: {{schema}}
            components:
              schemas:
                ApiError:
                  type: object
                  properties:
                    code: { type: string }
                    message: { type: string }
                ApiProblem:
                  type: object
                  properties:
                    title: { type: string }
                    detail: { type: string }
                Derived:
                  allOf:
                    - $ref: '#/components/schemas/ApiProblem'
                    - type: object
                      properties:
                        code: { type: string }
                ProblemDetails:
                  type: object
                  properties:
                    title: { type: string }
            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, "items.yaml");
        var response = document!.Paths!["/items"].Operations![HttpMethod.Get].Responses!["404"];

        Assert.Equal(expected, ErrorResponseShapeResolver.Resolve(response).CanHoldProblemDetails);
    }

    [Fact]
    public void ToProducesCall_DeclaredProblem_ProducesProblemWithStatusConstant()
    {
        var shape = new ErrorResponseShape(ErrorResponseKind.Problem, "application/problem+json", Schema: null);

        Assert.Equal(".ProducesProblem(StatusCodes.Status404NotFound)", ErrorResponseShapeResolver.ToProducesCall("404", shape, null));
        Assert.Equal(".ProducesProblem(StatusCodes.Status400BadRequest)", ErrorResponseShapeResolver.ToProducesCall("400", shape, null));
    }

    [Fact]
    public void ToProducesCall_UndeclaredProblemAt400_ProducesValidationProblem()
    {
        // An undeclared 400 is the validation filter's answer, a ValidationProblemDetails
        var shape = new ErrorResponseShape(ErrorResponseKind.Problem, MediaType: null, Schema: null);

        Assert.Equal(".ProducesValidationProblem()", ErrorResponseShapeResolver.ToProducesCall("400", shape, null));
        Assert.Equal(".ProducesProblem(StatusCodes.Status404NotFound)", ErrorResponseShapeResolver.ToProducesCall("404", shape, null));
    }

    [Fact]
    public void ToProducesCall_ValidationProblem_ProducesValidationProblemWithStatusUnless400()
    {
        var shape = new ErrorResponseShape(ErrorResponseKind.ValidationProblem, "application/problem+json", Schema: null);

        Assert.Equal(".ProducesValidationProblem()", ErrorResponseShapeResolver.ToProducesCall("400", shape, null));
        Assert.Equal(".ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)", ErrorResponseShapeResolver.ToProducesCall("422", shape, null));
    }

    [Fact]
    public void ToProducesCall_DefaultResponse_IsDescribedUnder500()
    {
        var shape = new ErrorResponseShape(ErrorResponseKind.Problem, MediaType: null, Schema: null);

        Assert.Equal(".ProducesProblem(StatusCodes.Status500InternalServerError)", ErrorResponseShapeResolver.ToProducesCall("default", shape, null));
    }

    [Fact]
    public void ToProducesCall_JsonObject_ProducesModelWithContentType()
    {
        var shape = new ErrorResponseShape(ErrorResponseKind.JsonObject, "application/json", Schema: null);

        Assert.Equal(".Produces<ApiError>(StatusCodes.Status409Conflict, contentType: \"application/json\")", ErrorResponseShapeResolver.ToProducesCall("409", shape, "ApiError"));

        // No model type: object; an unmapped status: the numeric literal
        Assert.Equal(".Produces<object>(599, contentType: \"application/json\")", ErrorResponseShapeResolver.ToProducesCall("599", shape, null));
    }

    [Theory]
    [InlineData(ErrorResponseKind.Text, "text/plain")]
    [InlineData(ErrorResponseKind.JsonString, "application/json")]
    public void ToProducesCall_StringBody_ProducesString(
        ErrorResponseKind kind,
        string mediaType)
    {
        var shape = new ErrorResponseShape(kind, mediaType, Schema: null);

        Assert.Equal($".Produces<string>(StatusCodes.Status500InternalServerError, contentType: \"{mediaType}\")", ErrorResponseShapeResolver.ToProducesCall("500", shape, "Ignored"));
    }

    [Fact]
    public void ToProducesCall_MediaTypeWithQuotedParameter_EscapesTheLiteral()
    {
        var shape = new ErrorResponseShape(ErrorResponseKind.JsonObject, "application/json; profile=\"urn:x\"", Schema: null);

        Assert.Equal(".Produces<ApiError>(StatusCodes.Status404NotFound, contentType: \"application/json; profile=\\\"urn:x\\\"\")", ErrorResponseShapeResolver.ToProducesCall("404", shape, "ApiError"));
    }

    [Fact]
    public void ToProducesCall_NullShape_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ErrorResponseShapeResolver.ToProducesCall("404", null!, null));
    }

    [Theory]
    [InlineData("404", "NotFoundError")]
    [InlineData("400", "BadRequestError")]
    [InlineData("500", "InternalServerErrorError")]
    [InlineData("499", "Status499Error")]
    [InlineData("default", "DefaultError")]
    public void InlineTypeContext_ReturnsExpectedResult(
        string statusCode,
        string expected)
    {
        Assert.Equal(expected, ErrorResponseShapeResolver.InlineTypeContext(statusCode));
    }

    [Fact]
    public void Resolve_ProblemDetailsReference_IsProblem()
    {
        var shape = ErrorResponseShapeResolver.Resolve(GetResponse("404"));

        Assert.Equal(ErrorResponseKind.Problem, shape.Kind);
        Assert.Equal("application/json", shape.MediaType);
        Assert.True(shape.IsProblem);
        Assert.True(shape.IsDeclared);
        Assert.Equal("ProblemDetails", shape.Schema!.GetReferenceId());
    }

    [Fact]
    public void Resolve_ValidationProblemDetailsReferenceUnderProblemJson_IsValidationProblem()
    {
        var shape = ErrorResponseShapeResolver.Resolve(GetResponse("400"));

        Assert.Equal(ErrorResponseKind.ValidationProblem, shape.Kind);
        Assert.Equal("application/problem+json", shape.MediaType);
        Assert.True(shape.IsProblem);
    }

    [Fact]
    public void Resolve_InlineStringUnderJson_IsJsonString()
    {
        var shape = ErrorResponseShapeResolver.Resolve(GetResponse("401"));

        Assert.Equal(ErrorResponseKind.JsonString, shape.Kind);
        Assert.Equal("application/json", shape.MediaType);
        Assert.False(shape.IsProblem);
    }

    [Fact]
    public void Resolve_ReferenceToPrimitiveAlias_UsesThePrimitive()
    {
        // ErrorMessage: { type: string } is no generated type, so the shape is the string itself
        var document = OpenApiDocumentHelper.ParseYaml(ErrorsYaml);
        var response = document.Paths["/items"].Operations![HttpMethod.Get].Responses!["403"];

        var shape = ErrorResponseShapeResolver.Resolve(response);

        Assert.Equal(ErrorResponseKind.JsonString, shape.Kind);
        Assert.Same(document.Components!.Schemas!["ErrorMessage"], shape.Schema);
    }

    [Fact]
    public void Resolve_ReferenceToStringEnum_KeepsTheReferenceAsJsonObject()
    {
        // An enum is a generated type; the client reads it as that type, not as a string
        var shape = ErrorResponseShapeResolver.Resolve(GetResponse("409"));

        Assert.Equal(ErrorResponseKind.JsonObject, shape.Kind);
        Assert.Equal("ErrorCode", shape.Schema!.GetReferenceId());
    }

    [Fact]
    public void Resolve_ModelReference_IsJsonObject()
    {
        var shape = ErrorResponseShapeResolver.Resolve(GetResponse("default"));

        Assert.Equal(ErrorResponseKind.JsonObject, shape.Kind);
        Assert.Equal("application/json", shape.MediaType);
        Assert.Equal("ApiError", shape.Schema!.GetReferenceId());
    }

    [Fact]
    public void Resolve_TextPlainString_IsText()
    {
        var shape = ErrorResponseShapeResolver.Resolve(GetResponse("410"));

        Assert.Equal(ErrorResponseKind.Text, shape.Kind);
        Assert.Equal("text/plain", shape.MediaType);
    }

    [Fact]
    public void Resolve_XmlOnlyModel_IsXmlObject()
    {
        var shape = ErrorResponseShapeResolver.Resolve(GetResponse("422"));

        Assert.Equal(ErrorResponseKind.XmlObject, shape.Kind);
        Assert.Equal("application/xml", shape.MediaType);
        Assert.Equal("ApiError", shape.Schema!.GetReferenceId());
    }

    [Fact]
    public void Resolve_JsonSuffixBesideText_PrefersJson()
    {
        var shape = ErrorResponseShapeResolver.Resolve(GetResponse("429"));

        Assert.Equal(ErrorResponseKind.JsonObject, shape.Kind);
        Assert.Equal("application/vnd.api+json", shape.MediaType);
    }

    [Fact]
    public void Resolve_NoContent_IsUndeclaredProblem()
    {
        var shape = ErrorResponseShapeResolver.Resolve(GetResponse("500"));

        Assert.Equal(ErrorResponseKind.Problem, shape.Kind);
        Assert.Null(shape.MediaType);
        Assert.Null(shape.Schema);
        Assert.False(shape.IsDeclared);
        Assert.True(shape.IsProblem);
    }

    [Fact]
    public void Resolve_NullResponse_IsUndeclaredProblem()
    {
        var shape = ErrorResponseShapeResolver.Resolve(null);

        Assert.Equal(new ErrorResponseShape(ErrorResponseKind.Problem, MediaType: null, Schema: null), shape);
    }

    private static IOpenApiResponse GetResponse(string statusCode)
        => OpenApiDocumentHelper.ParseYaml(ErrorsYaml).Paths["/items"].Operations![HttpMethod.Get].Responses![statusCode];
}