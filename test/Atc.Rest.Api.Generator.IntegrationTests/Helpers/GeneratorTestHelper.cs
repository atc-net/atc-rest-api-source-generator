namespace Atc.Rest.Api.Generator.IntegrationTests.Helpers;

/// <summary>
/// Helper class for testing code generation against scenario files.
/// Uses CodeGenerationService directly without Roslyn dependencies.
/// </summary>
public static class GeneratorTestHelper
{
    /// <summary>
    /// Gets the base path for test scenarios (test/Scenarios/).
    /// </summary>
    public static string GetScenariosBasePath()
    {
        // When running tests, the working directory is typically the test project's bin folder
        // The scenario files are copied to Scenarios subfolder
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Scenarios");
    }

    /// <summary>
    /// Loads the OpenAPI document from the specified YAML path.
    /// </summary>
    public static OpenApiDocument LoadOpenApiDocument(string yamlPath)
    {
        var yamlContent = File.ReadAllText(yamlPath);
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, yamlPath);
        return document ?? throw new InvalidOperationException($"Failed to parse OpenAPI document at {yamlPath}");
    }

    /// <summary>
    /// Gets all generated TypeScript client files with path information for folder-based output.
    /// Reads the marker file config to determine Fetch vs Axios HTTP client variant.
    /// </summary>
    public static IEnumerable<GeneratedType> GetTypeScriptClientTypesWithPaths(
        string yamlPath,
        string markerPath,
        string scenarioName)
    {
        var openApiDoc = LoadOpenApiDocument(yamlPath);

        // Read marker config to determine HTTP client variant
        var markerConfig = LoadTypeScriptMarkerConfig(markerPath);
        var httpClient = string.Equals(markerConfig?.HttpClient, "Axios", StringComparison.OrdinalIgnoreCase)
            ? TypeScriptHttpClient.Axios
            : TypeScriptHttpClient.Fetch;

        var hooksStyle = string.Equals(markerConfig?.HooksStyle, "ReactQuery", StringComparison.OrdinalIgnoreCase)
            ? TypeScriptHooksStyle.ReactQuery
            : string.Equals(markerConfig?.HooksStyle, "Swr", StringComparison.OrdinalIgnoreCase)
                ? TypeScriptHooksStyle.Swr
                : TypeScriptHooksStyle.None;

        var config = new TypeScriptClientConfig
        {
            HttpClient = httpClient,
            HooksStyle = hooksStyle,
            ConvertDates = markerConfig?.ConvertDates ?? false,
            BrandedIds = markerConfig?.BrandedIds ?? false,

            // Runtime validation needs the schemas to validate against — match the
            // CLI command's auto-imply so the scenario doesn't have to repeat both flags.
            ZodRuntimeValidate = markerConfig?.ZodRuntimeValidate ?? false,
            GenerateZodSchemas = (markerConfig?.ZodRuntimeValidate ?? false) || (markerConfig?.GenerateZodSchemas ?? false),
            EnumRuntimeValues = markerConfig?.EnumRuntimeValues ?? false,
            DryRun = false,
            GenerateFileHeaders = true,
        };

        // Generate to a temp directory
        var tempDir = Path.Combine(Path.GetTempPath(), "atc-ts-integration-tests", Guid.NewGuid().ToString("N"));

        try
        {
            TypeScriptClientGenerationService.Generate(openApiDoc, tempDir, config);

            // Walk the temp directory and produce GeneratedType records
            if (!Directory.Exists(tempDir))
            {
                yield break;
            }

            foreach (var filePath in Directory.EnumerateFiles(tempDir, "*.ts", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(tempDir, filePath);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var relativeDir = Path.GetDirectoryName(relativePath) ?? string.Empty;
                var subFolder = relativeDir.Replace('\\', '/');
                var category = string.IsNullOrEmpty(subFolder) ? "root" : subFolder;
                var content = File.ReadAllText(filePath);

                yield return new GeneratedType(
                    TypeName: fileName,
                    Category: category,
                    Namespace: string.Empty,
                    Content: content,
                    RequiredUsings: [],
                    GroupName: null,
                    SubFolder: subFolder);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch (IOException)
                {
                    // Best effort cleanup
                }
            }
        }
    }

    /// <summary>
    /// Loads the TypeScript client marker configuration.
    /// </summary>
    private static TypeScriptMarkerConfig? LoadTypeScriptMarkerConfig(
        string markerPath)
    {
        if (!File.Exists(markerPath))
        {
            return null;
        }

        try
        {
            var content = File.ReadAllText(markerPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            return JsonSerializer.Deserialize<TypeScriptMarkerConfig>(content, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Configuration from the .atc-rest-api-ts-client marker file.
    /// </summary>
    [SuppressMessage("", "S1144", Justification = "Properties populated by JSON deserialization")]
    [SuppressMessage("", "S3459", Justification = "Properties populated by JSON deserialization")]
    private sealed record TypeScriptMarkerConfig
    {
        public string HttpClient { get; init; } = "Fetch";

        public string? HooksStyle { get; init; }

        public bool ConvertDates { get; init; }

        public bool BrandedIds { get; init; }

        public bool ZodRuntimeValidate { get; init; }

        public bool GenerateZodSchemas { get; init; }

        public bool EnumRuntimeValues { get; init; }
    }
}