namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Verifies the post-initialization output of <see cref="ApiEndpointInjectionGenerator"/> — the
/// generated <c>EndpointRegistrationAttribute</c> is emitted unconditionally (regardless of marker
/// files or additional texts), so it is exercised here directly via the generator driver.
/// </summary>
public class ApiEndpointInjectionGeneratorTests
{
    [Fact]
    public void GeneratesEndpointRegistrationAttribute_WithGeneratedCodeAttribute()
    {
        // Arrange
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: CompilationVerificationHarness.GetMinimalReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new ApiEndpointInjectionGenerator());

        // Act
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out var diagnostics, TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();
        var attributeSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("EndpointRegistrationAttribute", StringComparison.Ordinal));

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(attributeSource);
        Assert.Contains("using System.CodeDom.Compiler;", attributeSource, StringComparison.Ordinal);
        Assert.Contains($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]", attributeSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// The endpoints of a multi-part specification live in its parts: the partial class is generated from the base
    /// merged with its parts, as the server generator reads it (it used to read the base file alone, so no endpoint
    /// of a part was injected).
    /// </summary>
    [Fact]
    public void GeneratesThePartialClass_FromTheBaseAndItsParts()
    {
        const string source = """
            namespace Contoso.Host;

            [Atc.Rest.Api.SourceGenerator.EndpointRegistration("Contoso")]
            public partial class Program
            {
            }
            """;
        const string baseYaml = """
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200':
                      description: OK
            """;
        const string partYaml = """
            openapi: 3.0.3
            paths:
              /others:
                get:
                  operationId: listOthers
                  responses:
                    '200':
                      description: OK
            """;
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            CompilationVerificationHarness.GetMinimalReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("Contoso.yaml", baseYaml),
            new InMemoryAdditionalText("Contoso_Others.yaml", partYaml));

        var driver = CSharpGeneratorDriver.Create(new ApiEndpointInjectionGenerator()).AddAdditionalTexts(additionalTexts);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var partial = Assert.Single(driver.GetRunResult().GeneratedTrees.Select(t => t.GetText().ToString()), s => s.Contains("partial class Program", StringComparison.Ordinal));
        Assert.Contains("IListThingsEndpoint", partial, StringComparison.Ordinal);
        Assert.Contains("IListOthersEndpoint", partial, StringComparison.Ordinal);
    }
}