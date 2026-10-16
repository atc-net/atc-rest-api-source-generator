namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// The compiler does not warn on <c>[property: Obsolete]</c> of a positional record parameter in the same compilation
/// - and generated code always is - so a deprecated property is declared explicitly in the record's body, with its
/// attributes and documentation, initialized from the parameter.
/// </summary>
public sealed class RecordContentGeneratorTests
{
    private static readonly AttributeParameters Obsolete = ObsoleteAttributeHelper.Attribute;

    [Fact]
    public void Generate_DeprecatedProperty_IsDeclaredInTheBody_WithItsAttributesAndDoc()
    {
        var record = Record(
            "Pet",
            docs: new Dictionary<string, string>(StringComparer.Ordinal) { ["Name"] = "The name.", ["Nickname"] = "Use name." },
            Parameter("Name"),
            Parameter("Nickname", new AttributeParameters("JsonPropertyName", "\"nick\""), Obsolete));

        var content = RecordContentGenerator.Generate(new CodeDocumentationTagsGenerator(), Container(record));

        Assert.EndsWith(
            Lines(
                "public sealed record Pet(",
                "    string? Name = null,",
                "    string? Nickname = null)",
                "{",
                "    /// <summary>",
                "    /// Use name.",
                "    /// </summary>",
                "    [JsonPropertyName(\"nick\")]",
                "    [Obsolete(\"Deprecated in the OpenAPI specification.\")]",
                "    public string? Nickname { get; init; } = Nickname;",
                "}"),
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_DeprecatedProperty_OfARecordWithABase_KeepsTheBaseClause()
    {
        var record = Record("Dog", docs: null, Parameter("Name"), Parameter("Bark", Obsolete)) with
        {
            BaseTypeName = "Animal",
            BaseConstructorArguments = ["Name"],
        };

        var content = RecordContentGenerator.Generate(new CodeDocumentationTagsGenerator(), Container(record));

        Assert.EndsWith(
            Lines(
                "    string? Bark = null) : Animal(Name)",
                "{",
                "    [Obsolete(\"Deprecated in the OpenAPI specification.\")]",
                "    public string? Bark { get; init; } = Bark;",
                "}"),
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithoutDeprecatedProperties_IsTheLibrarysOutput()
    {
        var container = Container(
            Record("Pet", docs: null, Parameter("Name", new AttributeParameters("Required", null))),
            Record("Owner", docs: null, Parameter("Pattern", new AttributeParameters("RegularExpression", "\"^(a|b);$\""))));

        Assert.Equal(
            new GenerateContentForRecords(new CodeDocumentationTagsGenerator(), container).Generate(),
            RecordContentGenerator.Generate(new CodeDocumentationTagsGenerator(), container));
    }

    [Fact]
    public void Generate_SeveralRecords_GivesEachItsOwnBody()
    {
        var container = Container(
            Record("Pet", docs: null, Parameter("Pattern", new AttributeParameters("RegularExpression", "\"^(a|b);$\"")), Parameter("Tag", Obsolete)),
            Record("Owner", docs: null, Parameter("Nickname", Obsolete)));

        var content = RecordContentGenerator.Generate(new CodeDocumentationTagsGenerator(), container);

        Assert.Contains(Lines("    string? Tag = null)", "{", "    [Obsolete(\"Deprecated in the OpenAPI specification.\")]", "    public string? Tag { get; init; } = Tag;", "}"), content, StringComparison.Ordinal);
        Assert.EndsWith(Lines("    public string? Nickname { get; init; } = Nickname;", "}"), content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateParameterRecords_ObliviousParameter_SaysWhyAboveItsRegion()
    {
        // An optional string parameter is declared nullable-oblivious for the Request Delegate Generator (CS8639);
        // the generated record says so, once per region, so a reader of the output does not have to guess
        var record = Record("ListPetsParameters", docs: null, Parameter("Filter"), Parameter("Sort"));

        // The region lines are written with '\n'; the generator's consumer normalizes the whole file afterwards
        var content = RecordContentGenerator.GenerateParameterRecords(new CodeDocumentationTagsGenerator(), Container(record))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains(
            string.Join(
                "\n",
                "public sealed record ListPetsParameters(",
                "    // Nullable-oblivious for the Request Delegate Generator (CS8639); the nullable property is declared in the body",
                "#nullable disable annotations",
                "    string Filter = null,",
                "    string Sort = null)",
                "#nullable restore annotations"),
            content,
            StringComparison.Ordinal);
        Assert.Equal(1, content.Split("Nullable-oblivious").Length - 1);
        Assert.Contains("    public string? Filter { get; init; } = Filter;", content, StringComparison.Ordinal);
    }

    private static string Lines(params string[] lines)
        => string.Join(Environment.NewLine, lines);

    private static RecordsParameters Container(
        params RecordParameters[] records)
        => new(
            HeaderContent: null,
            Namespace: "Contoso.Generated.Models",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters: records);

    private static RecordParameters Record(
        string name,
        Dictionary<string, string>? docs,
        params ParameterBaseParameters[] parameters)
        => new(
            DocumentationTags: docs is null ? null : new CodeDocumentationTags($"Represents a {name}.", docs, remark: null, code: null, example: null, exceptions: null, @return: null),
            DeclarationModifier: DeclarationModifiers.PublicSealedRecord,
            Name: name,
            Parameters: parameters);

    private static ParameterBaseParameters Parameter(
        string name,
        params AttributeParameters[] attributes)
        => new(
            Attributes: attributes.Length > 0 ? attributes : null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "string",
            IsNullableType: true,
            IsReferenceType: true,
            Name: name,
            DefaultValue: "null");
}