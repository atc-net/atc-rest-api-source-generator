namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Analyzes handler implementations in the Domain project.
/// </summary>
internal static class HandlerAnalyzer
{
    private static readonly Regex HandlerClassPattern = new(
        @"public\s+(?:sealed\s+)?class\s+(\w+Handler)\s*:\s*(I\w+Handler)",
        RegexOptions.Compiled);

    private static readonly Regex HandlerFolderPattern = new(
        @"[/\\]Handlers[/\\](\w+)[/\\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Analyzes handler implementations in the Domain project.
    /// </summary>
    /// <param name="domainProjectPath">Path to the Domain project.</param>
    /// <param name="expectedInterfaces">List of expected handler interfaces from generated code.</param>
    /// <returns>The handler analysis result.</returns>
    public static HandlerAnalysisResult Analyze(string? domainProjectPath, IReadOnlyList<string> expectedInterfaces)
    {
        var result = new HandlerAnalysisResult();

        if (string.IsNullOrEmpty(domainProjectPath))
        {
            return result;
        }

        var domainDir = Path.GetDirectoryName(domainProjectPath);
        if (string.IsNullOrEmpty(domainDir) || !Directory.Exists(domainDir))
        {
            return result;
        }

        try
        {
            // Look for handler files in Handlers folder
            var handlersDir = Path.Combine(domainDir, "Handlers");
            if (Directory.Exists(handlersDir))
            {
                AnalyzeHandlersDirectory(handlersDir, result);
            }
            else
            {
                // Search in the entire Domain project
                AnalyzeHandlersDirectory(domainDir, result);
            }

            // Check compliance with expected interfaces
            var implementedInterfaces = result.Handlers
                .Where(h => !string.IsNullOrEmpty(h.ImplementedInterface))
                .Select(h => h.ImplementedInterface!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingImplementations = expectedInterfaces
                .Where(i => !implementedInterfaces.Contains(i))
                .ToList();

            result.AllHandlersCompliant = missingImplementations.Count == 0;
            result.NonCompliantHandlers = missingImplementations;
        }
        catch
        {
            // Ignore errors during analysis
        }

        return result;
    }

    private static void AnalyzeHandlersDirectory(string directory, HandlerAnalysisResult result)
    {
        try
        {
            var csFiles = Directory.GetFiles(directory, "*Handler.cs", SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                // Skip bin/obj folders
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AnalyzeHandlerFile(file, result);
            }
        }
        catch
        {
            // Ignore errors during analysis
        }
    }

    private static void AnalyzeHandlerFile(string filePath, HandlerAnalysisResult result)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var match = HandlerClassPattern.Match(content);

            if (match.Success)
            {
                var handlerInfo = new HandlerInfo
                {
                    ClassName = match.Groups[1].Value,
                    FilePath = filePath,
                    ImplementedInterface = match.Groups[2].Value,
                    ResourceGroup = ExtractResourceGroup(filePath),
                    HasCustomLogic = HasCustomImplementation(content),
                };

                result.Handlers.Add(handlerInfo);
            }
        }
        catch
        {
            // Ignore errors reading individual files
        }
    }

    private static string? ExtractResourceGroup(string filePath)
    {
        var match = HandlerFolderPattern.Match(filePath);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool HasCustomImplementation(string content)
    {
        // Check if the handler has more than just a stub implementation
        // A stub typically just returns a NotImplementedException or a simple result

        // Look for patterns that indicate custom logic:
        // - Database/repository calls
        // - Service injections used in methods
        // - Complex logic (if statements, loops, etc.)

        var hasServiceUsage = Regex.IsMatch(content, @"_\w+\.\w+\(");
        var hasComplexLogic = Regex.IsMatch(content, @"\b(if|for|foreach|while|switch)\s*\(");
        var hasAwaitCalls = Regex.IsMatch(content, @"\bawait\s+");

        return hasServiceUsage || hasComplexLogic || hasAwaitCalls;
    }
}
