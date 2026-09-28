namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Resolves the specification and solution paths of the <c>migrate</c> commands.
/// </summary>
/// <remarks>
/// Like every other command, <c>migrate</c> takes the specification with <c>-s|--specification</c>. The solution
/// (<c>--solution</c>) is optional: without it, the nearest folder with a .sln/.slnx is used, searched from the
/// specification's folder upwards and then from the current directory upwards.
/// <para>
/// Earlier versions used <c>-s|--solution</c> and <c>-p|--spec</c>. Both spellings keep working: <c>-p|--spec</c>
/// is a hidden alias of the specification, and a <c>-s</c> that points at a .sln/.slnx file or a directory is taken
/// as the solution.
/// </para>
/// </remarks>
internal static class MigrationPathResolver
{
    private static readonly string[] SpecificationExtensions = [".yaml", ".yml", ".json"];

    /// <summary>
    /// Resolves the paths, returning an error message when they cannot be resolved.
    /// </summary>
    /// <param name="specificationPath">The <c>-s|--specification</c> value; replaced by the resolved specification path.</param>
    /// <param name="legacySpecificationPath">The <c>-p|--spec</c> value (the pre-2.1.1 spelling).</param>
    /// <param name="solutionPath">The <c>--solution</c> value; replaced by the resolved solution path.</param>
    /// <param name="notes">Notes for the user about how the paths were resolved.</param>
    /// <returns>The error message, or <see langword="null"/> when the paths are valid.</returns>
    public static string? Resolve(
        ref string specificationPath,
        string? legacySpecificationPath,
        ref string solutionPath,
        IList<string> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        // The pre-2.1.1 spelling: -s <solution or directory> -p <specification>.
        if (!string.IsNullOrWhiteSpace(specificationPath) &&
            string.IsNullOrWhiteSpace(solutionPath) &&
            IsSolutionLocation(PathHelper.ResolveRelativePath(specificationPath)))
        {
            solutionPath = specificationPath;
            specificationPath = string.Empty;
            notes.Add("-s is now --specification; pass the solution with --solution.");
        }

        if (!string.IsNullOrWhiteSpace(legacySpecificationPath))
        {
            if (!string.IsNullOrWhiteSpace(specificationPath))
            {
                return "The specification is given twice: use -s/--specification (-p/--spec is its old spelling).";
            }

            specificationPath = legacySpecificationPath;
        }

        if (string.IsNullOrWhiteSpace(specificationPath))
        {
            return "Specification path is required. Use -s or --specification.";
        }

        specificationPath = PathHelper.ResolveRelativePath(specificationPath);

        if (!File.Exists(specificationPath))
        {
            return $"Specification file not found: {specificationPath}";
        }

        if (!IsSpecificationFile(specificationPath))
        {
            return $"Specification file must be a YAML (.yaml, .yml) or JSON (.json) file: {specificationPath}";
        }

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            var specificationDirectory = Path.GetDirectoryName(Path.GetFullPath(specificationPath));
            var found = FindSolutionDirectory(specificationDirectory) ??
                        FindSolutionDirectory(Directory.GetCurrentDirectory());
            if (found is null)
            {
                return $"No .sln or .slnx file was found in {specificationDirectory} or its parent folders, " +
                       "or in the current directory or its parent folders. Use --solution to point at it.";
            }

            solutionPath = found;
            notes.Add($"Found the solution in {found}.");
            return null;
        }

        solutionPath = PathHelper.ResolveRelativePath(solutionPath);

        if (File.Exists(solutionPath))
        {
            if (!IsSolutionFile(solutionPath))
            {
                return $"Solution file must be a .sln or .slnx file (or the directory that contains it): {solutionPath}";
            }
        }
        else if (!Directory.Exists(solutionPath))
        {
            return $"Solution path not found: {solutionPath}";
        }

        return null;
    }

    /// <summary>
    /// Finds the nearest directory, from <paramref name="startDirectory"/> upwards, that contains a .sln or .slnx file.
    /// </summary>
    /// <param name="startDirectory">The directory to start in.</param>
    /// <returns>The directory, or <see langword="null"/> when none is found.</returns>
    public static string? FindSolutionDirectory(string? startDirectory)
    {
        for (var directory = startDirectory is null ? null : new DirectoryInfo(startDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (directory.Exists &&
                directory.EnumerateFiles().Any(file => IsSolutionFile(file.Name)))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    private static bool IsSolutionLocation(string path)
        => Directory.Exists(path) || (File.Exists(path) && IsSolutionFile(path));

    private static bool IsSolutionFile(string path)
        => Path.GetExtension(path).ToLowerInvariant() is ".sln" or ".slnx";

    private static bool IsSpecificationFile(string path)
        => SpecificationExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}