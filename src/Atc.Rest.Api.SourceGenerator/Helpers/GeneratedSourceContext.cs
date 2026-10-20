namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Thin proxy over <see cref="SourceProductionContext"/> that applies
/// <see cref="GeneratedCodeAttributeHelper.ApplyExcludeFromCodeCoverage"/> to every emitted source
/// file, and <see cref="GeneratedCodeAttributeHelper.SuppressObsolete"/> for a specification with deprecated
/// members. Generator methods that only ever call <c>AddGeneratedSource</c> can take this type instead of
/// <see cref="SourceProductionContext"/> without any other change to their body.
/// <para>
/// It also notes every C# enum it emits (<see cref="Enums"/>), so a file that lists them - the
/// <c>EnumWireConverterFactory</c> - is written from what was generated, after it.
/// </para>
/// <para>
/// It names the <c>EnumWireConverterFactory</c> every string enum names in its <c>[JsonConverter]</c>
/// (<see cref="EnumWireConverter"/>), so the enum is read as the factory reads it - a value in any casing only one value
/// has, and with <c>readEnumMemberNames</c> the member name too - also in a host that builds its own JSON options.
/// </para>
/// </summary>
internal readonly struct GeneratedSourceContext(
    SourceProductionContext context,
    bool excludeFromCodeCoverage,
    bool suppressObsolete,
    string? enumWireConverter = null,
    bool extensibleEnums = false,
    bool readEnumMemberNames = false)
{
    private readonly List<GeneratedEnumDeclaration> enums = [];

    /// <summary>
    /// The fully qualified <c>EnumWireConverterFactory</c> a string enum names in its <c>[JsonConverter]</c>;
    /// <see langword="null"/> for a project that writes no enums.
    /// </summary>
    public string? EnumWireConverter => enumWireConverter;

    /// <summary>
    /// Whether a string enum is also read by its member name (<c>readEnumMemberNames</c>).
    /// </summary>
    public bool ReadEnumMemberNames => readEnumMemberNames;

    /// <summary>
    /// Whether a string enum is an extensible enum (the server marker's <c>extensibleEnums</c>; the clients pass it to
    /// the extractors themselves).
    /// </summary>
    public bool ExtensibleEnums => extensibleEnums;

    /// <summary>
    /// The enums emitted so far, in order.
    /// </summary>
    public IReadOnlyList<GeneratedEnumDeclaration> Enums => enums;

    public void AddGeneratedSource(
        string hintName,
        SourceText sourceText)
    {
        var content = GeneratedCodeAttributeHelper.ApplyExcludeFromCodeCoverage(sourceText.ToString(), excludeFromCodeCoverage);
        if (suppressObsolete)
        {
            content = GeneratedCodeAttributeHelper.SuppressObsolete(content);
        }

        enums.AddRange(GeneratedEnumDeclaration.FindIn(content));
        context.AddGeneratedSource(hintName, SourceText.From(content, sourceText.Encoding ?? Encoding.UTF8));
    }
}