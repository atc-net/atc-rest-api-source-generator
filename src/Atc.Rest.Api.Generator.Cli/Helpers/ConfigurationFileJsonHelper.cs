namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Writes a configuration file (a marker file, the options file) with its JSON schema referenced first, so an editor
/// offers completion and flags a misspelled setting.
/// </summary>
public static class ConfigurationFileJsonHelper
{
    /// <summary>
    /// The JSON of a configuration, with <c>$schema</c> as its first property.
    /// </summary>
    /// <typeparam name="T">The configuration type.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="schemaReference">Where the JSON schema is published.</param>
    /// <param name="options">The serializer options.</param>
    /// <returns>The JSON.</returns>
    public static string Serialize<T>(
        T configuration,
        string schemaReference,
        JsonSerializerOptions options)
    {
        var properties = JsonSerializer.SerializeToNode(configuration, options)!.AsObject();
        var json = new JsonObject { [MarkerConfigInspector.SchemaProperty] = schemaReference };
        foreach (var name in properties.Select(p => p.Key).ToList())
        {
            var value = properties[name];
            properties.Remove(name);
            json[name] = value;
        }

        return json.ToJsonString(options);
    }
}