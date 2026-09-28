namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class MigrationPathResolverTests : IDisposable
{
    private readonly string root;
    private readonly string specPath;

    public MigrationPathResolverTests()
    {
        // <root>/src/Specs/api.yaml - the solution file is added by each test.
        root = Path.Combine(Path.GetTempPath(), "atc-migrate-paths", Guid.NewGuid().ToString("N"));
        var specDirectory = Path.Combine(root, "src", "Specs");
        Directory.CreateDirectory(specDirectory);
        specPath = Path.Combine(specDirectory, "api.yaml");
        File.WriteAllText(specPath, "openapi: 3.0.3");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Theory]
    [InlineData("MyApi.sln")]
    [InlineData("MyApi.slnx")]
    public void Resolve_SpecificationOnly_FindsTheSolutionAboveTheSpecification(
        string solutionFileName)
    {
        File.WriteAllText(Path.Combine(root, solutionFileName), string.Empty);

        var (error, specification, solution, notes) = Resolve(specPath, legacySpecification: null, solution: string.Empty);

        Assert.Null(error);
        Assert.Equal(specPath, specification);
        Assert.Equal(Path.GetFullPath(root), solution);
        Assert.Contains(notes, note => note.Contains(Path.GetFullPath(root), StringComparison.Ordinal));
    }

    [Fact]
    public void Resolve_SpecificationOnly_UsesTheNearestSolution()
    {
        File.WriteAllText(Path.Combine(root, "Outer.sln"), string.Empty);
        var inner = Path.Combine(root, "src");
        File.WriteAllText(Path.Combine(inner, "Inner.slnx"), string.Empty);

        var (error, _, solution, _) = Resolve(specPath, legacySpecification: null, solution: string.Empty);

        Assert.Null(error);
        Assert.Equal(Path.GetFullPath(inner), solution);
    }

    [Theory]
    [InlineData("MyApi.sln")]
    [InlineData("MyApi.slnx")]
    public void Resolve_ExplicitSolution_IsKept(string solutionFileName)
    {
        var solutionFile = Path.Combine(root, solutionFileName);
        File.WriteAllText(solutionFile, string.Empty);

        var (error, specification, solution, notes) = Resolve(specPath, legacySpecification: null, solutionFile);

        Assert.Null(error);
        Assert.Equal(specPath, specification);
        Assert.Equal(solutionFile, solution);
        Assert.Empty(notes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_OldSpelling_SolutionWithSAndSpecificationWithP_StillWorks(
        bool solutionAsDirectory)
    {
        var solutionFile = Path.Combine(root, "MyApi.sln");
        File.WriteAllText(solutionFile, string.Empty);
        var oldSolutionArgument = solutionAsDirectory ? root : solutionFile;

        var (error, specification, solution, notes) = Resolve(oldSolutionArgument, specPath, solution: string.Empty);

        Assert.Null(error);
        Assert.Equal(specPath, specification);
        Assert.Equal(oldSolutionArgument, solution);
        Assert.Contains(notes, note => note.Contains("--solution", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolve_SpecificationGivenTwice_IsAnError()
    {
        var (error, _, _, _) = Resolve(specPath, specPath, solution: string.Empty);

        Assert.NotNull(error);
        Assert.Contains("given twice", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_NoSpecification_IsAnError()
    {
        var (error, _, _, _) = Resolve(string.Empty, legacySpecification: null, solution: string.Empty);

        Assert.NotNull(error);
        Assert.Contains("-s or --specification", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_SolutionThatIsNotASolutionFile_NamesTheFile()
    {
        var otherPath = Path.Combine(root, "readme.txt");
        File.WriteAllText(otherPath, string.Empty);

        var (error, _, _, _) = Resolve(specPath, legacySpecification: null, otherPath);

        Assert.NotNull(error);
        Assert.Contains("must be a .sln or .slnx file", error, StringComparison.Ordinal);
        Assert.Contains(otherPath, error, StringComparison.Ordinal);
    }

    [Fact]
    public void FindSolutionDirectory_FindsTheStartDirectoryItself()
    {
        File.WriteAllText(Path.Combine(root, "MyApi.slnx"), string.Empty);

        Assert.Equal(Path.GetFullPath(root), MigrationPathResolver.FindSolutionDirectory(root));
    }

    [Fact]
    public void FindSolutionDirectory_IgnoresOtherFiles()
    {
        File.WriteAllText(Path.Combine(root, "MyApi.slnf"), string.Empty);
        File.WriteAllText(Path.Combine(root, "MyApi.csproj"), string.Empty);

        var found = MigrationPathResolver.FindSolutionDirectory(root);

        // Nothing in the test tree; anything found lies above it (outside the test's control).
        Assert.True(found is null || !found.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
    }

    private static (string? Error, string Specification, string Solution, List<string> Notes) Resolve(
        string specification,
        string? legacySpecification,
        string solution)
    {
        var notes = new List<string>();
        var error = MigrationPathResolver.Resolve(ref specification, legacySpecification, ref solution, notes);
        return (error, specification, solution, notes);
    }
}