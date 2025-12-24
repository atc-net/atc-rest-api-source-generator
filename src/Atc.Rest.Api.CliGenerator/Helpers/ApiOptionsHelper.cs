namespace Atc.Rest.Api.CliGenerator.Helpers;

/// <summary>
/// Helper class for loading and managing ApiGeneratorOptions.json configuration files.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "File operations need graceful fallback to defaults.")]
public static class ApiOptionsHelper
{
    /// <summary>
    /// Shared JSON options for serializing/deserializing ApiGeneratorOptions.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = JsonSerializerOptionsFactory.Create();

    /// <summary>
    /// Loads ApiGeneratorOptions from the specified path or auto-discovers the options file.
    /// </summary>
    /// <param name="optionsPath">Explicit path to options file (optional).</param>
    /// <param name="specificationPath">Path to OpenAPI specification file (used for auto-discovery).</param>
    /// <returns>Loaded options or default options if no file found.</returns>
    public static ApiGeneratorOptions LoadOptions(
        string? optionsPath,
        string? specificationPath)
    {
        var resolvedPath = ResolveOptionsFilePath(optionsPath, specificationPath);

        if (resolvedPath is null ||
            !File.Exists(resolvedPath))
        {
            return new ApiGeneratorOptions();
        }

        try
        {
            var json = File.ReadAllText(resolvedPath);
            var options = JsonSerializer.Deserialize<ApiGeneratorOptions>(json, JsonOptions);
            return options ?? new ApiGeneratorOptions();
        }
        catch (JsonException)
        {
            return new ApiGeneratorOptions();
        }
    }

    /// <summary>
    /// Loads ApiGeneratorOptions asynchronously from the specified path or auto-discovers the options file.
    /// </summary>
    /// <param name="optionsPath">Explicit path to options file (optional).</param>
    /// <param name="specificationPath">Path to OpenAPI specification file (used for auto-discovery).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Loaded options or default options if no file found.</returns>
    public static async Task<ApiGeneratorOptions> LoadOptionsAsync(
        string? optionsPath,
        string? specificationPath,
        CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolveOptionsFilePath(optionsPath, specificationPath);

        if (resolvedPath is null || !File.Exists(resolvedPath))
        {
            return new ApiGeneratorOptions();
        }

        try
        {
            await using var stream = File.OpenRead(resolvedPath);
            var options = await JsonSerializer.DeserializeAsync<ApiGeneratorOptions>(stream, JsonOptions, cancellationToken);
            return options ?? new ApiGeneratorOptions();
        }
        catch (JsonException)
        {
            return new ApiGeneratorOptions();
        }
    }

