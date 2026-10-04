namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Finds what a marker file sets that the generator does not use. A marker file is read leniently - a property the
/// configuration class does not have is ignored, a value a setting does not know is read as its fallback - so a typo
/// would otherwise change the generated code without a word.
/// </summary>
public static class MarkerConfigInspector
{
    /// <summary>
    /// The property naming the JSON schema of the marker file, allowed in every marker file.
    /// </summary>
    public const string SchemaProperty = "$schema";

    /// <summary>
    /// The settings and values of a marker file the generator does not know. Content that is not a JSON object is
    /// left to the parse error.
    /// </summary>
    /// <param name="json">The content of the marker file.</param>
    /// <param name="configType">The configuration class the marker file is read into.</param>
    /// <returns>One sentence per problem.</returns>
    public static IReadOnlyList<string> FindProblems(
        string json,
        Type configType)
    {
        if (configType is null)
        {
            throw new ArgumentNullException(nameof(configType));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json ?? string.Empty);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            var problems = new List<string>();
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                Inspect(document.RootElement, configType, path: null, problems);
            }

            return problems;
        }
    }

    /// <summary>
    /// The values a setting accepts: those of its converter or of its <see cref="MarkerValuesAttribute"/>.
    /// </summary>
    /// <param name="property">The setting.</param>
    /// <returns>The values, or <see langword="null"/> for a setting without a fixed set of values.</returns>
    public static IMarkerSettingValues? ValuesOf(PropertyInfo property)
    {
        if (property is null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        if (property.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType is { } converterType &&
            typeof(IMarkerSettingValues).IsAssignableFrom(converterType))
        {
            return (IMarkerSettingValues)Activator.CreateInstance(converterType);
        }

        return property.GetCustomAttribute<MarkerValuesAttribute>();
    }

    /// <summary>
    /// The settings of a configuration class: its public properties with a setter.
    /// </summary>
    /// <param name="configType">The configuration class.</param>
    /// <returns>The settings.</returns>
    public static IReadOnlyList<PropertyInfo> SettingsOf(Type configType)
        => configType is null
            ? throw new ArgumentNullException(nameof(configType))
            : configType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod?.IsPublic == true)
                .ToList();

    /// <summary>
    /// The JSON name of a setting: its <see cref="JsonPropertyNameAttribute"/> or its name in camelCase.
    /// </summary>
    /// <param name="property">The setting.</param>
    /// <returns>The JSON name.</returns>
    public static string JsonNameOf(PropertyInfo property)
        => property is null
            ? throw new ArgumentNullException(nameof(property))
            : property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
              ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

    /// <summary>
    /// The configuration class a setting holds, when it is a nested section (or a dictionary of them).
    /// </summary>
    /// <param name="property">The setting.</param>
    /// <param name="isDictionary">Whether the setting is a dictionary whose values are the section.</param>
    /// <returns>The section type, or <see langword="null"/> for a plain setting.</returns>
    public static Type? SectionTypeOf(
        PropertyInfo property,
        out bool isDictionary)
    {
        if (property is null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        var type = property.PropertyType;
        isDictionary = false;
        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
            type.GetGenericArguments()[0] == typeof(string))
        {
            isDictionary = true;
            type = type.GetGenericArguments()[1];
        }

        return type.IsClass && type.Assembly == typeof(MarkerConfigInspector).Assembly
            ? type
            : null;
    }

    /// <summary>
    /// The sentence naming the accepted values of a setting: "a, b or c".
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The sentence.</returns>
    public static string DescribeValues(IMarkerSettingValues values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var names = values.AcceptsBooleans
            ? values.Names.Concat(["true", "false"]).ToList()
            : values.Names.ToList();

        return names.Count == 1
            ? names[0]
            : string.Join(", ", names.Take(names.Count - 1)) + " or " + names[names.Count - 1];
    }

    private static void Inspect(
        JsonElement element,
        Type configType,
        string? path,
        List<string> problems)
    {
        var settings = SettingsOf(configType);
        foreach (var jsonProperty in element.EnumerateObject())
        {
            var settingPath = path is null ? jsonProperty.Name : $"{path}.{jsonProperty.Name}";
            if (path is null && string.Equals(jsonProperty.Name, SchemaProperty, StringComparison.Ordinal))
            {
                continue;
            }

            var setting = settings.FirstOrDefault(p => string.Equals(JsonNameOf(p), jsonProperty.Name, StringComparison.OrdinalIgnoreCase));
            if (setting is null)
            {
                problems.Add($"'{settingPath}' is not a setting of this marker file and is ignored");
                continue;
            }

            var value = jsonProperty.Value;
            if (ValuesOf(setting) is { } values)
            {
                if (!values.Accepts(value))
                {
                    problems.Add($"'{settingPath}' has the unknown value {value.GetRawText()} (expected {DescribeValues(values)}), so {values.FallbackName} is used");
                }

                continue;
            }

            if (SectionTypeOf(setting, out var isDictionary) is not { } sectionType ||
                value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!isDictionary)
            {
                Inspect(value, sectionType, settingPath, problems);
                continue;
            }

            foreach (var entry in value.EnumerateObject().Where(e => e.Value.ValueKind == JsonValueKind.Object))
            {
                Inspect(entry.Value, sectionType, $"{settingPath}.{entry.Name}", problems);
            }
        }
    }
}