namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class ResilienceRequestHelperTests
{
    private const string RetryYaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        x-retry-policy: standard
        paths:
          /items:
            get:
              operationId: listItems
              responses:
                '200': { description: OK }
            post:
              operationId: createItem
              responses:
                '201': { description: Created }
          /quiet:
            get:
              operationId: getQuiet
              x-retry-enabled: false
              x-retry-timeout-seconds: 600
              responses:
                '200': { description: OK }
          /optin:
            post:
              operationId: optIn
              x-retry-enabled: true
              responses:
                '201': { description: Created }
          /own:
            post:
              operationId: createOwn
              x-retry-policy: own-policy
              responses:
                '201': { description: Created }
          /wide-off:
            x-retry-policy: wide-off
            get:
              operationId: getWideOff
              x-retry-enabled: false
              responses:
                '200': { description: OK }
          /records:
            put:
              operationId: replaceRecords
              requestBody:
                content:
                  application/jsonl: { schema: { type: object } }
              responses:
                '204': { description: Replaced }
            post:
              operationId: importRecords
              x-retry-enabled: true
              requestBody:
                content:
                  application/jsonl: { schema: { type: object } }
              responses:
                '202': { description: Accepted }
          /records/own:
            post:
              operationId: importOwnRecords
              x-retry-policy: own-policy
              requestBody:
                content:
                  application/json-seq: { schema: { type: object } }
              responses:
                '202': { description: Accepted }
        """;

    [Fact]
    public void BuildStampStatement_StreamedRequestBodyOnASafeToRetryMethod_DisablesRetry()
    {
        // The records are read once, as they are written: the PUT the handler would retry is sent once
        var statement = Build("/records", HttpMethod.Put, "PUT");

        Assert.Equal("ApiResilienceRequest.DisableRetry(request);", statement);
    }

    [Fact]
    public void BuildStampStatement_StreamedRequestBodyOptedIn_DisablesRetry()
    {
        var statement = Build("/records", HttpMethod.Post, "POST");

        Assert.Equal("ApiResilienceRequest.DisableRetry(request);", statement);
    }

    [Fact]
    public void BuildStampStatement_StreamedRequestBodyWithItsOwnPolicy_KeepsThePolicyWithoutRetry()
    {
        // The policy's timeout still applies; its retries do not
        var statement = Build("/records/own", HttpMethod.Post, "POST");

        Assert.Equal("ApiResilienceRequest.DisableRetry(request, ResiliencePolicies.OwnPolicy);", statement);
    }

    [Fact]
    public void BuildStampStatement_SafeMethodWithTheDefaultPolicy_ReturnsNull()
    {
        var statement = Build("/items", HttpMethod.Get, "GET");

        Assert.Null(statement);
    }

    [Fact]
    public void BuildStampStatement_PostWithoutOptIn_ReturnsNull()
    {
        // The handler does not retry a POST on its own, so nothing has to be stamped
        var statement = Build("/items", HttpMethod.Post, "POST");

        Assert.Null(statement);
    }

    [Fact]
    public void BuildStampStatement_DisabledWithTimeout_DisablesRetryWithTheTimeout()
    {
        var statement = Build("/quiet", HttpMethod.Get, "GET");

        Assert.Equal("ApiResilienceRequest.DisableRetry(request, timeoutSeconds: 600);", statement);
    }

    [Fact]
    public void BuildStampStatement_PostOptedIn_UsesTheDefaultPolicy()
    {
        var statement = Build("/optin", HttpMethod.Post, "POST");

        Assert.Equal("ApiResilienceRequest.UsePolicy(request);", statement);
    }

    [Fact]
    public void BuildStampStatement_OwnPolicy_UsesThePolicyConstant()
    {
        var statement = Build("/own", HttpMethod.Post, "POST");

        Assert.Equal("ApiResilienceRequest.UsePolicy(request, ResiliencePolicies.OwnPolicy);", statement);
    }

    [Fact]
    public void BuildStampStatement_DisabledWithPathPolicy_DisablesRetryKeepingThePolicy()
    {
        // No retry, but the policy's timeout and circuit breaker still apply
        var statement = Build("/wide-off", HttpMethod.Get, "GET");

        Assert.Equal("ApiResilienceRequest.DisableRetry(request, ResiliencePolicies.WideOff);", statement);
    }

    [Fact]
    public void BuildStampStatement_UsesTheGivenRequestVariable()
    {
        var document = OpenApiDocumentHelper.ParseYaml(RetryYaml);
        var pathItem = document.Paths["/own"];
        var operation = pathItem.Operations![HttpMethod.Post];

        var statement = ResilienceRequestHelper.BuildStampStatement(operation, pathItem, document, "POST", "httpRequest");

        Assert.Equal("ApiResilienceRequest.UsePolicy(httpRequest, ResiliencePolicies.OwnPolicy);", statement);
    }

    [Fact]
    public void BuildStampStatement_DocumentWithoutRetryConfiguration_ReturnsNull()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths:
              /quiet:
                get:
                  operationId: getQuiet
                  x-retry-enabled: false
                  x-retry-timeout-seconds: 600
                  responses:
                    '200': { description: OK }
            """);
        var pathItem = document.Paths["/quiet"];
        var operation = pathItem.Operations![HttpMethod.Get];

        var statement = ResilienceRequestHelper.BuildStampStatement(operation, pathItem, document, "GET", "request");

        Assert.Null(statement);
    }

    [Fact]
    public void BuildStampStatement_NullPathItem_ReturnsNull()
    {
        var document = OpenApiDocumentHelper.ParseYaml(RetryYaml);
        var operation = document.Paths["/own"].Operations![HttpMethod.Post];

        Assert.Null(ResilienceRequestHelper.BuildStampStatement(operation, null, document, "POST", "request"));
    }

    [Fact]
    public void BuildBufferStatement_DocumentWithRetryConfiguration_BuffersTheResponse()
    {
        // Every response a client reads whole is read inside the handler's attempt, with its timeout and retry
        var document = OpenApiDocumentHelper.ParseYaml(RetryYaml);

        Assert.Equal("ApiResilienceRequest.BufferResponse(httpRequest);", ResilienceRequestHelper.BuildBufferStatement(document, "httpRequest"));
    }

    [Fact]
    public void BuildBufferStatement_DocumentWithoutRetryConfiguration_ReturnsNull()
    {
        // No resilience handler is generated, so there is no ApiResilienceRequest to tell
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths:
              /items:
                get:
                  operationId: listItems
                  responses:
                    '200': { description: OK }
            """);

        Assert.Null(ResilienceRequestHelper.BuildBufferStatement(document, "request"));
    }

    private static string? Build(
        string path,
        HttpMethod method,
        string httpMethod)
    {
        var document = OpenApiDocumentHelper.ParseYaml(RetryYaml);
        var pathItem = document.Paths[path];
        var operation = pathItem.Operations![method];

        return ResilienceRequestHelper.BuildStampStatement(operation, pathItem, document, httpMethod, "request");
    }
}