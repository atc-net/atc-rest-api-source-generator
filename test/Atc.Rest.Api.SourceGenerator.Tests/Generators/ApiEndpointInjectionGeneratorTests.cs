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
}