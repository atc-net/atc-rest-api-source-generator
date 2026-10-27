namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A constraint the generated code could not carry is an error (<c>ATC_API_SCH025</c>) under every strategy, naming
/// the schema and the property: bounds the wrong way round, a bound that is no finite number, a pattern that is no
/// regular expression, an integer enum with a member that is no integer, an enum with no member but <c>null</c>.
/// Without the rule the generated attribute throws at run time, or a type is referenced that is never generated.
/// </summary>
[RuleTest("ATC_API_SCH025")]
[RuleTest("ATC_API_SCH026")]
[RuleTest("ATC_API_SCH028")]
public class SchemaConstraintValidatorTests
{
    private const string TestFilePath = "constraints.yaml";

    public static TheoryData<string, string, string> BadConstraints
        => new()
        {
            { "minLength past maxLength", "title: { type: string, minLength: 10, maxLength: 5 }", "minLength" },
            { "minimum past maximum", "count: { type: integer, minimum: 5, maximum: 1 }", "minimum" },
            { "minItems past maxItems", "tags: { type: array, items: { type: string }, minItems: 3, maxItems: 1 }", "minItems" },
            { "a bound that is no number", "count: { type: integer, minimum: abc }", "minimum" },
            { "a bound no double holds", "ratio: { type: number, maximum: 1e400 }", "maximum" },
            { "a pattern that is no regular expression", "code: { type: string, pattern: '[a-' }", "pattern" },
            { "an integer enum with a member that is no integer", "level: { type: integer, enum: [1, 2.5] }", "enum" },
            { "an enum with no member but null", "tone: { type: string, nullable: true, enum: [null] }", "enum" },
        };

    /// <summary>
    /// An enum that lists a value twice (JSON Schema: the values SHOULD be unique) is generated with the value once;
    /// <c>ATC_API_SCH026</c> says so, naming the value. Values that differ in case only are no duplicates.
    /// </summary>
    [Theory]
    [InlineData("state: { type: string, enum: [active, archived, archived] }", "archived")]
    [InlineData("level: { type: integer, enum: [1, 2, 2] }", "2")]
    public void Validate_AnEnumListingAValueTwice_IsAWarningNamingIt(
        string property,
        string value)
    {
        var document = Parse($"""
                Thing:
                  type: object
                  properties:
                    {property}
            """);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var warning = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.EnumValueListedTwice);
            Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
            Assert.Contains("Thing", warning.Message, StringComparison.Ordinal);
            Assert.Contains(value, warning.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.SchemaConstraintNotGeneratable);
        }
    }

    /// <summary>
    /// A <c>pattern</c> without <c>^...$</c>: the generated server and clients match the whole value, where JSON Schema
    /// matches anywhere in it - <c>ATC_API_SCH028</c> (Info) says so, on a property and on an inline parameter alike.
    /// An anchored pattern says nothing.
    /// </summary>
    [Theory]
    [InlineData("[A-Z]{2}\\d{4}", true)]
    [InlineData("^[A-Z]{2}\\d{4}", true)]
    [InlineData("[A-Z]{2}\\d{4}$", true)]
    [InlineData("^[A-Z]{2}\\d{4}$", false)]
    [InlineData("^[^:]+:.+$", false)]
    public void Validate_APatternWithoutAnchors_IsAnInformationNamingIt(
        string pattern,
        bool reported)
    {
        var document = OpenApiDocumentHelper.ParseYaml($$"""
            openapi: 3.0.3
            info: { title: T, version: 1.0.0 }
            paths:
              /things:
                get:
                  parameters:
                    - name: code
                      in: query
                      schema: { type: string, pattern: '{{pattern}}' }
                  responses: { '204': { description: OK } }
            components:
              schemas:
                Thing:
                  type: object
                  properties:
                    code: { type: string, pattern: '{{pattern}}' }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath)
            .Where(d => d.RuleId == RuleIdentifiers.PatternNotAnchored)
            .ToList();

        if (!reported)
        {
            Assert.Empty(diagnostics);
            return;
        }

        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal(DiagnosticSeverity.Info, d.Severity));
        Assert.Contains(diagnostics, d => d.Message.Contains("Thing", StringComparison.Ordinal) && d.Message.Contains(pattern, StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Message.Contains("code", StringComparison.Ordinal) && d.Message.Contains("GET /things", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EnumValuesDifferingInCaseOnly_AreNoDuplicates()
    {
        var document = Parse("""
                Thing:
                  type: object
                  properties:
                    state: { type: string, enum: [active, Active] }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.EnumValueListedTwice);
    }

    [Theory]
    [MemberData(nameof(BadConstraints))]
    public void Validate_AConstraintTheCodeCannotCarry_IsAnErrorNamingIt_UnderEveryStrategy(
        string what,
        string property,
        string keyword)
    {
        var document = Parse($"""
                Thing:
                  type: object
                  properties:
                    {property}
            """);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.SchemaConstraintNotGeneratable);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains("Thing", error.Message, StringComparison.Ordinal);
            Assert.Contains(keyword, error.Message, StringComparison.Ordinal);
            Assert.True(error.Message.Length > 0, what);
        }
    }

    [Fact]
    public void Validate_ConstraintsTheCodeCarries_AreNoError()
    {
        var document = Parse("""
                Thing:
                  type: object
                  properties:
                    title: { type: string, minLength: 1, maxLength: 80, pattern: '^[^\s].*$' }
                    count: { type: integer, minimum: 0, exclusiveMinimum: true, maximum: 100 }
                    ratio: { type: number, minimum: 0.5, maximum: 1e3 }
                    tags: { type: array, items: { type: string }, minItems: 0, maxItems: 10 }
                    level: { type: integer, enum: [1, 2, 3] }
                    tone: { type: string, nullable: true, enum: [warm, cold, null] }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.SchemaConstraintNotGeneratable);
    }

    private static OpenApiDocument Parse(string schemas)
    {
        var yaml = $$"""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Thing' }
            components:
              schemas:
            {{schemas}}
            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, TestFilePath);
        Assert.NotNull(document);
        return document;
    }
}