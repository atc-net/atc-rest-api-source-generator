namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// A response that is a model or a file (a 200 that declares <c>application/json</c> and <c>application/pdf</c>): the
/// operation's data is <c>Model | Blob</c>, told apart by the answer's Content-Type. Fetch already reads a body by its
/// Content-Type; Axios asks for a Blob and reads a model's body back as text, through its own response transforms.
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
    /// Adds the <c>modelOrFile</c> request option to a generated ApiClient by anchored text edits.
    /// </summary>
    /// <param name="content">The generated ApiClient.</param>
    /// <param name="isAxios">Whether the client uses Axios (else fetch).</param>
    /// <returns>The ApiClient with the option.</returns>
    public static string AugmentApiClient(
        string content,
        bool isAxios)
    {
        ArgumentNullException.ThrowIfNull(content);

        var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var s = content.Replace("\r\n", "\n", StringComparison.Ordinal);

        s = Replace(s, "  arms?: readonly string[];\n", "  arms?: readonly string[];\n  /** A model or a file (the generated client passes it): a file answer (neither JSON nor XML) is a Blob, a model's is the model. */\n  modelOrFile?: boolean;\n");

        if (isAxios)
        {
            s = Replace(s, "responseType: options?.responseType === 'blob' ? 'arraybuffer'", "responseType: options?.responseType === 'blob' || options?.modelOrFile ? 'arraybuffer'");
            s = Replace(s, "    return fitToArms(await this.handleResponse<T>(response", "    // A model or a file: a file stays a Blob; a model's body (JSON or XML) is read as text and transformed like any body\n    if (options?.modelOrFile && response.status >= 200 && response.status < 300 && typeof Blob !== 'undefined' && response.data instanceof Blob) {\n      const modelContentType = String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '').toLowerCase();\n      if (/^(application\\/json|application\\/xml|text\\/xml)$|\\+(json|xml)$/.test((modelContentType.split(';')[0] ?? '').trim())) {\n        let body: unknown = await response.data.text();\n        for (const transform of ([] as unknown[]).concat(this.client.defaults.transformResponse ?? []) as ((this: unknown, data: unknown, headers: unknown) => unknown)[]) {\n          body = transform.call({ responseType: 'json' }, body, response.headers);\n        }\n\n        (response as { data: unknown }).data = body;\n      }\n    }\n\n    return fitToArms(await this.handleResponse<T>(response");
        }

        return s.Replace("\n", newline, StringComparison.Ordinal);

        static string Replace(
            string text,
            string anchor,
            string replacement)
        {
            var index = text.IndexOf(anchor, StringComparison.Ordinal);
            if (index < 0 || text.IndexOf(anchor, index + 1, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("The generated ApiClient no longer has exactly one: " + anchor);
            }

            return string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + anchor.Length));
        }
    }
}