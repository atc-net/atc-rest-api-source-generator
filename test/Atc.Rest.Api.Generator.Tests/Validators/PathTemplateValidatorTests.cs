namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A path template the generated code could not carry is an error under every strategy (<c>ATC_API_PTH001</c>): a
/// placeholder that is no name (<c>{*rest}</c>, <c>{id:int}</c>, <c>{id?}</c> - ASP.NET route syntax, not OpenAPI),
/// the same placeholder twice, an empty segment, a path that does not start with <c>/</c>, a character that breaks
/// the route or the C# literal (<c>"</c>, <c>\</c>, whitespace, <c>?</c>, <c>#</c>). A placeholder no parameter
/// declares is <c>ATC_API_OPR012</c> under every strategy too (the client names a variable that does not exist).
/// </summary>
public class PathTemplateValidatorTests
{
    private const string TestFilePath = "paths.yaml";

    [Theory]
    [InlineData("/pets/{*rest}")]
    [InlineData("/pets/{id:int}")]
    [InlineData("/pets/{id?}")]
    [InlineData("/pets/{id}/{id}")]
    [InlineData("/pets//list")]
    [InlineData("/pets/\"quoted\"")]
    [InlineData("/pets/back\\slash")]
    [InlineData("/pets?x=1")]
    [InlineData("/pets#top")]
    [InlineData("/pets/a b")]
    [InlineData("/pets/{id")]
    public void Validate_APathTemplateTheCodeCannotCarry_IsAnError_UnderEveryStrategy(
        string path)
    {
        var document = Parse(path, declaredParameter: null);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.PathParametersNotWellFormatted);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains(path, error.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("/pets/{id}.{format}", "id")]
    [InlineData("/pets:batch", null)]
    [InlineData("/pets/", null)]
    [InlineData("/pets/{pet-id}", "pet-id")]
    [InlineData("/pets/{pet_id}", "pet_id")]
    [InlineData("/", null)]
    public void Validate_APathTemplateTheCodeCarries_IsNoError(
        string path,
        string? declaredParameter)
    {
        var document = Parse(path, declaredParameter, alsoDeclare: path.Contains("{format}", StringComparison.Ordinal) ? "format" : null);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.None, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.PathParametersNotWellFormatted);
    }

    [Fact]
    public void Validate_APlaceholderNoParameterDeclares_IsAnError_UnderEveryStrategy()
    {
        var document = Parse("/pets/{id}", declaredParameter: null);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.OperationMissingPathParameter);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains("'id'", error.Message, StringComparison.Ordinal);
            Assert.Contains("getPet", error.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_APlaceholderAParameterDeclares_OnTheOperationOrThePath_IsNoError(
        bool onThePath)
    {
        var document = Parse("/pets/{id}", declaredParameter: "id", onThePath: onThePath);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.None, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.OperationMissingPathParameter);
    }

    private static OpenApiDocument Parse(
        string path,
        string? declaredParameter,
        bool onThePath = false,
        string? alsoDeclare = null)
    {
        var names = new[] { declaredParameter, alsoDeclare }.Where(n => n is not null).ToList();
        var parameters = names.Count == 0
            ? string.Empty
            : "parameters:\n" + string.Join("\n", names.Select(n => $"  - {{ name: {n}, in: path, required: true, schema: {{ type: string }} }}"));
        var pathLevel = onThePath ? Indent(parameters, 4) : string.Empty;
        var operationLevel = onThePath ? string.Empty : Indent(parameters, 6);
        var yaml = $$"""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              "{{path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}}":
            {{pathLevel}}
                get:
                  operationId: getPet
            {{operationLevel}}
                  responses:
                    '200':
                      description: OK
            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, TestFilePath);
        Assert.NotNull(document);
        return document;
    }

    private static string Indent(
        string text,
        int spaces)
        => string.IsNullOrEmpty(text)
            ? string.Empty
            : string.Join("\n", text.Split('\n').Select(l => new string(' ', spaces) + l));
}