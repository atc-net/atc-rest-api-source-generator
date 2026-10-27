namespace Atc.OpenApi.Tests.Models;

/// <summary>
/// The operation overrides its path item, the path item overrides the document - one rule for every x- setting.
/// </summary>
public sealed class OpenApiExtensionScopeTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Scope
          version: 1.0.0
        x-retry-max-attempts: 1
        x-retry-backoff: linear
        x-retry-use-jitter: false
        paths:
          /pets:
            x-retry-max-attempts: 2
            get:
              x-retry-max-attempts: 3
              responses:
                '204':
                  description: OK
          /owners:
            get:
              responses:
                '204':
                  description: OK
        """;

    [Fact]
    public void First_IsTheNearestLevelThatSetsTheValue()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var pets = document.Paths["/pets"];
        var owners = document.Paths["/owners"];

        var petsScope = new OpenApiExtensionScope(pets.Operations![HttpMethod.Get].Extensions, pets.Extensions, document.Extensions);
        var ownersScope = new OpenApiExtensionScope(owners.Operations![HttpMethod.Get].Extensions, owners.Extensions, document.Extensions);

        Assert.Equal(3, petsScope.First(e => e.ReadInt("x-retry-max-attempts")));
        Assert.Equal(1, ownersScope.First(e => e.ReadInt("x-retry-max-attempts")));
        Assert.Equal("linear", petsScope.First(e => e.ReadString("x-retry-backoff")));
        Assert.False(petsScope.First(e => e.ReadBool("x-retry-use-jitter")));
        Assert.Null(petsScope.First(e => e.ReadString("x-retry-policy")));
    }
}