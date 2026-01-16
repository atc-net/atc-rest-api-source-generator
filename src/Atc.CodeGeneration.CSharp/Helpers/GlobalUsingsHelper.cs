namespace Atc.CodeGeneration.CSharp.Helpers;

/// <summary>
/// Provides helper methods for creating and updating GlobalUsings.cs files in .NET projects.
/// </summary>
public static class GlobalUsingsHelper
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Creates or updates a GlobalUsings.cs file in the specified directory with the required namespaces.
    /// </summary>
    /// <param name="directoryInfo">The directory where GlobalUsings.cs should be created or updated.</param>
    /// <param name="requiredNamespaces">The list of namespaces to include in the global usings file.</param>
    /// <param name="setSystemFirst">Whether to place System namespaces before other namespaces. Default is true.</param>
    /// <param name="addNamespaceSeparator">Whether to add blank lines between namespace groups. Default is true.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directoryInfo"/> or <paramref name="requiredNamespaces"/> is null.</exception>
    public static void CreateOrUpdate(
        DirectoryInfo directoryInfo,
        IReadOnlyList<string> requiredNamespaces,
        bool setSystemFirst = true,
        bool addNamespaceSeparator = true)
    {
        if (directoryInfo is null)
        {
            throw new ArgumentNullException(nameof(directoryInfo));
        }

        if (requiredNamespaces is null)
        {
            throw new ArgumentNullException(nameof(requiredNamespaces));
        }

        var content = GetMergedContent(
            directoryInfo,
            requiredNamespaces,
            setSystemFirst,
            addNamespaceSeparator);

        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        var globalUsingFile = Path.Combine(
            directoryInfo.FullName,
            "GlobalUsings.cs");

        File.WriteAllText(globalUsingFile, content, Utf8NoBom);
    }

    /// <summary>
    /// Creates or updates a GlobalUsings.cs file at the specified path with the required namespaces.
    /// </summary>
    /// <param name="directoryPath">The directory path where GlobalUsings.cs should be created or updated.</param>
    /// <param name="requiredNamespaces">The list of namespaces to include in the global usings file.</param>
    /// <param name="setSystemFirst">Whether to place System namespaces before other namespaces. Default is true.</param>
    /// <param name="addNamespaceSeparator">Whether to add blank lines between namespace groups. Default is true.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="directoryPath"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="requiredNamespaces"/> is null.</exception>
    public static void CreateOrUpdate(
        string directoryPath,
        IReadOnlyList<string> requiredNamespaces,
        bool setSystemFirst = true,
        bool addNamespaceSeparator = true)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be null or empty.", nameof(directoryPath));
        }

        CreateOrUpdate(
            new DirectoryInfo(directoryPath),
            requiredNamespaces,
            setSystemFirst,
            addNamespaceSeparator);
    }

    /// <summary>
    /// Generates global usings content by merging existing namespaces (if the file exists) with required namespaces.
    /// </summary>
    /// <param name="directoryInfo">The directory to check for an existing GlobalUsings.cs file.</param>
    /// <param name="requiredNamespaces">The list of namespaces that must be included.</param>
    /// <param name="setSystemFirst">Whether to place System namespaces before other namespaces. Default is true.</param>
    /// <param name="addNamespaceSeparator">Whether to add blank lines between namespace groups. Default is true.</param>
    /// <returns>The formatted global usings content ready to be written to a file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directoryInfo"/> or <paramref name="requiredNamespaces"/> is null.</exception>
    public static string GetMergedContent(
        DirectoryInfo directoryInfo,
        IReadOnlyList<string> requiredNamespaces,
        bool setSystemFirst = true,
        bool addNamespaceSeparator = true)
    {
        if (directoryInfo is null)
        {
            throw new ArgumentNullException(nameof(directoryInfo));
        }

        if (requiredNamespaces is null)
        {
            throw new ArgumentNullException(nameof(requiredNamespaces));
        }

        var existingNamespaces = ReadNamespaces(directoryInfo);

        if (requiredNamespaces.Count == 0)
        {
            return existingNamespaces.Count > 0
                ? GenerateContent(
                    existingNamespaces,
                    setSystemFirst,
                    addNamespaceSeparator)
                : string.Empty;
        }

        var namespaces = MergeNamespaces(
            requiredNamespaces,
            existingNamespaces);

        return GenerateContent(
            namespaces,
            setSystemFirst,
            addNamespaceSeparator);
    }

    /// <summary>
    /// Generates the content for a GlobalUsings.cs file from a list of namespaces.
    /// </summary>
    /// <param name="namespaces">The namespaces to include in the global usings.</param>
    /// <param name="setSystemFirst">Whether to place System namespaces before other namespaces. Default is true.</param>
    /// <param name="addNamespaceSeparator">Whether to add blank lines between namespace groups. Default is true.</param>
    /// <returns>The formatted global usings content.</returns>
    public static string GenerateContent(
        IEnumerable<string> namespaces,
        bool setSystemFirst = true,
        bool addNamespaceSeparator = true)
    {
        if (namespaces is null)
        {
            throw new ArgumentNullException(nameof(namespaces));
        }

        var namespaceList = namespaces.ToList();
        if (namespaceList.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        if (setSystemFirst)
        {
            var sortedSystemNamespaces = namespaceList
                .Where(x => x.Equals("System", StringComparison.Ordinal) ||
                            x.StartsWith("System.", StringComparison.Ordinal))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sortedOtherNamespaces = namespaceList
                .Where(x => !x.Equals("System", StringComparison.Ordinal) &&
                            !x.StartsWith("System.", StringComparison.Ordinal))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var item in sortedSystemNamespaces)
            {
                sb.AppendLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "global using {0};",
                    item));
            }

            if (addNamespaceSeparator &&
                sortedSystemNamespaces.Count > 0 &&
                sortedOtherNamespaces.Count > 0)
            {
                sb.AppendLine();
            }

            AppendNamespacesWithGrouping(sb, sortedOtherNamespaces, addNamespaceSeparator);
        }
        else
        {
            var sortedNamespaces = namespaceList
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AppendNamespacesWithGrouping(sb, sortedNamespaces, addNamespaceSeparator);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Reads existing namespaces from a GlobalUsings.cs file in the specified directory.
    /// </summary>
    /// <param name="directoryInfo">The directory to check for an existing GlobalUsings.cs file.</param>
    /// <returns>A list of namespaces found in the file, or an empty list if the file doesn't exist.</returns>
    public static List<string> ReadNamespaces(DirectoryInfo directoryInfo)
    {
        if (directoryInfo is null)
        {
            throw new ArgumentNullException(nameof(directoryInfo));
        }

        var globalUsingFile = Path.Combine(
            directoryInfo.FullName,
            "GlobalUsings.cs");

        if (!File.Exists(globalUsingFile))
        {
            return new List<string>();
        }

        var lines = File.ReadAllLines(globalUsingFile);
        return ExtractNamespacesFromContent(lines);
    }

    /// <summary>
    /// Extracts namespaces from the content of a GlobalUsings.cs file.
    /// </summary>
    /// <param name="content">The file content as a string.</param>
    /// <returns>A list of namespaces found in the content.</returns>
    public static List<string> ExtractNamespacesFromContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return new List<string>();
        }

        var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        return ExtractNamespacesFromContent(lines);
    }

    /// <summary>
    /// Extracts namespaces from lines of a GlobalUsings.cs file.
    /// </summary>
    /// <param name="lines">The lines from the file.</param>
    /// <returns>A list of namespaces found in the lines.</returns>
    public static List<string> ExtractNamespacesFromContent(IEnumerable<string> lines)
    {
        if (lines is null)
        {
            throw new ArgumentNullException(nameof(lines));
        }

        var result = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.IndexOf("//", StringComparison.Ordinal) >= 0)
            {
                continue;
            }

            var trimLine = line;

            // Remove "global " prefix if present
            var globalIndex = trimLine.IndexOf("global ", StringComparison.Ordinal);
            if (globalIndex >= 0)
            {
                trimLine = trimLine.Substring(globalIndex + 7);
            }

            // Remove "using " prefix if present
            var usingIndex = trimLine.IndexOf("using ", StringComparison.Ordinal);
            if (usingIndex >= 0)
            {
                trimLine = trimLine.Substring(usingIndex + 6);
            }

            // Remove semicolon
            var semicolonIndex = trimLine.IndexOf(';');
            if (semicolonIndex >= 0)
            {
                trimLine = trimLine.Substring(0, semicolonIndex);
            }

            trimLine = trimLine.Trim();

            if (string.IsNullOrWhiteSpace(trimLine))
            {
                continue;
            }

            if (!ContainsOrdinal(result, trimLine))
            {
                result.Add(trimLine);
            }
        }

        return result;
    }

    /// <summary>
    /// Merges two lists of namespaces, removing duplicates.
    /// Required namespaces take precedence.
    /// </summary>
    /// <param name="requiredNamespaces">The required namespaces that must be included.</param>
    /// <param name="existingNamespaces">The existing namespaces to merge with.</param>
    /// <returns>A merged list of namespaces without duplicates.</returns>
    public static List<string> MergeNamespaces(
        IEnumerable<string> requiredNamespaces,
        IEnumerable<string> existingNamespaces)
    {
        if (requiredNamespaces is null)
        {
            throw new ArgumentNullException(nameof(requiredNamespaces));
        }

        if (existingNamespaces is null)
        {
            throw new ArgumentNullException(nameof(existingNamespaces));
        }

        var result = new List<string>();

        foreach (var item in requiredNamespaces)
        {
            if (!ContainsOrdinal(result, item))
            {
                result.Add(item);
            }
        }

        foreach (var item in existingNamespaces)
        {
            if (!ContainsOrdinal(result, item))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static void AppendNamespacesWithGrouping(
        StringBuilder sb,
        List<string> namespaces,
        bool addNamespaceSeparator)
    {
        if (!addNamespaceSeparator)
        {
            foreach (var item in namespaces)
            {
                sb.AppendLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "global using {0};",
                    item));
            }

            return;
        }

        var lastNamespacePrefix = string.Empty;
        foreach (var item in namespaces)
        {
            var parts = item.Split('.');
            var namespacePrefix = parts.Length > 0 ? parts[0] : string.Empty;

            if (string.IsNullOrEmpty(lastNamespacePrefix))
            {
                lastNamespacePrefix = namespacePrefix;
            }
            else if (!lastNamespacePrefix.Equals(namespacePrefix, StringComparison.Ordinal))
            {
                lastNamespacePrefix = namespacePrefix;
                sb.AppendLine();
            }

            sb.AppendLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "global using {0};",
                item));
        }
    }

    private static bool ContainsOrdinal(
        List<string> list,
        string item)
    {
        foreach (var existing in list)
        {
            if (string.Equals(existing, item, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
