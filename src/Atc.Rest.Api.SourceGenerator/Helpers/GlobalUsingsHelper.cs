namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Helpers for managing GlobalUsings.cs files in domain projects.
/// </summary>
[SuppressMessage("", "RS1035:The symbol 'File' is banned for use by analyzers: Do not do file IO in analyzers", Justification = "OK.")]
internal static class DomainGlobalUsingsHelper
{
    /// <summary>
    /// Ensures GlobalUsings.cs at the project root contains proper usings for handlers.
    /// Updates the file by adding missing usings while preserving existing content.
    /// </summary>
    public static void EnsureUpdated(
        string projectRootDirectory,
        HashSet<string> discoveredInterfaceNamespaces,
        string rootNamespace,
        List<string> pathSegments,
        OpenApiDocument openApiDoc,
        ServerDomainConfig config)
    {
        var globalUsingsPath = Path.Combine(projectRootDirectory, "GlobalUsings.cs");

        // Build required usings set
        var requiredUsings = BuildRequiredUsings(discoveredInterfaceNamespaces, rootNamespace, pathSegments, openApiDoc);

        // Read existing content
        var existingContent = File.Exists(globalUsingsPath)
            ? File.ReadAllText(globalUsingsPath)
            : string.Empty;

        // Parse existing global usings
        var existingUsings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in existingContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("global using ", StringComparison.Ordinal))
            {
                existingUsings.Add(trimmed);
            }
        }

        // Find missing usings
        var missingUsings = requiredUsings
            .Except(existingUsings, StringComparer.Ordinal)
            .ToList();

        // Check if file ends with newline
        var endsWithNewline = existingContent.Length > 0 &&
            (existingContent.EndsWith("\n", StringComparison.Ordinal) ||
             existingContent.EndsWith("\r", StringComparison.Ordinal));

        // If nothing to add and file ends with newline, skip
        if (missingUsings.Count == 0 && endsWithNewline)
        {
            return;
        }

        // If nothing to add but file doesn't end with newline, fix it
        if (missingUsings.Count == 0)
        {
            FileHelper.WriteCsFile(globalUsingsPath, existingContent);
            return;
        }

        // Merge all usings (existing + required) and re-sort the entire file
        var allUsings = new HashSet<string>(existingUsings, StringComparer.Ordinal);
        foreach (var required in requiredUsings)
        {
            allUsings.Add(required);
        }

        // Sort and format global usings with namespace grouping
        var sortedContent = UsingStatementHelper.SortGlobalUsings(
            allUsings,
            config.RemoveNamespaceGroupSeparatorInGlobalUsings);

        // Write file using helper (ensures proper newline handling)
        FileHelper.WriteCsFile(globalUsingsPath, sortedContent);
    }

    /// <summary>
    /// Builds the set of required global using directives based on discovered namespaces,
    /// path segments, and the OpenAPI document. This is a pure function with no filesystem access.
    /// </summary>
    public static HashSet<string> BuildRequiredUsings(
        HashSet<string> discoveredInterfaceNamespaces,
        string rootNamespace,
        List<string> pathSegments,
        OpenApiDocument openApiDoc)
    {
        var requiredUsings = new HashSet<string>(StringComparer.Ordinal)
        {
            "global using System;",
            "global using System.Threading;",
            "global using System.Threading.Tasks;",
        };

        // Use discovered interface namespaces if available
        if (discoveredInterfaceNamespaces.Count > 0)
        {
            // Add discovered namespaces and their related Parameters/Results namespaces
            foreach (var ns in discoveredInterfaceNamespaces.OrderBy(s => s, StringComparer.Ordinal))
            {
                requiredUsings.Add($"global using {ns};");

                // Derive Parameters and Results namespaces from Handlers namespace
                if (ns.EndsWith(".Handlers", StringComparison.Ordinal))
                {
                    var baseNs = ns.Substring(0, ns.Length - ".Handlers".Length);

                    // Extract segment name from namespace to check if it has parameters/results
                    var segments = baseNs.Split('.');
                    var segmentName = segments.Length > 0 ? segments[segments.Length - 1] : string.Empty;
                    var namespaces = PathSegmentHelper.GetPathSegmentNamespaces(openApiDoc, segmentName);

                    if (namespaces.HasParameters)
                    {
                        requiredUsings.Add($"global using {baseNs}.Parameters;");
                    }

                    if (namespaces.HasResults)
                    {
                        requiredUsings.Add($"global using {baseNs}.Results;");
                    }
                }
            }
        }
        else
        {
            // Fall back to path segment usings (sorted), conditionally based on OpenAPI spec
            var sortedSegments = pathSegments
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var segment in sortedSegments)
            {
                var namespaces = PathSegmentHelper.GetPathSegmentNamespaces(openApiDoc, segment);

                // Add conditional segment usings (exclude Models for domain handlers)
                foreach (var usingDirective in PathSegmentHelper.GetSegmentUsings(
                    rootNamespace, segment, namespaces, includeModels: false, isGlobalUsing: true))
                {
                    requiredUsings.Add(usingDirective);
                }
            }
        }

        return requiredUsings;
    }
}