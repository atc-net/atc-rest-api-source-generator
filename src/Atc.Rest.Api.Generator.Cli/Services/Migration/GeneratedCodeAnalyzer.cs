namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Analyzes generated code in old-generated projects.
/// </summary>
internal static class GeneratedCodeAnalyzer
{
    private static readonly Regex GeneratedCodePattern = new(
        @"\[GeneratedCode\(""ApiGenerator"",\s*""([^""]+)""\)\]",
        RegexOptions.Compiled);

    private static readonly Regex NamespacePattern = new(
        @"namespace\s+([\w.]+)",
        RegexOptions.Compiled);

    private static readonly Regex InterfacePattern = new(
        @"public\s+interface\s+(I\w+Handler)\b",
        RegexOptions.Compiled);

    private static readonly Regex EndpointDefinitionPattern = new(
        @"public\s+(?:sealed\s+)?class\s+(\w+EndpointDefinition)\b",
        RegexOptions.Compiled);

    private static readonly Regex ModelPattern = new(
        @"public\s+(?:sealed\s+)?(?:record|class)\s+(\w+)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Analyzes generated code in the specified projects.
    /// </summary>
    /// <param name="apiGeneratedProjectPath">Path to the Api.Generated project.</param>
    /// <param name="apiClientGeneratedProjectPath">Path to the ApiClient.Generated project (optional).</param>
    /// <returns>The generated code analysis result.</returns>
    public static GeneratedCodeResult Analyze(string? apiGeneratedProjectPath, string? apiClientGeneratedProjectPath)
    {
        var result = new GeneratedCodeResult();

        if (!string.IsNullOrEmpty(apiGeneratedProjectPath))
        {
            var serverDir = Path.GetDirectoryName(apiGeneratedProjectPath);
            if (!string.IsNullOrEmpty(serverDir) && Directory.Exists(serverDir))
            {
                AnalyzeDirectory(serverDir, result, isServer: true);
            }
        }

        if (!string.IsNullOrEmpty(apiClientGeneratedProjectPath))
        {
            var clientDir = Path.GetDirectoryName(apiClientGeneratedProjectPath);
            if (!string.IsNullOrEmpty(clientDir) && Directory.Exists(clientDir))
            {
                AnalyzeDirectory(clientDir, result, isServer: false);
            }
        }

        // Check for assembly markers
        result.HasApiContractMarker = result.DetectedNamespaces
            .Any(ns => ns.Contains("Generated", StringComparison.OrdinalIgnoreCase));

        result.HasDomainMarker = result.HandlerInterfaces.Count > 0;

        return result;
    }

    private static void AnalyzeDirectory(string directory, GeneratedCodeResult result, bool isServer)
    {
        try
        {
            var csFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                // Skip bin/obj folders
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AnalyzeFile(file, result, isServer);
            }
        }
        catch
        {
            // Ignore errors during analysis
        }
    }

    private static void AnalyzeFile(string filePath, GeneratedCodeResult result, bool isServer)
    {
        try
        {
            var content = File.ReadAllText(filePath);

            // Check for generated code marker
            var generatedMatch = GeneratedCodePattern.Match(content);
            if (generatedMatch.Success)
            {
                if (isServer)
                {
                    result.ServerFileCount++;
                }
                else
                {
                    result.ClientFileCount++;
                }

                // Extract generator version (use the latest found)
                var version = generatedMatch.Groups[1].Value;
                if (string.IsNullOrEmpty(result.GeneratorVersion) ||
                    string.Compare(version, result.GeneratorVersion, StringComparison.Ordinal) > 0)
                {
                    result.GeneratorVersion = version;
                }
            }

            // Extract namespaces
            var namespaceMatches = NamespacePattern.Matches(content);
            foreach (Match match in namespaceMatches)
            {
                var ns = match.Groups[1].Value;
                if (!result.DetectedNamespaces.Contains(ns))
                {
                    result.DetectedNamespaces.Add(ns);
                }
            }

            // Extract handler interfaces
            var interfaceMatches = InterfacePattern.Matches(content);
            foreach (Match match in interfaceMatches)
            {
                var interfaceName = match.Groups[1].Value;
                if (!result.HandlerInterfaces.Contains(interfaceName))
                {
                    result.HandlerInterfaces.Add(interfaceName);
                }
            }

            // Extract endpoint definitions
            var endpointMatches = EndpointDefinitionPattern.Matches(content);
            foreach (Match match in endpointMatches)
            {
                var endpointName = match.Groups[1].Value;
                if (!result.EndpointDefinitions.Contains(endpointName))
                {
                    result.EndpointDefinitions.Add(endpointName);
                }
            }

            // Extract model types (from Contracts/Models folders)
            if (filePath.Contains($"{Path.DirectorySeparatorChar}Models{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains($"{Path.DirectorySeparatorChar}Contracts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                var modelMatches = ModelPattern.Matches(content);
                foreach (Match match in modelMatches)
                {
                    var modelName = match.Groups[1].Value;
                    // Filter out common non-model types
                    if (!modelName.StartsWith("I", StringComparison.Ordinal) &&
                        !modelName.EndsWith("Handler", StringComparison.Ordinal) &&
                        !modelName.EndsWith("Endpoint", StringComparison.Ordinal) &&
                        !modelName.EndsWith("Result", StringComparison.Ordinal) &&
                        !modelName.EndsWith("Parameters", StringComparison.Ordinal) &&
                        !result.ModelTypes.Contains(modelName))
                    {
                        result.ModelTypes.Add(modelName);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors reading individual files
        }
    }
}
