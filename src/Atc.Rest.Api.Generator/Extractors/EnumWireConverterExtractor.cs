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
    /// The source of the factory, in the given namespace, for the enums the project generates: each with its generic
    /// converter, so the factory works in a trimmed or Native AOT build.
    /// </summary>
    /// <param name="namespaceName">The namespace of the generated file.</param>
    /// <param name="enums">The generated enums.</param>
    /// <returns>The file content.</returns>
    public static string Generate(
        string namespaceName,
        IReadOnlyList<GeneratedEnumDeclaration> enums)
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
            createConverter
                .AppendLine($"        if (typeToConvert == typeof({e.FullName}))")
                .AppendLine("        {")
                .AppendLine($"            return {converter}.CreateConverter(typeToConvert, options);")
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
                 }
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
}