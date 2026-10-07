namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// The <c>EnumWireConverterFactory</c> names the enums it serves and their converters: no reflection over attributes,
/// no <c>Activator.CreateInstance</c> and no non-generic <c>JsonStringEnumConverter</c>, all of which break a trimmed
/// or Native AOT build.
/// </summary>
public class EnumWireConverterExtractorTests
{
    private static readonly GeneratedEnumDeclaration[] Enums =
    [
        new("global::Contoso.Generated.Models.Color", IsInteger: false),
        new("global::Contoso.Generated.Models.Level", IsInteger: true),
    ];

    [Fact]
    public void Generate_NamesEachEnumWithItsGenericConverter()
    {
        var content = EnumWireConverterExtractor.Generate("Contoso.Generated", Enums);

        Assert.Contains("typeToConvert == typeof(global::Contoso.Generated.Models.Color)", content, StringComparison.Ordinal);
        Assert.Contains("new JsonStringEnumConverter<global::Contoso.Generated.Models.Color>(namingPolicy: null, allowIntegerValues: false)", content, StringComparison.Ordinal);
        Assert.Contains("new JsonNumberEnumConverter<global::Contoso.Generated.Models.Level>()", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Activator.")]
    [InlineData("GetCustomAttribute")]
    [InlineData("new JsonStringEnumConverter(")]
    [InlineData("System.Reflection")]
    public void Generate_UsesNoReflection(string reflection)
        => Assert.DoesNotContain(reflection, EnumWireConverterExtractor.Generate("Contoso.Generated", Enums), StringComparison.Ordinal);
}