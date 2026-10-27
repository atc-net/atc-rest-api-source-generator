namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Every generator names an operation's types the same way: the operationId when it is one, a name built from the
/// method and the path when it is missing or empty, and in both cases an identifier - a digit at the start or a
/// character that is no identifier would otherwise break the consumer's build.
/// </summary>
public class OperationIdHelperTests
{
    [Theory]
    [InlineData("listPets", "get", "/pets", "listPets")]
    [InlineData(null, "get", "/pets", "get_pets")]
    [InlineData("", "post", "/pets/{petId}/photos", "post_pets_petId_photos")]
    [InlineData("  ", "get", "/pets", "get_pets")]
    [InlineData("1stThing", "get", "/things", "Op1stThing")]
    [InlineData("repos/get", "get", "/repos", "repos_get")]
    [InlineData("get-user", "get", "/users", "get_user")]
    [InlineData("say \"hi\"", "get", "/hi", "say__hi_")]
    public void Effective_ReturnsExpected(
        string? operationId,
        string httpMethod,
        string path,
        string expected)
    {
        var operation = new OpenApiOperation { OperationId = operationId };

        Assert.Equal(expected, OperationIdHelper.Effective(operation, httpMethod, path));
    }
}