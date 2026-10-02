namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

/// <summary>
/// A query or header parameter the specification marks <c>required</c> is required in the generated TypeScript:
/// the member has no <c>?</c>, and the <c>query</c> / <c>headers</c> argument that holds it cannot be left out -
/// in the client methods and in the React Query and SWR hooks. A call without it does not compile, instead of
/// compiling and being answered with a 400.
/// </summary>
public sealed class TypeScriptRequiredParameterTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Required
          version: 1.0.0
        paths:
          /orders:
            get:
              operationId: listOrders
              parameters:
                - name: customerId
                  in: query
                  required: true
                  schema:
                    type: string
                - name: status
                  in: query
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: string
          /invoices:
            get:
              operationId: listInvoices
              parameters:
                - name: status
                  in: query
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: string
          /tenants:
            get:
              operationId: listTenants
              parameters:
                - name: X-Tenant
                  in: header
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: string
          /reports:
            get:
              operationId: listReports
              parameters:
                - name: status
                  in: query
                  schema:
                    type: string
                - name: X-Tenant
                  in: header
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: string
        """;

    [Fact]
    public void BuildQueryTypeInline_RequiredParameter_IsNotOptional()
    {
        var queryParams = new List<OpenApiParameter>
        {
            new() { Name = "customerId", In = ParameterLocation.Query, Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
            new() { Name = "status", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        var result = TypeScriptOperationHelper.BuildQueryTypeInline(queryParams);

        Assert.Equal("{ customerId: string; status?: string }", result);
    }

    [Fact]
    public void BuildQueryTypeInline_RequiredParameterWithADefault_StaysOptional()
    {
        // The default is what a missing value means, so the caller may leave the parameter out - as in the
        // C# clients, where such a parameter has a default value.
        var queryParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "pageSize",
                In = ParameterLocation.Query,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Default = System.Text.Json.Nodes.JsonValue.Create(10) },
            },
        };

        Assert.Equal("{ pageSize?: number /* default: 10 */ }", TypeScriptOperationHelper.BuildQueryTypeInline(queryParams));
        Assert.StartsWith("query?: ", TypeScriptOperationHelper.BuildQueryParameter(queryParams), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHeaderParameter_RequiredHeaderWithADefault_StaysOptional()
    {
        var headerParams = new List<OpenApiParameter>
        {
            new()
            {
                Name = "X-Api-Version",
                In = ParameterLocation.Header,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Default = System.Text.Json.Nodes.JsonValue.Create("1") },
            },
        };

        Assert.Equal("headers?: { 'X-Api-Version'?: string /* default: '1' */ }", TypeScriptOperationHelper.BuildHeaderParameter(headerParams));
    }

    [Theory]
    [InlineData(TypeScriptHooksStyle.None, "client/OrdersClient.ts", "listOrders(")]
    [InlineData(TypeScriptHooksStyle.ReactQuery, "hooks/useOrders.ts", "useListOrders(")]
    [InlineData(TypeScriptHooksStyle.Swr, "hooks/useOrders.ts", "useListOrders(")]
    public void RequiredQueryParameter_MakesTheQueryArgumentRequired(
        TypeScriptHooksStyle hooksStyle,
        string file,
        string method)
    {
        var signature = SignatureOf(hooksStyle, file, method);

        Assert.Contains("query: { customerId: string; status?: string }", signature, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TypeScriptHooksStyle.None, "client/InvoicesClient.ts", "listInvoices(")]
    [InlineData(TypeScriptHooksStyle.ReactQuery, "hooks/useInvoices.ts", "useListInvoices(")]
    [InlineData(TypeScriptHooksStyle.Swr, "hooks/useInvoices.ts", "useListInvoices(")]
    public void OptionalQueryParameters_KeepTheQueryArgumentOptional(
        TypeScriptHooksStyle hooksStyle,
        string file,
        string method)
    {
        var signature = SignatureOf(hooksStyle, file, method);

        Assert.Contains("query?: { status?: string }", signature, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TypeScriptHooksStyle.None, "client/TenantsClient.ts", "listTenants(")]
    [InlineData(TypeScriptHooksStyle.ReactQuery, "hooks/useTenants.ts", "useListTenants(")]
    [InlineData(TypeScriptHooksStyle.Swr, "hooks/useTenants.ts", "useListTenants(")]
    public void RequiredHeaderParameter_MakesTheHeadersArgumentRequired(
        TypeScriptHooksStyle hooksStyle,
        string file,
        string method)
    {
        var signature = SignatureOf(hooksStyle, file, method);

        Assert.Contains("headers: { 'X-Tenant': string }", signature, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TypeScriptHooksStyle.None, "client/ReportsClient.ts", "listReports(")]
    [InlineData(TypeScriptHooksStyle.ReactQuery, "hooks/useReports.ts", "useListReports(")]
    [InlineData(TypeScriptHooksStyle.Swr, "hooks/useReports.ts", "useListReports(")]
    public void OptionalArgumentBeforeARequiredOne_IsPassedAsUndefined(
        TypeScriptHooksStyle hooksStyle,
        string file,
        string method)
    {
        // TypeScript does not allow "query?" before a required "headers": the optional argument keeps its
        // position and takes undefined explicitly.
        var signature = SignatureOf(hooksStyle, file, method);

        Assert.Contains("query: { status?: string } | undefined, headers: { 'X-Tenant': string }", signature, StringComparison.Ordinal);
    }

    private static string SignatureOf(
        TypeScriptHooksStyle hooksStyle,
        string file,
        string method)
    {
        Assert.True(OpenApiDocumentHelper.TryParseYaml(Yaml, "Required.yaml", out var document));

        var outputPath = Path.Combine(Path.GetTempPath(), "atc-required-parameter", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputPath);

        try
        {
            var config = new TypeScriptClientConfig
            {
                HttpClient = TypeScriptHttpClient.Fetch,
                HooksStyle = hooksStyle,
                GenerateFileHeaders = false,
                Scaffold = false,
            };

            TypeScriptClientGenerationService.Generate(document!, outputPath, config);

            var content = File.ReadAllText(Path.Combine(outputPath, file));
            var line = content
                .Split('\n')
                .FirstOrDefault(l => l.Contains(method, StringComparison.Ordinal));

            Assert.True(line is not null, $"No '{method}' in {file}:\n{content}");
            return line!;
        }
        finally
        {
            Directory.Delete(outputPath, recursive: true);
        }
    }
}