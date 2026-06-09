namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Compile-verification tests for the ComponentsReuse scenario.
/// Covers two things:
///   1. Path-item $ref (components.pathItems) — getItemById must be generated.
///   2. components.mediaTypes reuse — Item schema must be emitted.
/// </summary>
public class ComponentsReuseTests
{
    [Fact]
    public void RefPathItem_ServerGeneratesOperationAndCompiles()
    {
        // Arrange + Act — run server generator for the scenario.
        var sources = CompilationVerificationHarness.RunServer(
            "ComponentsReuse",
            "ComponentsReuse.yaml");

        // The $ref path item (/items/{id} → components/pathItems/ItemById) must appear.
        // Before the cast fix, `is OpenApiPathItem` silently drops the ref — this assertion
        // catches that regression.
        Assert.True(
            sources.Any(s => s.Source.Contains("getItemById", StringComparison.OrdinalIgnoreCase)),
            "Expected getItemById operation from the $ref path item to be generated.");

        // Generated server must compile without errors.
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources);
        Assert.True(
            errors.Count == 0,
            "Server for ComponentsReuse did not compile:\n" + string.Join("\n", errors));
    }

    [Fact]
    public void RefPathItem_ClientGeneratesOperationAndCompiles()
    {
        // Arrange + Act — run typed-client generator for the scenario.
        var sources = CompilationVerificationHarness.RunClient(
            "ComponentsReuse",
            "ComponentsReuse.yaml");

        // Same assertion: typed client must include getItemById.
        Assert.True(
            sources.Any(s => s.Source.Contains("getItemById", StringComparison.OrdinalIgnoreCase)),
            "Expected getItemById operation from the $ref path item in typed client.");

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources);
        Assert.True(
            errors.Count == 0,
            "Typed client for ComponentsReuse did not compile:\n" + string.Join("\n", errors));
    }

    [Fact]
    public void ReusableMediaType_ItemSchemaIsEmittedAndCompiles()
    {
        // Arrange + Act — server output must contain the Item model (used via the
        // components.mediaTypes $ref proxy; verifies proxy resolution is *sufficient*,
        // not merely that .Schema is non-null).
        var sources = CompilationVerificationHarness.RunServer(
            "ComponentsReuse",
            "ComponentsReuse.yaml");

        Assert.True(
            sources.Any(s => s.Source.Contains("class Item", StringComparison.Ordinal)),
            "Expected Item model to be emitted (reached via components.mediaTypes proxy).");

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources);
        Assert.True(
            errors.Count == 0,
            "Server for ComponentsReuse did not compile:\n" + string.Join("\n", errors));
    }

    [Fact]
    public void AnonymousInlineMediaType_ProducesWarningDiagnostic()
    {
        // A components.mediaTypes entry whose schema is an anonymous inline array
        // (items are inline objects, not $ref to components.schemas) should produce ATC_API_SCH019.
        const string yaml = """
            openapi: "3.2.0"
            info:
              title: Anon Test
              version: 1.0.0
            paths:
              /things:
                get:
                  operationId: listThings
                  tags:
                    - things
                  responses:
                    "200":
                      description: OK
                      content:
                        application/json:
                          $ref: '#/components/mediaTypes/ThingList'
            components:
              mediaTypes:
                ThingList:
                  schema:
                    type: array
                    items:
                      type: object
                      properties:
                        id:
                          type: string
            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Standard,
            doc,
            [],
            "anon-test.yaml");

        Assert.True(
            diagnostics.Any(d => d.RuleId == Generator.RuleIdentifiers.AnonymousInlineMediaTypeSchema),
            "Expected ATC_API_SCH019 warning for anonymous inline schema in components.mediaTypes.");
    }
}