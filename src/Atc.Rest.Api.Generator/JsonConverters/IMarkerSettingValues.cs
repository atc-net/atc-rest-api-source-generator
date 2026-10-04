namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// The values a marker file setting accepts - one source for the converter that reads the setting, the check that
/// reports an unknown value and the JSON schema of the marker files.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Values of a setting.")]
public interface IMarkerSettingValues
{
    /// <summary>
    /// Gets the value names, one per value: the spelling a marker file is written with.
    /// </summary>
    IReadOnlyList<string> Names { get; }

    /// <summary>
    /// Gets every spelling the setting accepts (compared ignoring case): the names, the PascalCase and kebab-case forms.
    /// </summary>
    IReadOnlyList<string> Spellings { get; }

    /// <summary>
    /// Gets a value indicating whether <c>true</c> and <c>false</c> are accepted as well.
    /// </summary>
    bool AcceptsBooleans { get; }

    /// <summary>
    /// Gets the name of the value an unknown value is read as.
    /// </summary>
    string FallbackName { get; }

    /// <summary>
    /// Whether the JSON value is one the setting knows.
    /// </summary>
    /// <param name="value">The JSON value of the setting.</param>
    /// <returns><see langword="true"/> for a known value.</returns>
    bool Accepts(JsonElement value);
}