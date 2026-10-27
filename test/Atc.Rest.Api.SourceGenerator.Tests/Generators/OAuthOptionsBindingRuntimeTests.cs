namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The generated OAuth registration reads each option from its configuration section with generated code, as the
/// reflection-based binder would - a value given overrides the default from the specification, an option left out
/// keeps it, a number that does not parse is an error naming the key - so it works in a trimmed or Native AOT app,
/// where the configuration-binding source generator never sees generated code.
/// </summary>
public sealed class OAuthOptionsBindingRuntimeTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Orders
          version: 1.0.0
        paths:
          /orders:
            get:
              operationId: listOrders
              security:
                - oauth2: ['orders:read']
              responses:
                '204':
                  description: Listed
        components:
          securitySchemes:
            oauth2:
              type: oauth2
              flows:
                authorizationCode:
                  authorizationUrl: https://auth.contoso.com/authorize
                  tokenUrl: https://auth.contoso.com/token
                  refreshUrl: https://auth.contoso.com/refresh
                  scopes:
                    orders:read: Read orders
        """;

    private const string ProbeSource = """"
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Options;

        namespace Orders.RuntimeTest;

        public static class OAuthProbe
        {
            public static Dictionary<string, object?> Bind(Dictionary<string, string?> values)
            {
                var assembly = typeof(OAuthProbe).Assembly;
                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
                assembly.GetTypes().Single(t => t.Name == "OAuthServiceCollectionExtensions").GetMethods().Single(m => m.Name.EndsWith("OAuthAuthentication", StringComparison.Ordinal) && m.ReturnType == typeof(IServiceCollection)).Invoke(null, [services, null]);
                using var provider = services.BuildServiceProvider();
                var optionsType = assembly.GetTypes().Single(t => t.Name == "OAuthClientOptions");
                var accessor = provider.GetRequiredService(typeof(IOptions<>).MakeGenericType(optionsType));
                object options;
                try
                {
                    options = accessor.GetType().GetProperty("Value")!.GetValue(accessor)!;
                }
                catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    throw ex.InnerException;
                }

                return optionsType.GetProperties().ToDictionary(p => p.Name, p => p.GetValue(options));
            }
        }
        """";

    [Fact]
    public void Registration_BindsEveryOption_FromTheSection()
    {
        var options = Bind(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["OAuth:Orders:TokenUrl"] = "https://login.contoso.com/token",
            ["OAuth:Orders:ClientId"] = "orders-app",
            ["OAuth:Orders:ClientSecret"] = "s3cret",
            ["OAuth:Orders:RefreshBufferSeconds"] = "30",
            ["OAuth:Orders:RefreshUrl"] = "https://login.contoso.com/refresh",
        });

        Assert.Equal("https://login.contoso.com/token", options["TokenUrl"]);
        Assert.Equal("orders-app", options["ClientId"]);
        Assert.Equal("s3cret", options["ClientSecret"]);
        Assert.Equal(30, options["RefreshBufferSeconds"]);
        Assert.Equal("https://login.contoso.com/refresh", options["RefreshUrl"]);

        // Left out: the default from the specification
        Assert.Equal("orders:read", options["Scopes"]);
    }

    [Fact]
    public void Registration_ANumberThatDoesNotParse_IsAnErrorNamingTheKey()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Bind(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["OAuth:Orders:RefreshBufferSeconds"] = "soon",
        }));

        Assert.Contains("OAuth:Orders:RefreshBufferSeconds", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registration_UsesNoReflectionBasedBinder()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml);

        Assert.Contains(sources, s => s.Source.Contains("OAuthServiceCollectionExtensions", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, s => s.Source.Contains("BindConfiguration", StringComparison.Ordinal));
    }

    private static Dictionary<string, object?> Bind(
        Dictionary<string, string?> values)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml);
        sources.Add(("OAuthProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        try
        {
            return (Dictionary<string, object?>)assembly.GetType("Orders.RuntimeTest.OAuthProbe", throwOnError: true)!.GetMethod("Bind")!.Invoke(null, [values])!;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}