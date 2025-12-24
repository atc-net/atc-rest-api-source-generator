namespace Atc.CodeGeneration.CSharp.Content.Factories;

public static class EnumValueParametersFactory
{
    public static EnumValueParameters Create(
        string name)
        => new(
            DocumentationTags: null,
            DescriptionAttribute: null,
            Name: name,
            EnumMemberValue: null,
            Value: null);

    public static EnumValueParameters Create(
        string name,
        int? value)
        => new(
            DocumentationTags: null,
            DescriptionAttribute: null,
            Name: name,
            EnumMemberValue: null,
            Value: value);

    public static EnumValueParameters Create(
        string name,
        string? enumMemberValue)
        => new(
            DocumentationTags: null,
            DescriptionAttribute: null,
            Name: name,
            EnumMemberValue: enumMemberValue,
            Value: null);

    public static EnumValueParameters Create(
        string name,
        string? enumMemberValue,
        int? value)
        => new(
            DocumentationTags: null,
            DescriptionAttribute: null,
            Name: name,
            EnumMemberValue: enumMemberValue,
            Value: value);
}