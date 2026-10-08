namespace Atc.OpenApi.Tests.Extensions;

public class OpenApiParameterExtensionsTests
{
    // ========== Resolve Tests ==========
    [Fact]
    public void Resolve_DirectParameter_ReturnsParameterWithNullReferenceId()
    {
        IOpenApiParameter param = new OpenApiParameter { Name = "id", In = ParameterLocation.Path };

        var result = param.Resolve();

        Assert.NotNull(result.Parameter);
        Assert.Equal("id", result.Parameter.Name);
        Assert.Null(result.ReferenceId);
    }

    // ========== GetName Tests ==========
    [Fact]
    public void GetName_DirectParameter_ReturnsName()
    {
        IOpenApiParameter param = new OpenApiParameter { Name = "petId" };

        var result = param.GetName();

        Assert.Equal("petId", result);
    }

    // ========== ToCSharpType Tests ==========
    [Fact]
    public void ToCSharpType_NoSchema_ReturnsString()
    {
        var param = new OpenApiParameter { Name = "id" };

        var result = param.ToCSharpType();

        Assert.Equal("string", result);
    }

    // ========== GetBindingAttributeName Tests ==========
    [Theory]
    [InlineData(ParameterLocation.Query, "FromQuery")]
    [InlineData(ParameterLocation.Path, "FromRoute")]
    [InlineData(ParameterLocation.Header, "FromHeader")]
    [InlineData(ParameterLocation.Cookie, null)]
    public void GetBindingAttributeName_ReturnsExpected(
        ParameterLocation location,
        string? expected)
    {
        var param = new OpenApiParameter { Name = "test", In = location };

        var result = param.GetBindingAttributeName();

        Assert.Equal(expected, result);
    }

    // ========== IsValueType Tests ==========
    [Fact]
    public void IsValueType_IntegerSchema_ReturnsTrue()
    {
        var param = CreateParameterWithType(JsonSchemaType.Integer);

        Assert.True(param.IsValueType());
    }

    [Fact]
    public void IsValueType_NumberSchema_ReturnsTrue()
    {
        var param = CreateParameterWithType(JsonSchemaType.Number);

        Assert.True(param.IsValueType());
    }

    [Fact]
    public void IsValueType_BooleanSchema_ReturnsTrue()
    {
        var param = CreateParameterWithType(JsonSchemaType.Boolean);

        Assert.True(param.IsValueType());
    }

    [Fact]
    public void IsValueType_StringSchema_ReturnsFalse()
    {
        var param = CreateParameterWithType(JsonSchemaType.String);

        Assert.False(param.IsValueType());
    }

    [Fact]
    public void IsValueType_NullableInteger_ReturnsTrue()
    {
        var param = CreateParameterWithType(JsonSchemaType.Integer | JsonSchemaType.Null);

        Assert.True(param.IsValueType());
    }

    [Fact]
    public void IsValueType_NoSchema_ReturnsFalse()
    {
        var param = new OpenApiParameter { Name = "test" };

        Assert.False(param.IsValueType());
    }

