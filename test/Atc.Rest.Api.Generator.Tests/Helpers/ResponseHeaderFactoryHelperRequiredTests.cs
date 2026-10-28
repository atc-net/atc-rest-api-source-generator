namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// A response header that carries a model reads and writes its properties as the model declares them: a required
/// <c>readOnly</c> property is a required field of the header exactly when the model keeps it required
/// (<see cref="SchemaUsageReachability.KeepsRequired"/>) - when only responses carry the model, not when requests carry it
/// too. With a path filter, the document the models are generated from decides.
/// </summary>
public class ResponseHeaderFactoryHelperRequiredTests
{
    private const string ThingsPath = """
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
                  headers:
                    X-Thing:
                      schema: { $ref: '#/components/schemas/Thing' }
        """;

    private const string AdminPath = """
          /admin/things:
            post:
              operationId: createAdminThing
              requestBody:
                required: true
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Thing' }
              responses:
                '204': { description: No Content }
        """;

    private const string Components = """
        components:
          schemas:
            Thing:
              type: object
              required: [id, name]
              properties:
                id: { type: integer, format: int32, readOnly: true }
                name: { type: string }
        """;

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AReadOnlyRequiredField_IsRequiredExactlyWhenTheModelKeepsItRequired(
        bool usedInARequestToo,
        bool expectedRequired)
    {
        var document = Parse(usedInARequestToo ? ThingsPath + "\n" + AdminPath : ThingsPath);

        var (idField, nameField) = FieldsOf(document, usage: null);

        var modelKeepsRequired = SchemaUsageReachability.KeepsRequired(
            SchemaUsageReachability.For(document),
            "Thing",
            ThingSchema(document).Properties!["id"]);
        Assert.Equal(expectedRequired, modelKeepsRequired);
        Assert.Equal(modelKeepsRequired, idField.IsRequired);
        Assert.True(nameField.IsRequired);
    }

    /// <summary>
    /// The model of a path-filtered document is used in responses only - its readOnly property stays required - and the
    /// header agrees with it, not with the whole document (where a left-out request carries the model too).
    /// </summary>
    [Fact]
    public void WithAPathFilter_AReadOnlyRequiredField_FollowsTheFilteredDocument()
    {
        var document = Parse(ThingsPath + "\n" + AdminPath);
        var filtered = PathFilterHelper.WithFilteredPaths(document, includePaths: null, excludePaths: ["/admin/**"]);

        var (idField, _) = FieldsOf(filtered, SchemaUsageReachability.For(filtered));

        Assert.True(SchemaUsageReachability.KeepsRequired(SchemaUsageReachability.For(filtered), "Thing", ThingSchema(filtered).Properties!["id"]));
        Assert.True(idField.IsRequired);
    }

    private static (ResponseHeaderField Id, ResponseHeaderField Name) FieldsOf(
        OpenApiDocument document,
        SchemaUsageReachability? usage)
    {
        var operation = document.Paths["/things"].Operations![HttpMethod.Get];
        var header = Assert.Single(ResponseHeaderFactoryHelper.DeclaredBy(operation, usage: usage));
        Assert.Equal(ResponseHeaderValueKind.Model, header.Kind);
        var fields = Assert.IsType<List<ResponseHeaderField>>(header.Fields, exactMatch: false);
        return (Assert.Single(fields, f => f.WireName == "id"), Assert.Single(fields, f => f.WireName == "name"));
    }

    private static OpenApiSchema ThingSchema(OpenApiDocument document)
        => Assert.IsType<OpenApiSchema>(document.Components!.Schemas!["Thing"]);

    private static OpenApiDocument Parse(string paths)
    {
        var yaml = "openapi: 3.1.1\ninfo:\n  title: Things\n  version: 1.0.0\npaths:\n" + paths + "\n" + Components;
        Assert.True(OpenApiDocumentHelper.TryParseYaml(yaml, "things.yaml", out var document));
        return document!;
    }
}