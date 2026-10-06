namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Parameters that share a name across locations (<c>id</c> in the path, <c>id</c> in the query, <c>Id</c> in a
/// header) each get their own property: the path keeps the bare name, the others carry their location. Every
/// parameter is on the wire, so none may be dropped.
/// </summary>
public class ParameterPropertyNamesTests
{
    [Fact]
    public void For_TheSameNameInThreeLocations_GivesThreeProperties()
    {
        var operation = Operation(
            Parameter("id", ParameterLocation.Path),
            Parameter("id", ParameterLocation.Query),
            Parameter("Id", ParameterLocation.Header));

        var names = ParameterPropertyNames.For(operation, pathParameters: null);

        Assert.Equal("Id", names.Get(operation.Parameters![0], referenceId: null));
        Assert.Equal("IdQuery", names.Get(operation.Parameters![1], referenceId: null));
        Assert.Equal("IdHeader", names.Get(operation.Parameters![2], referenceId: null));
    }

    [Fact]
    public void For_TheSameNameTwiceInOneLocation_NumbersTheSecond()
    {
        var operation = Operation(
            Parameter("user_id", ParameterLocation.Query),
            Parameter("userId", ParameterLocation.Query));

        var names = ParameterPropertyNames.For(operation, pathParameters: null);

        Assert.Equal("UserId", names.Get(operation.Parameters![0], referenceId: null));
        Assert.Equal("UserId2", names.Get(operation.Parameters![1], referenceId: null));
    }

    [Fact]
    public void For_ThePathLevelParameterTheOperationOverrides_IsTheOperationsOne()
    {
        var pathLevel = new List<IOpenApiParameter> { Parameter("id", ParameterLocation.Path), Parameter("id", ParameterLocation.Query) };
        var operation = Operation(Parameter("id", ParameterLocation.Query));

        var names = ParameterPropertyNames.For(operation, pathLevel);

        Assert.Equal("Id", names.Get(pathLevel[0], referenceId: null));
        Assert.Equal("IdQuery", names.Get(operation.Parameters![0], referenceId: null));
    }

    [Fact]
    public void For_DistinctNames_KeepTheLocalRule()
    {
        var operation = Operation(
            Parameter("petId", ParameterLocation.Path),
            Parameter("x-correlation-id", ParameterLocation.Header));

        var names = ParameterPropertyNames.For(operation, pathParameters: null);

        Assert.Equal("PetId", names.Get(operation.Parameters![0], referenceId: null));
        Assert.Equal("CorrelationId", names.Get(operation.Parameters![1], referenceId: null));
    }

    [Fact]
    public void Local_AReferencedHeader_IsNamedAfterItsComponent()
        => Assert.Equal("Continuation", ParameterPropertyNames.Local("x-continuation", ParameterLocation.Header, "Continuation"));

    private static OpenApiOperation Operation(params IOpenApiParameter[] parameters)
        => new() { Parameters = [.. parameters] };

    private static OpenApiParameter Parameter(
        string name,
        ParameterLocation location)
        => new() { Name = name, In = location, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
}