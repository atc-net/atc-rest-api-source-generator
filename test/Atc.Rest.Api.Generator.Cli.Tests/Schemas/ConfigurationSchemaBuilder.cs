namespace Atc.Rest.Api.Generator.Cli.Tests.Schemas;

/// <summary>
/// Builds the JSON schema of a configuration file from its configuration class: a property per setting with the
/// summary of its XML documentation, its default (the value of a new instance) and, for a setting with a fixed set of
/// values, every spelling it accepts. Draft-07, which every editor's JSON support knows.
/// </summary>
internal static class ConfigurationSchemaBuilder
{
    private const string Draft = "http://json-schema.org/draft-07/schema#";

    private static readonly NullabilityInfoContext Nullability = new();
    private static readonly ConcurrentDictionary<Assembly, XDocument?> Documentation = new();

    /// <summary>
    /// The schema of a configuration file, as indented JSON.
    /// </summary>
    /// <param name="configType">The class the file is read into.</param>
    /// <param name="id">The URL the schema is published at.</param>
    /// <param name="title">The title.</param>
    /// <param name="description">What the file is.</param>
    /// <param name="valuesOf">The values a setting accepts, when they are a fixed set.</param>
    /// <param name="writeValue">The JSON value a default is written as.</param>
    /// <returns>The schema.</returns>
    public static string Build(
        Type configType,
        string id,
        string title,
        string description,
        Func<PropertyInfo, IReadOnlyList<JsonNode?>?> valuesOf,
        Func<PropertyInfo, object, JsonNode?> writeValue)
    {
        var schema = new JsonObject
        {
            ["$schema"] = Draft,
            ["$id"] = id,
            ["title"] = title,
            ["description"] = description,
        };

        foreach (var (key, value) in ObjectSchema(configType, valuesOf, writeValue, allowSchemaProperty: true))
        {
            schema[key] = value?.DeepClone();
        }

        return schema.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }).ReplaceLineEndings("\n") + "\n";
    }

    private static JsonObject ObjectSchema(
        Type type,
        Func<PropertyInfo, IReadOnlyList<JsonNode?>?> valuesOf,
        Func<PropertyInfo, object, JsonNode?> writeValue,
        bool allowSchemaProperty)
    {
        var defaults = Activator.CreateInstance(type);
        var properties = new JsonObject();
        if (allowSchemaProperty)
        {
            properties[MarkerConfigInspector.SchemaProperty] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The JSON schema of this file, for completion and validation in the editor.",
            };
        }

        // The settings every marker file has (BaseConfig) first
        foreach (var setting in MarkerConfigInspector.SettingsOf(type).OrderBy(p => DepthOf(p.DeclaringType!)))
        {
            var property = SettingSchema(setting, valuesOf, writeValue);
            if (Summary(setting) is { } summary)
            {
                property["description"] = summary;
            }

            if (setting.GetValue(defaults) is { } defaultValue &&
                (defaultValue is string or bool or int or long || defaultValue.GetType().IsEnum))
            {
                property["default"] = writeValue(setting, defaultValue);
            }

            properties[MarkerConfigInspector.JsonNameOf(setting)] = property;
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject SettingSchema(
        PropertyInfo setting,
        Func<PropertyInfo, IReadOnlyList<JsonNode?>?> valuesOf,
        Func<PropertyInfo, object, JsonNode?> writeValue)
    {
        if (valuesOf(setting) is { } values)
        {
            return new JsonObject { ["enum"] = new JsonArray(values.Select(v => v?.DeepClone()).ToArray()) };
        }

        var nullable = Nullability.Create(setting).WriteState == NullabilityState.Nullable;
        var type = Nullable.GetUnderlyingType(setting.PropertyType) ?? setting.PropertyType;
        if (MarkerConfigInspector.SectionTypeOf(setting, out var isDictionary) is { } sectionType ||
            IsOptionsSection(type, out sectionType))
        {
            var section = ObjectSchema(sectionType, valuesOf, writeValue, allowSchemaProperty: false);
            var schema = isDictionary
                ? new JsonObject { ["type"] = "object", ["additionalProperties"] = section }
                : section;
            return nullable ? OrNull(schema) : schema;
        }

        if (type == typeof(bool))
        {
            return new JsonObject { ["type"] = "boolean" };
        }

        if (type == typeof(int) || type == typeof(long))
        {
            return new JsonObject { ["type"] = "integer" };
        }

        if (type == typeof(string))
        {
            return new JsonObject { ["type"] = nullable ? new JsonArray("string", "null") : "string" };
        }

        if (type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            return new JsonObject
            {
                ["type"] = nullable ? new JsonArray("array", "null") : "array",
                ["items"] = new JsonObject { ["type"] = "string" },
            };
        }

        throw new NotSupportedException($"{setting.DeclaringType?.Name}.{setting.Name}: {setting.PropertyType} has no schema mapping.");
    }

    private static int DepthOf(Type type)
        => type.BaseType is null ? 0 : 1 + DepthOf(type.BaseType);

    private static bool IsOptionsSection(
        Type type,
        out Type sectionType)
    {
        sectionType = type;
        return type.IsClass &&
               type != typeof(string) &&
               type.Namespace == typeof(ApiGeneratorOptions).Namespace;
    }

    private static JsonObject OrNull(JsonObject schema)
        => new() { ["anyOf"] = new JsonArray(schema, new JsonObject { ["type"] = "null" }) };

    private static string? Summary(PropertyInfo setting)
    {
        var declaringType = setting.DeclaringType!;
        var documentation = Documentation.GetOrAdd(declaringType.Assembly, LoadDocumentation);
        var member = documentation?
            .Descendants("member")
            .FirstOrDefault(m => (string?)m.Attribute("name") == $"P:{declaringType.FullName}.{setting.Name}");
        if (member?.Element("summary") is not { } summary)
        {
            return null;
        }

        var text = string.Concat(summary.Nodes().Select(TextOf));
        return Regex.Replace(text, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
    }

    private static string TextOf(XNode node)
        => node switch
        {
            XText text => text.Value,
            XElement { Name.LocalName: "see" or "seealso" } see =>
                (string?)see.Attribute("langword") ??
                LastSegment((string?)see.Attribute("cref")) ??
                see.Value,
            XElement { Name.LocalName: "paramref" or "typeparamref" } reference => (string?)reference.Attribute("name") ?? string.Empty,
            XElement { Name.LocalName: "para" or "br" } element => " " + string.Concat(element.Nodes().Select(TextOf)) + " ",
            XElement element => string.Concat(element.Nodes().Select(TextOf)),
            _ => string.Empty,
        };

    private static string? LastSegment(string? cref)
    {
        if (cref is null)
        {
            return null;
        }

        var segments = cref.Split('.', ':');
        return segments[^1];
    }

    private static XDocument? LoadDocumentation(Assembly assembly)
    {
        var path = Path.ChangeExtension(assembly.Location, ".xml");
        return File.Exists(path) ? XDocument.Load(path) : null;
    }
}