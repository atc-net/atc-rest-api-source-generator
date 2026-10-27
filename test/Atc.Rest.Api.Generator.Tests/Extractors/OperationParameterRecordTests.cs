namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// The pure parts of a parameters record: the server's binding attribute of each location, and the required
/// properties before the optional ones.
/// </summary>
public sealed class OperationParameterRecordTests
{
    [Theory]
    [InlineData(ParameterLocation.Query, "FromQuery", "Name = \"page-size\"")]
    [InlineData(ParameterLocation.Path, "FromRoute", "Name = \"page-size\"")]
    [InlineData(ParameterLocation.Header, "FromHeader", "Name = \"page-size\"")]
    public void BindingAttribute_IsTheLocationsSource_ByTheParametersName(
        ParameterLocation location,
        string expectedName,
        string expectedContent)
    {
        var attribute = OperationParameterExtractor.BindingAttribute(location, "page-size");

        Assert.NotNull(attribute);
        Assert.Equal(expectedName, attribute.Name);
        Assert.Equal(expectedContent, attribute.Content);
    }

    [Fact]
    public void BindingAttribute_ACookie_IsAHeaderTheWireBindingStepFills()
    {
        var attribute = OperationParameterExtractor.BindingAttribute(ParameterLocation.Cookie, "session");

        Assert.NotNull(attribute);
        Assert.Equal("FromHeader", attribute.Name);
        Assert.Contains("session", attribute.Content, StringComparison.Ordinal);
        Assert.NotEqual("Name = \"session\"", attribute.Content, StringComparer.Ordinal);
    }

    [Fact]
    public void OrderRequiredFirst_KeepsTheSpecificationsOrder_WithinEachGroup()
    {
        ParameterBaseParameters[] parameters =
        [
            Parameter("Sort", "null"),
            Parameter("Id", null),
            Parameter("Limit", "10"),
            Parameter("Name", null),
        ];

        var ordered = OperationParameterExtractor.OrderRequiredFirst(parameters);

        Assert.Equal(["Id", "Name", "Sort", "Limit"], ordered.Select(p => p.Name), StringComparer.Ordinal);
    }

    private static ParameterBaseParameters Parameter(
        string name,
        string? defaultValue)
        => new(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "string",
            IsNullableType: defaultValue is not null,
            IsReferenceType: true,
            Name: name,
            DefaultValue: defaultValue);
}