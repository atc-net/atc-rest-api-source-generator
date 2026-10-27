namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// An <c>x-retry-*</c> setting no generated policy reads is a warning (<c>ATC_API_RTY001</c>), not silence. A policy
/// takes its values from where its name is first declared, inheriting operation → path → document; settings anywhere
/// else are dropped - with no <c>x-retry-policy</c> in the document at all, every one of them. The exception is the
/// timeout an operation that is not retried keeps (next to <c>x-retry-enabled: false</c>, or a POST that does not opt in).
/// </summary>
public sealed class RetrySettingValidationTests
{
    public static TheoryData<string, string, string?> Cases
        => new()
        {
            // a description, the specification's retry lines, the setting the warning names (null: none)
            {
                "no policy anywhere: an operation's timeout",
                """
                paths:
                  /things:
                    get:
                      operationId: listThings
                      x-retry-timeout-seconds: 5
                      responses: { '200': { description: OK } }
                """,
                "x-retry-timeout-seconds"
            },
            {
                "no policy anywhere: the document's attempts",
                """
                x-retry-max-attempts: 5
                paths:
                  /things:
                    get:
                      operationId: listThings
                      responses: { '200': { description: OK } }
                """,
                "x-retry-max-attempts"
            },
            {
                "a document policy: an operation's attempts without its own policy",
                """
                x-retry-policy: standard
                paths:
                  /things:
                    get:
                      operationId: listThings
                      x-retry-max-attempts: 5
                      responses: { '200': { description: OK } }
                """,
                "x-retry-max-attempts"
            },
            {
                "a policy name declared again: the second declaration's settings",
                """
                x-retry-policy: standard
                paths:
                  /things:
                    get:
                      operationId: listThings
                      x-retry-policy: standard
                      x-retry-max-attempts: 5
                      responses: { '200': { description: OK } }
                """,
                "x-retry-max-attempts"
            },
            {
                "the operation's own policy reads its settings",
                """
                paths:
                  /things:
                    get:
                      operationId: listThings
                      x-retry-policy: things
                      x-retry-max-attempts: 5
                      x-retry-timeout-seconds: 10
                      responses: { '200': { description: OK } }
                """,
                null
            },
            {
                "a policy below the path reads the path's settings",
                """
                paths:
                  /things:
                    x-retry-max-attempts: 5
                    get:
                      operationId: listThings
                      x-retry-policy: things
                      responses: { '200': { description: OK } }
                """,
                null
            },
            {
                "the timeout of an operation that is not retried",
                """
                x-retry-policy: standard
                paths:
                  /things:
                    get:
                      operationId: listThings
                      x-retry-enabled: false
                      x-retry-timeout-seconds: 600
                      responses: { '200': { description: OK } }
                """,
                null
            },
            {
                "the timeout of a POST that does not opt in",
                """
                x-retry-policy: standard
                paths:
                  /things:
                    post:
                      operationId: createThing
                      x-retry-timeout-seconds: 20
                      responses: { '201': { description: Created } }
                """,
                null
            },
            {
                "a path timeout its not-retried operation keeps",
                """
                x-retry-policy: standard
                paths:
                  /things:
                    x-retry-timeout-seconds: 20
                    get:
                      operationId: listThings
                      x-retry-enabled: false
                      responses: { '200': { description: OK } }
                """,
                null
            },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Validate_ARetrySettingNoPolicyReads_IsAnRTY001WarningNamingIt(
        string description,
        string retryLines,
        string? expectedSetting)
    {
        var yaml = "openapi: 3.0.3\ninfo: { title: Contoso, version: 1.0.0 }\n" + retryLines;
        Assert.True(OpenApiDocumentHelper.TryParseYaml(yaml, "contoso.yaml", out var document), description);

        var standard = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, document!, [], "contoso.yaml");
        var none = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.None, document!, [], "contoso.yaml");

        var warnings = standard.Where(d => d.RuleId == RuleIdentifiers.RetrySettingNotApplied).ToList();
        if (expectedSetting is null)
        {
            Assert.True(warnings.Count == 0, $"{description}: {string.Join(" | ", warnings.Select(w => w.Message))}");
        }
        else
        {
            var warning = Assert.Single(warnings);
            Assert.Equal(Generator.Models.DiagnosticSeverity.Warning, warning.Severity);
            Assert.Contains(expectedSetting, warning.Message, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(none, d => d.RuleId == RuleIdentifiers.RetrySettingNotApplied);
    }
}