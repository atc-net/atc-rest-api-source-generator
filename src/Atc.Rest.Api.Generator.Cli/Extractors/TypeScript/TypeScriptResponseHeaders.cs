namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The headers the responses of an operation declare, typed on the arms of its result: an interface per arm that reads
/// any, and the reader the ApiClient calls for the arm. Which headers are typed, and as what, follows the C# clients
/// (<see cref="ResponseHeaderFactoryHelper.DeclaredByResponses"/>); a header that is absent, or is not a value of its
/// type, is undefined.
/// </summary>
internal static class TypeScriptResponseHeaders
{
    /// <summary>
    /// The typed headers of one result arm.
    /// </summary>
    /// <param name="operationTypeName">The operation's name in Pascal case (<c>ListPets</c>).</param>
    /// <param name="discriminator">The arm (<c>ok</c>, <c>serverError</c>).</param>
    /// <param name="responses">The responses the arm stands for.</param>
    /// <param name="options">How the client writes dates and names the models' properties.</param>
    /// <param name="imports">The models and enums the headers are typed with are added to it.</param>
    /// <returns>The interface's name and declaration, and the reader's lines (an arrow function); null when the responses declare none.</returns>
    public static (string TypeName, string Declaration, IReadOnlyList<string> Reader)? ForArm(
        string operationTypeName,
        string discriminator,
        IReadOnlyList<IOpenApiResponse> responses,
        TypeScriptResponseHeaderOptions options,
        HashSet<string> imports)
    {
        var declared = ResponseHeaderFactoryHelper.DeclaredByResponses(responses, operationName: operationTypeName);
        if (declared.Count == 0)
        {
            return null;
        }

        var typeName = operationTypeName + char.ToUpperInvariant(discriminator[0]) + discriminator[1..] + "Headers";
        var declaration = new StringBuilder();
        declaration.Append("/** The headers the '").Append(discriminator).AppendLine("' result reads: undefined when one is absent or is not a value of its type. */");
        declaration.Append("export interface ").Append(typeName).AppendLine(" {");
        var reader = new List<string> { "(header): " + typeName + " => ({" };
        foreach (var header in declared)
        {
            var (type, expression) = Read(header, SchemaOf(responses, header.Name), options, imports);
            var key = TypeScriptLiteralHelper.ToStringLiteral(header.Name);
            declaration.Append("  ").Append(key).Append("?: ").Append(type).AppendLine(";");
            reader.Add("  " + key + ": " + expression + ",");
        }

        declaration.AppendLine("}");
        reader.Add("})");
        return (typeName, declaration.ToString(), reader);
    }

