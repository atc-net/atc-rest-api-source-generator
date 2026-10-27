namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// <c>status != DeviceStatus.Online</c> compiles unchanged on an extensible enum, and a value the specification adds
/// later is "not Online" there - often for good. <c>ATC_API_CLT004</c> (off by default, an <c>.editorconfig</c> line
/// turns it on) points at each comparison with one listed value, so the code decides what an unknown value means; one
/// in a condition that also reads <c>IsKnown</c> has decided, and is not reported.
/// </summary>
public class ExtensibleEnumComparisonAnalyzerTests
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
        namespace ErrorBodies.RuntimeTest;

        public enum Colour
        {
            Red,
            Green,
        }

        public static class Usage
        {
            public static bool[] Gates(DeviceStatus status, DeviceStatus other, DeviceStatus? maybe, Colour colour)
                =>
                [
                    // Reported: an unknown value is "not Online" / "not Offline"
                    status != DeviceStatus.Online,
                    DeviceStatus.Offline == status,
                    maybe == DeviceStatus.Online,

                    // Not reported: decided with IsKnown, two values, two listed values, a C# enum
                    status.IsKnown && status != DeviceStatus.Online,
                    !status.IsKnown || status == DeviceStatus.Offline,
                    status == other,
                    DeviceStatus.Online == DeviceStatus.Offline,
                    colour != Colour.Red,
                ];
        }
        """;

    [Theory]
    [InlineData("EndpointPerOperation")]
    [InlineData("TypedClient")]
    public async Task AComparisonWithOneListedValue_IsReportedAsCLT004_WhenTurnedOn(
        string generationMode)
    {
        // Arrange
        var compilation = Compile(generationMode);

        // Act
        var turnedOn = await Analyze(compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal) { [Generator.RuleIdentifiers.ExtensibleEnumComparedWithOneValue] = ReportDiagnostic.Warn })));
        var byDefault = await Analyze(compilation);

        // Assert - each gate in Usage.cs, naming the comparison and what an unknown value is there
        var reported = turnedOn
            .Where(d => d.Id == Generator.RuleIdentifiers.ExtensibleEnumComparedWithOneValue)
            .Select(d => d.GetMessage(CultureInfo.InvariantCulture))
            .ToList();
        Assert.Equal(3, reported.Count);
        Assert.Contains(reported, r => r.StartsWith("'status != DeviceStatus.Online' compares 'DeviceStatus' with one listed value: a value the specification does not list yet is not equal to it (the condition is true) here.", StringComparison.Ordinal));
        Assert.Contains(reported, r => r.StartsWith("'DeviceStatus.Offline == status'", StringComparison.Ordinal) && r.Contains("(the condition is false)", StringComparison.Ordinal));
        Assert.Contains(reported, r => r.StartsWith("'maybe == DeviceStatus.Online'", StringComparison.Ordinal));
        Assert.All(turnedOn.Where(d => d.Id == Generator.RuleIdentifiers.ExtensibleEnumComparedWithOneValue), d => Assert.Equal("Usage.cs", d.Location.GetLineSpan().Path));

        // Off by default: a comparison is mostly what the code means
        Assert.DoesNotContain(byDefault, d => d.Id == Generator.RuleIdentifiers.ExtensibleEnumComparedWithOneValue);
    }

    private static CSharpCompilation Compile(string generationMode)
    {
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
        return compilation;
    }

    private static Task<ImmutableArray<Diagnostic>> Analyze(
        CSharpCompilation compilation)
        => new Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers(
                compilation,
                [new ExtensibleEnumComparisonAnalyzer()],
                new Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzersOptions(new Microsoft.CodeAnalysis.Diagnostics.AnalyzerOptions([]), onAnalyzerException: null, concurrentAnalysis: false, logAnalyzerExecutionTime: false))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
}