namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

public class ProjectNameHelperTests
{
    [Theory]
    [InlineData("PizzaPlanet.Api.Contracts", "PizzaPlanet")]
    [InlineData("PizzaPlanet.Api.Domain", "PizzaPlanet")]
    [InlineData("PizzaPlanet.Api", "PizzaPlanet")]
    [InlineData("PizzaPlanet.Contracts", "PizzaPlanet")]
    [InlineData("PizzaPlanet.Domain", "PizzaPlanet")]
    public void ProjectNameHelper_ExtractSolutionName_StripsKnownSuffix(
        string projectName,
        string expected)
    {
        // Act
        var result = ProjectNameHelper.ExtractSolutionName(projectName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("pizzaplanet.api.contracts", "pizzaplanet")]
    [InlineData("PIZZAPLANET.API", "PIZZAPLANET")]
    [InlineData("PizzaPlanet.API.CONTRACTS", "PizzaPlanet")]
    public void ProjectNameHelper_ExtractSolutionName_IsCaseInsensitive(
        string projectName,
        string expected)
    {
        // Act
        var result = ProjectNameHelper.ExtractSolutionName(projectName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("PizzaPlanet")]
    [InlineData("PizzaPlanet.Server")]
    [InlineData("PizzaPlanet.Tests")]
    public void ProjectNameHelper_ExtractSolutionName_ReturnsInputVerbatim_WhenNoKnownSuffix(
        string projectName)
    {
        // Act
        var result = ProjectNameHelper.ExtractSolutionName(projectName);

        // Assert
        Assert.Equal(projectName, result);
    }

    [Fact]
    public void ProjectNameHelper_ExtractSolutionName_StripsOnlyFirstMatchingSuffix()
    {
        // "Contracts" is a suffix too, but ".Api.Contracts" is tried first and wins.
        // This guards against accidentally stripping both.
        // Arrange
        const string projectName = "PizzaPlanet.Api.Contracts";

        // Act
        var result = ProjectNameHelper.ExtractSolutionName(projectName);

        // Assert
        Assert.Equal("PizzaPlanet", result);
    }

    [Fact]
    public void ProjectNameHelper_ExtractSolutionName_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = ProjectNameHelper.ExtractSolutionName(string.Empty);

        // Assert
        Assert.Equal(string.Empty, result);
    }
}