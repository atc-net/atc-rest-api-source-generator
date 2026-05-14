namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class InlineSchemaExtractorTests
{
    [Fact]
    public void ExtractRecordFromInlineSchemaWithInlineEnums_ScalarEnumProperty_GeneratesEnumType()
    {
        // An inline body / response schema (declared directly on the operation, not via
        // $ref to components/schemas) with an inline enum property must produce a
        // generated enum type whose name is {RecordTypeName}{PropertyName}, and surface
        // that enum to the caller as a side-output for separate file emission.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /reports/{id}:
                                get:
                                  operationId: getReport
                                  parameters:
                                    - name: id
                                      in: path
                                      required: true
                                      schema: { type: string }
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: object
                                            properties:
                                              id: { type: string }
                                              status:
                                                type: string
                                                enum: [draft, published, archived]
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);
        var op = document!.Paths!.First().Value.Operations![HttpMethod.Get];
        var responseSchema = (OpenApiSchema)op.Responses!["200"].Content!["application/json"].Schema!;

        var inlineEnums = new Dictionary<string, InlineEnumInfo>(StringComparer.Ordinal);
        var record = InlineSchemaExtractor.ExtractRecordFromInlineSchemaWithInlineEnums(
            responseSchema,
            typeName: "GetReportResponse",
            ns: "Demo.Generated.Reports.Models",
            pathSegment: "Reports",
            inlineEnumsByValuesKey: inlineEnums);

        var statusProp = record.Parameters!.Single(p => p.Name == "Status");
        Assert.Equal("GetReportResponseStatus", statusProp.TypeName);

        var inlineEnum = Assert.Single(inlineEnums.Values);
        Assert.Equal("GetReportResponseStatus", inlineEnum.TypeName);
    }

    [Fact]
    public void ExtractRecordFromInlineSchemaWithInlineEnums_ArrayOfEnumProperty_GeneratesListOfEnumType()
    {
        // An array of inline enums (`roles: { type: array, items: { type: string,
        // enum: [...] } }`) on an inline body/response schema must surface as
        // `List<{RecordTypeName}{PropertyName}>` and report the enum as a side-output.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /users:
                                get:
                                  operationId: listUsers
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: object
                                            properties:
                                              id: { type: string }
                                              roles:
                                                type: array
                                                items:
                                                  type: string
                                                  enum: [Admin, Manager, Guest]
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);
        var op = document!.Paths!.First().Value.Operations![HttpMethod.Get];
        var responseSchema = (OpenApiSchema)op.Responses!["200"].Content!["application/json"].Schema!;

        var inlineEnums = new Dictionary<string, InlineEnumInfo>(StringComparer.Ordinal);
        var record = InlineSchemaExtractor.ExtractRecordFromInlineSchemaWithInlineEnums(
            responseSchema,
            typeName: "ListUsersResponse",
            ns: "Demo.Generated.Users.Models",
            pathSegment: "Users",
            inlineEnumsByValuesKey: inlineEnums);

        var rolesProp = record.Parameters!.Single(p => p.Name == "Roles");
        Assert.Equal("List<ListUsersResponseRoles>", rolesProp.TypeName);

        var inlineEnum = Assert.Single(inlineEnums.Values);
        Assert.Equal("ListUsersResponseRoles", inlineEnum.TypeName);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}