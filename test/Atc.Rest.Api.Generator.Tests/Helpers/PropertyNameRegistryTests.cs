namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class PropertyNameRegistryTests
{
    [Fact]
    public void Resolve_DistinctNames_ReturnsTheCandidates()
    {
        var registry = new PropertyNameRegistry();

        Assert.Equal("Name", registry.Resolve("name", "Name"));
        Assert.Equal("Age", registry.Resolve("age", "Age"));
    }

    [Fact]
    public void Resolve_SameKeyTwice_ReturnsNullTheSecondTime()
    {
        // An allOf composition can list the very same property twice; it is one member, not two
        var registry = new PropertyNameRegistry();

        Assert.Equal("Name", registry.Resolve("name", "Name"));
        Assert.Null(registry.Resolve("name", "Name"));
    }

    [Fact]
    public void Resolve_DifferentKeysWithTheSameCandidate_SuffixesTheLaterOnes()
    {
        // user_name and userName both PascalCase to UserName; the comparison ignores case, so Username collides too
        var registry = new PropertyNameRegistry();

        Assert.Equal("UserName", registry.Resolve("user_name", "UserName"));
        Assert.Equal("UserName2", registry.Resolve("userName", "UserName"));
        Assert.Equal("Username3", registry.Resolve("USERNAME", "Username"));
    }

    [Fact]
    public void Resolve_CandidateStartingWithADigit_PrefixesAnUnderscore()
    {
        var registry = new PropertyNameRegistry();

        Assert.Equal("_2Fa", registry.Resolve("2fa", "2Fa"));
    }

    [Fact]
    public void Resolve_EmptyCandidate_FallsBackToProperty()
    {
        // $id and $ref PascalCase to nothing; they are still members on the wire
        var registry = new PropertyNameRegistry();

        Assert.Equal("Property", registry.Resolve("$id", string.Empty));
        Assert.Equal("Property2", registry.Resolve("$ref", string.Empty));
    }
}