namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// A request body sent as a stream of records - JSON Lines (<c>application/jsonl</c> and its aliases) or JSON Text
/// Sequences (<c>application/json-seq</c>) - in the TypeScript clients. The caller passes the records as an
/// <c>AsyncIterable&lt;T&gt;</c> or an <c>Iterable&lt;T&gt;</c> (an array); the generated client passes
/// <c>sequence: 'json-lines' | 'json-seq'</c>. The Fetch client streams an async source with <c>fetch</c>'s
/// <c>duplex: 'half'</c> where the runtime can (outside a browser - Node, Deno, Bun - or always with
/// <c>requestStreaming: 'stream'</c>), and buffers it otherwise; a list is always sent whole. The Axios client buffers.
/// </summary>
internal static class TypeScriptRequestRecords
{
    /// <summary>
    /// Whether any operation of the document takes a streamed request body (the runtime pieces are emitted only then).
    /// </summary>
    public static bool HasSequenceBodies(OpenApiDocument openApiDoc)
        => openApiDoc?.Paths?.Values
            .Where(pathItem => pathItem?.Operations is not null)
            .SelectMany(pathItem => pathItem.Operations!.Values)
            .Any(operation => operation?.GetStreamedRequestBody() is not null) == true;

    /// <summary>
    /// The TypeScript type of a streamed request body of records of <paramref name="itemType"/>.
    /// </summary>
    public static string BodyType(string itemType)
        => $"AsyncIterable<{itemType}> | Iterable<{itemType}>";

    /// <summary>
    /// The request option that names the framing of a streamed request body.
    /// </summary>
    public static string SequenceOf(string mediaType)
        => StreamingMediaType.Classify(mediaType) == StreamingFraming.JsonSequence ? "json-seq" : "json-lines";

    /// <summary>
    /// The <c>sequence</c> request option's documentation and type.
    /// </summary>
    public static void AppendRequestOption(StringBuilder sb)
    {
        sb.AppendLine("  /** The body is a stream of records, written as JSON Lines or a JSON Text Sequence (the generated client passes it). */");
        sb.AppendLine("  sequence?: RequestSequence;");
    }

    /// <summary>
    /// The Fetch client's <c>requestStreaming</c> option.
    /// </summary>
    public static void AppendClientOption(StringBuilder sb)
    {
        sb.AppendLine("  /**");
        sb.AppendLine("   * How a body of records from an async source is sent: 'stream' writes each record as the source yields it (fetch with");
        sb.AppendLine("   * duplex: 'half' - a browser streams a request body only over HTTP/2), 'buffer' collects the records and sends them");
        sb.AppendLine("   * in one body, 'auto' (the default) streams outside a browser (Node, Deno, Bun) and buffers in one. A list is always");
        sb.AppendLine("   * sent whole.");
        sb.AppendLine("   */");
        sb.AppendLine("  requestStreaming?: 'auto' | 'stream' | 'buffer';");
    }

