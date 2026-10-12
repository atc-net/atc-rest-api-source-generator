namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// A response that is a model or a file (a 200 that declares <c>application/json</c> and <c>application/pdf</c>): the
/// operation's data is <c>Model | Blob</c>, told apart by the answer's Content-Type. Fetch already reads a body by its
/// Content-Type; Axios asks for a Blob and reads a model's body back as text, through its own response transforms. The
/// ApiClient extractors emit the <c>modelOrFile</c> request option through these helpers.
/// </summary>
public static class TypeScriptModelOrFileExtractor
{
    /// <summary>
    /// Whether any operation of the document answers a model or a file.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns><see langword="true"/> when the ApiClient needs the <c>modelOrFile</c> request option.</returns>
    public static bool HasModelOrFile(OpenApiDocument openApiDoc)
        => openApiDoc?.Paths is not null &&
           openApiDoc.Paths.Values
               .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
               .Any(operation => operation.GetModelOrFileMediaTypes().Count > 0);

    /// <summary>
    /// Appends the <c>modelOrFile</c> member of the <c>RequestOptions</c> interface.
    /// </summary>
    public static void AppendRequestOption(
        StringBuilder sb,
        bool hasModelOrFile)
    {
        ArgumentNullException.ThrowIfNull(sb);
        if (!hasModelOrFile)
        {
            return;
        }

        sb.AppendLine("  /** A model or a file (the generated client passes it): a file answer (neither JSON nor XML) is a Blob, a model's is the model. */");
        sb.AppendLine("  modelOrFile?: boolean;");
    }

    /// <summary>
    /// The Axios <c>responseType</c> of a request - always bytes, so a text body is decoded by the charset its
    /// Content-Type names (the adapters differ: Node's reads every text as UTF-8) - and its <c>bodyType</c>: what the
    /// operation reads (JSON, text, or a file's bytes), which <c>transformResponse</c> goes by.
    /// </summary>
    public static string AxiosResponseType(bool hasModelOrFile)
        => hasModelOrFile
            ? "      responseType: 'arraybuffer',\n      bodyType: options?.responseType === 'blob' || options?.modelOrFile ? 'arraybuffer' : options?.responseType ?? 'auto',"
            : "      responseType: 'arraybuffer',\n      bodyType: options?.responseType === 'blob' ? 'arraybuffer' : options?.responseType ?? 'auto',";

    /// <summary>
    /// Appends the Axios read of a model answer that was asked for as bytes: its body (JSON or XML) is read back as text
    /// and transformed like any body, while a file answer stays a Blob.
    /// </summary>
    public static void AppendAxiosModelBodyRead(
        StringBuilder sb,
        bool hasModelOrFile)
    {
        ArgumentNullException.ThrowIfNull(sb);
        if (!hasModelOrFile)
        {
            return;
        }

        sb.AppendLine("    // A model or a file: a file stays a Blob; a model's body (JSON or XML) is read as text and transformed like any body");
        sb.AppendLine("    if (options?.modelOrFile && response.status >= 200 && response.status < 300 && typeof Blob !== 'undefined' && response.data instanceof Blob) {");
        sb.AppendLine("      const modelContentType = String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '').toLowerCase();");
        sb.AppendLine("      if (/^(application\\/json|application\\/xml|text\\/xml)$|\\+(json|xml)$/.test((modelContentType.split(';')[0] ?? '').trim())) {");
        sb.AppendLine("        let body: unknown = decodeText(await response.data.arrayBuffer(), modelContentType);");
        sb.AppendLine("        for (const transform of ([] as unknown[]).concat(this.client.defaults.transformResponse ?? []) as ((this: unknown, data: unknown, headers: unknown) => unknown)[]) {");
        sb.AppendLine("          body = transform.call({}, body, response.headers);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        (response as { data: unknown }).data = body;");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}