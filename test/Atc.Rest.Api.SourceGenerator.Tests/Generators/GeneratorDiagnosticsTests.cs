namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The diagnostics only a generator run can report - a package the project lacks (<c>ATC_API_DEP*</c>), a
/// document that does not parse, a generation error, the summary (<c>ATC_API_GEN*</c>), a client option that is
/// ignored (<c>ATC_API_CLT002</c>), a document the OpenAPI parser rejects (<c>ATC_API_VAL001</c>) - each
/// produced by the real generator under a controlled reference set, and each silent when the condition is gone.
/// </summary>
public class GeneratorDiagnosticsTests
{
    private const string ProjectName = "Contoso.Api";

    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Thing'
        components:
          schemas:
            Thing:
              type: object
              required: [name]
              properties:
                name: { type: string }
        """;

    private const string MalformedYaml = """
        openapi: 3.0.3
        info:
          title: [unterminated
        paths:
        """;

    // The OpenAPI parser reads it but its rules reject it: a response without the required description
    private const string NotOpenApiYaml = """
        openapi: 3.0.3
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  content:
                    application/json:
                      schema:
                        type: string
        """;

    [Fact]
    public void Server_WithoutAspNetCore_ReportsDEP001()
    {
        var ids = Run(new ApiServerGenerator(), [Spec(Yaml), Marker(".atc-rest-api-server", "{}")], Without("Microsoft.AspNetCore"));
        Assert.Contains("ATC_API_DEP001", ids);

        Assert.DoesNotContain("ATC_API_DEP001", Run(new ApiServerGenerator(), [Spec(Yaml), Marker(".atc-rest-api-server", "{}")], Full()));
    }

    [Fact]
    public void Domain_WithoutAspNetCore_ReportsDEP002()
    {
        var ids = Run(new ApiServerDomainGenerator(), [Spec(Yaml), Marker(".atc-rest-api-server-handlers", "{}")], Without("Microsoft.AspNetCore"));
        Assert.Contains("ATC_API_DEP002", ids);
    }

    [Fact]
    public void Client_WithoutAtcRestClient_ReportsDEP003()
    {
        var ids = Run(new ApiClientGenerator(), [Spec(Yaml), Marker(".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation" }""")], Without("Atc.Rest.Client"));
        Assert.Contains("ATC_API_DEP003", ids);

        Assert.DoesNotContain("ATC_API_DEP003", Run(new ApiClientGenerator(), [Spec(Yaml), Marker(".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation" }""")], Full()));
    }

    [Fact]
    public void Client_WithRetryPoliciesButWithoutResilience_ReportsDEP005()
    {
        var withRetry = Yaml.Replace("paths:", "x-retry-policy: standard\nx-retry-max-attempts: 3\npaths:", StringComparison.Ordinal);
        var ids = Run(new ApiClientGenerator(), [Spec(withRetry), Marker(".atc-rest-api-client", "{}")], Without("Microsoft.Extensions.Http.Resilience"));
        Assert.Contains("ATC_API_DEP005", ids);

        Assert.DoesNotContain("ATC_API_DEP005", Run(new ApiClientGenerator(), [Spec(withRetry), Marker(".atc-rest-api-client", "{}")], Full()));
        Assert.DoesNotContain("ATC_API_DEP005", Run(new ApiClientGenerator(), [Spec(Yaml), Marker(".atc-rest-api-client", "{}")], Without("Microsoft.Extensions.Http.Resilience")));
    }

    /// <summary>
    /// A bearer scheme asks the host for a handler registered as <c>"Bearer"</c>; without a package that
    /// provides one (JwtBearer, Microsoft.Identity.Web) the generator says so - as information, since a
    /// hand-written handler is a valid choice.
    /// </summary>
    [Fact]
    public void Server_WithBearerSchemeButNoBearerHandlerPackage_ReportsDEP006()
    {
        var withBearer = Yaml.Replace("components:", "security:\n  - bearer_auth: []\ncomponents:\n  securitySchemes:\n    bearer_auth:\n      type: http\n      scheme: bearer", StringComparison.Ordinal);
        var diagnostics = RunDiagnostics(new ApiServerGenerator(), [Spec(withBearer), Marker(".atc-rest-api-server", "{}")], Without("Microsoft.AspNetCore.Authentication.JwtBearer", "Microsoft.Identity.Web"));
        var info = Assert.Single(diagnostics, d => d.Id == "ATC_API_DEP006");
        Assert.Equal(DiagnosticSeverity.Info, info.Severity);

        Assert.DoesNotContain("ATC_API_DEP006", Run(new ApiServerGenerator(), [Spec(withBearer), Marker(".atc-rest-api-server", "{}")], Plus(Full(), "Microsoft.AspNetCore.Authentication.JwtBearer")));
        Assert.DoesNotContain("ATC_API_DEP006", Run(new ApiServerGenerator(), [Spec(withBearer), Marker(".atc-rest-api-server", "{}")], Plus(Full(), "Microsoft.Identity.Web")));
        Assert.DoesNotContain("ATC_API_DEP006", Run(new ApiServerGenerator(), [Spec(Yaml), Marker(".atc-rest-api-server", "{}")], Without("Microsoft.AspNetCore.Authentication.JwtBearer")));
    }

    [Theory]
    [InlineData("useMinimalApiPackage")]
    [InlineData("useValidationFilter")]
    public void Server_MinimalApiModeEnabledWithoutPackage_ReportsDEP007(
        string setting)
    {
        var marker = Marker(".atc-rest-api-server", $$$"""{ "{{{setting}}}": "Enabled" }""");
        Assert.Contains("ATC_API_DEP007", Run(new ApiServerGenerator(), [Spec(Yaml), marker], Full()));
        Assert.DoesNotContain("ATC_API_DEP007", Run(new ApiServerGenerator(), [Spec(Yaml), marker], Full(includeMinimalApi: true)));
    }

    [Fact]
    public void Domain_InjectLoggerWithoutLoggingAbstractions_ReportsDEP008()
    {
        var marker = Marker(".atc-rest-api-server-handlers", """{ "injectLogger": true }""");
        Assert.Contains("ATC_API_DEP008", Run(new ApiServerDomainGenerator(), [Spec(Yaml), marker], Without("Microsoft.Extensions.Logging")));
        Assert.DoesNotContain("ATC_API_DEP008", Run(new ApiServerDomainGenerator(), [Spec(Yaml), marker], Full()));
    }

    [Fact]
    public void Server_HealthChecksConfiguredAndAHealthPathInTheSpec_ReportsDEP009()
    {
        var withHealthPath = Yaml.Replace("paths:", "paths:\n  /health:\n    get:\n      operationId: getHealth\n      responses:\n        '200':\n          description: OK", StringComparison.Ordinal);
        var marker = Marker(".atc-rest-api-server", """{ "healthChecks": { "enabled": true, "path": "/health" } }""");
        Assert.Contains("ATC_API_DEP009", Run(new ApiServerGenerator(), [Spec(withHealthPath), marker], Full()));
        Assert.DoesNotContain("ATC_API_DEP009", Run(new ApiServerGenerator(), [Spec(Yaml), marker], Full()));
    }

    [Fact]
    public void Server_HealthChecksConfiguredWithoutThePackage_ReportsDEP010()
    {
        var marker = Marker(".atc-rest-api-server", """{ "healthChecks": { "enabled": true } }""");
        Assert.Contains("ATC_API_DEP010", Run(new ApiServerGenerator(), [Spec(Yaml), marker], Without("Atc.Rest.HealthChecks")));
        Assert.DoesNotContain("ATC_API_DEP010", Run(new ApiServerGenerator(), [Spec(Yaml), marker], Plus(Full(), "Atc.Rest.HealthChecks")));
    }

    [Fact]
    public void Server_DocumentThatDoesNotParse_ReportsGEN002()
        => Assert.Contains("ATC_API_GEN002", Run(new ApiServerGenerator(), [Spec(MalformedYaml), Marker(".atc-rest-api-server", "{}")], Full()));

    [Fact]
    public void Client_DocumentThatDoesNotParse_ReportsGEN004()
        => Assert.Contains("ATC_API_GEN004", Run(new ApiClientGenerator(), [Spec(MalformedYaml), Marker(".atc-rest-api-client", "{}")], Full()));

    [Fact]
    public void Domain_DocumentThatDoesNotParse_ReportsGEN006()
        => Assert.Contains("ATC_API_GEN006", Run(new ApiServerDomainGenerator(), [Spec(MalformedYaml), Marker(".atc-rest-api-server-handlers", "{}")], Full()));

    [Fact]
    public void Domain_WithoutAnOutputDirectory_ReportsGEN007()
    {
        var ids = Run(new ApiServerDomainGenerator(), [Spec(Yaml), Marker(".atc-rest-api-server-handlers", """{ "generateHandlersOutput": "" }""")], Full());
        Assert.Contains("ATC_API_GEN007", ids);
    }

    [Fact]
    public void EndpointInjection_WithoutADocumentForTheProject_ReportsGEN009()
    {
        const string source = """
            namespace Contoso.Host;

            [Atc.Rest.Api.SourceGenerator.EndpointRegistration("Contoso.Api")]
            public static partial class Endpoints
            {
            }
            """;
        Assert.Contains("ATC_API_GEN009", Run(new ApiEndpointInjectionGenerator(), [Spec(Yaml, "Other.yaml")], Full(), source));
        Assert.DoesNotContain("ATC_API_GEN009", Run(new ApiEndpointInjectionGenerator(), [Spec(Yaml)], Full(), source));
    }

    [Fact]
    public void EndpointInjection_AnotherAttributeWithTheSameName_IsIgnored()
    {
        // Only the generator's own attribute (by its full name) triggers the injection, not a namesake
        const string source = """
            namespace Contoso.Other
            {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                public sealed class EndpointRegistrationAttribute(string name) : System.Attribute
                {
                    public string Name { get; } = name;
                }
            }

            namespace Contoso.Host
            {
                [Contoso.Other.EndpointRegistration("Contoso.Api")]
                public static partial class Endpoints
                {
                }
            }
            """;
        Assert.DoesNotContain("ATC_API_GEN009", Run(new ApiEndpointInjectionGenerator(), [Spec(Yaml, "Other.yaml")], Full(), source));
    }

    [Theory]
    [InlineData("server")]
    [InlineData("client")]
    public void Generator_ThatSucceeds_ReportsGEN010(string generator)
    {
        var diagnostics = generator == "server"
            ? RunDiagnostics(new ApiServerGenerator(), [Spec(Yaml), Marker(".atc-rest-api-server", "{}")], Full())
            : RunDiagnostics(new ApiClientGenerator(), [Spec(Yaml), Marker(".atc-rest-api-client", "{}")], Full());
        var summary = Assert.Single(diagnostics, d => d.Id == "ATC_API_GEN010");
        Assert.Equal(DiagnosticSeverity.Info, summary.Severity);
        Assert.Contains("Generated from", summary.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void Client_ClientNameUnderPerArea_ReportsCLT002()
    {
        var marker = Marker(".atc-rest-api-client", """{ "generationMode": "TypedClient", "clientGranularity": "PerArea", "clientName": "ContosoClient" }""");
        Assert.Contains("ATC_API_CLT002", Run(new ApiClientGenerator(), [Spec(Yaml), marker], Full()));
        Assert.DoesNotContain("ATC_API_CLT002", Run(new ApiClientGenerator(), [Spec(Yaml), Marker(".atc-rest-api-client", """{ "generationMode": "TypedClient", "clientGranularity": "Single", "clientName": "ContosoClient" }""")], Full()));
    }

    /// <summary>
    /// A document the OpenAPI parser reads but rejects (a <c>$ref</c> to nothing) is reported with the parser's
    /// own message, not as a generation error.
    /// </summary>
    [Fact]
    public void Server_DocumentTheOpenApiParserRejects_ReportsVAL001()
    {
        var ids = Run(new ApiServerGenerator(), [Spec(NotOpenApiYaml), Marker(".atc-rest-api-server", "{}")], Full());
        Assert.Contains("ATC_API_VAL001", ids);
        Assert.DoesNotContain("ATC_API_GEN001", ids);
    }

    private static (string Path, string Content) Spec(
        string yaml,
        string path = ProjectName + ".yaml")
        => (path, yaml);

    private static (string Path, string Content) Marker(
        string fileName,
        string content)
        => (fileName, content);

    private static HashSet<string> Run(
        IIncrementalGenerator generator,
        (string Path, string Content)[] texts,
        IEnumerable<MetadataReference> references,
        string? source = null)
        => RunDiagnostics(generator, texts, references, source).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

    private static List<Diagnostic> RunDiagnostics(
        IIncrementalGenerator generator,
        (string Path, string Content)[] texts,
        IEnumerable<MetadataReference> references,
        string? source = null)
    {
        var compilation = CSharpCompilation.Create(
            ProjectName,
            source is null ? [] : [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts([.. texts.Select(t => (AdditionalText)new CompilationVerificationHarness.InMemoryAdditionalText(t.Path, t.Content))]);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);
        return [.. diagnostics];
    }

    private static List<MetadataReference> Full(bool includeMinimalApi = false)
        => CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi);

    /// <summary>
    /// The full reference set without the assemblies whose name starts with any of the prefixes.
    /// </summary>
    private static List<MetadataReference> Without(
        params string[] assemblyNamePrefixes)
        => Full()
            .Where(r => r is not PortableExecutableReference pe ||
                        !assemblyNamePrefixes.Any(p => Path.GetFileNameWithoutExtension(pe.FilePath ?? string.Empty).StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    /// <summary>
    /// A reference set plus an empty assembly under the given name, standing in for a package the test host does
    /// not reference itself.
    /// </summary>
    private static List<MetadataReference> Plus(
        List<MetadataReference> references,
        string assemblyName)
    {
        var stub = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText("namespace Stub { internal static class Marker { } }")],
            CompilationVerificationHarness.GetMinimalReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return [.. references, stub.ToMetadataReference()];
    }
}