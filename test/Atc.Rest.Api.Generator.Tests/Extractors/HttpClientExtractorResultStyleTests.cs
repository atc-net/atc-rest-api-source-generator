namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class HttpClientExtractorResultStyleTests
{
    private const string JsonContentYaml = """
                                           openapi: 3.0.0
                                           info:
                                             title: Test API
                                             version: 1.0.0
                                           paths:
                                             /devices/{id}:
                                               get:
                                                 operationId: getDeviceById
                                                 parameters:
                                                   - name: id
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
                                                           $ref: '#/components/schemas/Device'
                                           components:
                                             schemas:
                                               Device:
                                                 type: object
                                                 properties:
                                                   id:
                                                     type: string
                                           """;

    private const string NoContentYaml = """
                                         openapi: 3.0.0
                                         info:
                                           title: Test API
                                           version: 1.0.0
                                         paths:
                                           /devices/{id}:
                                             delete:
                                               operationId: deleteDevice
                                               parameters:
                                                 - name: id
                                                   in: path
                                                   required: true
                                                   schema:
                                                     type: string
                                               responses:
                                                 '204':
                                                   description: No Content
                                         """;

    [Fact]
    public void Extract_ThrowStyle_JsonOperation_ReturnsBareTaskOfT()
    {
        var clientClass = ExtractClient(JsonContentYaml, TypedClientResultStyleType.Throw);

        var method = clientClass.Methods!.First(m => m.Name == "GetDeviceByIdAsync");

        Assert.Equal("Task", method.ReturnGenericTypeName);
        Assert.Equal("Device", method.ReturnTypeName);
    }

    [Fact]
    public void Extract_ResultStyle_JsonOperation_ReturnsEndpointResponseEnvelope()
    {
        var clientClass = ExtractClient(JsonContentYaml, TypedClientResultStyleType.Result);

        var method = clientClass.Methods!.First(m => m.Name == "GetDeviceByIdAsync");

        Assert.Equal("Task", method.ReturnGenericTypeName);
        Assert.Equal("EndpointResponse<Device>", method.ReturnTypeName);
    }

    [Fact]
    public void Extract_ThrowStyle_NoContentOperation_ReturnsBareTask()
    {
        var clientClass = ExtractClient(NoContentYaml, TypedClientResultStyleType.Throw);

        var method = clientClass.Methods!.First(m => m.Name == "DeleteDeviceAsync");

        Assert.Null(method.ReturnGenericTypeName);
        Assert.Equal("Task", method.ReturnTypeName);
    }

    [Fact]
    public void Extract_ResultStyle_NoContentOperation_ReturnsNonGenericEnvelope()
    {
        var clientClass = ExtractClient(NoContentYaml, TypedClientResultStyleType.Result);

        var method = clientClass.Methods!.First(m => m.Name == "DeleteDeviceAsync");

        Assert.Equal("Task", method.ReturnGenericTypeName);
        Assert.Equal("EndpointResponse", method.ReturnTypeName);
    }

    [Fact]
    public void Extract_ThrowStyle_IsTheDefault()
    {
        var document = ParseYaml(JsonContentYaml);
        Assert.NotNull(document);

        var withExplicitThrow = HttpClientExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false,
            useServersBasePath: true,
            resultStyle: TypedClientResultStyleType.Throw);

        var withDefault = HttpClientExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        Assert.NotNull(withExplicitThrow);
        Assert.NotNull(withDefault);

        var explicitMethod = withExplicitThrow.Methods!.First(m => m.Name == "GetDeviceByIdAsync");
        var defaultMethod = withDefault.Methods!.First(m => m.Name == "GetDeviceByIdAsync");

        Assert.Equal(explicitMethod.ReturnTypeName, defaultMethod.ReturnTypeName);
        Assert.Equal(explicitMethod.Content, defaultMethod.Content);
    }

    [Fact]
    public void Extract_ThrowStyle_DoesNotEmitEnvelopeHelpers()
    {
        var clientClass = ExtractClient(JsonContentYaml, TypedClientResultStyleType.Throw);

        Assert.Contains(clientClass.Methods!, m => m.Name == "EnsureSuccessAsync");
        Assert.DoesNotContain(clientClass.Methods!, m => m.Name.StartsWith("Build", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_ResultStyle_EmitsEnvelopeHelpers()
    {
        var clientClass = ExtractClient(JsonContentYaml, TypedClientResultStyleType.Result);

        Assert.Contains(clientClass.Methods!, m => m.Name == "ReadHeaders");
        Assert.Contains(clientClass.Methods!, m => m.Name == "BuildJsonResponseAsync<T>");
        Assert.Contains(clientClass.Methods!, m => m.Name == "BuildEmptyResponseAsync");
    }

    [Fact]
    public void Extract_ResultStyle_BodyDoesNotThrowOnStatus()
    {
        var clientClass = ExtractClient(JsonContentYaml, TypedClientResultStyleType.Result);

        var method = clientClass.Methods!.First(m => m.Name == "GetDeviceByIdAsync");

        Assert.DoesNotContain("EnsureSuccessAsync", method.Content, StringComparison.Ordinal);
        Assert.Contains("BuildJsonResponseAsync<Device>", method.Content, StringComparison.Ordinal);

        // The GetFromJsonAsync shortcut never materializes an HttpResponseMessage, so Result style
        // must fall back to GetAsync - otherwise there is nothing to build the envelope from.
        Assert.DoesNotContain("GetFromJsonAsync", method.Content, StringComparison.Ordinal);
        Assert.Contains("httpClient.GetAsync(url, cancellationToken)", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ThrowStyle_BodyThrowsOnStatus()
    {
        var clientClass = ExtractClient(JsonContentYaml, TypedClientResultStyleType.Throw);

        var method = clientClass.Methods!.First(m => m.Name == "GetDeviceByIdAsync");

        // A plain GET with a return type uses the GetFromJsonAsync shortcut, which throws on a
        // non-success status internally - so it needs no explicit EnsureSuccessAsync call.
        Assert.Contains("GetFromJsonAsync<Device>", method.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildJsonResponseAsync", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ThrowStyle_NoContentBodyCallsEnsureSuccess()
    {
        var clientClass = ExtractClient(NoContentYaml, TypedClientResultStyleType.Throw);

        var method = clientClass.Methods!.First(m => m.Name == "DeleteDeviceAsync");

        Assert.Contains("EnsureSuccessAsync", method.Content, StringComparison.Ordinal);
    }

    private static ClassParameters ExtractClient(
        string yaml,
        TypedClientResultStyleType resultStyle)
    {
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clientClass = HttpClientExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false,
            useServersBasePath: true,
            resultStyle: resultStyle);

        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        return clientClass;
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}