namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Builds generated code the way a Native AOT project does and returns what stands in its way. The ASP.NET Core
/// generators an AOT project switches on run over it - the Request Delegate Generator (RDG) and the configuration
/// binder, which intercept <c>Map*</c> and <c>Bind*</c> calls - and then the SDK's trim and AOT analyzer
/// (ILLink.RoslynAnalyzer, from Microsoft.NET.ILLink.Tasks) with <c>EnableTrimAnalyzer</c> and
/// <c>EnableAotAnalyzer</c> on, together with the generators' suppressors. What is left: the analyzer's warnings
/// (IL2xxx, IL3xxx) for code nothing can make AOT-safe, and any compile error in the generators' own output.
/// </summary>
/// <remarks>
/// The ASP.NET Core generators only see generated code that is part of the compilation they run on. In a real
/// build that holds for code emitted in the pre-compilation phase (Roslyn 5.9), not for a normal source
/// generator's output - this harness measures the target.
/// </remarks>
internal static class AotAnalyzerHarness
{
    // The namespaces the SDK allows interceptors from when it enables RDG and the configuration binder
    private const string InterceptorsNamespaces =
        "Microsoft.AspNetCore.Http.Generated;Microsoft.Extensions.Configuration.Binder.SourceGeneration";

    private static readonly Lazy<(ImmutableArray<ISourceGenerator> Generators, ImmutableArray<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer> Analyzers)> Tools = new(
        LoadTools,
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Builds the generated sources as an AOT project would and returns one line per problem
    /// (<c>file(line,column): warning IL2026: ...</c>, or a compile error in the ASP.NET Core generators' output).
    /// </summary>
    public static async Task<List<string>> AnalyzeAsync(
        List<(string HintName, string Source)> generatedSources,
        bool includeMinimalApi)
    {
        var withGenerated = WithAspNetCoreGenerators(generatedSources, includeMinimalApi);
        var (_, analyzers) = Tools.Value;

        var errors = withGenerated
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString());

        var options = new Microsoft.CodeAnalysis.Diagnostics.AnalyzerOptions([], new AotOptionsProvider());
        var diagnostics = await new Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers(withGenerated, analyzers, options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        var warnings = diagnostics
            .Where(d => !d.IsSuppressed && d.Id.StartsWith("IL", StringComparison.Ordinal))
            .Select(d => d.ToString());

        return [.. errors.Concat(warnings).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Compiles the generated sources as an AOT project does - RDG intercepting the generated <c>Map*</c> calls - and
    /// loads the assembly, so a test runs the endpoints through RDG's request delegates (an ordinary build binds them
    /// with <c>RequestDelegateFactory</c>). Asserts the build succeeded and RDG intercepted.
    /// </summary>
    public static Assembly EmitAndLoadWithRequestDelegateGenerator(
        List<(string HintName, string Source)> generatedSources,
        bool includeMinimalApi)
    {
        var withGenerated = WithAspNetCoreGenerators(generatedSources, includeMinimalApi);
        Assert.Contains(withGenerated.SyntaxTrees, t => t.ToString().Contains("InterceptsLocation", StringComparison.Ordinal));

        // RDG's request delegates, for working through a binding difference: ATC_RDG_DUMP=<file>
        if (Environment.GetEnvironmentVariable("ATC_RDG_DUMP") is { Length: > 0 } dumpPath)
        {
            File.WriteAllText(dumpPath, string.Join(Environment.NewLine, withGenerated.SyntaxTrees.Where(t => t.FilePath.Contains("RequestDelegateGenerator", StringComparison.Ordinal)).Select(t => t.ToString())));
        }

        using var ms = new MemoryStream();
        var emitResult = withGenerated.Emit(ms, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(
            emitResult.Success,
            "Emit with RDG failed:\n" + string.Join("\n", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).Distinct(StringComparer.Ordinal)));

        return Assembly.Load(ms.ToArray());
    }

    private static Compilation WithAspNetCoreGenerators(
        List<(string HintName, string Source)> generatedSources,
        bool includeMinimalApi)
    {
        var parseOptions = CSharpParseOptions.Default.WithFeatures(
        [
            new KeyValuePair<string, string>("InterceptorsNamespaces", InterceptorsNamespaces),
            new KeyValuePair<string, string>("InterceptorsPreviewNamespaces", InterceptorsNamespaces),
        ]);

        var compilation = CompilationVerificationHarness.CreateCompilation(generatedSources, includeMinimalApi);
        compilation = compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(
            compilation.SyntaxTrees.Select(t => t.WithRootAndOptions(t.GetRoot(TestContext.Current.CancellationToken), parseOptions)));

        CSharpGeneratorDriver
            .Create(Tools.Value.Generators, parseOptions: parseOptions, optionsProvider: new AotOptionsProvider())
            .RunGeneratorsAndUpdateCompilation(compilation, out var withGenerated, out _, TestContext.Current.CancellationToken);

        return withGenerated;
    }

    private static (ImmutableArray<ISourceGenerator> Generators, ImmutableArray<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer> Analyzers) LoadTools()
    {
        var aspNetCoreAnalyzers = AspNetCoreAnalyzersDirectory();
        var generatorAssemblies = new[]
        {
            Path.Combine(aspNetCoreAnalyzers, "Microsoft.AspNetCore.Http.RequestDelegateGenerator.dll"),
            Path.Combine(aspNetCoreAnalyzers, "Microsoft.Extensions.Configuration.Binder.SourceGeneration.dll"),
        }.Select(Assembly.LoadFrom).ToList();
        var trimAnalyzer = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "ILLinkAnalyzer", "ILLink.RoslynAnalyzer.dll"));

        var generators = generatorAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract &&
                        typeof(IIncrementalGenerator).IsAssignableFrom(t) &&
                        t.GetCustomAttributes<GeneratorAttribute>().Any())
            .Select(t => ((IIncrementalGenerator)Activator.CreateInstance(t)!).AsSourceGenerator());

        // The analyzer, and the generators' suppressors (RDG marks the Map* calls it intercepts)
        var analyzers = generatorAssemblies
            .Append(trimAnalyzer)
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract &&
                        typeof(Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer).IsAssignableFrom(t) &&
                        t.GetCustomAttributes<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzerAttribute>()
                            .Any(a => a.Languages.Contains(LanguageNames.CSharp)))
            .Select(t => (Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer)Activator.CreateInstance(t)!);

