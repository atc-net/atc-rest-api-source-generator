namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// Every place the OpenAPI reader can leave a <see langword="null"/> where an object belongs is an error naming that
/// place; a schema that may be absent (a parameter's, a media type's, the items, additionalProperties) is no error.
/// </summary>
public class OpenApiStructureValidatorTests
{
    private const string SourceFilePath = "Contoso.yaml";

    [Fact]
    public void Validate_NullDocument_IsNoError()
        => Assert.Empty(OpenApiStructureValidator.Validate(null!, SourceFilePath));

    [Fact]
    public void Validate_EmptyDocument_IsNoError()
        => Assert.Empty(OpenApiStructureValidator.Validate(new OpenApiDocument(), SourceFilePath));

    [Fact]
    public void Validate_DocumentWithoutPathsOrComponents_IsNoError()
        => Assert.Empty(OpenApiStructureValidator.Validate(new OpenApiDocument { Paths = null, Components = null }, SourceFilePath));

    [Fact]
    public void Validate_WholeDocument_IsNoError()
    {
        var pet = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["tags"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
            },
        };
        var document = Document(new OpenApiOperation
        {
            Parameters = [new OpenApiParameter { Name = "id", In = ParameterLocation.Path, Schema = new OpenApiSchema { Type = JsonSchemaType.Integer } }],
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Content = Json(pet) },
            },
        });
        document.Components = new OpenApiComponents
        {
            Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["Pet"] = pet },
        };

        Assert.Empty(OpenApiStructureValidator.Validate(document, SourceFilePath));
    }

    [Fact]
    public void Validate_PathItemThatIsNull_IsAnError_AndItsOperationsAreNotLookedAt()
    {
        var document = new OpenApiDocument { Paths = new OpenApiPaths { ["/pets"] = null } };

        var error = Assert.Single(OpenApiStructureValidator.Validate(document, SourceFilePath));

        Assert.Equal(RuleIdentifiers.OpenApiCoreError, error.RuleId);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Equal(SourceFilePath, error.FilePath);
        Assert.Equal("'#/paths//pets' must be an object, but the specification has a value of another kind there", error.Message);
    }

    [Fact]
    public void Validate_OperationThatIsNull_IsAnErrorNamingTheMethodInLowerCase()
    {
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/pets"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = null,
                        [HttpMethod.Delete] = new OpenApiOperation(),
                    },
                },
            },
        };

        Assert.Equal(["#/paths//pets/get"], Pointers(document));
    }

    [Fact]
    public void Validate_ParameterThatIsNull_IsAnErrorNamingItsIndex_OnThePathAndTheOperation()
    {
        var document = Document(new OpenApiOperation
        {
            Parameters = [new OpenApiParameter { Name = "limit", In = ParameterLocation.Query }, null],
        });
        ((OpenApiPathItem)document.Paths["/pets"]).Parameters = [null];

        Assert.Equal(["#/paths//pets/parameters/0", "#/paths//pets/get/parameters/1"], Pointers(document));
    }

    [Fact]
    public void Validate_ParameterWithoutASchema_IsNoError()
    {
        var document = Document(new OpenApiOperation
        {
            Parameters = [new OpenApiParameter { Name = "limit", In = ParameterLocation.Query, Schema = null }],
        });

        Assert.Empty(Pointers(document));
    }

    [Fact]
    public void Validate_ParameterSchemaWithAPropertyThatIsNull_IsAnError()
    {
        var document = Document(new OpenApiOperation
        {
            Parameters =
            [
                new OpenApiParameter
                {
                    Name = "filter",
                    In = ParameterLocation.Query,
                    Schema = new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["name"] = null } },
                },
            ],
        });

        Assert.Equal(["#/paths//pets/get/parameters/0/schema/properties/name"], Pointers(document));
    }

    [Fact]
    public void Validate_RequestBodyMediaTypeThatIsNull_IsAnError()
    {
        var document = Document(new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                {
                    ["application/json"] = null,
                    ["text/plain"] = new OpenApiMediaType(),
                },
            },
        });

        Assert.Equal(["#/paths//pets/get/requestBody/content/application/json"], Pointers(document));
    }

    [Fact]
    public void Validate_ResponseThatIsNull_IsAnErrorNamingTheStatus()
    {
        var document = Document(new OpenApiOperation
        {
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse(), ["4XX"] = null },
        });

        Assert.Equal(["#/paths//pets/get/responses/4XX"], Pointers(document));
    }

    [Fact]
    public void Validate_ResponseMediaTypeOrHeaderThatIsNull_IsAnError()
    {
        var document = Document(new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal) { ["application/xml"] = null },
                    Headers = new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal) { ["X-Rate-Limit"] = null },
                },
            },
        });

        Assert.Equal(
            ["#/paths//pets/get/responses/200/content/application/xml", "#/paths//pets/get/responses/200/headers/X-Rate-Limit"],
            Pointers(document));
    }

    [Fact]
    public void Validate_MediaTypeWithoutASchema_IsNoError()
    {
        var document = Document(new OpenApiOperation
        {
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Content = Json(null) } },
        });

        Assert.Empty(Pointers(document));
    }

    [Fact]
    public void Validate_SchemaPropertyOrCompositionEntryThatIsNull_IsAnError()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["owner"] = null },
            AllOf = [new OpenApiSchema(), null],
            AnyOf = [null],
            OneOf = [new OpenApiSchema(), new OpenApiSchema(), null],
        };

        Assert.Equal(
            [
                "#/components/schemas/Pet/properties/owner",
                "#/components/schemas/Pet/allOf/1",
                "#/components/schemas/Pet/anyOf/0",
                "#/components/schemas/Pet/oneOf/2",
            ],
            Pointers(Components(c => c.Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["Pet"] = schema })));
    }

    [Fact]
    public void Validate_SchemaWithoutItemsOrAdditionalProperties_IsNoError()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = null, AdditionalProperties = null };

        Assert.Empty(Pointers(Components(c => c.Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["Pets"] = schema })));
    }

    [Fact]
    public void Validate_NestedSchemas_AreFollowed_ThroughItemsAndAdditionalProperties()
    {
        var schema = new OpenApiSchema
        {
            Items = new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["a"] = null } },
            AdditionalProperties = new OpenApiSchema { AllOf = [null] },
        };

        Assert.Equal(
            ["#/components/schemas/Pets/items/properties/a", "#/components/schemas/Pets/additionalProperties/allOf/0"],
            Pointers(Components(c => c.Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["Pets"] = schema })));
    }

    [Fact]
    public void Validate_ComponentSchemaThatIsNull_IsAnError()
        => Assert.Equal(
            ["#/components/schemas/Page"],
            Pointers(Components(c => c.Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["Page"] = null })));

    [Fact]
    public void Validate_SelfReferencingInlineSchema_IsVisitedOnce()
    {
        // A cycle made in code (a reference is checked where its target is declared, so only an inline cycle loops)
        var node = new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) };
        node.Properties["next"] = node;
        node.Properties["broken"] = null;
        node.Items = node;
        node.AllOf = [node];

        Assert.Equal(
            ["#/components/schemas/Node/properties/broken"],
            Pointers(Components(c => c.Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["Node"] = node })));
    }

    [Fact]
    public void Validate_SchemaSharedByTwoPlaces_IsReportedWhereItIsFirstMet()
    {
        var shared = new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["x"] = null } };
        var document = Document(new OpenApiOperation
        {
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Content = Json(shared) } },
        });
        document.Components = new OpenApiComponents
        {
            Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["Shared"] = shared },
        };

        Assert.Equal(["#/paths//pets/get/responses/200/content/application/json/schema/properties/x"], Pointers(document));
    }

    [Fact]
    public void Validate_ComponentEntriesThatAreNull_AreErrors()
    {
        var document = Components(c =>
        {
            c.Responses = new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal) { ["NotFound"] = null };
            c.Parameters = new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal) { ["Limit"] = null };
            c.RequestBodies = new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal) { ["NewPet"] = null };
            c.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal) { ["ApiKey"] = null };
        });

        Assert.Equal(
            [
                "#/components/responses/NotFound",
                "#/components/parameters/Limit",
                "#/components/requestBodies/NewPet",
                "#/components/securitySchemes/ApiKey",
            ],
            Pointers(document));
    }

    [Fact]
    public void Validate_ComponentEntriesWithMissingInnerObjects_AreErrorsNamingTheInnerPlace()
    {
        var document = Components(c =>
        {
            c.Responses = new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal)
            {
                ["NotFound"] = new OpenApiResponse { Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal) { ["application/problem+json"] = null } },
            };
            c.Parameters = new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal)
            {
                ["Limit"] = new OpenApiParameter { Name = "limit", Schema = null },
                ["Filter"] = new OpenApiParameter { Name = "filter", Schema = new OpenApiSchema { AnyOf = [null] } },
            };
            c.RequestBodies = new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal)
            {
                ["NewPet"] = new OpenApiRequestBody { Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal) { ["application/json"] = null } },
            };
        });

        Assert.Equal(
            [
                "#/components/responses/NotFound/content/application/problem+json",
                "#/components/parameters/Filter/schema/anyOf/0",
                "#/components/requestBodies/NewPet/content/application/json",
            ],
            Pointers(document));
    }

    [Fact]
    public void Validate_SchemaReference_IsNotFollowed()
    {
        // The target is checked where it is declared: a reference to a component that is null is one error, not two
        var document = Document(new OpenApiOperation
        {
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Content = Json(new OpenApiSchemaReference("Page")) } },
        });
        document.Components = new OpenApiComponents
        {
            Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["Page"] = null },
        };

        Assert.Equal(["#/components/schemas/Page"], Pointers(document));
    }

    [Fact]
    public void Validate_EveryMissingObject_IsAnErrorOfItsOwn()
    {
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths { ["/a"] = null, ["/b"] = null, ["/c"] = new OpenApiPathItem() },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["A"] = null, ["B"] = null },
            },
        };

        var diagnostics = OpenApiStructureValidator.Validate(document, SourceFilePath);

        Assert.Equal(4, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }

    [Theory]
    [InlineData("responses:\n        '200':\n          description: OK\n          content:\n            application/json: text", "#/paths//pets/get/responses/200/content/application/json")]
    [InlineData("responses:\n        '200':\n          description: OK\n          headers:\n            X-Rate-Limit: text", "#/paths//pets/get/responses/200/headers/X-Rate-Limit")]
    [InlineData("requestBody:\n        content:\n          application/json: text\n      responses:\n        '204':\n          description: OK", "#/paths//pets/get/requestBody/content/application/json")]
    public void Validate_ValueWrittenAsAString_InYaml_IsAnErrorNamingThePlace(
        string operationBody,
        string expectedPointer)
    {
        // What the reader leaves behind: a null where the specification holds a string
        var yaml = $$"""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  {{operationBody}}
            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, SourceFilePath);
        Assert.NotNull(document);

        Assert.Equal([expectedPointer], Pointers(document));
    }

    [Theory]
    [InlineData("/pets: text")]
    [InlineData("/pets:\n    get:\n      responses:\n        '200': text")]
    public void Validate_PathItemOrResponseWrittenAsAString_InYaml_IsLeftToTheReader(
        string paths)
    {
        // The reader drops such an object and reports it itself, so it is no null for this validator
        var yaml = $$"""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              {{paths}}
            """;
        var (document, diagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, SourceFilePath);
        Assert.NotNull(document);

        Assert.Empty(Pointers(document));
        Assert.NotEmpty(diagnostic!.Errors);
    }

    private static OpenApiDocument Document(OpenApiOperation operation)
        => new()
        {
            Paths = new OpenApiPaths
            {
                ["/pets"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Get] = operation },
                },
            },
        };

    private static OpenApiDocument Components(Action<OpenApiComponents> fill)
    {
        var components = new OpenApiComponents();
        fill(components);
        return new OpenApiDocument { Components = components };
    }

    private static Dictionary<string, IOpenApiMediaType> Json(
        IOpenApiSchema? schema)
        => new(StringComparer.Ordinal) { ["application/json"] = new OpenApiMediaType { Schema = schema } };

    private static List<string> Pointers(OpenApiDocument document)
        => OpenApiStructureValidator
            .Validate(document, SourceFilePath)
            .Select(d => d.Message.Split('\'')[1])
            .ToList();
}