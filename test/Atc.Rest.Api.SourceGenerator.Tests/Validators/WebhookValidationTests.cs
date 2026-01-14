namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

public class WebhookValidationTests
{
    private const string TestFilePath = "test.yaml";

    //// ========== WBH003 Tests (Webhooks Detected - Info) ==========

    [Fact]
    public void Validate_WebhooksPresent_ReportsWBH003()
    {
        // Arrange
        var document = ParseYaml(CreateWebhookYaml(withOperationId: true, withRequestBody: true));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var wbh003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.WebhooksDetected);
        Assert.NotNull(wbh003);
        Assert.Contains("webhook", wbh003.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_NoWebhooks_NoWBH003()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.1.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /health:
                                get:
                                  operationId: healthCheck
                                  responses:
                                    '200':
                                      description: Success
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var wbh003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.WebhooksDetected);
        Assert.Null(wbh003);
    }

    //// ========== WBH001 Tests (Missing OperationId) ==========

    [Fact]
    public void Validate_WebhookMissingOperationId_ReportsWBH001()
    {
        // Arrange
        var document = ParseYaml(CreateWebhookYaml(withOperationId: false, withRequestBody: true));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var wbh001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.WebhookMissingOperationId);
        Assert.NotNull(wbh001);
        Assert.Contains("operationId", wbh001.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WebhookHasOperationId_NoWBH001()
    {
        // Arrange
        var document = ParseYaml(CreateWebhookYaml(withOperationId: true, withRequestBody: true));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var wbh001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.WebhookMissingOperationId);
        Assert.Null(wbh001);
    }

    //// ========== WBH002 Tests (Missing RequestBody) ==========

    [Fact]
    public void Validate_WebhookMissingRequestBody_ReportsWBH002()
    {
        // Arrange
        var document = ParseYaml(CreateWebhookYaml(withOperationId: true, withRequestBody: false));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var wbh002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.WebhookMissingRequestBody);
        Assert.NotNull(wbh002);
        Assert.Contains("request body", wbh002.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WebhookHasRequestBody_NoWBH002()
    {
        // Arrange
        var document = ParseYaml(CreateWebhookYaml(withOperationId: true, withRequestBody: true));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var wbh002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.WebhookMissingRequestBody);
        Assert.Null(wbh002);
    }

    [Fact]
    public void Validate_StandardMode_SkipsWebhookValidation()
    {
        // Arrange
        var document = ParseYaml(CreateWebhookYaml(withOperationId: false, withRequestBody: false));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Standard,
            document,
            [],
            TestFilePath);

        // Assert - Standard mode should not run strict validation
        var wbh001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.WebhookMissingOperationId);
        var wbh002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.WebhookMissingRequestBody);
        var wbh003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.WebhooksDetected);

        Assert.Null(wbh001);
        Assert.Null(wbh002);
        Assert.Null(wbh003);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;

    private static string CreateWebhookYaml(
        bool withOperationId,
        bool withRequestBody)
    {
        // Build webhook content based on parameters
        if (withOperationId && withRequestBody)
        {
            return """
                openapi: 3.1.0
                info:
                  title: Test API
                  version: 1.0.0
                paths: {}
                webhooks:
                  orderCreated:
                    post:
                      operationId: onOrderCreated
                      requestBody:
                        content:
                          application/json:
                            schema:
                              type: object
                      responses:
                        '200':
                          description: Webhook processed
                """;
        }

        if (withOperationId && !withRequestBody)
        {
            return """
                openapi: 3.1.0
                info:
                  title: Test API
                  version: 1.0.0
                paths: {}
                webhooks:
                  orderCreated:
                    post:
                      operationId: onOrderCreated
                      responses:
                        '200':
                          description: Webhook processed
                """;
        }

        if (!withOperationId && withRequestBody)
        {
            return """
                openapi: 3.1.0
                info:
                  title: Test API
                  version: 1.0.0
                paths: {}
                webhooks:
                  orderCreated:
                    post:
                      requestBody:
                        content:
                          application/json:
                            schema:
                              type: object
                      responses:
                        '200':
                          description: Webhook processed
                """;
        }

        // !withOperationId && !withRequestBody
        return """
            openapi: 3.1.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            webhooks:
              orderCreated:
                post:
                  responses:
                    '200':
                      description: Webhook processed
            """;
    }
}