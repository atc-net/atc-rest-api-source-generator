namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Regression tests for the duplicated <c>ProblemDetails</c> generation.
/// <para>
/// In EndpointPerOperation mode the generator emits its own <c>ProblemDetails</c> /
/// <c>ValidationProblemDetails</c> into <c>{project}.Generated</c>. Previously a same-named schema in
/// the specification was ALSO materialized into <c>{project}.Generated.{Segment}.Models</c>. That did
/// not break the build - C# resolves an unqualified name against enclosing namespaces before using
/// directives, so consumers under <c>{project}.Generated.*</c> always bound to the built-in - which
/// meant the user's schema was silently emitted as dead code.
/// </para>
/// <para>
/// The spec-defined schema is now dropped and reported via ATC_API_SCH021 instead.
/// </para>
/// </summary>
public class ProblemDetailsDuplicationTests
{
    private const string ScenarioName = "InlineSchemas";
    private const string YamlFileName = "InlineSchemas.yaml";
    private const string ShadowedRuleId = "ATC_API_SCH021";

    [Fact]
    public void SpecDefinedProblemDetails_IsNotGenerated()
    {
        // Arrange & Act
        var (_, generatedSources) = RunGenerator();

        var namespaces = generatedSources
            .Where(s => s.HintName.EndsWith("ProblemDetails.g.cs", StringComparison.Ordinal))
            .Select(s => GetNamespace(s.Source))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // Assert - only the built-in remains; the spec-derived duplicate is gone.
        Assert.Equal(["InlineSchemas.Generated"], namespaces);
    }

    [Fact]
    public void SpecDefinedProblemDetails_ReportsSCH021()
    {
        // Arrange & Act
        var (diagnostics, _) = RunGenerator();

        var shadowed = diagnostics
            .Where(d => d.Id.Equals(ShadowedRuleId, StringComparison.Ordinal))
            .ToList();

        // Assert - the user is told their schema is ignored rather than it vanishing silently.
        var diagnostic = Assert.Single(shadowed);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("ProblemDetails", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedSources_HaveNoAmbiguousProblemDetailsImports()
    {
        // Arrange & Act
        var (_, generatedSources) = RunGenerator();

        // A file may legitimately import both 'InlineSchemas.Generated' and a segment Models
        // namespace, because segments can own inline (anonymous) schema models. Ambiguity arises
        // only if more than one imported namespace also declares a ProblemDetails type, so assert
        // on the declaring namespaces rather than on the presence of usings.
        var declaringNamespaces = generatedSources
            .Where(s => s.HintName.EndsWith("ProblemDetails.g.cs", StringComparison.Ordinal))
            .Select(s => GetNamespace(s.Source))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Assert - exactly one namespace declares ProblemDetails, so no using combination can
        // produce CS0104. The old layout only avoided it by accident of namespace nesting.
        Assert.Equal(["InlineSchemas.Generated"], declaringNamespaces);
    }

    [Fact]
    public void ProblemDetailsReferences_BindToBuiltIn()
    {
        // Arrange
        var (_, generatedSources) = RunGenerator();

        // Act
        var symbol = ResolveProblemDetailsIn(
            generatedSources,
            "InlineSchemas.Generated.Reports.Endpoints.Interfaces.IGetReportEndpointResult.g.cs");

        // Assert
        Assert.Equal("InlineSchemas.Generated.ProblemDetails", symbol);
    }

    private static string ResolveProblemDetailsIn(
        List<(string HintName, string Source)> generatedSources,
        string hintNameSuffix)
    {
        var trees = generatedSources
            .Select(s => CSharpSyntaxTree.ParseText(
                SourceText.From(s.Source, Encoding.UTF8),
                path: s.HintName,
                cancellationToken: TestContext.Current.CancellationToken))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "ProblemDetailsBindingTest",
            trees,
            CompilationVerificationHarness.GetFullFrameworkReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var tree = trees.First(t => t.FilePath.EndsWith(hintNameSuffix, StringComparison.Ordinal));
        var model = compilation.GetSemanticModel(tree);

        var node = tree
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax>()
            .First(n => n.Identifier.Text == "ProblemDetails");

        var symbol = model.GetSymbolInfo(node, TestContext.Current.CancellationToken).Symbol;

        Assert.NotNull(symbol);

        return symbol.ToDisplayString();
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, List<(string HintName, string Source)> GeneratedSources) RunGenerator()
        => CompilationVerificationHarness.RunGenerator(
            new ApiClientGenerator(),
            ScenarioName,
            YamlFileName,
            ".atc-rest-api-client",
            "Client-Operation",
            useFullReferences: true);

    private static string GetNamespace(string source)
    {
        var line = source
            .Split('\n')
            .First(l => l.TrimStart().StartsWith("namespace ", StringComparison.Ordinal));

        return line.Trim()["namespace ".Length..].TrimEnd(';');
    }
}