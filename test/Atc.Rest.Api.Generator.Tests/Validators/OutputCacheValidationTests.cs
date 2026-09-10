namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// ATC_API_CACHE001 — output caching configured on a method the middleware never caches.
/// </summary>
/// <remarks>
/// The ASP.NET Core output-cache middleware's default policy stores only GET and HEAD responses,
/// so <c>.CacheOutput(...)</c> on any other verb is a no-op. The generator emits the call anyway
/// (a consumer can override the base policy to opt a verb in), but it now says so at build time
/// instead of leaving an endpoint that merely looks cached.
/// </remarks>
public class OutputCacheValidationTests
{
    private const string TestFilePath = "test.yaml";

    [Fact]
    public void Warns_WhenOutputCacheIsConfiguredOnQueryOperation()
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
                                  x-cache-policy: ResourceQueryPolicy
                                  x-cache-expiration-seconds: 60
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

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.OutputCacheOnNonCacheableMethod);
    }

    [Fact]
    public void Warns_WhenOutputCacheIsConfiguredOnPostOperation()
    {
        const string yaml = """
                            openapi: "3.0.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /reports:
                                post:
                                  operationId: createReport
                                  x-cache-policy: ReportPolicy
                                  responses:
                                    201:
                                      description: Created
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.OutputCacheOnNonCacheableMethod);
    }

    [Fact]
    public void DoesNotWarn_WhenOutputCacheIsConfiguredOnGetOperation()
    {
        const string yaml = """
                            openapi: "3.0.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                get:
                                  operationId: listResources
                                  x-cache-policy: ResourcePolicy
                                  responses:
                                    200:
                                      description: OK
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.OutputCacheOnNonCacheableMethod);
    }

    [Fact]
    public void DoesNotWarn_WhenCacheTypeIsHybrid()
    {
        // HybridCache runs inside the handler, not in the output-cache middleware, so it is
        // unaffected by the middleware's GET/HEAD restriction. This is in fact the recommended
        // way to cache a QUERY.
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                query:
                                  operationId: queryResources
                                  x-cache-type: hybrid
                                  x-cache-policy: ResourceQueryPolicy
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

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.OutputCacheOnNonCacheableMethod);
    }

    [Fact]
    public void DoesNotWarn_WhenCachingIsExplicitlyDisabled()
    {
        const string yaml = """
                            openapi: "3.0.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            x-cache-policy: DocumentPolicy
                            paths:
                              /reports:
                                post:
                                  operationId: createReport
                                  x-cache-enabled: false
                                  responses:
                                    201:
                                      description: Created
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.OutputCacheOnNonCacheableMethod);
    }

    [Fact]
    public void Message_ExplainsTheVerbRestrictionAndTheBodyKeyGap()
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
                                  x-cache-policy: ResourceQueryPolicy
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

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        var diagnostic = Assert.Single(
            diagnostics,
            d => d.RuleId == RuleIdentifiers.OutputCacheOnNonCacheableMethod);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("queryResources", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("QUERY", diagnostic.Message, StringComparison.Ordinal);

        // The body-not-in-the-cache-key half matters as much as the verb half: a QUERY carries
        // its criteria in the body, and the middleware keys only on the URL.
        Assert.Contains("request body", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("x-cache-type: hybrid", diagnostic.Message, StringComparison.Ordinal);
    }
}