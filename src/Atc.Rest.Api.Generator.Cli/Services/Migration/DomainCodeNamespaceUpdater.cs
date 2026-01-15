namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Updates namespace references in domain code files from old CLI pattern to new source generator pattern.
/// </summary>
internal static class DomainCodeNamespaceUpdater
{
    // Pattern to match: Api.Generated.Contracts.{Segment}.{Type}
    // Where {Segment} is PascalCase (e.g., Accounts) and {Type} is a type name
    private static readonly Regex OldNamespacePattern = new(
        @"Api\.Generated\.Contracts\.(?<segment>[A-Z][a-zA-Z0-9]*)\.(?<type>[A-Z][a-zA-Z0-9]*)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Updates namespace references in C# files from old CLI pattern to new source generator pattern.
    /// Pattern: Api.Generated.Contracts.{Segment}.{Type} → {ProjectName}.Api.Contracts.Generated.{Segment}.Models.{Type}
    /// </summary>
    /// <param name="domainProjectDirectory">The directory containing the Domain project.</param>
    /// <param name="projectName">The base project name (e.g., "KL.IoT.D365").</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The result of the update operation.</returns>
    public static DomainCodeUpdateResult UpdateNamespaceReferences(
        string domainProjectDirectory,
        string projectName,
        bool dryRun = false)
    {
        var result = new DomainCodeUpdateResult();

        if (!Directory.Exists(domainProjectDirectory))
        {
            result.Error = $"Domain project directory not found: {domainProjectDirectory}";
            return result;
        }

        try
        {
            var csFiles = Directory.GetFiles(domainProjectDirectory, "*.cs", SearchOption.AllDirectories);

            foreach (var filePath in csFiles)
            {
                var fileUpdate = ProcessFile(filePath, projectName, dryRun);
                if (fileUpdate != null && fileUpdate.Replacements.Count > 0)
                {
                    result.UpdatedFiles.Add(fileUpdate);
                    result.TotalReplacements += fileUpdate.Replacements.Count;
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = $"Failed to update domain code namespaces: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Processes a single C# file and updates namespace references.
    /// </summary>
    private static DomainFileUpdate? ProcessFile(
        string filePath,
        string projectName,
        bool dryRun)
    {
        var content = File.ReadAllText(filePath);
        var fileUpdate = new DomainFileUpdate { FilePath = filePath };
        var replacements = new HashSet<string>(StringComparer.Ordinal);

        // Find all matches and collect unique replacements
        var matches = OldNamespacePattern.Matches(content);
        foreach (Match match in matches)
        {
            var segment = match.Groups["segment"].Value;
            var type = match.Groups["type"].Value;

            var oldNamespace = $"Api.Generated.Contracts.{segment}.{type}";
            var newNamespace = $"{projectName}.Api.Contracts.Generated.{segment}.Models.{type}";

            if (replacements.Add($"{oldNamespace} → {newNamespace}"))
            {
                fileUpdate.Replacements.Add($"{oldNamespace} → {newNamespace}");
            }
        }

        if (fileUpdate.Replacements.Count == 0)
        {
            return null;
        }

        // Perform the actual replacement
        var newContent = OldNamespacePattern.Replace(
            content,
            match =>
            {
                var segment = match.Groups["segment"].Value;
                var type = match.Groups["type"].Value;
                return $"{projectName}.Api.Contracts.Generated.{segment}.Models.{type}";
            });

        if (!dryRun && newContent != content)
        {
            File.WriteAllText(filePath, newContent);
        }

        return fileUpdate;
    }
}