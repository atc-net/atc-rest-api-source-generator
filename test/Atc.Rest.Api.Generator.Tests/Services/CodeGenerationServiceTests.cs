namespace Atc.Rest.Api.Generator.Tests.Services;

public class CodeGenerationServiceTests
{
    private const string ProjectName = "TestApi";

    private const string InlineResponseYaml = """
        openapi: 3.0.3
        info:
          title: Test API
          version: 1.0.0
        paths:
          /reports/{reportId}:
            get:
              operationId: getReport
              parameters:
                - name: reportId
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: object
                        required:
                          - id
                        properties:
                          id:
                            type: string
                          name:
                            type: string
        """;

    private const string WebhookYaml = """
        openapi: 3.1.1
        info:
          title: Test API
          version: 1.0.0
        paths: {}
        webhooks:
          newPet:
            post:
              operationId: onNewPet
              summary: A new pet was added
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Pet'
              responses:
                '200':
                  description: Acknowledged
                '400':
                  description: Bad request
          petRetired:
            post:
              operationId: onPetRetired
              deprecated: true
              responses:
                '200':
                  description: Acknowledged
        components:
          schemas:
            Pet:
              type: object
              required:
                - id
              properties:
                id:
                  type: integer
                  format: int64
                name:
                  type: string
        """;

    private const string NoWebhookYaml = """
        openapi: 3.0.3
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: OK
        """;

    // ========== SanitizeProjectNameForIdentifier ==========
    [Theory]
    [InlineData("My.Shop.Api", "MyShopApi")]
    [InlineData("Shop", "Shop")]
    [InlineData("", "")]
    public void SanitizeProjectNameForIdentifier_RemovesDots(
        string projectName,
        string expected)
    {
        Assert.Equal(expected, CodeGenerationService.SanitizeProjectNameForIdentifier(projectName));
    }

    // ========== GetGroupNameFromPath ==========
    [Theory]
    [InlineData("/pets", "Pets")]
    [InlineData("/v1/pets/{petId}", "Pets")]
    [InlineData("/V2/orders", "Orders")]
    [InlineData("/{tenantId}/users", "Users")]
    [InlineData("/", "Api")]
    [InlineData("/v1/{id}", "Api")]
    public void GetGroupNameFromPath_SkipsVersionAndParameterSegments(
        string path,
        string expected)
    {
        Assert.Equal(expected, CodeGenerationService.GetGroupNameFromPath(path));
    }

