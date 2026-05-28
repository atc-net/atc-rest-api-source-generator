// ReSharper disable RedundantArgumentDefaultValue
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

    [Fact]
    public void IsDateParam_DateTimeFormat_ReturnsTrue()
    {
        var param = new OpenApiParameter
        {
            Name = "createdAt",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
        };

        Assert.True(TypeScriptOperationHelper.IsDateParam(param));
    }

    [Fact]
    public void IsDateParam_DateFormat_ReturnsTrue()
    {
        var param = new OpenApiParameter
        {
            Name = "birthday",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" },
        };

        Assert.True(TypeScriptOperationHelper.IsDateParam(param));
    }

    [Fact]
    public void IsDateParam_StringWithoutFormat_ReturnsFalse()
    {
        // A plain string param is not a date — must not trigger the Date branch in
        // GetParameterType nor get .toISOString() coercion in body emission.
        var param = new OpenApiParameter
        {
            Name = "name",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        };

        Assert.False(TypeScriptOperationHelper.IsDateParam(param));
    }

    [Fact]
    public void IsDateParam_StringWithUuidFormat_ReturnsFalse()
    {
        // Format is set but it's "uuid", not "date" or "date-time". Must not be a date.
        var param = new OpenApiParameter
        {
            Name = "id",
            In = ParameterLocation.Path,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
        };

        Assert.False(TypeScriptOperationHelper.IsDateParam(param));
    }

    [Fact]
    public void IsDateParam_IntegerSchema_ReturnsFalse()
    {
        var param = new OpenApiParameter
        {
            Name = "count",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Integer },
        };

        Assert.False(TypeScriptOperationHelper.IsDateParam(param));
    }

    [Fact]
    public void IsDateParam_NullSchema_ReturnsFalse()
    {
        var param = new OpenApiParameter { Name = "q", In = ParameterLocation.Query };

        Assert.False(TypeScriptOperationHelper.IsDateParam(param));
    }

    [Fact]
    public void GetDateSerializationSuffix_DateTimeFormat_ReturnsFullIsoString()
    {
        var param = new OpenApiParameter
        {
            Name = "from",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
        };

        Assert.Equal(".toISOString()", TypeScriptOperationHelper.GetDateSerializationSuffix(param));
    }

    [Fact]
    public void GetDateSerializationSuffix_DateFormat_TruncatesToDateOnly()
    {
        // OpenAPI `format: date` is YYYY-MM-DD on the wire. ISO datetime's substring(0, 10)
        // matches that contract; emitting full toISOString() would send the time portion too.
        var param = new OpenApiParameter
        {
            Name = "birthday",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" },
        };

        Assert.Equal(".toISOString().substring(0, 10)", TypeScriptOperationHelper.GetDateSerializationSuffix(param));
    }

    [Fact]
    public void GetParameterType_ConvertDatesTrue_DateTimeParam_ReturnsDate()
    {
        var param = new OpenApiParameter
        {
            Name = "from",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
        };

        Assert.Equal("Date", TypeScriptOperationHelper.GetParameterType(param, convertDates: true));
    }

    [Fact]
    public void GetParameterType_ConvertDatesTrue_DateParam_ReturnsDate()
    {
        var param = new OpenApiParameter
        {
            Name = "birthday",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" },
        };

        Assert.Equal("Date", TypeScriptOperationHelper.GetParameterType(param, convertDates: true));
    }

    [Fact]
    public void GetParameterType_ConvertDatesFalse_DateTimeParam_StillReturnsString()
    {
        // Default behavior must be preserved when --convert-dates is off, otherwise every
        // spec without that flag would silently start typing date params as Date.
        var param = new OpenApiParameter
        {
            Name = "from",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
        };

        Assert.Equal("string", TypeScriptOperationHelper.GetParameterType(param, convertDates: false));
    }

    [Fact]
    public void GetParameterType_ConvertDatesTrue_NonDateParam_FallsThroughToOriginalType()
    {
        // Only date params get the Date treatment — plain string params stay string.
        var param = new OpenApiParameter
        {
            Name = "name",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        };

        Assert.Equal("string", TypeScriptOperationHelper.GetParameterType(param, convertDates: true));
    }

    [Fact]
    public void BuildQueryTypeInline_ConvertDatesTrue_TypesDateParamAsDate()
    {
        var queryParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "from",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
            },
        };

        var result = TypeScriptOperationHelper.BuildQueryTypeInline(
            queryParams,
            TypeScriptNamingStrategy.CamelCase,
            convertDates: true);

        Assert.Contains("from?: Date", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQueryTypeInline_ConvertDatesFalse_LeavesDateParamAsString()
    {
        var queryParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "from",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
            },
        };

        var result = TypeScriptOperationHelper.BuildQueryTypeInline(queryParams, TypeScriptNamingStrategy.CamelCase);

        Assert.Contains("from?: string", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Date", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHeaderTypeInline_ConvertDatesTrue_TypesDateHeaderAsDate()
    {
        var headerParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "X-Since",
                In = ParameterLocation.Header,
                Required = false,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
            },
        };

        var result = TypeScriptOperationHelper.BuildHeaderTypeInline(headerParams, convertDates: true);

        // Header keys stay quoted to keep dashes valid, the value type becomes Date.
        Assert.Contains("'X-Since'?: Date", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInterpolatedPath_ConvertDatesTrue_DateTimePathParamEmitsToISOString()
    {
        // A date-time path param under convertDates=true must explicitly call toISOString()
        // in the template literal — JavaScript's implicit Date.toString() would send the
        // human-readable form ("Sat Jan 01 2026...") to the wire.
        var pathParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "snapshotAt",
                In = ParameterLocation.Path,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
            },
        };

        var result = TypeScriptOperationHelper.BuildInterpolatedPath(
            "/items/{snapshotAt}",
            pathParams,
            TypeScriptNamingStrategy.CamelCase,
            convertDates: true);

        Assert.Contains("${snapshotAt.toISOString()}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInterpolatedPath_ConvertDatesTrue_DatePathParamTruncatesToDateOnly()
    {
        // `format: date` path params should not include the time portion.
        var pathParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "day",
                In = ParameterLocation.Path,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" },
            },
        };

        var result = TypeScriptOperationHelper.BuildInterpolatedPath(
            "/days/{day}",
            pathParams,
            TypeScriptNamingStrategy.CamelCase,
            convertDates: true);

        Assert.Contains("${day.toISOString().substring(0, 10)}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInterpolatedPath_ConvertDatesFalse_DateParamDoesNotEmitToISOString()
    {
        // With convertDates off the param is still typed as `string`, no coercion needed.
        var pathParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "snapshotAt",
                In = ParameterLocation.Path,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
            },
        };

        var result = TypeScriptOperationHelper.BuildInterpolatedPath(
            "/items/{snapshotAt}",
            pathParams,
            TypeScriptNamingStrategy.CamelCase);

        Assert.Contains("${snapshotAt}", result, StringComparison.Ordinal);
        Assert.DoesNotContain("toISOString", result, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectSchemaRefTypes_StreamingResponseArrayAlias_AddsBothAliasAndItemType()
    {
        // A streaming op responds with a $ref to `Items`, where `Items = Item[]`.
        // The generated streaming hook yields the item type, so both `Items` (used by the
        // client method) AND `Item` (the yielded element) need to land in importTypes.
        // Without the chain-follow, the stream hook references `Item` with no matching import
        // → TS2552 "Cannot find name 'Item'".
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  x-return-async-enumerable: true
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Items'
                            components:
                              schemas:
                                Item:
                                  type: object
                                  properties:
                                    id: { type: string }
                                Items:
                                  type: array
                                  items:
                                    $ref: '#/components/schemas/Item'
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var op = doc!.Paths!.Values.First().Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports);

        Assert.Contains("Items", imports);
        Assert.Contains("Item", imports);
    }

    [Fact]
    public void CollectSchemaRefTypes_NonArrayAliasRef_DoesNotOverImport()
    {
        // The chain-follow logic must only fire for array aliases — chasing into a regular
        // object's properties used to drag every transitively-reachable model into every
        // client file. Defensive test: a $ref to a plain object adds only the named ref.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/ArrayTypes'
                            components:
                              schemas:
                                ArrayTypes:
                                  type: object
                                  properties:
                                    addresses:
                                      type: array
                                      items:
                                        $ref: '#/components/schemas/Address'
                                Address:
                                  type: object
                                  properties:
                                    street: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var op = doc!.Paths!.Values.First().Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports);

        Assert.Contains("ArrayTypes", imports);

        // The property-level Address ref must NOT be transitively pulled in — this is the
        // regression that ModelsAndProperties/ArraysClient hit when the property recursion
        // was overly broad.
        Assert.DoesNotContain("Address", imports);
    }

    [Fact]
    public void BuildQueryTypeInline_ParamHasStringDefault_EmitsInlineDefaultComment()
    {
        // OpenAPI `default: available` on a query param should surface as an inline
        // comment in the generated TS type literal so consumers can see the server's
        // default without having to read the spec.
        var queryParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "status",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Default = System.Text.Json.Nodes.JsonValue.Create("available"),
                },
            },
        };

        var result = TypeScriptOperationHelper.BuildQueryTypeInline(queryParams);

        Assert.Contains("status?: string /* default: 'available' */", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQueryTypeInline_ParamHasNumberDefault_EmitsInlineDefaultComment()
    {
        var queryParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "limit",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Integer,
                    Default = System.Text.Json.Nodes.JsonValue.Create(20),
                },
            },
        };

        var result = TypeScriptOperationHelper.BuildQueryTypeInline(queryParams);

        Assert.Contains("limit?: number /* default: 20 */", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQueryTypeInline_ParamHasBooleanDefault_EmitsInlineDefaultComment()
    {
        var queryParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "active",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Boolean,
                    Default = System.Text.Json.Nodes.JsonValue.Create(true),
                },
            },
        };

        var result = TypeScriptOperationHelper.BuildQueryTypeInline(queryParams);

        Assert.Contains("active?: boolean /* default: true */", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQueryTypeInline_ParamHasNoDefault_OmitsComment()
    {
        // Regression-guard: params without `default:` must stay clean (no stray comment).
        var queryParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "limit",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer },
            },
        };

        var result = TypeScriptOperationHelper.BuildQueryTypeInline(queryParams);

        Assert.Equal("{ limit?: number }", result);
        Assert.DoesNotContain("/* default:", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHeaderTypeInline_ParamHasDefault_EmitsInlineDefaultComment()
    {
        var headerParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "X-Api-Version",
                In = ParameterLocation.Header,
                Required = true,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Default = System.Text.Json.Nodes.JsonValue.Create("v1"),
                },
            },
        };

        var result = TypeScriptOperationHelper.BuildHeaderTypeInline(headerParams);

        Assert.Contains("'X-Api-Version': string /* default: 'v1' */", result, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectDeclared2xxDiscriminators_ReturnsArmsInDeclaredOrder()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /items:
                                post:
                                  operationId: createItem
                                  responses:
                                    '201':
                                      description: Created
                                    '202':
                                      description: Accepted async
                                    '400':
                                      description: Bad request
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var operation = ((OpenApiPathItem)doc!.Paths!["/items"]).Operations!.Values.First();

        var discriminators = TypeScriptOperationHelper.CollectDeclared2xxDiscriminators(operation);

        Assert.Equal(new[] { "created", "accepted" }, discriminators);
    }

    [Fact]
    public void CollectDeclared2xxDiscriminators_NoDeclaredSuccesses_DefaultsToOk()
    {
        // Hook narrowing emission would type-error with an empty list; the helper falls
        // back to ['ok'] so generated code still compiles for ops that omit success codes.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /items:
                                get:
                                  operationId: peekItems
                                  responses:
                                    '404':
                                      description: Not found
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var operation = ((OpenApiPathItem)doc!.Paths!["/items"]).Operations!.Values.First();

        var discriminators = TypeScriptOperationHelper.CollectDeclared2xxDiscriminators(operation);

        Assert.Equal(new[] { "ok" }, discriminators);
    }

    [Fact]
    public void BuildPerOperationResultType_NoResponses_EmitsOnlyParseError()
    {
        // Degenerate spec: an op declaring no responses still needs a callable shape.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /noop:
                                get:
                                  operationId: noop
                                  responses: {}
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var operation = ((OpenApiPathItem)doc!.Paths!["/noop"]).Operations!.Values.First();

        var (declaration, imports) = TypeScriptOperationHelper.BuildPerOperationResultType(
            operation,
            "NoopResult",
            isFileDownload: false,
            isTextDownload: false,
            TypeScriptHttpClient.Fetch);

        Assert.Contains("export type NoopResult =", declaration, StringComparison.Ordinal);
        Assert.Contains("status: 'parseError'", declaration, StringComparison.Ordinal);
        Assert.Empty(imports);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}