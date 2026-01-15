namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Validates and analyzes the OpenAPI specification file.
/// </summary>
internal static class SpecificationValidator
{
    /// <summary>
    /// Validates the OpenAPI specification at the given path.
    /// </summary>
    /// <param name="specificationPath">Path to the OpenAPI specification file.</param>
    /// <returns>The specification validation result.</returns>
    public static SpecificationResult Validate(string specificationPath)
    {
        var result = new SpecificationResult();

        // Check file exists
        result.FileExists = File.Exists(specificationPath);
        if (!result.FileExists)
        {
            result.ValidationErrors.Add($"Specification file not found: {specificationPath}");
            return result;
        }

        try
        {
            // Read and parse the specification
            var yamlContent = File.ReadAllText(specificationPath);
            var (openApiDoc, diagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, specificationPath);

            if (openApiDoc == null)
            {
                result.IsValid = false;
                if (diagnostic?.Errors != null)
                {
                    foreach (var error in diagnostic.Errors)
                    {
                        result.ValidationErrors.Add(error.Message);
                    }
                }

                return result;
            }

            result.IsValid = true;

            // Extract OpenAPI version
            result.OpenApiVersion = openApiDoc.Info?.Version != null
                ? $"3.x (API v{openApiDoc.Info.Version})"
                : "3.x";

            // Try to get the actual OpenAPI spec version from the YAML
            result.OpenApiVersion = ExtractOpenApiSpecVersion(yamlContent) ?? result.OpenApiVersion;

            // Extract API info
            result.ApiTitle = openApiDoc.Info?.Title;
            result.ApiVersion = openApiDoc.Info?.Version;

            // Count operations
            result.OperationCount = CountOperations(openApiDoc);

            // Count schemas
            result.SchemaCount = openApiDoc.Components?.Schemas?.Count ?? 0;

            // Detect multi-part specifications
            result.MultiPartFiles = DetectMultiPartFiles(specificationPath);
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ValidationErrors.Add($"Error parsing specification: {ex.Message}");
        }

        return result;
    }

    private static string? ExtractOpenApiSpecVersion(string yamlContent)
    {
        // Simple extraction of openapi version from YAML
        var lines = yamlContent.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("openapi:", StringComparison.OrdinalIgnoreCase))
            {
                var version = trimmed
                    .Replace("openapi:", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim()
                    .Trim('"', '\'');
                return version;
            }
        }

        return null;
    }

    private static int CountOperations(OpenApiDocument document)
    {
        var count = 0;

        if (document.Paths == null)
        {
            return count;
        }

        foreach (var path in document.Paths)
        {
            count += path.Value.Operations?.Count ?? 0;
        }

        return count;
    }

    private static List<string> DetectMultiPartFiles(string baseSpecPath)
    {
        var result = new List<string>();
        var directory = Path.GetDirectoryName(baseSpecPath);

        if (string.IsNullOrEmpty(directory))
        {
            return result;
        }

        var baseName = Path.GetFileNameWithoutExtension(baseSpecPath);
        var extension = Path.GetExtension(baseSpecPath);

        // Look for files matching pattern: {BaseName}_{PartName}.yaml
        var partPattern = $"{baseName}_*{extension}";

        try
        {
            var partFiles = Directory
                .GetFiles(directory, partPattern)
                .Where(f => !f.Equals(baseSpecPath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.AddRange(partFiles);
        }
        catch
        {
            // Ignore errors during multi-part detection
        }

        return result;
    }
}