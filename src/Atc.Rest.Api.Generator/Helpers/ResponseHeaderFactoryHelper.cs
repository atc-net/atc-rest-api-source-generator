namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Gives the server's result factories the headers their response declares: each header is an optional, typed
/// parameter after the parameters the factory has - so no call that compiled changes its meaning, and a header a
/// middleware writes (a rate limit, a version) need not be the handler's - and is written with the result. A header
/// that is not given is not written, and a factory called without any returns the result it always did.
/// </summary>
/// <remarks>
/// Not a parameter: <c>Content-Type</c> and <c>Content-Length</c> (the body's own; OpenAPI says a declared
/// <c>Content-Type</c> is ignored), and a header the factory already writes from a parameter it has
/// (<c>Location</c>, <c>Retry-After</c>).
/// </remarks>
public static class ResponseHeaderFactoryHelper
{
    /// <summary>
    /// What the generated code calls to write the headers: the marker the server generator looks for to emit
    /// <see cref="ResponseHeadersSource"/>.
    /// </summary>
    public const string WriterCall = "ResponseHeaders.Of(";

    // The headers HTTP defines as an HTTP date (RFC 9110 §5.6.7); any other date-time is written as RFC 3339
    private static readonly HashSet<string> HttpDateHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Date",
        "Expires",
        "Last-Modified",
        "Retry-After",
        "Sunset",
    };

    /// <summary>
    /// The factories of one response, each with the response's declared headers as parameters.
    /// </summary>
    /// <param name="factories">The factory methods generated for the response.</param>
    /// <param name="response">The response.</param>
    /// <param name="registry">The type names the models were generated under (an enum header's type).</param>
    /// <param name="operationName">The operation's name in Pascal case: an inline enum header is its type (see <see cref="InlineEnumsOf"/>); text without it.</param>
    /// <param name="componentTypeNames">The C# names of the component schemas, which an inline enum's name gives way to.</param>
    /// <returns>The factories; unchanged when the response declares no header a factory can take.</returns>
    public static List<MethodParameters> WithDeclaredHeaders(
        List<MethodParameters> factories,
        IOpenApiResponse response,
        TypeConflictRegistry? registry = null,
        string? operationName = null,
        ISet<string>? componentTypeNames = null)
    {
        if (factories is null)
        {
            throw new ArgumentNullException(nameof(factories));
        }

        var declared = (response?.Headers ?? new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))
            .Where(h => h.Value is not null && !IsTheBodysOwn(h.Key))
            .ToList();
        if (declared.Count == 0)
        {
            return factories;
        }

        // An implicit conversion takes its one value: it calls a factory, which then writes no header
        var factoryNames = new HashSet<string>(factories.Where(f => f.Name.Length > 0).Select(f => f.Name), StringComparer.Ordinal);
        return factories.ConvertAll(factory => factory.DeclarationModifier == DeclarationModifiers.PublicStaticImplicitOperator
            ? factory
            : WithHeaders(factory, declared, factoryNames, registry, operationName, componentTypeNames));
    }

    /// <summary>
    /// The <c>ResponseHeaders</c> helper and the <c>ResponseHeadersHttpResult</c> the factories write their headers
    /// through, emitted once per Results namespace.
    /// </summary>
    /// <returns>The source of both types.</returns>
    public static string ResponseHeadersSource()
        => $$"""

            /// <summary>
            /// Writes the headers a response declares with the result of its factory.
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            internal static class ResponseHeaders
            {
                /// <summary>
                /// The result with the headers that have a value; the result itself when none has.
                /// </summary>
                public static IResult Of(
                    IResult inner,
                    params (string Name, string? Value)[] headers)
                {
                    foreach (var header in headers)
                    {
                        if (header.Value is not null)
                        {
                            return new ResponseHeadersHttpResult(inner, headers);
                        }
                    }

                    return inner;
                }

                public static string? Text(bool? value)
                    => value is null ? null : value.Value ? "true" : "false";

                public static string? Text(int? value)
                    => value?.ToString(global::System.Globalization.CultureInfo.InvariantCulture);

                public static string? Text(long? value)
                    => value?.ToString(global::System.Globalization.CultureInfo.InvariantCulture);

                public static string? Text(float? value)
                    => value?.ToString("R", global::System.Globalization.CultureInfo.InvariantCulture);

                public static string? Text(double? value)
                    => value?.ToString("R", global::System.Globalization.CultureInfo.InvariantCulture);

                public static string? Text(global::System.Guid? value)
                    => value?.ToString("D");

                public static string? Text(global::System.DateOnly? value)
                    => value?.ToString("yyyy-MM-dd", global::System.Globalization.CultureInfo.InvariantCulture);

                /// <summary>
                /// A date-time as RFC 3339.
                /// </summary>
                public static string? Text(global::System.DateTimeOffset? value)
                    => value?.ToString("O", global::System.Globalization.CultureInfo.InvariantCulture);

                /// <summary>
                /// A date-time as the HTTP date (RFC 9110) of the headers HTTP defines as one (<c>Last-Modified</c>).
                /// </summary>
                public static string? HttpDate(global::System.DateTimeOffset? value)
                    => value?.ToUniversalTime().ToString("R", global::System.Globalization.CultureInfo.InvariantCulture);

                /// <summary>
                /// An array as its values joined by commas (OpenAPI's <c>simple</c> style).
                /// </summary>
                public static string? Text<T>(global::System.Collections.Generic.IEnumerable<T>? values)
                {
                    if (values is null)
                    {
                        return null;
                    }

                    var texts = new global::System.Collections.Generic.List<string>();
                    foreach (var value in values)
                    {
                        texts.Add(global::System.Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
                    }

                    return string.Join(",", texts);
                }

                /// <summary>
                /// An object's properties in OpenAPI's <c>simple</c> style - <c>name,value,…</c>, or exploded
                /// <c>name=value,…</c>; a property without a value is left out.
                /// </summary>
                public static string? Fields(
                    bool explode,
                    params (string Name, string? Value)[] fields)
                {
                    var texts = new global::System.Collections.Generic.List<string>();
                    foreach (var field in fields)
                    {
                        if (field.Value is not null)
                        {
                            texts.Add(explode ? field.Name + "=" + field.Value : field.Name + "," + field.Value);
                        }
                    }

                    return string.Join(",", texts);
                }

                /// <summary>
                /// An array of enum values as their wire values joined by commas.
                /// </summary>
                public static string? Text<T>(
                    global::System.Collections.Generic.IEnumerable<T>? values,
                    global::System.Func<T, string> wireValue)
                {
                    if (values is null)
                    {
                        return null;
                    }

                    var texts = new global::System.Collections.Generic.List<string>();
                    foreach (var value in values)
                    {
                        texts.Add(wireValue(value));
                    }

                    return string.Join(",", texts);
                }
            }

            /// <summary>
            /// Writes the wrapped result with the headers that have a value. Its status code and its value are the
            /// wrapped result's, so a test reads them the same way with or without headers; <see cref="Result"/> is the
            /// wrapped result itself.
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            internal sealed class ResponseHeadersHttpResult : IResult, IStatusCodeHttpResult, IValueHttpResult, INestedHttpResult, IHeaderWritingHttpResult
            {
                private readonly (string Name, string? Value)[] headers;

                public ResponseHeadersHttpResult(
                    IResult inner,
                    (string Name, string? Value)[] headers)
                {
                    Result = inner;
                    this.headers = headers;
                }

                public IResult Result { get; }

                public int? StatusCode
                    => (Result as IStatusCodeHttpResult)?.StatusCode;

                public object? Value
                    => (Result as IValueHttpResult)?.Value;

                public IEnumerable<KeyValuePair<string, string>> Headers
                {
                    get
                    {
                        foreach (var header in headers)
                        {
                            if (header.Value is not null)
                            {
                                yield return new KeyValuePair<string, string>(header.Name, header.Value);
                            }
                        }
                    }
                }

                public global::System.Threading.Tasks.Task ExecuteAsync(HttpContext httpContext)
                {
                    foreach (var header in headers)
                    {
                        if (header.Value is not null)
                        {
                            httpContext.Response.Headers[header.Name] = header.Value;
                        }
                    }

                    return Result.ExecuteAsync(httpContext);
                }
            }
            """;

    /// <summary>
    /// The headers the responses of an operation declare, one per name: what a client's result reads back. The same
    /// name declared with different types is text.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="registry">The type names the models were generated under (an enum header's type).</param>
    /// <param name="operationName">The operation's name in Pascal case: an inline enum header is its type (see <see cref="InlineEnumsOf"/>); text without it.</param>
    /// <param name="componentTypeNames">The C# names of the component schemas, which an inline enum's name gives way to.</param>
    /// <returns>Per header: its name, the property a result reads it through, the C# type of one value, whether it is an array, and how a value is read.</returns>
    public static List<DeclaredResponseHeader> DeclaredBy(
        OpenApiOperation? operation,
        TypeConflictRegistry? registry = null,
        string? operationName = null,
        ISet<string>? componentTypeNames = null)
        => DeclaredByResponses(operation?.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>(), registry, operationName, componentTypeNames);

    /// <summary>
    /// The headers some responses declare, one per name, by the rules of <see cref="DeclaredBy"/>: what a result that
    /// stands for these responses reads back.
    /// </summary>
    /// <param name="responses">The responses.</param>
    /// <param name="registry">The type names the models were generated under (an enum header's type).</param>
    /// <param name="operationName">The operation's name in Pascal case: an inline enum header is its type; text without it.</param>
    /// <param name="componentTypeNames">The C# names of the component schemas, which an inline enum's name gives way to.</param>
    /// <returns>Per header: its name, the property a result reads it through, the C# type of one value, whether it is an array, and how a value is read.</returns>
    public static List<DeclaredResponseHeader> DeclaredByResponses(
        IEnumerable<IOpenApiResponse?> responses,
        TypeConflictRegistry? registry = null,
        string? operationName = null,
        ISet<string>? componentTypeNames = null)
    {
        if (responses is null)
        {
            throw new ArgumentNullException(nameof(responses));
        }

        var headers = new List<DeclaredResponseHeader>();
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var response in responses)
        {
            foreach (var header in response?.Headers ?? new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))
            {
                if (header.Value is null || IsTheBodysOwn(header.Key))
                {
                    continue;
                }

                var inlineEnumTypeName = operationName is null ? null : InlineEnumTypeName(operationName, header.Key, componentTypeNames);
                var (type, isArray, kind) = ValueOf(header.Value.Schema, registry, inlineEnumTypeName);
                var fields = kind == ResponseHeaderValueKind.Model ? ObjectFieldsOf(header.Value.Schema, registry) : null;
                var explode = kind == ResponseHeaderValueKind.Model && header.Value.Explode;
                var index = headers.FindIndex(h => string.Equals(h.Name, header.Key, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    if (headers[index].Type != type || headers[index].IsArray != isArray || headers[index].Kind != kind || headers[index].Explode != explode)
                    {
                        headers[index] = headers[index] with { Type = "string", IsArray = false, Kind = ResponseHeaderValueKind.Scalar, Fields = null, Explode = false };
                    }

                    continue;
                }

                var propertyName = taken.Claim(header.Key.ToPascalCaseForDotNet().WithIdentifierStart("Response") + "Header");
                headers.Add(new DeclaredResponseHeader(header.Key, propertyName, type, isArray, kind, fields, explode));
            }
        }

        return headers;
    }

    /// <summary>
    /// The enums an operation's response headers declare inline (an <c>enum</c> in the header's schema, or in its
    /// array's items): each is generated as <c>{Operation}{Header}Header</c>, once per header name.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="operationName">The operation's name in Pascal case.</param>
    /// <param name="componentTypeNames">The C# names of the component schemas, which an inline enum's name gives way to.</param>
    /// <returns>Per enum: its type name and its schema.</returns>
    public static List<(string TypeName, OpenApiSchema Schema)> InlineEnumsOf(
        OpenApiOperation? operation,
        string operationName,
        ISet<string>? componentTypeNames)
    {
        var enums = new List<(string TypeName, OpenApiSchema Schema)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var response in operation?.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
        {
            foreach (var header in response?.Headers ?? new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))
            {
                if (header.Value is null ||
                    IsTheBodysOwn(header.Key) ||
                    InlineEnumSchemaOf(header.Value.Schema) is not { } schema ||
                    !seen.Add(header.Key))
                {
                    continue;
                }

                enums.Add((InlineEnumTypeName(operationName, header.Key, componentTypeNames), schema));
            }
        }

        return enums;
    }

    /// <summary>
    /// The typed client's <c>{Operation}ResponseHeaders</c>: the headers the responses of an operation declare, read as
    /// the EndpointPerOperation result reads them, from any headers - a Result-style response's
    /// (<c>EndpointResponse.Headers</c>) or the Throw style's <c>ApiException.Headers</c> - whatever their casing.
    /// </summary>
    /// <param name="namespaceName">The client's namespace.</param>
    /// <param name="usings">The namespaces of the models the headers' types are in.</param>
    /// <param name="className">The class's name.</param>
    /// <param name="operationId">The operation's id, for its documentation.</param>
    /// <param name="headers">The headers, from <see cref="DeclaredBy"/>.</param>
    /// <param name="extensibleEnums">Whether the client's string enums are extensible.</param>
    /// <returns>The source of the file.</returns>
    public static string TypedClientHeadersSource(
        string namespaceName,
        IEnumerable<string> usings,
        string className,
        string operationId,
        List<DeclaredResponseHeader> headers,
        bool extensibleEnums)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Collections.Generic;");
        foreach (var name in usings ?? [])
        {
            sb.AppendLine($"using {name};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// The headers the responses of <c>{(operationId ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")}</c> declare, typed: build it from a response's headers, or an");
        sb.AppendLine("/// exception's.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public sealed class {className}");
        sb.AppendLine("{");
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Reads the headers, whatever the casing of their names.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "/// <param name=\"headers\">A response's headers, or an exception's.</param>");
        sb.AppendLine(4, $"public {className}(IReadOnlyDictionary<string, IEnumerable<string>> headers)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "if (headers is null)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "throw new ArgumentNullException(nameof(headers));");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "var lookup = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine(8, "foreach (var header in headers)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "lookup[header.Key] = header.Value;");
        sb.AppendLine(8, "}");
        sb.AppendLine();
        sb.AppendLine(8, "Headers = lookup;");
        sb.AppendLine(4, "}");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// The headers, as given.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; }");
        sb.AppendLine();
        foreach (var line in ClientProperties(headers, extensibleEnums))
        {
            if (line.Length == 0)
            {
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine(4, line);
            }
        }

        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// The declaration of a client result's header properties, for its interface.
    /// </summary>
    /// <param name="headers">The headers, from <see cref="DeclaredBy"/>.</param>
    /// <returns>One line per property.</returns>
    public static List<string> ClientInterfaceProperties(
        List<DeclaredResponseHeader> headers)
        => (headers ?? []).ConvertAll(h => $"{ClientType(h.Type, h.IsArray)} {h.PropertyName} {{ get; }}");

    /// <summary>
    /// The header properties of a client result class: each reads its header from the response's <c>Headers</c> by
    /// name, whatever the casing, and is null when the header is absent or is not a value of its type. An extensible
    /// enum keeps a value the specification does not list (it is open); a classic enum reads it as null.
    /// </summary>
    /// <param name="headers">The headers, from <see cref="DeclaredBy"/>.</param>
    /// <param name="extensibleEnums">Whether the client's string enums are extensible (they parse any value).</param>
    /// <returns>The members' source lines, without indentation; none without headers.</returns>
    public static List<string> ClientProperties(
        List<DeclaredResponseHeader> headers,
        bool extensibleEnums = true)
    {
        var lines = new List<string>();
        if (headers is null || headers.Count == 0)
        {
            return lines;
        }

        foreach (var header in headers)
        {
            var name = header.Name.Replace("\\", "\\\\").Replace("\"", "\\\"");
            lines.Add("/// <summary>");
            lines.Add($"/// The <c>{header.Name.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")}</c> response header; null when it is absent or is not a value of this type.");
            lines.Add("/// </summary>");
            if (header.Kind == ResponseHeaderValueKind.Model)
            {
                lines.Add($"public {ClientType(header.Type, isArray: false)} {header.PropertyName}");
                lines.Add($"    => Read{header.PropertyName}(HeaderText(\"{name}\"));");
                lines.Add(string.Empty);
                continue;
            }

            if (!header.IsArray)
            {
                lines.Add($"public {ClientType(header.Type, isArray: false)} {header.PropertyName}");
                lines.Add($"    => {Parsed(header.Type, header.Kind, extensibleEnums, $"HeaderText(\"{name}\")", "value")};");
                lines.Add(string.Empty);
                continue;
            }

            lines.Add($"public {ClientType(header.Type, isArray: true)} {header.PropertyName}");
            lines.Add("{");
            lines.Add("    get");
            lines.Add("    {");
            lines.Add($"        if (HeaderText(\"{name}\") is not {{ }} text)");
            lines.Add("        {");
            lines.Add("            return null;");
            lines.Add("        }");
            lines.Add(string.Empty);
            lines.Add($"        var values = new global::System.Collections.Generic.List<{header.Type}>();");
            lines.Add("        foreach (var part in text.Split(','))");
            lines.Add("        {");
            lines.Add($"            if ({Parsed(header.Type, header.Kind, extensibleEnums, "part.Trim()", "item")} is not {{ }} parsed)");
            lines.Add("            {");
            lines.Add("                return null;");
            lines.Add("            }");
            lines.Add(string.Empty);
            lines.Add("            values.Add(parsed);");
            lines.Add("        }");
            lines.Add(string.Empty);
            lines.Add("        return values;");
            lines.Add("    }");
            lines.Add("}");
            lines.Add(string.Empty);
        }

        lines.Add("// A header's values as one text (several are joined by commas); the lookup ignores the casing of the name.");
        lines.Add("private string? HeaderText(string name)");
        lines.Add("    => Headers.TryGetValue(name, out var values) ? string.Join(\",\", values) : null;");

        foreach (var header in headers.Where(h => h.Kind == ResponseHeaderValueKind.Model && h.Fields is not null))
        {
            lines.Add(string.Empty);
            lines.AddRange(ObjectReader(header, extensibleEnums));
        }

        if (headers.Any(h => h.Kind == ResponseHeaderValueKind.Model))
        {
            lines.Add(string.Empty);
            lines.Add("// An object's properties in OpenAPI's simple style - name,value,… or, exploded, name=value,…; null when the");
            lines.Add("// text is not one");
            lines.Add("private static global::System.Collections.Generic.Dictionary<string, string>? HeaderFields(");
            lines.Add("    string text,");
            lines.Add("    bool explode)");
            lines.Add("{");
            lines.Add("    var parts = text.Split(',');");
            lines.Add("    var fields = new global::System.Collections.Generic.Dictionary<string, string>(global::System.StringComparer.Ordinal);");
            lines.Add("    if (explode)");
            lines.Add("    {");
            lines.Add("        foreach (var part in parts)");
            lines.Add("        {");
            lines.Add("            var separator = part.IndexOf('=');");
            lines.Add("            if (separator <= 0)");
            lines.Add("            {");
            lines.Add("                return null;");
            lines.Add("            }");
            lines.Add(string.Empty);
            lines.Add("            fields[part.Substring(0, separator).Trim()] = part.Substring(separator + 1).Trim();");
            lines.Add("        }");
            lines.Add(string.Empty);
            lines.Add("        return fields;");
            lines.Add("    }");
            lines.Add(string.Empty);
            lines.Add("    if (parts.Length % 2 != 0)");
            lines.Add("    {");
            lines.Add("        return null;");
            lines.Add("    }");
            lines.Add(string.Empty);
            lines.Add("    for (var i = 0; i < parts.Length; i += 2)");
            lines.Add("    {");
            lines.Add("        fields[parts[i].Trim()] = parts[i + 1].Trim();");
            lines.Add("    }");
            lines.Add(string.Empty);
            lines.Add("    return fields;");
            lines.Add("}");
        }

        var readsAClassicEnum = headers.Any(h => h.Kind == ResponseHeaderValueKind.StringEnum) ||
                                headers.Any(h => h.Fields?.Any(f => f.Kind == ResponseHeaderValueKind.StringEnum) == true);
        if (!extensibleEnums && readsAClassicEnum)
        {
            lines.Add(string.Empty);
            lines.Add("// The member of a classic enum whose wire value the text is; null for a value the specification does not list");
            lines.Add("private static T? HeaderEnum<T>(");
            lines.Add("    string? text,");
            lines.Add("    global::System.Func<T, string> wireValue)");
            lines.Add("    where T : struct, global::System.Enum");
            lines.Add("{");
            lines.Add("    if (text is null)");
            lines.Add("    {");
            lines.Add("        return null;");
            lines.Add("    }");
            lines.Add(string.Empty);
            lines.Add("    foreach (var value in global::System.Enum.GetValues<T>())");
            lines.Add("    {");
            lines.Add("        if (string.Equals(wireValue(value), text.Trim(), global::System.StringComparison.Ordinal))");
            lines.Add("        {");
            lines.Add("            return value;");
            lines.Add("        }");
            lines.Add("    }");
            lines.Add(string.Empty);
            lines.Add("    return null;");
            lines.Add("}");
        }

        return lines;
    }

    private static string ClientType(
        string type,
        bool isArray)
        => isArray
            ? $"global::System.Collections.Generic.IReadOnlyList<{type}>?"
            : type + "?";

    // The expression that is the text as a value of the header's type, or null
    private static string Parsed(
        string type,
        ResponseHeaderValueKind kind,
        bool extensibleEnums,
        string text,
        string variable)
    {
        const string invariant = "global::System.Globalization.CultureInfo.InvariantCulture";
        return kind switch
        {
            // An extensible enum is open: any value is one of it, known or not
            ResponseHeaderValueKind.StringEnum when extensibleEnums => $"({type}.TryParse({text}, out var {variable}) ? {variable} : ({type}?)null)",
            ResponseHeaderValueKind.StringEnum => $"HeaderEnum<{type}>({text}, static value => value.ToWireValue())",
            ResponseHeaderValueKind.IntegerEnum => $"(long.TryParse({text}, global::System.Globalization.NumberStyles.Integer, {invariant}, out var {variable}) && global::System.Enum.IsDefined(({type}){variable}) ? ({type}){variable} : ({type}?)null)",
            _ => type switch
            {
                "string" => text,
                "bool" => $"(bool.TryParse({text}, out var {variable}) ? {variable} : (bool?)null)",
                "int" => $"(int.TryParse({text}, global::System.Globalization.NumberStyles.Integer, {invariant}, out var {variable}) ? {variable} : (int?)null)",
                "long" => $"(long.TryParse({text}, global::System.Globalization.NumberStyles.Integer, {invariant}, out var {variable}) ? {variable} : (long?)null)",
                "float" => $"(float.TryParse({text}, global::System.Globalization.NumberStyles.Float, {invariant}, out var {variable}) ? {variable} : (float?)null)",
                "double" => $"(double.TryParse({text}, global::System.Globalization.NumberStyles.Float, {invariant}, out var {variable}) ? {variable} : (double?)null)",
                "decimal" => $"(decimal.TryParse({text}, global::System.Globalization.NumberStyles.Float, {invariant}, out var {variable}) ? {variable} : (decimal?)null)",
                "global::System.Guid" => $"(global::System.Guid.TryParse({text}, out var {variable}) ? {variable} : (global::System.Guid?)null)",
                "global::System.DateOnly" => $"(global::System.DateOnly.TryParseExact({text}, \"yyyy-MM-dd\", {invariant}, global::System.Globalization.DateTimeStyles.None, out var {variable}) ? {variable} : (global::System.DateOnly?)null)",
                _ => $"(global::System.DateTimeOffset.TryParse({text}, {invariant}, global::System.Globalization.DateTimeStyles.AssumeUniversal, out var {variable}) ? {variable} : (global::System.DateTimeOffset?)null)",
            },
        };
    }

    // A client result's reader of an object header: each property from its field, the model built by name; null when
    // the text is not the object - a required property missing, or a property that is not a value of its type
    private static List<string> ObjectReader(
        DeclaredResponseHeader header,
        bool extensibleEnums)
    {
        var fields = header.Fields!;
        var lines = new List<string>
        {
            $"private static {header.Type}? Read{header.PropertyName}(string? text)",
            "{",
            $"    if (text is null || HeaderFields(text, explode: {(header.Explode ? "true" : "false")}) is not {{ }} fields)",
            "    {",
            "        return null;",
            "    }",
            string.Empty,
        };

        var arguments = new List<string>();
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var wireName = field.WireName.Replace("\\", "\\\\").Replace("\"", "\\\"");
            lines.Add($"    fields.TryGetValue(\"{wireName}\", out var text{i});");
            lines.Add($"    var field{i} = {Parsed(field.Type, field.Kind, extensibleEnums, $"text{i}", $"parsed{i}")};");
            lines.Add(field.IsRequired ? $"    if (field{i} is null)" : $"    if (text{i} is not null && field{i} is null)");
            lines.Add("    {");
            lines.Add("        return null;");
            lines.Add("    }");
            lines.Add(string.Empty);
            arguments.Add(field.IsRequired && field.Type != "string"
                ? $"{field.PropertyName}: field{i}.Value"
                : $"{field.PropertyName}: field{i}");
        }

        lines.Add($"    return new {header.Type}({string.Join(", ", arguments)});");
        lines.Add("}");
        return lines;
    }

    // The server's text of an object header: its properties as fields, when the handler gave the object
    private static string ObjectText(
        string parameterName,
        List<ResponseHeaderField> fields,
        bool explode)
    {
        var value = parameterName + "Fields";
        var texts = fields.ConvertAll(f =>
        {
            var access = $"{value}.{f.PropertyName}";
            var fieldText = f.Kind switch
            {
                ResponseHeaderValueKind.StringEnum => $"{access}.ToWireValue()",
                ResponseHeaderValueKind.IntegerEnum => $"ResponseHeaders.Text((long?){access})",
                _ => f.Type == "string" ? access : $"ResponseHeaders.Text({access})",
            };
            return $"(\"{f.WireName.Replace("\\", "\\\\").Replace("\"", "\\\"")}\", {fieldText})";
        });

        return $"{parameterName} is {{ }} {value} ? ResponseHeaders.Fields({(explode ? "true" : "false")}, {string.Join(", ", texts)}) : null";
    }

    // The properties of an object a header carries, as its model declares them. Null when the header cannot carry it
    // in the simple style: no plain object (a composition, a dictionary), a property that is not one value (a list, a
    // model, an inline enum), one whose model type is not the header's (a widened number, a Uri), or one with a
    // default (the model's parameter is then not nullable).
    private static List<ResponseHeaderField>? ObjectFieldsOf(
        IOpenApiSchema? schema,
        TypeConflictRegistry? registry)
    {
        if (schema is not OpenApiSchemaReference { Target: OpenApiSchema target } reference ||
            reference.Reference.Id is not { Length: > 0 } id ||
            !target.IsOfType(JsonSchemaType.Object) ||
            target.Properties is not { Count: > 0 } properties ||
            target.AllOf is { Count: > 0 } ||
            target.OneOf is { Count: > 0 } ||
            target.AnyOf is { Count: > 0 } ||
            target.AdditionalProperties is not null)
        {
            return null;
        }

        var modelName = OpenApiSchemaExtensions.ResolveTypeName(id, registry);
        var required = target.Required ?? new HashSet<string>(StringComparer.Ordinal);
        var names = new PropertyNameRegistry();
        var fields = new List<ResponseHeaderField>();
        foreach (var property in properties)
        {
            // Named as the model names it
            var propertyName = names.Resolve(property.Key, property.Key.ToPascalCaseForDotNet());
            if (propertyName is null)
            {
                continue;
            }

            if (propertyName.Equals(modelName, StringComparison.OrdinalIgnoreCase))
            {
                propertyName += "Value";
            }

            var resolved = property.Value.Dereferenced;
            if (resolved is not OpenApiSchema propertySchema ||
                propertySchema.Default is not null ||
                propertySchema.IsOfType(JsonSchemaType.Object) ||
                propertySchema.IsOfType(JsonSchemaType.Array) ||
                ValueTypeOf(property.Value, registry) is not { } value)
            {
                return null;
            }

            // A readOnly or writeOnly property is optional in the model, as it is here
            var isRequired = required.Contains(property.Key) && !propertySchema.ReadOnly && !propertySchema.WriteOnly;
            var modelType = property.Value.ToCSharpTypeForModel(isRequired, registry).TrimEnd('?');
            var headerType = value.Kind == ResponseHeaderValueKind.Scalar ? ModelTypeOfScalar(value.Type) : value.Type;
            if (!string.Equals(modelType, headerType, StringComparison.Ordinal))
            {
                return null;
            }

            fields.Add(new ResponseHeaderField(property.Key, propertyName, value.Type, value.Kind, isRequired));
        }

        return fields.Count > 0 ? fields : null;
    }

    // A header scalar's type as a model's property names it
    private static string ModelTypeOfScalar(string scalar)
        => scalar switch
        {
            "global::System.Guid" => "Guid",
            "global::System.DateTimeOffset" => "DateTimeOffset",
            "global::System.DateOnly" => "DateOnly",
            _ => scalar,
        };

    // The C# type of one value of a header, whether the header is an array of them, and how a value is read;
    // anything else is text
    private static (string Type, bool IsArray, ResponseHeaderValueKind Kind) ValueOf(
        IOpenApiSchema? schema,
        TypeConflictRegistry? registry,
        string? inlineEnumTypeName)
    {
        if (schema is OpenApiSchema { Items: { } items } array &&
            array.IsOfType(JsonSchemaType.Array) &&
            ValueTypeOf(items, registry, inlineEnumTypeName) is { Kind: not ResponseHeaderValueKind.Model } item)
        {
            return (item.Type, true, item.Kind);
        }

        return ValueTypeOf(schema, registry, inlineEnumTypeName) is { } value
            ? (value.Type, false, value.Kind)
            : ("string", false, ResponseHeaderValueKind.Scalar);
    }

    private static MethodParameters WithHeaders(
        MethodParameters factory,
        List<KeyValuePair<string, IOpenApiHeader>> declared,
        HashSet<string> factoryNames,
        TypeConflictRegistry? registry,
        string? operationName,
        ISet<string>? componentTypeNames)
    {
        var parameters = (factory.Parameters ?? []).ToList();
        var taken = new HashSet<string>(parameters.Select(p => p.Name), StringComparer.Ordinal);

        var written = new List<string>();
        var passedOn = new List<string>();
        foreach (var header in declared)
        {
            if (IsWrittenByTheFactory(header.Key, taken))
            {
                continue;
            }

            var name = ParameterName(header.Key, taken);
            var inlineEnumTypeName = operationName is null ? null : InlineEnumTypeName(operationName, header.Key, componentTypeNames);
            var (typeName, isReference, text) = TypeOf(header.Key, header.Value.Schema, name, registry, inlineEnumTypeName, header.Value.Explode);
            parameters.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: typeName,
                IsNullableType: true,
                IsReferenceType: isReference,
                Name: name,
                DefaultValue: "null"));
            written.Add($"(\"{header.Key.Replace("\\", "\\\\").Replace("\"", "\\\"")}\", {text})");
            passedOn.Add($"{name}: {name}");
        }

        if (written.Count == 0 || string.IsNullOrEmpty(factory.Content))
        {
            return factory;
        }

        return factory with
        {
            Parameters = parameters,
            Content = WithHeadersWritten(factory.Content!, written, passedOn, factoryNames),
        };
    }

    // A factory's body is one of: new(<result>), a call of another factory of the response (which takes the headers
    // itself), or - nothing generates it today - any other expression of the result class
    private static string WithHeadersWritten(
        string content,
        List<string> written,
        List<string> passedOn,
        HashSet<string> factoryNames)
    {
        var headers = string.Join(", ", written);
        if (content.StartsWith("new(", StringComparison.Ordinal) && content.EndsWith(")", StringComparison.Ordinal))
        {
            var result = content.Substring("new(".Length, content.Length - "new(".Length - 1);
            return $"new({WriterCall}{result}, {headers}))";
        }

        var open = content.IndexOf('(');
        if (open > 0 && factoryNames.Contains(content.Substring(0, open)) && content.EndsWith(")", StringComparison.Ordinal))
        {
            var arguments = content.Substring(open + 1, content.Length - open - 2);
            var separator = arguments.Trim().Length == 0 ? string.Empty : ", ";
            return $"{content.Substring(0, open)}({arguments}{separator}{string.Join(", ", passedOn)})";
        }

        return $"new({WriterCall}({content}).Result, {headers}))";
    }

    private static bool IsWrittenByTheFactory(
        string headerName,
        HashSet<string> parameterNames)
        => (string.Equals(headerName, "Location", StringComparison.OrdinalIgnoreCase) &&
            (parameterNames.Contains("location") || parameterNames.Contains("uri"))) ||
           (string.Equals(headerName, "Retry-After", StringComparison.OrdinalIgnoreCase) &&
            parameterNames.Any(p => p.StartsWith("retryAfter", StringComparison.Ordinal)));

    private static string ParameterName(
        string headerName,
        HashSet<string> taken)
    {
        var pascal = headerName.ToPascalCaseForDotNet().WithIdentifierStart("Header");
        var name = IdentifierCasingHelper.ToLower(pascal[0]) + pascal.Substring(1);
        if (CSharpKeywords.IsKeyword(name) || taken.Contains(name))
        {
            name += "Header";
        }

        return taken.Claim(name);
    }

    private static (string TypeName, bool IsReference, string Text) TypeOf(
        string headerName,
        IOpenApiSchema? schema,
        string parameterName,
        TypeConflictRegistry? registry,
        string? inlineEnumTypeName,
        bool explode)
    {
        if (schema is OpenApiSchema { Items: { } items } array &&
            array.IsOfType(JsonSchemaType.Array) &&
            ValueTypeOf(items, registry, inlineEnumTypeName) is { Kind: not ResponseHeaderValueKind.Model } item)
        {
            var listType = $"global::System.Collections.Generic.IEnumerable<{item.Type}>";
            return item.Kind switch
            {
                ResponseHeaderValueKind.StringEnum => (listType, true, $"ResponseHeaders.Text({parameterName}, static value => value.ToWireValue())"),
                ResponseHeaderValueKind.IntegerEnum => (listType, true, $"ResponseHeaders.Text({parameterName}, static value => ((long)value).ToString(global::System.Globalization.CultureInfo.InvariantCulture))"),
                _ => (listType, true, $"ResponseHeaders.Text({parameterName})"),
            };
        }

        if (ValueTypeOf(schema, registry, inlineEnumTypeName) is not { } value || value.Type == "string")
        {
            return ("string", true, parameterName);
        }

        return value.Kind switch
        {
            // An enum is written as the specification writes it: a string enum by its value, an integer enum by its number
            ResponseHeaderValueKind.StringEnum => (value.Type, false, $"{parameterName}.ToWireValue()"),
            ResponseHeaderValueKind.IntegerEnum => (value.Type, false, $"ResponseHeaders.Text((long?){parameterName})"),
            ResponseHeaderValueKind.Model => (value.Type, true, ObjectText(parameterName, ObjectFieldsOf(schema, registry)!, explode)),
            _ => value.Type == "global::System.DateTimeOffset" && HttpDateHeaders.Contains(headerName)
                ? (value.Type, false, $"ResponseHeaders.HttpDate({parameterName})")
                : (value.Type, false, $"ResponseHeaders.Text({parameterName})"),
        };
    }

    private static bool IsTheBodysOwn(string headerName)
        => string.Equals(headerName, "Content-Type", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase);

    // The enum a header's schema declares inline: the schema itself, or its array's items
    private static OpenApiSchema? InlineEnumSchemaOf(IOpenApiSchema? schema)
    {
        if (schema.IsInlineEnum)
        {
            return (OpenApiSchema)schema!;
        }

        return schema is OpenApiSchema { Items: { } items } array &&
               array.IsOfType(JsonSchemaType.Array) &&
               items.IsInlineEnum
            ? (OpenApiSchema)items
            : null;
    }

    private static string InlineEnumTypeName(
        string operationName,
        string headerName,
        ISet<string>? componentTypeNames)
    {
        var pascal = headerName.ToPascalCaseForDotNet();
        return InlineEnumExtractor.GenerateInlineEnumTypeName(operationName, pascal + "Header", componentTypeNames);
    }

    // The C# type of one header value and how it is written and read: a scalar, or a generated enum - a component the
    // header's schema refers to, or the enum the header declares inline (inlineEnumTypeName; text without it). Null for
    // what is the text itself.
    private static (string Type, ResponseHeaderValueKind Kind)? ValueTypeOf(
        IOpenApiSchema? schema,
        TypeConflictRegistry? registry,
        string? inlineEnumTypeName = null)
    {
        if (inlineEnumTypeName is not null &&
            schema.IsInlineEnum &&
            schema is OpenApiSchema inline)
        {
            return inline.IsOfType(JsonSchemaType.String)
                ? (inlineEnumTypeName, ResponseHeaderValueKind.StringEnum)
                : (inlineEnumTypeName, ResponseHeaderValueKind.IntegerEnum);
        }

        if (schema is OpenApiSchemaReference reference)
        {
            if (reference.Target is not OpenApiSchema target)
            {
                return null;
            }

            if (target.Enum is { Count: > 0 } &&
                reference.Reference.Id is { Length: > 0 } id)
            {
                if (target.IsOfType(JsonSchemaType.String))
                {
                    return (OpenApiSchemaExtensions.ResolveTypeName(id, registry), ResponseHeaderValueKind.StringEnum);
                }

                if (target.IsOfType(JsonSchemaType.Integer))
                {
                    return (OpenApiSchemaExtensions.ResolveTypeName(id, registry), ResponseHeaderValueKind.IntegerEnum);
                }
            }

            // An object whose properties are all values is its model, in OpenAPI's simple style
            if (target.IsOfType(JsonSchemaType.Object))
            {
                return ObjectFieldsOf(reference, registry) is not null && reference.Reference.Id is { Length: > 0 } objectId
                    ? (OpenApiSchemaExtensions.ResolveTypeName(objectId, registry), ResponseHeaderValueKind.Model)
                    : null;
            }

            // A component that is a plain value (a uuid, a number) is that value
            return ScalarOf(target) is { } referenced ? (referenced, ResponseHeaderValueKind.Scalar) : null;
        }

        return schema is OpenApiSchema own && ScalarOf(own) is { } scalar
            ? (scalar, ResponseHeaderValueKind.Scalar)
            : null;
    }

    // The C# type of a header value that is one value; null for what is written as the text the handler gives
    private static string? ScalarOf(OpenApiSchema schema)
    {
        if (schema.IsOfType(JsonSchemaType.Boolean))
        {
            return "bool";
        }

        if (schema.IsOfType(JsonSchemaType.Integer))
        {
            return string.Equals(schema.Format, "int64", StringComparison.Ordinal) ? "long" : "int";
        }

        if (schema.IsOfType(JsonSchemaType.Number))
        {
            // As a model's property: float for format float, decimal for format decimal, double for any other
            return schema.Format switch
            {
                "float" => "float",
                "decimal" => "decimal",
                _ => "double",
            };
        }

        if (!schema.IsOfType(JsonSchemaType.String))
        {
            return null;
        }

        return schema.Format switch
        {
            "date-time" => "global::System.DateTimeOffset",
            "date" => "global::System.DateOnly",
            "uuid" => "global::System.Guid",
            _ => "string",
        };
    }
}