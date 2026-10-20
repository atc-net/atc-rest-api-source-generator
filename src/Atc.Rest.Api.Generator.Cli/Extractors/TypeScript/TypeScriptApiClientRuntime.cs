namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The parts of the generated <c>ApiClient</c> runtime the Fetch and the Axios client write alike: a multipart/mixed
/// stream read part by part (and the JSON framings after it), and a failed response's result arm.
/// </summary>
internal static class TypeScriptApiClientRuntime
{
    /// <summary>
    /// The multipart/mixed and JSON framings of <c>requestStream</c>, after the response's <c>contentType</c> is read.
    /// </summary>
    /// <param name="sb">The builder.</param>
    /// <param name="multipartParse">The expression a part's JSON body is parsed with.</param>
    public static void AppendMultipartFraming(
        StringBuilder sb,
        string multipartParse)
    {
        sb.AppendLine("      const m = /boundary=(\"?)([^\";]+)\\1/i.exec(contentType);");
        sb.AppendLine("      // Without a boundary the parts cannot be found: an error, not a guess");
        sb.AppendLine("      if (!m) throw new Error('A multipart/mixed response without a boundary in its Content-Type cannot be split into its parts.');");
        sb.AppendLine("      const delimiter = '--' + m[2];");
        sb.AppendLine("      let buf = '';");
        sb.AppendLine("      let finished = false;");
        sb.AppendLine("      try {");
        sb.AppendLine("        while (true) {");
        sb.AppendLine("          const { done, value } = await reader.read();");
        sb.AppendLine("          if (done) {");
        sb.AppendLine("            finished = true;");
        sb.AppendLine("            // A last part the stream ended on without its closing boundary (the server failed mid-stream): what it sent is yielded");
        sb.AppendLine("            if (buf.startsWith(delimiter)) {");
        sb.AppendLine("              const bodyText = multipartBody(buf.substring(delimiter.length)) ?? '';");
        sb.AppendLine("              if (bodyText.length > 0 && scanJsonValue(bodyText, 0, true) === bodyText.length) yield JSON.parse(bodyText) as T;");
        sb.AppendLine("            }");
        sb.AppendLine("            break;");
        sb.AppendLine("          }");
        sb.AppendLine("          buf += decoder.decode(value, { stream: true });");
        sb.AppendLine("          // Discard preamble (everything before the first delimiter boundary).");
        sb.AppendLine("          const first = buf.indexOf(delimiter);");
        sb.AppendLine("          if (first === -1) {");
        sb.AppendLine("            if (buf.length > delimiter.length) buf = buf.substring(buf.length - delimiter.length);");
        sb.AppendLine("            continue;");
        sb.AppendLine("          }");
        sb.AppendLine("          if (first > 0) buf = buf.substring(first);");
        sb.AppendLine("          // Yield each complete part; buf always starts at a delimiter after this loop.");
        sb.AppendLine("          while (buf.startsWith(delimiter)) {");
        sb.AppendLine("            const partStart = delimiter.length;");
        sb.AppendLine("            const next = buf.indexOf(delimiter, partStart);");
        sb.AppendLine("            if (next === -1) {");
        sb.AppendLine("              guardRecord(buf.length - partStart);");
        sb.AppendLine("              // A part whose body is complete JSON is yielded now, not at the next boundary - a server that fails after it never sends one");
        sb.AppendLine("              const pending = buf.substring(partStart);");
        sb.AppendLine("              const pendingBody = multipartBody(pending) ?? '';");
        sb.AppendLine("              if (pendingBody.length > 0 && pending.endsWith('\\r\\n') && scanJsonValue(pendingBody, 0, true) === pendingBody.length) {");
        sb.AppendLine("                yield JSON.parse(pendingBody) as T;");
        sb.AppendLine("                buf = '';");
        sb.AppendLine("              }");
        sb.AppendLine("              break;");
        sb.AppendLine("            }");
        sb.AppendLine("            guardRecord(next - partStart);");
        sb.AppendLine("            const part = buf.substring(partStart, next);");
        sb.AppendLine("            buf = buf.substring(next);");
        sb.AppendLine("            const bodyText = multipartBody(part);");
        sb.AppendLine("            if (bodyText === null) continue;");
        sb.Append("            if (bodyText.length > 0) yield ").Append(multipartParse).AppendLine(" as T;");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine("      } finally {");
        sb.AppendLine("        await releaseReader(reader, finished);");
        sb.AppendLine("      }");
        sb.AppendLine("      return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // JSON Lines, JSON Text Sequence, or a JSON array read one element at a time");
        sb.AppendLine("    if (framing === 'json-lines') {");
        sb.AppendLine("      yield* parseJsonLinesStream<T>(reader, decoder);");
        sb.AppendLine("    } else if (framing === 'json-seq') {");
        sb.AppendLine("      yield* parseJsonSeqStream<T>(reader, decoder);");
        sb.AppendLine("    } else {");
        sb.AppendLine("      yield* parseJsonArrayStream<T>(reader, decoder);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// The result arm of a failed response, after its <c>message</c> is built: badRequest with the validation errors,
    /// else the arm of its status.
    /// </summary>
    /// <param name="sb">The builder.</param>
    /// <param name="hasWireNames">Whether error bodies are renamed to the models' names.</param>
    public static void AppendErrorArms(
        StringBuilder sb,
        bool hasWireNames)
    {
        // With renaming, `body` is the declared error model under the models' names, while the message, the
        // ProblemDetails fields and the validation errors (keyed by the field names sent) come from the wire body.
        var errorBodyArguments = "errorBody";
        if (hasWireNames)
        {
            sb.AppendLine("    const shape = wire?.errors?.[String(response.status)] ?? wire?.errors?.['default'];");
            sb.AppendLine("    const body = shape ? fromWire(errorBody, shape) : errorBody;");
            sb.AppendLine();
            errorBodyArguments = "body, errorBody";
        }

        sb.AppendLine("    if (response.status === 400) {");
        sb.AppendLine("      // Always badRequest: a 400 without validation errors carries an empty errors map.");
        sb.AppendLine("      const errors = errorBody !== null && typeof errorBody === 'object' && typeof (errorBody as { errors?: unknown }).errors === 'object' && (errorBody as { errors?: unknown }).errors !== null");
        sb.AppendLine("        ? (errorBody as { errors: Record<string, string[]> }).errors");
        sb.AppendLine("        : {};");
        sb.AppendLine("      return {");
        sb.AppendLine("        status: 'badRequest',");
        sb.Append("        error: new ValidationError(response.status, response.statusText, message, errors, response, ").Append(errorBodyArguments).AppendLine("),");
        sb.AppendLine("        response,");
        sb.AppendLine("      };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    const apiError = new ApiError(response.status, response.statusText, message, response, ").Append(errorBodyArguments).AppendLine(");");
        sb.AppendLine();
        sb.AppendLine("    switch (response.status) {");
        sb.AppendLine("      case 401:");
        sb.AppendLine("        return { status: 'unauthorized', error: apiError, response };");
        sb.AppendLine("      case 403:");
        sb.AppendLine("        return { status: 'forbidden', error: apiError, response };");
        sb.AppendLine("      case 404:");
        sb.AppendLine("        return { status: 'notFound', error: apiError, response };");
        sb.AppendLine("      case 409:");
        sb.AppendLine("        return { status: 'conflict', error: apiError, response };");
        sb.AppendLine("      case 422:");
        sb.AppendLine("        return { status: 'unprocessableEntity', error: apiError, response };");
        sb.AppendLine("      case 429:");
        sb.AppendLine("        return { status: 'tooManyRequests', error: apiError, response };");
        sb.AppendLine("      default:");
        sb.AppendLine("        return response.status >= 400 && response.status < 500");
        sb.AppendLine("          ? { status: 'clientError', error: apiError, response }");
        sb.AppendLine("          : { status: 'serverError', error: apiError, response };");
        sb.AppendLine("    }");
    }
}