namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Generates the XML mapping of a model: a static <c>{Model}Xml</c> class next to the record that writes and reads the
/// XML the OpenAPI <c>xml</c> keyword describes (element and attribute names, namespaces and prefixes, wrapped and
/// unwrapped arrays), plus the shared <c>XmlModelSupport</c> helpers (safe reading: no DTD, no resolver).
/// </summary>
/// <remarks>
/// The built-in serializers cannot be used: <c>XmlSerializer</c> and <c>DataContractSerializer</c> both need a
/// parameterless constructor, and the models are positional records. So the mapping constructs the record by named
/// arguments, the way System.Text.Json source generation does for JSON.
/// <list type="bullet">
/// <item>Names: <c>xml.name</c> (a <c>'##default'</c> name is ignored), else the property / schema name. In an array,
/// <c>items.xml.name</c> names the items; the array's own <c>xml.name</c> names the wrapper, and only when
/// <c>wrapped</c> (then it also names unnamed items) - the OpenAPI XML Object examples.</item>
/// <item>Namespaces: an element or attribute with <c>xml.namespace</c> is written in it (with <c>xml.prefix</c>); an
/// element without one stays in its parent's default namespace, so <c>&lt;Doc xmlns="..."&gt;&lt;title&gt;</c> has
/// no <c>xmlns=""</c> noise. Reading matches local names only, so another producer's prefixes and namespaces are
/// read too.</item>
/// <item>Dictionaries: one element per entry, named by the (XML-encoded) key.</item>
/// <item>Values: the XML Schema lexical forms (<c>XmlConvert</c>); enums by their specification values.</item>
/// </list>
/// </remarks>
public static class XmlModelMapperExtractor
{
    /// <summary>The name of the generated helper class.</summary>
    public const string SupportTypeName = "XmlModelSupport";

    private const string Xml = "global::System.Xml";
    private const string XmlConvert = "global::System.Xml.XmlConvert";
    private const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";

