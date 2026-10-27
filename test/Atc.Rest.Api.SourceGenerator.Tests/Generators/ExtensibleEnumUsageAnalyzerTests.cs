namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// <c>System.Enum</c>'s methods compile on a generated extensible enum - a struct - and throw at run time. The package's
/// analyzer reports each such call (<c>ATC_API_CLT003</c>), so the build finds them as the compiler finds a <c>case</c>.
/// </summary>
public class ExtensibleEnumUsageAnalyzerTests
{
    private const string Yaml = """
        openapi: 3.1.1
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /devices:
            get:
              operationId: listDevices
              responses:
                '200':
                  description: The devices
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Device'
        components:
          schemas:
            Device:
              type: object
              required: [status]
              properties:
                status:
                  $ref: '#/components/schemas/DeviceStatus'
            DeviceStatus:
              type: string
              enum: [online, offline]
        """;

    private const string Usage = """
        using System;
        using System.Linq;

        namespace ErrorBodies.RuntimeTest;

        public enum Colour
        {
            Red,
            Green,
        }

        public static class Usage
        {
            public static void Calls(string text, DeviceStatus status)
            {
                // Reported: compile, throw at run time
                _ = Enum.TryParse<DeviceStatus>(text, out _);
                _ = Enum.Parse<DeviceStatus>(text);
                _ = Enum.GetValues(typeof(DeviceStatus));
                _ = Enum.IsDefined(typeof(DeviceStatus), status);

                // Not reported: the struct's own way, and a C# enum
                _ = DeviceStatus.TryParse(text, null, out _);
                _ = DeviceStatus.KnownValues.Select(v => v.Value).ToList();
                _ = Enum.TryParse<Colour>(text, out _);
                _ = Enum.GetValues(typeof(Colour));
            }
        }
        """;

    [Theory]
    [InlineData("EndpointPerOperation")]
    [InlineData("TypedClient")]
    public async Task SystemEnumOnAnExtensibleEnum_IsReportedAsCLT003(
        string generationMode)
    {
        // Arrange
        var sources = ErrorResponseBodyRuntimeTests.Generate(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            $$"""{ "generationMode": "{{generationMode}}", "validateSpecificationStrategy": "None" }""",
            includeMinimalApi: false,
            Yaml);
        var usings = string.Join(
            Environment.NewLine,
            sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"using {ns};"));
        var compilation = CSharpCompilation.Create(
            "ErrorBodies",
            [
                .. sources.Select(s => CSharpSyntaxTree.ParseText(s.Source, path: s.HintName, cancellationToken: TestContext.Current.CancellationToken)),
                CSharpSyntaxTree.ParseText(usings + Environment.NewLine + Usage, path: "Usage.cs", cancellationToken: TestContext.Current.CancellationToken),
            ],
            CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: false),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));

        // Act
        var diagnostics = await new Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers(
                compilation,
                [new ExtensibleEnumUsageAnalyzer()],
                new Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzersOptions(new Microsoft.CodeAnalysis.Diagnostics.AnalyzerOptions([]), onAnalyzerException: null, concurrentAnalysis: false, logAnalyzerExecutionTime: false))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert - each call in Usage.cs, on its line, naming the method and the type
        var reported = diagnostics
            .Where(d => d.Id == Generator.RuleIdentifiers.SystemEnumOnExtensibleEnum)
            .Select(d => $"{d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage(CultureInfo.InvariantCulture)}")
            .ToList();
        Assert.Equal(4, reported.Count);

        // A parse leads with the IsKnown check: the struct's own TryParse accepts every value, so swapping one TryParse
        // for the other would drop a fallback for an unlisted value silently
        Assert.Contains(reported, r => r.Contains("Enum.TryParse", StringComparison.Ordinal) &&
                                       r.Contains("new DeviceStatus(text) is { IsKnown: true } value ? value :", StringComparison.Ordinal) &&
                                       r.Contains("'DeviceStatus.TryParse' accepts every value", StringComparison.Ordinal));
        Assert.Contains(reported, r => r.Contains("Enum.Parse", StringComparison.Ordinal) && r.Contains("IsKnown", StringComparison.Ordinal));
        Assert.Contains(reported, r => r.Contains("Enum.GetValues", StringComparison.Ordinal) && r.Contains("KnownValues", StringComparison.Ordinal));
        Assert.Contains(reported, r => r.Contains("Enum.IsDefined", StringComparison.Ordinal) && r.Contains("new DeviceStatus(text).IsKnown", StringComparison.Ordinal));
        Assert.All(diagnostics.Where(d => d.Id == Generator.RuleIdentifiers.SystemEnumOnExtensibleEnum), d => Assert.Equal("Usage.cs", d.Location.GetLineSpan().Path));
    }

    [Fact]
    public async Task APackagedClient_IsCheckedInAConsumerThatReferencesTheGeneratorPackage()
    {
        // Arrange - the client compiled on its own, as its package's lib/ assembly
        var sources = ErrorResponseBodyRuntimeTests.Generate(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            """{ "generationMode": "TypedClient", "validateSpecificationStrategy": "None" }""",
            includeMinimalApi: false,
            Yaml);
        var references = CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: false);
        var client = CSharpCompilation.Create(
            "ErrorBodies.ApiClient",
            sources.Select(s => CSharpSyntaxTree.ParseText(s.Source, path: s.HintName, cancellationToken: TestContext.Current.CancellationToken)),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var image = new MemoryStream();
        Assert.True(client.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success);

        // The consumer: no marker file, the client by reference
        var usings = string.Join(
            Environment.NewLine,
            sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"using {ns};"));
        var consumer = CSharpCompilation.Create(
            "Gateway",
            [CSharpSyntaxTree.ParseText(usings + Environment.NewLine + Usage, path: "Usage.cs", cancellationToken: TestContext.Current.CancellationToken)],
            [.. references, MetadataReference.CreateFromImage(image.ToArray())],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(consumer.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));

        // Act - the package's generators (which generate nothing without a marker) and its analyzer
        var run = CSharpGeneratorDriver
            .Create(new ApiClientGenerator().AsSourceGenerator(), new ApiServerGenerator().AsSourceGenerator())
            .RunGenerators(consumer, TestContext.Current.CancellationToken)
            .GetRunResult();
        var diagnostics = await new Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers(
                consumer,
                [new ExtensibleEnumUsageAnalyzer()],
                new Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzersOptions(new Microsoft.CodeAnalysis.Diagnostics.AnalyzerOptions([]), onAnalyzerException: null, concurrentAnalysis: false, logAnalyzerExecutionTime: false))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(run.GeneratedTrees);
        Assert.Empty(run.Diagnostics);
        Assert.Equal(4, diagnostics.Count(d => d.Id == Generator.RuleIdentifiers.SystemEnumOnExtensibleEnum));
    }
}