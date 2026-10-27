namespace Atc.Rest.Api.Generator.Tests.Services;

/// <summary>
/// The handler each operation of a domain project gets - names, namespaces, files, a user's handler before a leftover
/// stub, the signature - planned without a compilation.
/// </summary>
public sealed class HandlerScaffoldPlannerTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /pets/{petId}:
            parameters:
              - { name: petId, in: path, required: true, schema: { type: string } }
            get:
              operationId: getPet
              tags: [pet store]
              responses:
                '200': { description: OK }
          /owners:
            get:
              operationId: listOwners
              responses:
                '200': { description: OK }
            delete:
              operationId: deleteOwners
              deprecated: true
              responses:
                '204': { description: OK }
        """;

    private static readonly Dictionary<string, List<string>> NoHandlers = new(StringComparer.Ordinal);

    [Theory]
    [InlineData(SubFolderStrategyType.None, "Contoso.Domain.ApiHandlers", "GetPetHandler.cs")]
    [InlineData(SubFolderStrategyType.FirstPathSegment, "Contoso.Domain.ApiHandlers.Pets", "Pets/GetPetHandler.cs")]
    [InlineData(SubFolderStrategyType.OpenApiTag, "Contoso.Domain.ApiHandlers.PetStore", "PetStore/GetPetHandler.cs")]
    public void Plan_AStub_IsInTheNamespaceAndFolderOfTheSubFolderStrategy(
        SubFolderStrategyType strategy,
        string expectedNamespace,
        string expectedFile)
    {
        var plan = PlanOf("GetPetHandler", new ServerDomainConfig { SubFolderStrategy = strategy }, NoHandlers);

        Assert.Equal(expectedNamespace, plan.ScaffoldNamespace);
        Assert.Equal(expectedNamespace, plan.Namespace);
        Assert.False(plan.IsImplemented);
        Assert.Equal(Path.Combine("domain", "ApiHandlers", expectedFile.Replace('/', Path.DirectorySeparatorChar)), plan.FilePath);
    }

    [Fact]
    public void Plan_AUserHandlerBesideALeftoverStub_IsRegistered_AndShadowsTheStub()
    {
        var implemented = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["ListOwnersHandler"] = ["Contoso.Domain.ApiHandlers", "Contoso.Domain.Owners"],
        };

        var plan = PlanOf("ListOwnersHandler", new ServerDomainConfig(), implemented);

        Assert.True(plan.IsImplemented);
        Assert.True(plan.ShadowsStub);
        Assert.Equal("Contoso.Domain.Owners", plan.Namespace);
        Assert.Equal("Contoso.Domain.ApiHandlers", plan.ScaffoldNamespace);
    }

    [Fact]
    public void Plan_AUserHandlerAlone_IsRegistered_WithoutShadowingAStub()
    {
        var implemented = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["ListOwnersHandler"] = ["Contoso.Domain.Owners"],
        };

        var plan = PlanOf("ListOwnersHandler", new ServerDomainConfig(), implemented);

        Assert.True(plan.IsImplemented);
        Assert.False(plan.ShadowsStub);
        Assert.Equal("Contoso.Domain.Owners", plan.Namespace);
    }

    [Theory]
    [InlineData(false, "GetPetHandler,ListOwnersHandler")]
    [InlineData(true, "GetPetHandler,ListOwnersHandler,DeleteOwnersHandler")]
    public void Plan_ADeprecatedOperation_HasAHandlerOnlyWhenIncluded(
        bool includeDeprecated,
        string expected)
        => Assert.Equal(
            expected,
            string.Join(",", Plans(new ServerDomainConfig { IncludeDeprecated = includeDeprecated }, NoHandlers).Select(p => p.HandlerName)));

    [Fact]
    public void Plan_TheSignature_TakesTheParametersOfAPathLevelParameter_AndNoneWithout()
    {
        var getPet = PlanOf("GetPetHandler", new ServerDomainConfig(), NoHandlers);
        var listOwners = PlanOf("ListOwnersHandler", new ServerDomainConfig(), NoHandlers);

        Assert.Contains("GetPetParameters parameters,", getPet.ExpectedSignature, StringComparison.Ordinal);
        Assert.DoesNotContain("Parameters parameters", listOwners.ExpectedSignature, StringComparison.Ordinal);
        Assert.StartsWith("public Task<ListOwnersResult> ExecuteAsync(", listOwners.ExpectedSignature, StringComparison.Ordinal);
    }

    private static HandlerPlan PlanOf(
        string handlerName,
        ServerDomainConfig config,
        Dictionary<string, List<string>> implemented)
        => Assert.Single(Plans(config, implemented), p => p.HandlerName == handlerName);

    private static List<HandlerPlan> Plans(
        ServerDomainConfig config,
        Dictionary<string, List<string>> implemented)
        => HandlerScaffoldPlanner.Plan(
            OpenApiDocumentHelper.ParseYaml(Yaml),
            config,
            "Contoso.Domain",
            "domain",
            implemented);
}