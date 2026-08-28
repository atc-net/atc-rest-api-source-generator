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
        var op = document.Paths.First().Value.Operations[HttpMethod.Get];
        var responseSchema = op.Responses["200"].Content["application/json"].Schema as OpenApiSchema;

        var inlineEnums = new Dictionary<string, InlineEnumInfo>(StringComparer.Ordinal);
        var record = InlineSchemaExtractor.ExtractRecordFromInlineSchemaWithInlineEnums(
            responseSchema,
            typeName: "GetReportResponse",
            ns: "Demo.Generated.Reports.Models",
            pathSegment: "Reports",
            inlineEnumsByValuesKey: inlineEnums);

        var statusProp = record.Parameters.Single(p => p.Name == "Status");
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
        var op = document.Paths.First().Value.Operations[HttpMethod.Get];
        var responseSchema = op.Responses["200"].Content["application/json"].Schema as OpenApiSchema;

        var inlineEnums = new Dictionary<string, InlineEnumInfo>(StringComparer.Ordinal);
        var record = InlineSchemaExtractor.ExtractRecordFromInlineSchemaWithInlineEnums(
            responseSchema,
            typeName: "ListUsersResponse",
            ns: "Demo.Generated.Users.Models",
            pathSegment: "Users",
            inlineEnumsByValuesKey: inlineEnums);

        var rolesProp = record.Parameters.Single(p => p.Name == "Roles");
        Assert.Equal("List<ListUsersResponseRoles>", rolesProp.TypeName);

        var inlineEnum = Assert.Single(inlineEnums.Values);
        Assert.Equal("ListUsersResponseRoles", inlineEnum.TypeName);
    }

    [Fact]
    public void ExtractRecordFromInlineSchemaWithInlineEnums_UnderscoredProperty_EmitsJsonPropertyName()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /documents:
                                get:
                                  operationId: getDocuments
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: object
                                            properties:
                                              createdDateTime: { type: string }
                                              sender_MarketParticipant.name: { type: string }
                                              status:
                                                type: string
                                                enum: [draft, published]
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);
        var op = document.Paths.First().Value.Operations[HttpMethod.Get];
        var responseSchema = op.Responses["200"].Content["application/json"].Schema as OpenApiSchema;

        var inlineEnums = new Dictionary<string, InlineEnumInfo>(StringComparer.Ordinal);
        var record = InlineSchemaExtractor.ExtractRecordFromInlineSchemaWithInlineEnums(
            responseSchema,
            typeName: "GetDocumentsResponse",
            ns: "Demo.Generated.Documents.Models",
            pathSegment: "Documents",
            inlineEnumsByValuesKey: inlineEnums);

        var senderProp = record.Parameters.Single(p => p.Name == "SenderMarketParticipantName");
        Assert.NotNull(senderProp.Attributes);
        var attribute = senderProp.Attributes.Single(a => a.Name == "JsonPropertyName");
        Assert.Equal("\"sender_MarketParticipant.name\"", attribute.Content);

        var createdProp = record.Parameters.Single(p => p.Name == "CreatedDateTime");
        Assert.True(
            createdProp.Attributes is null ||
            createdProp.Attributes.All(a => a.Name != "JsonPropertyName"));
    }

    [Fact]
    public void ExtractRecordFromInlineSchema_UnderscoredProperty_EmitsJsonPropertyName()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /documents:
                                post:
                                  operationId: createDocument
                                  requestBody:
                                    content:
                                      application/json:
                                        schema:
                                          type: object
                                          properties:
                                            error_code: { type: string }
                                            name: { type: string }
                                  responses:
                                    '200':
                                      description: OK
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);
        var op = document.Paths.First().Value.Operations[HttpMethod.Post];
        var requestSchema = op.RequestBody!.Content["application/json"].Schema as OpenApiSchema;

        var record = InlineSchemaExtractor.ExtractRecordFromInlineSchema(
            requestSchema!,
            typeName: "CreateDocumentRequest",
            registry: null);

        var errorCodeProp = record.Parameters.Single(p => p.Name == "ErrorCode");
        Assert.NotNull(errorCodeProp.Attributes);
        var attribute = errorCodeProp.Attributes.Single(a => a.Name == "JsonPropertyName");
        Assert.Equal("\"error_code\"", attribute.Content);

        var nameProp = record.Parameters.Single(p => p.Name == "Name");
        Assert.True(
            nameProp.Attributes is null ||
            nameProp.Attributes.All(a => a.Name != "JsonPropertyName"));
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}