    /// <summary>
    /// The module-level helpers: the framing type, a record on the wire, a buffered body and - for Fetch - a streamed
    /// body and the runtime check.
    /// </summary>
    public static void AppendRuntime(
        StringBuilder sb,
        bool streams)
    {
        sb.AppendLine("""
            /** How a request body of records is framed: JSON Lines (one JSON value per line) or a JSON Text Sequence (RFC 7464). */
            export type RequestSequence = 'json-lines' | 'json-seq';

            /** One record as it goes on the wire: its JSON on one line (after a record separator in a JSON Text Sequence). */
            function encodeRecord(record: unknown, sequence: RequestSequence): string {
              return (sequence === 'json-seq' ? '\u001e' : '') + JSON.stringify(record) + '\n';
            }

            /** The records in one body: a list, or an async source that is not streamed. */
            async function bufferRecords(
              records: AsyncIterable<unknown> | Iterable<unknown>,
              sequence: RequestSequence,
              toRecord: (record: unknown) => unknown,
            ): Promise<string> {
              let text = '';
              for await (const record of records) {
                text += encodeRecord(toRecord(record), sequence);
              }

              return text;
            }

            """);

        if (!streams)
        {
            return;
        }

        sb.AppendLine("""
            /** The records as a body written as the source yields them - each record is sent when it is read. */
            function recordStream(
              records: AsyncIterable<unknown>,
              sequence: RequestSequence,
              toRecord: (record: unknown) => unknown,
            ): ReadableStream<Uint8Array> {
              const encoder = new TextEncoder();
              const iterator = records[Symbol.asyncIterator]();
              return new ReadableStream<Uint8Array>({
                async pull(controller) {
                  const next = await iterator.next();
                  if (next.done) {
                    controller.close();
                    return;
                  }

                  controller.enqueue(encoder.encode(encodeRecord(toRecord(next.value), sequence)));
                },
                async cancel() {
                  await iterator.return?.();
                },
              });
            }

            /** Outside a browser (Node, Deno, Bun) fetch streams a request body over HTTP/1.1 too; a browser or a worker only over HTTP/2. */
            function canStreamRequestBodies(): boolean {
              const scope = globalThis as { window?: unknown; WorkerGlobalScope?: unknown };
              return typeof scope.window === 'undefined' && typeof scope.WorkerGlobalScope === 'undefined';
            }

            """);
    }

    /// <summary>
    /// The Fetch ApiClient's <c>recordsBody</c>: the Content-Type, then the records streamed or buffered.
    /// </summary>
    public static void AppendFetchRecordsBodyMethod(
        StringBuilder sb,
        bool hasWireNames)
    {
        var toRecord = hasWireNames
            ? "(record: unknown) => toWire(record, options.wire?.body)"
            : "(record: unknown) => record";
        sb.AppendLine("  /** A body of records: streamed as the source yields them, or buffered (a list; in a browser unless requestStreaming is 'stream'). */");
        sb.AppendLine("  private async recordsBody(options: RequestOptions, headers: Headers): Promise<{ body: BodyInit; streamed: boolean }> {");
        sb.AppendLine("    const sequence = options.sequence ?? 'json-lines';");
        sb.AppendLine("    headers.set('Content-Type', options.contentType ?? (sequence === 'json-seq' ? 'application/json-seq' : 'application/jsonl'));");
        sb.AppendLine("    const records = options.body as AsyncIterable<unknown> | Iterable<unknown>;");
        sb.Append("    const toRecord = ").Append(toRecord).AppendLine(";");
        sb.AppendLine("    const mode = this.options.requestStreaming ?? 'auto';");
        sb.AppendLine("    if (!Array.isArray(records) && Symbol.asyncIterator in Object(records) && (mode === 'stream' || (mode === 'auto' && canStreamRequestBodies()))) {");
        sb.AppendLine("      return { body: recordStream(records as AsyncIterable<unknown>, sequence, toRecord), streamed: true };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    return { body: await bufferRecords(records, sequence, toRecord), streamed: false };");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    /// <summary>
    /// The Axios body of records (always buffered): sets the Content-Type and <c>data</c>.
    /// </summary>
    public static void AppendAxiosRecordsBody(
        StringBuilder sb,
        bool hasWireNames,
        string indent)
    {
        var toRecord = hasWireNames
            ? "(record: unknown) => toWire(record, options.wire?.body)"
            : "(record: unknown) => record";
        sb.Append(indent).AppendLine("const sequence = options.sequence ?? 'json-lines';");
        sb.Append(indent).AppendLine("headers['Content-Type'] = options.contentType ?? (sequence === 'json-seq' ? 'application/json-seq' : 'application/jsonl');");

        // As bytes: axios encodes a string again for any media type that names json (application/jsonl does)
        sb.Append(indent).Append("data = new TextEncoder().encode(await bufferRecords(options.body as AsyncIterable<unknown> | Iterable<unknown>, sequence, ").Append(toRecord).AppendLine("));");
    }
}