        return ([.. generators], [.. analyzers]);
    }

    // packs/Microsoft.AspNetCore.App.Ref/<the running framework's version>/analyzers/dotnet/cs of this dotnet
    private static string AspNetCoreAnalyzersDirectory()
    {
        var aspNetCoreShared = Path.GetDirectoryName(typeof(Microsoft.AspNetCore.Http.HttpContext).Assembly.Location)!;
        var version = Path.GetFileName(aspNetCoreShared);
        var dotnetRoot = Path.GetFullPath(Path.Combine(aspNetCoreShared, "..", "..", ".."));
        var directory = Path.Combine(dotnetRoot, "packs", "Microsoft.AspNetCore.App.Ref", version, "analyzers", "dotnet", "cs");

        return Directory.Exists(directory)
            ? directory
            : throw new DirectoryNotFoundException($"The ASP.NET Core targeting pack {version} was not found at {directory}.");
    }

    // The MSBuild properties an AOT project makes visible to the compiler
    private sealed class AotOptionsProvider : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
    {
        private static readonly AotOptions Options = new();

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions
            => Options;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(
            SyntaxTree tree)
            => Options;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(
            AdditionalText textFile)
            => Options;
    }

    private sealed class AotOptions : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
    {
        public override bool TryGetValue(
            string key,
            [NotNullWhen(true)] out string? value)
        {
            value = key is "build_property.EnableTrimAnalyzer"
                or "build_property.EnableAotAnalyzer"
                or "build_property.EnableRequestDelegateGenerator"
                or "build_property.EnableConfigurationBindingGenerator"
                ? "true"
                : null;
            return value is not null;
        }
    }
}