namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptOperationHelperOtherMethodsTests
{
    [Fact]
    public void GetParameterType_NoSchema_FallsBackToString()
    {
        var param = new OpenApiParameter { Name = "q", In = ParameterLocation.Query };

        Assert.Equal("string", TypeScriptOperationHelper.GetParameterType(param));
    }

    [Fact]
    public void GetParameterType_InlineStringEnum_RendersLiteralUnion()
    {
        // An inline enum on a URL parameter (no $ref to a component schema) carries the
        // allowed values right there. Returning the primitive `string` loses every bit
        // of that information and lets callers pass arbitrary text. We want a TypeScript
        // literal union so the compiler enforces the valid set at the call site.
        var param = new OpenApiParameter
        {
            Name = "role",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum =
                [
                    System.Text.Json.Nodes.JsonValue.Create("Admin"),
                    System.Text.Json.Nodes.JsonValue.Create("Manager"),
                    System.Text.Json.Nodes.JsonValue.Create("Guest"),
                ],
            },
        };

        var result = TypeScriptOperationHelper.GetParameterType(param);

        Assert.Equal("'Admin' | 'Manager' | 'Guest'", result);
    }

    [Fact]
    public void GetParameterType_InlineIntegerEnum_RendersNumericLiteralUnion()
    {
        // Numeric enums (e.g., a status code filter) should produce a numeric literal
        // union — no quotes around the values.
        var param = new OpenApiParameter
        {
            Name = "level",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Enum =
                [
                    System.Text.Json.Nodes.JsonValue.Create(1),
                    System.Text.Json.Nodes.JsonValue.Create(2),
                    System.Text.Json.Nodes.JsonValue.Create(3),
                ],
            },
        };

        var result = TypeScriptOperationHelper.GetParameterType(param);

        Assert.Equal("1 | 2 | 3", result);
    }

    [Fact]
    public void GetParameterType_StripsTrailingPipeNullForUrlParameters()
    {
        // URL params are absent (undefined) or present (a value) — never the literal `null`.
        // Any "| null" tail must be stripped to keep the generated TS signature accurate.
        var param = new OpenApiParameter
        {
            Name = "q",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
        };

        var result = TypeScriptOperationHelper.GetParameterType(param);

        Assert.DoesNotContain("| null", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInterpolatedPath_SinglePathParam_EmitsTemplateLiteral()
    {
        var pathParams = new List<OpenApiParameter>
        {
            new() { Name = "petId", In = ParameterLocation.Path, Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        var result = TypeScriptOperationHelper.BuildInterpolatedPath("/pets/{petId}", pathParams, TypeScriptNamingStrategy.CamelCase);

        Assert.Contains("`/pets/${", result, StringComparison.Ordinal);
        Assert.Contains("petId", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInterpolatedPath_NoPathParams_ReturnsQuotedLiteral()
    {
        // No interpolation means a plain string literal — using a template literal would be noise.
        var result = TypeScriptOperationHelper.BuildInterpolatedPath("/pets", [], TypeScriptNamingStrategy.CamelCase);

        Assert.Equal("'/pets'", result);
    }

    [Fact]
    public void BuildInterpolatedPath_HonorsNamingStrategy()
    {
        // The OpenAPI param name is `pet_id` — with CamelCase the TS variable becomes `petId`.
        var pathParams = new List<OpenApiParameter>
        {
            new() { Name = "pet_id", In = ParameterLocation.Path, Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        var camel = TypeScriptOperationHelper.BuildInterpolatedPath("/pets/{pet_id}", pathParams, TypeScriptNamingStrategy.CamelCase);
        Assert.Contains("petId", camel, StringComparison.Ordinal);

        var original = TypeScriptOperationHelper.BuildInterpolatedPath("/pets/{pet_id}", pathParams, TypeScriptNamingStrategy.Original);
        Assert.Contains("pet_id", original, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMergedParameters_PathLevelAndOperationLevelMerged()
    {
        // OpenAPI lets parameters live on the path item AND the operation; both must be returned.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /pets/{petId}:
                                parameters:
                                  - name: petId
                                    in: path
                                    required: true
                                    schema:
                                      type: string
                                get:
                                  operationId: getPet
                                  parameters:
                                    - name: include
                                      in: query
                                      schema:
                                        type: string
                                  responses:
                                    '200': { description: OK }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var (_, pathItem) = doc!.Paths!.First();
        var op = pathItem.Operations![HttpMethod.Get];

        var pathParams = TypeScriptOperationHelper.GetMergedParameters(op, doc!, "/pets/{petId}", ParameterLocation.Path);
        var queryParams = TypeScriptOperationHelper.GetMergedParameters(op, doc!, "/pets/{petId}", ParameterLocation.Query);

        Assert.Contains(pathParams, p => p.Name == "petId");
        Assert.Contains(queryParams, p => p.Name == "include");
    }

    [Fact]
    public void CollectImportTypes_ResponseRefSchema_AddsTypeName()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Pet'
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var op = doc!.Paths!.Values.First().Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports);

        Assert.Contains("Pet", imports);
    }

    [Fact]
    public void CollectImportTypes_QueryParameterRefSchema_AddsTypeName()
    {
        // Query parameters whose schema is a $ref to an enum (or any component schema)
        // must be added to the import set, otherwise the generated TS files reference an
        // undeclared type and fail TS2304.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /people:
                                get:
                                  operationId: listPeople
                                  parameters:
                                    - name: businessLine
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/BusinessLine'
                                  responses:
                                    '200': { description: OK }
                            components:
                              schemas:
                                BusinessLine:
                                  type: string
                                  enum: [Alpha, Beta]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var op = doc!.Paths!.Values.First().Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports);

        Assert.Contains("BusinessLine", imports);
    }

    [Fact]
    public void CollectImportTypes_PathParameterRefSchema_AddsTypeName()
    {
        // Path parameters can also $ref enums — same import requirement applies.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /people/{businessLine}:
                                get:
                                  operationId: getByBusinessLine
                                  parameters:
                                    - name: businessLine
                                      in: path
                                      required: true
                                      schema:
                                        $ref: '#/components/schemas/BusinessLine'
                                  responses:
                                    '200': { description: OK }
                            components:
                              schemas:
                                BusinessLine:
                                  type: string
                                  enum: [Alpha, Beta]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var op = doc!.Paths!.Values.First().Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports);

        Assert.Contains("BusinessLine", imports);
    }

    [Fact]
    public void CollectImportTypes_HeaderParameterRefSchema_AddsTypeName()
    {
        // Header params ARE now surfaced in generated TS signatures, so their referenced
        // types must be imported just like query/path params.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /things:
                                get:
                                  operationId: listThings
                                  parameters:
                                    - name: X-Tier
                                      in: header
                                      schema:
                                        $ref: '#/components/schemas/Tier'
                                  responses:
                                    '200': { description: OK }
                            components:
                              schemas:
                                Tier:
                                  type: string
                                  enum: [Free, Pro]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var op = doc!.Paths!.Values.First().Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports);

        Assert.Contains("Tier", imports);
    }

    [Fact]
    public void CollectImportTypes_CookieParameterRefSchema_IsNotImported()
    {
        // Same rationale as the header test above — cookie params are not surfaced
        // in the generated TS method signature, so their referenced types must not
        // bloat the import list.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /things:
                                get:
                                  operationId: listThings
                                  parameters:
                                    - name: session
                                      in: cookie
                                      schema:
                                        $ref: '#/components/schemas/Session'
                                  responses:
                                    '200': { description: OK }
                            components:
                              schemas:
                                Session:
                                  type: string
                                  enum: [Active, Expired]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var op = doc!.Paths!.Values.First().Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports);

        Assert.DoesNotContain("Session", imports);
    }

    [Fact]
    public void CollectImportTypes_MultipleQueryParametersWithDifferentRefs_AddsAllTypeNames()
    {
        // Defence-in-depth: more than one query param, each $refing a different enum.
        // Both names must end up in the import set.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /things:
                                get:
                                  operationId: listThings
                                  parameters:
                                    - name: status
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/Status'
                                    - name: priority
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/Priority'
                                  responses:
                                    '200': { description: OK }
                            components:
                              schemas:
                                Status:
                                  type: string
                                  enum: [Active, Inactive]
                                Priority:
                                  type: string
                                  enum: [Low, High]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var op = doc!.Paths!.Values.First().Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports);

        Assert.Contains("Status", imports);
        Assert.Contains("Priority", imports);
    }

    [Fact]
    public void CollectImportTypes_PathItemLevelParameterRefSchema_AddsTypeName()
    {
        // Path-level parameters live on the pathItem (shared by every operation under that
        // path) and are merged into each operation's signature at write time. Their schemas
        // must therefore feed the import set too — without the doc + path, the helper has
        // no way to find them, so callers must supply both.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /people:
                                parameters:
                                  - name: businessLine
                                    in: query
                                    schema:
                                      $ref: '#/components/schemas/BusinessLine'
                                get:
                                  operationId: listPeople
                                  responses:
                                    '200': { description: OK }
                            components:
                              schemas:
                                BusinessLine:
                                  type: string
                                  enum: [Alpha, Beta]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var (path, pathItem) = doc!.Paths!.First();
        var op = pathItem.Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports, doc, path);

        Assert.Contains("BusinessLine", imports);
    }

    [Fact]
    public void CollectImportTypes_ReusableParameterRefSchemaInQuery_AddsTypeName()
    {
        // "Global" reusable parameters live under components.parameters and are pulled
        // in via $ref. Their resolved schema must feed the import set the same way an
        // inline parameter would — otherwise factoring a query filter out into a
        // reusable parameter would silently re-introduce the missing-import bug.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /things:
                                get:
                                  operationId: listThings
                                  parameters:
                                    - $ref: '#/components/parameters/BusinessLineFilter'
                                  responses:
                                    '200': { description: OK }
                            components:
                              parameters:
                                BusinessLineFilter:
                                  name: businessLine
                                  in: query
                                  schema:
                                    $ref: '#/components/schemas/BusinessLine'
                              schemas:
                                BusinessLine:
                                  type: string
                                  enum: [Alpha, Beta]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var op = doc!.Paths!.Values.First().Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports);

        Assert.Contains("BusinessLine", imports);
    }

    [Fact]
    public void GetStreamingItemType_ArraySchema_ReturnsItemType()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.String },
        };

        var result = TypeScriptOperationHelper.GetStreamingItemType(schema);

        // Streaming endpoints unwrap T[] to T at the per-item yield boundary.
        Assert.Equal("string", result);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}