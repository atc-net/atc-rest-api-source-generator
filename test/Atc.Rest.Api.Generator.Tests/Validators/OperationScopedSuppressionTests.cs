namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A rule that finds something at an operation reports it at that operation - under every strategy, not only the
/// Strict rules: the diagnostic carries the operation as its place, so <c>x-atc-suppress</c> on the operation or on its
/// path item switches the rule off there, and the diagnostic is put on the operation's line.
/// </summary>
public class OperationScopedSuppressionTests
{
    private const string TestFilePath = "things.yaml";

    // Every problem is inside an operation: a status range, a HEAD response with content, one media type declared
    // twice with two schemas, and an operation without an operationId
    private const string Yaml = """
        openapi: 3.1.1
        info:
          title: Things
          version: 1.0.0
        paths:
          /things/{id}:
            parameters:
              - name: id
                in: path
                required: true
                schema:
                  type: string
            get:
              operationId: getThing
              responses:
                '200':
                  description: The thing
                  content:
                    application/json:
                      schema:
                        type: string
                    application/json; charset=utf-8:
                      schema:
                        type: integer
                '4XX':
                  description: A client error
            head:
              operationId: probeThing
              responses:
                '200':
                  description: The thing exists
                  content:
                    application/json:
                      schema:
                        type: string
          /others:
            get:
              responses:
                '200':
                  description: The others
        """;

    private static readonly string[] Rules =
    [
        RuleIdentifiers.ResponseStatusRange,
        RuleIdentifiers.HeadResponseContentIgnored,
        RuleIdentifiers.MediaTypeDeclaredTwice,
        RuleIdentifiers.OperationIdMissing,
    ];

    [Theory]
    [InlineData(ValidateSpecificationStrategy.None)]
    [InlineData(ValidateSpecificationStrategy.Standard)]
    [InlineData(ValidateSpecificationStrategy.Strict)]
    public void AWarningFoundAtAnOperation_IsReportedAtThatOperation(
        ValidateSpecificationStrategy strategy)
    {
        var diagnostics = Validate(strategy, Yaml);

        var found = diagnostics.Where(d => d.Severity != DiagnosticSeverity.Error && Rules.Contains(d.RuleId, StringComparer.Ordinal)).ToList();
        Assert.Contains(found, d => d.RuleId == RuleIdentifiers.ResponseStatusRange);
        Assert.All(found, d =>
        {
            Assert.True(d.Scope?.StartsWith("operation:", StringComparison.Ordinal) == true, $"{d.RuleId} has no operation as its place: {d.Message}");
            Assert.NotNull(d.LineNumber);
        });
    }

    [Theory]
    [InlineData(ValidateSpecificationStrategy.None)]
    [InlineData(ValidateSpecificationStrategy.Standard)]
    [InlineData(ValidateSpecificationStrategy.Strict)]
    public void XAtcSuppressOnTheOperation_SwitchesTheRulesOffThere(
        ValidateSpecificationStrategy strategy)
    {
        var suppress = $"      x-atc-suppress: [{string.Join(", ", Rules)}]\n";

        // The edits match "\n": the raw string has the line endings of the checkout (CRLF on Windows)
        var yaml = Yaml
            .ReplaceLineEndings("\n")
            .Replace("      operationId: getThing\n", "      operationId: getThing\n" + suppress, StringComparison.Ordinal)
            .Replace("      operationId: probeThing\n", "      operationId: probeThing\n" + suppress, StringComparison.Ordinal)
            .Replace("  /others:\n    get:\n", "  /others:\n    get:\n" + suppress, StringComparison.Ordinal);

        var diagnostics = Validate(strategy, yaml);

        var left = diagnostics.Where(d => d.Severity != DiagnosticSeverity.Error && Rules.Contains(d.RuleId, StringComparer.Ordinal));
        Assert.Empty(left);
    }

    [Fact]
    public void XAtcSuppressOnThePathItem_SwitchesARuleOffForItsOperations()
    {
        var yaml = Yaml.ReplaceLineEndings("\n").Replace(
            "  /things/{id}:\n",
            $"  /things/{{id}}:\n    x-atc-suppress: [{RuleIdentifiers.ResponseStatusRange}]\n",
            StringComparison.Ordinal);

        var diagnostics = Validate(ValidateSpecificationStrategy.Standard, yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ResponseStatusRange);
        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.HeadResponseContentIgnored);
    }

    private static List<DiagnosticMessage> Validate(
        ValidateSpecificationStrategy strategy,
        string yaml)
    {
        var (document, diagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, TestFilePath);
        Assert.NotNull(document);
        return OpenApiDocumentValidator.Validate(strategy, document, diagnostic?.Errors ?? [], TestFilePath, yaml);
    }
}