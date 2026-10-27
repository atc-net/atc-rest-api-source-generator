namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The secured health-check endpoints compile, with the key read from the header only (the default) and with a query
/// parameter configured as well; the key is compared in constant time.
/// </summary>
public sealed class HealthCheckSecurityCompileTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
        """;

    [Theory]
    [InlineData("""{ "healthChecks": { "enabled": true, "security": "apiKey" } }""", false)]
    [InlineData("""{ "healthChecks": { "enabled": true, "security": "apiKey", "apiKeyQueryParameterName": "api-key" } }""", true)]
    public void SecuredHealthChecks_Compile(
        string marker,
        bool readsTheQueryString)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", marker, includeMinimalApi: true, Yaml);

        var health = Assert.Single(sources, s => s.Source.Contains("HealthChecks:ApiKey", StringComparison.Ordinal)).Source;
        Assert.Contains("CryptographicOperations.FixedTimeEquals", health, StringComparison.Ordinal);
        Assert.Equal(readsTheQueryString, health.Contains("Request.Query[\"api-key\"]", StringComparison.Ordinal));

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true);
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }
}