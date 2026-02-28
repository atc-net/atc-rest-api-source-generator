namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates the ApiService.ts root client orchestrator that wraps all segment clients.
/// </summary>
public static class TypeScriptApiServiceExtractor
{
    /// <summary>
    /// Generates the content for ApiService.ts.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated file header.</param>
    /// <param name="clientClassNames">List of segment client class names (e.g., AccountsClient, UsersClient).</param>
    /// <returns>The TypeScript file content.</returns>
    public static string Generate(
        string? headerContent,
        List<string> clientClassNames)
    {
        ArgumentNullException.ThrowIfNull(clientClassNames);

        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        AppendImports(sb, clientClassNames);
        AppendClass(sb, clientClassNames);

        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        List<string> clientClassNames)
    {
        sb.AppendLine("import { ApiClient, ApiClientOptions } from './ApiClient';");

        foreach (var className in clientClassNames)
        {
            sb.Append("import { ").Append(className).Append(" } from './").Append(className).AppendLine("';");
        }

        sb.AppendLine();
    }

    private static void AppendClass(
        StringBuilder sb,
        List<string> clientClassNames)
    {
        sb.AppendLine("export class ApiService {");

        // Readonly properties
        foreach (var className in clientClassNames)
        {
            var propertyName = GetPropertyName(className);
            sb.Append("  readonly ").Append(propertyName).Append(": ").Append(className).AppendLine(";");
        }

        sb.AppendLine();

        // Constructor
        sb.AppendLine("  constructor(baseUrl: string, options?: ApiClientOptions) {");
        sb.AppendLine("    const api = new ApiClient(baseUrl, options);");

        foreach (var className in clientClassNames)
        {
            var propertyName = GetPropertyName(className);
            sb.Append("    this.").Append(propertyName).Append(" = new ").Append(className).AppendLine("(api);");
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    /// <summary>
    /// Derives a property name from a client class name by stripping the "Client" suffix
    /// and converting to camelCase. For example: "AccountsClient" → "accounts".
    /// </summary>
    private static string GetPropertyName(string className)
    {
        var name = className;
        if (name.EndsWith("Client", StringComparison.Ordinal))
        {
            name = name[..^"Client".Length];
        }

        return name.ToCamelCase();
    }
}