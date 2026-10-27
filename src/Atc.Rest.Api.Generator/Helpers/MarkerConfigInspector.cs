namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Finds what a marker file sets that the generator does not use. A marker file is read leniently - a property the
/// configuration class does not have is ignored, a value a setting does not know gives the setting its default - so a typo
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

        return Inspect(json, configType, config: null);
    }

    /// <summary>
    /// Reads a marker file leniently: a setting the configuration class does not have is ignored, and a value a
    /// setting does not know gives the setting its default.
    /// </summary>
    /// <typeparam name="T">The configuration class the marker file is read into.</typeparam>
    /// <param name="json">The content of the marker file.</param>
    /// <param name="problems">One sentence per ignored setting or unknown value.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="JsonException">The content is not JSON, or a setting has a value of the wrong type.</exception>
    public static T Read<T>(
        string json,
        out IReadOnlyList<string> problems)
        where T : class, new()
    {
        var config = JsonSerializer.Deserialize<T>(json, JsonHelper.ConfigOptions) ?? new T();
        problems = Inspect(json, typeof(T), config);

        return config;
    }

    /// <summary>
    /// What went wrong reading a marker file, for the person who wrote it: a value of the wrong kind names the
    /// setting and the kind expected (<c>'generate' has a value of the wrong kind: true or false is expected</c>);
    /// anything else is the reader's message without its position suffix.
    /// </summary>
    /// <param name="exception">The exception the read threw.</param>
    /// <param name="configType">The configuration class the marker file is read into.</param>
    /// <returns>One sentence.</returns>
    public static string DescribeReadError(
        Exception exception,
        Type configType)
    {
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        if (configType is null)
        {
            throw new ArgumentNullException(nameof(configType));
        }

        if (exception is JsonException { Path: { Length: > 2 } path } && path.StartsWith("$.", StringComparison.Ordinal) &&
            SettingAt(configType, path.Substring(2)) is { } setting)
        {
            return $"'{path.Substring(2)}' has a value of the wrong kind: {KindOf(setting.PropertyType)} is expected";
        }

        // "... Path: $.x | LineNumber: 0 | BytePositionInLine: 12." says where, which the diagnostic does better
        var message = exception.Message;
        var position = message.IndexOf(" Path: ", StringComparison.Ordinal);
        return (position > 0 ? message.Substring(0, position) : message).TrimEnd('.', ' ');
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

    // The setting a JSON path ("healthChecks.enabled", "overlays[0]") names, through the sections
    private static PropertyInfo? SettingAt(
        Type configType,
        string path)
    {
        PropertyInfo? setting = null;
        var type = configType;
        foreach (var segment in path.Split('.'))
        {
            var name = segment.Split('[')[0];
            setting = SettingsOf(type).FirstOrDefault(p => string.Equals(JsonNameOf(p), name, StringComparison.OrdinalIgnoreCase));
            if (setting is null)
            {
                return null;
            }

            type = SectionTypeOf(setting, out _) ?? setting.PropertyType;
        }

        return setting;
    }

    // A map of strings (ruleSeverities): its keys are rule ids, each value one of the setting's values
    private static bool IsStringMap(PropertyInfo setting)
        => setting.PropertyType == typeof(Dictionary<string, string>);

    // A rule the generator does not have, or a severity it does not know, is named and left out: the rule keeps its
    // severity
    private static void InspectRuleSeverities(
        JsonElement value,
        PropertyInfo setting,
        object? config,
        string settingPath,
        List<string> problems)
    {
        if (value.ValueKind != JsonValueKind.Object || ValuesOf(setting) is not { } values)
        {
            return;
        }

        var entries = config is null ? null : setting.GetValue(config) as Dictionary<string, string>;
        foreach (var entry in value.EnumerateObject())
        {
            var problem = !Validators.RuleSeverityOverrides.IsRule(entry.Name)
                ? $"'{settingPath}.{entry.Name}' is not a rule of the generator and is ignored"
                : !values.Accepts(entry.Value)
                    ? $"'{settingPath}.{entry.Name}' has the unknown value {entry.Value.GetRawText()} (expected {DescribeValues(values)}), so the rule keeps its severity"
                    : null;
            if (problem is not null)
            {
                problems.Add(problem);
                entries?.Remove(entry.Name);
            }
        }
    }

    private static string KindOf(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(bool))
        {
            return "true or false";
        }

        if (underlying == typeof(string) || underlying.IsEnum)
        {
            return "a string";
        }

        if (underlying.IsPrimitive || underlying == typeof(decimal))
        {
            return "a number";
        }

        if (typeof(IEnumerable).IsAssignableFrom(underlying) && !typeof(IDictionary).IsAssignableFrom(underlying))
        {
            return "an array";
        }

        return "an object";
    }

    private static IReadOnlyList<string> Inspect(
        string json,
        Type configType,
        object? config)
    {
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
                Inspect(document.RootElement, configType, config, path: null, problems);
            }

            return problems;
        }
    }

    /// <param name="element">The JSON object of the section.</param>
    /// <param name="configType">The configuration class of the section.</param>
    /// <param name="config">The section as read, whose unknown values are set to their default; or
    /// <see langword="null"/> to only find the problems.</param>
    /// <param name="path">The path of the section.</param>
    /// <param name="problems">The problems found.</param>
    private static void Inspect(
        JsonElement element,
        Type configType,
        object? config,
        string? path,
        List<string> problems)
    {
        var settings = SettingsOf(configType);
        var defaults = Activator.CreateInstance(configType);
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
            if (IsStringMap(setting))
            {
                InspectRuleSeverities(value, setting, config, settingPath, problems);
                continue;
            }

            if (ValuesOf(setting) is { } values)
            {
                if (!values.Accepts(value))
                {
                    var defaultValue = setting.GetValue(defaults);
                    if (config is not null)
                    {
                        setting.SetValue(config, defaultValue);
                    }

                    problems.Add($"'{settingPath}' has the unknown value {value.GetRawText()} (expected {DescribeValues(values)}), so {values.NameOf(defaultValue)} is used");
                }

                continue;
            }

            if (SectionTypeOf(setting, out var isDictionary) is not { } sectionType ||
                value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var section = config is null ? null : setting.GetValue(config);
            if (!isDictionary)
            {
                Inspect(value, sectionType, section, settingPath, problems);
                continue;
            }

            var entries = section as IDictionary;
            foreach (var entry in value.EnumerateObject().Where(e => e.Value.ValueKind == JsonValueKind.Object))
            {
                var entrySection = entries is not null && entries.Contains(entry.Name) ? entries[entry.Name] : null;
                Inspect(entry.Value, sectionType, entrySection, $"{settingPath}.{entry.Name}", problems);
            }
        }
    }
}