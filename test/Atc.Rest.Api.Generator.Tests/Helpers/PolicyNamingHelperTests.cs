namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class PolicyNamingHelperTests
{
    [Theory]
    [InlineData("global", "Global")]
    [InlineData("create-user", "CreateUser")]
    [InlineData("read_only", "ReadOnly")]
    [InlineData("rate:limit", "RateLimit")]
    [InlineData("two words", "TwoWords")]
    [InlineData("mixed-of_all:the words", "MixedOfAllTheWords")]
    public void ToConstantName_SplitsSeparatorsAndPascalCases(
        string policyName,
        string expected)
    {
        // Act
        var result = PolicyNamingHelper.ToConstantName(policyName);

        // Assert
        Assert.Equal(expected, result);
    }
}