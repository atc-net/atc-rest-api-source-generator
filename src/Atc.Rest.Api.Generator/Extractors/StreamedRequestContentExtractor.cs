namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Emits the C# clients' <c>StreamedRequestContent&lt;T&gt;</c>: the <c>HttpContent</c> of a request body sent as a
/// stream of records - JSON Lines (one compact JSON value and a line feed per record) or JSON Text Sequences (a record
/// separator, the value and a line feed). It writes each record as the caller's <c>IAsyncEnumerable&lt;T&gt;</c> yields
/// it, with no length (chunked), and sends what it has whenever the source makes it wait - so a live source reaches the
/// server record by record and a fast one in full buffers. It is sent once: a second send (a retry, a redirect) would
/// enumerate the source again, so it is refused.
/// </summary>
public static class StreamedRequestContentExtractor
{
    /// <summary>
    /// The name of the emitted type.
    /// </summary>
    public const string TypeName = "StreamedRequestContent";

    /// <summary>
    /// The expression that creates the content of a streamed request body (the record type is inferred from the
    /// records); <see langword="null"/> content when an optional body is not given.
    /// </summary>
    /// <param name="recordsExpression">The records, e.g. <c>parameters.Request</c>.</param>
    /// <param name="serializerExpression">The JSON options (typed client) or the contract serializer (endpoint per
    /// operation) expression.</param>
    /// <param name="mediaType">The media type sent.</param>
    /// <param name="isRequired">Whether the body is required (an optional one may be null: no body).</param>
    /// <param name="namespacePrefix">The type's namespace with a trailing dot, or empty when the file imports it.</param>
    /// <returns>The expression.</returns>
    public static string CreateContentExpression(
        string recordsExpression,
        string serializerExpression,
        string mediaType,
        bool isRequired,
        string namespacePrefix = "")
    {
        var create = $"{namespacePrefix}{TypeName}.Create({recordsExpression}, {serializerExpression}, {CSharpLiteralHelper.ToStringLiteral(mediaType)})";
        return isRequired
            ? create
            : $"({recordsExpression} is null ? null : {create})";
    }

