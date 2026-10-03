namespace Atc.Rest.Api.Generator.Tests.Services;

/// <summary>
/// The diagnostics of a multi-part merge (<c>ATC_API_MPT*</c>): what a part may not contain, what the merge
/// cannot resolve, the success note, and a base file that is not there.
/// </summary>
public class MultiPartDiagnosticsTests
{
    private const string BaseYaml = """
        openapi: 3.1.1
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
        components:
          schemas:
            Thing:
              type: object
              properties:
                name: { type: string }
        """;

    [Fact]
    public void Merge_DuplicatePathInAPart_ReportsMPT001()
    {
        var result = Merge(BaseYaml, Part("""
            paths:
              /things:
                post:
                  operationId: createThing
                  responses:
                    '201':
                      description: Created
            """));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.RuleId == RuleIdentifiers.DuplicatePathInPart);
    }

    [Fact]
    public void Merge_DuplicateSchemaInAPart_ReportsMPT002()
    {
        var result = Merge(BaseYaml, Part("""
            paths:
              /others:
                get:
                  operationId: listOthers
                  responses:
                    '200':
                      description: OK
            components:
              schemas:
                Thing:
                  type: object
                  properties:
                    other: { type: string }
            """));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.RuleId == RuleIdentifiers.DuplicateSchemaInPart);
    }

    [Theory]
    [InlineData("servers:\n  - url: https://api.contoso.example")]
    [InlineData("components:\n  securitySchemes:\n    bearer_auth:\n      type: http\n      scheme: bearer")]
    public void ValidatePartFile_ProhibitedSection_ReportsMPT003(string section)
    {
        var part = SpecificationService.ReadFromContent("openapi: 3.1.1\n" + section + "\npaths: {}\n", "Contoso_Others.yaml");

        var diagnostics = SpecificationService.ValidatePartFile(part);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.PartFileContainsProhibitedSection);
    }

    [Fact]
    public void Merge_ThatSucceeds_ReportsMPT004()
    {
        var result = Merge(BaseYaml, Part("""
            paths:
              /others:
                get:
                  operationId: listOthers
                  responses:
                    '200':
                      description: OK
            """));

        Assert.True(result.IsSuccess);
        var info = Assert.Single(result.Diagnostics, d => d.RuleId == RuleIdentifiers.MultiPartMergeSuccessful);
        Assert.Equal(DiagnosticSeverity.Info, info.Severity);
        Assert.Contains("1 part file", info.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Merge_ReferenceNoFileDefines_ReportsMPT006()
    {
        var result = Merge(BaseYaml, Part("""
            paths:
              /others:
                get:
                  operationId: listOthers
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Nowhere'
            """));

        Assert.Contains(result.Diagnostics, d => d.RuleId == RuleIdentifiers.UnresolvedReferenceAfterMerge && d.Message.Contains("Nowhere", StringComparison.Ordinal));
    }

    /// <summary>
    /// A part's <c>info</c> is ignored (the merge takes it from the base file), and the part is told so.
    /// </summary>
    [Fact]
    public void ValidatePartFile_WithAnInfoSection_ReportsMPT007()
    {
        var part = SpecificationService.ReadFromContent("openapi: 3.1.1\ninfo:\n  title: Part\n  version: 2.0.0\npaths: {}\n", "Contoso_Others.yaml");

        var diagnostics = SpecificationService.ValidatePartFile(part);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.PartFileInfoIgnored);
        Assert.DoesNotContain(SpecificationService.ValidatePartFile(SpecificationService.ReadFromContent("openapi: 3.1.1\npaths: {}\n", "Contoso_Others.yaml")), d => d.RuleId == RuleIdentifiers.PartFileInfoIgnored);
    }

    [Fact]
    public void ReadAndMerge_BaseFileThatDoesNotExist_ReportsMPT008()
    {
        var missing = Path.Combine(Path.GetTempPath(), "atc-mpt", Guid.NewGuid().ToString("N"), "Contoso.yaml");

        var result = SpecificationService.ReadAndMerge(missing);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.RuleId == RuleIdentifiers.BaseFileNotFound && d.Message.Contains(missing, StringComparison.Ordinal));
    }

    private static string Part(string body)
        => "openapi: 3.1.1\n" + body + "\n";

    private static MergeResult Merge(
        string baseYaml,
        params string[] parts)
    {
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Contoso.yaml");
        var partFiles = parts.Select((p, i) => SpecificationService.ReadFromContent(p, $"Contoso_Part{i}.yaml")).ToList();
        return SpecificationService.MergeSpecifications(baseFile, partFiles, MultiPartConfiguration.Default);
    }
}