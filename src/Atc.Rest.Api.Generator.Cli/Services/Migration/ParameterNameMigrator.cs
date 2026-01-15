namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Migrates parameter property name usages in handler files from old generator format to new format.
/// </summary>
/// <remarks>
/// The old CLI generator and new source generator produce different property names for parameters:
/// - Old generator: strips "x-" prefix from header names, then converts to PascalCase
/// - New generator: uses reference ID for headers if available, otherwise ToPascalCaseForDotNet()
///
/// Example:
/// - YAML parameter: "x-continuation" with reference ID "ContinuationToken"
/// - Old generator: "Continuation"
/// - New generator: "ContinuationToken"
///
/// This migrator finds usages of old parameter names (e.g., parameters.Continuation)
/// and replaces them with new names (e.g., parameters.ContinuationToken).
/// </remarks>
internal static class ParameterNameMigrator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Migrates parameter property name usages in handler files using an OpenAPI specification file.
    /// </summary>
    /// <param name="domainProjectDirectory">The directory containing the Domain project.</param>
    /// <param name="specFilePath">The path to the OpenAPI specification file.</param>
    /// <param name="dryRun">If true, only reports what would be changed without making changes.</param>
    /// <returns>Result containing modified files and parameter replacements made.</returns>
    public static ParameterMigrationResult MigrateParameterNames(
        string domainProjectDirectory,
        string specFilePath,
        bool dryRun = false)
    {
        var result = new ParameterMigrationResult();

        if (!File.Exists(specFilePath))
        {
            result.Error = $"OpenAPI spec file not found: {specFilePath}";
            return result;
        }

        var openApiDoc = ParseOpenApiDocument(specFilePath);
        if (openApiDoc == null)
        {
            result.Error = "Failed to parse OpenAPI specification";
            return result;
        }

        return MigrateParameterNames(domainProjectDirectory, openApiDoc, dryRun);
    }

    /// <summary>
    /// Migrates parameter property name usages in handler files.
    /// </summary>
    /// <param name="domainProjectDirectory">The directory containing the Domain project.</param>
    /// <param name="openApiDoc">The OpenAPI document to extract parameter names from.</param>
    /// <param name="dryRun">If true, only reports what would be changed without making changes.</param>
    /// <returns>Result containing modified files and parameter replacements made.</returns>
    public static ParameterMigrationResult MigrateParameterNames(
        string domainProjectDirectory,
        OpenApiDocument openApiDoc,
        bool dryRun = false)
    {
        var result = new ParameterMigrationResult();

        try
        {
            // Build mapping of old names -> new names from OpenAPI spec
            var parameterMapping = BuildParameterNameMapping(openApiDoc);

            if (parameterMapping.Count == 0)
            {
                return result;
            }

            // Find all handler .cs files in Domain project
            var handlerFiles = FindHandlerFiles(domainProjectDirectory);

            // Migrate each handler file
            foreach (var filePath in handlerFiles)
            {
                MigrateHandlerFile(filePath, parameterMapping, result, dryRun);
            }
        }
        catch (Exception ex)
        {
            result.Error = $"Failed to migrate parameter names: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Builds a mapping of old parameter property names to new names.
    /// </summary>
    private static Dictionary<string, string> BuildParameterNameMapping(
        OpenApiDocument doc)
    {
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);

        if (doc.Paths == null)
        {
            return mapping;
        }

        foreach (var path in doc.Paths)
        {
            if (path.Value is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            // Collect all parameters: path-level + operation-level
            var allParameters = new List<IOpenApiParameter>();

            if (pathItem.Parameters != null)
            {
                allParameters.AddRange(pathItem.Parameters);
            }

            foreach (var operation in pathItem.Operations)
            {
                if (operation.Value?.Parameters != null)
                {
                    allParameters.AddRange(operation.Value.Parameters);
                }
            }

            foreach (var paramOrRef in allParameters)
            {
                var resolved = paramOrRef.Resolve();
                var parameter = resolved.Parameter;
                var referenceId = resolved.ReferenceId;

                if (parameter == null || string.IsNullOrEmpty(parameter.Name))
                {
                    continue;
                }

                var paramLocation = parameter.In ?? ParameterLocation.Query;

                // New generator logic: use referenceId for headers if available,
                // otherwise strip x- prefix and convert to PascalCase
                string newName;
                if (paramLocation == ParameterLocation.Header)
                {
                    newName = !string.IsNullOrEmpty(referenceId)
                        ? referenceId!.ToPascalCaseForDotNet()
                        : parameter.Name!.ToHeaderPropertyName();
                }
                else
                {
                    newName = parameter.Name!.ToPascalCaseForDotNet();
                }

                // Old generator logic: strip x- prefix, then PascalCase with hyphen handling
                var oldName = GetOldGeneratorParameterName(parameter.Name!);

                // Only add to mapping if names differ
                if (!oldName.Equals(newName, StringComparison.Ordinal) && !mapping.ContainsKey(oldName))
                {
                    mapping[oldName] = newName;
                }
            }
        }

        return mapping;
    }

    /// <summary>
    /// Calculates the property name as the old generator would produce it.
    /// </summary>
    /// <remarks>
    /// Old generator uses EnsureValidFormattedPropertyName() which:
    /// 1. Strips "x-" prefix (case-insensitive)
    /// 2. Converts to PascalCase treating hyphens as word separators
    /// </remarks>
    private static string GetOldGeneratorParameterName(string parameterName)
    {
        var name = parameterName;

        // Step 1: Strip "x-" prefix (case-insensitive)
        if (name.StartsWith("x-", StringComparison.OrdinalIgnoreCase))
        {
            name = name[2..];
        }

        // Step 2: Convert to PascalCase treating hyphens as separators
        if (name.Contains('-', StringComparison.Ordinal))
        {
            var parts = name.Split('-');
            var result = new StringBuilder();
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                result.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    result.Append(part[1..].ToLowerInvariant());
                }
            }

            return result.ToString();
        }

        // Just capitalize first character
        return string.IsNullOrEmpty(name)
            ? name
            : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Finds all handler .cs files in the domain project directory.
    /// </summary>
    private static IEnumerable<string> FindHandlerFiles(string domainDirectory)
    {
        if (!Directory.Exists(domainDirectory))
        {
            return [];
        }

        var objPath = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;
        var binPath = Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar;

        // Look for handler files - typically in Handlers folder or files ending with Handler.cs
        return Directory.GetFiles(domainDirectory, "*Handler*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(objPath, StringComparison.Ordinal) &&
                        !f.Contains(binPath, StringComparison.Ordinal));
    }

    /// <summary>
    /// Migrates parameter usages in a single handler file.
    /// </summary>
    private static void MigrateHandlerFile(
        string filePath,
        Dictionary<string, string> parameterMapping,
        ParameterMigrationResult result,
        bool dryRun)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var modified = content;

            foreach (var (oldName, newName) in parameterMapping)
            {
                // Pattern: parameters.OldName (followed by word boundary)
                // Also handle Parameters.OldName (with capital P)
                var patterns = new[]
                {
                    $@"parameters\.{Regex.Escape(oldName)}\b",
                    $@"Parameters\.{Regex.Escape(oldName)}\b",
                };

                foreach (var pattern in patterns)
                {
                    if (Regex.IsMatch(modified, pattern, RegexOptions.None, RegexTimeout))
                    {
                        modified = Regex.Replace(
                            modified,
                            pattern,
                            m =>
                            {
                                // Preserve the prefix (parameters. or Parameters.)
                                var prefix = m.Value.Substring(0, m.Value.LastIndexOf('.') + 1);
                                return prefix + newName;
                            },
                            RegexOptions.None,
                            RegexTimeout);

                        var replacement = $"{oldName} → {newName} in {Path.GetFileName(filePath)}";
                        if (!result.ReplacedParameters.Contains(replacement, StringComparer.Ordinal))
                        {
                            result.ReplacedParameters.Add(replacement);
                        }
                    }
                }
            }

            if (modified != content)
            {
                if (!result.ModifiedFiles.Contains(filePath, StringComparer.Ordinal))
                {
                    result.ModifiedFiles.Add(filePath);
                }

                if (!dryRun)
                {
                    File.WriteAllText(filePath, modified);
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail the entire migration for a single file
            result.ReplacedParameters.Add($"Error processing {Path.GetFileName(filePath)}: {ex.Message}");
        }
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
}