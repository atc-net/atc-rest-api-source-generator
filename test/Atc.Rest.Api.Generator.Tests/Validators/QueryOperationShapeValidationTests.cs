namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// ATC_API_OPR028 / ATC_API_OPR029 — an OpenAPI 3.2 <c>QUERY</c> operation whose shape contradicts
/// what the method means.
/// </summary>
/// <remarks>
/// <c>QUERY</c> is a safe, idempotent read that carries its criteria in a request body. An operation
/// that declares no body has given up the only thing <c>QUERY</c> offers over <c>GET</c>, and one
/// that declares a <c>201</c> or <c>409</c> is describing a mutation under a method callers and
/// caches are entitled to treat as read-only.
/// </remarks>
public class QueryOperationShapeValidationTests
{
    private const string TestFilePath = "test.yaml";

    [Fact]
    public void Warns_WhenQueryOperationHasNoRequestBody()
    {
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                query:
                                  operationId: queryResources
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.QueryOperationWithoutRequestBody);
    }

    [Fact]
    public void DoesNotWarn_WhenQueryOperationHasARequestBody()
    {
        var diagnostics = Validate(QueryWithBodyYaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.QueryOperationWithoutRequestBody);
    }

    [Fact]
    public void DoesNotWarn_ForAGetWithoutARequestBody()
    {
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                get:
                                  operationId: listResources
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.QueryOperationWithoutRequestBody);
    }

    [Fact]
    public void Warns_WhenQueryOperationDeclaresCreated()
    {
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                query:
                                  operationId: queryResources
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/ResourceQuery'
                                  responses:
                                    201:
                                      description: Created
                            components:
                              schemas:
                                ResourceQuery:
                                  type: object
                                  title: ResourceQuery
                                  properties:
                                    filter:
                                      type: string
                            """;

        var diagnostics = Validate(yaml);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.QueryOperationWithMutationResponse);
    }

    [Fact]
    public void DoesNotWarn_WhenQueryOperationDeclaresOnlyReadResponses()
    {
        var diagnostics = Validate(QueryWithBodyYaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.QueryOperationWithMutationResponse);
    }

    [Fact]
    public void DoesNotWarn_ForAPostDeclaringCreated()
    {
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                post:
                                  operationId: createResource
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/ResourceQuery'
                                  responses:
                                    201:
                                      description: Created
                            components:
                              schemas:
                                ResourceQuery:
                                  type: object
                                  title: ResourceQuery
                                  properties:
                                    filter:
                                      type: string
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.QueryOperationWithMutationResponse);
    }

    [Fact]
    public void Message_ForMissingBody_ExplainsWhyQueryWithoutABodyIsPointless()
    {
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                query:
                                  operationId: queryResources
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        var diagnostic = Assert.Single(
            diagnostics,
            d => d.RuleId == RuleIdentifiers.QueryOperationWithoutRequestBody);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("queryResources", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("GET", diagnostic.Message, StringComparison.Ordinal);
    }

    private const string QueryWithBodyYaml = """
                                             openapi: "3.2.0"
                                             info:
                                               title: Test API
                                               version: "1.0.0"
                                             paths:
                                               /resources:
                                                 query:
                                                   operationId: queryResources
                                                   requestBody:
                                                     required: true
                                                     content:
                                                       application/json:
                                                         schema:
                                                           $ref: '#/components/schemas/ResourceQuery'
                                                   responses:
                                                     200:
                                                       description: OK
                                             components:
                                               schemas:
                                                 ResourceQuery:
                                                   type: object
                                                   title: ResourceQuery
                                                   properties:
                                                     filter:
                                                       type: string
                                             """;

    private static IReadOnlyList<DiagnosticMessage> Validate(string yaml)
    {
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        return OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);
    }
}