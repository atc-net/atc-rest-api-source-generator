namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Analyzes generator options files from old-generated projects.
/// </summary>
internal static class GeneratorOptionsAnalyzer
{
    private static readonly string[] OptionsFilePatterns =
    [
        "*GeneratorOptions.json",
        "*Options.json",
        "ApiGeneratorOptions.json",
    ];

    /// <summary>
    /// Analyzes generator options in the given directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory to search.</param>
    /// <returns>The generator options analysis result.</returns>
    public static GeneratorOptionsResult Analyze(string rootDirectory)
    {
        var result = new GeneratorOptionsResult();

        // Find options file
        var optionsFile = FindOptionsFile(rootDirectory);
        if (optionsFile == null)
        {
            result.Found = false;
            return result;
        }

        result.Found = true;
        result.FilePath = optionsFile;

        try
        {
            var jsonContent = File.ReadAllText(optionsFile);
            ParseOptionsFile(jsonContent, result);
            result.IsValid = result.ParsingErrors.Count == 0;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ParsingErrors.Add($"Error reading options file: {ex.Message}");
        }

        return result;
    }

    private static string? FindOptionsFile(string rootDirectory)
    {
        foreach (var pattern in OptionsFilePatterns)
        {
            try
            {
                var files = Directory.GetFiles(rootDirectory, pattern, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    // Prefer files not in bin/obj folders
                    var preferredFile = files.FirstOrDefault(f =>
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

                    return preferredFile ?? files[0];
                }
            }
            catch
            {
                // Continue with next pattern
            }
        }

        return null;
    }

    private static void ParseOptionsFile(string jsonContent, GeneratorOptionsResult result)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            // Try to parse old format (generator/validation sections)
            if (root.TryGetProperty("generator", out var generatorSection))
            {
                ParseOldFormatGeneratorSection(generatorSection, result);
            }

            if (root.TryGetProperty("validation", out var validationSection))
            {
                ParseOldFormatValidationSection(validationSection, result);
            }

            // Try to parse new format (General/Server/Client sections)
            if (root.TryGetProperty("General", out var generalSection))
            {
                ParseNewFormatGeneralSection(generalSection, result);
            }

            if (root.TryGetProperty("Server", out var serverSection))
            {
                ParseNewFormatServerSection(serverSection, result);
            }

            if (root.TryGetProperty("Client", out var clientSection))
            {
                ParseNewFormatClientSection(clientSection, result);
            }
        }
        catch (JsonException ex)
        {
            result.ParsingErrors.Add($"Invalid JSON: {ex.Message}");
        }
    }

    private static void ParseOldFormatGeneratorSection(JsonElement section, GeneratorOptionsResult result)
    {
        if (section.TryGetProperty("aspNetOutputType", out var outputType))
        {
            result.AspNetOutputType = outputType.GetString();
        }

        if (section.TryGetProperty("useRestExtended", out var useRestExtended))
        {
            result.UseRestExtended = useRestExtended.GetBoolean();
        }

        if (section.TryGetProperty("httpClientName", out var httpClientName))
        {
            result.HttpClientName = httpClientName.GetString();
        }

        if (section.TryGetProperty("response", out var responseSection))
        {
            if (responseSection.TryGetProperty("useProblemDetailsAsDefaultBody", out var useProblemDetails))
            {
                result.UseProblemDetails = useProblemDetails.GetBoolean();
            }
        }
    }

    private static void ParseOldFormatValidationSection(JsonElement section, GeneratorOptionsResult result)
    {
        if (section.TryGetProperty("strictMode", out var strictMode))
        {
            result.StrictMode = strictMode.GetBoolean();
        }

        if (section.TryGetProperty("operationIdCasingStyle", out var operationIdCasing))
        {
            result.OperationIdCasingStyle = operationIdCasing.GetString();
        }

        if (section.TryGetProperty("modelNameCasingStyle", out var modelNameCasing))
        {
            result.ModelNameCasingStyle = modelNameCasing.GetString();
        }

        if (section.TryGetProperty("modelPropertyNameCasingStyle", out var modelPropertyCasing))
        {
            result.ModelPropertyNameCasingStyle = modelPropertyCasing.GetString();
        }
    }

    private static void ParseNewFormatGeneralSection(JsonElement section, GeneratorOptionsResult result)
    {
        // New format uses ValidateSpecificationStrategy instead of strictMode
        if (section.TryGetProperty("ValidateSpecificationStrategy", out var strategy))
        {
            var strategyValue = strategy.GetString();
            result.StrictMode = string.Equals(strategyValue, "Strict", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void ParseNewFormatServerSection(JsonElement section, GeneratorOptionsResult result)
    {
        if (section.TryGetProperty("UseMinimalApiPackage", out var useMinimalApi))
        {
            result.AspNetOutputType = useMinimalApi.GetBoolean() ? "MinimalApi" : "Mvc";
        }
    }

    private static void ParseNewFormatClientSection(JsonElement section, GeneratorOptionsResult result)
    {
        // Client section options for new format
        if (section.TryGetProperty("ClientSuffix", out var clientSuffix))
        {
            result.HttpClientName = clientSuffix.GetString();
        }
    }
}
