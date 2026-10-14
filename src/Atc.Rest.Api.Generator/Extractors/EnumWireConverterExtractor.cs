namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// The <c>EnumWireConverterFactory</c> of a generated project: put first in a <see cref="System.Text.Json.JsonSerializerOptions"/>,
/// it makes every generated enum go on the wire as the specification writes it, whatever enum converter the host (or
/// Atc.Rest.Client) registers. System.Text.Json takes a converter in the options before the one a type names, so a
/// host-wide <c>JsonStringEnumConverter</c> would otherwise write an integer enum's member name and read a string
/// enum from any integer. An integer enum is read and written by the <c>JsonNumberEnumConverter&lt;T&gt;</c> its
/// <c>[JsonConverter]</c> names; a string enum by its member names (<c>[JsonStringEnumMemberName]</c>), never from a
/// number.
/// </summary>
public static class EnumWireConverterExtractor
{
    /// <summary>
    /// The class name of the generated factory.
    /// </summary>
    public const string ClassName = "EnumWireConverterFactory";

    /// <summary>
    /// The class name of the converter that also reads a string enum by its member name (<c>readEnumMemberNames</c>).
    /// </summary>
    public const string MemberNameClassName = "EnumMemberNameReadingConverter";

    /// <summary>
    /// The source of the factory, in the given namespace, for the enums the project generates: each with its generic
    /// converter, so the factory works in a trimmed or Native AOT build.
    /// </summary>
    /// <param name="namespaceName">The namespace of the generated file.</param>
    /// <param name="enums">The generated enums.</param>
    /// <param name="readMemberNames">Whether a string enum is also read by its C# member name (the marker's
    /// <c>readEnumMemberNames</c>): the factory then wraps its string converter in <c>EnumMemberNameReadingConverter</c>.</param>
    /// <returns>The file content.</returns>
    public static string Generate(
        string namespaceName,
        IReadOnlyList<GeneratedEnumDeclaration> enums,
        bool readMemberNames = false)
    {
        if (enums is null)
        {
            throw new ArgumentNullException(nameof(enums));
        }

        var distinct = enums
            .GroupBy(e => e.FullName, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        var canConvert = distinct.Count == 0
            ? "false"
            : string.Join($"{Environment.NewLine}           || ", distinct.Select(e => $"typeToConvert == typeof({e.FullName})"));

        var createConverter = new StringBuilder();
        foreach (var e in distinct)
        {
            var converter = e.IsInteger
                ? $"new JsonNumberEnumConverter<{e.FullName}>()"
                : $"new JsonStringEnumConverter<{e.FullName}>(namingPolicy: null, allowIntegerValues: false)";
            var created = !e.IsInteger && readMemberNames
                ? $"new {MemberNameClassName}<{e.FullName}>((JsonConverter<{e.FullName}>){converter}.CreateConverter(typeToConvert, options)!, options)"
                : $"{converter}.CreateConverter(typeToConvert, options)";
            createConverter
                .AppendLine($"        if (typeToConvert == typeof({e.FullName}))")
                .AppendLine("        {")
                .AppendLine($"            return {created};")
                .AppendLine("        }")
                .AppendLine();
        }

        return $$"""
                 {{HeaderBuilder.WithUsings(
                     NamespaceConstants.System,
                     NamespaceConstants.SystemCodeDomCompiler,
                     NamespaceConstants.SystemTextJson,
                     NamespaceConstants.SystemTextJsonSerialization).TrimEnd()}}

                 namespace {{namespaceName}};

                 /// <summary>
                 /// Reads and writes every generated enum as the specification writes it, whatever enum converter is
                 /// registered after it: an integer enum as its number, a string enum as its values and never from a number.
                 /// </summary>
                 [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
                 public sealed class {{ClassName}} : JsonConverterFactory
                 {
                     /// <inheritdoc />
                     public override bool CanConvert(Type typeToConvert)
                         => {{canConvert}};

                     /// <inheritdoc />
                     public override JsonConverter? CreateConverter(
                         Type typeToConvert,
                         JsonSerializerOptions options)
                     {
                 {{createConverter.ToString().TrimEnd()}}

                         return null;
                     }
                 }{{(readMemberNames ? Environment.NewLine + Environment.NewLine + MemberNameConverter() : string.Empty)}}
                 """;
    }

    /// <summary>
    /// Whether the document has an enum the generator emits as a C# enum: one in a schema reachable from the
    /// components, the paths or the webhooks.
    /// </summary>
    /// <param name="openApiDoc">The specification.</param>
    /// <returns>True when there is at least one enum.</returns>
    public static bool HasEnums(OpenApiDocument openApiDoc)
        => SchemaWalker.Any(
            openApiDoc,
            schema => schema.Enum is { Count: > 0 } values &&
                      values.Any(v => v is not null && !JsonNullSentinel.IsJsonNullSentinel(v)));

    // Reads the specification's value first, then the member name, then the member name in another casing when only one
    // member has it; writes the value. Its tables come from the enum's own values and from writing each value once, so
    // nothing is found through reflection and a number, a number as text or a list of names is never read.
    private static string MemberNameConverter()
        => $$"""
             /// <summary>
             /// Reads a string enum by the value in the specification and, failing that, by its member name - in another
             /// casing when only one member has it (<c>readEnumMemberNames</c>: a client or server that writes member
             /// names keeps working). Writes the value.
             /// </summary>
             /// <typeparam name="T">The enum.</typeparam>
             [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
             public sealed class {{MemberNameClassName}}<T> : JsonConverter<T>
                 where T : struct, Enum
             {
                 private readonly JsonConverter<T> wire;
                 private readonly System.Collections.Generic.Dictionary<string, T> byValue = new(StringComparer.Ordinal);
                 private readonly System.Collections.Generic.Dictionary<string, T> byName = new(StringComparer.Ordinal);
                 private readonly System.Collections.Generic.Dictionary<string, T?> byNameIgnoringCase = new(StringComparer.OrdinalIgnoreCase);

                 /// <summary>
                 /// Initializes a new instance of the <see cref="{{MemberNameClassName}}{T}"/> class.
                 /// </summary>
                 /// <param name="wire">The converter of the values in the specification.</param>
                 /// <param name="options">The options the values are written with.</param>
                 public {{MemberNameClassName}}(
                     JsonConverter<T> wire,
                     JsonSerializerOptions options)
                 {
                     this.wire = wire ?? throw new ArgumentNullException(nameof(wire));

                     var names = Enum.GetNames<T>();
                     var values = Enum.GetValues<T>();
                     for (var i = 0; i < values.Length; i++)
                     {
                         byValue[ValueOf(values[i], options)] = values[i];
                         byName[names[i]] = values[i];

                         // A name two members share in another casing (FOO, Foo) is no member's
                         byNameIgnoringCase[names[i]] = byNameIgnoringCase.ContainsKey(names[i]) ? null : values[i];
                     }
                 }

                 /// <inheritdoc />
                 public override T Read(
                     ref Utf8JsonReader reader,
                     Type typeToConvert,
                     JsonSerializerOptions options)
                     => reader.TokenType == JsonTokenType.String
                         ? Find(reader.GetString()!)
                         : wire.Read(ref reader, typeToConvert, options);

                 /// <inheritdoc />
                 public override void Write(
                     Utf8JsonWriter writer,
                     T value,
                     JsonSerializerOptions options)
                     => wire.Write(writer, value, options);

                 /// <inheritdoc />
                 public override T ReadAsPropertyName(
                     ref Utf8JsonReader reader,
                     Type typeToConvert,
                     JsonSerializerOptions options)
                     => Find(reader.GetString()!);

                 /// <inheritdoc />
                 public override void WriteAsPropertyName(
                     Utf8JsonWriter writer,
                     T value,
                     JsonSerializerOptions options)
                     => wire.WriteAsPropertyName(writer, value, options);

                 private T Find(string text)
                     => byValue.TryGetValue(text, out var value) ||
                        byName.TryGetValue(text, out value)
                         ? value
                         : byNameIgnoringCase.TryGetValue(text, out var named) && named is { } member
                             ? member
                             : throw new JsonException($"'{text}' is no value of {typeof(T).Name}.");

                 private string ValueOf(
                     T value,
                     JsonSerializerOptions options)
                 {
                     using var buffer = new System.IO.MemoryStream();
                     using (var writer = new Utf8JsonWriter(buffer))
                     {
                         wire.Write(writer, value, options);
                     }

                     var reader = new Utf8JsonReader(buffer.ToArray());
                     reader.Read();
                     return reader.GetString()!;
                 }
             }
             """;
}