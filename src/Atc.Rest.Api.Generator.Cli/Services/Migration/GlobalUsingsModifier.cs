namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Modifies GlobalUsings.cs files during migration to update namespace references.
/// </summary>
internal static class GlobalUsingsModifier
{
    /// <summary>
    /// Updates namespace references in GlobalUsings.cs from old Api.Generated to new Api.Contracts.Generated.
    /// </summary>
    /// <param name="domainProjectDirectory">The directory containing the Domain project.</param>
    /// <param name="projectName">The base project name (e.g., "MyProject").</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The result of the modification.</returns>
    public static GlobalUsingsModificationResult UpdateNamespaces(
        string domainProjectDirectory,
        string projectName,
        bool dryRun = false)
    {
        var globalUsingsPath = Path.Combine(domainProjectDirectory, "GlobalUsings.cs");
        var result = new GlobalUsingsModificationResult { FilePath = globalUsingsPath };

        if (!File.Exists(globalUsingsPath))
        {
            // GlobalUsings.cs doesn't exist - not an error, just nothing to do
            return result;
        }

        try
        {
            var lines = File.ReadAllLines(globalUsingsPath);
            var modified = false;
            var newLines = new List<string>();

            // Pattern for transformation: {projectName}.Api.Generated.* -> {projectName}.Api.Contracts.Generated.*
            // But the BASE namespace {projectName}.Api.Generated (without suffix) should be REMOVED
            var oldNamespaceBase = $"{projectName}.Api.Generated";
            var oldNamespaceWithDot = $"{projectName}.Api.Generated.";
            var newNamespaceWithDot = $"{projectName}.Api.Contracts.Generated.";

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Check if this is the exact base namespace (should be removed, not transformed)
                if (trimmedLine.Equals($"global using {oldNamespaceBase};", StringComparison.Ordinal))
                {
                    // Remove this line - the base namespace is not valid in new pattern
                    result.RemovedUsings.Add(oldNamespaceBase);
                    modified = true;
                    continue;
                }

                // Transform sub-namespaces: Api.Generated.Something -> Api.Contracts.Generated.Something
                if (line.Contains(oldNamespaceWithDot, StringComparison.Ordinal))
                {
                    var newLine = line.Replace(oldNamespaceWithDot, newNamespaceWithDot, StringComparison.Ordinal);
                    newLines.Add(newLine);
                    result.UpdatedUsings.Add($"{ExtractUsingNamespace(line)} → {ExtractUsingNamespace(newLine)}");
                    modified = true;
                }
                else
                {
                    newLines.Add(line);
                }
            }

            if (modified)
            {
                result.WasModified = true;

                if (!dryRun)
                {
                    WriteAllLinesWithoutTrailingNewline(globalUsingsPath, newLines);
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = $"Failed to update GlobalUsings.cs: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Removes old CLI-generated namespace usings that follow the old pattern.
    /// Old pattern: *.Generated.Contracts.* or *.Generated.Endpoints.*
    /// These are replaced by the new source generator patterns.
    /// </summary>
    /// <param name="domainProjectDirectory">The directory containing the Domain project.</param>
    /// <param name="contractsNamespace">The namespace for the contracts project (e.g., "MyProject.Api.Contracts").</param>
    /// <param name="dryRun">If true, only returns what would be removed.</param>
    /// <returns>The result of the modification.</returns>
    public static GlobalUsingsModificationResult RemoveOldGeneratedUsings(
        string domainProjectDirectory,
        string contractsNamespace,
        bool dryRun = false)
    {
        var globalUsingsPath = Path.Combine(domainProjectDirectory, "GlobalUsings.cs");
        var result = new GlobalUsingsModificationResult { FilePath = globalUsingsPath };

        if (!File.Exists(globalUsingsPath))
        {
            return result;
        }

        try
        {
            var lines = File.ReadAllLines(globalUsingsPath);
            var newLines = new List<string>();

            // Old CLI patterns to detect and remove:
            // Post-migration patterns (after renaming to Api.Contracts):
            // - *.Api.Contracts.Generated.Contracts.* (old CLI generated contracts)
            // - *.Api.Contracts.Generated.Endpoints.* (old CLI generated endpoints)
            // - *.Api.Contracts.Generated.Resources.* (old CLI generated resources)
            // Pre-migration patterns (before renaming):
            // - *.Api.Generated.Contracts.* (old CLI generated contracts)
            // - *.Api.Generated.Endpoints.* (old CLI generated endpoints)
            // - *.Api.Generated.Resources.* (old CLI generated resources)
            var projectBaseName = contractsNamespace.Replace(".Api.Contracts", string.Empty, StringComparison.Ordinal);
            var oldPatterns = new[]
            {
                // Post-migration patterns
                $"{contractsNamespace}.Generated.Contracts.",
                $"{contractsNamespace}.Generated.Endpoints.",
                $"{contractsNamespace}.Generated.Resources.",

                // Pre-migration patterns
                $"{projectBaseName}.Api.Generated.Contracts.",
                $"{projectBaseName}.Api.Generated.Endpoints.",
                $"{projectBaseName}.Api.Generated.Resources.",
            };

            foreach (var line in lines)
            {
                var shouldRemove = false;
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("global using ", StringComparison.Ordinal))
                {
                    foreach (var pattern in oldPatterns)
                    {
                        if (trimmedLine.Contains(pattern, StringComparison.Ordinal))
                        {
                            shouldRemove = true;
                            result.RemovedUsings.Add(ExtractUsingNamespace(trimmedLine));
                            break;
                        }
                    }
                }

                if (!shouldRemove)
                {
                    newLines.Add(line);
                }
            }

            if (result.RemovedUsings.Count > 0)
            {
                result.WasModified = true;

                if (!dryRun)
                {
                    WriteAllLinesWithoutTrailingNewline(globalUsingsPath, newLines);
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = $"Failed to remove old generated usings: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Adds required generated namespace usings to GlobalUsings.cs based on the OpenAPI spec paths.
    /// Uses proper OpenAPI parsing and PathSegmentHelper to determine exactly which namespaces exist.
    /// </summary>
    /// <param name="domainProjectDirectory">The directory containing the Domain project.</param>
    /// <param name="specFilePath">The path to the OpenAPI specification file.</param>
    /// <param name="contractsNamespace">The namespace for the contracts project (e.g., "MyProject.Api.Contracts").</param>
    /// <param name="dryRun">If true, only returns what would be added.</param>
    /// <returns>The result of the modification.</returns>
    public static GlobalUsingsModificationResult AddGeneratedNamespaceUsings(
        string domainProjectDirectory,
        string specFilePath,
        string contractsNamespace,
        bool dryRun = false)
    {
        var globalUsingsPath = Path.Combine(domainProjectDirectory, "GlobalUsings.cs");
        var result = new GlobalUsingsModificationResult { FilePath = globalUsingsPath };

        if (!File.Exists(globalUsingsPath))
        {
            result.Error = "GlobalUsings.cs not found in Domain project";
            return result;
        }

        if (!File.Exists(specFilePath))
        {
            result.Error = $"OpenAPI spec file not found: {specFilePath}";
            return result;
        }

        try
        {
            var openApiDoc = ParseOpenApiDocument(specFilePath);
            if (openApiDoc == null)
            {
                result.Error = "Failed to parse OpenAPI specification";
                return result;
            }

            var pathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);
            if (pathSegments.Count == 0)
            {
                return result;
            }

            var existingContent = File.ReadAllText(globalUsingsPath);
            var existingLines = File.ReadAllLines(globalUsingsPath).ToList();
            var usingsToAdd = new List<string>();

            foreach (var segment in pathSegments)
            {
                var namespaces = PathSegmentHelper.GetPathSegmentNamespaces(openApiDoc, segment);
                var segmentUsings = PathSegmentHelper.GetSegmentUsings(
                    contractsNamespace,
                    segment,
                    namespaces,
                    includeHandlers: true,
                    includeModels: true,
                    isGlobalUsing: true);

                foreach (var usingStatement in segmentUsings)
                {
                    var fullNamespace = ExtractUsingNamespace(usingStatement);
                    if (!existingContent.Contains(fullNamespace, StringComparison.Ordinal))
                    {
                        usingsToAdd.Add(usingStatement);
                        result.AddedUsings.Add(fullNamespace);
                    }
                }
            }

            if (usingsToAdd.Count > 0)
            {
                result.WasModified = true;

                if (!dryRun)
                {
                    var insertIndex = FindInsertIndex(existingLines, contractsNamespace);
                    existingLines.InsertRange(insertIndex, usingsToAdd);
                    WriteAllLinesWithoutTrailingNewline(globalUsingsPath, existingLines);
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = $"Failed to add generated namespace usings: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Parses an OpenAPI document from a file path.
    /// </summary>
    /// <param name="specFilePath">The path to the OpenAPI specification file.</param>
    /// <returns>The parsed OpenAPI document, or null if parsing failed.</returns>
    private static OpenApiDocument? ParseOpenApiDocument(string specFilePath)
    {
        var content = File.ReadAllText(specFilePath);

        if (OpenApiDocumentHelper.TryParseYaml(content, specFilePath, out var document))
        {
            return document;
        }

        return null;
    }

    /// <summary>
    /// Finds the best index to insert new usings (after existing project-related usings).
    /// </summary>
    /// <param name="lines">The existing file lines.</param>
    /// <param name="contractsNamespace">The contracts namespace to look for.</param>
    /// <returns>The index where new usings should be inserted.</returns>
    private static int FindInsertIndex(
        List<string> lines,
        string contractsNamespace)
    {
        var lastContractsIndex = -1;
        var lastGlobalUsingIndex = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.StartsWith("global using ", StringComparison.Ordinal))
            {
                lastGlobalUsingIndex = i;
                if (line.Contains(contractsNamespace, StringComparison.Ordinal))
                {
                    lastContractsIndex = i;
                }
            }
        }

        if (lastContractsIndex >= 0)
        {
            return lastContractsIndex + 1;
        }

        return lastGlobalUsingIndex >= 0 ? lastGlobalUsingIndex + 1 : lines.Count;
    }

    /// <summary>
    /// Removes old namespace patterns and sorts all global usings using GlobalUsingsHelper.
    /// System namespaces are placed first, followed by other namespaces grouped by prefix.
    /// </summary>
    /// <param name="projectDirectory">The directory containing the project.</param>
    /// <param name="projectName">The base project name (e.g., "MyProject").</param>
    /// <param name="requiredNamespaces">Optional list of namespaces that must be present.</param>
    /// <param name="removeNamespaceGroupSeparator">If true, removes blank lines between namespace groups.</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The result of the modification.</returns>
    public static GlobalUsingsModificationResult CleanupAndSortGlobalUsings(
        string projectDirectory,
        string projectName,
        IReadOnlyList<string>? requiredNamespaces = null,
        bool removeNamespaceGroupSeparator = false,
        bool dryRun = false)
    {
        var globalUsingsPath = Path.Combine(projectDirectory, "GlobalUsings.cs");
        var result = new GlobalUsingsModificationResult { FilePath = globalUsingsPath };

        var directoryInfo = new DirectoryInfo(projectDirectory);

        try
        {
            // Patterns to remove:
            // 1. {ProjectName}.Api.Generated (exact match - old base namespace)
            // 2. {ProjectName}.Api.Generated.* (any sub-namespace of old pattern)
            // Note: Atc.Rest.MinimalApi.* namespaces are NOT removed as they contain
            // runtime functionality needed by the Host project (e.g., UseGlobalErrorHandler,
            // ValidationFilterOptions, ApiVersionConstants).
            var oldApiGeneratedExact = $"{projectName}.Api.Generated";
            var oldApiGeneratedPrefix = $"{projectName}.Api.Generated.";

            // Read existing namespaces and filter out old patterns
            var existingNamespaces = ReadExistingNamespaces(globalUsingsPath);
            var removedUsings = new List<string>();
            var filteredNamespaces = new List<string>();

            foreach (var ns in existingNamespaces)
            {
                if (ns.Equals(oldApiGeneratedExact, StringComparison.Ordinal) ||
                    ns.StartsWith(oldApiGeneratedPrefix, StringComparison.Ordinal))
                {
                    removedUsings.Add(ns);
                }
                else
                {
                    filteredNamespaces.Add(ns);
                }
            }

            // Merge with required namespaces
            var allNamespaces = requiredNamespaces != null
                ? filteredNamespaces.Union(requiredNamespaces, StringComparer.Ordinal).ToList()
                : filteredNamespaces;

            // Track added namespaces
            if (requiredNamespaces != null)
            {
                foreach (var ns in requiredNamespaces)
                {
                    if (!existingNamespaces.Contains(ns, StringComparer.Ordinal))
                    {
                        result.AddedUsings.Add(ns);
                    }
                }
            }

            // Get the new content to compare
            var newContent = GlobalUsingsHelper.GetMergedContent(
                directoryInfo,
                allNamespaces,
                setSystemFirst: true,
                addNamespaceSeparator: !removeNamespaceGroupSeparator);

            // Read current content to compare
            var currentContent = File.Exists(globalUsingsPath)
                ? File.ReadAllText(globalUsingsPath)
                : string.Empty;

            var contentChanged = !string.Equals(currentContent, newContent, StringComparison.Ordinal);

            if (removedUsings.Count > 0 || contentChanged || result.AddedUsings.Count > 0)
            {
                result.WasModified = true;
                result.RemovedUsings.AddRange(removedUsings);

                if (contentChanged && removedUsings.Count == 0 && result.AddedUsings.Count == 0)
                {
                    result.UpdatedUsings.Add("Sorted usings (System first, then by namespace group)");
                }

                if (!dryRun)
                {
                    // Pre-clean the file by writing only filtered namespaces.
                    // This removes .Generated lines BEFORE GlobalUsingsHelper reads the file,
                    // preventing the helper from merging the removed namespaces back in.
                    var linesToWrite = filteredNamespaces
                        .Select(ns => $"global using {ns};")
                        .ToList();
                    WriteAllLinesWithoutTrailingNewline(globalUsingsPath, linesToWrite);

                    // Now GlobalUsingsHelper will merge our required namespaces
                    // with the pre-cleaned file (no more .Generated lines)
                    GlobalUsingsHelper.CreateOrUpdate(
                        directoryInfo,
                        allNamespaces,
                        setSystemFirst: true,
                        addNamespaceSeparator: !removeNamespaceGroupSeparator);
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = $"Failed to cleanup GlobalUsings.cs: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Reads existing namespaces from a GlobalUsings.cs file.
    /// </summary>
    private static List<string> ReadExistingNamespaces(string globalUsingsPath)
    {
        var namespaces = new List<string>();

        if (!File.Exists(globalUsingsPath))
        {
            return namespaces;
        }

        var lines = File.ReadAllLines(globalUsingsPath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("global using ", StringComparison.Ordinal))
            {
                var ns = ExtractUsingNamespace(trimmed);
                if (!string.IsNullOrEmpty(ns))
                {
                    namespaces.Add(ns);
                }
            }
        }

        return namespaces;
    }

    /// <summary>
    /// Sorts global usings using GlobalUsingsHelper (System first, then by namespace group).
    /// </summary>
    /// <param name="projectDirectory">The directory containing the project.</param>
    /// <param name="removeNamespaceGroupSeparator">If true, removes blank lines between namespace groups.</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The result of the modification.</returns>
    public static GlobalUsingsModificationResult SortGlobalUsings(
        string projectDirectory,
        bool removeNamespaceGroupSeparator = false,
        bool dryRun = false)
    {
        var globalUsingsPath = Path.Combine(projectDirectory, "GlobalUsings.cs");
        var result = new GlobalUsingsModificationResult { FilePath = globalUsingsPath };

        if (!File.Exists(globalUsingsPath))
        {
            return result;
        }

        var directoryInfo = new DirectoryInfo(projectDirectory);

        try
        {
            var existingNamespaces = ReadExistingNamespaces(globalUsingsPath);

            if (existingNamespaces.Count == 0)
            {
                return result;
            }

            if (!dryRun)
            {
                GlobalUsingsHelper.CreateOrUpdate(
                    directoryInfo,
                    existingNamespaces,
                    setSystemFirst: true,
                    addNamespaceSeparator: !removeNamespaceGroupSeparator);
            }

            result.WasModified = true;
            result.UpdatedUsings.Add("Sorted usings (System first, then by namespace group)");
        }
        catch (Exception ex)
        {
            result.Error = $"Failed to sort GlobalUsings.cs: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Extracts the namespace from a global using statement.
    /// </summary>
    /// <param name="line">The line containing the global using statement.</param>
    /// <returns>The extracted namespace or the original line if not a using statement.</returns>
    private static string ExtractUsingNamespace(string line)
    {
        // "global using Some.Namespace;" -> "Some.Namespace"
        var trimmed = line.Trim();
        if (trimmed.StartsWith("global using ", StringComparison.Ordinal) && trimmed.EndsWith(';'))
        {
            return trimmed["global using ".Length..^1];
        }

        return trimmed;
    }

    /// <summary>
    /// Writes lines to a file without a trailing newline to avoid SA1518 warning.
    /// </summary>
    private static void WriteAllLinesWithoutTrailingNewline(
        string path,
        IEnumerable<string> lines)
    {
        var content = string.Join(Environment.NewLine, lines);
        File.WriteAllText(path, content);
    }
}