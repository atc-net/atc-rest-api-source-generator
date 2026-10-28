namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// An explicit empty <c>required: []</c> says every property is optional on purpose: from OpenAPI 3.1 (JSON Schema 2020-12
/// allows the empty list) it does not report <c>ATC_API_SCH022</c>, in YAML and JSON. An absent <c>required</c> still
/// does. OpenAPI 3.0's schema object takes <c>required</c> from JSON Schema draft 5, which asks for at least one name,
/// so there an empty list is no valid way to say it, and the warning stays.
/// </summary>
[RuleTest("ATC_API_SCH022")]
public class SchemaWithoutRequiredEmptyListTests
{
    [Theory]
    [InlineData("3.0.3", false, "[]", true)]
    [InlineData("3.0.3", true, "[]", true)]
    [InlineData("3.1.1", false, "[]", false)]
    [InlineData("3.1.1", true, "[]", false)]
    [InlineData("3.2.0", false, "[]", false)]
    [InlineData("3.2.0", true, "[]", false)]
    [InlineData("3.1.1", false, null, true)]
    [InlineData("3.1.1", true, null, true)]
    [InlineData("3.0.3", false, null, true)]
    public void AnEmptyRequiredList_IsAllOptionalOnPurpose_FromOpenApi31(
        string version,
        bool json,
        string? required,
        bool reported)
    {
        var text = json ? Json(version, required) : Yaml(version, required);
        var result = SpecificationPipeline.ParseAndValidate(json ? "things.json" : "things.yaml", text, ValidateSpecificationStrategy.Standard);

        Assert.NotNull(result.Document);
        var filter = Assert.IsType<OpenApiSchema>(result.Document!.Components!.Schemas!["ThingFilter"]);
        if (required is null)
        {
            Assert.Null(filter.Required);
        }
        else
        {
            Assert.NotNull(filter.Required);
            Assert.Empty(filter.Required!);
        }

        Assert.Equal(reported, result.Diagnostics.Any(d => d.RuleId == RuleIdentifiers.SchemaWithoutRequiredProperties));
    }

    /// <summary>
    /// A schema a part file declares keeps its empty list through the merge.
    /// </summary>
    [Theory]
    [InlineData("required: []", false)]
    [InlineData("", true)]
    public void AnEmptyRequiredListInAPartFile_IsAllOptionalOnPurpose(
        string required,
        bool reported)
    {
        const string baseYaml = """
            openapi: 3.1.1
            info: { title: Things, version: 1.0.0 }
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '204': { description: No Content }
            """;
        var part = $$"""
            openapi: 3.1.1
            paths:
              /things/search:
                post:
                  operationId: searchThings
                  requestBody:
                    content:
                      application/json:
                        schema: { $ref: '#/components/schemas/ThingFilter' }
                  responses:
                    '204': { description: No Content }
            components:
              schemas:
                ThingFilter:
                  type: object
                  {{required}}
                  properties:
                    name: { type: string }
            """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Things.yaml");
        var partFile = SpecificationService.ReadFromContent(part, "Things_Search.yaml");
        var merged = SpecificationService.MergeSpecifications(baseFile, [partFile], MultiPartConfiguration.Default);
        Assert.True(merged.IsSuccess);

        var result = SpecificationPipeline.Validate(merged.Document!, [("Things.yaml", baseYaml), ("Things_Search.yaml", part)], ValidateSpecificationStrategy.Standard);

        Assert.Equal(reported, result.Diagnostics.Any(d => d.RuleId == RuleIdentifiers.SchemaWithoutRequiredProperties));
    }

    private static string Yaml(
        string version,
        string? required)
        => $$"""
             openapi: {{version}}
             info: { title: Things, version: 1.0.0 }
             paths:
               /things:
                 post:
                   operationId: searchThings
                   requestBody:
                     content:
                       application/json:
                         schema: { $ref: '#/components/schemas/ThingFilter' }
                   responses:
                     '204': { description: No Content }
             components:
               schemas:
                 ThingFilter:
                   type: object
             {{(required is null ? string.Empty : "      required: " + required)}}
                   properties:
                     name: { type: string }
             """;

    private static string Json(
        string version,
        string? required)
        => $$"""
             {
               "openapi": "{{version}}",
               "info": { "title": "Things", "version": "1.0.0" },
               "paths": {
                 "/things": {
                   "post": {
                     "operationId": "searchThings",
                     "requestBody": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ThingFilter" } } } },
                     "responses": { "204": { "description": "No Content" } }
                   }
                 }
               },
               "components": {
                 "schemas": {
                   "ThingFilter": {
                     "type": "object",
                     {{(required is null ? string.Empty : "\"required\": " + required + ",")}}
                     "properties": { "name": { "type": "string" } }
                   }
                 }
               }
             }
             """;
}