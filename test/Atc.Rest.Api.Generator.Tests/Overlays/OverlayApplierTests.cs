namespace Atc.Rest.Api.Generator.Tests.Overlays;

/// <summary>
/// An OpenAPI Overlay (1.0 to 1.2) applied to a document: the examples of the Overlay Specification 1.2.0
/// (section 4.6) give the documented result, every merge rule of the Action Object holds, and every way an overlay
/// can be wrong is reported - an error stops, a target that selects nothing is a warning.
/// </summary>
public sealed class OverlayApplierTests
{
    private const string PathsDocument = """
        openapi: 3.2.0
        info:
          title: Example API
          version: 1.0.0
        paths:
          /items:
            get:
              responses:
                200:
                  description: OK
          /some-items:
            delete:
              responses:
                200:
                  description: OK
        """;

    [Fact]
    public void Update_AddsAnElementToEveryArrayTheTargetSelects()
    {
        var result = Apply(
            """
            openapi: 3.2.0
            paths:
              /a: { get: { parameters: [ { name: x, in: query } ] } }
              /b: { get: { parameters: [] } }
            """,
            """
            overlay: 1.2.0
            info: { title: Add an array element, version: 1.0.0 }
            actions:
              - target: $.paths.*.get.parameters
                update:
                  name: newParam
                  in: query
            """);

        Assert.Equal("""[{"name":"x","in":"query"},{"name":"newParam","in":"query"}]""", result.Document!["paths"]!["/a"]!["get"]!["parameters"]!.ToJsonString());
        Assert.Equal("""[{"name":"newParam","in":"query"}]""", result.Document["paths"]!["/b"]!["get"]!["parameters"]!.ToJsonString());
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void Remove_DeletesTheArrayElementsAFilterSelects()
    {
        var result = Apply(
            """
            openapi: 3.2.0
            paths:
              /a: { get: { parameters: [ { name: dummy }, { name: keep }, { name: dummy } ], tags: [dummy, real] } }
            """,
            """
            overlay: 1.2.0
            info: { title: Remove array elements, version: 1.0.0 }
            actions:
              - target: $.paths.*.get.parameters[?@.name == 'dummy']
                remove: true
              - target: $.paths.*.get.tags[?@ == 'dummy']
                remove: true
            """);

        Assert.Equal("""[{"name":"keep"}]""", result.Document!["paths"]!["/a"]!["get"]!["parameters"]!.ToJsonString());
        Assert.Equal("""["real"]""", result.Document["paths"]!["/a"]!["get"]!["tags"]!.ToJsonString());
    }

    [Fact]
    public void Traits_AddParametersToTheOperationsThatDeclareTheTrait()
    {
        var result = Apply(
            """
            openapi: 3.2.0
            paths:
              /items:
                get:
                  x-oai-traits: ['paged']
              /other:
                get:
                  responses: {}
            """,
            """
            overlay: 1.2.0
            info: { title: Apply Traits, version: 1.0.0 }
            actions:
              - target: $.paths[?(@.get['x-oai-traits'][?(@ == 'paged')])].get
                update:
                  parameters:
                    - name: top
                      in: query
                    - name: skip
                      in: query
            """);

        Assert.Equal(2, result.Document!["paths"]!["/items"]!["get"]!["parameters"]!.AsArray().Count);
        Assert.Null(result.Document["paths"]!["/other"]!["get"]!["parameters"]);
    }

    [Fact]
    public void Copy_MergesTheSourceIntoTheTarget()
    {
        var result = Apply(
            PathsDocument,
            """
            overlay: 1.1.0
            info: { title: Copy contents of existing path to new location, version: 1.0.0 }
            actions:
              - target: '$.paths["/some-items"]'
                copy: '$.paths["/items"]'
            """);

        var someItems = result.Document!["paths"]!["/some-items"]!.AsObject();
        Assert.True(someItems.ContainsKey("get"));
        Assert.True(someItems.ContainsKey("delete"));
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void Move_IsAnUpdateACopyAndARemove()
    {
        var result = Apply(
            PathsDocument,
            """
            overlay: 1.1.0
            info: { title: Update the path for an API endpoint, version: 1.0.0 }
            actions:
              - target: '$.paths'
                update: { "/new-items": {} }
              - target: '$.paths["/new-items"]'
                copy: '$.paths["/items"]'
              - target: '$.paths["/items"]'
                remove: true
            """);

        Assert.Equal(["/some-items", "/new-items"], result.Document!["paths"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).Reverse(), StringComparer.Ordinal);
        Assert.NotNull(result.Document["paths"]!["/new-items"]!["get"]);
    }

    [Fact]
    public void ReusableAction_IsAppliedAtEachReferencesTarget()
    {
        var result = Apply(
            PathsDocument,
            """
            overlay: 1.2.0
            info: { title: Use reusable actions to insert error responses, version: 1.0.0 }
            components:
              actions:
                errorResponse:
                  fields:
                    update:
                      404:
                        description: Not Found
                  description: Adds an error response to the operation
            actions:
              - $ref: '#/components/actions/errorResponse'
                target: "$.paths['/items'].get.responses"
              - $ref: '#/components/actions/errorResponse'
                target: "$.paths['/some-items'].delete.responses"
            """);

        Assert.Equal("Not Found", (string?)result.Document!["paths"]!["/items"]!["get"]!["responses"]!["404"]!["description"]);
        Assert.Equal("Not Found", (string?)result.Document["paths"]!["/some-items"]!["delete"]!["responses"]!["404"]!["description"]);
    }

    [Fact]
    public void Update_MergesObjectsRecursively_ReplacesPrimitives_AndConcatenatesArrays()
    {
        var result = Apply(
            """
            openapi: 3.1.0
            components:
              schemas:
                Reading:
                  type: object
                  description: old
                  required: [id]
                  properties:
                    id: { type: string }
            """,
            """
            overlay: 1.0.0
            info: { title: t, version: 1.0.0 }
            actions:
              - target: $.components.schemas.Reading
                update:
                  description: new
                  required: [readAt]
                  properties:
                    readAt: { type: string, format: date-time }
            """);

        var reading = result.Document!["components"]!["schemas"]!["Reading"]!;
        Assert.Equal("new", (string?)reading["description"]);
        Assert.Equal("""["id","readAt"]""", reading["required"]!.ToJsonString());
        Assert.Equal("string", (string?)reading["properties"]!["id"]!["type"]);
        Assert.Equal("date-time", (string?)reading["properties"]!["readAt"]!["format"]);
    }

    [Fact]
    public void Update_ReplacesAPrimitiveTheTargetSelects()
    {
        var result = Apply(
            "openapi: 3.1.0\ninfo: { title: Old, version: 1.0.0 }",
            """
            overlay: 1.2.0
            info: { title: t, version: 1.0.0 }
            actions:
              - target: $.info.title
                update: New
            """);

        Assert.Equal("New", (string?)result.Document!["info"]!["title"]);
    }

    [Fact]
    public void TargetThatSelectsNothing_IsAWarning_AndChangesNothing()
    {
        var result = Apply(
            PathsDocument,
            """
            overlay: 1.2.0
            info: { title: t, version: 1.0.0 }
            actions:
              - target: $.paths['/renamed-by-the-vendor'].get
                description: Fix the listing
                update: { operationId: listThings }
            """);

        var problem = Assert.Single(result.Problems);
        Assert.False(problem.IsError);
        Assert.Equal(0, problem.ActionIndex);
        Assert.Contains("selects nothing", problem.Message, StringComparison.Ordinal);
        Assert.Contains("Fix the listing", problem.Message, StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(OverlayDocumentReader.Read(PathsDocument), result.Document));
    }

    [Theory]
    [InlineData("overlay: 2.0.0\ninfo: { title: t, version: 1 }\nactions: [ { target: $, remove: true } ]", "version")]
    [InlineData("overlay: 1.2.0\nactions: [ { target: $.a, remove: true } ]", "info")]
    [InlineData("overlay: 1.2.0\ninfo: { title: t, version: 1 }\nactions: []", "at least one action")]
    [InlineData("overlay: 1.2.0\ninfo: { title: t, version: 1 }\nactions: [ { target: '$.a[', remove: true } ]", "JSONPath")]
    [InlineData("overlay: 1.2.0\ninfo: { title: t, version: 1 }\nactions: [ { target: $.a, updte: {} } ]", "updte")]
    [InlineData("overlay: 1.2.0\ninfo: { title: t, version: 1 }\nactions: [ { target: $, remove: true } ]", "root")]
    [InlineData("overlay: 1.2.0\ninfo: { title: t, version: 1 }\nactions: [ { target: $.paths, copy: $.paths.* } ]", "exactly one node")]
    [InlineData("overlay: 1.2.0\ninfo: { title: t, version: 1 }\nactions: [ { target: \"$.paths['/items'].get\", update: [1] } ]", "object")]
    [InlineData("overlay: 1.2.0\ninfo: { title: t, version: 1 }\nactions: [ { target: $.paths.*.*, update: { x: 1 } }, { target: '$..description', update: { x: 1 } } ]", "primitive")]
    [InlineData("overlay: 1.2.0\ninfo: { title: t, version: 1 }\nactions: [ { target: $.info, update: { title: [1] } } ]", "title")]
    [InlineData("overlay: 1.2.0\ninfo: { title: t, version: 1 }\nactions: [ { $ref: '#/components/actions/missing', target: $.info } ]", "missing")]
    [InlineData("overlay: 1.2.0\ninfo: { title: t, version: 1 }\ncomponents: { actions: { a: { fields: { target: $.x, remove: true } } } }\nactions: [ { $ref: '#/components/actions/a', target: $.info } ]", "target")]
    public void InvalidOverlay_IsAnError_NamingWhatIsWrong(
        string overlay,
        string expected)
    {
        var result = Apply(PathsDocument, overlay);

        var error = Assert.Single(result.Problems, p => p.IsError);
        Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ExtensionsAreAllowedOnTheOverlayAndItsActions()
    {
        var result = Apply(
            PathsDocument,
            """
            overlay: 1.2.0
            x-owner: platform-team
            info: { title: t, version: 1.0.0, x-ticket: API-12 }
            actions:
              - target: $.info
                x-reason: vendor typo
                update: { title: Fixed }
            """);

        Assert.Empty(result.Problems);
        Assert.Equal("Fixed", (string?)result.Document!["info"]!["title"]);
    }

    [Fact]
    public void Remove_OfSeveralElementsOfOneArray_RemovesExactlyThose()
    {
        var result = Apply(
            "list: [a, b, c, d, e]",
            """
            overlay: 1.2.0
            info: { title: t, version: 1.0.0 }
            actions:
              - target: $.list[0,2,4,2]
                remove: true
            """);

        Assert.Equal("""["b","d"]""", result.Document!["list"]!.ToJsonString());
    }

    /// <summary>
    /// The JSONPath targets the wiki (Working with Overlays) documents select what it says they select.
    /// </summary>
    [Theory]
    [InlineData("$.info", "$['info']")]
    [InlineData("$.paths['/pets/{id}'].get", "$['paths']['/pets/{id}']['get']")]
    [InlineData("$.paths.*.get", "$['paths']['/pets']['get'] $['paths']['/pets/{id}']['get']")]
    [InlineData("$.components.schemas['Charge','Site']", "$['components']['schemas']['Charge'] $['components']['schemas']['Site']")]
    [InlineData("$.paths.*[?@.operationId == 'listPets']", "$['paths']['/pets']['get']")]
    [InlineData("$.paths.*[?@.deprecated == true]", "$['paths']['/pets/{id}']['delete']")]
    [InlineData("$.paths.*.*.parameters[?@.name == 'api-version']", "$['paths']['/pets']['get']['parameters'][0]")]
    [InlineData("$.paths.*.*.tags[?@ == 'Devices']", "$['paths']['/pets']['get']['tags'][1]")]
    [InlineData("$..properties[?@.format == 'date']", "$['components']['schemas']['Charge']['properties']['day']")]
    [InlineData("$.components.schemas[?length(@.required) > 3]", "$['components']['schemas']['Site']")]
    [InlineData("$.paths.*[?@.tags[?@ == 'internal']]", "$['paths']['/pets/{id}']['delete']")]
    [InlineData("$.paths.*.*.responses", "$['paths']['/pets']['get']['responses'] $['paths']['/pets/{id}']['get']['responses'] $['paths']['/pets/{id}']['delete']['responses']")]
    public void DocumentedTargets_SelectWhatTheWikiSays(
        string target,
        string expected)
    {
        var document = OverlayDocumentReader.Read(
            """
            openapi: 3.1.0
            info: { title: Pets, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [pets, Devices]
                  parameters: [ { name: api-version, in: query } ]
                  responses: {}
              /pets/{id}:
                get:
                  operationId: getPet
                  responses: {}
                delete:
                  operationId: deletePet
                  deprecated: true
                  tags: [internal]
                  responses: {}
            components:
              schemas:
                Charge:
                  required: [id]
                  properties:
                    day: { type: string, format: date }
                Site:
                  required: [id, name, location, visibility]
            """);

        var selected = JsonPathQuery.Parse(target).Select(document).Select(m => m.NormalizedPath);

        Assert.Equal(expected.Split(' '), selected, StringComparer.Ordinal);
    }

    private static OverlayApplication Apply(
        string document,
        string overlay)
        => OverlayApplier.Apply(OverlayDocumentReader.Read(document), OverlayDocumentReader.Read(overlay));
}