    /// <summary>
    /// Generates <c>StreamedRequestContent&lt;T&gt;</c>.
    /// </summary>
    /// <param name="projectName">The project name.</param>
    /// <param name="withJsonOptions">True for the typed client, which serializes with its <c>JsonSerializerOptions</c>
    /// (and the generated <c>TypeInfo&lt;T&gt;</c>); false for the endpoint-per-operation client, which serializes with
    /// Atc.Rest.Client's <c>IContractSerializer</c>.</param>
    /// <returns>The source.</returns>
    public static string GenerateContent(
        string projectName,
        bool withJsonOptions)
    {
        var constructor = withJsonOptions
            ? $$"""
                /// <summary>
                /// The records, serialized with the client's JSON options (always compact: a record is one line).
                /// </summary>
                public {{TypeName}}(
                    global::System.Collections.Generic.IAsyncEnumerable<T> records,
                    global::System.Text.Json.JsonSerializerOptions options,
                    string mediaType)
                    : this(records, mediaType)
                {
                    global::System.ArgumentNullException.ThrowIfNull(options);
                    var compact = options.WriteIndented
                        ? new global::System.Text.Json.JsonSerializerOptions(options) { WriteIndented = false }
                        : options;
                    var typeInfo = compact.TypeInfo<T>();
                    write = (stream, record, cancellationToken) => new global::System.Threading.Tasks.ValueTask(
                        global::System.Text.Json.JsonSerializer.SerializeAsync(stream, record, typeInfo, cancellationToken));
                }
            """
            : $$"""
                /// <summary>
                /// The records, serialized with the client's contract serializer. A line break of an indenting serializer is
                /// whitespace (JSON escapes one inside a string), so a record stays one line.
                /// </summary>
                public {{TypeName}}(
                    global::System.Collections.Generic.IAsyncEnumerable<T> records,
                    global::Atc.Rest.Client.Serialization.IContractSerializer serializer,
                    string mediaType)
                    : this(records, mediaType)
                {
                    global::System.ArgumentNullException.ThrowIfNull(serializer);
                    write = (stream, record, cancellationToken) =>
                    {
                        var json = record is null ? "null" : serializer.Serialize(record);
                        if (!isSequence)
                        {
                            json = json.Replace('\r', ' ').Replace('\n', ' ');
                        }

                        return stream.WriteAsync(global::System.Text.Encoding.UTF8.GetBytes(json), cancellationToken);
                    };
                }
            """;

        return $$"""
            // <auto-generated />
            #nullable enable

            namespace {{NamespaceBuilder.BuildBase(projectName)}}.Streaming;

            /// <summary>
            /// Creates the content of a request body sent as a stream of records, its record type inferred.
            /// </summary>
            [global::System.CodeDom.Compiler.GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            internal static class {{TypeName}}
            {
                /// <summary>
                /// The records, sent as the media type (JSON Lines, or a JSON Text Sequence for application/json-seq).
                /// </summary>
                public static {{TypeName}}<T> Create<T>(
                    global::System.Collections.Generic.IAsyncEnumerable<T> records,
                    {{(withJsonOptions ? "global::System.Text.Json.JsonSerializerOptions options" : "global::Atc.Rest.Client.Serialization.IContractSerializer serializer")}},
                    string mediaType)
                    => new(records, {{(withJsonOptions ? "options" : "serializer")}}, mediaType);
            }

            /// <summary>
            /// A request body sent as a stream of records: JSON Lines (one compact JSON value and a line feed per record) or JSON
            /// Text Sequences (a record separator, the value and a line feed). Each record is written as the source yields it,
            /// with no length (chunked); what is written is sent whenever the source makes it wait.
            /// </summary>
            /// <remarks>
            /// It is sent once: the records are read as they are written, so a retry or a redirect that sends it again is
            /// refused rather than sending other records.
            /// </remarks>
            /// <typeparam name="T">The record.</typeparam>
            [global::System.CodeDom.Compiler.GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            internal sealed class {{TypeName}}<T> : global::System.Net.Http.HttpContent
            {
                private static readonly byte[] LineFeed = [0x0A];
                private static readonly byte[] RecordSeparator = [0x1E];

                private readonly global::System.Collections.Generic.IAsyncEnumerable<T> records;
                private readonly bool isSequence;
                private global::System.Func<global::System.IO.Stream, T, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask> write = null!;
                private int sent;

            {{constructor}}

                private {{TypeName}}(
                    global::System.Collections.Generic.IAsyncEnumerable<T> records,
                    string mediaType)
                {
                    global::System.ArgumentNullException.ThrowIfNull(records);
                    global::System.ArgumentException.ThrowIfNullOrEmpty(mediaType);
                    this.records = records;
                    var baseType = mediaType.Split(';')[0].Trim();
                    isSequence = baseType.Equals("application/json-seq", global::System.StringComparison.OrdinalIgnoreCase) ||
                                 baseType.EndsWith("+json-seq", global::System.StringComparison.OrdinalIgnoreCase);
                    Headers.ContentType = global::System.Net.Http.Headers.MediaTypeHeaderValue.Parse(mediaType);
                }

                /// <inheritdoc />
                protected override global::System.Threading.Tasks.Task SerializeToStreamAsync(
                    global::System.IO.Stream stream,
                    global::System.Net.TransportContext? context)
                    => SerializeToStreamAsync(stream, context, global::System.Threading.CancellationToken.None);

                /// <inheritdoc />
                protected override async global::System.Threading.Tasks.Task SerializeToStreamAsync(
                    global::System.IO.Stream stream,
                    global::System.Net.TransportContext? context,
                    global::System.Threading.CancellationToken cancellationToken)
                {
                    if (global::System.Threading.Interlocked.Exchange(ref sent, 1) == 1)
                    {
                        throw new global::System.InvalidOperationException(
                            "A streamed request body is sent once: its records are read as they are written, so a retry or a redirect cannot send them again. " +
                            "The first attempt's failure is the cause - turn off the retries of an HttpClient resilience handler for this request.");
                    }

                    var enumerator = records.GetAsyncEnumerator(cancellationToken);
                    var next = default(global::System.Threading.Tasks.ValueTask<bool>);
                    var pending = false;
                    var disposeLater = false;
                    try
                    {
                        next = enumerator.MoveNextAsync();
                        pending = true;
                        while (true)
                        {
                            // The source makes the request wait: what is written goes to the server now
                            if (!next.IsCompleted)
                            {
                                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                            }

                            pending = false;
                            if (!await next.ConfigureAwait(false))
                            {
                                break;
                            }

                            if (isSequence)
                            {
                                await stream.WriteAsync(RecordSeparator, cancellationToken).ConfigureAwait(false);
                            }

                            await write(stream, enumerator.Current, cancellationToken).ConfigureAwait(false);
                            await stream.WriteAsync(LineFeed, cancellationToken).ConfigureAwait(false);
                            next = enumerator.MoveNextAsync();
                            pending = true;
                        }
                    }
                    catch when (pending)
                    {
                        // The send failed while the source was still making its next record: the source is disposed once
                        // that record has come (an iterator cannot be disposed while it runs), and the send's own error
                        // goes to the caller - not one about the disposal
                        disposeLater = true;
                        var source = enumerator;
                        _ = next.AsTask().ContinueWith(
                            _ => source.DisposeAsync().AsTask(),
                            global::System.Threading.CancellationToken.None,
                            global::System.Threading.Tasks.TaskContinuationOptions.None,
                            global::System.Threading.Tasks.TaskScheduler.Default).Unwrap();
                        throw;
                    }
                    finally
                    {
                        if (!disposeLater)
                        {
                            await enumerator.DisposeAsync().ConfigureAwait(false);
                        }
                    }

                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                /// <inheritdoc />
                protected override bool TryComputeLength(out long length)
                {
                    length = 0;
                    return false;
                }
            }
            """;
    }
}