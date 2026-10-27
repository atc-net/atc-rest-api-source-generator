namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// Reads an enum setting of a marker file from any of its spellings, ignoring case, and writes it with its name. A
/// value it does not know (or a JSON value that is not a string) is read as the fallback, never an exception: an
/// exception would throw away every other setting of the marker file. <see cref="MarkerConfigInspector.Read{T}"/>
/// then gives the setting its default and reports the unknown value as a diagnostic.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public abstract class MarkerValueConverter<TEnum> : JsonConverter<TEnum>, IMarkerSettingValues
    where TEnum : struct, Enum
{
    private readonly TEnum fallback;
    private readonly Dictionary<string, TEnum> valuesBySpelling = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<TEnum, string> nameByValue = [];
    private readonly List<string> names = [];
    private readonly List<string> spellings = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkerValueConverter{TEnum}"/> class.
    /// </summary>
    /// <param name="fallback">The value an unknown value is read as.</param>
    /// <param name="values">Every value with its spellings; the first spelling is the name it is written with. The
    /// PascalCase member name is always accepted.</param>
    protected MarkerValueConverter(
        TEnum fallback,
        params KeyValuePair<TEnum, string[]>[] values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        this.fallback = fallback;
        foreach (var value in values)
        {
            if (!nameByValue.ContainsKey(value.Key))
            {
                nameByValue[value.Key] = value.Value[0];
                names.Add(value.Value[0]);
            }

            foreach (var spelling in value.Value.Append(value.Key.ToString()))
            {
                if (spellings.Contains(spelling, StringComparer.Ordinal))
                {
                    continue;
                }

                spellings.Add(spelling);
                valuesBySpelling[spelling] = value.Key;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Names => names;

    /// <inheritdoc />
    public IReadOnlyList<string> Spellings => spellings;

    /// <inheritdoc />
    public virtual bool AcceptsBooleans => false;

    /// <inheritdoc />
    public string NameOf(object? value)
        => value is TEnum known && nameByValue.TryGetValue(known, out var name)
            ? name
            : nameByValue[fallback];

    /// <inheritdoc />
    public bool Accepts(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => valuesBySpelling.ContainsKey(value.GetString()!),
            JsonValueKind.True or JsonValueKind.False => AcceptsBooleans,
            _ => false,
        };

    /// <inheritdoc />
    public override TEnum Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False && AcceptsBooleans)
        {
            return ReadBoolean(reader.TokenType == JsonTokenType.True);
        }

        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }

        return reader.TokenType == JsonTokenType.String &&
               reader.GetString() is { } value &&
               valuesBySpelling.TryGetValue(value, out var known)
            ? known
            : fallback;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        TEnum value,
        JsonSerializerOptions options)
    {
        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        writer.WriteStringValue(nameByValue.TryGetValue(value, out var name) ? name : nameByValue[fallback]);
    }

    /// <summary>
    /// A value with its spellings, for the constructor.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="spellings">The spellings; the first is the name it is written with.</param>
    /// <returns>The value with its spellings.</returns>
    protected static KeyValuePair<TEnum, string[]> Value(
        TEnum value,
        params string[] spellings)
        => new(value, spellings);

    /// <summary>
    /// Reads a JSON boolean, when <see cref="AcceptsBooleans"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="value">The boolean.</param>
    /// <returns>The value.</returns>
    protected virtual TEnum ReadBoolean(bool value)
        => fallback;
}