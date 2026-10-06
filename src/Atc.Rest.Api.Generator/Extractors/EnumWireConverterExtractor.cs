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
    /// The source of the factory, in the given namespace.
    /// </summary>
    /// <param name="namespaceName">The namespace of the generated file.</param>
    /// <returns>The file content.</returns>
    public static string Generate(string namespaceName)
        => $$"""
             {{HeaderBuilder.WithUsings(
                 NamespaceConstants.System,
                 NamespaceConstants.SystemCodeDomCompiler,
                 "System.Reflection",
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
                 private static readonly JsonStringEnumConverter StringValuesOnly = new(namingPolicy: null, allowIntegerValues: false);

                 /// <inheritdoc />
                 public override bool CanConvert(Type typeToConvert)
                     => typeToConvert.IsEnum &&
                        typeToConvert.GetCustomAttribute<GeneratedCodeAttribute>() is { Tool: "{{GeneratorInfo.Name}}" };

                 /// <inheritdoc />
                 public override JsonConverter? CreateConverter(
                     Type typeToConvert,
                     JsonSerializerOptions options)
                     => typeToConvert.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType is { IsGenericType: true } numberConverter &&
                        numberConverter.GetGenericTypeDefinition() == typeof(JsonNumberEnumConverter<>)
                         ? ((JsonConverterFactory)Activator.CreateInstance(numberConverter)!).CreateConverter(typeToConvert, options)
                         : StringValuesOnly.CreateConverter(typeToConvert, options);
             }
             """;

    /// <summary>
    /// Whether the document has an enum the generator emits as a C# enum: one in a schema reachable from the
    /// components, the paths or the webhooks.
    /// </summary>
    /// <param name="openApiDoc">The specification.</param>
    /// <returns>True when there is at least one enum.</returns>
    public static bool HasEnums(OpenApiDocument openApiDoc)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        var visited = new HashSet<IOpenApiSchema>();
        var schemas = new Stack<IOpenApiSchema>();

        foreach (var schema in openApiDoc.Components?.Schemas?.Values ?? [])
        {
            Push(schema);
        }

        // A document as the reader leaves it may hold null entries (a webhook that is no object): skipped
        var pathItems = (openApiDoc.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
            .Concat(openApiDoc.Webhooks?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
            .Where(pathItem => pathItem is not null);
        foreach (var pathItem in pathItems)
        {
            foreach (var parameter in pathItem.Parameters ?? [])
            {
                PushParameter(parameter);
            }

            foreach (var operation in pathItem.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                if (operation is null)
                {
                    continue;
                }

                foreach (var parameter in operation.Parameters ?? [])
                {
                    PushParameter(parameter);
                }

                PushContent(operation.RequestBody?.Content);
                foreach (var response in operation.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
                {
                    PushContent(response?.Content);
                }
            }
        }

        while (schemas.Count > 0)
        {
            var schema = schemas.Pop();
            if (!visited.Add(schema))
            {
                continue;
            }

            if (schema.Enum is { Count: > 0 } values &&
                values.Any(v => v is not null && !JsonNullSentinel.IsJsonNullSentinel(v)))
            {
                return true;
            }

            foreach (var property in schema.Properties?.Values ?? [])
            {
                Push(property);
            }

            Push(schema.Items);
            Push(schema.AdditionalProperties);
            foreach (var part in (schema.AllOf ?? []).Concat(schema.OneOf ?? []).Concat(schema.AnyOf ?? []))
            {
                Push(part);
            }
        }

        return false;

        void PushParameter(IOpenApiParameter? parameter)
        {
            Push(parameter?.Schema);
            PushContent(parameter?.Content);
        }

        void PushContent(IDictionary<string, IOpenApiMediaType>? content)
        {
            foreach (var mediaType in content?.Values ?? [])
            {
                Push(mediaType?.Schema);
            }
        }

        void Push(IOpenApiSchema? schema)
        {
            if (schema is not null)
            {
                schemas.Push(schema);
            }
        }
    }
}