    private static readonly Dictionary<string, (string Format, string Parse)> Scalars = new(StringComparer.Ordinal)
    {
        ["bool"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToBoolean"),
        ["byte"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToByte"),
        ["sbyte"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToSByte"),
        ["short"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToInt16"),
        ["ushort"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToUInt16"),
        ["int"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToInt32"),
        ["uint"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToUInt32"),
        ["long"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToInt64"),
        ["ulong"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToUInt64"),
        ["float"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToSingle"),
        ["double"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToDouble"),
        ["decimal"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToDecimal"),
        ["Guid"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToGuid"),
        ["TimeSpan"] = ($"{XmlConvert}.ToString({{0}})", $"{XmlConvert}.ToTimeSpan"),
        ["DateTimeOffset"] = ($"{XmlConvert}.ToString({{0}})", "{support}.ParseDateTimeOffset"),
        ["DateTime"] = ($"{XmlConvert}.ToString({{0}}, {Xml}.XmlDateTimeSerializationMode.RoundtripKind)", "{support}.ParseDateTime"),
        ["DateOnly"] = ($"{{0}}.ToString(\"yyyy-MM-dd\", {Invariant})", "{support}.ParseDateOnly"),
        ["TimeOnly"] = ($"{{0}}.ToString(\"HH:mm:ss.FFFFFFF\", {Invariant})", "{support}.ParseTimeOnly"),
        ["Uri"] = ("{0}.OriginalString", "{support}.ParseUri"),
        ["byte[]"] = ("global::System.Convert.ToBase64String({0})", "global::System.Convert.FromBase64String"),
    };

    private static readonly HashSet<string> ReferenceScalars = new(StringComparer.Ordinal) { "Uri", "byte[]" };

    private enum ValueKind
    {
        Text,
        Scalar,
        Enum,
        Model,
        Unsupported,
    }

    private enum PropertyShape
    {
        Single,
        List,
        Dictionary,
        Unsupported,
    }

    /// <summary>
    /// Generates the <c>{Model}Xml</c> class of a model record.
    /// </summary>
    /// <param name="source">The model.</param>
    /// <param name="headerContent">The records' header (auto-generated comment and usings).</param>
    /// <param name="ns">The records' namespace.</param>
    /// <param name="projectName">The project name (the support class lives in <c>{projectName}.Generated</c>).</param>
    /// <param name="xmlModels">The schemas that get an XML mapping (a property of another model type is not mapped).</param>
    /// <returns>The source of the class.</returns>
    public static string GenerateCompanion(
        XmlModelSource source,
        string? headerContent,
        string ns,
        string projectName,
        ISet<string> xmlModels)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (xmlModels is null)
        {
            throw new ArgumentNullException(nameof(xmlModels));
        }

        var support = $"global::{projectName}.Generated.{SupportTypeName}";
        var typeName = source.TypeName;
        var properties = PlanProperties(source, xmlModels);
        var (rootName, rootNamespace, rootPrefix) = NameOf(source.Schema.Xml, source.SchemaKey);

        var sb = new StringBuilder();
        sb.Append(string.IsNullOrEmpty(headerContent) ? "// <auto-generated />\n#nullable enable\n\n" : headerContent);
        if (!(headerContent ?? string.Empty).EndsWith("\n\n", StringComparison.Ordinal))
        {
            sb.AppendLine();
        }

        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// The XML representation of <see cref=\"{typeName}\"/> as the OpenAPI specification describes it (<c>xml</c>:");
        sb.AppendLine("/// element and attribute names, namespaces, array wrapping).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[global::System.CodeDom.Compiler.GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public static class {typeName}Xml");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>The name of the root element.</summary>");
        sb.AppendLine($"    public const string ElementName = {Literal(rootName)};");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>The namespace of the root element (empty for none).</summary>");
        sb.AppendLine($"    public const string ElementNamespace = {Literal(rootNamespace ?? string.Empty)};");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Writes the model as an XML document (UTF-8, no declaration).</summary>");
        sb.AppendLine($"    public static string ToXml({typeName} value)");
        sb.AppendLine($"        => {support}.ToXml(writer => Write(writer, value));");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Writes the model as the UTF-8 bytes of an XML document (no BOM, no declaration).</summary>");
        sb.AppendLine($"    public static byte[] ToUtf8Bytes({typeName} value)");
        sb.AppendLine($"        => {support}.ToUtf8Bytes(writer => Write(writer, value));");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Reads the model from an XML document (no DTD).</summary>");
        sb.AppendLine($"    public static {typeName} FromXml(string xml)");
        sb.AppendLine($"        => Read({support}.Parse(xml));");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Reads the model from a stream holding an XML document (no DTD).</summary>");
        sb.AppendLine($"    public static async global::System.Threading.Tasks.Task<{typeName}> ReadAsync(");
        sb.AppendLine("        global::System.IO.Stream stream,");
        sb.AppendLine("        global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine($"        => Read(await {support}.LoadAsync(stream, cancellationToken).ConfigureAwait(false));");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Writes the model as the root element.</summary>");
        sb.AppendLine($"    public static void Write({Xml}.XmlWriter writer, {typeName} value)");
        sb.AppendLine($"        => WriteElement(writer, value, ElementName, ElementNamespace, {Literal(rootPrefix)});");
        sb.AppendLine();
        AppendListMethods(sb, typeName, source.SchemaKey, support);
        AppendWriteElement(sb, typeName, properties, support);
        sb.AppendLine();
        AppendRead(sb, typeName, properties, support);

        foreach (var enumValue in properties
                     .Select(p => p.Value)
                     .Where(v => v.Kind == ValueKind.Enum)
                     .GroupBy(v => v.TypeName, StringComparer.Ordinal)
                     .Select(g => g.First()))
        {
            sb.AppendLine();
            AppendEnumHelpers(sb, enumValue);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// The XML names of a model's properties that differ from their JSON (specification) names: the element name, or
    /// the attribute name of an attribute. A validation filter maps an XML request's error keys through them.
    /// </summary>
    /// <param name="source">The model.</param>
    /// <param name="xmlModels">The schemas that get an XML mapping.</param>
    /// <returns>The record parameter name to its XML name and whether it is an attribute.</returns>
    public static Dictionary<string, (string Name, bool IsAttribute)> XmlNameOverrides(
        XmlModelSource source,
        ISet<string> xmlModels)
    {
        var overrides = new Dictionary<string, (string Name, bool IsAttribute)>(StringComparer.Ordinal);
        foreach (var property in PlanProperties(source, xmlModels))
        {
            var name = property.Shape == PropertyShape.List && !property.IsWrapped
                ? property.ItemName!
                : property.ElementName;
            if (!string.Equals(name, property.WireName, StringComparison.Ordinal))
            {
                overrides[property.Parameter.Name] = (name, property.IsAttribute);
            }
        }

        return overrides;
    }

    /// <summary>
    /// Generates the server's request binding of a model offered as XML: a <c>BindAsync</c> in a partial of the record
    /// that reads the body by its <c>Content-Type</c> - XML with the model's mapping, JSON (when accepted) with the
    /// host's JSON options. A malformed body is a 400 and another media type a 415 (<c>BadHttpRequestException</c>).
    /// </summary>
    /// <param name="typeName">The record's type name.</param>
    /// <param name="ns">The models' namespace.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="acceptsJson">Whether an operation accepts the model as JSON too.</param>
    /// <returns>The source of the partial record.</returns>
    public static string GenerateBinding(
        string typeName,
        string ns,
        string projectName,
        bool acceptsJson)
    {
        var support = $"global::{projectName}.Generated.{SupportTypeName}";
        var http = "global::Microsoft.AspNetCore.Http";
        var expected = acceptsJson ? "application/json or XML" : "XML";
        var json = acceptsJson
            ? $$"""

                if ({{http}}.HttpRequestJsonExtensions.HasJsonContentType(request))
                {
                    try
                    {
                        return await {{http}}.HttpRequestJsonExtensions.ReadFromJsonAsync<{{typeName}}>(request, context.RequestAborted).ConfigureAwait(false);
                    }
                    catch (global::System.Text.Json.JsonException ex)
                    {
                        throw new {{http}}.BadHttpRequestException($"The JSON request body is invalid: {ex.Message}", {{http}}.StatusCodes.Status400BadRequest, ex);
                    }
                }

        """
            : string.Empty;

        return $$"""
            // <auto-generated />
            #nullable enable

            namespace {{ns}};

            /// <summary>
            /// Binds <see cref="{{typeName}}"/> from the request body by its Content-Type ({{expected}}).
            /// </summary>
            public partial record {{typeName}}
            {
                /// <summary>
                /// Reads the request body: XML by the model's XML mapping{{(acceptsJson ? ", JSON by the host's JSON options" : string.Empty)}}.
                /// A malformed body is a 400, another media type a 415.
                /// </summary>
                public static async global::System.Threading.Tasks.ValueTask<{{typeName}}?> BindAsync(
                    {{http}}.HttpContext context,
                    global::System.Reflection.ParameterInfo parameter)
                {
                    var request = context.Request;
                    if (request.ContentLength == 0 || (request.ContentLength is null && string.IsNullOrEmpty(request.ContentType)))
                    {
                        return null;
                    }

                    var mediaType = global::Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(request.ContentType, out var parsed)
                        ? parsed.MediaType.Value
                        : null;
                    if ({{support}}.IsXmlMediaType(mediaType))
                    {
                        try
                        {
                            return {{typeName}}Xml.Read(await {{support}}.LoadAsync(request.Body, context.RequestAborted).ConfigureAwait(false));
                        }
                        catch (global::System.Exception ex) when (ex is global::System.Xml.XmlException or global::System.FormatException or global::System.NotSupportedException)
                        {
                            throw new {{http}}.BadHttpRequestException($"The XML request body is invalid: {ex.Message}", {{http}}.StatusCodes.Status400BadRequest, ex);
                        }
                    }
            {{json}}
                    throw new {{http}}.BadHttpRequestException($"The request body must be {{expected}}, not '{mediaType}'.", {{http}}.StatusCodes.Status415UnsupportedMediaType);
                }
            }
            """;
    }

    /// <summary>
    /// Generates the shared <c>XmlModelSupport</c> helpers the <c>{Model}Xml</c> classes use.
    /// </summary>
    /// <param name="projectName">The project name.</param>
    /// <returns>The source of the class.</returns>
    public static string GenerateSupport(string projectName)
        => $$"""
            // <auto-generated />
            #nullable enable

            namespace {{projectName}}.Generated;

            /// <summary>
            /// Writing and reading of the generated models' XML (the <c>{Model}Xml</c> classes): UTF-8 without BOM or
            /// declaration, and reading without DTD processing or an external resolver.
            /// </summary>
            [global::System.CodeDom.Compiler.GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            public static class {{SupportTypeName}}
            {
                private static readonly global::System.Xml.XmlWriterSettings WriterSettings = new()
                {
                    OmitXmlDeclaration = true,
                    Encoding = new global::System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    Indent = false,
                };

                /// <summary>Writes an XML document to a string.</summary>
                public static string ToXml(global::System.Action<global::System.Xml.XmlWriter> write)
                {
                    var builder = new global::System.Text.StringBuilder();
                    using (var writer = global::System.Xml.XmlWriter.Create(builder, WriterSettings))
                    {
                        write(writer);
                    }

                    return builder.ToString();
                }

                /// <summary>Writes an XML document as UTF-8 bytes.</summary>
                public static byte[] ToUtf8Bytes(global::System.Action<global::System.Xml.XmlWriter> write)
                {
                    using var stream = new global::System.IO.MemoryStream();
                    using (var writer = global::System.Xml.XmlWriter.Create(stream, WriterSettings))
                    {
                        write(writer);
                    }

                    return stream.ToArray();
                }

                /// <summary>Parses an XML document and returns its root element.</summary>
                public static global::System.Xml.Linq.XElement Parse(string xml)
                {
                    using var text = new global::System.IO.StringReader(xml);
                    using var reader = global::System.Xml.XmlReader.Create(text, ReaderSettings(async: false));
                    return global::System.Xml.Linq.XDocument.Load(reader).Root
                        ?? throw new global::System.Xml.XmlException("The XML document has no root element.");
                }

                /// <summary>
                /// Parses the body of a response kept by Atc.Rest.Client: the bytes (a non-text media type such as
                /// <c>application/xml</c>) or the text (<c>text/xml</c>), and returns its root element.
                /// </summary>
                public static global::System.Xml.Linq.XElement ParseContent(object? content)
                {
                    switch (content)
                    {
                        case byte[] bytes:
                        {
                            using var stream = new global::System.IO.MemoryStream(bytes);
                            using var reader = global::System.Xml.XmlReader.Create(stream, ReaderSettings(async: false));
                            return global::System.Xml.Linq.XDocument.Load(reader).Root
                                ?? throw new global::System.Xml.XmlException("The XML document has no root element.");
                        }

                        case string text:
                            return Parse(text);
                        default:
                            throw new global::System.Xml.XmlException("The response has no XML body.");
                    }
                }

                /// <summary>Reads an XML document from a stream (asynchronously) and returns its root element.</summary>
                public static async global::System.Threading.Tasks.Task<global::System.Xml.Linq.XElement> LoadAsync(
                    global::System.IO.Stream stream,
                    global::System.Threading.CancellationToken cancellationToken = default)
                {
                    using var reader = global::System.Xml.XmlReader.Create(stream, ReaderSettings(async: true));
                    var document = await global::System.Xml.Linq.XDocument.LoadAsync(reader, global::System.Xml.Linq.LoadOptions.None, cancellationToken).ConfigureAwait(false);
                    return document.Root
                        ?? throw new global::System.Xml.XmlException("The XML document has no root element.");
                }

                /// <summary>An XML request body: the UTF-8 bytes with their media type.</summary>
                public static global::System.Net.Http.HttpContent ToContent(byte[] body, string mediaType)
                {
                    var content = new global::System.Net.Http.ByteArrayContent(body);
                    content.Headers.ContentType = new global::System.Net.Http.Headers.MediaTypeHeaderValue(mediaType) { CharSet = "utf-8" };
                    return content;
                }

                /// <summary>Whether a response media type is XML (<c>application/xml</c>, <c>text/xml</c>, <c>*+xml</c>).</summary>
                public static bool IsXmlMediaType(string? mediaType)
                    => mediaType is not null &&
                       (mediaType.Equals("application/xml", global::System.StringComparison.OrdinalIgnoreCase) ||
                        mediaType.Equals("text/xml", global::System.StringComparison.OrdinalIgnoreCase) ||
                        mediaType.EndsWith("+xml", global::System.StringComparison.OrdinalIgnoreCase));

                /// <summary>The namespace an unqualified child element is written in: the parent's default namespace.</summary>
                public static string ChildNamespace(string ns, string? prefix)
                    => string.IsNullOrEmpty(prefix) ? ns : string.Empty;

                /// <summary>The first child element with this local name (any namespace).</summary>
                public static global::System.Xml.Linq.XElement? Child(global::System.Xml.Linq.XElement parent, string localName)
                {
                    foreach (var element in parent.Elements())
                    {
                        if (element.Name.LocalName == localName)
                        {
                            return element;
                        }
                    }

                    return null;
                }

                /// <summary>The child elements with this local name (any namespace).</summary>
                public static global::System.Collections.Generic.IEnumerable<global::System.Xml.Linq.XElement> Children(global::System.Xml.Linq.XElement parent, string localName)
                {
                    foreach (var element in parent.Elements())
                    {
                        if (element.Name.LocalName == localName)
                        {
                            yield return element;
                        }
                    }
                }

                /// <summary>The text of a child element, or <see langword="null"/> when there is none.</summary>
                public static string? Text(global::System.Xml.Linq.XElement parent, string localName)
                    => Child(parent, localName)?.Value;

                /// <summary>The value of an attribute (any namespace), or <see langword="null"/> when there is none.</summary>
                public static string? AttributeText(global::System.Xml.Linq.XElement element, string localName)
                {
                    foreach (var attribute in element.Attributes())
                    {
                        if (!attribute.IsNamespaceDeclaration && attribute.Name.LocalName == localName)
                        {
                            return attribute.Value;
                        }
                    }

                    return null;
                }

                /// <summary>A child element's value, parsed; <see langword="null"/> when there is none.</summary>
                public static T? ValueOf<T>(global::System.Xml.Linq.XElement parent, string localName, global::System.Func<string, T> parse)
                    where T : struct
                    => Text(parent, localName) is { } raw ? Convert(parent, localName, raw, parse) : null;

                /// <summary>A child element's value, parsed; <see langword="null"/> when there is none.</summary>
                public static T? RefOf<T>(global::System.Xml.Linq.XElement parent, string localName, global::System.Func<string, T> parse)
                    where T : class
                    => Text(parent, localName) is { } raw ? Convert(parent, localName, raw, parse) : null;

                /// <summary>An attribute's value, parsed; <see langword="null"/> when there is none.</summary>
                public static T? AttributeValueOf<T>(global::System.Xml.Linq.XElement element, string localName, global::System.Func<string, T> parse)
                    where T : struct
                    => AttributeText(element, localName) is { } raw ? Convert(element, localName, raw, parse) : null;

                /// <summary>An attribute's value, parsed; <see langword="null"/> when there is none.</summary>
                public static T? AttributeRefOf<T>(global::System.Xml.Linq.XElement element, string localName, global::System.Func<string, T> parse)
                    where T : class
                    => AttributeText(element, localName) is { } raw ? Convert(element, localName, raw, parse) : null;

                /// <summary>Parses a value; a malformed one is a <see cref="global::System.FormatException"/> naming the element.</summary>
                public static T Convert<T>(global::System.Xml.Linq.XElement owner, string localName, string raw, global::System.Func<string, T> parse)
                {
                    try
                    {
                        return parse(raw);
                    }
                    catch (global::System.Exception ex) when (ex is global::System.FormatException or global::System.OverflowException or global::System.ArgumentException)
                    {
                        throw new global::System.FormatException($"The XML element '{localName}' of '{owner.Name.LocalName}' has an invalid value: '{raw}'.", ex);
                    }
                }

                /// <summary>The exception for a required element or attribute that is missing.</summary>
                public static global::System.FormatException Missing(global::System.Xml.Linq.XElement owner, string localName)
                    => new($"The XML element '{localName}' of '{owner.Name.LocalName}' is required but missing.");

                /// <summary>The exception for an enum value the specification does not list.</summary>
                public static global::System.FormatException UnknownValue(string raw, string typeName)
                    => new($"'{raw}' is not a value of {typeName}.");

                /// <summary>Fails for a property the XML mapping cannot represent (e.g. a polymorphic type).</summary>
                public static T NotRepresentable<T>(string localName, string modelName)
                    => throw new global::System.NotSupportedException($"The property '{localName}' of '{modelName}' has no XML representation.");

                /// <summary>Fails when an element the XML mapping cannot represent is present.</summary>
                public static void EnsureAbsent(global::System.Xml.Linq.XElement parent, string localName, string modelName)
                {
                    if (Child(parent, localName) is not null)
                    {
                        NotRepresentable<object>(localName, modelName);
                    }
                }

                /// <summary>Writes an element holding text.</summary>
                public static void WriteValue(global::System.Xml.XmlWriter writer, string localName, string ns, string? prefix, string text)
                {
                    writer.WriteStartElement(prefix, localName, ns);
                    writer.WriteString(text);
                    writer.WriteFullEndElement();
                }

                /// <summary>Writes an attribute.</summary>
                public static void WriteAttribute(global::System.Xml.XmlWriter writer, string localName, string ns, string? prefix, string text)
                    => writer.WriteAttributeString(prefix, localName, ns, text);

                /// <summary>Parses an <c>xs:dateTime</c> or <c>xs:date</c> (a date without offset is UTC).</summary>
                public static global::System.DateTimeOffset ParseDateTimeOffset(string raw)
                    => global::System.DateTimeOffset.Parse(raw.Trim(), global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.AssumeUniversal);

                /// <summary>Parses an <c>xs:dateTime</c>.</summary>
                public static global::System.DateTime ParseDateTime(string raw)
                    => global::System.Xml.XmlConvert.ToDateTime(raw.Trim(), global::System.Xml.XmlDateTimeSerializationMode.RoundtripKind);

                /// <summary>Parses an <c>xs:date</c>.</summary>
                public static global::System.DateOnly ParseDateOnly(string raw)
                    => global::System.DateOnly.Parse(raw.Trim(), global::System.Globalization.CultureInfo.InvariantCulture);

                /// <summary>Parses an <c>xs:time</c>.</summary>
                public static global::System.TimeOnly ParseTimeOnly(string raw)
                    => global::System.TimeOnly.Parse(raw.Trim(), global::System.Globalization.CultureInfo.InvariantCulture);

                /// <summary>Parses an <c>xs:anyURI</c>.</summary>
                public static global::System.Uri ParseUri(string raw)
                    => new(raw.Trim(), global::System.UriKind.RelativeOrAbsolute);

                private static global::System.Xml.XmlReaderSettings ReaderSettings(bool async)
                    => new()
                    {
                        Async = async,
                        DtdProcessing = global::System.Xml.DtdProcessing.Prohibit,
                        XmlResolver = null,
                        IgnoreComments = true,
                        IgnoreProcessingInstructions = true,
                    };
            }
            """;

    private static void AppendListMethods(
        StringBuilder sb,
        string typeName,
        string schemaKey,
        string support)
    {
        var items = $"global::System.Collections.Generic.IEnumerable<{typeName}>";
        var list = $"global::System.Collections.Generic.List<{typeName}>";
        sb.AppendLine("    /// <summary>The root element of a list of the model, unless the specification names it (the array's <c>xml.name</c>).</summary>");
        sb.AppendLine($"    public const string ListElementName = {Literal("ArrayOf" + schemaKey)};");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Writes a list of the model as an XML document (UTF-8, no declaration).</summary>");
        sb.AppendLine($"    public static string ListToXml({items} items, string? rootName = null)");
        sb.AppendLine($"        => {support}.ToXml(writer => WriteList(writer, items, rootName));");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Writes a list of the model as the UTF-8 bytes of an XML document (no BOM, no declaration).</summary>");
        sb.AppendLine($"    public static byte[] ListToUtf8Bytes({items} items, string? rootName = null)");
        sb.AppendLine($"        => {support}.ToUtf8Bytes(writer => WriteList(writer, items, rootName));");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Reads a list of the model from an XML document (no DTD).</summary>");
        sb.AppendLine($"    public static {list} ListFromXml(string xml)");
        sb.AppendLine($"        => ReadList({support}.Parse(xml));");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Reads a list of the model from a stream holding an XML document (no DTD).</summary>");
        sb.AppendLine($"    public static async global::System.Threading.Tasks.Task<{list}> ReadListAsync(");
        sb.AppendLine("        global::System.IO.Stream stream,");
        sb.AppendLine("        global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine($"        => ReadList(await {support}.LoadAsync(stream, cancellationToken).ConfigureAwait(false));");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Writes a list of the model: a root element holding one element per item.</summary>");
        sb.AppendLine($"    public static void WriteList({Xml}.XmlWriter writer, {items} items, string? rootName = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        writer.WriteStartElement(rootName ?? ListElementName);");
        sb.AppendLine("        foreach (var item in items)");
        sb.AppendLine("        {");
        sb.AppendLine("            Write(writer, item);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteEndElement();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Reads a list of the model: the items named like the model's element, else every child element.</summary>");
        sb.AppendLine($"    public static {list} ReadList({Xml}.Linq.XElement element)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var items = global::System.Linq.Enumerable.ToList({support}.Children(element, ElementName));");
        sb.AppendLine("        if (items.Count == 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            items = global::System.Linq.Enumerable.ToList(element.Elements());");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.Select(items, Read));");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static List<PropertyPlan> PlanProperties(
        XmlModelSource source,
        ISet<string> xmlModels)
    {
        var parameters = source.Record.Parameters ?? [];
        var plans = new List<PropertyPlan>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var schemaName = source.TypeName;

        foreach (var property in source.EffectiveSchema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            // The record's parameter name (SchemaExtractor's rule: PascalCase, first wins, "Value" when it is the type's name)
            var name = property.Key.ToPascalCaseForDotNet();
            if (!seen.Add(name))
            {
                continue;
            }

            if (name.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
            {
                name += "Value";
            }

            var parameter = parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
            if (parameter is null)
            {
                continue;
            }

            var xml = property.Value.Xml;
            var resolved = Resolve(property.Value);
            var isArray = resolved?.Type is { } type && (type & JsonSchemaType.Array) == JsonSchemaType.Array;
            var elementName = XmlName(xml) ?? property.Key;
            var (ns, prefix) = NamespaceOf(xml);

            PropertyPlan plan;
            if (isArray && TryGenericArgument(parameter.TypeName, "List<", out var itemTypeName))
            {
                var items = resolved!.Items;

                // OpenAPI 3.0/3.1 `wrapped: true` is the 3.2 node type `element` of an array
                var wrapped = xml?.NodeType == OpenApiXmlNodeType.Element;
                var itemName = XmlName(items?.Xml) ?? (wrapped ? elementName : property.Key);
                var (itemNs, itemPrefix) = NamespaceOf(items?.Xml);
                var item = ClassifyValue(itemTypeName, items, xmlModels);
                plan = new PropertyPlan(parameter, property.Key, elementName, ns, prefix, IsAttribute: false, PropertyShape.List, item)
                {
                    IsWrapped = wrapped,
                    ItemName = itemName,
                    ItemNamespace = itemNs,
                    ItemPrefix = itemPrefix,
                };
            }
            else if (TryGenericArgument(parameter.TypeName, "Dictionary<string, ", out var valueTypeName) && resolved?.AdditionalProperties is { } valueSchema)
            {
                plan = new PropertyPlan(parameter, property.Key, elementName, ns, prefix, IsAttribute: false, PropertyShape.Dictionary, ClassifyValue(valueTypeName, valueSchema, xmlModels));
            }
            else
            {
                var value = ClassifyValue(parameter.TypeName, property.Value, xmlModels);
                var isAttribute = IsAttributeNode(xml) && value.Kind is ValueKind.Text or ValueKind.Scalar or ValueKind.Enum;
                plan = new PropertyPlan(parameter, property.Key, elementName, ns, prefix, isAttribute, value.Kind == ValueKind.Unsupported ? PropertyShape.Unsupported : PropertyShape.Single, value);
            }

            if (plan.Value.Kind == ValueKind.Unsupported)
            {
                plan = plan with { Shape = PropertyShape.Unsupported };
            }

            plans.Add(plan);
        }

        return plans;
    }

    private static XmlValue ClassifyValue(
        string typeName,
        IOpenApiSchema? schema,
        ISet<string> xmlModels)
    {
        var clean = typeName.TrimEnd('?');
        var resolved = Resolve(schema);

        if (resolved?.Enum is { Count: > 0 } && EnumExtractor.ExtractEnumValues(resolved.Enum) is { Count: > 0 } values)
        {
            return new XmlValue(ValueKind.Enum, clean, values);
        }

        if (clean == "string")
        {
            return new XmlValue(ValueKind.Text, clean, null);
        }

        if (Scalars.ContainsKey(clean))
        {
            return new XmlValue(ValueKind.Scalar, clean, null);
        }

        if (schema is OpenApiSchemaReference reference &&
            (reference.Reference?.Id ?? reference.Id) is { } key &&
            xmlModels.Contains(key) &&
            reference.Target is OpenApiSchema target &&
            !target.HasPolymorphicComposition() &&
            !clean.Contains('<'))
        {
            return new XmlValue(ValueKind.Model, clean, null);
        }

        return new XmlValue(ValueKind.Unsupported, clean, null);
    }

    private static void AppendWriteElement(
        StringBuilder sb,
        string typeName,
        List<PropertyPlan> properties,
        string support)
    {
        sb.AppendLine("    /// <summary>Writes the model as an element of this name.</summary>");
        sb.AppendLine($"    public static void WriteElement({Xml}.XmlWriter writer, {typeName} value, string localName, string ns, string? prefix)");
        sb.AppendLine("    {");
        sb.AppendLine("        writer.WriteStartElement(prefix, localName, ns);");
        sb.AppendLine($"        var childNamespace = {support}.ChildNamespace(ns, prefix);");

        var index = 0;
        foreach (var property in properties.Where(p => p.IsAttribute).Concat(properties.Where(p => !p.IsAttribute)))
        {
            index++;
            sb.AppendLine();
            AppendWriteProperty(sb, property, index, support, typeName);
        }

        sb.AppendLine();
        sb.AppendLine("        writer.WriteEndElement();");
        sb.AppendLine("    }");
    }

    private static void AppendWriteProperty(
        StringBuilder sb,
        PropertyPlan property,
        int index,
        string support,
        string typeName)
    {
        var access = $"value.{property.Parameter.Name}";
        var local = $"v{index}";
        var nullable = property.Parameter.IsNullableType;
        var elementNs = property.Namespace is null ? "childNamespace" : Literal(property.Namespace);
        var elementPrefix = Literal(property.Prefix);

        if (property.Shape == PropertyShape.Unsupported)
        {
            sb.AppendLine(nullable
                ? $"        if ({access} is not null) {{ {support}.NotRepresentable<object>({Literal(property.ElementName)}, {Literal(typeName)}); }}"
                : $"        {support}.NotRepresentable<object>({Literal(property.ElementName)}, {Literal(typeName)});");
            return;
        }

        var indent = "        ";
        if (nullable)
        {
            sb.AppendLine($"        if ({access} is {{ }} {local})");
            sb.AppendLine("        {");
            indent = "            ";
        }
        else
        {
            sb.AppendLine($"        var {local} = {access};");
        }

        switch (property.Shape)
        {
            case PropertyShape.Single when property.IsAttribute:
                sb.AppendLine($"{indent}{support}.WriteAttribute(writer, {Literal(property.ElementName)}, {Literal(property.Namespace ?? string.Empty)}, {elementPrefix}, {Format(property.Value, local)});");
                break;
            case PropertyShape.Single:
                AppendWriteValue(sb, indent, property.Value, local, Literal(property.ElementName), elementNs, elementPrefix, support);
                break;
            case PropertyShape.List:
                var itemNs = property.ItemNamespace is null ? (property.IsWrapped ? ChildNamespaceOf(elementNs, elementPrefix, support) : elementNs) : Literal(property.ItemNamespace);
                if (property.IsWrapped)
                {
                    sb.AppendLine($"{indent}writer.WriteStartElement({elementPrefix}, {Literal(property.ElementName)}, {elementNs});");
                }

                sb.AppendLine($"{indent}foreach (var item in {local})");
                sb.AppendLine($"{indent}{{");
                AppendWriteValue(sb, indent + "    ", property.Value, "item", Literal(property.ItemName!), itemNs, Literal(property.ItemPrefix), support);
                sb.AppendLine($"{indent}}}");
                if (property.IsWrapped)
                {
                    sb.AppendLine($"{indent}writer.WriteEndElement();");
                }

                break;
            case PropertyShape.Dictionary:
                sb.AppendLine($"{indent}writer.WriteStartElement({elementPrefix}, {Literal(property.ElementName)}, {elementNs});");
                sb.AppendLine($"{indent}foreach (var entry in {local})");
                sb.AppendLine($"{indent}{{");
                AppendWriteValue(sb, indent + "    ", property.Value, "entry.Value", $"{XmlConvert}.EncodeLocalName(entry.Key)!", ChildNamespaceOf(elementNs, elementPrefix, support), "null", support);
                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}writer.WriteEndElement();");
                break;
        }

        if (nullable)
        {
            sb.AppendLine("        }");
        }
    }

    private static void AppendWriteValue(
        StringBuilder sb,
        string indent,
        XmlValue value,
        string expression,
        string nameExpression,
        string nsExpression,
        string prefixExpression,
        string support)
    {
        if (value.Kind == ValueKind.Model)
        {
            sb.AppendLine($"{indent}{CompanionOf(value.TypeName)}.WriteElement(writer, {expression}, {nameExpression}, {nsExpression}, {prefixExpression});");
            return;
        }

        if (value.Kind == ValueKind.Text)
        {
            sb.AppendLine($"{indent}if ({expression} is not null) {{ {support}.WriteValue(writer, {nameExpression}, {nsExpression}, {prefixExpression}, {expression}); }}");
            return;
        }

        sb.AppendLine($"{indent}{support}.WriteValue(writer, {nameExpression}, {nsExpression}, {prefixExpression}, {Format(value, expression)});");
    }

    private static void AppendRead(
        StringBuilder sb,
        string typeName,
        List<PropertyPlan> properties,
        string support)
    {
        sb.AppendLine("    /// <summary>Reads the model from its element (element and attribute names match by local name).</summary>");
        sb.AppendLine($"    public static {typeName} Read({Xml}.Linq.XElement element)");
        sb.AppendLine("    {");

        var arguments = new List<string>();
        var index = 0;
        foreach (var property in properties)
        {
            index++;
            var local = $"p{index}";
            var name = Literal(property.ElementName);

            switch (property.Shape)
            {
                case PropertyShape.Unsupported:
                    sb.AppendLine($"        {support}.EnsureAbsent(element, {name}, {Literal(typeName)});");
                    arguments.Add(property.Parameter.IsNullableType
                        ? $"{property.Parameter.Name}: null"
                        : $"{property.Parameter.Name}: {support}.NotRepresentable<{property.Parameter.TypeName}>({name}, {Literal(typeName)})");
                    continue;
                case PropertyShape.Single when property.IsAttribute:
                    sb.AppendLine($"        var {local} = {ReadAttribute(property.Value, name, support)};");
                    break;
                case PropertyShape.Single:
                    sb.AppendLine($"        var {local} = {ReadChild(property.Value, "element", name, support, index)};");
                    break;
                case PropertyShape.List:
                    var itemName = Literal(property.ItemName!);
                    if (property.IsWrapped)
                    {
                        sb.AppendLine($"        var {local} = {support}.Child(element, {name}) is {{ }} w{index}");
                        sb.AppendLine($"            ? {ItemsOf($"w{index}", itemName, property.Value, support)}");
                        sb.AppendLine("            : null;");
                    }
                    else
                    {
                        var isRequiredList = !property.Parameter.IsNullableType;
                        sb.AppendLine($"        var items{index} = {ItemsOf("element", itemName, property.Value, support)};");
                        sb.AppendLine(isRequiredList
                            ? $"        var {local} = items{index};"
                            : $"        var {local} = items{index}.Count > 0 ? items{index} : null;");
                    }

                    break;
                case PropertyShape.Dictionary:
                    sb.AppendLine($"        var {local} = {support}.Child(element, {name}) is {{ }} d{index}");
                    sb.AppendLine($"            ? global::System.Linq.Enumerable.ToDictionary(d{index}.Elements(), e => {XmlConvert}.DecodeName(e.Name.LocalName)!, e => {ValueFromElement(property.Value, $"d{index}", "e", $"{XmlConvert}.DecodeName(e.Name.LocalName)!", support)}, global::System.StringComparer.Ordinal)");
                    sb.AppendLine("            : null;");
                    break;
            }

            arguments.Add($"{property.Parameter.Name}: {ArgumentOf(property, local, name, support)}");
        }

        if (arguments.Count == 0)
        {
            sb.AppendLine($"        return new {typeName}();");
        }
        else
        {
            sb.AppendLine($"        return new {typeName}(");
            for (var i = 0; i < arguments.Count; i++)
            {
                sb.AppendLine($"            {arguments[i]}{(i < arguments.Count - 1 ? "," : ");")}");
            }
        }

        sb.AppendLine("    }");
    }

    private static string ArgumentOf(
        PropertyPlan property,
        string local,
        string name,
        string support)
    {
        var parameter = property.Parameter;
        if (parameter.DefaultValue is { } defaultValue && defaultValue != "null")
        {
            return $"{local} ?? {defaultValue}";
        }

        if (parameter.IsNullableType)
        {
            return local;
        }

        // Required and not nullable: absent is an error, as with JSON's required properties
        return $"{local} ?? throw {support}.Missing(element, {name})";
    }

    private static string ItemsOf(
        string container,
        string itemName,
        XmlValue value,
        string support)
        => $"global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.Select({support}.Children({container}, {itemName}), i => {ValueFromElement(value, container, "i", itemName, support)}))";

    private static string ValueFromElement(
        XmlValue value,
        string owner,
        string element,
        string nameExpression,
        string support)
        => value.Kind switch
        {
            ValueKind.Model => $"{CompanionOf(value.TypeName)}.Read({element})",
            ValueKind.Text => $"{element}.Value",
            _ => $"{support}.Convert({owner}, {nameExpression}, {element}.Value, {Parser(value, support)})",
        };

    private static string ReadChild(
        XmlValue value,
        string element,
        string name,
        string support,
        int index)
        => value.Kind switch
        {
            ValueKind.Model => $"{support}.Child({element}, {name}) is {{ }} c{index} ? {CompanionOf(value.TypeName)}.Read(c{index}) : null",
            ValueKind.Text => $"{support}.Text({element}, {name})",
            _ => IsReferenceValue(value)
                ? $"{support}.RefOf({element}, {name}, {Parser(value, support)})"
                : $"{support}.ValueOf({element}, {name}, {Parser(value, support)})",
        };

    private static string ReadAttribute(
        XmlValue value,
        string name,
        string support)
        => value.Kind switch
        {
            ValueKind.Text => $"{support}.AttributeText(element, {name})",
            _ => IsReferenceValue(value)
                ? $"{support}.AttributeRefOf(element, {name}, {Parser(value, support)})"
                : $"{support}.AttributeValueOf(element, {name}, {Parser(value, support)})",
        };

    private static bool IsReferenceValue(XmlValue value)
        => value.Kind == ValueKind.Scalar && ReferenceScalars.Contains(value.TypeName);

    private static string Parser(
        XmlValue value,
        string support)
        => value.Kind == ValueKind.Enum
            ? $"Parse{HelperSuffix(value.TypeName)}"
            : $"static s => {Scalars[value.TypeName].Parse.Replace("{support}", support)}(s)";

    private static string Format(
        XmlValue value,
        string expression)
        => value.Kind switch
        {
            ValueKind.Enum => $"Format{HelperSuffix(value.TypeName)}({expression})",
            ValueKind.Text => expression,
            _ => string.Format(CultureInfo.InvariantCulture, Scalars[value.TypeName].Format, expression),
        };

    private static void AppendEnumHelpers(
        StringBuilder sb,
        XmlValue value)
    {
        var suffix = HelperSuffix(value.TypeName);
        var members = value.EnumValues!
            .Select(v => (Member: $"{value.TypeName}.{v.Name}", Wire: v.EnumMemberValue ?? (v.Value is { } number ? number.ToString(CultureInfo.InvariantCulture) : v.Name)))
            .ToList();

        sb.AppendLine($"    private static string Format{suffix}({value.TypeName} value)");
        sb.AppendLine("        => value switch");
        sb.AppendLine("        {");
        foreach (var (member, wire) in members)
        {
            sb.AppendLine($"            {member} => {Literal(wire)},");
        }

        sb.AppendLine($"            _ => global::System.Convert.ToString(value, {Invariant})!,");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine($"    private static {value.TypeName} Parse{suffix}(string raw)");
        sb.AppendLine("    {");
        sb.AppendLine("        var text = raw.Trim();");
        foreach (var (member, wire) in members)
        {
            sb.AppendLine($"        if (string.Equals(text, {Literal(wire)}, global::System.StringComparison.OrdinalIgnoreCase))");
            sb.AppendLine("        {");
            sb.AppendLine($"            return {member};");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine($"        throw new global::System.FormatException($\"'{{raw}}' is not a value of {value.TypeName}.\");");
        sb.AppendLine("    }");
    }

    private static string HelperSuffix(string typeName)
        => new(typeName.Substring(typeName.LastIndexOf('.') + 1).Where(char.IsLetterOrDigit).ToArray());

    private static string ChildNamespaceOf(
        string nsExpression,
        string prefixExpression,
        string support)
        => prefixExpression == "null" ? nsExpression : $"{support}.ChildNamespace({nsExpression}, {prefixExpression})";

    private static string CompanionOf(string typeName)
        => typeName + "Xml";

    private static bool TryGenericArgument(
        string typeName,
        string prefix,
        out string argument)
    {
        var clean = typeName.TrimEnd('?');
        if (clean.StartsWith(prefix, StringComparison.Ordinal) && clean.EndsWith(">", StringComparison.Ordinal))
        {
            argument = clean.Substring(prefix.Length, clean.Length - prefix.Length - 1).Trim();
            return !argument.Contains('<');
        }

        argument = string.Empty;
        return false;
    }

    private static IOpenApiSchema? Resolve(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference reference ? reference.Target : schema;

    private static string? XmlName(OpenApiXml? xml)
        => xml?.Name is { Length: > 0 } name && name != "##default" ? name : null;

    private static bool IsAttributeNode(OpenApiXml? xml)
        => xml?.NodeType == OpenApiXmlNodeType.Attribute;

    private static (string? Namespace, string? Prefix) NamespaceOf(
        OpenApiXml? xml)
    {
        var ns = xml?.Namespace?.OriginalString;
        return string.IsNullOrEmpty(ns)
            ? (null, null)
            : (ns, string.IsNullOrEmpty(xml!.Prefix) ? null : xml.Prefix);
    }

    private static (string Name, string? Namespace, string? Prefix) NameOf(
        OpenApiXml? xml,
        string schemaKey)
    {
        var (ns, prefix) = NamespaceOf(xml);
        return (XmlName(xml) ?? schemaKey, ns, prefix);
    }

    private static string Literal(string? value)
        => value is null
            ? "null"
            : "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private sealed record XmlValue(
        ValueKind Kind,
        string TypeName,
        List<EnumValueParameters>? EnumValues);

    private sealed record PropertyPlan(
        ParameterBaseParameters Parameter,
        string WireName,
        string ElementName,
        string? Namespace,
        string? Prefix,
        bool IsAttribute,
        PropertyShape Shape,
        XmlValue Value)
    {
        public bool IsWrapped { get; init; }

        public string? ItemName { get; init; }

        public string? ItemNamespace { get; init; }

        public string? ItemPrefix { get; init; }
    }
}