    /// <summary>
    /// The runtime the readers use, in <c>ApiClient.ts</c>: the reader of a response's headers, the readers of one
    /// value, and the result with its typed headers.
    /// </summary>
    /// <param name="sb">The ApiClient's content.</param>
    /// <param name="axios">Whether the response is an AxiosResponse (its headers a record) rather than a fetch Response.</param>
    public static void AppendRuntime(
        StringBuilder sb,
        bool axios)
    {
        sb.AppendLine("""
            /** Reads one header value from its text: undefined when the text is not such a value. */
            export type HeaderValueParser<T> = (text: string | undefined) => T | undefined;

            function headerNumber(text: string | undefined): number | undefined {
              const value = text === undefined || text.trim() === '' ? NaN : Number(text);
              return Number.isFinite(value) ? value : undefined;
            }

            /** The readers of one header value, for the typed headers of a result. */
            export const headerValue = {
              /** The text itself. */
              text: (text: string | undefined): string | undefined => text,
              /** A number. */
              number: headerNumber,
              /** A whole number. */
              integer: (text: string | undefined): number | undefined => {
                const value = headerNumber(text);
                return value !== undefined && Number.isInteger(value) ? value : undefined;
              },
              /** true or false, whatever the casing. */
              boolean: (text: string | undefined): boolean | undefined => {
                const value = text?.trim().toLowerCase();
                return value === 'true' ? true : value === 'false' ? false : undefined;
              },
              /** A date or a date-time: ISO 8601, or an HTTP date. */
              date: (text: string | undefined): Date | undefined => {
                if (text === undefined || text.trim() === '') {
                  return undefined;
                }

                const value = new Date(text.trim());
                return Number.isNaN(value.getTime()) ? undefined : value;
              },
              /** One of an enum's values. */
              oneOf: <T>(values: readonly (string | number)[]): HeaderValueParser<T> => (text) => {
                const value = text?.trim();
                return values.find((candidate) => String(candidate) === value) as T | undefined;
              },
            };

            /** Reads the headers of a response for the typed headers of its result. */
            export class ResponseHeaderReader {
              private readonly get: (name: string) => string | undefined;

              constructor(get: (name: string) => string | undefined) {
                this.get = get;
              }

              /** A header's text. */
              text(name: string): string | undefined {
                return this.get(name);
              }

              /** A header's value: undefined when it is not a value of its type. */
              value<T>(name: string, parse: HeaderValueParser<T>): T | undefined {
                const text = this.get(name);
                return text === undefined ? undefined : parse(text);
              }

              /** An array header, its items separated by commas: undefined when an item is not a value of its type. */
              list<T>(name: string, parse: HeaderValueParser<T>): T[] | undefined {
                const text = this.get(name);
                if (text === undefined) {
                  return undefined;
                }

                const items: T[] = [];
                for (const part of text.split(',')) {
                  const item = parse(part.trim());
                  if (item === undefined) {
                    return undefined;
                  }

                  items.push(item);
                }

                return items;
              }

              /**
               * An object header in OpenAPI's simple style - name,value,... or name=value,... when exploded - read by the
               * operation's mapping: undefined when the text is not such an object, or a required field is missing.
               */
              object<T>(
                name: string,
                explode: boolean,
                read: (fields: Record<string, string>) => { [K in keyof T]?: T[K] },
                required: readonly (keyof T)[],
              ): T | undefined {
                const text = this.get(name);
                if (text === undefined) {
                  return undefined;
                }

                const fields: Record<string, string> = Object.create(null);
                const parts = text.split(',');
                if (explode) {
                  for (const part of parts) {
                    const at = part.indexOf('=');
                    if (at < 0) {
                      return undefined;
                    }

                    fields[part.slice(0, at).trim()] = part.slice(at + 1).trim();
                  }
                } else {
                  if (parts.length % 2 !== 0) {
                    return undefined;
                  }

                  for (let i = 0; i < parts.length; i += 2) {
                    fields[(parts[i] ?? '').trim()] = (parts[i + 1] ?? '').trim();
                  }
                }

                const value = read(fields);
                return required.every((key) => value[key] !== undefined) ? value as T : undefined;
              }
            }

            /** The readers of an operation's typed response headers, by result arm (the generated client passes them). */
            export type ResponseHeaderReaders = Partial<Record<string, (header: ResponseHeaderReader) => object>>;

            """);

        sb.AppendLine(axios
            ? """
              // A header of an AxiosResponse, whatever the casing of its name (several values are joined by commas)
              function responseHeader(response: AxiosResponse, name: string): string | undefined {
                const headers = (response.headers ?? {}) as Record<string, unknown>;
                const key = Object.keys(headers).find((candidate) => candidate.toLowerCase() === name.toLowerCase());
                const value = key === undefined ? undefined : headers[key];
                if (value === undefined || value === null) {
                  return undefined;
                }

                return Array.isArray(value) ? value.join(', ') : String(value);
              }
              """
            : """
              // A header of a Response (several values are joined by commas)
              function responseHeader(response: Response, name: string): string | undefined {
                return response.headers.get(name) ?? undefined;
              }
              """);

        sb.AppendLine("""

            // The result with the typed headers of its arm, when the operation declares any for it
            function withResponseHeaders<T>(result: ApiResult<T>, readers: ResponseHeaderReaders | undefined): ApiResult<T> {
              const read = readers?.[result.status];
              return read
                ? Object.assign({}, result, { headers: read(new ResponseHeaderReader((name) => responseHeader(result.response, name))) })
                : result;
            }

            """);
    }

