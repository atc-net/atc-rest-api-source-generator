namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// ATC_API_OPR027 — a <c>GET</c> whose array query parameter can outgrow the URL.
/// </summary>
/// <remarks>
/// Kestrel's default <c>MaxRequestLineSize</c> is 8 KB. A <c>GET</c> that serializes 1000 GUIDs into
/// the query string is roughly 40 KB and is rejected with <c>414 URI Too Long</c> before it reaches a
/// handler — and intermediaries impose their own, often lower, caps. The fix is an OpenAPI 3.2
/// <c>query:</c> operation carrying the criteria in a request body, so this rule moves the discovery
/// from runtime to build time.
/// <para>
/// The rule only warns when it can justify the claim: either the array is unbounded (no
/// <c>maxItems</c>, so the size cannot be reasoned about at all), or the worst case can be computed
/// from a known item size and exceeds the limit. A bounded array of items whose length is unknowable
/// is left alone rather than guessed at.
/// </para>
/// </remarks>
public class LargeArrayQueryParameterValidationTests
{
    private const string TestFilePath = "test.yaml";

    [Fact]
    public void Warns_WhenArrayQueryParameterHasNoMaxItems()
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
                                  parameters:
                                    - name: ids
                                      in: query
                                      schema:
                                        type: array
                                        items:
                                          type: string
                                          format: uuid
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.LargeArrayQueryParameterShouldUseQueryMethod);
    }

    [Fact]
    public void Warns_WhenBoundedArrayOfGuidsExceedsTheRequestLineLimit()
    {
        // 1000 GUIDs at 36 characters each, plus "ids=" and a separator, is ~41 KB.
        const string yaml = """
                            openapi: "3.0.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                get:
                                  operationId: listResources
                                  parameters:
                                    - name: ids
                                      in: query
                                      schema:
                                        type: array
                                        maxItems: 1000
                                        items:
                                          type: string
                                          format: uuid
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.LargeArrayQueryParameterShouldUseQueryMethod);
    }

    [Fact]
    public void DoesNotWarn_WhenBoundedArrayOfGuidsFitsComfortably()
    {
        // 10 GUIDs is around 400 bytes — nowhere near the limit.
        const string yaml = """
                            openapi: "3.0.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                get:
                                  operationId: listResources
                                  parameters:
                                    - name: ids
                                      in: query
                                      schema:
                                        type: array
                                        maxItems: 10
                                        items:
                                          type: string
                                          format: uuid
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.LargeArrayQueryParameterShouldUseQueryMethod);
    }

    [Fact]
    public void DoesNotWarn_WhenBoundedArrayItemLengthIsUnknowable()
    {
        // A bounded array of unconstrained strings cannot be sized without guessing, and a guess
        // would produce false positives on specs that are perfectly fine. Silence is correct here.
        const string yaml = """
                            openapi: "3.0.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                get:
                                  operationId: listResources
                                  parameters:
                                    - name: tags
                                      in: query
                                      schema:
                                        type: array
                                        maxItems: 50
                                        items:
                                          type: string
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.LargeArrayQueryParameterShouldUseQueryMethod);
    }

    [Fact]
    public void Warns_WhenBoundedArrayOfLongStringsExceedsTheLimit()
    {
        // maxLength makes the worst case computable without guessing: 200 x 100 characters.
        const string yaml = """
                            openapi: "3.0.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                get:
                                  operationId: listResources
                                  parameters:
                                    - name: names
                                      in: query
                                      schema:
                                        type: array
                                        maxItems: 200
                                        items:
                                          type: string
                                          maxLength: 100
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.LargeArrayQueryParameterShouldUseQueryMethod);
    }

    [Fact]
    public void DoesNotWarn_WhenTheParameterIsNotAnArray()
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
                                  parameters:
                                    - name: id
                                      in: query
                                      schema:
                                        type: string
                                        format: uuid
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.LargeArrayQueryParameterShouldUseQueryMethod);
    }

    [Fact]
    public void DoesNotWarn_WhenTheOperationIsAlreadyAQuery()
    {
        // The author has already taken the advice this rule gives. Warning here would be noise.
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths:
                              /resources:
                                query:
                                  operationId: queryResources
                                  parameters:
                                    - name: ids
                                      in: query
                                      schema:
                                        type: array
                                        items:
                                          type: string
                                          format: uuid
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.LargeArrayQueryParameterShouldUseQueryMethod);
    }

    [Fact]
    public void Message_NamesTheParameterAndSuggestsTheQueryMethod()
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
                                  parameters:
                                    - name: ids
                                      in: query
                                      schema:
                                        type: array
                                        items:
                                          type: string
                                          format: uuid
                                  responses:
                                    200:
                                      description: OK
                            """;

        var diagnostics = Validate(yaml);

        var diagnostic = Assert.Single(
            diagnostics,
            d => d.RuleId == RuleIdentifiers.LargeArrayQueryParameterShouldUseQueryMethod);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("ids", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("listResources", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("414", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("query", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("maxItems", diagnostic.Message, StringComparison.Ordinal);
    }

    private static IReadOnlyList<DiagnosticMessage> Validate(string yaml)
    {
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        return OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);
    }
}