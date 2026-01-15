namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Modifies Program.cs files during migration to update endpoint registration patterns.
/// </summary>
internal static class ProgramCsModifier
{
    /// <summary>
    /// Updates Program.cs from old CLI patterns to source generator patterns.
    /// </summary>
    /// <param name="hostProjectDirectory">The directory containing the Host project.</param>
    /// <param name="projectName">The base project name (e.g., "MyProject").</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The result of the modification.</returns>
    public static ProgramCsModificationResult UpdateToSourceGenerator(
        string hostProjectDirectory,
        string projectName,
        bool dryRun = false)
    {
        var programCsPath = Path.Combine(hostProjectDirectory, "Program.cs");
        var result = new ProgramCsModificationResult { FilePath = programCsPath };

        if (!File.Exists(programCsPath))
        {
            result.Error = "Program.cs not found in Host project";
            return result;
        }

        try
        {
            var lines = File.ReadAllLines(programCsPath);
            var newLines = new List<string>();
            var modified = false;

            var hasAddApiHandlersFromDomain = false;
            var hasMapEndpoints = false;

            // First pass: analyze what exists
            foreach (var line in lines)
            {
                if (line.Contains("AddApiHandlersFromDomain", StringComparison.Ordinal))
                {
                    hasAddApiHandlersFromDomain = true;
                }

                if (line.Contains("MapEndpoints", StringComparison.Ordinal))
                {
                    hasMapEndpoints = true;
                }
            }

            // Second pass: modify lines
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                var shouldSkipLine = false;

                // Remove: services.AddEndpointDefinitions(...) or similar
                if (trimmed.Contains("AddEndpointDefinitions", StringComparison.Ordinal))
                {
                    result.RemovedStatements.Add("AddEndpointDefinitions(...)");
                    shouldSkipLine = true;
                    modified = true;
                }

                // Replace: app.UseEndpointDefinitions() → app.MapEndpoints()
                if (trimmed.Contains("UseEndpointDefinitions", StringComparison.Ordinal) && !hasMapEndpoints)
                {
                    var newLine = ReplaceUseEndpointDefinitions(line);
                    newLines.Add(newLine);
                    result.ReplacedStatements.Add("UseEndpointDefinitions() → MapEndpoints()");
                    hasMapEndpoints = true;
                    modified = true;
                    continue;
                }

                if (!shouldSkipLine)
                {
                    newLines.Add(line);
                }
            }

            // Add: builder.Services.AddApiHandlersFromDomain() if not present
            if (!hasAddApiHandlersFromDomain)
            {
                var insertIndex = FindAddApiHandlersInsertIndex(newLines);
                if (insertIndex >= 0)
                {
                    var indent = DetectIndent(newLines, insertIndex);
                    var handlerRegistration = $"{indent}builder.Services.AddApiHandlersFromDomain();";

                    // Add with an empty line before if the previous line is not empty
                    if (insertIndex > 0 && !string.IsNullOrWhiteSpace(newLines[insertIndex - 1]))
                    {
                        newLines.Insert(insertIndex, string.Empty);
                        insertIndex++;
                    }

                    newLines.Insert(insertIndex, handlerRegistration);
                    result.AddedStatements.Add("AddApiHandlersFromDomain()");
                    modified = true;
                }
            }

            if (modified)
            {
                result.WasModified = true;

                if (!dryRun)
                {
                    WriteAllLinesWithoutTrailingNewline(programCsPath, newLines);
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = $"Failed to update Program.cs: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Replaces UseEndpointDefinitions() with MapEndpoints() in a line.
    /// Handles both with and without semicolons.
    /// </summary>
    private static string ReplaceUseEndpointDefinitions(string line)
        => line.Replace("UseEndpointDefinitions()", "MapEndpoints()", StringComparison.Ordinal);

    /// <summary>
    /// Finds the best index to insert AddApiHandlersFromDomain().
    /// Looks for a good location after other service registrations.
    /// </summary>
    private static int FindAddApiHandlersInsertIndex(List<string> lines)
    {
        // Look for app = builder.Build() or var app = builder.Build()
        // We want to insert handler registration before builder.Build()
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.Contains("builder.Build()", StringComparison.Ordinal))
            {
                // Insert before this line
                return i;
            }
        }

        // Fallback: look for WebApplication.CreateBuilder and insert after service registrations
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("services.", StringComparison.Ordinal) ||
                line.StartsWith("builder.Services.", StringComparison.Ordinal))
            {
                // Insert after this line
                return i + 1;
            }
        }

        return -1;
    }

    /// <summary>
    /// Detects the indentation used around a given line index.
    /// </summary>
    private static string DetectIndent(
        List<string> lines,
        int nearIndex)
    {
        // Look at nearby lines to detect the indentation pattern
        var searchStart = System.Math.Max(0, nearIndex - 5);
        var searchEnd = System.Math.Min(lines.Count, nearIndex + 5);

        for (var i = searchStart; i < searchEnd; i++)
        {
            var line = lines[i];
            if (!string.IsNullOrWhiteSpace(line) &&
                (line.TrimStart().StartsWith("services.", StringComparison.Ordinal) ||
                 line.TrimStart().StartsWith("builder.", StringComparison.Ordinal)))
            {
                // Extract the leading whitespace
                var leadingWhitespace = line[..^line.TrimStart().Length];
                return leadingWhitespace;
            }
        }

        // Default to 4 spaces
        return "    ";
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