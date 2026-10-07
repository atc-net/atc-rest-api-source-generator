namespace Atc.Rest.Api.SourceGenerator.Tests.Helpers;

/// <summary>
/// Runs code that takes a <see cref="SourceProductionContext"/> - which only Roslyn can make - inside a generator of
/// its own, and gives back what it reported.
/// </summary>
public static class SourceProductionContextRunner
{
    /// <summary>
    /// Runs the action in a source output and returns the diagnostics it reported.
    /// </summary>
    /// <param name="action">The code to run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The reported diagnostics.</returns>
    public static ImmutableArray<Diagnostic> Run(
        Action<SourceProductionContext> action,
        CancellationToken cancellationToken)
    {
        var compilation = CSharpCompilation.Create(
            "Contoso",
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(new ActionGenerator(action));

        var result = driver.RunGenerators(compilation, cancellationToken).GetRunResult();

        // Roslyn turns an exception thrown in a generator into a CS8785 warning: it is the test's failure instead
        if (result.Results.Single().Exception is { } exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        return result.Diagnostics;
    }

    private sealed class ActionGenerator(
        Action<SourceProductionContext> action) : IIncrementalGenerator
    {
        public void Initialize(
            IncrementalGeneratorInitializationContext context)
            => context.RegisterSourceOutput(context.CompilationProvider, (productionContext, _) => action(productionContext));
    }
}