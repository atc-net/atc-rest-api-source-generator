namespace Atc.Rest.Api.SourceGenerator.Tests.Helpers;

/// <summary>
/// The first step of every generator: the specification is parsed (or its parts merged), its overlays applied, it is
/// validated, and generation goes on only when no error was reported. Every file path is unique to its test, since the
/// parser caches a document by its path.
/// </summary>
public class SpecificationLoaderTests
{
    private const string Pets = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: OK
                  content:
                    Application/JSON; charset=utf-8:
                      schema: { type: string }
                '4XX':
                  description: Client error
        """;

    private const string WithoutOperationIds = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /pets:
            get:
              responses:
                '200':
                  description: OK
        """;

    private const string AddOperationId = """
        overlay: 1.0.0
        info: { title: Contoso fixes, version: 1.0.0 }
        actions:
          - target: $.paths['/pets'].get
            update: { operationId: listPets }
        """;

    private const string StaleOverlay = """
        overlay: 1.0.0
        info: { title: Contoso fixes, version: 1.0.0 }
        actions:
          - target: $.paths['/old'].get
            update: { deprecated: true }
        """;

    private const string InvalidOverlay = """
        overlay: 1.0.0
        info: { title: Contoso fixes, version: 1.0.0 }
        actions: not-a-list
        """;

