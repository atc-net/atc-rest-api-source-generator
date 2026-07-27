namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Regression tests for the handler name-collision bug: a leftover scaffolded stub under
/// the scaffold namespace (e.g. "...ApiHandlers") must not shadow a hand-written handler of
/// the same name elsewhere — otherwise the endpoint silently resolves to the
/// NotImplementedException stub and returns HTTP 501.
/// </summary>
public class HandlerCollisionReproTests
{
    private const string Yaml = """
        openapi: 3.0.1
        info:
          title: Atc.Api
          version: v1
        paths:
          /github/repository/contributors/{repositoryName}:
            get:
              operationId: getContributorsByRepositoryByName
              parameters:
                - name: repositoryName
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
          /github/repository/contributors/{repositoryName}/activity:
            get:
              operationId: getContributorActivityByRepositoryByName
              parameters:
                - name: repositoryName
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
        """;

    private const string UserHandlers = """
        namespace TestAssembly.Handlers;

        public sealed class GetContributorsByRepositoryByNameHandler { }

        public sealed class GetContributorActivityByRepositoryByNameHandler { }
        """;

    private const string LeftoverStub = """
        namespace TestAssembly.ApiHandlers;

        public sealed class GetContributorActivityByRepositoryByNameHandler { }
        """;

    // Same collision, but both handler and stub implement the generated interface so the
    // interface-based discovery path (Method 1) is exercised, not only the classname path.
    private const string UserHandlersWithInterfaces = """
        namespace TestAssembly.Handlers;

        public interface IGetContributorsByRepositoryByNameHandler { }

        public interface IGetContributorActivityByRepositoryByNameHandler { }

        public sealed class GetContributorsByRepositoryByNameHandler : IGetContributorsByRepositoryByNameHandler { }

        public sealed class GetContributorActivityByRepositoryByNameHandler : IGetContributorActivityByRepositoryByNameHandler { }
        """;

    private const string LeftoverStubWithInterface = """
        namespace TestAssembly.ApiHandlers;

        public sealed class GetContributorActivityByRepositoryByNameHandler : TestAssembly.Handlers.IGetContributorActivityByRepositoryByNameHandler { }
        """;

    [Fact]
    public void CleanBuild_RegistersUserHandler()
    {
        var (registration, _) = Run([UserHandlers]);

        Assert.Contains(
            "global::TestAssembly.Handlers.GetContributorActivityByRepositoryByNameHandler",
            registration,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LeftoverStub_PrefersUserHandler_AndWarns_GEN012()
    {
        var (registration, diagnostics) = Run([UserHandlers, LeftoverStub]);

        // The user's handler must win over the leftover stub (otherwise HTTP 501).
        Assert.Contains(
            "global::TestAssembly.Handlers.GetContributorActivityByRepositoryByNameHandler",
            registration,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "global::TestAssembly.ApiHandlers.GetContributorActivityByRepositoryByNameHandler",
            registration,
            StringComparison.Ordinal);

        // And the shadowing must be surfaced rather than silent.
        Assert.Contains(diagnostics, d => d.Id == "ATC_API_GEN012");
    }

    [Fact]
    public void LeftoverStub_InterfacePath_PrefersUserHandler()
    {
        var (registration, _) = Run([UserHandlersWithInterfaces, LeftoverStubWithInterface]);

        Assert.Contains(
            "global::TestAssembly.Handlers.GetContributorActivityByRepositoryByNameHandler",
            registration,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "global::TestAssembly.ApiHandlers.GetContributorActivityByRepositoryByNameHandler",
            registration,
            StringComparison.Ordinal);
    }

    private static (string Registration, List<Diagnostic> Diagnostics) Run(
        string[] handlerSources)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "atc-handler-repro", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var trees = handlerSources
                .Select((src, i) => CSharpSyntaxTree.ParseText(src, path: Path.Combine(tempDir, "Handlers", $"H{i}.cs")))
                .ToArray();

            var additionalTexts = ImmutableArray.Create<AdditionalText>(
                new InMemoryText(Path.Combine(tempDir, "Atc.Api.yaml"), Yaml),
                new InMemoryText(Path.Combine(tempDir, ".atc-rest-api-server-handlers"), "{}"));

            var asmDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var refs = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(asmDir, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(Path.Combine(asmDir, "netstandard.dll")),
                CreateAspNetCoreStubReference(),
            };

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                syntaxTrees: trees,
                references: refs,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var driver = CSharpGeneratorDriver
                .Create(new ApiServerDomainGenerator())
                .AddAdditionalTexts(additionalTexts);

            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
                compilation, out _, out _, CancellationToken.None);

            var result = driver.GetRunResult();
            var tree = result.GeneratedTrees
                .FirstOrDefault(t => t.FilePath.EndsWith("ApiHandlerDependencyRegistration.g.cs", StringComparison.Ordinal));

            return (
                tree?.GetText().ToString() ?? "(no ApiHandlerDependencyRegistration.g.cs produced)",
                result.Diagnostics.ToList());
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    // The domain generator gates on a referenced assembly whose name starts with
    // "Microsoft.AspNetCore". Compile a trivial assembly with that name in memory to
    // satisfy the gate without dragging the full ASP.NET runtime into the unit test.
    private static MetadataReference CreateAspNetCoreStubReference()
    {
        var stub = CSharpCompilation.Create(
            "Microsoft.AspNetCore.App.Stub",
            new[] { CSharpSyntaxTree.ParseText("namespace Microsoft.AspNetCore { internal static class Marker { } }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emit = stub.Emit(ms);
        if (!emit.Success)
        {
            throw new InvalidOperationException("Failed to emit ASP.NET Core stub reference assembly.");
        }

        return MetadataReference.CreateFromImage(ms.ToArray());
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    private sealed class InMemoryText(string path, string content) : AdditionalText
    {
        private readonly SourceText sourceText = SourceText.From(content, Encoding.UTF8);

        public override string Path { get; } = path;

        public override SourceText GetText(
            CancellationToken cancellationToken = default)
            => sourceText;
    }
}