namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// The <c>EnumWireConverterFactory</c> of a generated project: put first in a <see cref="System.Text.Json.JsonSerializerOptions"/>,
/// it makes every generated enum go on the wire as the specification writes it, whatever enum converter the host (or
/// Atc.Rest.Client) registers. System.Text.Json takes a converter in the options before the one a type names, so a
/// host-wide <c>JsonStringEnumConverter</c> would otherwise write an integer enum's member name and read a string
/// enum from any integer. An integer enum is read and written by the <c>JsonNumberEnumConverter&lt;T&gt;</c> its
/// <c>[JsonConverter]</c> names; a string enum by its values (<c>[JsonStringEnumMemberName]</c>), never from a
/// number - read in any casing only one value has (<see cref="ReadingClassName"/>).
/// </summary>
public static class EnumWireConverterExtractor
{
    /// <summary>
    /// The class name of the generated factory.
    /// </summary>
    public const string ClassName = "EnumWireConverterFactory";

    /// <summary>
    /// The class name of the converter a string enum is read with: by its value, in any casing only one value has,
    /// and with <c>readEnumMemberNames</c> by its member name too.
    /// </summary>
    public const string ReadingClassName = "EnumValueReadingConverter";

    /// <summary>
    /// The source of the factory, in the given namespace, for the enums the project generates: each with its generic
    /// converter, so the factory works in a trimmed or Native AOT build.
    /// </summary>
    /// <param name="namespaceName">The namespace of the generated file.</param>
    /// <param name="enums">The generated enums.</param>
    /// <param name="readMemberNames">Whether a string enum is also read by its C# member name (the marker's
    /// <c>readEnumMemberNames</c>).</param>
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
            var empty = e.EmptyMember is null ? string.Empty : $", empty: {e.FullName}.{e.EmptyMember}";
            var created = e.IsInteger
                ? $"{converter}.CreateConverter(typeToConvert, options)"
                : $"new {ReadingClassName}<{e.FullName}>((JsonConverter<{e.FullName}>){converter}.CreateConverter(typeToConvert, options)!, options, readMemberNames: {(readMemberNames ? "true" : "false")}{empty})";
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
                 /// registered after it: an integer enum as its number, a string enum as its values - read in any casing
                 /// only one value has - and never from a number.
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
                 }{{(distinct.Exists(e => !e.IsInteger) ? Environment.NewLine + Environment.NewLine + ReadingConverter() : string.Empty)}}
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

    // Reads the specification's value as written first, then (readEnumMemberNames) the member name; then either in
    // another casing when only one member has it; writes the value. Its tables come from the enum's own values and from
    // writing each value once, so nothing is found through reflection and a number, a number as text or a list of names
    // is never read.
    private static string ReadingConverter()
        => $$"""
             /// <summary>
             /// Reads a string enum by the value in the specification - as written, or in another casing only one value
             /// has (<c>OUTOFSTOCK</c> for <c>outOfStock</c>; <c>ACTIVE</c> names neither of <c>active</c> and
             /// <c>Active</c>) - and with <c>readEnumMemberNames</c> by its member name too, so a client or server that
             /// writes member names keeps working. Writes the value; the empty string too, which no
             /// <c>[JsonStringEnumMemberName]</c> can name.
             /// </summary>
             /// <typeparam name="T">The enum.</typeparam>
             [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
             public sealed class {{ReadingClassName}}<T> : JsonConverter<T>
                 where T : struct, Enum
             {
                 private readonly JsonConverter<T> wire;
                 private readonly T? empty;
                 private readonly System.Collections.Generic.Dictionary<string, T> exact = new(StringComparer.Ordinal);
                 private readonly System.Collections.Generic.Dictionary<string, T?> ignoringCase = new(StringComparer.OrdinalIgnoreCase);

                 /// <summary>
                 /// Initializes a new instance of the <see cref="{{ReadingClassName}}{T}"/> class.
                 /// </summary>
                 /// <param name="wire">The converter of the values in the specification.</param>
                 /// <param name="options">The options the values are written with.</param>
                 /// <param name="readMemberNames">Whether a value is read by its member name too.</param>
                 /// <param name="empty">The member whose value is the empty string, if there is one.</param>
                 public {{ReadingClassName}}(
                     JsonConverter<T> wire,
                     JsonSerializerOptions options,
                     bool readMemberNames,
                     T? empty = null)
                 {
                     this.wire = wire ?? throw new ArgumentNullException(nameof(wire));
                     this.empty = empty;

                     var names = Enum.GetNames<T>();
                     var values = Enum.GetValues<T>();
                     var texts = new System.Collections.Generic.List<(string Text, T Value)>();
                     for (var i = 0; i < values.Length; i++)
                     {
                         texts.Add((IsEmpty(values[i]) ? string.Empty : ValueOf(values[i], options), values[i]));
                     }

                     if (readMemberNames)
                     {
                         for (var i = 0; i < values.Length; i++)
                         {
                             texts.Add((names[i], values[i]));
                         }
                     }

                     // The values before the member names: a name another member's value is names that member
                     foreach (var (text, value) in texts)
                     {
                         exact.TryAdd(text, value);

                         // A text two members share in another casing is no member's
                         ignoringCase[text] = ignoringCase.TryGetValue(text, out var other) && (other is null || !other.Value.Equals(value))
                             ? null
                             : value;
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
                 {
                     if (IsEmpty(value))
                     {
                         writer.WriteStringValue(string.Empty);
                         return;
                     }

                     wire.Write(writer, value, options);
                 }

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
                 {
                     if (IsEmpty(value))
                     {
                         writer.WritePropertyName(string.Empty);
                         return;
                     }

                     wire.WriteAsPropertyName(writer, value, options);
                 }

                 private bool IsEmpty(T value)
                     => empty is { } member && System.Collections.Generic.EqualityComparer<T>.Default.Equals(member, value);

                 private T Find(string text)
                     => exact.TryGetValue(text, out var value)
                         ? value
                         : ignoringCase.TryGetValue(text, out var named) && named is { } member
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