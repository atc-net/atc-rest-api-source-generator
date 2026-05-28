namespace Atc.Rest.Api.Generator.Cli.Tests.Services;

public class TypeScriptClientGenerationServiceTests
{
    [Fact]
    public void Generate_OperationWithCookieParameter_EmitsWarningNamingParamAndOperation()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /users/{userId}:
                                get:
                                  operationId: getUser
                                  parameters:
                                    - name: userId
                                      in: path
                                      required: true
                                      schema: { type: string }
                                    - name: sessionId
                                      in: cookie
                                      schema: { type: string }
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var config = new TypeScriptClientConfig { DryRun = true };

        var result = TypeScriptClientGenerationService.Generate(document!, outputPath: "n/a", config);

        // Cookie params are deliberately not emitted in the TS client, but silent skipping
        // is bad DX — the warning surfaces the skip so spec authors know what to expect.
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("GET /users/{userId}", warning, StringComparison.Ordinal);
        Assert.Contains("sessionId", warning, StringComparison.Ordinal);
        Assert.Contains("credentials: 'include'", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_PathItemLevelCookieParameter_IncludedInWarning()
    {
        // Cookie params can be declared at the path-item level so every operation under
        // that path inherits them; the warning needs to surface those too, not just the
        // operation-level ones.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                parameters:
                                  - name: tenantId
                                    in: cookie
                                    schema: { type: string }
                                get:
                                  operationId: listItems
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var config = new TypeScriptClientConfig { DryRun = true };

        var result = TypeScriptClientGenerationService.Generate(document!, outputPath: "n/a", config);

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("tenantId", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_NoCookieParameters_WarningsListEmpty()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var config = new TypeScriptClientConfig { DryRun = true };

        var result = TypeScriptClientGenerationService.Generate(document!, outputPath: "n/a", config);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Generate_MultipleCookieParametersOnOneOperation_SingleWarningListsAll()
    {
        // One warning per operation keeps the report from blowing up on ops that declare
        // several cookie params (e.g. session + tenant + locale).
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /dashboard:
                                get:
                                  operationId: getDashboard
                                  parameters:
                                    - name: sessionId
                                      in: cookie
                                      schema: { type: string }
                                    - name: tenantId
                                      in: cookie
                                      schema: { type: string }
                                    - name: locale
                                      in: cookie
                                      schema: { type: string }
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var config = new TypeScriptClientConfig { DryRun = true };

        var result = TypeScriptClientGenerationService.Generate(document!, outputPath: "n/a", config);

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("sessionId", warning, StringComparison.Ordinal);
        Assert.Contains("tenantId", warning, StringComparison.Ordinal);
        Assert.Contains("locale", warning, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}