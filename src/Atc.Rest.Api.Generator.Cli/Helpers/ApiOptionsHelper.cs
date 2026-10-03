namespace Atc.Rest.Api.Generator.Cli.Helpers;

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
    /// <returns>The loaded options, or the defaults when no options file is found.</returns>
    /// <exception cref="FileNotFoundException">An explicit <paramref name="optionsPath"/> does not lead to a file.</exception>
    /// <exception cref="InvalidDataException">The options file is not valid JSON or has a value of the wrong type.</exception>
    public static ApiGeneratorOptions LoadOptions(
        string? optionsPath,
        string? specificationPath)
    {
        var resolvedPath = ResolveOptionsFilePath(optionsPath, specificationPath);

        if (resolvedPath is null)
        {
            return string.IsNullOrWhiteSpace(optionsPath)
                ? new ApiGeneratorOptions()
                : throw new FileNotFoundException($"Options file not found: {Path.GetFullPath(optionsPath)}", optionsPath);
        }

        try
        {
            var json = File.ReadAllText(resolvedPath);
            return JsonSerializer.Deserialize<ApiGeneratorOptions>(json, JsonOptions) ?? new ApiGeneratorOptions();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Options file {resolvedPath} is not valid: {ex.Message}", ex);
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
            ReportUnknownProperties(document.RootElement, typeof(ApiGeneratorOptions), path: null, result.Warnings);
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
    /// Adds a warning for every JSON property the options classes do not have (a misspelled name is
    /// otherwise ignored silently), walking into the nested sections.
    /// </summary>
    private static void ReportUnknownProperties(
        JsonElement element,
        Type optionsType,
        string? path,
        ICollection<string> warnings)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var comparison = JsonOptions.PropertyNameCaseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var properties = optionsType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var jsonProperty in element.EnumerateObject())
        {
            var propertyPath = path is null ? jsonProperty.Name : $"{path}.{jsonProperty.Name}";
            var property = properties.FirstOrDefault(p => string.Equals(JsonNameOf(p), jsonProperty.Name, comparison));
            if (property is null)
            {
                warnings.Add($"Unknown property '{propertyPath}' is ignored.");
                continue;
            }

            if (property.PropertyType.IsClass &&
                property.PropertyType.Namespace == typeof(ApiGeneratorOptions).Namespace)
            {
                ReportUnknownProperties(jsonProperty.Value, property.PropertyType, propertyPath, warnings);
            }
        }
    }

    private static string JsonNameOf(System.Reflection.PropertyInfo property)
        => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>(property)?.Name
           ?? JsonOptions.PropertyNamingPolicy?.ConvertName(property.Name)
           ?? property.Name;

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