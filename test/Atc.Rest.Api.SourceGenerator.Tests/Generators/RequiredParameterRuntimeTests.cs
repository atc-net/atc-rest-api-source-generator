namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A required query or header parameter that was not sent is answered with a problem naming it on every host - also
/// in Development, where <c>RouteHandlerOptions.ThrowOnBadRequest</c> is <see langword="true"/> and the framework
/// throws <c>BadHttpRequestException</c> while it binds the <c>[AsParameters]</c> record, before any endpoint filter
/// (the ValidationFilter) runs.
/// </summary>
[Collection(nameof(RequiredParameterRuntimeTests))]
[CollectionDefinition(nameof(RequiredParameterRuntimeTests), DisableParallelization = true)]
public sealed class RequiredParameterRuntimeTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /meters:
            get:
              operationId: listMeters
              parameters:
                - name: energyTypes
                  in: query
                  required: true
                  schema:
                    type: array
                    minItems: 1
                    items:
                      $ref: '#/components/schemas/EnergyType'
                - name: limit
                  in: query
                  required: true
                  schema:
                    type: integer
                    format: int32
                - name: X-Tenant
                  in: header
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: What the server bound
                  content:
                    application/json:
                      schema:
                        type: string
        components:
          schemas:
            EnergyType:
              type: string
              enum: [electricity, heat, water]
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class ListMetersHandlerImpl : IListMetersHandler
        {
            public System.Threading.Tasks.Task<ListMetersResult> ExecuteAsync(ListMetersParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(ListMetersResult.Ok($"{string.Join("|", parameters.EnergyTypes)} {parameters.Limit} {parameters.Tenant}"));
        }

        public static class RequiredRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<IListMetersHandler, ListMetersHandlerImpl>();
        }
        """";

    /// <summary>
    /// Each required parameter left out on its own is answered with a 400 problem naming it, by the name the
    /// specification gives it - in Development (<paramref name="throwOnBadRequest"/>) as in Production.
    /// </summary>
    [Theory]
    [InlineData(true, "/meters?limit=5", "acme", "energyTypes")]
    [InlineData(false, "/meters?limit=5", "acme", "energyTypes")]
    [InlineData(true, "/meters?energyTypes=heat", "acme", "limit")]
    [InlineData(false, "/meters?energyTypes=heat", "acme", "limit")]
    [InlineData(true, "/meters?energyTypes=heat&limit=5", null, "X-Tenant")]
    [InlineData(false, "/meters?energyTypes=heat&limit=5", null, "X-Tenant")]
    public async Task Server_AMissingRequiredParameter_IsA400NamingIt(
        bool throwOnBadRequest,
        string path,
        string? tenant,
        string expectedKey)
    {
        await using var host = await StartAsync(throwOnBadRequest);

        var (status, mediaType, body) = await SendAsync(host, path, tenant);

        Assert.True(status == 400, $"{status}: {body}");
        Assert.StartsWith("application/problem+json", mediaType, StringComparison.Ordinal);
        Assert.True(JsonNode.Parse(body)?["errors"] is JsonObject, body);
        var errors = JsonNode.Parse(body)!["errors"]!.AsObject();
        Assert.Equal([expectedKey], errors.Select(e => e.Key), StringComparer.Ordinal);
        Assert.Equal($"The {expectedKey} field is required.", (string?)errors[expectedKey]?.AsArray().FirstOrDefault());
    }

    /// <summary>
    /// Every required parameter that was left out is named in the one problem.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Server_EveryMissingRequiredParameter_IsNamedInTheOneProblem(
        bool throwOnBadRequest)
    {
        await using var host = await StartAsync(throwOnBadRequest);

        var (status, mediaType, body) = await SendAsync(host, "/meters", tenant: null);

        Assert.Equal(400, status);
        Assert.StartsWith("application/problem+json", mediaType, StringComparison.Ordinal);
        var errors = JsonNode.Parse(body)!["errors"]!.AsObject();
        Assert.Equal(["X-Tenant", "energyTypes", "limit"], errors.Select(e => e.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    /// <summary>
    /// With every required parameter sent the request reaches the handler, in Development as in Production.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Server_EveryRequiredParameterSent_ReachesTheHandler(
        bool throwOnBadRequest)
    {
        await using var host = await StartAsync(throwOnBadRequest);

        var (status, _, body) = await SendAsync(host, "/meters?energyTypes=heat&energyTypes=water&limit=5", "acme");

        Assert.Equal(200, status);
        Assert.Equal("\"Heat|Water 5 acme\"", body);
    }

    private static Task<XmlTestHost> StartAsync(bool throwOnBadRequest)
        => XmlTestHost.StartAsync(
            Yaml,
            HandlerSource,
            "ErrorBodies.RuntimeTest.RequiredRegistration",
            configureServices: services => services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = throwOnBadRequest));

    private static async Task<(int Status, string? MediaType, string Body)> SendAsync(
        XmlTestHost host,
        string path,
        string? tenant)
    {
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        if (tenant is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Tenant", tenant);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return ((int)response.StatusCode, response.Content.Headers.ContentType?.ToString(), await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}