    /// <summary>
    /// Resolves the path to the ApiGeneratorOptions.json file using priority order:
    /// 1. Explicit path provided via --options argument
    /// 2. Same folder as specification file
    /// 3. Current working directory
    /// 4. Same folder as CLI tool executable
    /// </summary>
    /// <param name="optionsPath">Explicit path to options file (optional).</param>
    /// <param name="specificationPath">Path to OpenAPI specification file.</param>
    /// <returns>Resolved path to options file, or null if not found.</returns>
    public static string? ResolveOptionsFilePath(
        string? optionsPath,
        string? specificationPath)
    {
        // Priority 1: Explicit path provided
        if (!string.IsNullOrWhiteSpace(optionsPath))
        {
            var fullPath = Path.GetFullPath(optionsPath);

            // If it's a directory, append the default file name
            if (Directory.Exists(fullPath))
            {
                fullPath = Path.Combine(fullPath, ApiGeneratorOptions.FileName);
            }

            return File.Exists(fullPath) ? fullPath : null;
        }

        // Priority 2: Same folder as specification file
        if (!string.IsNullOrWhiteSpace(specificationPath))
        {
            var specDirectory = Path.GetDirectoryName(Path.GetFullPath(specificationPath));
            if (!string.IsNullOrWhiteSpace(specDirectory))
            {
                var specFolderPath = Path.Combine(specDirectory, ApiGeneratorOptions.FileName);
                if (File.Exists(specFolderPath))
                {
                    return specFolderPath;
                }
            }
        }

        // Priority 3: Current working directory
        var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), ApiGeneratorOptions.FileName);
        if (File.Exists(cwdPath))
        {
            return cwdPath;
        }

        // Priority 4: Same folder as CLI tool executable
        var exePath = AppContext.BaseDirectory;
        var exeFolderPath = Path.Combine(exePath, ApiGeneratorOptions.FileName);
        if (File.Exists(exeFolderPath))
        {
            return exeFolderPath;
        }

        return null;
    }

    /// <summary>
    /// Creates a default ApiGeneratorOptions.json file at the specified path.
    /// </summary>
    /// <param name="outputPath">Directory or file path for the options file.</param>
    /// <returns>True if file was created successfully; otherwise, false.</returns>
    public static bool CreateDefaultOptionsFile(string outputPath)
    {
        try
        {
            var filePath = ResolveOutputPath(outputPath);
            var defaultOptions = new ApiGeneratorOptions();
            var json = JsonSerializer.Serialize(defaultOptions, JsonOptions);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a default ApiGeneratorOptions.json file at the specified path asynchronously.
    /// </summary>
    /// <param name="outputPath">Directory or file path for the options file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if file was created successfully; otherwise, false.</returns>
    public static async Task<bool> CreateDefaultOptionsFileAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = ResolveOutputPath(outputPath);
            var defaultOptions = new ApiGeneratorOptions();

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, defaultOptions, JsonOptions, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates the structure and values of an ApiGeneratorOptions.json file.
    /// </summary>
    /// <param name="optionsPath">Path to the options file.</param>
    /// <returns>Validation result with any error messages.</returns>
    public static OptionsValidationResult ValidateOptionsFile(
        string optionsPath)
    {
        var result = new OptionsValidationResult();

        var filePath = Path.GetFullPath(optionsPath);

        // If it's a directory, append the default file name
        if (Directory.Exists(filePath))
        {
            filePath = Path.Combine(filePath, ApiGeneratorOptions.FileName);
        }

        if (!File.Exists(filePath))
        {
            result.Errors.Add($"Options file not found: {filePath}");
            return result;
        }

        try
        {
            var json = File.ReadAllText(filePath);

            // Try to parse as JSON
            using var document = JsonDocument.Parse(json);

            // Try to deserialize to ApiGeneratorOptions
            var options = JsonSerializer.Deserialize<ApiGeneratorOptions>(json, JsonOptions);

            if (options is null)
            {
                result.Errors.Add("Failed to deserialize options file.");
                return result;
            }

            // Validate specific values
            ValidateOptions(options, result);

            result.IsValid = result.Errors.Count == 0;
            result.Options = options;
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"Invalid JSON format: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Validates the structure and values of an ApiGeneratorOptions.json file asynchronously.
    /// </summary>
    /// <param name="optionsPath">Path to the options file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any error messages.</returns>
    public static async Task<OptionsValidationResult> ValidateOptionsFileAsync(
        string optionsPath,
        CancellationToken cancellationToken = default)
    {
        var result = new OptionsValidationResult();

        var filePath = Path.GetFullPath(optionsPath);

        // If it's a directory, append the default file name
        if (Directory.Exists(filePath))
        {
            filePath = Path.Combine(filePath, ApiGeneratorOptions.FileName);
        }

        if (!File.Exists(filePath))
        {
            result.Errors.Add($"Options file not found: {filePath}");
            return result;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);

            // Try to parse as JSON
            using var document = JsonDocument.Parse(json);

            // Try to deserialize to ApiGeneratorOptions
            var options = JsonSerializer.Deserialize<ApiGeneratorOptions>(json, JsonOptions);

            if (options is null)
            {
                result.Errors.Add("Failed to deserialize options file.");
                return result;
            }

            // Validate specific values
            ValidateOptions(options, result);

            result.IsValid = result.Errors.Count == 0;
            result.Options = options;
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"Invalid JSON format: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Gets the resolved file path for an options file, given a directory or file path.
    /// </summary>
    /// <param name="outputPath">Directory or file path.</param>
    /// <returns>Full file path to the options file.</returns>
    private static string ResolveOutputPath(string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);

        // If it's a directory or doesn't have a .json extension, append the default file name
        if (Directory.Exists(fullPath) || !fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(fullPath, ApiGeneratorOptions.FileName);
        }

        return fullPath;
    }

    /// <summary>
    /// Validates the loaded options and populates the result with any errors or warnings.
    /// </summary>
    private static void ValidateOptions(
        ApiGeneratorOptions options,
        OptionsValidationResult result)
    {
        // Validate server options
        if (!string.IsNullOrWhiteSpace(options.Server.DefaultApiVersion) &&
            !Version.TryParse(options.Server.DefaultApiVersion, out _))
        {
            result.Warnings.Add($"server.defaultApiVersion '{options.Server.DefaultApiVersion}' is not a valid version format (expected: major.minor).");
        }

        // Validate domain options
        if (string.IsNullOrWhiteSpace(options.Server.Domain.GenerateHandlersOutput))
        {
            result.Warnings.Add("server.domain.generateHandlersOutput should not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.Server.Domain.HandlerSuffix))
        {
            result.Warnings.Add("server.domain.handlerSuffix should not be empty.");
        }

        var validStubImplementations = new[] { "throw-not-implemented", "error-501", "default-value" };
        if (!validStubImplementations.Contains(options.Server.Domain.StubImplementation, StringComparer.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"server.domain.stubImplementation '{options.Server.Domain.StubImplementation}' is not recognized. Valid values: {string.Join(", ", validStubImplementations)}");
        }

        // Validate client options
        if (string.IsNullOrWhiteSpace(options.Client.ClientSuffix))
        {
            result.Warnings.Add("client.clientSuffix should not be empty.");
        }
    }
}