namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// The values a string setting of a marker file accepts (compared ignoring case). The first is the default, and what
/// any other value means.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MarkerValuesAttribute : Attribute, IMarkerSettingValues
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarkerValuesAttribute"/> class.
    /// </summary>
    /// <param name="values">The values; the first is the default.</param>
    public MarkerValuesAttribute(params string[] values)
        => Values = values;

    /// <summary>
    /// Gets the values; the first is the default.
    /// </summary>
    public IReadOnlyList<string> Values { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Names => Values;

    /// <inheritdoc />
    public IReadOnlyList<string> Spellings => Names;

    /// <inheritdoc />
    public bool AcceptsBooleans => false;

    /// <inheritdoc />
    public string FallbackName => Names[0];

    /// <inheritdoc />
    public bool Accepts(JsonElement value)
        => value.ValueKind == JsonValueKind.String &&
           Names.Contains(value.GetString(), StringComparer.OrdinalIgnoreCase);
}