    /// <summary>
    /// The <c>responseHeaders</c> request option, when an operation's result arms have typed headers.
    /// </summary>
    /// <param name="sb">The RequestOptions interface's content.</param>
    /// <param name="hasResponseHeaders">Whether an operation's result arms have typed headers.</param>
    public static void AppendRequestOption(
        StringBuilder sb,
        bool hasResponseHeaders)
    {
        if (!hasResponseHeaders)
        {
            return;
        }

        sb.AppendLine("  /** The readers of the typed headers of the operation's result arms (the generated client passes them). */");
        sb.AppendLine("  responseHeaders?: ResponseHeaderReaders;");
    }

    /// <summary>
    /// The result a request returns: with the typed headers of its arm, when an operation's result arms have any.
    /// </summary>
    /// <param name="result">The result's expression.</param>
    /// <param name="hasResponseHeaders">Whether an operation's result arms have typed headers.</param>
    /// <returns>The expression the request returns.</returns>
    public static string Returned(
        string result,
        bool hasResponseHeaders)
        => hasResponseHeaders
            ? "withResponseHeaders(" + result + ", options?.responseHeaders)"
            : result;

    // The schema a header is declared with: the first response's that declares it
    private static IOpenApiSchema? SchemaOf(
        IReadOnlyList<IOpenApiResponse> responses,
        string headerName)
    {
        foreach (var response in responses)
        {
            foreach (var header in response.Headers ?? new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))
            {
                if (string.Equals(header.Key, headerName, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value?.Schema;
                }
            }
        }

        return null;
    }

    private static (string Type, string Expression) Read(
        DeclaredResponseHeader header,
        IOpenApiSchema? schema,
        TypeScriptResponseHeaderOptions options,
        HashSet<string> imports)
    {
        var name = TypeScriptLiteralHelper.ToStringLiteral(header.Name);
        var valueSchema = header.IsArray && schema is OpenApiSchema { Items: { } items } ? items : schema;
        if (header.Kind == ResponseHeaderValueKind.Model)
        {
            return ObjectRead(header, valueSchema, name, options, imports);
        }

        var (type, parser) = ValueOf(header.Kind, header.Type, valueSchema, options, imports);
        if (header.IsArray)
        {
            var itemType = type.Contains(' ', StringComparison.Ordinal) ? "(" + type + ")" : type;
            return (itemType + "[]", $"header.list({name}, {parser ?? "headerValue.text"})");
        }

        return parser is null
            ? (type, $"header.text({name})")
            : (type, $"header.value({name}, {parser})");
    }

    // The TypeScript type of one value, and the headerValue reader that reads it (null: the text itself)
    private static (string Type, string? Parser) ValueOf(
        ResponseHeaderValueKind kind,
        string csharpType,
        IOpenApiSchema? schema,
        TypeScriptResponseHeaderOptions options,
        HashSet<string> imports)
    {
        if (kind is ResponseHeaderValueKind.StringEnum or ResponseHeaderValueKind.IntegerEnum)
        {
            return EnumOf(kind, schema, imports);
        }

        return csharpType switch
        {
            "bool" => ("boolean", "headerValue.boolean"),
            "int" or "long" => ("number", "headerValue.integer"),
            "float" or "double" => ("number", "headerValue.number"),
            "global::System.DateTimeOffset" or "global::System.DateOnly" => options.ConvertDates
                ? ("Date", "headerValue.date")
                : ("string", null),
            _ => ("string", null),
        };
    }

