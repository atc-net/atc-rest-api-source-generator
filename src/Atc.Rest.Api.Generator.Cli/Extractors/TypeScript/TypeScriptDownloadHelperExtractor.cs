namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates the download helper: the file name of a downloaded file, read from the <c>Content-Disposition</c> header
/// of a result's <c>response</c> (<c>filename*</c> first, RFC 6266 / RFC 5987) - for the Fetch <c>Response</c> and the
/// Axios response alike.
/// </summary>
public static class TypeScriptDownloadHelperExtractor
{
    /// <summary>
    /// Generates the download.ts utility file content.
    /// </summary>
    /// <param name="headerContent">The file header, or <see langword="null"/>.</param>
    /// <returns>The file content.</returns>
    public static string Generate(string? headerContent)
    {
        var sb = new StringBuilder();

        sb.Append(headerContent);

        sb.AppendLine("/**");
        sb.AppendLine(" * The file name of a downloaded file, from the response's Content-Disposition header: the RFC 5987");
        sb.AppendLine(" * `filename*` (UTF-8, e.g. `Rapport-Å.pdf`) first, then `filename`; `undefined` for an inline file.");
        sb.AppendLine(" *");
        sb.AppendLine(" * Works with the Fetch `Response` and the Axios response (`result.response` of a download).");
        sb.AppendLine(" * In a browser the API must expose the header to scripts (`Access-Control-Expose-Headers: Content-Disposition`).");
        sb.AppendLine(" */");
        sb.AppendLine("export function getDownloadFileName(response: { headers: unknown }): string | undefined {");
        sb.AppendLine("  const headers = response.headers as { get?: (name: string) => unknown } & Record<string, unknown>;");
        sb.AppendLine("  const raw = typeof headers?.get === 'function'");
        sb.AppendLine("    ? headers.get('content-disposition')");
        sb.AppendLine("    : headers?.['content-disposition'] ?? headers?.['Content-Disposition'];");
        sb.AppendLine("  if (typeof raw !== 'string' || raw.length === 0) {");
        sb.AppendLine("    return undefined;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  const extended = /filename\\*\\s*=\\s*[^']*'[^']*'([^;]+)/i.exec(raw)?.[1];");
        sb.AppendLine("  if (extended !== undefined) {");
        sb.AppendLine("    try {");
        sb.AppendLine("      return decodeURIComponent(extended.trim());");
        sb.AppendLine("    } catch {");
        sb.AppendLine("      // A malformed percent-encoding: fall back to the plain filename.");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  const plain = /filename\\s*=\\s*(\"([^\"]*)\"|[^;]+)/i.exec(raw);");
        sb.AppendLine("  const value = plain?.[2] ?? plain?.[1];");
        sb.AppendLine("  return value !== undefined ? value.trim() : undefined;");
        sb.AppendLine("}");

        return sb.ToString();
    }
}