namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Emits a <c>Servers.ts</c> file containing the OpenAPI <c>servers:</c> list as a typed
/// <c>const</c> object plus a <c>ServerName</c> union of its keys. The consumer chooses
/// the active server by reading from <c>Servers</c> and passing it to
/// <c>new ApiClient(Servers.production, …)</c>. Skipped entirely when the spec declares
/// fewer than two servers — a single-server spec keeps the existing single-baseUrl
/// constructor pattern without the extra indirection.
/// </summary>
public static class TypeScriptServersExtractor
{
    /// <summary>
    /// Generates <c>Servers.ts</c> content, or <c>null</c> when the spec has fewer than
    /// two server entries. Server keys are derived from the <c>description</c> field via
    /// camelCase conversion; entries without a description or whose key collides with a
    /// sibling fall back to <c>server1</c>, <c>server2</c>, … to keep the object literal
    /// shape valid.
    /// </summary>
    public static string? Generate(
        OpenApiDocument openApiDoc,
        string? headerContent)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        if (openApiDoc.Servers == null || openApiDoc.Servers.Count < 2)
        {
            return null;
        }

        var entries = new List<(string Key, string Url)>();
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < openApiDoc.Servers.Count; i++)
        {
            var server = openApiDoc.Servers[i];
            if (server.Url == null)
            {
                continue;
            }

            var url = ServerUrlHelper.ResolveServerVariables(server.Url, server.Variables);

            // Derive a TS-safe key: prefer the description (camelCased), else `serverN`.
            var key = DeriveServerKey(server.Description, fallbackIndex: i + 1);

            // De-dup: two servers with the same description must not collide on the key.
            if (!usedKeys.Add(key))
            {
                var suffix = 2;
                while (!usedKeys.Add(key + suffix))
                {
                    suffix++;
                }

                key = key + suffix;
            }

            entries.Add((key, url));
        }

        if (entries.Count < 2)
        {
            return null;
        }

        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("export const Servers = {");
        foreach (var (key, url) in entries)
        {
            sb.Append("  ").Append(key).Append(": '").Append(url).AppendLine("',");
        }

        sb.AppendLine("} as const;");
        sb.AppendLine();
        sb.AppendLine("export type ServerName = keyof typeof Servers;");

        return sb.ToString();
    }

    private static string DeriveServerKey(
        string? description,
        int fallbackIndex)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "server" + fallbackIndex.ToString(CultureInfo.InvariantCulture);
        }

        // camelCase the description: "Local development" -> "localDevelopment".
        var camel = description.ToCamelCase();

        // Final safety net: if ToCamelCase produced something that isn't a valid TS
        // identifier start (digit, empty), or it collides with a reserved word, fall
        // back to the indexed key. ToTypeScriptIdentifier already handles the reserved-
        // word + leading-digit cases via prefix.
        if (string.IsNullOrEmpty(camel))
        {
            return "server" + fallbackIndex.ToString(CultureInfo.InvariantCulture);
        }

        return camel.ToTypeScriptIdentifier();
    }
}