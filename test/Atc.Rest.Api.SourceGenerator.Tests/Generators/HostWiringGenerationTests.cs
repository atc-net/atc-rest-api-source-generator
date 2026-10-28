namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A host wires the generated server with three methods: <c>Add{Api}Api()</c> (services), <c>Use{Api}Api()</c>
/// (middleware) and <c>Map{Api}Api()</c> (endpoints), all in one generated file per kind. v2's separate
/// <c>Use{Api}Api(Action&lt;GlobalErrorHandlingOptions&gt;)</c>, which added the global error handler and nothing else,
/// is not generated: the new <c>Use{Api}Api()</c> adds that handler itself, first, behind its
/// <c>UseGlobalErrorHandler</c> switch and with <c>ConfigureErrorHandling</c> for its options.
/// </summary>
public sealed class HostWiringGenerationTests
{
    [Fact]
    public void Server_WithTheGlobalErrorHandler_GeneratesTheUseAndMapMethodsInOneFile()
    {
        var sources = CompilationVerificationHarness.RunServer("SecurityBearerOnly", "SecurityBearerOnly.yaml");

        Assert.DoesNotContain(sources, s => s.HintName.EndsWith(".WebApplicationExtensions.g.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, s => s.Source.Contains("Api(this WebApplication app, Action<GlobalErrorHandlingOptions>?", StringComparison.Ordinal));

        var unified = Assert.Single(sources, s => s.HintName.EndsWith(".UnifiedWebApplicationExtensions.g.cs", StringComparison.Ordinal));
        Assert.Contains("app.UseGlobalErrorHandler(options.ConfigureErrorHandling);", unified.Source, StringComparison.Ordinal);
        Assert.Contains("public static WebApplication UseSecurityBearerOnlyApi(", unified.Source, StringComparison.Ordinal);
        Assert.Contains("public static WebApplication MapSecurityBearerOnlyApi(", unified.Source, StringComparison.Ordinal);
    }
}