    // ========== GetParameterSerialization Tests ==========
    [Fact]
    public void GetParameterSerialization_QueryArray_DefaultsToFormExplodeSupported()
    {
        var param = new OpenApiParameter
        {
            Name = "tags",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        var s = param.GetParameterSerialization();

        Assert.Equal(ParameterStyle.Form, s.Style);
        Assert.True(s.Explode);
        Assert.Equal(ParameterValueKind.Array, s.ValueKind);
        Assert.True(s.IsSupported);
        Assert.False(s.AllowReserved);
    }

    [Fact]
    public void GetParameterSerialization_QueryPrimitive_DefaultsToFormSupported()
    {
        var param = new OpenApiParameter { Name = "q", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
        var s = param.GetParameterSerialization();
        Assert.Equal(ParameterStyle.Form, s.Style);
        Assert.Equal(ParameterValueKind.Primitive, s.ValueKind);
        Assert.True(s.IsSupported);
    }

    [Fact]
    public void GetParameterSerialization_PathPrimitive_DefaultsToSimpleSupported()
    {
        var param = new OpenApiParameter { Name = "id", In = ParameterLocation.Path, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
        var s = param.GetParameterSerialization();
        Assert.Equal(ParameterStyle.Simple, s.Style);
        Assert.True(s.IsSupported);
    }

    [Fact]
    public void GetParameterSerialization_DeepObjectArray_NotSupported()
    {
        var param = new OpenApiParameter
        {
            Name = "tags",
            In = ParameterLocation.Query,
            Style = ParameterStyle.DeepObject,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
        };
        Assert.False(param.GetParameterSerialization().IsSupported);
    }

    /// <summary>
    /// The query array styles the generated server and clients carry: repeated keys (explode: true) or one value
    /// with the style's delimiter between the items (explode: false).
    /// </summary>
    [Theory]
    [InlineData(ParameterStyle.Form, true, null)]
    [InlineData(ParameterStyle.Form, false, ',')]
    [InlineData(ParameterStyle.PipeDelimited, false, '|')]
    [InlineData(ParameterStyle.PipeDelimited, true, null)]
    [InlineData(ParameterStyle.SpaceDelimited, false, ' ')]
    [InlineData(ParameterStyle.SpaceDelimited, true, null)]
    public void GetParameterSerialization_QueryArrayStyles_AreSupported_WithTheirDelimiter(
        ParameterStyle style,
        bool explode,
        char? delimiter)
    {
        var param = new OpenApiParameter
        {
            Name = "tags",
            In = ParameterLocation.Query,
            Style = style,
            Explode = explode,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        Assert.True(param.GetParameterSerialization().IsSupported);
        Assert.Equal(delimiter, param.GetArrayDelimiter());
    }

    [Fact]
    public void GetArrayDelimiter_IsNullForAnythingButAQueryArray()
    {
        var header = new OpenApiParameter
        {
            Name = "tags",
            In = ParameterLocation.Header,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
        };
        var primitive = new OpenApiParameter
        {
            Name = "tag",
            In = ParameterLocation.Query,
            Explode = false,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        };

        Assert.Null(header.GetArrayDelimiter());
        Assert.Null(primitive.GetArrayDelimiter());
    }

    [Fact]
    public void GetParameterSerialization_AllowReserved_IsCaptured()
    {
        var param = new OpenApiParameter { Name = "q", In = ParameterLocation.Query, AllowReserved = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
        Assert.True(param.GetParameterSerialization().AllowReserved);
    }

    [Fact]
    public void GetParameterSerialization_ParsedFromYaml_QueryArrayNoExplode_DefaultsToFormExplodeSupported()
    {
        // Locks the parse-path behavior the seam depends on: when explode is NOT declared,
        // the YAML reader applies the OpenAPI style-based default (true for Form). Guards
        // against future Microsoft.OpenApi changes that the construct-object tests can't cover.
        const string yaml = """
                            openapi: 3.0.3
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  parameters:
                                    - name: tags
                                      in: query
                                      schema:
                                        type: array
                                        items:
                                          type: string
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var operation = document.Paths["/items"].Operations.Values.First();
        var param = operation.Parameters[0].Resolve().Parameter;

        Assert.NotNull(param);
        var s = param.GetParameterSerialization();

        Assert.Equal(ParameterStyle.Form, s.Style);
        Assert.True(s.Explode);
        Assert.Equal(ParameterValueKind.Array, s.ValueKind);
        Assert.True(s.IsSupported);
    }

    // ========== GetQueryObject Tests ==========
    [Theory]
    [InlineData("in: query, style: deepObject, explode: true, schema: { $ref: '#/components/schemas/Flat' }", true, true)]
    [InlineData("in: query, schema: { $ref: '#/components/schemas/Flat' }", true, false)]
    [InlineData("in: query, style: form, explode: true, schema: { $ref: '#/components/schemas/WithWrappedEnum' }", true, false)]
    [InlineData("in: query, style: form, explode: false, schema: { $ref: '#/components/schemas/Flat' }", false, false)]
    [InlineData("in: query, style: deepObject, explode: true, schema: { $ref: '#/components/schemas/Nested' }", false, false)]
    [InlineData("in: query, style: deepObject, explode: true, schema: { $ref: '#/components/schemas/WithArray' }", false, false)]
    [InlineData("in: query, style: deepObject, explode: true, schema: { type: object, properties: { name: { type: string } } }", false, false)]
    [InlineData("in: header, schema: { $ref: '#/components/schemas/Flat' }", false, false)]
    public void GetQueryObject_IsAFlatModel_SentAsDeepObjectOrFormExplode(
        string declaration,
        bool isQueryObject,
        bool deepObject)
    {
        var yaml = $$"""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /items:
                get:
                  operationId: listItems
                  parameters:
                    - { name: filter, {{declaration}} }
                  responses:
                    '200': { description: OK }
            components:
              schemas:
                Flat:
                  type: object
                  properties:
                    name: { type: string }
                    size: { type: integer }
                    color: { $ref: '#/components/schemas/Color' }
                WithWrappedEnum:
                  type: object
                  properties:
                    color: { nullable: true, allOf: [ { $ref: '#/components/schemas/Color' } ] }
                Nested:
                  type: object
                  properties:
                    inner: { $ref: '#/components/schemas/Flat' }
                WithArray:
                  type: object
                  properties:
                    tags: { type: array, items: { type: string } }
                Color:
                  type: string
                  enum: [red, green]
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var param = document.Paths["/items"].Operations!.Values.First().Parameters![0].Resolve().Parameter!;

        var queryObject = param.GetQueryObject();

        Assert.Equal(isQueryObject, queryObject is not null);
        if (queryObject is not null)
        {
            Assert.Equal(deepObject, queryObject.DeepObject);
            Assert.Equal(deepObject ? "filter[color]" : "color", queryObject.KeyOf(queryObject.Properties.Single(p => p.WireName == "color")));
            Assert.True(param.GetParameterSerialization().IsSupported);
        }
        else if (declaration.StartsWith("in: query", StringComparison.Ordinal))
        {
            Assert.False(param.GetParameterSerialization().IsSupported);
        }
    }

    [Fact]
    public void GetParameterSerialization_RefToArraySchema_ClassifiesAsArraySupported()
    {
        // A query param whose schema is a $ref to a components array schema must resolve the
        // reference before classifying — otherwise Type==null falls through to Primitive and the
        // typed client silently serializes the collection's .ToString() (the silent-wrong gap).
        const string yaml = """
                            openapi: 3.0.3
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  parameters:
                                    - name: ids
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/IdList'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                IdList:
                                  type: array
                                  items:
                                    type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var operation = document.Paths["/items"].Operations.Values.First();
        var param = operation.Parameters[0].Resolve().Parameter;

        Assert.NotNull(param);
        var s = param.GetParameterSerialization();

        Assert.Equal(ParameterValueKind.Array, s.ValueKind);
        Assert.True(s.IsSupported);
    }

    [Fact]
    public void GetParameterSerialization_RefToObjectSchema_ClassifiesAsObjectNotSupported()
    {
        // A query param whose schema is a $ref to a components object schema must resolve to Object
        // (not Primitive) so the unsupported-style warning (ATC_API_OPR026) fires for an object query
        // param delivered via $ref in a style the generated code does not send (form without explode;
        // deepObject and form with explode are supported, see GetQueryObject).
        const string yaml = """
                            openapi: 3.0.3
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  parameters:
                                    - name: filter
                                      in: query
                                      style: form
                                      explode: false
                                      schema:
                                        $ref: '#/components/schemas/Filter'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                Filter:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var operation = document.Paths["/items"].Operations.Values.First();
        var param = operation.Parameters[0].Resolve().Parameter;

        Assert.NotNull(param);
        var s = param.GetParameterSerialization();

        Assert.Equal(ParameterValueKind.Object, s.ValueKind);
        Assert.False(s.IsSupported);
    }

    // ========== GetParameterSerialization — cookie style tests ==========
    [Fact]
    public void GetParameterSerialization_CookieStyleInCookiePrimitive_IsSupported()
    {
        // ATC_API_OPR026 must NOT fire for style:cookie on in:cookie primitive params.
        var param = new OpenApiParameter
        {
            Name = "session",
            In = ParameterLocation.Cookie,
            Style = ParameterStyle.Cookie,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        };

        var s = param.GetParameterSerialization();

        Assert.Equal(ParameterStyle.Cookie, s.Style);
        Assert.Equal(ParameterValueKind.Primitive, s.ValueKind);
        Assert.True(s.IsSupported);
    }

    [Fact]
    public void GetParameterSerialization_CookieStyleInCookieArray_IsSupported()
    {
        // style:cookie on an array produces semicolon-separated RFC 6265 pairs.
        var param = new OpenApiParameter
        {
            Name = "prefs",
            In = ParameterLocation.Cookie,
            Style = ParameterStyle.Cookie,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        var s = param.GetParameterSerialization();

        Assert.Equal(ParameterStyle.Cookie, s.Style);
        Assert.Equal(ParameterValueKind.Array, s.ValueKind);
        Assert.True(s.IsSupported);
    }

    [Fact]
    public void GetParameterSerialization_ParsedFromYaml_ExplicitCookieStyle_IsSupported()
    {
        // Locks the parse path: in:cookie with explicit style:cookie must survive YAML parsing
        // and be classified as supported (no ATC_API_OPR026).
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  parameters:
                                    - name: session
                                      in: cookie
                                      style: cookie
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var operation = document.Paths["/items"].Operations.Values.First();
        var param = operation.Parameters[0].Resolve().Parameter;

        Assert.NotNull(param);
        var s = param.GetParameterSerialization();

        Assert.Equal(ParameterStyle.Cookie, s.Style);
        Assert.True(s.IsSupported);
    }

    // ========== Helper Methods ==========
    private static OpenApiParameter CreateParameterWithType(JsonSchemaType type)
        => new()
        {
            Name = "test",
            Schema = new OpenApiSchema { Type = type },
        };
}