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

    /// <summary>
    /// Adds a generated file from its text, normalized for source output.
    /// </summary>
    /// <param name="hintName">The file's hint name.</param>
    /// <param name="content">The file's text.</param>
    public void AddGeneratedSource(
        string hintName,
        string content)
        => context.AddSource(hintName, SourceText.From(SourceProductionContextExtensions.Finish(Finish(content.NormalizeForSourceOutput())), Encoding.UTF8));

    /// <summary>
    /// Adds a generated file as it is.
    /// </summary>
    /// <param name="hintName">The file's hint name.</param>
    /// <param name="sourceText">The file's source.</param>
    public void AddGeneratedSource(
        string hintName,
        SourceText sourceText)
        => context.AddGeneratedSource(hintName, SourceText.From(Finish(sourceText.ToString()), sourceText.Encoding ?? Encoding.UTF8));

    // The project's finishing: [ExcludeFromCodeCoverage] when configured, the obsolete warnings suppressed for a
    // specification with deprecated members; the enums it declares are noted
    private string Finish(string content)
    {
        content = GeneratedCodeAttributeHelper.ApplyExcludeFromCodeCoverage(content, excludeFromCodeCoverage);
        if (suppressObsolete)
        {
            content = GeneratedCodeAttributeHelper.SuppressObsolete(content);
        }

        enums.AddRange(GeneratedEnumDeclaration.FindIn(content));
        return content;
    }
}