    // An enum: its type, read as one of its values - or as any text when the type is open (an extensible enum keeps a
    // value the server adds later)
    private static (string Type, string? Parser) EnumOf(
        ResponseHeaderValueKind kind,
        IOpenApiSchema? schema,
        HashSet<string> imports)
    {
        if (schema is OpenApiSchemaReference { Target: OpenApiSchema target })
        {
            var typeName = schema.ToTypeScriptTypeForModel(isRequired: true);
            TypeScriptOperationHelper.CollectSchemaRefTypes(schema, imports);
            return kind == ResponseHeaderValueKind.StringEnum && TypeScriptOperationHelper.EnumTypesAreOpen
                ? (typeName, null)
                : (typeName, $"headerValue.oneOf<{typeName}>({ValuesOf(target)})");
        }

        if (schema is OpenApiSchema inline && TypeScriptOperationHelper.LiteralUnionOf(inline) is { } union)
        {
            return kind == ResponseHeaderValueKind.StringEnum && TypeScriptOperationHelper.InlineEnumsAreOpen
                ? (union, null)
                : (union, $"headerValue.oneOf<{union}>({ValuesOf(inline)})");
        }

        return ("string", null);
    }

    private static string ValuesOf(OpenApiSchema schema)
    {
        var values = new List<string>();
        foreach (var value in schema.Enum ?? [])
        {
            if (value is not JsonValue jsonValue)
            {
                continue;
            }

            values.Add(jsonValue.TryGetValue<string>(out var text)
                ? TypeScriptLiteralHelper.ToStringLiteral(text)
                : jsonValue.ToJsonString());
        }

        return "[" + string.Join(", ", values) + "]";
    }

    // An object in the simple style: its model, each field read as the model types it; a required field that is
    // missing makes it undefined
    private static (string Type, string Expression) ObjectRead(
        DeclaredResponseHeader header,
        IOpenApiSchema? schema,
        string name,
        TypeScriptResponseHeaderOptions options,
        HashSet<string> imports)
    {
        if (schema is not OpenApiSchemaReference { Target: OpenApiSchema { Properties: { } properties } target } reference ||
            header.Fields is null)
        {
            return ("string", $"header.text({name})");
        }

        var typeName = schema.ToTypeScriptTypeForModel(isRequired: true);
        TypeScriptOperationHelper.CollectSchemaRefTypes(schema, imports);
        var keys = properties.Keys.ToList();
        var parts = new List<string>();
        var required = new List<string>();
        foreach (var field in header.Fields)
        {
            var propertySchema = properties[field.WireName];
            var resolved = propertySchema.Dereferenced;

            // A writeOnly property is not in the model a response is read into
            if (resolved is OpenApiSchema { WriteOnly: true })
            {
                continue;
            }

            var propertyName = TypeScriptPropertyNames.Resolve(field.WireName, keys, options.NamingStrategy);
            var text = "fields[" + TypeScriptLiteralHelper.ToStringLiteral(field.WireName) + "]";
            var (_, parser) = ValueOf(field.Kind, field.Type, propertySchema, options, imports);
            var value = parser is null ? text : parser + "(" + text + ")";

            // A branded id is the model's brand
            if (options.BrandedIds &&
                field.Kind == ResponseHeaderValueKind.Scalar &&
                TypeScriptBrandedIdExtractor.ResolvePropertyBrand(reference.Reference.Id ?? typeName, field.WireName, propertySchema) is not null)
            {
                value += " as " + typeName + "[" + TypeScriptLiteralHelper.ToStringLiteral(propertyName) + "]";
            }

            parts.Add(TypeScriptPropertyNames.ToMemberSyntax(propertyName) + ": " + value);
            if (target.Required?.Contains(field.WireName) == true)
            {
                required.Add(TypeScriptLiteralHelper.ToStringLiteral(propertyName));
            }
        }

        var explode = header.Explode ? "true" : "false";
        return (typeName, $"header.object<{typeName}>({name}, {explode}, (fields) => ({{ {string.Join(", ", parts)} }}), [{string.Join(", ", required)}])");
    }
}