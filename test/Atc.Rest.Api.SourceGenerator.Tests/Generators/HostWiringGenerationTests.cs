namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A host wires the generated server with two methods: <c>Add{Api}Api()</c> and <c>Map{Api}Api()</c>.
/// <c>Use{Api}Api()</c>, which v2 generated (with Atc.Rest.MinimalApi) to add the global error handler and nothing
/// else, is not generated: <c>Map{Api}Api()</c> adds that handler itself, first, behind its <c>UseGlobalErrorHandler</c>
/// switch and with <c>ConfigureErrorHandling</c> for its options, and a host that keeps <c>MapEndpoints()</c> calls
/// <c>UseGlobalErrorHandler()</c> directly.
/// </summary>
public sealed class HostWiringGenerationTests
{
    [Fact]
    public void Server_WithTheGlobalErrorHandler_GeneratesNoUseApiMethod()
    {
        var sources = CompilationVerificationHarness.RunServer("SecurityBearerOnly", "SecurityBearerOnly.yaml");

        Assert.DoesNotContain(sources, s => s.HintName.EndsWith(".WebApplicationExtensions.g.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, s => s.Source.Contains("Api(this WebApplication app, Action<GlobalErrorHandlingOptions>?", StringComparison.Ordinal));

        var unified = Assert.Single(sources, s => s.HintName.EndsWith(".UnifiedWebApplicationExtensions.g.cs", StringComparison.Ordinal));
        Assert.Contains("app.UseGlobalErrorHandler(options.ConfigureErrorHandling);", unified.Source, StringComparison.Ordinal);
    }
}