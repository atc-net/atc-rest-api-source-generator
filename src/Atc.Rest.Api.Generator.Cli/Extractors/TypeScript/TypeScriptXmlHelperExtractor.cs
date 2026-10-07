namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates <c>helpers/xml.ts</c>: the XML mapping of the models an XML body carries (<c>read{Model}</c> /
/// <c>write{Model}</c>, and the list forms) as the OpenAPI <c>xml</c> keyword describes it - the same rules as the C#
/// <c>{Model}Xml</c> - plus the runtime the generated ApiClient uses: an injectable parser (<c>setXmlParser</c>;
/// browsers have <c>DOMParser</c>, Node takes e.g. <c>@xmldom/xmldom</c>), the media-type check and
/// <c>readProblemXml</c> for <c>application/problem+xml</c> errors (RFC 9457 Appendix B).
/// </summary>
public static class TypeScriptXmlHelperExtractor
{
    private enum ValueKind
    {
        Text,
        Number,
        Boolean,
        Date,
        DateOnly,
        Model,
    }

    /// <summary>
    /// The TypeScript read / write function names of a schema's model.
    /// </summary>
    /// <param name="schemaKey">The schema name.</param>
    /// <returns>The read and write function names.</returns>
    public static (string Read, string Write, string ReadList, string WriteList) FunctionNames(
        string schemaKey)
    {
        var name = OpenApiSchemaExtensions.SanitizeSchemaName(schemaKey);
        return ($"read{name}", $"write{name}", $"read{name}List", $"write{name}List");
    }

    /// <summary>
    /// Generates the helper module, or <see langword="null"/> when the specification has no XML body.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="headerContent">The file header.</param>
    /// <param name="namingStrategy">The models' property naming strategy.</param>
    /// <param name="convertDates">Whether date properties are <c>Date</c> objects.</param>
    /// <returns>The module content.</returns>
    public static string? Generate(
        OpenApiDocument openApiDoc,
        string? headerContent,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var xmlModels = XmlBodyReachability.Collect(openApiDoc);
        if (xmlModels.Count == 0)
        {
            return null;
        }

        var models = xmlModels
            .Where(key => openApiDoc.Components?.Schemas?.TryGetValue(key, out var schema) == true &&
                          XmlBodyHelper.IsMappedModel(new OpenApiSchemaReference(key, openApiDoc)))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        if (models.Count > 0)
        {
            sb.Append("import type { ")
              .Append(string.Join(", ", models.Select(OpenApiSchemaExtensions.SanitizeSchemaName)))
              .AppendLine(" } from '../models';");
            sb.AppendLine();
        }

        sb.Append(Runtime);

        foreach (var key in models)
        {
            AppendModel(sb, key, (OpenApiSchema)openApiDoc.Components!.Schemas![key], openApiDoc, models, namingStrategy, convertDates);
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    /// <summary>
    /// Adds XML bodies to a generated ApiClient (Fetch or Axios): the <c>xml</c> request option (Accept, an XML request
    /// body), an XML success body read by the operation's mapping (before the JSON handling - no key renaming, no
    /// schema check), and an <c>application/problem+xml</c> error read into a ProblemDetails object.
    /// </summary>
    /// <param name="content">The generated ApiClient.</param>
    /// <param name="isAxios">Whether it is the Axios client.</param>
    /// <returns>The ApiClient with XML support.</returns>
    public static string AugmentApiClient(
        string content,
        bool isAxios)
    {
        ArgumentNullException.ThrowIfNull(content);

        var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var s = content.Replace("\r\n", "\n", StringComparison.Ordinal);

        s = Replace(s, "import { ValidationError } from '../errors/ValidationError';\n", "import { ValidationError } from '../errors/ValidationError';\nimport { isXmlMediaType, parseXml, readProblemXml, withResponse, xmlSuccessArm, type XmlOptions } from '../helpers/xml';\n");
        s = Replace(s, "  arms?: readonly string[];\n", "  arms?: readonly string[];\n  /** An XML body (the generated client passes it): the Accept, how to write the request body and read the response. */\n  xml?: XmlOptions;\n");

        if (isAxios)
        {
            // Only the fetch client hands back the server response of an XML error model
            s = Replace(s, "readProblemXml, withResponse, xmlSuccessArm", "readProblemXml, xmlSuccessArm");
            s = ReplaceIfPresent(s, "        if (data instanceof FormData || data instanceof Blob) return data;\n", "        if (data instanceof FormData || data instanceof Blob) return data;\n        if (typeof data === 'string' && /xml/i.test(String(headers['Content-Type'] ?? ''))) return data;\n");
            s = Replace(s, "      } else if (options.body instanceof Blob) {\n        const fileType", "      } else if (options.xml?.write) {\n        headers['Content-Type'] = options.xml.contentType ?? 'application/xml; charset=utf-8';\n        data = options.xml.write(options.body as never);\n      } else if (options.body instanceof Blob) {\n        const fileType");
            s = Replace(s, "    if (options?.headers) {\n      for (const [key, value] of Object.entries(options.headers)) {\n        if (value !== undefined) {\n          headers[key] = String(value);\n        }\n      }\n    }\n", "    if (options?.headers) {\n      for (const [key, value] of Object.entries(options.headers)) {\n        if (value !== undefined) {\n          headers[key] = String(value);\n        }\n      }\n    }\n\n    if (options?.xml?.accept) {\n      headers['Accept'] = options.xml.accept;\n    }\n");
            s = Replace(s, "    return fitToArms(await this.handleResponse<T>(response", "    // An XML body: read by the operation's mapping (no JSON handling)\n    const responseContentType = String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '');\n    const readXmlError = options?.xml?.errors?.[String(response.status)] ?? options?.xml?.errors?.['default'];\n    if (readXmlError && (response.status < 200 || response.status >= 300) && isXmlMediaType(responseContentType) && typeof response.data === 'string') {\n      try {\n        (response as { data: unknown }).data = readXmlError(parseXml(response.data));\n      } catch {\n        // Not the declared model: the error arm gets the text\n      }\n    }\n\n    if (options?.xml?.read && response.status >= 200 && response.status < 300 && response.status !== 204 && response.status !== 205 && isXmlMediaType(responseContentType) && typeof response.data === 'string') {\n      try {\n        return fitToArms({ status: xmlSuccessArm(response.status), data: options.xml.read(parseXml(response.data)) as T, response } as ApiResult<T>, options.arms);\n      } catch (parseError) {\n        return fitToArms({ status: 'parseError', error: parseError as Error, response }, options.arms);\n      }\n    }\n\n    return fitToArms(await this.handleResponse<T>(response");
            s = Replace(s, "      } else if (errorContentType.includes('application/json') || errorContentType.includes('+json')) {\n", "      } else if (errorContentType.includes('application/problem+xml')) {\n        try {\n          errorBody = readProblemXml(parseXml(errorBody));\n        } catch {\n          errorBody = undefined;\n        }\n      } else if (errorContentType.includes('application/json') || errorContentType.includes('+json')) {\n");
        }
        else
        {
            // The fetch client's result keeps the server's response (exactly one return reads the response)
            var fetchReturn = new Regex(@"^    return fitToArms\(await this\.handleResponse<T>\((?<args>response[^\n]*)\), options\?\.arms\);$", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
            if (fetchReturn.Matches(s).Count != 1)
            {
                throw new InvalidOperationException("The generated ApiClient no longer has exactly one return of handleResponse");
            }

            s = fetchReturn.Replace(s, "    return fitToArms(withResponse(serverResponse, await this.handleResponse<T>(${args})), options?.arms);");
            s = Replace(s, "    const headers = await this.getHeaders(options?.headers);\n\n    let fetchBody", "    const headers = await this.getHeaders(options?.headers);\n    if (options?.xml?.accept) {\n      headers.set('Accept', options.xml.accept);\n    }\n\n    let fetchBody");
            s = Replace(s, "      } else if (options.body instanceof Blob) {\n        headers.set('Content-Type', options.contentType ?? (options.body.type || 'application/octet-stream'));\n        fetchBody = options.body;\n", "      } else if (options.body instanceof Blob) {\n        headers.set('Content-Type', options.contentType ?? (options.body.type || 'application/octet-stream'));\n        fetchBody = options.body;\n      } else if (options.xml?.write) {\n        headers.set('Content-Type', options.xml.contentType ?? 'application/xml; charset=utf-8');\n        fetchBody = options.xml.write(options.body as never);\n");
            s = Replace(s, "    for (const interceptor of this.options.responseInterceptors ?? []) {\n      response = await interceptor(response);\n    }\n\n    return fitToArms", "    for (const interceptor of this.options.responseInterceptors ?? []) {\n      response = await interceptor(response);\n    }\n\n    // A declared XML error model: read into the model and handed on as a JSON error body - the result keeps the\n    // server's response (its headers say XML)\n    let serverResponse: Response | undefined;\n    const readXmlError = options?.xml?.errors?.[String(response.status)] ?? options?.xml?.errors?.['default'];\n    if (readXmlError && !response.ok && isXmlMediaType(response.headers.get('Content-Type'))) {\n      try {\n        const errorModel = readXmlError(parseXml(await response.clone().text()));\n        const errorHeaders = new Headers(response.headers);\n        errorHeaders.set('Content-Type', 'application/json');\n        serverResponse = response;\n        response = new Response(JSON.stringify(errorModel), { status: response.status, statusText: response.statusText, headers: errorHeaders });\n      } catch {\n        // Not the declared model: the error arm gets the text\n      }\n    }\n\n    // An XML body: read by the operation's mapping (no JSON handling)\n    if (options?.xml?.read && response.ok && response.status !== 204 && response.status !== 205 && isXmlMediaType(response.headers.get('Content-Type'))) {\n      try {\n        return fitToArms({ status: xmlSuccessArm(response.status), data: options.xml.read(parseXml(await response.text())) as T, response } as ApiResult<T>, options.arms);\n      } catch (parseError) {\n        return fitToArms({ status: 'parseError', error: parseError as Error, response }, options.arms);\n      }\n    }\n\n    return fitToArms");
            s = Replace(s, "    try {\n      if (contentType.includes('application/json') || contentType.includes('+json')) {\n        errorBody = ", "    try {\n      if (contentType.includes('application/problem+xml')) {\n        const text = await response.text();\n        errorBody = text.length > 0 ? readProblemXml(parseXml(text)) : undefined;\n      } else if (contentType.includes('application/json') || contentType.includes('+json')) {\n        errorBody = ");
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

        static string ReplaceIfPresent(
            string text,
            string anchor,
            string replacement)
            => text.Contains(anchor, StringComparison.Ordinal) ? Replace(text, anchor, replacement) : text;
    }

    private static void AppendModel(
        StringBuilder sb,
        string key,
        OpenApiSchema schema,
        OpenApiDocument openApiDoc,
        List<string> models,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates)
    {
        var typeName = OpenApiSchemaExtensions.SanitizeSchemaName(key);
        var (read, write, readList, writeList) = FunctionNames(key);
        var rootName = XmlName(schema.Xml) ?? key;
        var (rootNamespace, rootPrefix) = NamespaceOf(schema.Xml);
        var properties = CollectProperties(schema);

        // read
        sb.AppendLine();
        sb.Append("/** Reads a {@link ").Append(typeName).AppendLine("} from its XML element (names match by local name). */");
        sb.Append("export function ").Append(read).Append("(element: Element): ").Append(typeName).AppendLine(" {");
        sb.Append("  return compact<").Append(typeName).AppendLine(">({");
        foreach (var (wireName, propertySchema) in properties)
        {
            sb.Append("    ").Append(ObjectKey(PropertyKey(wireName, namingStrategy))).Append(": ")
              .Append(ReadExpression(wireName, propertySchema, openApiDoc, models, convertDates))
              .AppendLine(",");
        }

        sb.AppendLine("  });");
        sb.AppendLine("}");

        // write
        sb.AppendLine();
        sb.Append("/** Writes a {@link ").Append(typeName).AppendLine("} as its XML element. */");
        sb.Append("export function ").Append(write).Append("(value: ").Append(typeName)
          .Append(", name = '").Append(Js(rootName)).Append("', namespace = '").Append(Js(rootNamespace ?? string.Empty))
          .Append("', prefix = '").Append(Js(rootPrefix ?? string.Empty)).AppendLine("'): string {");
        sb.AppendLine("  const v = value as unknown as Record<string, unknown>;");
        sb.AppendLine("  return xmlElement(name, namespace, prefix, [");
        foreach (var (wireName, propertySchema) in properties.Where(p => IsAttribute(p.Schema.Xml)))
        {
            sb.Append("    xmlAttribute('").Append(Js(XmlName(propertySchema.Xml) ?? wireName)).Append("', ")
              .Append(ScalarValue(propertySchema, $"v['{Js(PropertyKey(wireName, namingStrategy))}']", convertDates)).AppendLine("),");
        }

        sb.AppendLine("  ].join(''), [");
        foreach (var (wireName, propertySchema) in properties.Where(p => !IsAttribute(p.Schema.Xml)))
        {
            sb.Append("    ").Append(WriteExpression(wireName, propertySchema, $"v['{Js(PropertyKey(wireName, namingStrategy))}']", models, convertDates)).AppendLine(",");
        }

        sb.AppendLine("  ].join(''));");
        sb.AppendLine("}");

        // lists
        sb.AppendLine();
        sb.Append("/** Reads a list of {@link ").Append(typeName).AppendLine("}: the items named like the model's element, else every child element. */");
        sb.Append("export function ").Append(readList).Append("(element: Element): ").Append(typeName).AppendLine("[] {");
        sb.Append("  const items = xmlChildren(element, '").Append(Js(rootName)).AppendLine("');");
        sb.Append("  return (items.length > 0 ? items : childElements(element)).map(").Append(read).AppendLine(");");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append("/** Writes a list of {@link ").Append(typeName).AppendLine("}: a root element holding one element per item. */");
        sb.Append("export function ").Append(writeList).Append("(items: readonly ").Append(typeName)
          .Append("[], root = 'ArrayOf").Append(Js(key)).AppendLine("'): string {");
        sb.Append("  return xmlElement(root, '', '', '', items.map((item) => ").Append(write).AppendLine("(item)).join(''));");
        sb.AppendLine("}");
    }

    private static string ReadExpression(
        string wireName,
        IOpenApiSchema propertySchema,
        OpenApiDocument openApiDoc,
        List<string> models,
        bool convertDates)
    {
        var xml = propertySchema.Xml;
        var name = XmlName(xml) ?? wireName;
        var resolved = Resolve(propertySchema);

        if (IsAttribute(xml))
        {
            return Parse(Classify(propertySchema, models, convertDates), $"xmlAttr(element, '{Js(name)}')", propertySchema, models);
        }

        if (resolved is OpenApiSchema { Type: { } type } array && (type & JsonSchemaType.Array) == JsonSchemaType.Array && array.Items is { } items)
        {
            var wrapped = xml?.NodeType == OpenApiXmlNodeType.Element;
            var itemName = XmlName(items.Xml) ?? (wrapped ? name : wireName);
            var item = ItemReader(Classify(items, models, convertDates), items, models);
            return wrapped
                ? $"xmlWrapped(xmlChild(element, '{Js(name)}'), '{Js(itemName)}', {item})"
                : $"xmlList(xmlChildren(element, '{Js(itemName)}'), {item})";
        }

        if (resolved?.AdditionalProperties is { } values)
        {
            return $"xmlRecord(xmlChild(element, '{Js(name)}'), {ItemReader(Classify(values, models, convertDates), values, models)})";
        }

        var kind = Classify(propertySchema, models, convertDates);
        return kind == ValueKind.Model
            ? $"xmlMap(xmlChild(element, '{Js(name)}'), {FunctionNames(RefKey(propertySchema)!).Read})"
            : Parse(kind, $"xmlText(xmlChild(element, '{Js(name)}'))", propertySchema, models);
    }

    private static string WriteExpression(
        string wireName,
        IOpenApiSchema propertySchema,
        string access,
        List<string> models,
        bool convertDates)
    {
        var xml = propertySchema.Xml;
        var name = XmlName(xml) ?? wireName;
        var (ns, prefix) = NamespaceOf(xml);
        var resolved = Resolve(propertySchema);

        if (resolved is OpenApiSchema { Type: { } type } array && (type & JsonSchemaType.Array) == JsonSchemaType.Array && array.Items is { } items)
        {
            var wrapped = xml?.NodeType == OpenApiXmlNodeType.Element;
            var itemName = XmlName(items.Xml) ?? (wrapped ? name : wireName);
            var (itemNs, itemPrefix) = NamespaceOf(items.Xml);
            var writeItem = ItemWriter(items, itemName, itemNs, itemPrefix, models, convertDates);
            var itemsExpression = $"xmlItems({access}, {writeItem})";
            return wrapped
                ? $"xmlOptionalElement({access}, '{Js(name)}', '{Js(ns ?? string.Empty)}', '{Js(prefix ?? string.Empty)}', {itemsExpression})"
                : itemsExpression;
        }

        if (resolved?.AdditionalProperties is { } values)
        {
            return $"xmlOptionalElement({access}, '{Js(name)}', '{Js(ns ?? string.Empty)}', '{Js(prefix ?? string.Empty)}', xmlEntries({access}, {EntryWriter(values, models, convertDates)}))";
        }

        if (RefKey(propertySchema) is { } modelKey && models.Contains(modelKey))
        {
            return $"({access} === undefined || {access} === null ? '' : {FunctionNames(modelKey).Write}({access} as never, '{Js(name)}', '{Js(ns ?? string.Empty)}', '{Js(prefix ?? string.Empty)}'))";
        }

        return $"xmlValue('{Js(name)}', '{Js(ns ?? string.Empty)}', '{Js(prefix ?? string.Empty)}', {ScalarValue(propertySchema, access, convertDates)})";
    }

    /// <summary>
    /// A scalar value to write: a <c>format: date</c> Date (with <c>--convert-dates</c>) as the calendar day it is in
    /// the caller's time zone, like the JSON bodies; anything else as it is (<c>xmlString</c> writes a Date as ISO 8601).
    /// </summary>
    private static string ScalarValue(
        IOpenApiSchema? schema,
        string access,
        bool convertDates)
        => convertDates && Resolve(schema) is { } resolved && TypeScriptOperationHelper.IsDateOnly(resolved)
            ? $"xmlDateOnlyText({access})"
            : access;

    private static string ItemReader(
        ValueKind kind,
        IOpenApiSchema items,
        List<string> models)
        => kind == ValueKind.Model
            ? FunctionNames(RefKey(items)!).Read
            : $"(e: Element) => {Parse(kind, "xmlText(e)", items, models)}";

    private static string ItemWriter(
        IOpenApiSchema items,
        string itemName,
        string? ns,
        string? prefix,
        List<string> models,
        bool convertDates)
        => RefKey(items) is { } key && models.Contains(key)
            ? $"(item: unknown) => {FunctionNames(key).Write}(item as never, '{Js(itemName)}', '{Js(ns ?? string.Empty)}', '{Js(prefix ?? string.Empty)}')"
            : $"(item: unknown) => xmlValue('{Js(itemName)}', '{Js(ns ?? string.Empty)}', '{Js(prefix ?? string.Empty)}', {ScalarValue(items, "item", convertDates)})";

    private static string EntryWriter(
        IOpenApiSchema values,
        List<string> models,
        bool convertDates)
        => RefKey(values) is { } key && models.Contains(key)
            ? $"(name: string, item: unknown) => {FunctionNames(key).Write}(item as never, name)"
            : $"(name: string, item: unknown) => xmlValue(name, '', '', {ScalarValue(values, "item", convertDates)})";

    private static string Parse(
        ValueKind kind,
        string textExpression,
        IOpenApiSchema schema,
        List<string> models)
        => kind switch
        {
            ValueKind.Number => $"xmlNumber({textExpression})",
            ValueKind.Boolean => $"xmlBoolean({textExpression})",
            ValueKind.Date => $"xmlDate({textExpression})",
            ValueKind.DateOnly => $"xmlDateOnly({textExpression})",
            ValueKind.Model => $"undefined /* {RefKey(schema)} */",
            _ => textExpression,
        };

    private static ValueKind Classify(
        IOpenApiSchema? schema,
        List<string> models,
        bool convertDates)
    {
        if (RefKey(schema) is { } key && models.Contains(key))
        {
            return ValueKind.Model;
        }

        var resolved = Resolve(schema);
        var type = resolved?.Type ?? JsonSchemaType.String;
        if (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number))
        {
            return ValueKind.Number;
        }

        if (type.HasFlag(JsonSchemaType.Boolean))
        {
            return ValueKind.Boolean;
        }

        if (!convertDates)
        {
            return ValueKind.Text;
        }

        return resolved?.Format switch
        {
            "date-time" => ValueKind.Date,
            "date" => ValueKind.DateOnly,
            _ => ValueKind.Text,
        };
    }

    private static List<(string WireName, IOpenApiSchema Schema)> CollectProperties(
        OpenApiSchema schema)
    {
        var properties = new List<(string WireName, IOpenApiSchema Schema)>();
        void Add(IOpenApiSchema? part)
        {
            var resolved = Resolve(part);
            if (resolved is null)
            {
                return;
            }

            foreach (var allOf in resolved.AllOf ?? [])
            {
                Add(allOf);
            }

            foreach (var property in resolved.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
            {
                if (properties.TrueForAll(p => !string.Equals(p.WireName, property.Key, StringComparison.Ordinal)))
                {
                    properties.Add((property.Key, property.Value));
                }
            }
        }

        Add(schema);
        return properties;
    }

    private static string PropertyKey(
        string wireName,
        TypeScriptNamingStrategy namingStrategy)
        => wireName.ApplyNamingStrategy(namingStrategy);

    private static string ObjectKey(string propertyKey)
        => propertyKey.Length > 0 && (char.IsLetter(propertyKey[0]) || propertyKey[0] is '_' or '$') && propertyKey.All(c => char.IsLetterOrDigit(c) || c is '_' or '$')
            ? propertyKey
            : "'" + Js(propertyKey) + "'";

    private static string? RefKey(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference reference ? reference.Reference?.Id ?? reference.Id : null;

    private static IOpenApiSchema? Resolve(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference reference ? reference.Target : schema;

    private static string? XmlName(OpenApiXml? xml)
        => xml?.Name is { Length: > 0 } name && name != "##default" ? name : null;

    private static bool IsAttribute(OpenApiXml? xml)
        => xml?.NodeType == OpenApiXmlNodeType.Attribute;

    private static (string? Namespace, string? Prefix) NamespaceOf(
        OpenApiXml? xml)
    {
        var ns = xml?.Namespace?.OriginalString;
        return string.IsNullOrEmpty(ns)
            ? (null, null)
            : (ns, string.IsNullOrEmpty(xml!.Prefix) ? null : xml.Prefix);
    }

    private static string Js(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    private const string Runtime = """
        /** A DOMParser-compatible parser (browsers: DOMParser; Node: e.g. @xmldom/xmldom). */
        export interface XmlParser {
          parseFromString(text: string, mimeType: 'application/xml'): Document;
        }

        let xmlParserFactory: (() => XmlParser) | undefined;

        /** Sets the XML parser (default: the global DOMParser). */
        export function setXmlParser(factory: () => XmlParser): void {
          xmlParserFactory = factory;
        }

        /** How the generated client sends and reads an operation's XML body. */
        export interface XmlOptions {
          read?: (element: Element) => unknown;
          write?: (body: never) => string;
          accept?: string;
          contentType?: string;
          /** Declared XML error models by status ('404', 'default'): read into the model, then handled like a JSON error body. */
          errors?: Record<string, (element: Element) => unknown>;
        }

        /** Whether a media type is an XML model body (application/xml, text/xml, *+xml - not problem+xml). */
        export function isXmlMediaType(contentType: string | null | undefined): boolean {
          const type = ((contentType ?? '').split(';')[0] ?? '').trim().toLowerCase();
          return type === 'application/xml' || type === 'text/xml' || (type.endsWith('+xml') && type !== 'application/problem+xml');
        }

        /** A result with the server's response in place of the stand-in an XML error model was read through. */
        export function withResponse<R extends { response: unknown }>(serverResponse: R['response'] | undefined, result: R): R {
          if (serverResponse === undefined) {
            return result;
          }

          const error = (result as { error?: unknown }).error;
          if (error !== null && typeof error === 'object' && 'response' in error) {
            Object.defineProperty(error, 'response', { value: serverResponse, enumerable: true, configurable: true });
          }

          return { ...result, response: serverResponse };
        }

        /** The success arm of a status. */
        export function xmlSuccessArm(status: number): 'ok' | 'created' | 'accepted' {
          return status === 201 ? 'created' : status === 202 ? 'accepted' : 'ok';
        }

        /** Parses an XML document and returns its root element. */
        export function parseXml(text: string): Element {
          const parser = xmlParserFactory?.() ?? (typeof DOMParser !== 'undefined' ? new DOMParser() : undefined);
          if (!parser) {
            throw new Error('No XML parser: call setXmlParser(() => new DOMParser()) - in Node e.g. from @xmldom/xmldom');
          }

          const document = parser.parseFromString(text, 'application/xml');
          const root = document.documentElement;
          if (!root || root.localName === 'parsererror' || document.getElementsByTagName('parsererror').length > 0) {
            throw new Error('The XML document could not be parsed');
          }

          return root;
        }

        /** An application/problem+xml body (RFC 9457 Appendix B) as a ProblemDetails object. */
        export function readProblemXml(element: Element): Record<string, unknown> {
          const problem: Record<string, unknown> = {};
          for (const child of childElements(element)) {
            const name = decodeXmlName(child.localName);
            if (name === 'status') {
              problem[name] = xmlNumber(xmlText(child));
            } else if (name === 'errors') {
              problem[name] = Object.fromEntries(childElements(child).map((field) => {
                const items = childElements(field);
                return [decodeXmlName(field.localName), items.length > 0 ? items.map((i) => xmlText(i) ?? '') : [xmlText(field) ?? '']];
              }));
            } else {
              problem[name] = xmlGeneric(child);
            }
          }

          return problem;
        }

        export function xmlGeneric(element: Element): unknown {
          const children = childElements(element);
          if (children.length === 0) {
            return xmlText(element);
          }

          return children.every((c) => c.localName === 'i')
            ? children.map(xmlGeneric)
            : Object.fromEntries(children.map((c) => [decodeXmlName(c.localName), xmlGeneric(c)]));
        }

        export function childElements(element: Element): Element[] {
          const result: Element[] = [];
          for (let i = 0; i < element.childNodes.length; i++) {
            const node = element.childNodes[i];
            if (node && node.nodeType === 1) {
              result.push(node as Element);
            }
          }

          return result;
        }

        export function xmlChildren(element: Element | undefined, name: string): Element[] {
          return element ? childElements(element).filter((e) => e.localName === name) : [];
        }

        export function xmlChild(element: Element | undefined, name: string): Element | undefined {
          return xmlChildren(element, name)[0];
        }

        export function xmlText(element: Element | undefined): string | undefined {
          return element ? element.textContent ?? '' : undefined;
        }

        export function xmlAttr(element: Element, name: string): string | undefined {
          for (let i = 0; i < element.attributes.length; i++) {
            const attribute = element.attributes[i];
            if (attribute && attribute.localName === name && attribute.prefix !== 'xmlns' && attribute.name !== 'xmlns') {
              return attribute.value;
            }
          }

          return undefined;
        }

        export function xmlNumber(text: string | undefined): number | undefined {
          return text === undefined || text.trim() === '' ? undefined : Number(text.trim());
        }

        export function xmlBoolean(text: string | undefined): boolean | undefined {
          return text === undefined ? undefined : ['true', '1'].includes(text.trim().toLowerCase());
        }

        export function xmlDate(text: string | undefined): Date | undefined {
          return text === undefined || text.trim() === '' ? undefined : new Date(text.trim());
        }

        /** A format: date (yyyy-MM-dd): the calendar day as a local Date (new Date('2024-01-15') would be UTC midnight). */
        export function xmlDateOnly(text: string | undefined): Date | undefined {
          if (text === undefined || text.trim() === '') {
            return undefined;
          }

          const day = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text.trim());
          return day ? new Date(Number(day[1]), Number(day[2]) - 1, Number(day[3])) : new Date(text.trim());
        }

        /** A format: date Date as text: the calendar day it is in the caller's time zone (yyyy-MM-dd); anything else as it is. */
        export function xmlDateOnlyText(value: unknown): unknown {
          if (!(value instanceof Date)) {
            return value;
          }

          const month = String(value.getMonth() + 1).padStart(2, '0');
          const day = String(value.getDate()).padStart(2, '0');
          return `${String(value.getFullYear()).padStart(4, '0')}-${month}-${day}`;
        }

        export function xmlMap<T>(element: Element | undefined, read: (element: Element) => T): T | undefined {
          return element ? read(element) : undefined;
        }

        export function xmlList<T>(elements: Element[], read: (element: Element) => T | undefined): T[] | undefined {
          return elements.length > 0 ? elements.map((e) => read(e) as T) : undefined;
        }

        export function xmlWrapped<T>(wrapper: Element | undefined, itemName: string, read: (element: Element) => T | undefined): T[] | undefined {
          return wrapper ? xmlChildren(wrapper, itemName).map((e) => read(e) as T) : undefined;
        }

        export function xmlRecord<T>(element: Element | undefined, read: (element: Element) => T | undefined): Record<string, T> | undefined {
          return element ? Object.fromEntries(childElements(element).map((e) => [decodeXmlName(e.localName), read(e) as T])) : undefined;
        }

        export function compact<T>(value: Record<string, unknown>): T {
          for (const key of Object.keys(value)) {
            if (value[key] === undefined) {
              delete value[key];
            }
          }

          return value as T;
        }

        /** Escapes text for XML content and attribute values. */
        export function escapeXml(text: string): string {
          return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
        }

        export function encodeXmlName(name: string): string {
          return name.length === 0
            ? '_'
            : Array.from(name).map((c, i) => (/[A-Za-z_]/.test(c) || (i > 0 && /[0-9.\-]/.test(c)) ? c : '_x' + c.charCodeAt(0).toString(16).toUpperCase().padStart(4, '0') + '_')).join('');
        }

        export function decodeXmlName(name: string): string {
          return name === '_' ? '' : name.replace(/_x([0-9A-Fa-f]{4})_/g, (_, hex: string) => String.fromCharCode(parseInt(hex, 16)));
        }

        export function xmlString(value: unknown): string {
          return value instanceof Date ? value.toISOString() : typeof value === 'boolean' ? (value ? 'true' : 'false') : String(value);
        }

        export function xmlElement(name: string, namespace: string, prefix: string, attributes: string, content = ''): string {
          const qualified = prefix ? prefix + ':' + name : name;
          const declaration = namespace ? (prefix ? ' xmlns:' + prefix + '="' + escapeXml(namespace) + '"' : ' xmlns="' + escapeXml(namespace) + '"') : '';
          return content.length > 0
            ? '<' + qualified + declaration + attributes + '>' + content + '</' + qualified + '>'
            : '<' + qualified + declaration + attributes + ' />';
        }

        export function xmlAttribute(name: string, value: unknown): string {
          return value === undefined || value === null ? '' : ' ' + name + '="' + escapeXml(xmlString(value)) + '"';
        }

        export function xmlValue(name: string, namespace: string, prefix: string, value: unknown): string {
          if (value === undefined || value === null) {
            return '';
          }

          const qualified = prefix ? prefix + ':' + name : name;
          const declaration = namespace ? (prefix ? ' xmlns:' + prefix + '="' + escapeXml(namespace) + '"' : ' xmlns="' + escapeXml(namespace) + '"') : '';
          return '<' + qualified + declaration + '>' + escapeXml(xmlString(value)) + '</' + qualified + '>';
        }

        export function xmlItems(values: unknown, write: (item: unknown) => string): string {
          return Array.isArray(values) ? values.filter((v) => v !== undefined && v !== null).map(write).join('') : '';
        }

        export function xmlEntries(values: unknown, write: (name: string, item: unknown) => string): string {
          return values !== null && typeof values === 'object'
            ? Object.entries(values as Record<string, unknown>).map(([key, item]) => write(encodeXmlName(key), item)).join('')
            : '';
        }

        export function xmlOptionalElement(value: unknown, name: string, namespace: string, prefix: string, content: string): string {
          return value === undefined || value === null ? '' : xmlElement(name, namespace, prefix, '', content);
        }

        """;
}