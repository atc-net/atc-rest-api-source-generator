namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The generated <c>AddApiRateLimiting()</c> registers its policies once, however often it is called: the generated
/// <c>Add{Project}Api()</c> calls it, and a host that still calls it itself (as it had to before) must not see the
/// policies added twice - <c>RateLimiterOptions</c> throws on a policy name it already has, and the host would never
/// start. A repeat call still applies its own <c>configure</c>.
/// </summary>
public sealed class RateLimitingRegistrationRuntimeTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things:
            x-ratelimit-policy: things-fixed
            x-ratelimit-permit-limit: 10
            x-ratelimit-window-seconds: 60
            get:
              operationId: listThings
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /things/per-caller:
            x-ratelimit-policy: things-per-ip
            x-ratelimit-permit-limit: 5
            x-ratelimit-window-seconds: 60
            x-ratelimit-partition: ip
            get:
              operationId: listThingsPerCaller
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
        components:
          schemas:
            Thing:
              type: object
              properties:
                name: { type: string }
        """;

    private static readonly Lazy<Assembly> Server = new(() =>
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(
            new ApiServerGenerator(),
            ".atc-rest-api-server",
            ErrorResponseBodyRuntimeTests.ServerMarker,
            includeMinimalApi: true,
            Yaml);
        return CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
    });

    [Fact]
    public void ASingleCall_RegistersEachPolicyOnce()
    {
        var services = new ServiceCollection();
        AddApiRateLimiting(services);

        Assert.Equal(["things-fixed", "things-per-ip"], PolicyNames(services));
    }

    [Fact]
    public void AHostsOwnCall_BeforeAddApi_DoesNotRegisterThePoliciesTwice()
    {
        var services = new ServiceCollection();
        AddApiRateLimiting(services);
        AddApi(services);

        Assert.Equal(["things-fixed", "things-per-ip"], PolicyNames(services));
    }

    [Fact]
    public void AHostsOwnCall_AfterAddApi_DoesNotRegisterThePoliciesTwice()
    {
        var services = new ServiceCollection();
        AddApi(services);
        AddApiRateLimiting(services);

        Assert.Equal(["things-fixed", "things-per-ip"], PolicyNames(services));
    }

    [Fact]
    public void AHostsOwnCallWithConfigure_AfterAddApi_AppliesTheConfigure()
    {
        var services = new ServiceCollection();
        AddApi(services);
        AddApiRateLimiting(services, o => o.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>>().Value;

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, options.RejectionStatusCode);
    }

    [Fact]
    public void AHostsOwnCallWithConfigure_BeforeAddApi_KeepsTheConfigure()
    {
        var services = new ServiceCollection();
        AddApiRateLimiting(services, o => o.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable);
        AddApi(services);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>>().Value;

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, options.RejectionStatusCode);
    }

    private static void AddApi(IServiceCollection services)
        => Server.Value
            .GetType("ErrorBodies.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!
            .GetMethod("AddErrorBodiesApi")!
            .Invoke(null, [services, null]);

    private static void AddApiRateLimiting(
        IServiceCollection services,
        Action<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>? configure = null)
        => Server.Value
            .GetType("ErrorBodies.Generated.RateLimiting.RateLimitingServiceCollectionExtensions", throwOnError: true)!
            .GetMethod("AddApiRateLimiting")!
            .Invoke(null, [services, configure]);

    /// <summary>
    /// The policy names the resolved <c>RateLimiterOptions</c> hold (resolving it runs every registered configure,
    /// and throws on a policy name added twice), sorted.
    /// </summary>
    private static List<string> PolicyNames(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>>().Value;

        return new[] { "PolicyMap", "UnactivatedPolicyMap" }
            .Select(name => (System.Collections.IDictionary)typeof(Microsoft.AspNetCore.RateLimiting.RateLimiterOptions)
                .GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(options)!)
            .SelectMany(map => map.Keys.Cast<string>())
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}