    // ========== GetGroupNameForSchema ==========
    [Fact]
    public void GetGroupNameForSchema_ReferencedByAResponse_UsesTheOperationTag()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info:
              title: Test API
              version: 1.0.0
            paths:
              /v1/orders:
                get:
                  operationId: listOrders
                  tags:
                    - shop-orders
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Order'
            components:
              schemas:
                Order:
                  type: object
                Unused:
                  type: object
            """);

        Assert.Equal("ShopOrders", CodeGenerationService.GetGroupNameForSchema(document, "Order"));
        Assert.Null(CodeGenerationService.GetGroupNameForSchema(document, "Unused"));
    }

    [Fact]
    public void GetGroupNameForSchema_ReferencedByARequestBodyWithoutTags_UsesThePathSegment()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info:
              title: Test API
              version: 1.0.0
            paths:
              /v1/orders:
                post:
                  operationId: createOrder
                  requestBody:
                    content:
                      application/json:
                        schema:
                          $ref: '#/components/schemas/CreateOrderRequest'
                  responses:
                    '201':
                      description: Created
            components:
              schemas:
                CreateOrderRequest:
                  type: object
            """);

        Assert.Equal("Orders", CodeGenerationService.GetGroupNameForSchema(document, "CreateOrderRequest"));
    }

    [Fact]
    public void GetGroupNameForSchema_ReferencedByAParameter_UsesTheOperationGroup()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info:
              title: Test API
              version: 1.0.0
            paths:
              /pets:
                get:
                  operationId: listPets
                  parameters:
                    - name: status
                      in: query
                      schema:
                        $ref: '#/components/schemas/PetStatus'
                  responses:
                    '200':
                      description: OK
            components:
              schemas:
                PetStatus:
                  type: string
                  enum: [available, sold]
            """);

        Assert.Equal("Pets", CodeGenerationService.GetGroupNameForSchema(document, "PetStatus"));
    }

    // ========== GenerateInlineModels (EndpointPerOperationExtractor) ==========
    [Fact]
    public void GenerateInlineModels_EndpointPerOperationInlineSchemas_EmitsTheRecordUnderTheSegmentModels()
    {
        var document = OpenApiDocumentHelper.ParseYaml(InlineResponseYaml);
        var (_, inlineSchemas) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            ProjectName,
            "Reports",
            registry: null);
        Assert.Contains("GetReportResponse", inlineSchemas.Keys, StringComparer.Ordinal);

        var models = CodeGenerationService.GenerateInlineModels(inlineSchemas, ProjectName);

        var model = Assert.Single(models);
        Assert.Equal("GetReportResponse", model.TypeName);
        Assert.Equal("InlineModels", model.Category);
        Assert.Equal("TestApi.Generated.Reports.Models", model.Namespace);
        Assert.Equal("Reports", model.GroupName);
        Assert.Equal(@"Contracts\Reports", model.SubFolder);
        Assert.Contains("public sealed record GetReportResponse(", model.Content, StringComparison.Ordinal);
        Assert.Contains("[property: Required] string Id", model.Content, StringComparison.Ordinal);
        Assert.Contains("string? Name = null", model.Content, StringComparison.Ordinal);
        Assert.Contains(NamespaceConstants.SystemComponentModelDataAnnotations, model.RequiredUsings, StringComparer.Ordinal);
        Assert.Contains(NamespaceConstants.SystemCodeDomCompiler, model.RequiredUsings, StringComparer.Ordinal);
    }

    [Fact]
    public void GenerateInlineModels_EndpointPerOperationInlineSchemasForTheServer_PlacesTheModelUnderContractsModels()
    {
        var document = OpenApiDocumentHelper.ParseYaml(InlineResponseYaml);
        var (_, inlineSchemas) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            ProjectName,
            "Reports",
            registry: null);

        var models = CodeGenerationService.GenerateInlineModels(
            inlineSchemas,
            ProjectName,
            CodeGenerationService.GeneratorType.Server);

        var model = Assert.Single(models);
        Assert.Equal(@"Contracts\Reports/Models", model.SubFolder);
        Assert.Equal("TestApi.Generated.Reports.Models", model.Namespace);
    }

    // ========== GenerateInlineModels (HttpClientExtractor) ==========
    [Fact]
    public void GenerateInlineModels_HttpClientInlineSchemas_EmitsTheRecordUnderTheSegmentModels()
    {
        var document = OpenApiDocumentHelper.ParseYaml(InlineResponseYaml);
        var (_, inlineSchemas) = HttpClientExtractor.ExtractWithInlineSchemas(
            document,
            ProjectName,
            "Reports",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]));
        Assert.Contains("GetReportResponse", inlineSchemas.Keys, StringComparer.Ordinal);

        var models = CodeGenerationService.GenerateInlineModels(inlineSchemas, ProjectName);

        var model = Assert.Single(models);
        Assert.Equal("GetReportResponse", model.TypeName);
        Assert.Equal("TestApi.Generated.Reports.Models", model.Namespace);
        Assert.Equal("Reports", model.GroupName);
        Assert.Equal(@"Contracts\Reports", model.SubFolder);
        Assert.Contains("public sealed record GetReportResponse(", model.Content, StringComparison.Ordinal);
        Assert.Contains("[property: Required] string Id", model.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateInlineModels_HttpClientInlineSchemasUnderSingleGranularity_DropsTheSegment()
    {
        var document = OpenApiDocumentHelper.ParseYaml(InlineResponseYaml);
        var (_, inlineSchemas) = HttpClientExtractor.ExtractWithInlineSchemas(
            document,
            ProjectName,
            "Reports",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]));

        var models = CodeGenerationService.GenerateInlineModels(
            inlineSchemas,
            ProjectName,
            CodeGenerationService.GeneratorType.Client,
            ClientGranularityType.Single);

        var model = Assert.Single(models);
        Assert.Equal("TestApi.Generated.Models", model.Namespace);
        Assert.Null(model.GroupName);
        Assert.Equal("Contracts", model.SubFolder);
    }

    // ========== GenerateInlineModels (ResultClassExtractor) ==========
    [Fact]
    public void GenerateInlineModels_ResultClassInlineSchemas_EmitsTheRecordUnderTheServerSegmentModels()
    {
        var document = OpenApiDocumentHelper.ParseYaml(InlineResponseYaml);
        var (_, inlineSchemas) = ResultClassExtractor.ExtractWithInlineSchemas(
            document,
            ProjectName,
            "Reports",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]));
        Assert.Contains("GetReportResponse", inlineSchemas.Keys, StringComparer.Ordinal);

        var models = CodeGenerationService.GenerateInlineModels(inlineSchemas, ProjectName);

        var model = Assert.Single(models);
        Assert.Equal("GetReportResponse", model.TypeName);
        Assert.Equal("InlineModels", model.Category);
        Assert.Equal("TestApi.Generated.Reports.Models", model.Namespace);
        Assert.Equal("Reports", model.GroupName);
        Assert.Equal(@"Contracts\Reports/Models", model.SubFolder);
        Assert.Contains("public sealed record GetReportResponse(", model.Content, StringComparison.Ordinal);
        Assert.Contains("string? Name = null", model.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateInlineModels_ResultClassInlineSchemasWithACollapsedSegment_UsesTheFlatModelsNamespace()
    {
        var document = OpenApiDocumentHelper.ParseYaml(InlineResponseYaml);
        var (_, inlineSchemas) = ResultClassExtractor.ExtractWithInlineSchemas(
            document,
            ProjectName,
            "Reports",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            namespaceSegment: string.Empty);

        var models = CodeGenerationService.GenerateInlineModels(inlineSchemas, ProjectName);

        var model = Assert.Single(models);
        Assert.Equal("TestApi.Generated.Models", model.Namespace);
        Assert.Equal(@"Contracts\Common/Models", model.SubFolder);
    }

    // ========== GenerateSequentialResults ==========
    [Fact]
    public void GenerateSequentialResults_JsonLinesStreamingOperation_ProducesTheStreamingHelperFile()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.2.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /events:
                get:
                  operationId: streamEvents
                  responses:
                    '200':
                      description: OK
                      content:
                        application/jsonl:
                          itemSchema:
                            $ref: '#/components/schemas/Event'
            components:
              schemas:
                Event:
                  type: object
                  properties:
                    id:
                      type: string
            """);

        var result = CodeGenerationService.GenerateSequentialResults(document, ProjectName);

        Assert.NotNull(result);
        Assert.Equal("SequentialResults", result.TypeName);
        Assert.Equal("Streaming", result.Category);
        Assert.Equal("TestApi.Generated.Streaming", result.Namespace);
        Assert.Equal("Streaming", result.SubFolder);
        Assert.Null(result.GroupName);
        Assert.Empty(result.RequiredUsings);
        Assert.StartsWith("// <auto-generated />", result.Content, StringComparison.Ordinal);
        Assert.Contains("namespace TestApi.Generated.Streaming;", result.Content, StringComparison.Ordinal);
        Assert.Contains("public static class SequentialStreamWriter", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateSequentialResults_NoStreamingOperation_ReturnsNull()
    {
        var document = OpenApiDocumentHelper.ParseYaml(NoWebhookYaml);

        Assert.Null(CodeGenerationService.GenerateSequentialResults(document, ProjectName));
    }

    // ========== FormatAsFile ==========
    [Fact]
    public void FormatAsFile_BareTypeContent_AddsHeaderSortedUsingsAndNamespace()
    {
        var type = new GeneratedType(
            TypeName: "Pet",
            Category: "Models",
            Namespace: "TestApi.Generated.Models",
            Content: "public sealed record Pet(string Name);",
            RequiredUsings:
            [
                NamespaceConstants.SystemComponentModelDataAnnotations,
                NamespaceConstants.SystemTextJsonSerialization,
                NamespaceConstants.SystemCodeDomCompiler,
            ]);

        var file = CodeGenerationService.FormatAsFile(type);

        var expected = string.Join(
            Environment.NewLine,
            "// <auto-generated />",
            "#nullable enable",
            string.Empty,
            "using System.CodeDom.Compiler;",
            "using System.ComponentModel.DataAnnotations;",
            "using System.Text.Json.Serialization;",
            string.Empty,
            "namespace TestApi.Generated.Models;",
            string.Empty,
            "public sealed record Pet(string Name);");
        Assert.Equal(expected, file);
    }

    [Fact]
    public void FormatAsFile_ContentThatIsAlreadyAFullFile_IsReturnedUnchanged()
    {
        const string content = "// <auto-generated />\n#nullable enable\n\nnamespace Other;\n\npublic sealed record Pet();";
        var type = new GeneratedType(
            TypeName: "Pet",
            Category: "Models",
            Namespace: "TestApi.Generated.Models",
            Content: content,
            RequiredUsings: [NamespaceConstants.System]);

        var file = CodeGenerationService.FormatAsFile(type);

        Assert.Same(content, file);
    }

    [Fact]
    public void FormatAsFile_NullType_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CodeGenerationService.FormatAsFile(null!));
    }

    // ========== HasQueryArrayParameters ==========
    [Fact]
    public void HasQueryArrayParameters_ArrayQueryParameterOnTheOperation_ReturnsTrue()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info:
              title: Test API
              version: 1.0.0
            paths:
              /pets:
                get:
                  operationId: listPets
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
            """);

        Assert.True(CodeGenerationService.HasQueryArrayParameters(document));
    }

    [Fact]
    public void HasQueryArrayParameters_ArrayQueryParameterOnThePathItem_ReturnsTrue()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info:
              title: Test API
              version: 1.0.0
            paths:
              /pets:
                parameters:
                  - name: ids
                    in: query
                    schema:
                      type: array
                      items:
                        type: integer
                get:
                  operationId: listPets
                  responses:
                    '200':
                      description: OK
            """);

        Assert.True(CodeGenerationService.HasQueryArrayParameters(document));
    }

    [Fact]
    public void HasQueryArrayParameters_ArrayHeader_ReturnsTrue()
    {
        // A header (or path) array is its items joined by commas, bound through ParsableList like a query array
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info:
              title: Test API
              version: 1.0.0
            paths:
              /pets:
                get:
                  operationId: listPets
                  parameters:
                    - name: X-Tags
                      in: header
                      schema:
                        type: array
                        items:
                          type: string
                    - name: limit
                      in: query
                      schema:
                        type: integer
                  responses:
                    '200':
                      description: OK
            """);

        Assert.True(CodeGenerationService.HasQueryArrayParameters(document));
    }

    [Fact]
    public void HasQueryArrayParameters_FormBodyWithAListField_ReturnsTrue()
    {
        // A form bound field by field with a list field also goes through the ParsableList types.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info:
              title: Test API
              version: 1.0.0
            paths:
              /uploads:
                post:
                  operationId: upload
                  requestBody:
                    required: true
                    content:
                      multipart/form-data:
                        schema:
                          type: object
                          properties:
                            file:
                              type: string
                              format: binary
                            labels:
                              type: array
                              items:
                                type: string
                  responses:
                    '204':
                      description: Done
            """);

        Assert.True(CodeGenerationService.HasQueryArrayParameters(document));
    }

    // ========== GenerateWebhookHandlerInterfaces ==========
    [Fact]
    public void GenerateWebhookHandlerInterfaces_WebhookWithBody_ProducesTheHandlerInterfaceWithParametersAndResult()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);

        var interfaces = CodeGenerationService.GenerateWebhookHandlerInterfaces(document, ProjectName);

        var handler = Assert.Single(interfaces);
        Assert.Equal("IOnNewPetWebhookHandler", handler.TypeName);
        Assert.Equal("WebhookHandlers", handler.Category);
        Assert.Equal("TestApi.Generated.Webhooks.Handlers", handler.Namespace);
        Assert.Equal("Webhooks", handler.GroupName);
        Assert.Equal(@"Webhooks\Handlers", handler.SubFolder);
        Assert.Contains("public interface IOnNewPetWebhookHandler", handler.Content, StringComparison.Ordinal);
        Assert.Contains("Task<OnNewPetWebhookResult> ExecuteAsync(", handler.Content, StringComparison.Ordinal);
        Assert.Contains("OnNewPetWebhookParameters parameters", handler.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace TEMP", handler.Content, StringComparison.Ordinal);
        Assert.Contains("TestApi.Generated.Webhooks.Parameters", handler.RequiredUsings, StringComparer.Ordinal);
        Assert.Contains("TestApi.Generated.Webhooks.Results", handler.RequiredUsings, StringComparer.Ordinal);
        Assert.Contains(NamespaceConstants.SystemThreadingTasks, handler.RequiredUsings, StringComparer.Ordinal);
    }

    [Fact]
    public void GenerateWebhookHandlerInterfaces_IncludeDeprecated_AlsoProducesTheDeprecatedWebhookHandler()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);

        var interfaces = CodeGenerationService.GenerateWebhookHandlerInterfaces(document, ProjectName, includeDeprecated: true);

        Assert.Equal(2, interfaces.Count);
        Assert.Contains(interfaces, i => i.TypeName == "IOnPetRetiredWebhookHandler");
    }

    [Fact]
    public void GenerateWebhookHandlerInterfaces_NoWebhooks_ReturnsEmpty()
    {
        var document = OpenApiDocumentHelper.ParseYaml(NoWebhookYaml);

        Assert.Empty(CodeGenerationService.GenerateWebhookHandlerInterfaces(document, ProjectName));
    }

    // ========== GenerateWebhookParameters ==========
    [Fact]
    public void GenerateWebhookParameters_WebhookWithBody_ProducesASelfContainedParametersFile()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);

        var parameters = CodeGenerationService.GenerateWebhookParameters(document, ProjectName);

        var parameter = Assert.Single(parameters);
        Assert.Equal("OnNewPetWebhookParameters", parameter.TypeName);
        Assert.Equal("WebhookParameters", parameter.Category);
        Assert.Equal("TestApi.Generated.Webhooks.Parameters", parameter.Namespace);
        Assert.Equal("Webhooks", parameter.GroupName);
        Assert.Equal(@"Webhooks\Parameters", parameter.SubFolder);
        Assert.Empty(parameter.RequiredUsings);
        Assert.Contains("OnNewPetWebhookParameters", parameter.Content, StringComparison.Ordinal);
        Assert.Contains("Pet Payload", parameter.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateWebhookParameters_NoWebhooks_ReturnsEmpty()
    {
        var document = OpenApiDocumentHelper.ParseYaml(NoWebhookYaml);

        Assert.Empty(CodeGenerationService.GenerateWebhookParameters(document, ProjectName));
    }

    // ========== GenerateWebhookResults ==========
    [Fact]
    public void GenerateWebhookResults_WebhookWithTwoResponses_ProducesAFactoryPerStatus()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);

        var results = CodeGenerationService.GenerateWebhookResults(document, ProjectName);

        var result = Assert.Single(results);
        Assert.Equal("OnNewPetWebhookResult", result.TypeName);
        Assert.Equal("WebhookResults", result.Category);
        Assert.Equal("TestApi.Generated.Webhooks.Results", result.Namespace);
        Assert.Equal("Webhooks", result.GroupName);
        Assert.Equal(@"Webhooks\Results", result.SubFolder);
        Assert.Empty(result.RequiredUsings);
        Assert.Contains("public static OnNewPetWebhookResult Ok()", result.Content, StringComparison.Ordinal);
        Assert.Contains("public static OnNewPetWebhookResult BadRequest()", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateWebhookResults_IncludeDeprecated_AlsoProducesTheDeprecatedWebhookResult()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);

        var results = CodeGenerationService.GenerateWebhookResults(document, ProjectName, includeDeprecated: true);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.TypeName == "OnPetRetiredWebhookResult");
    }

    // ========== GenerateWebhookEndpoints ==========
    [Fact]
    public void GenerateWebhookEndpoints_Webhooks_ProducesTheMapWebhooksExtensionUnderTheConfiguredBasePath()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);
        var config = new ServerConfig { WebhookBasePath = "/hooks" };

        var endpoints = CodeGenerationService.GenerateWebhookEndpoints(document, ProjectName, config);

        Assert.NotNull(endpoints);
        Assert.Equal("WebhookEndpointExtensions", endpoints.TypeName);
        Assert.Equal("WebhookEndpoints", endpoints.Category);
        Assert.Equal("TestApi.Generated.Webhooks.Endpoints", endpoints.Namespace);
        Assert.Equal("Webhooks", endpoints.GroupName);
        Assert.Equal(@"Webhooks\Endpoints", endpoints.SubFolder);
        Assert.Contains("public static class WebhookEndpointExtensions", endpoints.Content, StringComparison.Ordinal);
        Assert.Contains("MapTestWebhooks(", endpoints.Content, StringComparison.Ordinal);
        Assert.Contains("endpoints.MapGroup(\"/hooks\")", endpoints.Content, StringComparison.Ordinal);
        Assert.Contains("MapPost(", endpoints.Content, StringComparison.Ordinal);
        Assert.Contains("IOnNewPetWebhookHandler", endpoints.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("IOnPetRetiredWebhookHandler", endpoints.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace TEMP", endpoints.Content, StringComparison.Ordinal);
        Assert.Contains(NamespaceConstants.MicrosoftAspNetCoreRouting, endpoints.RequiredUsings, StringComparer.Ordinal);
        Assert.Contains("TestApi.Generated.Webhooks.Handlers", endpoints.RequiredUsings, StringComparer.Ordinal);
        Assert.Contains("TestApi.Generated.Webhooks.Parameters", endpoints.RequiredUsings, StringComparer.Ordinal);
        Assert.Contains("TestApi.Generated.Webhooks.Results", endpoints.RequiredUsings, StringComparer.Ordinal);
    }

    [Fact]
    public void GenerateWebhookEndpoints_NoWebhooks_ReturnsNull()
    {
        var document = OpenApiDocumentHelper.ParseYaml(NoWebhookYaml);

        Assert.Null(CodeGenerationService.GenerateWebhookEndpoints(document, ProjectName, new ServerConfig()));
    }
}