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
    /// <returns>The factories; unchanged when the response declares no header a factory can take.</returns>
    public static List<MethodParameters> WithDeclaredHeaders(
        List<MethodParameters> factories,
        IOpenApiResponse response)
    {
        if (factories is null)
        {
            throw new ArgumentNullException(nameof(factories));
        }

        var declared = (response?.Headers ?? new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))
            .Where(h => h.Value is not null &&
                        !string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(h.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (declared.Count == 0)
        {
            return factories;
        }

        // An implicit conversion takes its one value: it calls a factory, which then writes no header
        var factoryNames = new HashSet<string>(factories.Where(f => f.Name.Length > 0).Select(f => f.Name), StringComparer.Ordinal);
        return factories.ConvertAll(factory => factory.DeclarationModifier == DeclarationModifiers.PublicStaticImplicitOperator
            ? factory
            : WithHeaders(factory, declared, factoryNames));
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
            }

            /// <summary>
            /// Writes the wrapped result with the headers that have a value. Its status code and its value are the
            /// wrapped result's, so a test reads them the same way with or without headers.
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            internal sealed class ResponseHeadersHttpResult : IResult, IStatusCodeHttpResult, IValueHttpResult
            {
                private readonly IResult inner;
                private readonly (string Name, string? Value)[] headers;

                public ResponseHeadersHttpResult(
                    IResult inner,
                    (string Name, string? Value)[] headers)
                {
                    this.inner = inner;
                    this.headers = headers;
                }

                public int? StatusCode
                    => (inner as IStatusCodeHttpResult)?.StatusCode;

                public object? Value
                    => (inner as IValueHttpResult)?.Value;

                public global::System.Threading.Tasks.Task ExecuteAsync(HttpContext httpContext)
                {
                    foreach (var header in headers)
                    {
                        if (header.Value is not null)
                        {
                            httpContext.Response.Headers[header.Name] = header.Value;
                        }
                    }

                    return inner.ExecuteAsync(httpContext);
                }
            }
            """;

    /// <summary>
    /// The headers the responses of an operation declare, one per name: what a client's result reads back. The same
    /// name declared with different types is text.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>Per header: its name, the property a result reads it through, the C# type of one value and whether it is an array.</returns>
    public static List<(string Name, string PropertyName, string Scalar, bool IsArray)> DeclaredBy(
        OpenApiOperation? operation)
    {
        var headers = new List<(string Name, string PropertyName, string Scalar, bool IsArray)>();
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var response in operation?.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
        {
            foreach (var header in response?.Headers ?? new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))
            {
                if (header.Value is null ||
                    string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var (scalar, isArray) = ValueOf(header.Value.Schema);
                var index = headers.FindIndex(h => string.Equals(h.Name, header.Key, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    if (headers[index].Scalar != scalar || headers[index].IsArray != isArray)
                    {
                        headers[index] = (headers[index].Name, headers[index].PropertyName, "string", false);
                    }

                    continue;
                }

                var pascal = header.Key.ToPascalCaseForDotNet();
                if (pascal.Length == 0 || !(char.IsLetter(pascal[0]) || pascal[0] == '_'))
                {
                    pascal = "Response" + pascal;
                }

                var propertyName = pascal + "Header";
                var number = 1;
                while (!taken.Add(propertyName))
                {
                    number++;
                    propertyName = pascal + "Header" + number.ToString(CultureInfo.InvariantCulture);
                }

                headers.Add((header.Key, propertyName, scalar, isArray));
            }
        }

        return headers;
    }

    /// <summary>
    /// The declaration of a client result's header properties, for its interface.
    /// </summary>
    /// <param name="headers">The headers, from <see cref="DeclaredBy"/>.</param>
    /// <returns>One line per property.</returns>
    public static List<string> ClientInterfaceProperties(
        List<(string Name, string PropertyName, string Scalar, bool IsArray)> headers)
        => (headers ?? []).ConvertAll(h => $"{ClientType(h.Scalar, h.IsArray)} {h.PropertyName} {{ get; }}");

    /// <summary>
    /// The header properties of a client result class: each reads its header from the response's <c>Headers</c> by
    /// name, whatever the casing, and is null when the header is absent or is not a value of its type.
    /// </summary>
    /// <param name="headers">The headers, from <see cref="DeclaredBy"/>.</param>
    /// <returns>The members' source lines, without indentation; none without headers.</returns>
    public static List<string> ClientProperties(
        List<(string Name, string PropertyName, string Scalar, bool IsArray)> headers)
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
            if (!header.IsArray)
            {
                lines.Add($"public {ClientType(header.Scalar, isArray: false)} {header.PropertyName}");
                lines.Add($"    => {Parsed(header.Scalar, $"HeaderText(\"{name}\")", "value")};");
                lines.Add(string.Empty);
                continue;
            }

            lines.Add($"public {ClientType(header.Scalar, isArray: true)} {header.PropertyName}");
            lines.Add("{");
            lines.Add("    get");
            lines.Add("    {");
            lines.Add($"        if (HeaderText(\"{name}\") is not {{ }} text)");
            lines.Add("        {");
            lines.Add("            return null;");
            lines.Add("        }");
            lines.Add(string.Empty);
            lines.Add($"        var values = new global::System.Collections.Generic.List<{header.Scalar}>();");
            lines.Add("        foreach (var part in text.Split(','))");
            lines.Add("        {");
            lines.Add($"            if ({Parsed(header.Scalar, "part.Trim()", "item")} is not {{ }} parsed)");
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

        lines.Add("// A header's values as one text (several are joined by commas). Atc.Rest.Client looks a name up whatever its");
        lines.Add("// casing.");
        lines.Add("private string? HeaderText(string name)");
        lines.Add("    => Headers.TryGetValue(name, out var values) ? string.Join(\",\", values) : null;");
        return lines;
    }

    private static string ClientType(
        string scalar,
        bool isArray)
        => isArray
            ? $"global::System.Collections.Generic.IReadOnlyList<{scalar}>?"
            : scalar + "?";

    // The expression that is the text as a value of the type, or null
    private static string Parsed(
        string scalar,
        string text,
        string variable)
    {
        const string invariant = "global::System.Globalization.CultureInfo.InvariantCulture";
        return scalar switch
        {
            "string" => text,
            "bool" => $"(bool.TryParse({text}, out var {variable}) ? {variable} : (bool?)null)",
            "int" => $"(int.TryParse({text}, global::System.Globalization.NumberStyles.Integer, {invariant}, out var {variable}) ? {variable} : (int?)null)",
            "long" => $"(long.TryParse({text}, global::System.Globalization.NumberStyles.Integer, {invariant}, out var {variable}) ? {variable} : (long?)null)",
            "double" => $"(double.TryParse({text}, global::System.Globalization.NumberStyles.Float, {invariant}, out var {variable}) ? {variable} : (double?)null)",
            "global::System.Guid" => $"(global::System.Guid.TryParse({text}, out var {variable}) ? {variable} : (global::System.Guid?)null)",
            "global::System.DateOnly" => $"(global::System.DateOnly.TryParseExact({text}, \"yyyy-MM-dd\", {invariant}, global::System.Globalization.DateTimeStyles.None, out var {variable}) ? {variable} : (global::System.DateOnly?)null)",
            _ => $"(global::System.DateTimeOffset.TryParse({text}, {invariant}, global::System.Globalization.DateTimeStyles.AssumeUniversal, out var {variable}) ? {variable} : (global::System.DateTimeOffset?)null)",
        };
    }

    // The C# type of one value of a header and whether the header is an array of them; anything else is text
    private static (string Scalar, bool IsArray) ValueOf(IOpenApiSchema? schema)
    {
        if (schema is OpenApiSchema { Items: OpenApiSchema items } array &&
            array.IsOfType(JsonSchemaType.Array) &&
            ScalarOf(items) is { } itemType)
        {
            return (itemType, true);
        }

        return schema is OpenApiSchema own && ScalarOf(own) is { } scalar
            ? (scalar, false)
            : ("string", false);
    }

    private static MethodParameters WithHeaders(
        MethodParameters factory,
        List<KeyValuePair<string, IOpenApiHeader>> declared,
        HashSet<string> factoryNames)
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
            taken.Add(name);
            var (typeName, isReference, text) = TypeOf(header.Key, header.Value.Schema, name);
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
        var pascal = headerName.ToPascalCaseForDotNet();
        if (pascal.Length == 0 || !(char.IsLetter(pascal[0]) || pascal[0] == '_'))
        {
            pascal = "Header" + pascal;
        }

        var name = char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
        if (CSharpKeywords.IsKeyword(name) || taken.Contains(name))
        {
            name += "Header";
        }

        var unique = name;
        var number = 1;
        while (taken.Contains(unique))
        {
            number++;
            unique = name + number.ToString(CultureInfo.InvariantCulture);
        }

        return unique;
    }

    private static (string TypeName, bool IsReference, string Text) TypeOf(
        string headerName,
        IOpenApiSchema? schema,
        string parameterName)
    {
        if (schema is OpenApiSchema { Items: OpenApiSchema items } array &&
            array.IsOfType(JsonSchemaType.Array) &&
            ScalarOf(items) is { } itemType)
        {
            return ($"global::System.Collections.Generic.IEnumerable<{itemType}>", true, $"ResponseHeaders.Text({parameterName})");
        }

        if (schema is not OpenApiSchema own || ScalarOf(own) is not { } scalar || scalar == "string")
        {
            return ("string", true, parameterName);
        }

        return scalar == "global::System.DateTimeOffset" && HttpDateHeaders.Contains(headerName)
            ? (scalar, false, $"ResponseHeaders.HttpDate({parameterName})")
            : (scalar, false, $"ResponseHeaders.Text({parameterName})");
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
            return "double";
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