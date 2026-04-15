namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class WebhookExtractorTests
{
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
          systemHeartbeat:
            post:
              operationId: onSystemHeartbeat
              summary: System heartbeat
              responses:
                '200':
                  description: OK
        components:
          schemas:
            Pet:
              type: object
              required:
                - id
                - name
              properties:
                id:
                  type: integer
                  format: int64
                name:
                  type: string
        """;

    // ========== WebhookHandlerExtractor ==========
    [Fact]
    public void WebhookHandlerExtractor_Extract_ProducesHandlerInterfaces()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);
        var resolver = new SystemTypeConflictResolver([]);

        var result = WebhookHandlerExtractor.Extract(document, "TestApi", resolver);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var newPetHandler = result.First(h => h.InterfaceTypeName == "IOnNewPetWebhookHandler");
        Assert.NotNull(newPetHandler);
        Assert.NotNull(newPetHandler.Methods);
        Assert.Single(newPetHandler.Methods);
        Assert.Equal("ExecuteAsync", newPetHandler.Methods[0].Name);
    }

    [Fact]
    public void WebhookHandlerExtractor_Extract_HeartbeatHasNoParameters()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);
        var resolver = new SystemTypeConflictResolver([]);

        var result = WebhookHandlerExtractor.Extract(document, "TestApi", resolver);

        Assert.NotNull(result);
        var heartbeatHandler = result.First(h => h.InterfaceTypeName == "IOnSystemHeartbeatWebhookHandler");
        var method = heartbeatHandler.Methods![0];

        // Heartbeat has no requestBody -> only CancellationToken parameter
        Assert.NotNull(method.Parameters);
        Assert.Single(method.Parameters);
        Assert.Equal("CancellationToken", method.Parameters[0].TypeName);
    }

    // ========== WebhookResultExtractor ==========
    [Fact]
    public void WebhookResultExtractor_Extract_ProducesResultClasses()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);

        var result = WebhookResultExtractor.Extract(document, "TestApi");

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var (className, content) = result.First(r => r.ClassName == "OnNewPetWebhookResult");
        Assert.Contains("public static OnNewPetWebhookResult Ok()", content, StringComparison.Ordinal);
        Assert.Contains("public static OnNewPetWebhookResult BadRequest()", content, StringComparison.Ordinal);
    }

    [Fact]
    public void WebhookResultExtractor_Extract_UsesTypedResultFactories()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);

        var result = WebhookResultExtractor.Extract(document, "TestApi");

        Assert.NotNull(result);
        var (_, content) = result.First(r => r.ClassName == "OnNewPetWebhookResult");

        Assert.Contains("TypedResults.Ok()", content, StringComparison.Ordinal);
        Assert.Contains("TypedResults.BadRequest()", content, StringComparison.Ordinal);
    }

    // ========== WebhookParameterExtractor ==========
    [Fact]
    public void WebhookParameterExtractor_Extract_ProducesParameterClasses()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);
        var resolver = new SystemTypeConflictResolver([]);

        var result = WebhookParameterExtractor.Extract(document, "TestApi", resolver);

        Assert.NotNull(result);
        Assert.Single(result);
        var (className, content) = result[0];
        Assert.Equal("OnNewPetWebhookParameters", className);
        Assert.Contains("Pet Payload", content, StringComparison.Ordinal);
    }

    [Fact]
    public void WebhookParameterExtractor_Extract_NoBodyWebhook_NoParameters()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);
        var resolver = new SystemTypeConflictResolver([]);

        var result = WebhookParameterExtractor.Extract(document, "TestApi", resolver);

        Assert.NotNull(result);
        Assert.DoesNotContain(result, r => r.ClassName.Contains("Heartbeat", StringComparison.Ordinal));
    }

    // ========== WebhookEndpointExtractor ==========
    [Fact]
    public void WebhookEndpointExtractor_Extract_ProducesEndpointClass()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);
        var config = new ServerConfig();

        var result = WebhookEndpointExtractor.Extract(document, "TestApi", config);

        Assert.NotNull(result);

        // Verify the class has the expected structure
        Assert.NotNull(result.Methods);
        Assert.True(result.Methods.Count > 0);
    }

    // ========== WebhookDependencyInjectionExtractor ==========
    [Fact]
    public void WebhookDependencyInjectionExtractor_Extract_ProducesDIClass()
    {
        var document = OpenApiDocumentHelper.ParseYaml(WebhookYaml);

        var result = WebhookDependencyInjectionExtractor.Extract(document, "TestApi");

        Assert.NotNull(result);
        Assert.NotNull(result.Methods);
        Assert.True(result.Methods.Count > 0);
    }

    // ========== No Webhooks ==========
    [Fact]
    public void AllWebhookExtractors_NoWebhooks_ReturnNull()
    {
        var yaml = """
                   openapi: 3.0.0
                   info:
                     title: Test
                     version: 1.0.0
                   paths:
                     /pets:
                       get:
                         operationId: listPets
                         responses:
                           '200':
                             description: OK
                   """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var resolver = new SystemTypeConflictResolver([]);
        var config = new ServerConfig();

        Assert.Null(WebhookHandlerExtractor.Extract(document, "TestApi", resolver));
        Assert.Null(WebhookResultExtractor.Extract(document, "TestApi"));
        Assert.Null(WebhookParameterExtractor.Extract(document, "TestApi", resolver));
        Assert.Null(WebhookEndpointExtractor.Extract(document, "TestApi", config));
        Assert.Null(WebhookDependencyInjectionExtractor.Extract(document, "TestApi"));
    }
}