namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A host on <c>Add{Api}ApiServices()</c>, <c>Use{Api}ApiMiddleware()</c> and <c>Map{Api}ApiEndpoints()</c> builds without
/// a warning (a host with TreatWarningsAsErrors would stop at one) in every form it can call them.
/// </summary>
public sealed class HostMethodNamesCompileTests
{
    private const string Scenario = "SecurityBearerOnly";

    private const string Host = """
                                using Microsoft.AspNetCore.Builder;
                                using SecurityBearerOnly.Generated.Endpoints;
                                using SecurityBearerOnly.Generated.Extensions;

                                namespace HostNames;

                                internal static class Hosts
                                {
                                    public static void NewNames(WebApplicationBuilder builder)
                                    {
                                        builder.Services.AddSecurityBearerOnlyApiServices();
                                        builder.Services.AddSecurityBearerOnlyApiServices(o => o.UseProblemDetails = false);
                                        var app = builder.Build();
                                        app.UseSecurityBearerOnlyApiMiddleware();
                                        app.UseSecurityBearerOnlyApiMiddleware(o => o.AfterErrorHandling = pipeline => pipeline.UseHsts());
                                        app.MapSecurityBearerOnlyApiEndpoints();
                                        app.MapSecurityBearerOnlyApiEndpoints(requireUse: false);
                                        app.MapSecurityBearerOnlyApiEndpoints(false);
                                    }

                                }
                                """;

    [Fact]
    public void Server_TheHostMethods_BuildWithoutAWarning()
    {
        // Arrange
        var sources = CompilationVerificationHarness.RunServer(Scenario, Scenario + ".yaml");
        sources.Add(("Host.cs", Host));
        var includeMinimalApi = CompilationVerificationHarness.ScenarioReferencesMinimalApi(Scenario);

        // Act
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi);
        var warnings = CompilationVerificationHarness.CompileGeneratedSourcesForWarnings(sources, includeMinimalApi);

        // Assert
        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    /// <summary>
    /// A path segment named like the API (<c>/security-bearer-only-api</c>) gets its own
    /// <c>MapSecurityBearerOnlyApiEndpoints(this WebApplication)</c>. A host that imports both namespaces must not
    /// quietly call it - it maps one segment and skips the check that the middleware was added - so the call is
    /// ambiguous (CS0121) and the host picks one by its class.
    /// </summary>
    [Fact]
    public void Server_ASegmentNamedLikeTheApi_MakesTheCallAmbiguous_NotTheSegmentsMethod()
    {
        // Arrange
        var sources = CompilationVerificationHarness.RunGenerator(
                new ApiServerGenerator(),
                Scenario,
                Scenario + ".yaml",
                ".atc-rest-api-server",
                "Server",
                useFullReferences: true,
                yamlTransform: yaml => yaml.Replace(
                    "paths:\n",
                    "paths:\n  /security-bearer-only-api/ping:\n    get:\n      operationId: ping\n      responses:\n        '204':\n          description: No Content\n",
                    StringComparison.Ordinal))
            .GeneratedSources;
        Assert.Contains(sources, s => s.Source.Contains("public static WebApplication MapSecurityBearerOnlyApiEndpoints(this WebApplication app)", StringComparison.Ordinal));
        sources.Add(("Host.cs", """
                                using Microsoft.AspNetCore.Builder;
                                using SecurityBearerOnly.Generated.Endpoints;
                                using SecurityBearerOnly.Generated.Extensions;

                                namespace HostNames;

                                internal static class Hosts
                                {
                                    public static void Map(WebApplication app)
                                        => app.MapSecurityBearerOnlyApiEndpoints();
                                }
                                """));

        // Act
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, CompilationVerificationHarness.ScenarioReferencesMinimalApi(Scenario));

        // Assert
        Assert.Contains(errors, e => e.Contains("CS0121", StringComparison.Ordinal));
    }
}