    [Fact]
    public void ParseAndValidate_ValidSpecification_IsTheDocument_WithCanonicalMediaTypesAndExplicitStatuses()
    {
        var (document, diagnostics, parsingErrors) = Parse(nameof(ParseAndValidate_ValidSpecification_IsTheDocument_WithCanonicalMediaTypesAndExplicitStatuses), Pets);

        Assert.NotNull(document);
        Assert.Empty(parsingErrors);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var responses = document.Paths["/pets"].Operations![HttpMethod.Get].Responses!;
        Assert.Equal(["200", "400"], responses.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["application/json"], responses["200"].Content!.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void ParseAndValidate_SpecificationTheParserCannotRead_IsNull_WithTheParsingErrorAndTheParsersReason()
    {
        const string path = "Contoso.ParseAndValidate.Unreadable.yaml";
        var (document, diagnostics, parsingErrors) = Parse(path, "openapi: 9.9.9\ninfo: { title: Contoso, version: 1.0.0 }\npaths: {}\n", raw: true);

        Assert.Null(document);
        Assert.Equal([path], parsingErrors, StringComparer.Ordinal);
        var error = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.OpenApiCoreError, error.Id);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
    }

    [Fact]
    public void ParseAndValidate_ValidationError_IsNull_AndTheErrorIsReported()
    {
        var (document, diagnostics, parsingErrors) = Parse(nameof(ParseAndValidate_ValidationError_IsNull_AndTheErrorIsReported), WithoutOperationIds, ValidateSpecificationStrategy.Strict);

        Assert.Null(document);
        Assert.Empty(parsingErrors);
        Assert.Contains(diagnostics, d => d.Id == RuleIdentifiers.OperationIdMissing && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ParseAndValidate_NoValidation_IsTheDocument_EvenWithoutOperationIds()
    {
        var (document, diagnostics, _) = Parse(nameof(ParseAndValidate_NoValidation_IsTheDocument_EvenWithoutOperationIds), WithoutOperationIds, ValidateSpecificationStrategy.None);

        Assert.NotNull(document);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ParseAndValidate_Overlay_IsAppliedBeforeTheValidation()
    {
        var (document, diagnostics, _) = Parse(
            nameof(ParseAndValidate_Overlay_IsAppliedBeforeTheValidation),
            WithoutOperationIds,
            ValidateSpecificationStrategy.Strict,
            new OverlayFile("Contoso.overlay.yaml", AddOperationId));

        Assert.NotNull(document);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("listPets", document.Paths["/pets"].Operations![HttpMethod.Get].OperationId);
    }

    [Fact]
    public void ParseAndValidate_OverlayThatCannotBeApplied_IsNull_AndAnError()
    {
        var (document, diagnostics, parsingErrors) = Parse(
            nameof(ParseAndValidate_OverlayThatCannotBeApplied_IsNull_AndAnError),
            Pets,
            ValidateSpecificationStrategy.Standard,
            new OverlayFile("Broken.overlay.yaml", InvalidOverlay));

        Assert.Null(document);
        Assert.Empty(parsingErrors);
        var error = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.OverlayInvalid, error.Id);
        Assert.Contains("Broken.overlay.yaml", error.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void ParseAndValidate_OverlayThatChangesNothing_IsAWarning_AndTheDocument()
    {
        var (document, diagnostics, _) = Parse(
            nameof(ParseAndValidate_OverlayThatChangesNothing_IsAWarning_AndTheDocument),
            Pets,
            ValidateSpecificationStrategy.Standard,
            new OverlayFile("Stale.overlay.yaml", StaleOverlay));

        Assert.NotNull(document);
        var warning = Assert.Single(diagnostics, d => d.Id == RuleIdentifiers.OverlayTargetSelectsNothing);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void ParseAndValidate_NoOverlays_IsTheSpecificationAsWritten()
    {
        var (document, _, _) = Parse(
            nameof(ParseAndValidate_NoOverlays_IsTheSpecificationAsWritten),
            WithoutOperationIds,
            ValidateSpecificationStrategy.None);

        Assert.NotNull(document);
        Assert.Null(document.Paths["/pets"].Operations![HttpMethod.Get].OperationId);
    }

    [Fact]
    public void MergeAndValidate_TheBaseAndItsParts_AreOneDocument()
    {
        var (baseFile, parts) = Files(nameof(MergeAndValidate_TheBaseAndItsParts_AreOneDocument));
        OpenApiDocument? document = null;

        var diagnostics = Run(context => document = SpecificationLoader.MergeAndValidate(context, baseFile, parts, null, ValidateSpecificationStrategy.Standard));

        Assert.NotNull(document);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal(["/owners", "/pets"], document.Paths.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);

        // The merged document is normalized like a single file
        var ownerResponses = document.Paths["/owners"].Operations![HttpMethod.Get].Responses!;
        Assert.Contains("500", ownerResponses.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void MergeAndValidate_PartThatRedeclaresAPath_IsNull_AndTheMergeErrorIsReported()
    {
        var name = nameof(MergeAndValidate_PartThatRedeclaresAPath_IsNull_AndTheMergeErrorIsReported);
        var (baseFile, _) = Files(name);
        YamlFileInfo[] parts = [new($"{name}_Pets.yaml", Part("/pets", "listPetsAgain"))];
        OpenApiDocument? document = new();

        var diagnostics = Run(context => document = SpecificationLoader.MergeAndValidate(context, baseFile, parts, null, ValidateSpecificationStrategy.Standard));

        Assert.Null(document);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void MergeAndValidate_ValidationErrorInAPart_IsNull()
    {
        var name = nameof(MergeAndValidate_ValidationErrorInAPart_IsNull);
        var (baseFile, _) = Files(name);
        YamlFileInfo[] parts = [new($"{name}_Owners.yaml", Part("/owners", operationId: null))];
        OpenApiDocument? document = new();

        var diagnostics = Run(context => document = SpecificationLoader.MergeAndValidate(context, baseFile, parts, null, ValidateSpecificationStrategy.Strict));

        Assert.Null(document);
        Assert.Contains(diagnostics, d => d.Id == RuleIdentifiers.OperationIdMissing);
    }

    [Fact]
    public void MergeAndValidate_Overlay_SeesThePartsToo()
    {
        var name = nameof(MergeAndValidate_Overlay_SeesThePartsToo);
        var (baseFile, _) = Files(name);
        YamlFileInfo[] parts = [new($"{name}_Owners.yaml", Part("/owners", operationId: null))];
        const string overlay = """
            overlay: 1.0.0
            info: { title: Contoso fixes, version: 1.0.0 }
            actions:
              - target: $.paths['/owners'].get
                update: { operationId: listOwners }
            """;
        OpenApiDocument? document = null;

        var diagnostics = Run(context => document = SpecificationLoader.MergeAndValidate(
            context,
            baseFile,
            parts,
            null,
            ValidateSpecificationStrategy.Strict,
            [new OverlayFile("Contoso.overlay.yaml", overlay)]));

        Assert.NotNull(document);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("listOwners", document.Paths["/owners"].Operations![HttpMethod.Get].OperationId);
    }

    [Fact]
    public void Merge_DoesNotValidate()
    {
        var name = nameof(Merge_DoesNotValidate);
        var (baseFile, _) = Files(name);
        YamlFileInfo[] parts = [new($"{name}_Owners.yaml", Part("/owners", operationId: null))];
        OpenApiDocument? document = null;

        var diagnostics = Run(context => document = SpecificationLoader.Merge(context, baseFile, parts, null, null));

        Assert.NotNull(document);
        Assert.DoesNotContain(diagnostics, d => d.Id == RuleIdentifiers.OperationIdMissing);
    }

    [Fact]
    public void Merge_PartThatCannotBeMerged_IsNull_AndNeverSilent()
    {
        var name = nameof(Merge_PartThatCannotBeMerged_IsNull_AndNeverSilent);
        var (baseFile, _) = Files(name);
        YamlFileInfo[] parts = [new($"{name}_Owners.yaml", "paths:\n  /owners:\n    get:\n      responses:\n        '200':\n          description: OK\n")];
        OpenApiDocument? document = new();

        var diagnostics = Run(context => document = SpecificationLoader.Merge(context, baseFile, parts, null, null));

        // A part without its openapi version cannot be read: the merge reports which part
        Assert.Null(document);
        var error = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains(parts[0].Path, error.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void Merge_NoParts_IsTheBase()
    {
        var (baseFile, _) = Files(nameof(Merge_NoParts_IsTheBase));
        OpenApiDocument? document = null;

        var diagnostics = Run(context => document = SpecificationLoader.Merge(context, baseFile, [], MultiPartConfiguration.Default, []));

        Assert.NotNull(document);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal(["/pets"], document.Paths.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void Merge_OverlayThatCannotBeApplied_IsNull_AndAnError()
    {
        var (baseFile, parts) = Files(nameof(Merge_OverlayThatCannotBeApplied_IsNull_AndAnError));
        OpenApiDocument? document = new();

        var diagnostics = Run(context => document = SpecificationLoader.Merge(context, baseFile, parts, null, [new OverlayFile("Broken.overlay.yaml", InvalidOverlay)]));

        Assert.Null(document);
        Assert.Contains(diagnostics, d => d.Id == RuleIdentifiers.OverlayInvalid);
    }

    private static (OpenApiDocument? Document, ImmutableArray<Diagnostic> Diagnostics, List<string> ParsingErrors) Parse(
        string name,
        string content,
        ValidateSpecificationStrategy strategy = ValidateSpecificationStrategy.Standard,
        OverlayFile? overlay = null,
        bool raw = false)
    {
        var path = raw ? name : $"Contoso.{name}.yaml";
        var parsingErrors = new List<string>();
        OpenApiDocument? document = null;

        var diagnostics = Run(context => document = SpecificationLoader.ParseAndValidate(
            context,
            path,
            content,
            strategy,
            (_, file) => parsingErrors.Add(file),
            overlay is null ? null : [overlay]));

        return (document, diagnostics, parsingErrors);
    }

    private static (YamlFileInfo BaseFile, YamlFileInfo[] Parts) Files(
        string name)
    {
        var baseFile = new YamlFileInfo(
            $"{name}.yaml",
            """
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  responses:
                    '200':
                      description: OK
            """);

        return (baseFile, [new YamlFileInfo($"{name}_Owners.yaml", Part("/owners", "listOwners"))]);
    }

    private static string Part(
        string path,
        string? operationId)
        => $$"""
            openapi: 3.0.3
            paths:
              {{path}}:
                get:
                  {{(operationId is null ? string.Empty : "operationId: " + operationId)}}
                  responses:
                    '200':
                      description: OK
                    5XX:
                      description: Server error
            """;

    private static ImmutableArray<Diagnostic> Run(
        Action<SourceProductionContext> action)
        => SourceProductionContextRunner.Run(action, TestContext.Current.CancellationToken);
}